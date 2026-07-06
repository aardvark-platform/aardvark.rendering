#!/usr/bin/env bash
# renderbench report: heap vs baked-multidraw baseline on THIS machine.
#
#   ./bench-report.sh [-n "100000 300000 700000"] [-f frames] [-o report.md]
#                     [--icd /usr/share/vulkan/icd.d/radeon_icd.x86_64.json]
#                     [--no-query]
#
#   -n          object counts to sweep (default: 100k 300k 700k)
#   -f          timed frames per round (default 60; use ~20 on slow iGPUs)
#   -o          output markdown report (default: bench-report-<host>.md here)
#   --icd       Vulkan ICD json to pin a specific GPU/driver (sets VK_DRIVER_FILES)
#   --no-query  fence-blocked CPU timing instead of GPU time queries
#               (HEAPSPIKE_NO_GPU_QUERY=1 — needed on drivers whose time queries
#               are broken, e.g. KosmicKrisp beta)
#
# Run from anywhere; builds HeapSpike Release and sweeps `renderbench`.
set -euo pipefail
cd "$(dirname "$0")"

NS="100000 300000 700000"
FRAMES=60
OUT="bench-report-$(hostname).md"
while [[ $# -gt 0 ]]; do case "$1" in
  -n) NS="$2"; shift 2 ;;
  -f) FRAMES="$2"; shift 2 ;;
  -o) OUT="$2"; shift 2 ;;
  --icd) export VK_DRIVER_FILES="$2" VK_ICD_FILENAMES="$2"; shift 2 ;;
  --no-query) export HEAPSPIKE_NO_GPU_QUERY=1; shift ;;
  *) echo "unknown arg: $1" >&2; exit 1 ;;
esac; done

echo "building HeapSpike (Release)…" >&2
dotnet build -c Release -v q >/dev/null
BIN="../../../bin/Release/net8.0/40 - HeapSpike"

# (vulkaninfo gets SIGPIPE from grep -m1 -> nonzero under pipefail; swallow it)
gpuinfo=$( (vulkaninfo 2>/dev/null || true) | grep -m1 -E "deviceName" | sed 's/.*= //')
driver=$( (vulkaninfo 2>/dev/null || true) | grep -m1 -E "driverName" | sed 's/.*= //')
gpuinfo=${gpuinfo:-unknown}; driver=${driver:-unknown}
commit=$(git rev-parse --short HEAD 2>/dev/null || echo "?")

{
  echo "# renderbench — $(hostname)"
  echo
  echo "| | |"
  echo "|---|---|"
  echo "| date | $(date -u '+%Y-%m-%d %H:%M UTC') |"
  echo "| GPU | ${gpuinfo} |"
  echo "| driver | ${driver} |"
  echo "| OS | $(uname -sr) |"
  echo "| commit | ${commit} |"
  echo "| frames/round | ${FRAMES} (median of 3 rounds, 30-frame warmup) |"
  [[ -n "${VK_DRIVER_FILES:-}" ]] && echo "| ICD override | ${VK_DRIVER_FILES} |"
  [[ -n "${HEAPSPIKE_NO_GPU_QUERY:-}" ]] && echo "| timing | fence-blocked CPU (no GPU queries) |"
  echo
  echo "| objects | heap GPU ms | baseline GPU ms | ratio | heap CPU ms | baseline CPU ms | ingest s | upload s |"
  echo "|---:|---:|---:|---:|---:|---:|---:|---:|"
} > "$OUT"

for n in $NS; do
  echo "=== n=$n ===" >&2
  log=$(mktemp)
  if ! "$BIN" renderbench --n "$n" --frames "$FRAMES" > "$log" 2>&1; then
    echo "| $n | run FAILED (see $log) | | | | | | |" >> "$OUT"
    tail -3 "$log" >&2
    continue
  fi
  heapG=$(grep -oP 'renderbench\[heap\]: GPU \K[0-9.]+' "$log" | tail -1)
  heapC=$(grep -oP 'renderbench\[heap\]:.*task\.Run CPU \K[0-9.]+' "$log" | tail -1)
  baseG=$(grep -oP 'renderbench\[baked-baseline\]: GPU \K[0-9.]+' "$log" | tail -1)
  baseC=$(grep -oP 'renderbench\[baked-baseline\]:.*task\.Run CPU \K[0-9.]+' "$log" | tail -1)
  ratio=$(grep -oP 'heap/baseline = \K[0-9.]+x' "$log" | tail -1)
  ingest=$(grep -oP 'ingest [0-9]+ parts: \K[0-9]+' "$log" | tail -1)
  upload=$(grep -oP 'GPU upload \(cum\) [0-9.]+ MB: \K[0-9]+' "$log" | tail -1)
  # no-query mode reports GPU as 0.00 — fall back to the fence-blocked CPU numbers
  if [[ -n "${HEAPSPIKE_NO_GPU_QUERY:-}" ]]; then
    ratio=$(awk "BEGIN{printf \"%.2fx*\", ${heapC:-0} / ${baseC:-1}}")
    heapG="—"; baseG="—"
  fi
  echo "| $n | ${heapG:-?} | ${baseG:-?} | ${ratio:-?} | ${heapC:-?} | ${baseC:-?} | $(awk "BEGIN{printf \"%.1f\", ${ingest:-0}/1000}") | $(awk "BEGIN{printf \"%.1f\", ${upload:-0}/1000}") |" >> "$OUT"
  rm -f "$log"
done

{
  echo
  echo "*heap = storage-decoded indirect multidraw (clustered); baseline = pre-baked soup, one multidraw, fixed-function vertex input. \\*ratio from fence-blocked CPU times.*"
} >> "$OUT"

echo "report: $OUT" >&2
cat "$OUT"
