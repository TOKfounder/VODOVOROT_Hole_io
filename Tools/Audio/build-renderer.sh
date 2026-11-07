#!/usr/bin/env bash
set -euo pipefail
task_audio_root="$(cd -- "$(dirname -- "$0")/../.." && pwd)"
task_audio_output="$task_audio_root/Builds/Stage3Review/Tools"
mkdir -p "$task_audio_output"
task_audio_flags=(-O2 -std=c++17)
# On the development host the default macOS 27 SDK advertises architectures
# unsupported by the installed linker. Use the installed compatible SDK locally;
# no system SDK selection or developer settings are changed.
if [[ "$(uname -s)" == Darwin && -d /Library/Developer/CommandLineTools/SDKs/MacOSX26.5.sdk ]]; then
    task_audio_flags+=(-isysroot /Library/Developer/CommandLineTools/SDKs/MacOSX26.5.sdk -mmacosx-version-min=15.0)
fi
"${CXX:-clang++}" "${task_audio_flags[@]}" "$task_audio_root/Tools/Audio/tsf-render.cpp" -o "$task_audio_output/tsf-render"
printf '%s\n' "$task_audio_output/tsf-render"
