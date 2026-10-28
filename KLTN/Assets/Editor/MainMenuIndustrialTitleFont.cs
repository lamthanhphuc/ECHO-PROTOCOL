using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Builds the original, angular display face used only for ECHO / PROTOCOL.
/// The atlas, material and font remain the same asset objects on subsequent runs.
/// </summary>
public static class MainMenuIndustrialTitleFont
{
    private const int AtlasWidth = 1024;
    private const int AtlasHeight = 512;
    private const int CellSize = 256;
    private const int CapHeight = 180;
    private const int Padding = 9;

    private static readonly char[] Letters = { 'E', 'C', 'H', 'O', 'P', 'R', 'T', 'L' };
    private static readonly int[] Advances = { 135, 148, 145, 150, 143, 153, 139, 133 };

    public static TMP_FontAsset Ensure(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !folder.StartsWith("Assets/", StringComparison.Ordinal))
            throw new ArgumentException("Expected an Assets/ folder.", nameof(folder));

        string path = folder.TrimEnd('/') + "/IndustrialTitleTMP.asset";
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Industrial title asset path is occupied: " + path);

            // TMP's factory initializes the private atlas metadata and its SDF material.
            // The imported font is only a bootstrap; all glyphs below are our own shapes.
            Font bootstrap = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Roboto-Black.ttf");
            if (bootstrap == null)
                throw new InvalidOperationException("Missing bundled font bootstrap at Assets/Fonts/Roboto-Black.ttf.");

            font = TMP_FontAsset.CreateFontAsset(bootstrap, CapHeight, Padding,
                GlyphRenderMode.SDFAA, AtlasWidth, AtlasHeight, AtlasPopulationMode.Static, false);
            if (font == null)
                throw new InvalidOperationException("TMP could not initialize the industrial title asset.");

