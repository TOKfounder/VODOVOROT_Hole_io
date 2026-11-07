#!/usr/bin/env python3
"""Four developed original, periodic Vodovorot acoustic folk-hop arrangements.

Uses the approved Menu V3 motif and three recorded FreePats CC0 instruments.
No third-party tune, reference transcription, V2 noise-string lead or system AU.
TinySoundFont renders THREE continuous performances of the exact frame-aligned
score. After two complete warm-up cycles the third is extracted. Natural sample
tails cross the cycle boundary; room and EQ processing are circular. No intro or
outro fade is applied. Two joins, periodicity and musical turnarounds are logged.

Outputs only Builds/Stage3Review/FullMusic. Never edits Resources/scenes/player.
Python 3 + numpy. --renderer points to the compiled Tools/Audio/tsf-render.cpp.
--theme menu/city/garden/medieval selects one; --check is read-only PCM/manifest QA.
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
OUTPUT = ROOT / "Builds/Stage3Review/FullMusic"
RATE = 44100
WARMUP_CYCLES = 2
SAMPLES = dict(nylon="Samples/SpanishClassicalGuitar-SF2-20190618/SpanishClassicalGuitar-20190618.sf2",
               ukulele="Samples/Ukulele SF2-20260811/Ukulele 20260811.sf2",
               bass="Samples/FingerBassYR SF2-20190930/FingerBassYR 20190930.sf2")
STEM_BANK = dict(nylon_melody="nylon", nylon_fingerpick="nylon", ukulele_comp="ukulele",
                 ukulele_touch="ukulele", ukulele_answer="ukulele", finger_bass="bass")
CHORDS = dict(D6=[50, 57, 59, 62, 66], Bm7=[47, 54, 57, 62, 66], Gmaj7=[43, 50, 54, 59, 62],
              Aadd9=[45, 52, 57, 59, 64], Em7=[52, 55, 59, 62, 66], A7=[45, 52, 55, 59, 61])
ROOTS = dict(D6=38, Bm7=35, Gmaj7=43, Aadd9=33, Em7=40, A7=33)
A_HARMONY = ["D6", "Bm7", "Gmaj7", "Aadd9", "D6", "Gmaj7", "Em7", "A7"]
ANSWER_HARMONY = ["D6", "Bm7", "Em7", "Aadd9", "Gmaj7", "D6", "Em7", "A7"]
B_HARMONY = ["Bm7", "Gmaj7", "D6", "Aadd9", "Em7", "Bm7", "Gmaj7", "A7"]
TURN_HARMONY = ["Gmaj7", "Em7", "Aadd9", "A7"]
# Own approved Menu V3 melody, with new answers/development below.
MOTIF = [66, 64, 62, 57, 59, 62, 64, 66]
PHRASES = [
    [(.52, 66, .78), (1.48, 64, .54), (2.32, 62, .76), (3.38, 57, .38)],
    [(.38, 59, .72), (1.32, 62, .60), (2.30, 64, .50), (3.16, 66, .56)],
    [(.50, 62, .62), (1.34, 67, .80), (2.42, 66, .54), (3.28, 62, .42)],
    [(.36, 61, .62), (1.28, 59, .55), (2.16, 57, 1.10)],
    [(.50, 66, .82), (1.54, 64, .52), (2.34, 62, .88)],
    [(.44, 62, .60), (1.30, 64, .55), (2.22, 66, .62), (3.20, 69, .54)],
    [(.48, 67, .74), (1.45, 66, .54), (2.30, 64, .54), (3.18, 62, .50)],
    [(.32, 61, .55), (1.16, 64, .56), (2.35, 62, 1.45)],
]
ANSWERS = [
    [(.44, 69, .48), (1.22, 66, .60), (2.24, 64, .50), (3.12, 62, .58)],
    [(.32, 66, .56), (1.18, 62, .52), (2.10, 59, .70), (3.25, 62, .44)],
    [(.52, 67, .78), (1.56, 66, .48), (2.38, 62, .94)],
    [(.40, 64, .55), (1.30, 61, .62), (2.30, 59, .50), (3.15, 57, .40)],
    [(.45, 66, .50), (1.24, 69, .65), (2.30, 66, .52), (3.20, 64, .48)],
    [(.38, 67, .58), (1.28, 66, .54), (2.15, 62, .78), (3.30, 59, .35)],
    [(.46, 64, .62), (1.38, 67, .60), (2.42, 66, .50), (3.25, 64, .42)],
    [(.35, 62, .62), (1.30, 61, .58), (2.34, 59, .90)],
]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def relative(path):
    return str(Path(path).relative_to(ROOT))


def bank_path(key):
    for path in [ROOT / "Tools/Audio" / SAMPLES[key], ROOT / "Builds/Stage3Review/MusicV3" / SAMPLES[key]]:
        if path.is_file():
            return path
    raise FileNotFoundError("Recorded CC0 input missing: " + SAMPLES[key])


def filename(theme):
    return "Full" + theme.capitalize() + ".wav"


class Arrangement:
    def __init__(self, theme, bpm, bars, units, seed):
        self.theme, self.bpm, self.bars, self.units = theme, bpm, bars, units
        self.beat = 60 / bpm
        self.frames = round(bars * units * self.beat * RATE)
        self.seconds = self.frames / RATE
        self.rng = np.random.default_rng(seed)
        self.stems = {key: [] for key in STEM_BANK}
        self.sections, self.harmony, self.beat_events = [], [], []

    def note(self, stem, at, length, midi, velocity, pan, drift=.008):
        frame = round((at * self.beat + float(self.rng.uniform(-drift, drift))) * RATE) % self.frames
        self.stems[stem].append(dict(frame=frame, durationFrames=max(1, round(length * self.beat * RATE)),
            midi=int(midi), velocity=round(float(np.clip(velocity * self.rng.uniform(.94, 1.04), .01, .98)), 6), pan=pan))

    def strum(self, stem, notes, at, length, velocity, pan, spread=.028):
        for i, midi in enumerate(notes):
            self.note(stem, at + i * spread, length, midi, velocity * (1 - i * .025), pan)

    def hit(self, kind, at, gain, pan=0):
        frame = round((at * self.beat + float(self.rng.uniform(-.004, .004))) * RATE) % self.frames
        self.beat_events.append(dict(kind=kind, frame=frame, gain=gain * float(self.rng.uniform(.92, 1.04)), pan=pan))

    def section(self, name, first, count, character):
        self.sections.append(dict(name=name, firstBar=first, bars=count, character=character))

    def save_score(self):
        directory = OUTPUT / self.theme
        directory.mkdir(parents=True, exist_ok=True)
        for name, events in self.stems.items():
            if not events:
                continue
            base = ["# One exact integer-frame musical cycle; startSeconds durationSeconds midi velocity pan"]
            continuous = ["# Two whole warmup cycles, third delivered cycle, natural release tail"]
            ordered = sorted(events, key=lambda event: event['frame'])
            for cycle in range(WARMUP_CYCLES + 1):
                for event in ordered:
                    line = (f"{(event['frame'] + cycle * self.frames) / RATE:.9f}\t"
                            f"{event['durationFrames'] / RATE:.9f}\t{event['midi']}\t"
                            f"{event['velocity']:.6f}\t{event['pan']:.4f}")
                    continuous.append(line)
                    if cycle == 0:
                        base.append(line)
            (directory / (name + "-cycle.tsv")).write_text("\n".join(base) + "\n")
            (directory / (name + "-warmup.tsv")).write_text("\n".join(continuous) + "\n")
        payload = dict(theme=self.theme, bpm=self.bpm, beatUnit="eighth" if self.units == 6 else "quarter",
                       meter="6/8" if self.units == 6 else "4/4", bars=self.bars, cycleFrames=self.frames,
                       seconds=self.seconds, sections=self.sections, harmony=self.harmony, stems=self.stems,
                       beatEvents=self.beat_events, sharedOriginalMotif=MOTIF,
                       loopTurnaround="Final A7/Aadd9 answers resolve into D6 at the first bar; no outro.")
        (directory / "score.json").write_text(json.dumps(payload, indent=2) + "\n")


def bar_music(a, bar, chord_name, phrase, variant, intensity=1, breath=False, answer=False):
    """4/4 performance vocabulary; variations change notes, gesture, rhythm and density."""
    base, chord, root = bar * 4, CHORDS[chord_name], ROOTS[chord_name]
    a.harmony.append(dict(bar=bar, chord=chord_name))
    pick = [(0.03, 0), (.62, 1), (1.35, 3), (2.03, 2), (2.76, 4), (3.45, 1)]
    if variant == "city":
        pick = [(.04, 0), (.78, 2), (1.48, 1), (2.26, 3), (3.0, 2)]
    elif variant == "garden":
        pick = [(.04, 0), (.78, 2), (1.56, 1), (2.35, 3), (3.12, 4)]
    if breath:
        pick = pick[:4]
    for i, (at, tone) in enumerate(pick):
        length = 1.35 if variant == "garden" else .66 if variant == "city" else .95 if i % 2 == 0 else .74
        a.note("nylon_fingerpick", base + at, length, chord[tone], (.54 if i % 2 == 0 else .43) * intensity, -.31)
    if variant == "city":
        for at in ([.45, 1.75, 3.18] if not breath else [.45, 2.70]):
            upper = [midi if midi >= 60 else midi + 12 for midi in chord[2:]]
            a.strum("ukulele_comp", upper, base + at, .26, .34 * intensity, .30, .016)
    elif variant == "menu" and bar % 4 in (1, 2):
        a.strum("nylon_fingerpick", chord[2:], base + 2.62, .70, .31 * intensity, -.25)
    elif variant == "garden" and bar % 4 == 0:
        a.strum("ukulele_comp", [m if m >= 60 else m + 12 for m in chord[2:]], base + 2.65, .95, .22 * intensity, .29, .07)

    for at, midi, length in phrase:
        if variant == "city":
            at, length = at * .91 + .06, length * .72
        elif variant == "garden":
            length *= 1.35
        stem = "ukulele_answer" if answer else "nylon_melody"
        if answer and midi < 60:
            midi += 12
        a.note(stem, base + at, length, midi, (.65 if answer else .72) * intensity, .23 if answer else .12, .010)

    line = [(.03, root, .73, .69), (1.80, root + 7, .42, .49), (2.62, root, .65, .59)]
    if variant == "city":
        line = [(.03, root, .53, .68), (.82, root + 12, .24, .43), (1.5, root + 7, .30, .55),
                (2.28, root, .47, .63), (3.02, root + 12, .23, .46)]
    elif variant == "garden":
        line = [(.04, root, 1.30, .61), (2.58, root + 7, .90, .43)]
    elif bar % 4 == 1:
        line.append((3.48, root + 12, .27, .43))
    if breath:
        line = line[:2]
    for at, midi, length, velocity in line:
        a.note("finger_bass", base + at, length, midi, velocity * intensity, -.015, .004)

    if variant != "garden":
        a.note("ukulele_touch", base + 1.04, .15, 48, .28 * intensity, .21, .006)
        if not breath:
            a.note("ukulele_touch", base + 3.06, .14, 48, .25 * intensity, .21, .006)
        if bar % 2 == 0:
            a.note("ukulele_touch", base + 2.78, .18, 47, .20 * intensity, -.16, .006)
    else:
        a.note("ukulele_touch", base + .06, .26, 47, .22 * intensity, -.18, .006)
        if bar % 2 == 0:
            a.note("ukulele_touch", base + 2.63, .20, 47, .18 * intensity, .20, .006)
    if variant == "menu" and bar % 4 == 1 and not breath:
        a.strum("ukulele_comp", [62, 66, 69], base + 3.38, .34, .24 * intensity, .30)
    if variant == "garden":
        if bar % 2 == 0:
            a.hit("kick", base + .05, .055 * intensity)
        a.hit("brush", base + 2.07, .021 * intensity, -.12)
    else:
        a.hit("kick", base + .015, (.115 if variant == "city" else .10) * intensity)
        a.hit("brush", base + 1.045, (.044 if variant == "city" else .034) * intensity, -.14)
        if not breath:
            a.hit("kick", base + 2.48, .070 * intensity)
            a.hit("brush", base + 3.07, (.040 if variant == "city" else .030) * intensity, -.14)
        if variant == "city":
            for at in (.52, 1.52, 2.52, 3.52):
                a.hit("paper", base + at, .011 * intensity, .27)


def menu():
    a = Arrangement("menu", 88, 36, 4, 881203)
    plans = [("A: approved motif", 8, A_HARMONY, PHRASES, 1.0),
             ("A answer: new contour and uke replies", 8, ANSWER_HARMONY, ANSWERS, .95),
             ("B: lower guitar, space and changed harmony", 8, B_HARMONY, ANSWERS, .82),
             ("A return: varied melody and lifted accompaniment", 8, A_HARMONY, PHRASES, 1.02),
             ("Four-bar turnaround", 4, TURN_HARMONY, ANSWERS[4:], .90)]
    bar = 0
    for index, (name, count, harmony, phrases, gain) in enumerate(plans):
        a.section(name, bar, count, name)
        for local in range(count):
            phrase = list(phrases[local % len(phrases)])
            if index == 2:
                phrase = [(at, midi - 12 if midi >= 66 else midi, length * 1.15) for at, midi, length in phrase]
                if local in (2, 6):
                    phrase = []
            elif index == 3 and local in (1, 5):
                phrase += [(3.80, 64 if local == 1 else 66, .22)]
            if index == 4:
                phrase = [(at, midi, length) for at, midi, length in phrase if at < 3]
            bar_music(a, bar, harmony[local % len(harmony)], phrase, "menu", gain,
                      breath=(index == 2 and local % 4 == 3), answer=(index == 1 and local in (2, 5)))
            bar += 1
    return a


def city():
    a = Arrangement("city", 96, 40, 4, 961204)
    plans = [("A: syncopated motif", A_HARMONY, PHRASES, 1.0),
             ("A answer: bass variations and uke", ANSWER_HARMONY, ANSWERS, 1.02),
             ("B: different harmony, shorter guitar answers", B_HARMONY, ANSWERS, .92),
             ("Break: muted density, phrase fragments", B_HARMONY, PHRASES, .78),
             ("A return: complete theme", A_HARMONY, PHRASES, 1.02)]
    bar = 0
    for section, (name, harmony, phrases, gain) in enumerate(plans):
        a.section(name, bar, 8, name)
        for local in range(8):
            phrase = list(phrases[local])
            if section == 2:
                phrase = phrase[::2] + [(3.45, 66 if local % 2 == 0 else 64, .28)]
            if section == 3:
                phrase = [] if local in (0, 2) else phrase[:2] if local < 4 else phrase[1:]
            if section == 4 and local in (1, 5):
                phrase += [(3.82, 62, .20)]
            bar_music(a, bar, harmony[local], phrase, "city", gain, breath=(section == 3),
                      answer=(section == 1 and local in (2, 5, 6)))
            bar += 1
    return a


def garden():
    a = Arrangement("garden", 80, 32, 4, 801205)
    plans = [("A: soft fingerpicked motif", A_HARMONY, PHRASES, .94),
             ("Answer: spacious guitar and low wooden taps", ANSWER_HARMONY, ANSWERS, .86),
             ("Open B: wide rests, lower register", B_HARMONY, ANSWERS, .76),
             ("A return: small melodic ornaments", A_HARMONY, PHRASES, .93)]
    bar = 0
    for section, (name, harmony, phrases, gain) in enumerate(plans):
        a.section(name, bar, 8, name)
        for local in range(8):
            phrase = list(phrases[local])
            if section == 1:
                phrase = phrase[:3]
            elif section == 2:
                phrase = [] if local in (1, 3, 6) else [(at, midi - 12 if midi >= 66 else midi, length) for at, midi, length in phrase[:3]]
            elif section == 3 and local == 5:
                phrase = [(at, midi, length) for at, midi, length in ANSWERS[5]]
            bar_music(a, bar, harmony[local], phrase, "garden", gain, breath=(section == 2 or local == 3))
            bar += 1
    return a


def medieval():
    a = Arrangement("medieval", 276, 72, 6, 921206)
    names = ["A: comic plucked dance", "A answer: ukulele replies", "B: low string answer",
             "Call and response", "Breathing bridge", "A: lifted melody", "B: new ending figures",
             "Theme return", "Eight-bar turnaround"]
    for section, name in enumerate(names):
        a.section(name, section * 8, 8, name)
        harmony = TURN_HARMONY * 2 if section == 8 else B_HARMONY if section in (2, 4, 6) else ANSWER_HARMONY if section in (1, 3) else A_HARMONY
        for local in range(8):
            bar, chord_name = section * 8 + local, harmony[local]
            base, chord, root = bar * 6, CHORDS[chord_name], ROOTS[chord_name]
            a.harmony.append(dict(bar=bar, chord=chord_name))
            intensity = .78 if section == 4 else .91 if section in (2, 6, 8) else 1.0
            for at, midi in [(0.04, root), (3.02, root + 7)]:
                a.note("finger_bass", base + at, 1.90, midi, (.63 if at < 1 else .47) * intensity, -.02, .005)
            for at, tone in [(0.08, 0), (1.10, 2), (2.10, 1), (3.08, 3), (4.12, 4), (5.14, 1)]:
                if section == 4 and at > 4:
                    continue
                a.note("nylon_fingerpick", base + at, 1.35, chord[tone], .46 * intensity, -.31)
            for at in ([1, 4] if section != 4 else [1]):
                upper = [m if m >= 60 else m + 12 for m in chord[2:]]
                a.strum("ukulele_comp", upper, base + at, .95, .27 * intensity, .30, .045)
            phrase = PHRASES[local] if section in (0, 5, 7) else ANSWERS[local]
            phrase = [(at * 1.43, midi, length * 1.35) for at, midi, length in phrase]
            if section in (2, 6):
                phrase = [(at, midi - 12 if midi >= 66 else midi, length) for at, midi, length in phrase]
            if section == 4:
                phrase = [] if local in (0, 2, 5) else phrase[:2]
            if section == 5 and local in (1, 5):
                phrase += [(5.6, 69 if local == 5 else 66, .35)]
            if section == 8:
                phrase = phrase[:3]
            answer = section in (1, 3) and local in (2, 5, 6)
            for at, midi, length in phrase:
                if answer and midi < 60:
                    midi += 12
                a.note("ukulele_answer" if answer else "nylon_melody", base + at, length, midi,
                       .67 * intensity, .25 if answer else .12, .009)
            a.note("ukulele_touch", base + 1.08, .40, 48, .23 * intensity, .20)
            if section != 4:
                a.note("ukulele_touch", base + 4.11, .38, 48, .21 * intensity, .20)
            if local % 2 == 0:
                a.note("ukulele_touch", base + 3.05, .60, 47, .18 * intensity, -.16)
            a.hit("hand", base + .04, .070 * intensity, -.04)
            if section != 4:
                a.hit("hand", base + 3.05, .045 * intensity, -.04)
            if local in (3, 7):
                a.hit("brush", base + 5.12, .021 * intensity, -.16)
    return a


BUILDERS = dict(menu=menu, city=city, garden=garden, medieval=medieval)


def periodic_filter(x, cutoff=6500):
    # N-point FFT deliberately treats this as a periodic signal; no zero-padding
    # creates a new filter transient at the file start.
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    curve = 1 / np.sqrt(1 + (f / cutoff) ** 6)
    curve *= f / np.sqrt(f * f + 24 ** 2)
    return np.fft.irfft(np.fft.rfft(x, axis=0) * curve[:, None], n=len(x), axis=0).astype(np.float32)


def cyclic_add(mix, sound, frame, gain, pan):
    lr = np.array([math.cos((pan + 1) * math.pi / 4), math.sin((pan + 1) * math.pi / 4)], np.float32)
    position, remaining = frame % len(mix), len(sound)
    offset = 0
    while remaining:
        count = min(remaining, len(mix) - position)
        mix[position:position + count] += sound[offset:offset + count, None] * lr * gain
        offset, remaining, position = offset + count, remaining - count, 0


def beat_track(a):
    rng = np.random.default_rng(5700 + a.bars)
    mix = np.zeros((a.frames, 2), np.float32)
    sounds = {}
    for variant in range(4):
        t = np.arange(round(.26 * RATE)) / RATE
        kick = np.sin(2 * math.pi * (48 * t + 30 * .018 * (1 - np.exp(-t / .018)))) * np.exp(-t * 21)
        kick[:80] *= np.linspace(0, 1, 80)
        t2 = np.arange(round(.13 * RATE)) / RATE
        n = rng.normal(0, 1, len(t2))
        brush = np.convolve(n, np.ones(5) / 5, mode="same") * np.exp(-t2 * 40)
        brush += .12 * np.sin(2 * math.pi * (166 + variant * 2) * t2) * np.exp(-t2 * 45)
        brush[:55] *= np.linspace(0, 1, 55)
        t3 = np.arange(round(.11 * RATE)) / RATE
        hand = (np.sin(2 * math.pi * 121 * t3) + .24 * np.sin(2 * math.pi * 208 * t3)) * np.exp(-t3 * 32)
        hand[:55] *= np.linspace(0, 1, 55)
        paper = (n - np.convolve(n, np.ones(15) / 15, mode="same")) * np.exp(-t2 * 50) * .50
        paper[:60] *= np.linspace(0, 1, 60)
        sounds[variant] = dict(kick=kick.astype(np.float32), brush=brush.astype(np.float32),
                               hand=hand.astype(np.float32), paper=paper.astype(np.float32))
    for i, event in enumerate(a.beat_events):
        cyclic_add(mix, sounds[i % 4][event['kind']], event['frame'], event['gain'], event['pan'])
    return periodic_filter(mix, 3400)


def write_wav(path, x):
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.rint(np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as f:
        f.setnchannels(2); f.setsampwidth(2); f.setframerate(RATE); f.writeframes(pcm.tobytes())


def extract_cycle(path, a):
    """Keep cycle 3; independently compare it with cycle 2 from continuous rendering."""
    with wave.open(str(path), "rb") as f:
        assert f.getnchannels() == 2 and f.getsampwidth() == 2 and f.getframerate() == RATE
        f.setpos(WARMUP_CYCLES * a.frames)
        delivered = np.frombuffer(f.readframes(a.frames), dtype="<i2").reshape(-1, 2).copy()
        assert len(delivered) == a.frames
        f.setpos((WARMUP_CYCLES - 1) * a.frames)
        peak, squares, equal, count = 0, 0.0, 0, 0
        for at in range(0, a.frames, 65536):
            n = min(65536, a.frames - at)
            previous = np.frombuffer(f.readframes(n), dtype="<i2").reshape(-1, 2)
            difference = delivered[at:at + n].astype(np.int32) - previous.astype(np.int32)
            peak = max(peak, int(np.max(np.abs(difference))))
            squares += float(np.sum(difference.astype(np.float64) ** 2))
            equal += int(np.count_nonzero(difference == 0)); count += difference.size
    periodicity = dict(comparedContinuousCycles=[2, 3], maxDifferencePcm16=peak,
                       rmsDifference=math.sqrt(squares / count) / 32767, exactSampleFraction=equal / count)
    # Tiny roundoff in the SF2 render engine is allowed; an actual unwrapped tail is not.
    assert peak <= 64 and periodicity['rmsDifference'] < .0001, "Nonperiodic source stem: " + str(periodicity)
    return delivered.astype(np.float32) / 32767, periodicity


def render_theme(a, renderer):
    a.save_score()
    directory = OUTPUT / a.theme
    mix = np.zeros((a.frames, 2), np.float32)
    room = np.zeros_like(mix)
    echoes = np.zeros_like(mix)
    stem_results = {}
    target = dict(nylon_melody=.057, nylon_fingerpick=.043, ukulele_comp=.014, ukulele_touch=.010,
                  ukulele_answer=.018, finger_bass=.048)
    caps = dict(nylon_melody=.37, nylon_fingerpick=.35, ukulele_comp=.24, ukulele_touch=.20,
                ukulele_answer=.29, finger_bass=.31)
    if a.theme == "city":
        target.update(nylon_fingerpick=.036, ukulele_comp=.025, ukulele_touch=.013, finger_bass=.052)
    elif a.theme == "garden":
        target.update(nylon_melody=.052, nylon_fingerpick=.050, ukulele_comp=.013, ukulele_touch=.006, finger_bass=.041)
    elif a.theme == "medieval":
        target.update(nylon_fingerpick=.047, ukulele_comp=.022, ukulele_touch=.012, finger_bass=.043)

    for name, events in a.stems.items():
        if not events:
            continue
        bank = bank_path(STEM_BANK[name])
        temporary = directory / (name + "-continuous-render.tmp.wav")
        # All notes plus their releases remain inside the renderer's explicit duration.
        command = [str(renderer), str(bank), str(directory / (name + "-warmup.tsv")), str(temporary),
                   f"{(WARMUP_CYCLES + 1) * a.seconds + 4:.9f}", "0"]
        result = subprocess.run(command, capture_output=True, text=True, check=True)
        native = json.loads(result.stdout.strip())
        x, periodicity = extract_cycle(temporary, a)
        raw_rms, raw_peak = float(np.sqrt(np.mean(x.astype(np.float64) ** 2))), float(np.max(np.abs(x)))
        assert raw_rms > .00001 and raw_peak < .995
        gain = min(target[name] / raw_rms, caps[name] / raw_peak)
        x *= gain
        mix += x
        if name in ("nylon_melody", "nylon_fingerpick"):
            room += x
        if name in ("ukulele_comp", "ukulele_answer"):
            echoes += x
        stem_results[name] = dict(eventsPerCycle=len(events), bank=relative(bank), bankSha256=sha(bank),
                                 render=native, sourcePeriodicity=periodicity, rawCycleRms=raw_rms,
                                 rawCyclePeak=raw_peak, mixGain=gain)
        # The very large 3-cycle intermediate is reproducible from the retained TSV.
        # Delete only this script's own temporary file after its two-cycle metrics exist.
        temporary.unlink()
        print(a.theme + " " + name + ": " + json.dumps(periodicity), flush=True)

    mix += beat_track(a)
    room = periodic_filter(room, 2600)
    for secs, level, reverse in [(.047, .10, False), (.091, .07, True), (.173, .037, True), (.227, .020, False)]:
        reflected = np.roll(room, round(secs * RATE), axis=0)
        mix += reflected[:, ::-1] * level if reverse else reflected * level
    del room
    echoes = periodic_filter(echoes, 1750)
    mix += np.roll(echoes[:, ::-1], round(.75 * (60 / (92 if a.theme == "medieval" else a.bpm)) * RATE), axis=0) * .08
    del echoes
    mix = periodic_filter(mix, 6500)
    mix -= mix.mean(axis=0)
    mix = np.tanh(mix * 1.7) / 1.7
    rms, peak = float(np.sqrt(np.mean(mix.astype(np.float64) ** 2))), float(np.max(np.abs(mix)))
    mix *= min(.108 / rms, .56 / peak)
    destination = OUTPUT / filename(a.theme)
    write_wav(destination, mix)
    del mix
    metrics = validate(destination)
    joins = join_checks(destination, a)
    record = dict(theme=a.theme, title={"menu": "Small Things, Slowly", "city": "Sidewalk, Soft Shuffle",
                  "garden": "By the Fern", "medieval": "Pockets and Puddles"}[a.theme],
                  bpm=a.bpm, meter="6/8" if a.units == 6 else "4/4", bars=a.bars,
                  warmupCycles=WARMUP_CYCLES, extractedContinuousCycle=3, introOutroFade=False,
                  sampleTailWrap=True, circularEffects=True, sections=a.sections,
                  score=relative(directory / "score.json"), stems=stem_results, pcm=metrics, loopJoins=joins,
                  musicalSeam=dict(lastChord=a.harmony[-1]['chord'], nextChord=a.harmony[0]['chord'],
                                   behavior="A7/Aadd9 answer leads back to D6 and the shared motif; no stopping cadence."))
    (directory / "manifest.json").write_text(json.dumps(record, indent=2) + "\n")
    print(a.theme + " FULL_READY " + json.dumps(metrics), flush=True)
    return record


def validate(path):
    with wave.open(str(path), "rb") as f:
        channels, rate, width, frames = f.getnchannels(), f.getframerate(), f.getsampwidth(), f.getnframes()
        pcm = np.frombuffer(f.readframes(frames), dtype="<i2").reshape(-1, 2)
    x = pcm.astype(np.float32) / 32767
    peak, rms = float(np.max(np.abs(x))), float(np.sqrt(np.mean(x.astype(np.float64) ** 2)))
    mean = float(np.max(np.abs(np.mean(x.astype(np.float64), axis=0))))
    energy = np.mean(x.astype(np.float64) ** 2, axis=0)
    cross = float(np.mean(x[:, 0].astype(np.float64) * x[:, 1]))
    correlation = cross / math.sqrt(float(energy[0] * energy[1]))
    mono_rms = float(np.sqrt(np.mean(((x[:, 0].astype(np.float64) + x[:, 1]) * .5) ** 2)))
    assert channels == 2 and width == 2 and rate == RATE and 90 <= frames / rate <= 120
    assert .30 < peak <= .561 and .060 < rms < .13 and mean < .0005
    assert correlation > .4 and mono_rms / rms > .85, "Stereo phase loses too much in mono"
    return dict(file=path.name, seconds=round(frames / rate, 8), frames=frames, rate=rate, channels=channels,
                bits=width * 8, peak=round(peak, 7), rms=round(rms, 7), rmsDbFS=round(20 * math.log10(rms), 3),
                dc=round(mean, 9), stereoCorrelation=round(correlation, 7), monoRmsRatio=round(mono_rms / rms, 7),
                firstFramePcm16=pcm[0].tolist(), lastFramePcm16=pcm[-1].tolist(), sha256=sha(path))


def join_checks(path, a):
    with wave.open(str(path), "rb") as f:
        pcm = np.frombuffer(f.readframes(f.getnframes()), dtype="<i2").reshape(-1, 2).copy()
    n = len(pcm)
    x = pcm.astype(np.float64) / 32767
    within = np.abs(np.diff(x, axis=0))
    p99 = float(np.percentile(within, 99))
    records = []
    for join in (1, 2):
        # These are the two real sample joins produced by playing this exact PCM
        # period three consecutive times, without fades or a crossfade patch.
        indices = np.arange(-8, 8) % n
        delta = (pcm[0].astype(np.int32) - pcm[-1].astype(np.int32)) / 32767
        peak = float(np.max(np.abs(delta)))
        assert peak < .015 and peak <= max(.002, p99 * 2), "Loop sample step exceeds normal waveform motion"
        records.append(dict(joinNumber=join, timelineFrame=join * n, deltaPerChannel=delta.tolist(),
                            maxAbsDelta=peak, normalAdjacentP99=p99, pcm16EightFramesBeforeAfter=pcm[indices].tolist()))
    half = round(1.8 * RATE)
    window = np.concatenate([x[-half:], x[:half]])
    silence = np.zeros((round(.25 * RATE), 2))
    qa = np.concatenate([window, silence, window])
    qa_path = OUTPUT / "QA" / (a.theme + "-two-loop-joins.wav")
    write_wav(qa_path, qa)
    payload = dict(source=relative(path), twoJoins=records,
                   qaAudio=relative(qa_path), qaFormat="Two 3.6s seam windows separated by 0.25s silence",
                   note="Numerical PCM/score verification only; no claim of subjective listening.")
    (OUTPUT / "QA" / (a.theme + "-loop-joins.json")).write_text(json.dumps(payload, indent=2) + "\n")
    return payload


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--renderer", type=Path, default=ROOT / "Builds/Stage3Review/Tools/tsf-render")
    p.add_argument("--theme", choices=BUILDERS)
    p.add_argument("--score-only", action="store_true")
    p.add_argument("--check", action="store_true")
    args = p.parse_args()
    themes = [args.theme] if args.theme else list(BUILDERS)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    if args.check:
        result = [validate(OUTPUT / filename(theme)) for theme in themes]
        for theme in themes:
            record = json.loads((OUTPUT / theme / "manifest.json").read_text())
            assert record['pcm']['sha256'] == sha(OUTPUT / filename(theme))
            assert len(record['loopJoins']['twoJoins']) == 2 and not record['introOutroFade']
        print(json.dumps(dict(passed=True, clips=result), indent=2))
        return
    if not args.score_only and not args.renderer.is_file():
        p.error("Compile Tools/Audio/tsf-render.cpp first; no synthetic instrument fallback")
    records = []
    for theme in themes:
        a = BUILDERS[theme]()
        if args.score_only:
            a.save_score()
            print(theme, a.seconds, len(a.harmony), "bars; original developed score ready")
        else:
            records.append(render_theme(a, args.renderer.resolve()))
    if not args.score_only:
        manifest = dict(status="four_full_original_loops_for_root_import_and_playback_QA", source=relative(Path(__file__)),
                        sourceSha256=sha(Path(__file__)), renderer=relative(args.renderer.resolve()),
                        rendererSha256=sha(args.renderer), sourceBanks={key: dict(path=relative(bank_path(key)), sha256=sha(bank_path(key)),
                        license="CC0-1.0") for key in SAMPLES}, sharedOriginalMotif=MOTIF,
                        noIntroOutroFade=True, warmupCycles=WARMUP_CYCLES, naturalTailsWrapped=True,
                        browserCompressedSeamCheckRequired=True, subjectivelyListened=False, clips=records)
        name = "FullMusicManifest.json" if not args.theme else "FullMusic-" + args.theme + ".json"
        (OUTPUT / name).write_text(json.dumps(manifest, indent=2) + "\n")


if __name__ == "__main__":
    main()
