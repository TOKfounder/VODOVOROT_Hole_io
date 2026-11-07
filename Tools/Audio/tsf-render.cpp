// Device-free audio authoring tool. TinySoundFont is separately licensed under MIT.
// Build: clang++ -O2 -std=c++17 Tools/Audio/tsf-render.cpp -o Builds/Stage3Review/Tools/tsf-render
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>
#define TSF_IMPLEMENTATION
#include "ThirdParty/TinySoundFont/tsf.h"

namespace {
constexpr int Rate = 44100;
struct Event { int64_t frame; int channel, midi; float velocity, pan; bool on; };
void u16(std::ostream& out, uint16_t value) {
    const char bytes[] = {char(value), char(value >> 8)}; out.write(bytes, 2);
}
void u32(std::ostream& out, uint32_t value) {
    const char bytes[] = {char(value), char(value >> 8), char(value >> 16), char(value >> 24)}; out.write(bytes, 4);
}
void writeWave(const std::string& path, const std::vector<float>& pcm, double gain) {
    std::ofstream out(path, std::ios::binary);
    if (!out) throw std::runtime_error("Cannot open output WAV: " + path);
    uint32_t bytes = static_cast<uint32_t>(pcm.size() * 2);
    out.write("RIFF", 4); u32(out, 36 + bytes); out.write("WAVEfmt ", 8); u32(out, 16);
    u16(out, 1); u16(out, 2); u32(out, Rate); u32(out, Rate * 4); u16(out, 4); u16(out, 16);
    out.write("data", 4); u32(out, bytes);
    for (float value : pcm) {
        const double safe = std::clamp(static_cast<double>(value) * gain, -1.0, 1.0);
        u16(out, static_cast<uint16_t>(static_cast<int16_t>(std::lround(safe * 32767))));
    }
    if (!out) throw std::runtime_error("Failed to write complete WAV.");
}
}

int main(int argc, char** argv) {
    tsf* synth = nullptr;
    try {
        if (argc < 4 || argc > 6) {
            std::cerr << "Usage: tsf-render bank.sf2 events.tsv output.wav [seconds] [presetIndex]\n"
                      << "Rows: startSeconds durationSeconds midi velocity(0..1) pan(-1..1)\n";
            return 2;
        }
        std::ifstream input(argv[2]);
        if (!input) throw std::runtime_error("Cannot open event score.");
        std::vector<Event> events;
        std::string line; int notes = 0, lineNumber = 0; int64_t lastOff = 0;
        while (std::getline(input, line)) {
            lineNumber++;
            if (line.empty() || line[0] == '#') continue;
            double start, duration; int midi; float velocity, pan;
            std::istringstream row(line);
            if (!(row >> start >> duration >> midi >> velocity >> pan) || !std::isfinite(start) || !std::isfinite(duration) ||
                !std::isfinite(velocity) || !std::isfinite(pan) || start < 0 || duration <= 0 || start + duration > 600 ||
                midi < 0 || midi > 127 || velocity <= 0 || velocity > 1 || pan < -1 || pan > 1 || notes >= 10000)
                throw std::runtime_error("Invalid note at score line " + std::to_string(lineNumber));
            int64_t at = std::llround(start * Rate), off = std::max(at + 1, std::llround((start + duration) * Rate));
            // Separate channels preserve overlapping same-pitch notes and each note's pan.
            events.push_back({at, notes, midi, velocity, pan, true});
            events.push_back({off, notes, midi, 0, pan, false});
            lastOff = std::max(lastOff, off); notes++;
        }
        double seconds = argc > 4 ? std::stod(argv[4]) : lastOff / double(Rate) + 2;
        if (!std::isfinite(seconds) || seconds <= 0 || seconds > 600) throw std::runtime_error("Duration must be 0..600 seconds.");
        int64_t frames = std::llround(seconds * Rate);
        if (lastOff > frames) throw std::runtime_error("Explicit duration truncates scored notes; extend the stem.");
        int preset = argc > 5 ? std::stoi(argv[5]) : 0;
        synth = tsf_load_filename(argv[1]);
        if (!synth) throw std::runtime_error("Cannot load the SF2 bank.");
        if (preset < 0 || preset >= tsf_get_presetcount(synth)) throw std::runtime_error("Preset index absent from SF2 bank.");
        tsf_set_output(synth, TSF_STEREO_INTERLEAVED, Rate, -6);
        if (!tsf_set_max_voices(synth, 512)) throw std::runtime_error("Cannot allocate render voices.");
        std::sort(events.begin(), events.end(), [](const Event& a, const Event& b) {
            if (a.frame != b.frame) return a.frame < b.frame;
            if (a.on != b.on) return !a.on;
            return a.channel < b.channel;
        });
        std::vector<float> pcm(static_cast<size_t>(frames) * 2, 0);
        size_t next = 0; int64_t at = 0;
        while (at < frames) {
            while (next < events.size() && events[next].frame <= at) {
                const Event& note = events[next++];
                if (note.on) {
                    if (!tsf_channel_set_presetindex(synth, note.channel, preset) ||
                        !tsf_channel_set_pan(synth, note.channel, (note.pan + 1) * .5f) ||
                        !tsf_channel_note_on(synth, note.channel, note.midi, note.velocity))
                        throw std::runtime_error("Could not start a scored voice.");
                } else tsf_channel_note_off(synth, note.channel, note.midi);
            }
            int64_t until = next < events.size() ? events[next].frame : frames;
            int count = static_cast<int>(std::min<int64_t>({1024, frames - at, until - at}));
            if (count <= 0) continue;
            tsf_render_float(synth, pcm.data() + at * 2, count, 0); at += count;
        }
        double peak = 0, sum = 0;
        for (float value : pcm) {
            if (!std::isfinite(value)) throw std::runtime_error("Renderer produced a nonfinite sample.");
            peak = std::max(peak, std::abs(double(value))); sum += double(value) * value;
        }
        // Render in unbounded float first; attenuate only if needed before PCM conversion.
        double gain = peak > .9 ? .9 / peak : 1;
        writeWave(argv[3], pcm, gain);
        std::cout << std::setprecision(10) << "{\"rate\":44100,\"channels\":2,\"bits\":16,\"notes\":" << notes
                  << ",\"seconds\":" << frames / double(Rate) << ",\"rawFloatPeak\":" << peak << ",\"pcmGain\":" << gain
                  << ",\"peak\":" << peak * gain << ",\"rms\":" << std::sqrt(sum / pcm.size()) * gain << "}\n";
        tsf_close(synth); return 0;
    } catch (const std::exception& error) {
        if (synth) tsf_close(synth);
        std::cerr << "tsf-render: " << error.what() << "\n"; return 1;
    }
}