            font.name = "IndustrialTitleTMP";
            font.atlasTexture.name = "IndustrialTitle Atlas";
            font.material.name = "IndustrialTitle Material";
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
        }

        Texture2D atlas = font.atlasTexture;
        Material material = font.material;
        if (atlas == null || material == null || !AssetDatabase.Contains(atlas) || !AssetDatabase.Contains(material))
            throw new InvalidOperationException("Industrial title asset is missing its original atlas or material subasset: " + path);

        // Reinitialize changes the same Texture2D object's pixels, preserving file IDs and
        // any material/scene references created by previous authoring runs.
        if (atlas.width != AtlasWidth || atlas.height != AtlasHeight)
        {
            if (!atlas.Reinitialize(AtlasWidth, AtlasHeight))
                throw new InvalidOperationException("Could not resize industrial title atlas.");
        }

        atlas.filterMode = FilterMode.Bilinear;
        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.SetPixelData(BuildAtlas(), 0);
        atlas.Apply(false, false);

        FaceInfo face = font.faceInfo;
        face.familyName = "ECHO Industrial";
        face.styleName = "Heavy";
        face.pointSize = CapHeight;
        face.scale = 1;
        face.lineHeight = 241; // 118 px between lines when the cap is 88 px.
        face.ascentLine = 190;
        face.capLine = CapHeight;
        face.meanLine = 108;
        face.baseline = 0;
        face.descentLine = -51;
        font.faceInfo = face;
        var serializedFont = new SerializedObject(font);
        SerializedProperty unitsPerEm = serializedFont.FindProperty("m_FaceInfo.m_UnitsPerEM");
        if (unitsPerEm != null)
        {
            unitsPerEm.intValue = CapHeight;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
        }
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.isMultiAtlasTexturesEnabled = false;

        font.glyphTable.Clear();
        font.characterTable.Clear();
        for (int i = 0; i < Letters.Length; i++)
        {
            int width = Advances[i] - 9;
            int x = (i % 4) * CellSize + 58;
            int y = (i / 4) * CellSize + 38;
            uint glyphIndex = (uint)(i + 1);
            var glyph = new Glyph(glyphIndex,
                new GlyphMetrics(width, CapHeight, 0, CapHeight, Advances[i]),
                new GlyphRect(x, y, width, CapHeight), 1, 0);
            font.glyphTable.Add(glyph);
            font.characterTable.Add(new TMP_Character(Letters[i], font, glyph));
        }

        var space = new Glyph(9, new GlyphMetrics(0, 0, 0, 0, 65), GlyphRect.zero, 1, 0);
        font.glyphTable.Add(space);
        font.characterTable.Add(new TMP_Character(' ', font, space));
        font.ReadFontAssetDefinition();

        material.SetTexture("_MainTex", atlas);
        material.SetFloat("_TextureWidth", AtlasWidth);
        material.SetFloat("_TextureHeight", AtlasHeight);
        material.SetFloat("_GradientScale", Padding + 1);

        EditorUtility.SetDirty(atlas);
        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }

    private static byte[] BuildAtlas()
    {
        var pixels = new byte[AtlasWidth * AtlasHeight];
        for (int i = 0; i < Letters.Length; i++)
        {
            int width = Advances[i] - 9;
            int originX = (i % 4) * CellSize + 58;
            int originY = (i / 4) * CellSize + 38;
            for (int py = -Padding; py < CapHeight + Padding; py++)
            {
                int atlasY = originY + py;
                for (int px = -Padding; px < width + Padding; px++)
                {
                    int atlasX = originX + px;
                    float d = SignedDistance(Letters[i], px + 0.5f, py + 0.5f, width);
                    float alpha = Mathf.Clamp01(0.5f - d / (2f * Padding));
                    pixels[atlasY * AtlasWidth + atlasX] = (byte)Mathf.RoundToInt(alpha * 255f);
                }
            }
        }
        return pixels;
    }

    private static float SignedDistance(char letter, float x, float y, float w)
    {
        const float h = CapHeight;
        const float stem = 34;
        float shape;
        switch (letter)
        {
            case 'E':
                shape = Union(Box(x, y, 0, 0, stem, h),
                    Box(x, y, 0, 146, w, 34),
                    Box(x, y, 0, 73, w - 13, 34),
                    Box(x, y, 0, 0, w, 34));
                break;
            case 'C':
                shape = Ring(x, y, w, h, 34);
                shape = Mathf.Max(shape, -Box(x, y, w - 54, 44, 80, 92));
                break;
            case 'H':
                shape = Union(Box(x, y, 0, 0, stem, h),
                    Box(x, y, w - stem, 0, stem, h),
                    Box(x, y, 0, 74, w, 34));
                break;
            case 'O':
                shape = Ring(x, y, w, h, 35);
                break;
            case 'P':
            case 'R':
                shape = Union(Box(x, y, 0, 0, stem, h),
                    Box(x, y, 0, 146, w, 34),
                    Box(x, y, 0, 76, w, 34),
                    Box(x, y, w - stem, 76, stem, 104));
                if (letter == 'R')
                    shape = Mathf.Min(shape, Quad(x, y,
                        new Vector2(w - 39, 0), new Vector2(w, 0),
                        new Vector2(84, 97), new Vector2(48, 97)));
                break;
            case 'T':
                shape = Union(Box(x, y, 0, 146, w, 34),
                    Box(x, y, (w - stem) * 0.5f, 0, stem, h));
                break;
            case 'L':
                shape = Union(Box(x, y, 0, 0, stem, h),
                    Box(x, y, 0, 0, w, 34));
                break;
            default:
                return 100;
        }

        // Every silhouette has cut metal corners, including terminal bars.
        return Mathf.Max(shape, ChamferBox(x, y, w, h, 13));
    }

    private static float Ring(float x, float y, float w, float h, float stroke)
    {
        float outer = ChamferBox(x, y, w, h, 19);
        float inner = ChamferBox(x - stroke, y - stroke,
            w - 2 * stroke, h - 2 * stroke, 12);
        return Mathf.Max(outer, -inner);
    }

    private static float ChamferBox(float x, float y, float w, float h, float cut)
    {
        const float diagonal = 0.70710678118f;
        return Mathf.Max(
            Mathf.Max(Mathf.Max(-x, x - w), Mathf.Max(-y, y - h)),
            Mathf.Max(
                Mathf.Max((cut - x - y) * diagonal, (x - y - w + cut) * diagonal),
                Mathf.Max((y - x - h + cut) * diagonal,
                    (x + y - w - h + cut) * diagonal)));
    }

    private static float Box(float x, float y, float left, float bottom, float width, float height)
    {
        Vector2 center = new Vector2(left + width * 0.5f, bottom + height * 0.5f);
        Vector2 q = new Vector2(Mathf.Abs(x - center.x) - width * 0.5f,
            Mathf.Abs(y - center.y) - height * 0.5f);
        return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude
            + Mathf.Min(Mathf.Max(q.x, q.y), 0);
    }

    private static float Union(params float[] distances)
    {
        float result = float.PositiveInfinity;
        foreach (float distance in distances)
            result = Mathf.Min(result, distance);
        return result;
    }

    private static float Quad(float x, float y, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var p = new Vector2(x, y);
        Vector2[] vertices = { a, b, c, d };
        float minDistanceSquared = float.PositiveInfinity;
        bool inside = true;
        for (int i = 0; i < 4; i++)
        {
            Vector2 start = vertices[i];
            Vector2 edge = vertices[(i + 1) % 4] - start;
            Vector2 relative = p - start;
            float t = Mathf.Clamp01(Vector2.Dot(relative, edge) / edge.sqrMagnitude);
            minDistanceSquared = Mathf.Min(minDistanceSquared, (relative - edge * t).sqrMagnitude);
            if (edge.x * relative.y - edge.y * relative.x < 0)
                inside = false;
        }
        float distance = Mathf.Sqrt(minDistanceSquared);
        return inside ? -distance : distance;
    }
}
