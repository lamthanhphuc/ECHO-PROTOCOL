"""Render the original jumpscare sting (standard library, no external recordings)."""
import array
import math
from pathlib import Path
import random
import sys
import wave

RATE = 44100
DURATION = 1.8
OUT = Path(__file__).resolve().parents[1] / 'KLTN/Assets/Audio/stalker/jumpscare.wav'


def render():
    rng = random.Random(20260920)
    samples = []
    phase = low = band = 0.0
    for i in range(round(RATE * DURATION)):
        t = i / RATE
        noise = rng.uniform(-1, 1)
        low += 0.08 * (noise - low)
        band += 0.42 * (noise - band)
        # Falling chest impact, rough voiced scream and dissonant metallic scrape.
        phase += math.tau * (185 + 310 * math.exp(-t * 5) + 13 * math.sin(57 * t)) / RATE
        voice = sum(math.sin(phase * h + 0.35 * math.sin(t * 39)) / h
                    for h in range(1, 13))
        voice = math.tanh(voice * 2.1) * (0.8 + 0.2 * math.sin(91 * t))
        body = math.sin(math.tau * (48 * t + 6 * (1 - math.exp(-t * 24))))
        scrape = sum(math.sin(math.tau * (f * t - 110 * t * t))
                     for f in (1313, 1741, 2297)) / 3
        attack = min(1.0, t / 0.004)
        x = (0.65 * body * math.exp(-t * 11)
             + 0.47 * voice * math.exp(-t * 2.5)
             + 0.24 * (band - low) * math.exp(-t * 4)
             + 0.16 * scrape * math.exp(-t * 3.5)) * attack
        samples.append(x)
    dry = samples[:]
    for delay, gain in ((0.043, 0.24), (0.079, 0.18), (0.131, 0.12), (0.211, 0.07)):
        offset = round(delay * RATE)
        for i in range(offset, len(samples)):
            samples[i] += dry[i - offset] * gain
    for i in range(len(samples)):
        # Smooth tail reaches zero before the normal catch deadline.
        tail = min(1.0, (len(samples) - 1 - i) / (RATE * 0.35))
        samples[i] *= math.sin(tail * math.pi / 2) ** 2
    gain = 10 ** (-3 / 20) / max(abs(x) for x in samples)
    pcm = array.array('h', (round(x * gain * 32767) for x in samples))
    if sys.byteorder != 'little':
        pcm.byteswap()
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT), 'wb') as f:
        f.setparams((1, 2, RATE, len(samples), 'NONE', 'not compressed'))
        f.writeframes(pcm.tobytes())
    print(f'{OUT}: {DURATION}s, mono PCM16, {RATE} Hz, peak -3 dBFS')


if __name__ == '__main__':
    render()
