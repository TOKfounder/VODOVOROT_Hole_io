# Offline music authoring

This directory contains the source tool and recorded source instruments for the
third music audition. It is outside Unity `Assets`: the renderer and SoundFonts
are not shipped in the game. Unity receives only the rendered, reviewed WAVs.

Build the device-free renderer with `bash Tools/Audio/build-renderer.sh`. It
requires a C++17 compiler and standard C/C++ libraries, without an audio device,
AudioUnit, package installation or system configuration. The output executable
is under the ignored `Builds/Stage3Review/Tools` directory.

The renderer uses [TinySoundFont](https://github.com/schellingb/TinySoundFont),
pinned to commit `853a0a171759f1ddba0de1442133a75912bbeffa`. Its unmodified header,
MIT license and SHA-256 provenance are retained under `ThirdParty/TinySoundFont`.
It renders a bank and timed note events in float, then attenuates if necessary
before conversion to stereo 44.1 kHz / 16-bit PCM. No intermediate clipping is
used. Standard output records the raw float peak, conversion gain and RMS.

Command: `tsf-render bank.sf2 events.tsv output.wav [seconds] [presetIndex]`.
Each non-comment TSV row has five columns: start seconds, held seconds, MIDI
note, velocity 0–1, pan −1–1. Distinct channels preserve overlapping notes and
per-note pan. A requested stem duration may include release tail and must not
truncate scored note-off events.

`Samples/` retains the actual FreePats nylon guitar, ukulele and finger bass
banks, original CC0 licenses and recording descriptions. `SampleProvenance.json`
records authors, versions, official download URLs and hashes. These are recorded
instruments used to perform a new score, not excerpts from another soundtrack:

- [Spanish classical guitar](https://freepats.zenvoid.org/Guitar/acoustic-guitar.html)
- [Ukulele](https://freepats.zenvoid.org/GuitarFamily/ukulele.html)
- [Fingered electric bass](https://freepats.zenvoid.org/ElectricGuitar/clean-electric-bass.html)

The original score/mix source is
`Assets/Scripts/Stage3/Editor/Stage3MusicCompositionV3.py`. It writes its stems,
note score, manifest and listening prototype to `Builds/Stage3Review/MusicV3`.
Python with numpy is required. `--check` verifies existing audio; regeneration
uses `--renderer Builds/Stage3Review/Tools/tsf-render`. The source banks above
remain available after cleaning generated `Builds` outputs.

Actual renderer checks include sample onset timing, C4/G4 pitch fundamentals,
independent stereo pan, absence of clipping and identical bytes across repeated
renders. These establish the rendering path; the author evaluates the music.
