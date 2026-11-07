#!/usr/bin/env python3
"""One original Menu V3 listening prototype, using recorded CC0 instruments.

The musical direction is warm acoustic folk-hop / mellow playful groove. This
is a new score, not the V1/V2 motif revoiced, and not a transcription of reference
music. Main instruments are actual FreePats nylon guitar, ukulele and finger bass
recordings rendered by the project's MIT TinySoundFont offline tool. No AU,
audio device, third-party tune, external generator or system installation.

Outputs ONLY Builds/Stage3Review/MusicV3, never Unity Resources. Requires numpy.
Usage: python3 Stage3MusicCompositionV3.py --renderer /path/to/tsf-render
       python3 Stage3MusicCompositionV3.py --score-only
       python3 Stage3MusicCompositionV3.py --check
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[4]
OUTPUT = ROOT / "Builds/Stage3Review/MusicV3"
RATE = 44100
BPM = 88
BEAT = 60 / BPM
BARS = 8
SECONDS = BARS * 4 * BEAT + 2.0
SAMPLES = {
    "nylon": "Samples/SpanishClassicalGuitar-SF2-20190618/SpanishClassicalGuitar-20190618.sf2",
    "ukulele": "Samples/Ukulele SF2-20260811/Ukulele 20260811.sf2",
    "bass": "Samples/FingerBassYR SF2-20190930/FingerBassYR 20190930.sf2",
}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def resolve_bank(key):
    relative = SAMPLES[key]
    for candidate in [ROOT / "Tools/Audio" / relative, OUTPUT / relative]:
        if candidate.is_file():
            return candidate
    raise FileNotFoundError("CC0 source bank missing in Tools/Audio or review draft: " + relative)


def resolve_provenance():
    for candidate in [ROOT / "Tools/Audio/SampleProvenance.json", ROOT / "Tools/Audio/Samples/SampleProvenance.json",
                      OUTPUT / "SampleProvenance.json"]:
        if candidate.is_file():
            return candidate
    raise FileNotFoundError("SampleProvenance.json with CC0 origins/checksums is required")


def fade_edges(x, attack=.006, release=.6):
    a, r = max(2, round(attack * RATE)), max(2, round(release * RATE))
    x[:a] *= np.sin(np.linspace(0, math.pi / 2, a))[:, None] ** 2
    x[-r:] *= np.cos(np.linspace(0, math.pi / 2, r))[:, None] ** 2
    x[0] = x[-1] = 0
    return x


class MenuScore:
    def __init__(self):
        self.rng = np.random.default_rng(2026102503)
        self.stems = {key: [] for key in ["nylon_melody", "nylon_fingerpick", "ukulele_comp", "ukulele_touch", "finger_bass"]}

    def note(self, stem, beat, length, pitch, velocity, pan, drift=.008):
        start = max(0, beat * BEAT + float(self.rng.uniform(-drift, drift)))
        self.stems[stem].append(dict(start=round(start, 6), duration=round(length * BEAT, 6),
                                    midi=pitch, velocity=round(velocity * float(self.rng.uniform(.94, 1.04)), 5), pan=pan))

    def strum(self, stem, notes, beat, length, velocity, pan):
        for i, pitch in enumerate(notes):
            self.note(stem, beat + i * .029, length, pitch, velocity * (1 - i * .035), pan)

    def compose(self):
        # Own D6/Bm7/Gmaj7/Aadd9 palette, with a different second-half answer.
        chords = [
            [50, 57, 59, 62, 66], [47, 54, 57, 62, 66],
            [43, 50, 54, 59, 62], [45, 52, 57, 59, 64],
            [50, 57, 59, 62, 66], [43, 50, 54, 59, 62],
            [52, 55, 59, 62, 66], [45, 52, 55, 59, 61],
        ]
        roots = [38, 35, 43, 33, 38, 43, 40, 33]
        # Mid-register guitar sings in relaxed phrases; a real note's natural body
        # remains after each touch. This is NOT the short noisy V2 string hook.
        phrases = [
            [(.52, 66, .78), (1.48, 64, .54), (2.32, 62, .76), (3.38, 57, .38)],
            [(.38, 59, .72), (1.32, 62, .60), (2.30, 64, .50), (3.16, 66, .56)],
            [(.50, 62, .62), (1.34, 67, .80), (2.42, 66, .54), (3.28, 62, .42)],
            [(.36, 61, .62), (1.28, 59, .55), (2.16, 57, 1.10)],
            [(.50, 66, .82), (1.54, 64, .52), (2.34, 62, .88)],
            [(.44, 62, .60), (1.30, 64, .55), (2.22, 66, .62), (3.20, 69, .54)],
            [(.48, 67, .74), (1.45, 66, .54), (2.30, 64, .54), (3.18, 62, .50)],
            [(.32, 61, .55), (1.16, 64, .56), (2.35, 62, 1.65)],
        ]
        pick_positions = [(0.03, 0), (.62, 1), (1.35, 3), (2.03, 2), (2.76, 4), (3.45, 1)]
        for bar in range(BARS):
            base, chord, root = bar * 4, chords[bar], roots[bar]
            for i, (at, tone) in enumerate(pick_positions):
                # One less upper touch in the breathing bar and at the final cadence.
                if bar in (3, 7) and at > 3:
                    continue
                pitch = chord[tone]
                self.note("nylon_fingerpick", base + at, .95 if i % 2 == 0 else .74, pitch,
                          .54 if i % 2 == 0 else .43, -.31)
            if bar in (1, 5):
                self.strum("nylon_fingerpick", chord[2:], base + 2.62, .70, .32, -.25)
            for i, (at, pitch, length) in enumerate(phrases[bar]):
                self.note("nylon_melody", base + at, length, pitch,
                          .70 + (.045 if i == 0 else 0), .12, .010)
            # Relaxed two-to-four bass touches, with a little approach into a change.
            bass_line = [(0.03, root, .73, .69), (1.80, root + 7, .42, .49), (2.62, root, .65, .59)]
            if bar in (1, 4, 5):
                bass_line.append((3.48, root + 12, .27, .43))
            if bar == 3:
                bass_line = bass_line[:2]
            if bar == 7:
                bass_line = [(0.03, root, .72, .63), (1.62, root + 7, .40, .45), (2.35, 38, 1.6, .63)]
            for at, pitch, length, velocity in bass_line:
                self.note("finger_bass", base + at, length, pitch, velocity, -.015, .004)
            # Recorded ukulele chuck/body-tap add a physical pulse, not a second lead.
            self.note("ukulele_touch", base + 1.04, .15, 48, .28, .21, .006)
            if bar != 3:
                self.note("ukulele_touch", base + 3.06, .14, 48, .24, .21, .006)
            if bar in (0, 2, 4, 6):
                self.note("ukulele_touch", base + 2.78, .18, 47, .22, -.16, .006)
            if bar in (2, 4, 5, 6):
                upper = [p if p >= 60 else p + 12 for p in chord[2:]]
                self.strum("ukulele_comp", upper, base + 1.75, .42, .30, .31)
            if bar in (1, 5):
                self.strum("ukulele_comp", [62, 66, 69], base + 3.38, .34, .24, .30)
        # One gentle natural guitar cadence; do not hard-cut a sample at the bar line.
        self.strum("nylon_fingerpick", [50, 57, 62, 66, 69], 30.38, 2.1, .40, -.22)
        self.strum("ukulele_comp", [62, 66, 69], 30.48, 1.25, .24, .28)
        return self

    def write(self):
        folder = OUTPUT / "Stems"
        folder.mkdir(parents=True, exist_ok=True)
        for stem, events in self.stems.items():
            rows = ["# startSeconds durationSeconds midi velocity0..1 pan(-1..1)"]
            for e in sorted(events, key=lambda x: x['start']):
                rows.append(f"{e['start']:.6f}\t{e['duration']:.6f}\t{e['midi']}\t{e['velocity']:.5f}\t{e['pan']:.3f}")
            (folder / (stem + ".tsv")).write_text("\n".join(rows) + "\n")
        score = dict(title="Small Things, Slowly — Menu V3", themeId="menu", revision=3,
                     bpm=BPM, meter="4/4", bars=BARS, seconds=SECONDS, originalScore=True,
                     tonalPlan="D6, Bm7, Gmaj7, Aadd9, D6, Gmaj7, Em7, A7 to D6",
                     sharedMotifFromV1OrV2=False, stems=self.stems)
        (OUTPUT / "menu-v3-score.json").write_text(json.dumps(score, indent=2) + "\n")


def read_pcm(path):
    with wave.open(str(path), "rb") as f:
        assert f.getnchannels() == 2 and f.getsampwidth() == 2 and f.getframerate() == RATE, path
        x = np.frombuffer(f.readframes(f.getnframes()), dtype="<i2").reshape(-1, 2).astype(np.float64) / 32767
    assert np.max(np.abs(x)) < .995, "A rendered source stem clipped before mixing: " + str(path)
    count = round(SECONDS * RATE)
    result = np.zeros((count, 2), np.float64)
    result[:min(count, len(x))] = x[:count]
    return result


def low_pass(x, hz):
    count = len(x)
    length = 1 << (count + 8192 - 1).bit_length()
    spectrum = np.fft.rfft(x, n=length, axis=0)
    frequencies = np.fft.rfftfreq(length, 1 / RATE)
    curve = 1 / np.sqrt(1 + (frequencies / hz) ** 6)
    return np.fft.irfft(spectrum * curve[:, None], n=length, axis=0)[:count]


def soft_beat():
    """Quiet original low drum/brush underneath the real recorded uke percussion."""
    rng = np.random.default_rng(883305)
    x = np.zeros((round(SECONDS * RATE), 2), np.float64)

    def put(sound, beat, gain, pan=0):
        at = round((beat * BEAT + .007) * RATE)
        n = min(len(sound), len(x) - at)
        lr = np.array([math.cos((pan + 1) * math.pi / 4), math.sin((pan + 1) * math.pi / 4)])
        x[at:at + n] += sound[:n, None] * lr * gain

    t = np.arange(round(.26 * RATE)) / RATE
    kick = np.sin(2 * math.pi * (48 * t + 30 * .018 * (1 - np.exp(-t / .018)))) * np.exp(-t * 21)
    kick[:90] *= np.linspace(0, 1, 90)
    t = np.arange(round(.12 * RATE)) / RATE
    noise = rng.normal(0, 1, len(t))
    brush = np.convolve(noise, np.ones(5) / 5, mode="same") * np.exp(-t * 41)
    brush += .12 * np.sin(2 * math.pi * 169 * t) * np.exp(-t * 45)
    brush[:55] *= np.linspace(0, 1, 55)
    for bar in range(BARS):
        base = bar * 4
        put(kick, base, .12 if bar not in (3, 7) else .095)
        if bar != 3:
            put(kick, base + 2.48, .075)
        put(brush, base + 1.045, .043, -.14)
        if bar != 3:
            put(brush, base + 3.07, .039, -.14)
    return low_pass(x, 3300)


def write_wav(path, x):
    pcm = np.rint(np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as f:
        f.setnchannels(2); f.setsampwidth(2); f.setframerate(RATE); f.writeframes(pcm.tobytes())


def render(renderer):
    folder = OUTPUT / "Stems"
    bank_for_stem = dict(nylon_melody="nylon", nylon_fingerpick="nylon", ukulele_comp="ukulele",
                         ukulele_touch="ukulele", finger_bass="bass")
    banks = {key: resolve_bank(key) for key in SAMPLES}
    provenance = resolve_provenance()
    render_stats = {}
    for stem, bank in bank_for_stem.items():
        command = [str(renderer), str(banks[bank]), str(folder / (stem + ".tsv")),
                   str(folder / (stem + ".wav")), f"{SECONDS:.6f}", "0"]
        result = subprocess.run(command, check=True, capture_output=True, text=True)
        render_stats[stem] = json.loads(result.stdout.strip())
        print(stem + ": " + result.stdout.strip())

    stems = {name: read_pcm(folder / (name + ".wav")) for name in bank_for_stem}
    # Preserve sample attacks/natural decay; balance musical roles, not each note's peak.
    desired_rms = dict(nylon_melody=.061, nylon_fingerpick=.046, ukulele_comp=.013,
                       ukulele_touch=.010, finger_bass=.050)
    levels = {}
    for name, x in stems.items():
        raw_rms = float(np.sqrt(np.mean(x * x)))
        assert raw_rms > .00005, "Silent recorded instrument stem: " + name
        gain = desired_rms[name] / raw_rms
        stems[name] = x * gain
        levels[name] = dict(rawRms=raw_rms, mixGain=gain, bank=SAMPLES[bank_for_stem[name]])
    mix = sum(stems.values())
    mix += soft_beat()
    # Short, dark reflections keep wood in the room rather than an artificial lead.
    room = low_pass(stems['nylon_melody'] + stems['nylon_fingerpick'], 2600)
    for secs, level, swap in [(.047, .10, False), (.091, .07, True), (.173, .037, True)]:
        delay = round(secs * RATE)
        mix[delay:] += room[:-delay, ::-1] * level if swap else room[:-delay] * level
    # Very quiet delayed ukulele fragment is the electronic detail; no added beep lead.
    delay = round(.75 * BEAT * RATE)
    ghost = low_pass(stems['ukulele_comp'], 1700)
    mix[delay:] += ghost[:-delay, ::-1] * .085
    mix = low_pass(mix, 6500)
    mix -= mix.mean(axis=0)
    # Soft rounding only for isolated stacked pluck peaks, retaining acoustic dynamics.
    mix = np.tanh(mix * 1.7) / 1.7
    mix *= min(.108 / np.sqrt(np.mean(mix ** 2)), .56 / np.max(np.abs(mix)))
    fade_edges(mix)
    destination = OUTPUT / "DemoMenuV3.wav"
    write_wav(destination, mix)
    report = dict(status="single_menu_v3_prototype_awaiting_user_listening", originalComposition=True,
                  mainPitchedVoices="Recorded FreePats CC0 nylon guitar, ukulele, finger bass",
                  appleBankUsed=False, v2NoiseStringUsed=False, resourcesReplaced=False,
                  renderer=str(renderer), score="menu-v3-score.json", stemMix=levels,
                  sourceRenderStats=render_stats, sampleProvenance=str(provenance.relative_to(ROOT)),
                  sampleProvenanceSha256=sha(provenance),
                  sourceBanks={key: dict(path=str(path.relative_to(ROOT)), sha256=sha(path), license="CC0-1.0")
                               for key, path in banks.items()},
                  compositionSource="Assets/Scripts/Stage3/Editor/Stage3MusicCompositionV3.py",
                  compositionSourceSha256=sha(Path(__file__)), rendererSha256=sha(renderer),
                  pcm=check(destination))
    (OUTPUT / "MusicV3MenuManifest.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


def check(path):
    with wave.open(str(path), "rb") as f:
        channels, bits, rate, frames = f.getnchannels(), f.getsampwidth() * 8, f.getframerate(), f.getnframes()
        x = np.frombuffer(f.readframes(frames), dtype="<i2").reshape(-1, 2).astype(np.float64) / 32767
    peak, rms, dc = float(np.max(np.abs(x))), float(np.sqrt(np.mean(x ** 2))), float(np.max(np.abs(x.mean(axis=0))))
    assert channels == 2 and bits == 16 and rate == RATE and 20 <= frames / rate <= 30
    assert .2 < peak <= .561 and .055 < rms < .13 and dc < .0005
    assert np.max(np.abs(x[[0, -1]])) == 0
    return dict(file=path.name, seconds=round(frames / rate, 5), channels=channels, bits=bits, rate=rate,
                peak=round(peak, 6), rms=round(rms, 6), rmsDbFS=round(20 * math.log10(rms), 2),
                dc=round(dc, 8), sha256=sha(path))


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--renderer", type=Path)
    p.add_argument("--score-only", action="store_true")
    p.add_argument("--check", action="store_true")
    args = p.parse_args()
    if args.check:
        print(json.dumps(dict(passed=True, pcm=check(OUTPUT / "DemoMenuV3.wav")), indent=2))
        return
    OUTPUT.mkdir(parents=True, exist_ok=True)
    MenuScore().compose().write()
    if args.score_only:
        print("Original Menu V3 score/stem TSVs ready; no synthesis fallback or Resources writes.")
        return
    if args.renderer is None or not args.renderer.is_file():
        p.error("--renderer must name the compiled project TinySoundFont renderer; no V2 synth fallback")
    render(args.renderer.resolve())


if __name__ == "__main__":
    main()
