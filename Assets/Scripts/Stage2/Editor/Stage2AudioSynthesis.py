#!/usr/bin/env python3
"""Reproducible original sounds: no samples, downloads, models, or external packages.
Run from any directory; --check verifies committed WAVs without rewriting them.
"""
from __future__ import annotations
import argparse
import json
import math
from pathlib import Path
import random
import struct
import wave

RATE = 44100
ROOT = Path(__file__).resolve().parents[4]
OUTPUT = ROOT / 'Assets/Audio/Stage2/Resources/Stage2Audio'


def fade_edges(samples: list[float], fade: float = .009) -> list[float]:
    count = max(1, int(RATE * fade))
    for i in range(min(count, len(samples) // 2)):
        gain = math.sin((i / count) * math.pi / 2) ** 2
        samples[i] *= gain
        samples[-1 - i] *= gain
    samples[0] = samples[-1] = 0.0
    return samples


def normalize(samples: list[float], peak: float) -> list[float]:
    mean = sum(samples) / len(samples)
    samples = [sample - mean for sample in samples]
    samples = fade_edges(samples)
    gain = peak / max(abs(sample) for sample in samples)
    return [sample * gain for sample in samples]


def plop(deep: bool, family: str) -> list[float]:
    duration = .30 if deep else .175
    rng = random.Random(4417 + (101 if deep else 0) + {'Base': 0, 'Gold': 37, 'Lord': 73}[family])
    phase = 0.0
    smoothed = previous = 0.0
    samples = []
    for i in range(round(duration * RATE)):
        t = i / RATE
        # A descending, rounded resonance resembles a little object crossing a liquid surface.
        start, end, glide = (178, 73, .039) if deep else (335, 156, .022)
        frequency = end + (start - end) * math.exp(-t / glide)
        phase += math.tau * frequency / RATE
        attack = 1 - math.exp(-t / .0028)
        body = (math.sin(phase) + .18 * math.sin(phase * 1.99 + .2)) * math.exp(-t / (.055 if deep else .031))
        bubble = .26 * math.sin(phase * .51 + .5) * math.exp(-t / (.075 if deep else .043))
        # Very soft filtered noise gives a tactile contact, without a sharp click or hiss.
        noise = rng.uniform(-1, 1)
        smoothed += .14 * (noise - smoothed)
        band = smoothed - previous
        previous += .018 * (smoothed - previous)
        contact = .11 * band * math.exp(-t / .019)
        tone = body + bubble + contact
        if family == 'Gold':
            tone += .065 * math.sin(math.tau * (713 if deep else 1091) * t) * math.exp(-t / .024)
            tone += .027 * math.sin(math.tau * 1487 * t) * math.exp(-t / .012)
        elif family == 'Lord':
            tone += .09 * math.sin(math.tau * (91 if deep else 201) * t) * math.exp(-t / .045)
            tone -= .035 * math.sin(phase * 2.97) * math.exp(-t / .021)
        samples.append(tone * attack)
    return normalize(samples, .72)


def rank_accent() -> list[float]:
    duration = .82
    samples = [0.0] * round(duration * RATE)
    # Original four-note D-major mallet flourish. No melody or recording is borrowed.
    notes = [(0.0, 587.3295, .72), (.09, 739.9888, .65), (.18, 880.0, .64), (.29, 1174.659, .72)]
    for onset, frequency, strength in notes:
        for i in range(round(onset * RATE), len(samples)):
            t = i / RATE - onset
            attack = 1 - math.exp(-t / .003)
            fundamental = math.sin(math.tau * frequency * t) * math.exp(-t / .105)
            warm = .20 * math.sin(math.tau * frequency * .5 * t) * math.exp(-t / .14)
            mallet = .075 * math.sin(math.tau * frequency * 2.01 * t) * math.exp(-t / .035)
            samples[i] += strength * attack * (fundamental + warm + mallet)
    return normalize(samples, .62)


def write_wav(path: Path, samples: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = struct.pack('<' + 'h' * len(samples), *(round(max(-1, min(1, value)) * 32767) for value in samples))
    with wave.open(str(path), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(pcm)


def inspect(path: Path) -> dict:
    with wave.open(str(path), 'rb') as source:
        assert source.getnchannels() == 1 and source.getsampwidth() == 2 and source.getframerate() == RATE, str(path)
        pcm = source.readframes(source.getnframes())
        values = [value / 32767 for value in struct.unpack('<' + 'h' * (len(pcm) // 2), pcm)]
    peak = max(abs(value) for value in values)
    rms = math.sqrt(sum(value * value for value in values) / len(values))
    dc = sum(values) / len(values)
    # Zero-crossing frequency over the resonant body provides a repeatable light/deep separation check.
    body = values[round(.014 * RATE):round(.075 * RATE)]
    crossings = sum((a < 0) != (b < 0) for a, b in zip(body, body[1:]))
    frequency = crossings * RATE / (2 * len(body))
    accent = path.stem == 'RankImproved'
    assert peak <= (.6201 if accent else .7201), (path, peak)
    assert .04 < rms < .3, (path, rms)
    assert abs(dc) < .003, (path, dc)
    assert abs(values[0]) < 1e-6 and abs(values[-1]) < 1e-6, path
    assert .8 <= len(values) / RATE <= .84 if accent else .17 <= len(values) / RATE <= .31, path
    return dict(file=path.name, seconds=round(len(values) / RATE, 4), peak=round(peak, 5),
                rms=round(rms, 5), dc=round(dc, 7), body_frequency_hz=round(frequency, 2))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    if not args.check:
        for family in ['Base', 'Gold', 'Lord']:
            for deep in [False, True]:
                write_wav(OUTPUT / f'Absorb{family}{"Deep" if deep else "Light"}.wav', plop(deep, family))
        write_wav(OUTPUT / 'RankImproved.wav', rank_accent())
    reports = [inspect(OUTPUT / f'Absorb{family}{kind}.wav') for family in ['Base', 'Gold', 'Lord'] for kind in ['Light', 'Deep']]
    reports.append(inspect(OUTPUT / 'RankImproved.wav'))
    for family in ['Base', 'Gold', 'Lord']:
        light, deep = [entry for entry in reports if family in entry['file']]
        assert light['body_frequency_hz'] > deep['body_frequency_hz'] * 1.4, family
    print(json.dumps(dict(passed=True, clips=reports, maximum_stage2_linear_peak_bound=round(3 * .28 * .72 + .24 * .62, 4)), indent=2))


if __name__ == '__main__':
    main()
