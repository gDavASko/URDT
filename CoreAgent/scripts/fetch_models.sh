#!/usr/bin/env bash
# Downloads the local sense models used by URDT L3 (vision, hearing) into CoreAgent/models.
# Models are not tracked in git (~8 GB). Existing files are skipped.
# llama.cpp servers (CoreAgent/tools/llama.cpp-cuda, tools/llama.cpp) come from
# https://github.com/ggml-org/llama.cpp/releases (build b11120: win-cuda-12.4-x64 + cudart, win-vulkan-x64).
set -euo pipefail
cd "$(dirname "$0")/../models"
HF=https://huggingface.co
get() { [ -s "$2" ] && { echo "skip $2"; return; }; echo "get  $2"; curl -fL --retry 3 -o "$2.part" "$1" && mv "$2.part" "$2"; }
get "$HF/Qwen/Qwen3-VL-4B-Instruct-GGUF/resolve/main/Qwen3VL-4B-Instruct-Q4_K_M.gguf"      Qwen3VL-4B-Instruct-Q4_K_M.gguf
get "$HF/Qwen/Qwen3-VL-4B-Instruct-GGUF/resolve/main/mmproj-Qwen3VL-4B-Instruct-Q8_0.gguf"  mmproj-Qwen3VL-4B-Instruct-Q8_0.gguf
get "$HF/Qwen/Qwen3-VL-2B-Instruct-GGUF/resolve/main/Qwen3VL-2B-Instruct-Q4_K_M.gguf"      Qwen3VL-2B-Instruct-Q4_K_M.gguf
get "$HF/Qwen/Qwen3-VL-2B-Instruct-GGUF/resolve/main/mmproj-Qwen3VL-2B-Instruct-Q8_0.gguf"  mmproj-Qwen3VL-2B-Instruct-Q8_0.gguf
get "$HF/ggml-org/Qwen3-ASR-1.7B-GGUF/resolve/main/Qwen3-ASR-1.7B-Q8_0.gguf"               Qwen3-ASR-1.7B-Q8_0.gguf
get "$HF/ggml-org/Qwen3-ASR-1.7B-GGUF/resolve/main/mmproj-Qwen3-ASR-1.7B-Q8_0.gguf"        mmproj-Qwen3-ASR-1.7B-Q8_0.gguf
get "$HF/ggml-org/Qwen3-ASR-0.6B-GGUF/resolve/main/Qwen3-ASR-0.6B-Q8_0.gguf"               Qwen3-ASR-0.6B-Q8_0.gguf
get "$HF/ggml-org/Qwen3-ASR-0.6B-GGUF/resolve/main/mmproj-Qwen3-ASR-0.6B-Q8_0.gguf"        mmproj-Qwen3-ASR-0.6B-Q8_0.gguf
get "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx" silero_vad.onnx
