param(
    [Parameter(Mandatory = $true)]
    [string] $DownloadsDirectory,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$utf8 = [System.Text.UTF8Encoding]::new($false)
$wordNamespace = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
$relationshipNamespace = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'

function Read-ZipEntryXml {
    param(
        [System.IO.Compression.ZipArchive] $Archive,
        [string] $EntryName
    )

    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) {
        return $null
    }

    $stream = $entry.Open()
    $reader = [System.IO.StreamReader]::new($stream)
    try {
        return [xml] $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Escape-MarkdownCell {
    param([AllowEmptyString()][string] $Value)

    if ($null -eq $Value) {
        return ''
    }

    return $Value.Replace('\', '\\').Replace('|', '\|').Replace("`r", '').Replace("`n", '<br>')
}

function Get-WordNodeText {
    param(
        [System.Xml.XmlNode] $Node,
        [System.Xml.XmlNamespaceManager] $NamespaceManager
    )

    $builder = [System.Text.StringBuilder]::new()
    foreach ($part in $Node.SelectNodes('.//w:t|.//w:tab|.//w:br', $NamespaceManager)) {
        switch ($part.LocalName) {
            't' { [void] $builder.Append($part.InnerText) }
            'tab' { [void] $builder.Append('    ') }
            'br' { [void] $builder.Append('<br>') }
        }
    }

    return $builder.ToString().Trim()
}

function Convert-DocxArchiveToMarkdown {
    param(
        [System.IO.Compression.ZipArchive] $Archive,
        [string] $SourceName
    )

    $document = Read-ZipEntryXml -Archive $Archive -EntryName 'word/document.xml'
    if ($null -eq $document) {
        throw "The DOCX source '$SourceName' has no word/document.xml entry."
    }

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
    $namespaceManager.AddNamespace('w', $wordNamespace)
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("<!-- Converted from $SourceName. Source formatting is simplified; content order is preserved. -->")
    $lines.Add('')

    $body = $document.SelectSingleNode('//w:body', $namespaceManager)
    foreach ($node in $body.ChildNodes) {
        if ($node.LocalName -eq 'p') {
            $text = Get-WordNodeText -Node $node -NamespaceManager $namespaceManager
            if ([string]::IsNullOrWhiteSpace($text)) {
                continue
            }

            $styleNode = $node.SelectSingleNode('./w:pPr/w:pStyle', $namespaceManager)
            $style = if ($null -eq $styleNode) { '' } else { $styleNode.GetAttribute('val', $wordNamespace) }
            $numberingNode = $node.SelectSingleNode('./w:pPr/w:numPr', $namespaceManager)

            if ($style -eq 'Title') {
                $lines.Add("# $text")
            }
            elseif ($style -match '^Heading([1-6])$') {
                $level = [Math]::Min([int] $Matches[1] + 1, 6)
                $lines.Add(('#' * $level) + " $text")
            }
            elseif ($null -ne $numberingNode) {
                $lines.Add("- $text")
            }
            else {
                $lines.Add($text)
            }

            $lines.Add('')
            continue
        }

        if ($node.LocalName -ne 'tbl') {
            continue
        }

        $rows = [System.Collections.Generic.List[object]]::new()
        $maxColumns = 0
        foreach ($rowNode in $node.SelectNodes('./w:tr', $namespaceManager)) {
            $cells = [System.Collections.Generic.List[string]]::new()
            foreach ($cellNode in $rowNode.SelectNodes('./w:tc', $namespaceManager)) {
                $cells.Add((Escape-MarkdownCell (Get-WordNodeText -Node $cellNode -NamespaceManager $namespaceManager)))
            }
            $maxColumns = [Math]::Max($maxColumns, $cells.Count)
            $rows.Add($cells)
        }

        if ($rows.Count -eq 0 -or $maxColumns -eq 0) {
            continue
        }

        for ($rowIndex = 0; $rowIndex -lt $rows.Count; $rowIndex++) {
            $cells = [System.Collections.Generic.List[string]] $rows[$rowIndex]
            while ($cells.Count -lt $maxColumns) {
                $cells.Add('')
            }
            $lines.Add('| ' + ($cells -join ' | ') + ' |')
            if ($rowIndex -eq 0) {
                $lines.Add('| ' + ((1..$maxColumns | ForEach-Object { '---' }) -join ' | ') + ' |')
            }
        }
        $lines.Add('')
    }

    return ($lines -join "`n").TrimEnd() + "`n"
}

function Convert-DocxFile {
    param(
        [string] $InputPath,
        [string] $OutputPath
    )

    $fileStream = [System.IO.File]::Open(
        $InputPath,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::ReadWrite)
    $archive = [System.IO.Compression.ZipArchive]::new(
        $fileStream,
        [System.IO.Compression.ZipArchiveMode]::Read)
    try {
        $markdown = Convert-DocxArchiveToMarkdown -Archive $archive -SourceName ([System.IO.Path]::GetFileName($InputPath))
        [System.IO.File]::WriteAllText($OutputPath, $markdown, $utf8)
    }
    finally {
        $archive.Dispose()
        $fileStream.Dispose()
    }
}

function Get-ColumnNumber {
    param([string] $CellReference)

    $letters = [regex]::Match($CellReference, '^[A-Z]+').Value
    $number = 0
    foreach ($character in $letters.ToCharArray()) {
        $number = ($number * 26) + ([int] $character - [int] [char] 'A' + 1)
    }
    return $number
}

function Get-ColumnName {
    param([int] $ColumnNumber)

    $name = ''
    while ($ColumnNumber -gt 0) {
        $ColumnNumber--
        $name = [char] ([int] [char] 'A' + ($ColumnNumber % 26)) + $name
        $ColumnNumber = [Math]::Floor($ColumnNumber / 26)
    }
    return $name
}

function Test-IsDateFormat {
    param(
        [int] $FormatId,
        [string] $FormatCode
    )

    if ($FormatId -in 14..22 -or $FormatId -in 45..47) {
        return $true
    }

    if ([string]::IsNullOrWhiteSpace($FormatCode)) {
        return $false
    }

    $clean = [regex]::Replace($FormatCode, '"[^"]*"|\\.', '')
    return $clean -match '[dy]' -and $clean -match '[mdy]'
}

function Convert-XlsxArchiveToMarkdown {
    param(
        [System.IO.Compression.ZipArchive] $Archive,
        [string] $SourceName
    )

    $sharedStrings = [System.Collections.Generic.List[string]]::new()
    $sharedStringsXml = Read-ZipEntryXml -Archive $Archive -EntryName 'xl/sharedStrings.xml'
    if ($null -ne $sharedStringsXml) {
        foreach ($item in $sharedStringsXml.SelectNodes('//*[local-name()="si"]')) {
            $sharedStrings.Add((@($item.SelectNodes('.//*[local-name()="t"]') | ForEach-Object { $_.InnerText }) -join ''))
        }
    }

    $customFormats = @{}
    $cellFormats = [System.Collections.Generic.List[int]]::new()
    $stylesXml = Read-ZipEntryXml -Archive $Archive -EntryName 'xl/styles.xml'
    if ($null -ne $stylesXml) {
        foreach ($format in $stylesXml.SelectNodes('//*[local-name()="numFmts"]/*[local-name()="numFmt"]')) {
            $customFormats[[int] $format.numFmtId] = [string] $format.formatCode
        }
        foreach ($format in $stylesXml.SelectNodes('//*[local-name()="cellXfs"]/*[local-name()="xf"]')) {
            $cellFormats.Add([int] $format.numFmtId)
        }
    }

    $workbook = Read-ZipEntryXml -Archive $Archive -EntryName 'xl/workbook.xml'
    $relationships = Read-ZipEntryXml -Archive $Archive -EntryName 'xl/_rels/workbook.xml.rels'
    $relationshipTargets = @{}
    foreach ($relationship in $relationships.SelectNodes('//*[local-name()="Relationship"]')) {
        $relationshipTargets[[string] $relationship.Id] = [string] $relationship.Target
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("<!-- Converted from $SourceName. Each worksheet is represented as a coordinate-preserving Markdown table. -->")
    $lines.Add('')
    $lines.Add("# $([System.IO.Path]::GetFileNameWithoutExtension($SourceName))")
    $lines.Add('')

    foreach ($sheet in $workbook.SelectNodes('//*[local-name()="sheet"]')) {
        $sheetName = [string] $sheet.name
        $relationshipId = $sheet.GetAttribute('id', $relationshipNamespace)
        $target = $relationshipTargets[$relationshipId].Replace('\', '/')
        if ($target.StartsWith('/')) {
            $worksheetPath = $target.TrimStart('/')
        }
        elseif ($target.StartsWith('xl/')) {
            $worksheetPath = $target
        }
        else {
            $worksheetPath = 'xl/' + $target.TrimStart('.', '/')
        }

        $worksheet = Read-ZipEntryXml -Archive $Archive -EntryName $worksheetPath
        $renderedRows = [System.Collections.Generic.List[object]]::new()
        $maximumColumn = 0

        foreach ($row in $worksheet.SelectNodes('//*[local-name()="sheetData"]/*[local-name()="row"]')) {
            $values = @{}
            foreach ($cell in $row.SelectNodes('./*[local-name()="c"]')) {
                $reference = [string] $cell.r
                $column = Get-ColumnNumber -CellReference $reference
                $valueNode = $cell.SelectSingleNode('./*[local-name()="v"]')
                $inlineNode = $cell.SelectSingleNode('./*[local-name()="is"]')
                $value = ''

                if ($cell.t -eq 's' -and $null -ne $valueNode) {
                    $value = $sharedStrings[[int] $valueNode.InnerText]
                }
                elseif ($cell.t -eq 'inlineStr' -and $null -ne $inlineNode) {
                    $value = @($inlineNode.SelectNodes('.//*[local-name()="t"]') | ForEach-Object { $_.InnerText }) -join ''
                }
                elseif ($null -ne $valueNode) {
                    $value = $valueNode.InnerText
                    if ($cell.t -ne 'b' -and $cell.s -ne '' -and $value -match '^-?\d+(\.\d+)?$') {
                        $styleIndex = [int] $cell.s
                        if ($styleIndex -lt $cellFormats.Count) {
                            $formatId = $cellFormats[$styleIndex]
                            $formatCode = if ($customFormats.ContainsKey($formatId)) { $customFormats[$formatId] } else { '' }
                            if (Test-IsDateFormat -FormatId $formatId -FormatCode $formatCode) {
                                $value = [DateTime]::FromOADate([double] $value).ToString('yyyy-MM-dd')
                            }
                        }
                    }
                }

                if (-not [string]::IsNullOrEmpty($value)) {
                    $values[$column] = Escape-MarkdownCell $value
                    $maximumColumn = [Math]::Max($maximumColumn, $column)
                }
            }

            if ($values.Count -gt 0) {
                $renderedRows.Add([pscustomobject]@{
                    RowNumber = [int] $row.r
                    Values = $values
                })
            }
        }

        $lines.Add("## $sheetName")
        $lines.Add('')
        if ($renderedRows.Count -eq 0) {
            $lines.Add('_Empty worksheet._')
            $lines.Add('')
            continue
        }

        $headers = [System.Collections.Generic.List[string]]::new()
        $headers.Add('Row')
        for ($column = 1; $column -le $maximumColumn; $column++) {
            $headers.Add((Get-ColumnName $column))
        }
        $lines.Add('| ' + ($headers -join ' | ') + ' |')
        $lines.Add('| ' + ((1..$headers.Count | ForEach-Object { '---' }) -join ' | ') + ' |')

        foreach ($renderedRow in $renderedRows) {
            $cells = [System.Collections.Generic.List[string]]::new()
            $cells.Add([string] $renderedRow.RowNumber)
            for ($column = 1; $column -le $maximumColumn; $column++) {
                if ($renderedRow.Values.ContainsKey($column)) {
                    $cells.Add([string] $renderedRow.Values[$column])
                }
                else {
                    $cells.Add('')
                }
            }
            $lines.Add('| ' + ($cells -join ' | ') + ' |')
        }
        $lines.Add('')
    }

    return ($lines -join "`n").TrimEnd() + "`n"
}

function Open-NestedArchive {
    param(
        [System.IO.Compression.ZipArchive] $OuterArchive,
        [string] $EntryName
    )

    $entry = $OuterArchive.GetEntry($EntryName)
    if ($null -eq $entry) {
        throw "Archive entry '$EntryName' was not found."
    }

    $memoryStream = [System.IO.MemoryStream]::new()
    $entryStream = $entry.Open()
    try {
        $entryStream.CopyTo($memoryStream)
    }
    finally {
        $entryStream.Dispose()
    }
    $memoryStream.Position = 0

    return [pscustomobject]@{
        Stream = $memoryStream
        Archive = [System.IO.Compression.ZipArchive]::new(
            $memoryStream,
            [System.IO.Compression.ZipArchiveMode]::Read)
    }
}

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

Convert-DocxFile `
    -InputPath (Join-Path $DownloadsDirectory 'ECHO PROTO.docx') `
    -OutputPath (Join-Path $OutputDirectory 'ECHO_PROTO.md')
Convert-DocxFile `
    -InputPath ((Get-ChildItem -LiteralPath $DownloadsDirectory -Filter '*Backend.docx' | Select-Object -First 1).FullName) `
    -OutputPath (Join-Path $OutputDirectory 'CHOT_BACKEND.md')

$planningPackagePath = Join-Path $DownloadsDirectory 'ECHO_PROTOCOL_KLTN_Planning_Package_REVISED_2026-08-19.zip'
$outerArchive = [System.IO.Compression.ZipFile]::OpenRead($planningPackagePath)
try {
    $documents = @(
        @{ Input = '01_ECHO_PROTOCOL_Project_Scope_REVISED.docx'; Output = '01_ECHO_PROTOCOL_Project_Scope_REVISED.md'; Type = 'docx' },
        @{ Input = '02_ECHO_PROTOCOL_System_Architecture_REVISED.docx'; Output = '02_ECHO_PROTOCOL_System_Architecture_REVISED.md'; Type = 'docx' },
        @{ Input = '03_ECHO_PROTOCOL_Implementation_Spec_REVISED.xlsx'; Output = '03_ECHO_PROTOCOL_Implementation_Spec_REVISED.md'; Type = 'xlsx' },
        @{ Input = '04_ECHO_PROTOCOL_Project_Management_Baseline_REVISED.xlsx'; Output = '04_ECHO_PROTOCOL_Project_Management_Baseline_REVISED.md'; Type = 'xlsx' },
        @{ Input = '05_ECHO_PROTOCOL_Project_Plan_4P_2026_REVISED.xlsx'; Output = '05_ECHO_PROTOCOL_Project_Plan_4P_2026_REVISED.md'; Type = 'xlsx' }
    )

    foreach ($document in $documents) {
        $nested = Open-NestedArchive -OuterArchive $outerArchive -EntryName $document.Input
        try {
            if ($document.Type -eq 'docx') {
                $markdown = Convert-DocxArchiveToMarkdown -Archive $nested.Archive -SourceName $document.Input
            }
            else {
                $markdown = Convert-XlsxArchiveToMarkdown -Archive $nested.Archive -SourceName $document.Input
            }

            [System.IO.File]::WriteAllText(
                (Join-Path $OutputDirectory $document.Output),
                $markdown,
                $utf8)
        }
        finally {
            $nested.Archive.Dispose()
            $nested.Stream.Dispose()
        }
    }
}
finally {
    $outerArchive.Dispose()
}

Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.md' |
    Where-Object { $_.Name -in @(
        'ECHO_PROTO.md',
        'CHOT_BACKEND.md',
        '01_ECHO_PROTOCOL_Project_Scope_REVISED.md',
        '02_ECHO_PROTOCOL_System_Architecture_REVISED.md',
        '03_ECHO_PROTOCOL_Implementation_Spec_REVISED.md',
        '04_ECHO_PROTOCOL_Project_Management_Baseline_REVISED.md',
        '05_ECHO_PROTOCOL_Project_Plan_4P_2026_REVISED.md'
    ) } |
    Select-Object Name, Length
