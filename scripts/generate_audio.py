"""Generate original procedural prototype SFX. Python standard library only."""
import array
import hashlib
import json
import math
from pathlib import Path
import random
import sys
import wave
import argparse

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'KLTN/Assets/Audio'
RATE = 44100
TAU = 2 * math.pi


def render(item, replace=False):
    key = item['group'] + ' ' + item['name'] + ' ' + item['keywords']
    k = key.lower()
    rng = random.Random(int(hashlib.sha256(key.encode()).hexdigest()[:16], 16))
    loop = item['loop']
    duration = item['duration']
    n = round(duration * RATE)
    data = array.array('f')
    low = 0.0
    phase = 0.0
    base = item.get('frequency', rng.uniform(43, 68))
    profile = item['profile']
    breath = profile in ('breath', 'growl')
    impact = profile in ('metal', 'concrete', 'flesh', 'debris', 'step', 'switch')
    sweep = profile in ('slide', 'hinge')
    noise = profile in ('whoosh', 'cloth', 'spark', 'creak', 'steam')
    ambience = profile in ('hum', 'fan', 'drone', 'rumble', 'alarm', 'data')
    error = profile == 'error'
    for i in range(n):
        t = i / RATE
        u = t / duration
        white = rng.uniform(-1, 1)
        low += .035 * (white - low)
        high = white - low
        if profile == 'heartbeat':
            q = t % 1.0
            x = .5 * math.exp(-q * 28) * math.sin(TAU * 65 * q)
            if q >= .18:
                x += .3 * math.exp(-(q - .18) * 32) * math.sin(TAU * 82 * (q - .18))
        elif profile in ('beacon', 'beacon_sequence'):
            q = t if profile == 'beacon' else t - (1 + 1.5 * max(0, int((t - 1) / 1.5)))
            if 0 <= q < .42:
                env = min(1, q / .008) * min(1, (.42 - q) / .08)
                x = env * (.28 * math.sin(TAU * 740 * q) + .16 * math.sin(TAU * 1110 * q))
            else:
                x = 0
        elif breath:
            rhythm = .5 - .5 * math.cos(TAU * t / item.get('breath_period', 2.5))
            phase += TAU * base * (1 + .09 * math.sin(TAU * 3.2 * t)) / RATE
            monster = profile == 'growl'
            voice = sum(math.sin(phase * h) / h for h in (1, 2, 3, 5)) if monster else .10 * math.sin(phase * 2)
            envelope = (.12 + rhythm ** 1.6) if loop else math.sin(math.pi * u) ** .7
            x = (.27 * voice + low * 2.4 + high * .055) * envelope
        elif impact:
            # Each footstep file is one contact; gameplay controls cadence.
            x = 0.0
            strikes = (0, .16, .39, .64) if profile == 'debris' else (0, .025)
            for start in strikes:
                q = t - start
                if q >= 0:
                    x += .42 * math.exp(-q * 15) * math.sin(TAU * (base + 25) * q)
                    x += high * .22 * math.exp(-q * 34)
                    if profile not in ('concrete', 'flesh'):
                        ring = item.get('ring', 1)
                        x += ring * sum(.10 * math.exp(-q * decay) * math.sin(TAU * f * q) for f, decay in ((327, 10), (791, 18), (1327, 25)))
        elif sweep:
            env = math.sin(math.pi * u) ** .7
            phase += TAU * (base + 115 * math.sin(math.pi * u)) / RATE
            x = env * (.21 * math.sin(phase) + .08 * math.sin(phase * 3.03) + low * 1.8 + high * .045)
            if profile == 'hinge':
                x = env * (low + .13 * math.sin(phase * 3.1))
            x += .22 * math.exp(-t * 35) * math.sin(TAU * 410 * t)
            q = t - duration * .86
            if q > 0:
                x += .3 * math.exp(-q * 23) * math.sin(TAU * 160 * q)
        elif noise:
            env = math.sin(math.pi * u) ** 2
            x = (high * .27 + low * 1.5) * env
            if profile == 'spark':
                x *= (.5 + .5 * math.sin(TAU * 23 * t)) ** 12
            if profile == 'creak':
                x += .18 * math.sin(TAU * (260 * t + 65 * t * t)) * env
            if profile == 'cloth':
                x = low * 2 * (.35 + .65 * math.sin(TAU * 3 * t) ** 2)
        elif ambience:
            # Integer-cycle oscillators keep the tonal layers periodic.
            f = round(base * duration) / duration
            x = .21 * math.sin(TAU * f * t) + .075 * math.sin(TAU * f * 2 * t)
            x += low * (1.8 if profile == 'fan' else .65)
            x *= .75 + .25 * math.cos(TAU * 2 * u)
            if profile == 'rumble':
                x = low * 2 + .25 * math.sin(TAU * f * t)
            if profile == 'drone':
                x += .13 * math.sin(TAU * (f + 1) * t) + .07 * math.sin(TAU * (f * 1.5) * t)
            if profile == 'data':
                x = low * .3 + .15 * math.sin(TAU * 960 * t) * max(0, math.sin(TAU * 4 * t)) ** 12
            if profile == 'alarm':
                x += .22 * math.sin(TAU * 880 * t) * max(0, math.sin(TAU * t)) ** 4
        else:
            notes = (220, 174, 146) if error else (523.25, 659.25, 783.99, 1046.5)
            if profile == 'stinger':
                notes = (98, 104, 147, 196)
            if profile == 'down':
                notes = tuple(reversed(notes))
            if profile == 'tick':
                notes = (1200,)
            if profile == 'click':
                notes = (720,)
            x = 0.0
            spacing = duration * .55 / len(notes)
            for j, f in enumerate(notes):
                q = t - j * spacing
                if q >= 0:
                    env = min(1, q / .008) * math.exp(-q * 9 / duration)
                    x += .23 * env * (math.sin(TAU * f * q) + .18 * math.sin(TAU * f * 2 * q))
            x += .06 * high * math.exp(-t * 65)
        if not loop:
            x *= min(1, t / .008, (duration - t) / .06)
        data.append(x)
    if loop:
        # Overlap the beginning with a continuation from the end, then trim.
        overlap = int(.20 * RATE)
        for i in range(overlap):
            a = i / (overlap - 1)
            data[n - overlap + i] = data[n - overlap + i] * (1 - a) + data[i] * a
        data = data[overlap:]
    mean = sum(data) / len(data)
    peak = max(abs(x - mean) for x in data)
    gain = 10 ** (item.get('peak_dbfs', -12 if loop else -6) / 20) / max(peak, .001)
    pcm = array.array('h', (round((x - mean) * gain * 32767) for x in data))
    if sys.byteorder != 'little':
        pcm.byteswap()
    path = OUT / item['file']
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and not replace:
        raise FileExistsError(f'Refusing to overwrite {path}')
    temporary = path.with_suffix('.wav.tmp')
    with wave.open(str(temporary), 'wb') as w:
        w.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        w.writeframes(pcm.tobytes())
    temporary.replace(path)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--replace', action='store_true', help='Replace catalog WAVs, preserving Unity meta files')
    args = parser.parse_args()
    items = json.loads((OUT / 'audio_manifest.json').read_text(encoding='utf-8'))
    for index, item in enumerate(items, 1):
        render(item, replace=args.replace)
        print(f'{index}/{len(items)} {item["file"]}', flush=True)
