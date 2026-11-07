#!/usr/bin/env python3
"""Four original Vodovorot audition cues, composed and synthesized offline.

No recordings, sample libraries, downloaded MIDI, pretrained models, API keys or
network calls are used. The score, instruments, percussion, stereo room and mix
are defined here. Requires Python 3 + numpy. Run with --check to validate the
committed PCM without writing. These short demos are NOT approved release music.
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
ROOT = Path(__file__).resolve().parents[4]
OUTPUT = ROOT / "Builds/Stage3Review/MusicV1"
REPORT = OUTPUT / "Stage3MusicDemoManifest.json"
TAU = 2 * math.pi

# Common fingerprint, stated in each arrangement and rhythmically rephrased:
# C E G A G E | D E G ... C. All pitches and accompaniment are original here.
MOTIF = [72, 76, 79, 81, 79, 76, 74, 76, 79, 72]


def hz(midi: int) -> float:
    return 440.0 * 2 ** ((midi - 69) / 12)


def soft_edges(signal: np.ndarray, attack: float = .004, release: float = .035) -> np.ndarray:
    a = min(len(signal), max(2, int(attack * RATE)))
    r = min(len(signal), max(2, int(release * RATE)))
    signal[:a] *= np.sin(np.linspace(0, math.pi / 2, a)) ** 2
    signal[-r:] *= np.sin(np.linspace(math.pi / 2, 0, r)) ** 2
    return signal


def adsr(t: np.ndarray, held: float, attack: float, decay: float, sustain: float, release: float) -> np.ndarray:
    env = np.where(t < attack, t / attack,
                   sustain + (1 - sustain) * np.exp(-np.maximum(0, t - attack) / decay))
    env *= np.exp(-np.maximum(0, t - held) / release)
    return env


def low_noise(n: int, rng: np.random.Generator, width: int = 9) -> np.ndarray:
    noise = rng.standard_normal(n)
    return np.convolve(noise, np.ones(width) / width, mode="same")


def instrument(name: str, midi: int, held: float, rng: np.random.Generator) -> np.ndarray:
    """Acoustic-inspired additive / FM resonators, not a single oscillator beep."""
    f = hz(midi)
    tails = {"bell": 1.75, "vibes": 1.0, "harp": .9, "piano": .65,
             "lute": .35, "guitar": .45, "rhodes": .75, "pad": .7}
    t = np.arange(int((held + tails.get(name, .2)) * RATE)) / RATE
    phase = TAU * f * t
    out = np.zeros_like(t)
    if name in ("harp", "lute", "guitar", "clav"):
        bright = {"harp": .65, "lute": 1.15, "guitar": .85, "clav": 1.35}[name]
        decay = {"harp": 1.15, "lute": .40, "guitar": .65, "clav": .19}[name]
        for k in range(1, 13):
            # Slight string inharmonicity and faster high partial loss make a pluck.
            amp = math.sin(k * .73) / k ** bright
            out += amp * np.sin(phase * k * math.sqrt(1 + .000035 * k * k)) * np.exp(-t * (1 + k * .31) / decay)
        out += low_noise(len(t), rng, 5) * .055 * np.exp(-t * 115)
        out *= np.exp(-np.maximum(0, t - held) / .14)
        soft_edges(out, .002, .04)
    elif name == "piano":
        for k, amp in enumerate((1, .48, .26, .18, .09, .06, .035), 1):
            stiffness = math.sqrt(1 + .00014 * k * k)
            decay = (1.8 if midi < 60 else 1.2) / (1 + .3 * k)
            out += amp * np.sin(phase * k * stiffness) * np.exp(-t / decay)
            out += amp * .18 * np.sin(phase * k * stiffness * 1.0017) * np.exp(-t / decay)
        out *= np.exp(-np.maximum(0, t - held) / .22)
        soft_edges(out, .003, .05)
    elif name == "rhodes":
        # Bell tine attack dissolves into a warm low-index FM body.
        index = 1.3 * np.exp(-t * 7) + .18
        out = np.sin(phase + index * np.sin(phase * 2.003))
        out += .24 * np.sin(phase * 3.001) * np.exp(-t * 5)
        out *= np.exp(-t / 1.2) * np.exp(-np.maximum(0, t - held) / .23)
        out *= 1 + .035 * np.sin(TAU * 4.7 * t)
        soft_edges(out, .006, .055)
    elif name in ("reed", "accordion", "recorder", "muted"):
        vibrato = .0022 * np.sin(TAU * 5.1 * t) * (1 - np.exp(-t * 8))
        p = phase + f / 5.1 * .0022 * np.sin(TAU * 5.1 * t) * (1 - np.exp(-t * 8))
        if name == "reed":
            harmonics = (1, .12, .40, .05, .12, .025, .035)
        elif name == "accordion":
            harmonics = (1, .40, .33, .15, .10, .055, .025)
        elif name == "muted":
            harmonics = (1, .29, .16, .15, .06, .035)
        else:
            harmonics = (1, .10, .10, .018)
        for k, amp in enumerate(harmonics, 1):
            out += amp * np.sin(p * k)
            if name == "accordion":
                out += amp * .32 * np.sin(p * k * 1.003)
        breath = low_noise(len(t), rng, 23) * (.038 if name == "recorder" else .012)
        env = adsr(t, held, .025 if name != "muted" else .014, .1, .72, .055)
        out = (out + breath) * env
        soft_edges(out, .012, .04)
    elif name in ("bell", "vibes"):
        ratios = (1, 2.756, 5.404, 8.933) if name == "bell" else (1, 3.98, 9.18)
        amps = (1, .32, .10, .035) if name == "bell" else (1, .21, .055)
        for k, (ratio, amp) in enumerate(zip(ratios, amps)):
            out += amp * np.sin(phase * ratio) * np.exp(-t * (1 + k * 1.7) / (1.1 if name == "bell" else .85))
        if name == "vibes":
            out *= 1 + .045 * np.sin(TAU * 5.5 * t)
        soft_edges(out, .002, .09)
    elif name in ("bass", "upright"):
        out = np.sin(phase) + .32 * np.sin(phase * 2) * np.exp(-t * 7)
        out += .12 * np.sin(phase * 3) * np.exp(-t * 12)
        if name == "upright":
            out += .10 * low_noise(len(t), rng, 7) * np.exp(-t * 100)
            out *= np.exp(-t * 2.4) * np.exp(-np.maximum(0, t - held) * 18)
        else:
            out *= adsr(t, held, .006, .12, .58, .075)
        soft_edges(out, .004, .04)
    elif name == "pad":
        for ratio, amp in ((1, 1), (2.001, .15), (.998, .25), (3, .045)):
            out += amp * np.sin(phase * ratio)
        out *= adsr(t, held, .30, .5, .65, .22)
        soft_edges(out, .10, .15)
    else:
        raise ValueError(name)
    peak = max(.01, np.max(np.abs(out)))
    return (out / peak).astype(np.float32)


def drum(kind: str, rng: np.random.Generator) -> np.ndarray:
    durations = {"kick": .32, "snare": .20, "brush": .19, "hat": .065,
                 "openhat": .17, "rim": .08, "shaker": .085, "hand": .20, "tamb": .12}
    t = np.arange(int(durations[kind] * RATE)) / RATE
    noise = rng.standard_normal(len(t))
    high = noise - np.convolve(noise, np.ones(7) / 7, mode="same")
    if kind == "kick":
        phase = TAU * (48 * t + 55 * .026 * (1 - np.exp(-t / .026)))
        out = np.sin(phase) * np.exp(-t * 17) + .055 * high * np.exp(-t * 170)
    elif kind in ("snare", "brush"):
        body = np.sin(TAU * 175 * t) * np.exp(-t * 38)
        grain = np.convolve(noise, np.ones(3) / 3, mode="same")
        out = .38 * body + grain * np.exp(-t * (22 if kind == "brush" else 31))
        if kind == "brush":
            out *= (1 - np.exp(-t * 95))
    elif kind in ("hat", "openhat", "shaker", "tamb"):
        env = np.exp(-t * (70 if kind == "hat" else 28 if kind == "openhat" else 43))
        if kind == "shaker":
            env *= np.sin(np.minimum(1, t / .025) * math.pi / 2)
        out = .35 * high * env
        if kind == "tamb":
            for freq in (2801, 3727, 4369, 5671):
                out += .12 * np.sin(TAU * freq * t) * np.exp(-t * 40)
    elif kind == "hand":
        out = (np.sin(TAU * (135 * t + 13 * .015 * (1 - np.exp(-t / .015))))
               + .33 * np.sin(TAU * 221 * t)) * np.exp(-t * 25)
        out += .15 * high * np.exp(-t * 90)
    else:
        out = (.5 * np.sin(TAU * 840 * t) + .3 * np.sin(TAU * 1265 * t)
               + .18 * high) * np.exp(-t * 90)
    soft_edges(out, .0008, .01)
    return (out / max(.01, np.max(np.abs(out)))).astype(np.float32)


class Arrangement:
    def __init__(self, theme: str, bpm: float, beats: int, tail: float, seed: int):
        self.theme = theme
        self.bpm = bpm
        self.beat = 60 / bpm
        self.beats = beats
        self.rng = np.random.default_rng(seed)
        self.mix = np.zeros((round((beats * self.beat + tail) * RATE), 2), np.float64)
        self.room = np.zeros_like(self.mix)
        self.events = []
        self.drum_cache = {}

    def put(self, signal: np.ndarray, beat: float, gain: float, pan: float, room: float):
        at = round(beat * self.beat * RATE)
        if at < 0:
            signal, at = signal[-at:], 0
        count = min(len(signal), len(self.mix) - at)
        if count <= 0:
            return
        # Constant-power panning. Width is restrained for mobile mono compatibility.
        stereo = signal[:count, None] * np.array([math.cos((pan + 1) * math.pi / 4),
                                                math.sin((pan + 1) * math.pi / 4)])
        self.mix[at:at + count] += stereo * gain
        self.room[at:at + count] += stereo * gain * room

    def note(self, name: str, pitch: int, at: float, length: float, gain: float, pan: float = 0, room: float = .18):
        # Small deterministic velocity differences, no timing random walk.
        velocity = float(self.rng.uniform(.94, 1.04))
        self.put(instrument(name, pitch, length * self.beat, self.rng), at, gain * velocity, pan, room)
        self.events.append([name, pitch, round(at, 4), round(length, 4)])

    def chord(self, name: str, pitches: list[int], at: float, length: float, gain: float, pan: float, strum: float = .018):
        for i, pitch in enumerate(pitches):
            self.note(name, pitch, at + i * strum, length, gain, pan + (i - len(pitches) / 2) * .025)

    def hit(self, name: str, at: float, gain: float, pan: float = 0):
        if name not in self.drum_cache:
            self.drum_cache[name] = drum(name, self.rng)
        self.put(self.drum_cache[name], at, gain, pan, .025)
        self.events.append([name, None, round(at, 4), 0])

    def finish(self) -> np.ndarray:
        # A sparse, damped stereo early-reflection room rather than a wash.
        for delay, level, swap in ((.049, .27, False), (.083, .20, True), (.137, .14, False),
                                    (.211, .10, True), (.307, .065, False)):
            offset = round(delay * RATE)
            signal = self.room[:-offset, ::-1] if swap else self.room[:-offset]
            self.mix[offset:] += signal * level
        # Remove subsonic/DC and apply gentle analog-style rounding, with headroom.
        self.mix -= np.mean(self.mix, axis=0)
        # The sparse garden plucks need gentle transient rounding to match audition
        # loudness without raising their isolated attacks above the same peak cap.
        drive = 6.0 if self.theme == "garden" else 1.5
        self.mix = np.tanh(self.mix * drive) / drive
        rms = np.sqrt(np.mean(self.mix ** 2))
        self.mix *= min(.120 / rms, .57 / np.max(np.abs(self.mix)))
        # Same perceived energy target across all four cues, no brickwall limiting.
        fade = round(.55 * RATE)
        self.mix[-fade:] *= np.cos(np.linspace(0, math.pi / 2, fade))[:, None] ** 2
        self.mix[:220] *= np.sin(np.linspace(0, math.pi / 2, 220))[:, None] ** 2
        self.mix[0] = self.mix[-1] = 0
        return np.rint(np.clip(self.mix, -1, 1) * 32767).astype("<i2")


def menu() -> Arrangement:
    a = Arrangement("menu", 94, 32, 1.8, 31001)
    chords = [[60, 64, 67, 69], [57, 60, 64, 67], [62, 65, 69, 72], [59, 65, 69, 76],
              [59, 64, 67, 74], [61, 67, 69, 76], [60, 65, 69, 74], [59, 65, 69, 74]]
    bass = [[36, 40, 43, 47], [45, 48, 52, 51], [38, 41, 45, 42], [43, 47, 50, 51],
            [40, 43, 47, 46], [45, 49, 52, 51], [38, 41, 45, 42], [43, 47, 36, 43]]
    phrases = [
        [(0.12, 72, .42), (.68, 76, .33), (1.12, 79, .68), (2.12, 81, .38), (2.68, 79, .32), (3.12, 76, .57)],
        [(.12, 74, .48), (.78, 76, .30), (1.43, 79, .95), (3.12, 72, .52)],
        [(.12, 74, .42), (.68, 77, .32), (1.12, 81, .60), (2.12, 83, .34), (2.68, 81, .30), (3.12, 77, .56)],
        [(.12, 79, .43), (.78, 77, .30), (1.43, 74, .75), (2.68, 71, .32), (3.12, 67, .54)],
        [(.12, 76, .42), (.68, 79, .32), (1.12, 83, .60), (2.12, 84, .34), (2.68, 83, .30), (3.12, 79, .56)],
        [(.12, 81, .49), (.78, 79, .30), (1.43, 76, .95), (3.12, 73, .52)],
        [(.12, 74, .42), (.68, 77, .32), (1.12, 81, .60), (2.12, 79, .34), (2.68, 77, .30), (3.12, 74, .56)],
        [(.12, 71, .40), (.68, 74, .28), (1.12, 79, .58), (2.12, 76, .30), (2.68, 74, .28), (3.12, 72, 1.50)],
    ]
    for bar in range(8):
        base = bar * 4
        for beat, pitch in enumerate(bass[bar]):
            a.note("upright", pitch, base + beat + .015, .70, .21, -.06, .08)
        for at, vel in ((.04, .065), (1.68, .053), (2.68, .055)):
            a.chord("piano", chords[bar], base + at, .46 if at else .76, vel, -.25)
        for beat in range(4):
            a.hit("hat", base + beat, .026 if beat % 2 == 0 else .032, .28)
            a.hit("hat", base + beat + .67, .017, .28)
        a.hit("brush", base + 1, .060, -.12)
        a.hit("brush", base + 3, .067, -.12)
        a.hit("kick", base, .085)
        if bar % 2:
            a.hit("kick", base + 2.67, .055)
        for at, pitch, length in phrases[bar]:
            a.note("reed", pitch, base + at, length, .12, .06, .24)
        if bar in (1, 3, 5):
            for i, pitch in enumerate((chords[bar][-1] + 12, chords[bar][-2] + 12)):
                a.note("vibes", pitch, base + 2.68 + i * .53, .30, .038, .38, .26)
    a.chord("piano", [48, 60, 64, 67, 69], 31.10, 1.5, .065, -.22)
    return a


def city() -> Arrangement:
    a = Arrangement("city", 96, 32, 1.7, 31002)
    chords = [[60, 64, 67, 71, 74], [57, 60, 64, 67, 71], [60, 62, 65, 69, 76], [59, 65, 69, 74, 76],
              [60, 64, 67, 71, 74], [57, 60, 64, 67, 71], [60, 62, 65, 69, 76], [60, 64, 67, 69, 74]]
    roots = [36, 33, 38, 31, 36, 33, 38, 36]
    phrases = [
        [(.42, 72, .50), (1.20, 76, .30), (1.74, 79, .45), (2.48, 81, .32), (3.00, 79, .25), (3.48, 76, .35)],
        [(.24, 74, .45), (1.0, 76, .27), (1.72, 79, .88), (3.23, 72, .40)],
        [(.42, 74, .50), (1.20, 77, .30), (1.74, 81, .45), (2.48, 84, .32), (3.0, 81, .25), (3.48, 77, .35)],
        [(.24, 79, .45), (1.0, 77, .27), (1.72, 74, .88), (3.23, 71, .40)],
        [(.42, 72, .50), (1.20, 76, .30), (1.74, 79, .45), (2.48, 81, .32), (3.0, 79, .25), (3.48, 76, .35)],
        [(.24, 74, .45), (1.0, 76, .27), (1.72, 79, .88), (3.23, 84, .40)],
        [(.42, 81, .50), (1.20, 79, .30), (1.74, 77, .45), (2.48, 76, .32), (3.0, 74, .25), (3.48, 71, .35)],
        [(.24, 72, .58), (1.20, 76, .27), (1.72, 79, .55), (2.72, 72, 1.8)],
    ]
    for bar in range(8):
        base, root = bar * 4, roots[bar]
        for at, pitch, length, gain in [(0, root, .62, .22), (.78, root + 12, .20, .15),
                                       (1.5, root + 7, .35, .18), (2.25, root, .42, .20),
                                       (3.0, root + 12, .30, .17), (3.65, root + 10, .25, .14)]:
            a.note("bass", pitch, base + at, length, gain, -.035, .015)
        for at, gain in ((.02, .045), (1.48, .040), (2.74, .045)):
            a.chord("rhodes", chords[bar], base + at, .53, gain, -.29)
        for at, pitch in ((.75, chords[bar][1]), (1.75, chords[bar][2]), (2.5, chords[bar][3]), (3.5, chords[bar][2])):
            a.note("clav", pitch, base + at, .21, .041, .32, .10)
        for at, gain in ((0, .16), (1.75, .10), (2.5, .13)):
            a.hit("kick", base + at, gain)
        a.hit("snare", base + 1, .11, -.09)
        a.hit("snare", base + 3, .12, -.09)
        for i in range(8):
            a.hit("hat", base + i * .5, .030 if i % 2 == 0 else .021, .25)
        if bar in (3, 7):
            a.hit("openhat", base + 3.5, .035, .25)
            a.hit("rim", base + 3.75, .035, -.14)
        for at, pitch, length in phrases[bar]:
            a.note("muted", pitch, base + at, length, .113, .06, .19)
    a.chord("rhodes", [48, 60, 64, 67, 71, 74], 30.72, 2.0, .047, -.20)
    return a


def garden() -> Arrangement:
    a = Arrangement("garden", 82, 32, 2.0, 31003)
    chords = [[48, 55, 60, 64, 71], [45, 52, 57, 60, 67], [41, 48, 53, 57, 64], [43, 50, 55, 59, 64],
              [48, 55, 60, 64, 71], [40, 47, 52, 55, 62], [38, 45, 50, 53, 60], [48, 55, 60, 64, 69]]
    melody = [
        [(0.12, 72, 1.1), (1.50, 76, 1.0), (2.85, 79, .75)],
        [(.10, 81, .95), (1.45, 79, 1.0), (2.90, 76, .75)],
        [(.12, 74, 1.1), (1.50, 76, .9), (2.85, 79, .75)],
        [(.10, 76, .95), (1.5, 74, .9), (2.85, 72, .85)],
        [(.12, 72, 1.1), (1.5, 76, .9), (2.85, 79, .75)],
        [(.10, 83, .95), (1.45, 79, 1.0), (2.90, 76, .75)],
        [(.12, 81, 1.1), (1.50, 77, .9), (2.85, 74, .75)],
        [(.10, 76, .95), (1.45, 74, .7), (2.65, 72, 2.1)],
    ]
    order = [0, 2, 1, 3, 2, 4, 3, 2]
    for bar in range(8):
        base, chord = bar * 4, chords[bar]
        a.note("upright", chord[0] - 12, base + .02, 2.7, .13, -.02, .06)
        a.note("upright", chord[1] - 12, base + 2.05, 1.5, .075, -.02, .06)
        for i, tone in enumerate(order):
            a.note("guitar", chord[tone] + (12 if tone == 0 and i > 0 else 0),
                   base + i * .5 + .025, .65, .066 if i % 2 == 0 else .052, -.33, .30)
        for pitch in chord[1:4]:
            a.note("pad", pitch, base, 3.8, .018, .28, .35)
        for at, pitch, length in melody[bar]:
            a.note("harp", pitch, base + at, length, .136, .12, .36)
        if bar % 2 == 0:
            a.note("bell", chord[-1] + 12, base + 3.1, .4, .043, .38, .40)
        for at in (.5, 1.5, 2.5, 3.5):
            a.hit("shaker", base + at, .014, .20)
        if bar in (1, 3, 5):
            a.note("vibes", chord[2] + 12, base + 3.55, .25, .025, -.12, .35)
    a.chord("harp", [48, 60, 64, 67, 72], 30.6, 2.4, .042, -.16, .10)
    return a


def medieval() -> Arrangement:
    # Unit is an eighth note: 264 eighths/min = 88 dotted quarters/min, 6/8.
    a = Arrangement("medieval", 264, 96, 1.8, 31004)
    chords = [[48, 55, 60, 64, 67], [48, 55, 60, 64, 67], [45, 52, 57, 60, 64], [45, 52, 57, 60, 64],
              [41, 48, 53, 57, 60], [43, 50, 55, 59, 62], [48, 55, 60, 64, 67], [43, 50, 55, 59, 62]]
    tune = [
        [(0, 72, .8), (1, 76, .8), (2, 79, .8), (3, 81, .8), (4, 79, .8), (5, 76, .8)],
        [(0, 74, 1.6), (2, 76, .8), (3, 79, 1.65), (5, 72, .8)],
        [(0, 72, .8), (1, 76, .8), (2, 81, .8), (3, 84, 1.5), (5, 81, .8)],
        [(0, 79, 1.6), (2, 76, .8), (3, 72, 1.65), (5, 69, .8)],
        [(0, 72, .8), (1, 77, .8), (2, 81, .8), (3, 84, .8), (4, 81, .8), (5, 77, .8)],
        [(0, 79, 1.6), (2, 77, .8), (3, 74, 1.65), (5, 71, .8)],
        [(0, 72, .8), (1, 76, .8), (2, 79, .8), (3, 81, .8), (4, 79, .8), (5, 76, .8)],
        [(0, 74, .8), (1, 76, .8), (2, 74, .8), (3, 71, 1.5), (5, 67, .8)],
    ]
    for bar in range(16):
        base, chord = bar * 6, chords[bar % 8]
        if bar == 15:
            chord = chords[0]
        for at, pitch in ((0, chord[0] - 12), (3, chord[1] - 12)):
            a.note("upright", pitch, base + at, 2.3, .16, -.04, .06)
            a.hit("hand", base + at, .072 if at == 0 else .050, -.08)
        for at, gain in ((1, .041), (2, .035), (4, .043), (5, .036)):
            a.chord("lute", chord[1:], base + at, .83, gain, -.30, .035)
        for at in (1, 2, 4, 5):
            a.hit("tamb", base + at, .020 if at in (1, 4) else .014, .29)
        phrase = tune[bar % 8]
        if bar == 15:
            phrase = [(0, 74, .8), (1, 76, .8), (2, 79, .8), (3, 72, 5.0)]
        lead = "accordion" if bar < 8 else "recorder"
        for at, pitch, length in phrase:
            a.note(lead, pitch, base + at + .04, length, .099 if lead == "accordion" else .135, .06, .22)
        # A light second voice answers, without doubling every melody note.
        if bar in (3, 7, 11):
            for i, pitch in enumerate((chord[2] + 12, chord[3] + 12, chord[4] + 12)):
                a.note("recorder" if bar < 8 else "accordion", pitch, base + 3 + i, .70, .035, .32, .26)
        if bar in (7, 15):
            a.hit("rim", base + 4.5, .038, -.18)
            a.hit("hand", base + 5.25, .036, -.08)
    a.chord("lute", [48, 55, 60, 64, 67, 72], 93.05, 5.5, .056, -.24, .06)
    return a


BUILDERS = {"menu": menu, "city": city, "garden": garden, "medieval": medieval}
TITLES = {"menu": "A Little Whirl — menu swing", "city": "Sidewalk Whirl — city light funk",
          "garden": "Dewdrop Whirl — garden plucks", "medieval": "Jester's Whirl — comic folk"}


def filename(theme: str) -> str:
    return "Demo" + theme.capitalize() + ".wav"


def inspect_pcm(path: Path) -> dict:
    with wave.open(str(path), "rb") as f:
        channels, width, rate, frames = f.getnchannels(), f.getsampwidth(), f.getframerate(), f.getnframes()
        payload = f.readframes(frames)
    assert channels == 2 and width == 2 and rate == RATE, f"Wrong PCM format: {path}"
    x = np.frombuffer(payload, dtype="<i2").reshape(-1, 2).astype(np.float64) / 32767
    peak = float(np.max(np.abs(x)))
    rms = float(np.sqrt(np.mean(x ** 2)))
    dc = float(np.max(np.abs(x.mean(axis=0))))
    seconds = frames / rate
    assert 20 <= seconds <= 30, f"Not a 20–30 second audition: {seconds}"
    assert 0.35 < peak <= .571, f"Peak/headroom changed: {peak}"
    assert .075 < rms < .155, f"RMS outside musical demo range: {rms}"
    assert dc < .0005, f"DC bias: {dc}"
    assert np.max(np.abs(x[0])) == np.max(np.abs(x[-1])) == 0, "Non-silent endpoints"
    assert float(np.mean(x[:, 0] * x[:, 1])) > 0, "Mono downmix may cancel"
    # A real arrangement must have activity across the cue, not one long tone/tail.
    windows = x[:(frames // RATE) * RATE].reshape(-1, RATE, 2)
    active_rms = np.sqrt(np.mean(windows ** 2, axis=(1, 2)))
    assert np.count_nonzero(active_rms > .025) >= seconds - 4, "Insufficient musical activity"
    return {"file": path.name, "seconds": round(seconds, 4), "rate": rate, "channels": channels,
            "bits": width * 8, "peak": round(peak, 6), "peakDbFS": round(20 * math.log10(peak), 2),
            "rms": round(rms, 6), "rmsDbFS": round(20 * math.log10(rms), 2),
            "dc": round(dc, 8), "pcmBytes": len(payload),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Read-only check of committed demos")
    parser.add_argument("--theme", choices=list(BUILDERS), help="Regenerate one audition only")
    args = parser.parse_args()
    themes = [args.theme] if args.theme else list(BUILDERS)
    scores = {}
    if not args.check:
        OUTPUT.mkdir(parents=True, exist_ok=True)
        for theme in themes:
            a = BUILDERS[theme]()
            pcm = a.finish()
            with wave.open(str(OUTPUT / filename(theme)), "wb") as f:
                f.setnchannels(2)
                f.setsampwidth(2)
                f.setframerate(RATE)
                f.writeframes(pcm.tobytes())
            scores[theme] = {"eventCount": len(a.events), "instrumentFamilies": sorted({e[0] for e in a.events}),
                             "tempo": a.bpm, "beatUnit": "eighth note" if theme == "medieval" else "quarter note",
                             "scoreSha256": hashlib.sha256(json.dumps(a.events).encode()).hexdigest()}
    checks = [{"themeId": theme, "title": TITLES[theme], **inspect_pcm(OUTPUT / filename(theme)),
               **scores.get(theme, {})} for theme in themes]
    rms_values = [c["rmsDbFS"] for c in checks]
    assert max(rms_values) - min(rms_values) < 2.0, "Demo energy differs by more than 2 dB"
    if not args.check and not args.theme:
        REPORT.write_text(json.dumps({"status": "audition_demos_awaiting_user_approval", "originalComposition": True,
            "synthesisSource": "Assets/Scripts/Stage3/Editor/Stage3MusicComposition.py",
            "sharedMotifMidi": MOTIF, "targetRmsDbFS": -18.4, "releaseTracksCreated": False,
            "clips": checks}, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"passed": True, "checks": checks}, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
