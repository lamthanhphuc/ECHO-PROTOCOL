"""Validate generated audio assets and gameplay-critical timing without Unity."""
import array
import hashlib
import json
import math
from pathlib import Path
import sys
import wave

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'KLTN/Assets/Audio'


def main():
    items = json.loads((OUT/'audio_manifest.json').read_text(encoding='utf-8'))
    results = []
    for item in items:
        path = OUT/item['file']
        assert (ROOT/item['source']).is_file(), item['source']
        with wave.open(str(path), 'rb') as w:
            assert (w.getnchannels(), w.getsampwidth(), w.getframerate()) == (1, 2, 44100), path
            pcm = array.array('h', w.readframes(w.getnframes()))
        if sys.byteorder != 'little':
            pcm.byteswap()
        duration = len(pcm)/44100
        peak = max(abs(x) for x in pcm)
        rms = math.sqrt(sum(x*x for x in pcm)/len(pcm))
        assert 0 < peak < 32767 and rms > 20, path
        expected = item['duration'] - (.2 if item['loop'] else 0)
        assert abs(duration-expected) < 1/44100, (path, duration, expected)
        assert abs(20*math.log10(peak/32767)-item['peak_dbfs']) < .02, path
        if item['file'] == 'noise_maker/beacon_sequence_6s.wav':
            # Four and only four audible pulse windows, aligned to BeaconRoutine.
            for begin, end in [(0,.99),(1.43,2.49),(2.93,3.99),(4.43,5.49),(5.93,6)]:
                assert max(abs(x) for x in pcm[round(begin*44100):round(end*44100)]) < 32
            for begin in (1,2.5,4,5.5):
                segment=pcm[round((begin+.01)*44100):round((begin+.4)*44100)]
                assert math.sqrt(sum(x*x for x in segment)/len(segment)) > 1000
        results.append(dict(file=item['file'],seconds=round(duration,4),peak_dbfs=round(20*math.log10(peak/32768),2),
            rms_dbfs=round(20*math.log10(rms/32768),2),
            loop_boundary_delta=abs(pcm[-1]-pcm[0]) if item['loop'] else None,
            sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    assert len({x['sha256'] for x in results}) == len(items), 'Duplicate waveforms'
    assert {p.relative_to(OUT).as_posix() for p in OUT.rglob('*.wav')} == {i['file'] for i in items}
    durations = {r['file']:r['seconds'] for r in results}
    assert durations['door/sliding_open.wav'] == durations['door/sliding_close.wav'] == .85
    assert durations['stalker/attack_swing.wav'] == .75
    report=dict(count=len(results),format='44100 Hz mono PCM16 WAV',
        checks=['catalog coverage','source paths exist','unique WAV content','non-silence','no clipping','per-clip peak targets','duration','door 0.85 s','attack windup 0.75 s','beacon four timed pulses'],
        limitations='Procedural prototype assets. Not auditioned or integrated in Unity. No gameplay C# changes.',results=results)
    (OUT/'validation_report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(f'PASS: {len(results)} WAVs; {sum(i["loop"] for i in items)} loops; source references and critical timing verified')


if __name__ == '__main__':
    main()
