#!/usr/bin/env python3
"""Vodovorot music revision 2: original tactile folk-hop / cartoon compositions.

Python + numpy only; no recordings, third-party soundbank, external generators,
downloaded MIDI, keys, voices or wind instruments. A Karplus-style string model,
damped wooden modes, felt piano/EP, rounded bass and hand-made percussion replace
the previous whistle-like lead palette. Writes REVIEW DRAFTS ONLY under Builds.

Run normally to render four 20–30s demos; --theme menu renders the first prototype.
--check validates existing files without rewriting them. Resources remain V1.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import wave

import numpy as np

RATE = 44100
TAU = 2 * math.pi
ROOT = Path(__file__).resolve().parents[4]
OUTPUT = ROOT / "Builds/Stage3Review/MusicV2"
MOTIF = [67, 64, 62, 64, 69, 67, 64, 60, 62, 64, 67, 62]


def hz(note):
    return 440 * 2 ** ((note - 69) / 12)


def edges(x, attack=.0025, release=.035):
    a, r = min(len(x), max(2, int(attack * RATE))), min(len(x), max(2, int(release * RATE)))
    x[:a] *= np.sin(np.linspace(0, math.pi / 2, a)) ** 2
    x[-r:] *= np.cos(np.linspace(0, math.pi / 2, r)) ** 2
    return x


def smooth_noise(count, rng, width=5):
    return np.convolve(rng.normal(0, 1, count), np.ones(width) / width, mode="same")


def string_voice(pitch, held, rng, muted=False):
    """Noise-excited, lossy feedback string with pick notch and exact tuning.

    Averaging adjacent samples each round damps upper string modes faster than
    the fundamental. Resampling corrects the integer delay's tuning error.
    Body resonators and note-off damping keep these tactile rather than sine leads.
    """
    f = hz(pitch)
    duration = held + (.18 if muted else .42)
    count = int(duration * RATE)
    period = max(12, round(RATE / f - .5))
    native = RATE / (period + .5)
    ratio = f / native
    needed = math.ceil(count * ratio) + period + 2
    raw = np.zeros(needed)
    seed = smooth_noise(period, rng, 4 if muted else 3)
    seed -= np.roll(seed, max(1, int(period * .22))) * .70
    seed += .24 * np.sin(np.arange(period) * TAU / period)
    seed -= np.mean(seed)
    seed /= max(.01, np.max(np.abs(seed)))
    previous = seed
    for at in range(0, needed, period):
        current = .996 * (previous + np.roll(previous, 1)) * .5
        raw[at:at + min(period, needed - at)] = current[:min(period, needed - at)]
        previous = current
    x = np.interp(np.arange(count) * ratio, np.arange(needed), raw)
    t = np.arange(count) / RATE
    x *= np.exp(-t / (.22 if muted else .75))
    x *= np.exp(-np.maximum(0, t - held) / (.025 if muted else .08))
    # Quiet broad body resonances establish the touch of a wooden box.
    x += .035 * np.sin(TAU * 187 * t) * np.exp(-t * 42)
    x += .018 * np.sin(TAU * 326 * t) * np.exp(-t * 65)
    return edges(x, .002, .03)


def pitched(name, pitch, held, rng):
    if name in ("muted_string", "body_guitar", "lute_pluck"):
        x = string_voice(pitch, held, rng, muted=name == "muted_string")
        if name == "lute_pluck":
            # Quiet doubled-course shimmer, with no added upper-register melody.
            delay = round(.00085 * RATE)
            x[delay:] += .13 * x[:-delay]
    else:
        duration = held + (.4 if name in ("felt_keys", "warm_ep") else .2)
        t = np.arange(int(duration * RATE)) / RATE
        f, phase = hz(pitch), TAU * hz(pitch) * t
        x = np.zeros_like(t)
        if name == "wood_mallet":
            # Damped bar resonators: warm fundamental + short, quiet inharmonic attack.
            for ratio, gain, decay in [(1, 1, .27), (3.98, .13, .065), (9.17, .018, .025)]:
                x += gain * np.sin(phase * ratio) * np.exp(-t / decay)
            x += smooth_noise(len(t), rng, 9) * .05 * np.exp(-t * 95)
            x *= np.exp(-np.maximum(0, t - held) / .045)
        elif name == "felt_keys":
            for k, gain in enumerate((1, .43, .21, .10, .045, .018), 1):
                x += gain * np.sin(phase * k * math.sqrt(1 + .00009 * k * k)) * np.exp(-t * (1 + .4 * k) / .85)
                x += gain * .11 * np.sin(phase * k * 1.0013) * np.exp(-t * (1 + .4 * k))
            x += smooth_noise(len(t), rng, 11) * .035 * np.exp(-t * 110)
            x *= np.exp(-np.maximum(0, t - held) / .14)
        elif name == "warm_ep":
            # Low-index FM core and quickly damped tine partials; no sustained solo sine.
            x = np.sin(phase + (.65 * np.exp(-t * 14) + .11) * np.sin(phase * 2.002))
            x += .18 * np.sin(phase * 2) * np.exp(-t * 8)
            x += .065 * np.sin(phase * 3.001) * np.exp(-t * 16)
            x *= np.exp(-t / .75) * np.exp(-np.maximum(0, t - held) / .10)
        elif name == "rubber_bass":
            p = phase + TAU * f * .0011 * (1 - np.exp(-t * 75))
            x = np.sin(p) + .34 * np.sin(2 * p) * np.exp(-t * 6)
            x += .14 * np.sin(3 * p) * np.exp(-t * 12)
            x += .05 * np.sin(4 * p) * np.exp(-t * 20)
            x *= (1 - np.exp(-t * 480)) * np.exp(-t * 1.9)
            x *= np.exp(-np.maximum(0, t - held) / .045)
        else:
            raise ValueError(name)
        edges(x, .003, .035)
    return (x / max(.01, np.max(np.abs(x)))).astype(np.float32)


def percussion(name, rng):
    seconds = {"soft_kick": .26, "wood_rim": .08, "paper_shake": .08,
               "hand_drum": .18, "brush_tap": .13, "wood_tick": .055, "round_pop": .10}[name]
    t = np.arange(round(seconds * RATE)) / RATE
    n = smooth_noise(len(t), rng, 4)
    if name == "soft_kick":
        p = TAU * (49 * t + 46 * .019 * (1 - np.exp(-t / .019)))
        x = np.sin(p) * np.exp(-t * 19) + .065 * n * np.exp(-t * 140)
    elif name == "wood_rim":
        x = (.54 * np.sin(TAU * 710 * t) + .24 * np.sin(TAU * 1171 * t)) * np.exp(-t * 95)
        x += .18 * n * np.exp(-t * 120)
    elif name == "paper_shake":
        x = (n - np.convolve(n, np.ones(13) / 13, mode="same"))
        x *= np.sin(np.minimum(1, t / .018) * math.pi / 2) * np.exp(-t * 55)
    elif name == "hand_drum":
        p = TAU * (122 * t + 32 * .010 * (1 - np.exp(-t / .010)))
        x = (np.sin(p) + .24 * np.sin(TAU * 209 * t)) * np.exp(-t * 26)
        x += .14 * n * np.exp(-t * 90)
    elif name == "brush_tap":
        x = (.23 * np.sin(TAU * 178 * t) + n) * np.exp(-t * 37)
    elif name == "round_pop":
        p = TAU * (310 * t + 130 * .014 * (1 - np.exp(-t / .014)))
        x = (np.sin(p) + .19 * np.sin(p * 2)) * np.exp(-t * 47)
    else:
        x = (.6 * np.sin(TAU * 520 * t) + .32 * np.sin(TAU * 841 * t) + .12 * n) * np.exp(-t * 110)
    edges(x, .0007, .01)
    return (x / max(.01, np.max(np.abs(x)))).astype(np.float32)


class Score:
    def __init__(self, theme, bpm, beats, tail, seed):
        self.theme, self.bpm, self.beats = theme, bpm, beats
        self.beat = 60 / bpm
        self.rng = np.random.default_rng(seed)
        self.mix = np.zeros((round((beats * self.beat + tail) * RATE), 2), np.float64)
        self.room = np.zeros_like(self.mix)
        self.events = []
        self.drums = {}

    def add(self, x, at, gain, pan=0, room=.10):
        offset = round(at * self.beat * RATE)
        if offset < 0:
            x, offset = x[-offset:], 0
        count = min(len(x), len(self.mix) - offset)
        if count <= 0:
            return
        pan = max(-.7, min(.7, pan))
        lr = np.array([math.cos((pan + 1) * math.pi / 4), math.sin((pan + 1) * math.pi / 4)])
        sound = x[:count, None] * lr[None, :] * gain
        self.mix[offset:offset + count] += sound
        self.room[offset:offset + count] += sound * room

    def note(self, name, pitch, at, length, gain, pan=0, room=.10):
        # Each touch has its own excitation and velocity, with intentional scored timing.
        velocity = float(self.rng.uniform(.92, 1.04))
        self.add(pitched(name, pitch, length * self.beat, self.rng), at, gain * velocity, pan, room)
        self.events.append(dict(instrument=name, pitch=pitch, at=round(at, 5), length=round(length, 5),
                                gain=round(gain * velocity, 6), pan=pan))

    def chord(self, name, notes, at, length, gain, pan=0, strum=.018):
        for i, pitch in enumerate(notes):
            self.note(name, pitch, at + i * strum, length, gain, pan + (i - len(notes) / 2) * .018)

    def hit(self, name, at, gain, pan=0):
        if name not in self.drums:
            self.drums[name] = percussion(name, self.rng)
        self.add(self.drums[name], at, gain, pan, .03)
        self.events.append(dict(instrument=name, pitch=None, at=round(at, 5), length=0, gain=gain, pan=pan))

    def finish(self):
        for secs, gain, reverse in [(.041, .23, False), (.087, .15, True), (.151, .085, False), (.213, .04, True)]:
            delay = round(secs * RATE)
            self.mix[delay:] += self.room[:-delay, ::-1] * gain if reverse else self.room[:-delay] * gain
        # A restrained warm shelf removes brittle pick/click extremes, preserving the
        # transient body. Zero-phase offline shaping, not a sustained whistle oscillator.
        count = len(self.mix)
        fft_count = 1 << (count + 16384 - 1).bit_length()
        spectrum = np.fft.rfft(self.mix, n=fft_count, axis=0)
        f = np.fft.rfftfreq(fft_count, 1 / RATE)
        shape = 1 / np.sqrt(1 + (f / 4300) ** 6)
        shape *= f / np.sqrt(f * f + 27 ** 2)
        self.mix = np.fft.irfft(spectrum * shape[:, None], n=fft_count, axis=0)[:count]
        # Gentle transient rounding only. No samples or soundbanks are hidden here.
        self.mix = np.tanh(self.mix * 2.3) / 2.3
        self.mix -= self.mix.mean(axis=0)
        rms, peak = np.sqrt(np.mean(self.mix ** 2)), np.max(np.abs(self.mix))
        self.mix *= min(.108 / rms, .56 / peak)
        attack, release = round(.007 * RATE), round(.65 * RATE)
        self.mix[:attack] *= np.sin(np.linspace(0, math.pi / 2, attack))[:, None] ** 2
        self.mix[-release:] *= np.cos(np.linspace(0, math.pi / 2, release))[:, None] ** 2
        self.mix[0] = self.mix[-1] = 0
        return np.rint(self.mix * 32767).astype("<i2")


# NEW middle-register motif. Two-bar call/answer with breathing gaps; no V1 lead line.
CALL = [(0, 67, .29), (.5, 64, .24), (.875, 62, .24), (1.5, 64, .36),
        (2.5, 69, .25), (2.875, 67, .25), (3.5, 64, .27)]
ANSWER = [(.25, 60, .28), (1, 62, .28), (1.5, 64, .35), (2.25, 67, .30), (3, 62, .47)]
RISE = [(0, 64, .27), (.5, 67, .24), (.875, 69, .24), (1.5, 72, .35),
        (2.5, 71, .26), (3, 67, .28), (3.5, 64, .25)]
FALL = [(.25, 62, .28), (1, 65, .28), (1.5, 69, .34), (2.25, 67, .24), (2.75, 64, .24), (3.25, 60, .48)]
CHORDS = [[48, 55, 60, 64, 69], [45, 52, 57, 60, 67], [41, 48, 53, 57, 62], [43, 50, 55, 59, 64]]


def menu():
    s = Score("menu", 110, 48, 1.7, 2026102501)
    phrases = [CALL, ANSWER, RISE, FALL, CALL, ANSWER, RISE, FALL,
               [(0, 67, .32), (.75, 64, .30), (1.5, 62, .30), (2.5, 64, .40)],
               [(.25, 69, .30), (1, 67, .30), (2, 64, .38)], CALL,
               [(.25, 62, .26), (1, 64, .28), (1.5, 67, .28), (2.5, 60, 1.6)]]
    for bar in range(12):
        base, chord = bar * 4, CHORDS[bar % 4]
        if bar == 11:
            chord = CHORDS[0]
        root = chord[0] - 12 if chord[0] >= 45 else chord[0]
        for at, pitch, length, gain in [(0, root, .55, .19), (.84, root + 12, .18, .10),
                                       (1.5, root + 7, .32, .14), (2.5, root, .48, .16)]:
            s.note("rubber_bass", pitch, base + at, length, gain)
        # Off-beat strings and a couple of blocky EP touches leave holes for SFX.
        for at, gain in [(.48, .034), (1.98, .030), (3.24, .028)]:
            if bar in (8, 9) and at > 2:
                continue
            s.chord("muted_string", chord[1:], base + at, .25, gain, -.32)
        s.chord("warm_ep", chord[2:], base + .02, .62, .033, .27)
        if bar not in (8, 9):
            s.chord("warm_ep", chord[2:], base + 2.72, .35, .025, .27)
        lead = "body_guitar" if bar < 8 or bar == 10 else "wood_mallet"
        for at, pitch, length in phrases[bar]:
            s.note(lead, pitch, base + at + .025, length, .128 if lead == "body_guitar" else .101, .02, .12)
        if bar in (1, 3, 5, 7):
            for i, pitch in enumerate([chord[2], chord[3]]):
                s.note("wood_mallet", pitch, base + 3.55 + i * .20, .16, .042, .34)
        if bar in (8, 9):
            s.hit("wood_tick", base + 1, .032, -.18)
            s.hit("hand_drum", base + 3, .046, -.10)
            continue
        for at, gain in [(0, .13), (1.75, .078), (2.5, .105)]:
            s.hit("soft_kick", base + at, gain)
        s.hit("wood_rim", base + 1.04, .052, -.16)
        s.hit("brush_tap", base + 3.02, .067, -.12)
        for i in range(8):
            s.hit("paper_shake", base + i * .5 + (.035 if i % 2 else 0), .020 if i % 2 == 0 else .014, .29)
        if bar in (3, 7):
            s.hit("round_pop", base + 3.76, .030, .18)
    s.chord("body_guitar", [48, 55, 60, 64, 67], 46.55, 1.6, .040, -.20, .04)
    return s


def city():
    s = Score("city", 108, 48, 1.7, 2026102502)
    for bar in range(12):
        base, chord = bar * 4, CHORDS[bar % 4]
        if bar == 11:
            chord = CHORDS[0]
        root = chord[0] - 12 if chord[0] >= 45 else chord[0]
        line = [(0, root, .45), (.75, root + 12, .15), (1.5, root + 7, .25),
                (2.25, root, .30), (3.0, root + 10, .18), (3.65, root + 12, .19)]
        if bar in (7, 9):
            line = line[:4]
        for at, pitch, length in line:
            s.note("rubber_bass", pitch, base + at, length, .19 if at in (0, 2.25) else .13)
        for at in (.46, 1.74, 2.98):
            s.chord("warm_ep", chord[1:], base + at, .30, .034, -.30)
        for at, pitch in [(1.23, chord[2]), (2.48, chord[3]), (3.48, chord[1] + 12)]:
            s.note("muted_string", pitch, base + at, .16, .049, .34)
        phrase = [CALL, ANSWER, RISE, FALL][bar % 4]
        if bar in (4, 5, 8, 9):
            phrase = [(at + .08, pitch, length) for at, pitch, length in phrase if at < 2.7]
        if bar == 11:
            phrase = [(.25, 62, .25), (1, 64, .25), (1.5, 67, .30), (2.5, 60, 1.6)]
        for at, pitch, length in phrase:
            s.note("felt_keys", pitch, base + at + .02, length, .111, .02, .08)
        for at, gain in [(0, .16), (1.75, .09), (2.5, .115)]:
            s.hit("soft_kick", base + at, gain)
        s.hit("wood_rim", base + 1.03, .068, -.18)
        s.hit("brush_tap", base + 3.03, .084, -.14)
        for i in range(8):
            s.hit("paper_shake", base + i * .5 + (.025 if i % 2 else 0), .024 if i % 2 == 0 else .017, .28)
        if bar in (3, 7):
            s.hit("wood_tick", base + 3.75, .035, .18)
    s.chord("warm_ep", [48, 60, 64, 67, 69], 46.55, 1.7, .038, -.22)
    return s


def garden():
    s = Score("garden", 82, 32, 1.9, 2026102503)
    # Same call/answer, more space and a softer physical touch, without upper bells.
    garden_chords = [CHORDS[0], CHORDS[1], CHORDS[2], CHORDS[3],
                    CHORDS[0], [40, 47, 52, 55, 62], [38, 45, 50, 53, 60], CHORDS[0]]
    for bar in range(8):
        base, chord = bar * 4, garden_chords[bar]
        root = chord[0] - 12 if chord[0] >= 45 else chord[0]
        s.note("rubber_bass", root, base + .03, .9, .135)
        s.note("rubber_bass", root + 7, base + 2.55, .6, .075)
        # Sparse six-touch fingerpicking pattern, not a continuous high arpeggio.
        for i, (at, tone) in enumerate([(0, 0), (.68, 2), (1.3, 1), (2, 3), (2.68, 2), (3.35, 4)]):
            s.note("body_guitar", chord[tone], base + at + .025, .7, .061 if i % 2 == 0 else .048, -.30, .25)
        for pitch in chord[2:]:
            s.note("warm_ep", pitch, base + .08, 1.5, .022, .29, .22)
        phrase = [CALL, ANSWER, RISE, FALL][bar % 4]
        # Select the motif's main landmarks; keep its pitch identity and natural rests.
        phrase = [x for i, x in enumerate(phrase) if i in (0, 1, 2, 3, 4)]
        if bar == 7:
            phrase = [(.25, 62, .40), (1.1, 64, .40), (2.3, 60, 1.5)]
        for at, pitch, length in phrase:
            s.note("wood_mallet", pitch, base + at + .05, max(.4, length), .112, .07, .23)
        s.hit("hand_drum", base, .040, -.1)
        s.hit("wood_tick", base + 2.04, .023, -.2)
        for at in (.5, 1.5, 2.5):
            s.hit("paper_shake", base + at, .013, .24)
    s.chord("body_guitar", [48, 55, 60, 64, 67], 30.35, 1.8, .038, -.22, .06)
    return s


def medieval():
    s = Score("medieval", 276, 96, 1.9, 2026102504)  # 6/8 at dotted-quarter 92
    phrases = [
        [(0, 67, .70), (1, 64, .65), (2, 62, .65), (3, 64, 1.25), (5, 69, .65)],
        [(0, 67, .70), (1, 64, .65), (3, 60, .75), (4, 62, 1.10)],
        [(0, 64, .70), (1, 67, .65), (2, 69, .65), (3, 72, 1.25), (5, 71, .65)],
        [(0, 67, .70), (1, 64, .65), (3, 62, .75), (4, 60, 1.10)],
        [(0, 67, .70), (1, 64, .65), (2, 62, .65), (3, 64, 1.25), (5, 69, .65)],
        [(0, 67, .70), (1, 64, .65), (3, 60, .75), (4, 62, 1.10)],
        [(0, 65, .70), (1, 69, .65), (2, 67, .65), (3, 64, 1.25), (5, 62, .65)],
        [(0, 60, 1.1), (2, 64, .70), (3, 62, .75), (5, 60, .65)],
    ]
    folk_chords = [CHORDS[0], CHORDS[0], CHORDS[1], CHORDS[1], CHORDS[2], CHORDS[3], CHORDS[0], CHORDS[3]]
    for bar in range(16):
        base, chord = bar * 6, folk_chords[bar % 8]
        if bar == 15:
            chord = CHORDS[0]
        root = chord[0] - 12 if chord[0] >= 45 else chord[0]
        for at, pitch in [(0, root), (3, root + 7)]:
            s.note("rubber_bass", pitch, base + at + .02, 1.4, .15 if at == 0 else .10)
            s.hit("hand_drum", base + at, .067 if at == 0 else .045, -.08)
        for at, gain in [(1, .031), (2, .027), (4, .033), (5, .026)]:
            s.chord("lute_pluck", chord[1:], base + at, .55, gain, -.31, .035)
        phrase = phrases[bar % 8]
        if bar == 15:
            phrase = [(0, 62, .70), (1, 64, .65), (2, 67, .65), (3, 60, 5)]
        lead = "lute_pluck" if bar < 8 else "felt_keys"
        for at, pitch, length in phrase:
            s.note(lead, pitch, base + at + .035, length, .138 if lead == "lute_pluck" else .104, .05, .14)
        for at in (1, 2, 4, 5):
            s.hit("paper_shake", base + at, .018 if at in (1, 4) else .012, .27)
        s.hit("wood_tick", base + 4.04, .030, -.16)
        if bar in (3, 7, 11):
            s.hit("wood_rim", base + 5.35, .030, -.16)
    s.chord("lute_pluck", [48, 55, 60, 64, 67], 93.2, 5.8, .046, -.22, .09)
    return s


BUILDERS = dict(menu=menu, city=city, garden=garden, medieval=medieval)
TITLES = dict(menu="Pocket Whirl — tactile menu groove", city="Sidewalk Bounce — dry funky keys",
              garden="Pebble Hop — soft strings and wood", medieval="Jester's Pockets — comic plucked dance")


def filename(theme):
    return "Demo" + theme.capitalize() + "V2.wav"


def check(path):
    with wave.open(str(path), "rb") as f:
        assert f.getnchannels() == 2 and f.getsampwidth() == 2 and f.getframerate() == RATE
        frames = f.getnframes()
        pcm = f.readframes(frames)
    x = np.frombuffer(pcm, dtype="<i2").reshape(-1, 2).astype(np.float64) / 32767
    seconds, peak, rms = frames / RATE, float(np.max(np.abs(x))), float(np.sqrt(np.mean(x ** 2)))
    dc = float(np.max(np.abs(x.mean(axis=0))))
    assert 20 <= seconds <= 30 and .25 < peak <= .561 and .07 < rms < .13
    assert dc < .0005 and np.max(np.abs(x[[0, -1]])) == 0
    assert np.mean(x[:, 0] * x[:, 1]) > 0
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    spectrum = np.abs(np.fft.rfft(x.mean(axis=1))) ** 2
    high_share = float(spectrum[f > 4000].sum() / spectrum.sum())
    assert high_share < .10, "High-frequency mix became brittle"
    return dict(file=path.name, seconds=round(seconds, 4), channels=2, rate=RATE, bits=16,
                peak=round(peak, 6), rms=round(rms, 6), rmsDbFS=round(20 * math.log10(rms), 2),
                dc=round(dc, 8), energyAbove4kHzPercent=round(high_share * 100, 3),
                sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--check", action="store_true")
    p.add_argument("--theme", choices=BUILDERS)
    args = p.parse_args()
    themes = [args.theme] if args.theme else list(BUILDERS)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    results = []
    for theme in themes:
        score_meta = {}
        if not args.check:
            s = BUILDERS[theme]()
            pcm = s.finish()
            with wave.open(str(OUTPUT / filename(theme)), "wb") as f:
                f.setnchannels(2); f.setsampwidth(2); f.setframerate(RATE); f.writeframes(pcm.tobytes())
            score = dict(theme=theme, bpm=s.bpm, beats=s.beats,
                         beatUnit="eighth note" if theme == "medieval" else "quarter note", events=s.events)
            (OUTPUT / (theme + "-score.json")).write_text(json.dumps(score, indent=2) + "\n")
            score_meta = dict(eventCount=len(s.events), instruments=sorted({e['instrument'] for e in s.events}),
                              scoreSha256=hashlib.sha256(json.dumps(s.events).encode()).hexdigest())
        results.append(dict(themeId=theme, title=TITLES[theme], **check(OUTPUT / filename(theme)), **score_meta))
    if len(results) == 4:
        assert max(x['rmsDbFS'] for x in results) - min(x['rmsDbFS'] for x in results) < 2.0
    report = dict(status="revision_2_drafts_awaiting_user_listening", originalNotesAndSynthesis=True,
                  thirdPartySamplesOrBanks=False, resourcesReplaced=False, melodicFingerprintMidi=MOTIF,
                  noWindInstruments=True, clips=results)
    if not args.check:
        (OUTPUT / ("MusicV2Manifest.json" if not args.theme else "MusicV2-" + args.theme + "-prototype.json")).write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(dict(passed=True, **report), indent=2))


if __name__ == "__main__":
    main()
