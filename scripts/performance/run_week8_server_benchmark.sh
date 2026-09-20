#!/usr/bin/env bash
set -euo pipefail

mode="${1:-}"

if [[ "$mode" != "sequential" && "$mode" != "optimized" ]]; then
  echo "Uso: $0 <sequential|optimized>" >&2
  exit 1
fi

case "$mode" in
  sequential) execution_mode="Sequential" ;;
  optimized) execution_mode="Optimized" ;;
esac

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
profile="${PROFILE:-concurrent}"
vus="${VUS:-4}"
repetitions="${REPETITIONS:-3}"
audio_file="${AUDIO_FILE:-$repo_root/tests/performance/audio/audio-test.mp3}"
output_dir="$repo_root/artifacts/performance/week8/server/$mode/$profile"
api_log="$output_dir/api.log"
api_pid=""

if [[ "$profile" == "single" ]]; then
  vus=1
fi

cleanup() {
  if [[ -n "$api_pid" ]] && kill -0 "$api_pid" 2>/dev/null; then
    kill "$api_pid"
    wait "$api_pid" 2>/dev/null || true
  fi
}

trap cleanup EXIT INT TERM

if ! [[ "$repetitions" =~ ^[1-9][0-9]*$ ]]; then
  echo "REPETITIONS debe ser un entero mayor que cero." >&2
  exit 1
fi

if curl --silent --fail http://localhost:5270/health >/dev/null 2>&1; then
  echo "El puerto 5270 ya está ocupado por otra API. Detén ese proceso antes del benchmark." >&2
  exit 1
fi

mkdir -p "$output_dir"
cd "$repo_root"

dotnet build AudioThread.sln --disable-build-servers -m:1

AudioProcessing__ExecutionMode="$execution_mode" \
  dotnet run --no-build --project src/AudioThread.Api >"$api_log" 2>&1 &
api_pid=$!

for _ in $(seq 1 120); do
  if curl --silent --fail http://localhost:5270/health >/dev/null 2>&1; then
    break
  fi

  if ! kill -0 "$api_pid" 2>/dev/null; then
    echo "La API terminó antes de estar saludable. Revisa $api_log" >&2
    exit 1
  fi

  sleep 1
done

if ! curl --silent --fail http://localhost:5270/health >/dev/null; then
  echo "La API no quedó saludable dentro de 120 segundos. Revisa $api_log" >&2
  exit 1
fi

echo "Calentamiento: modo=$mode, perfil=$profile, usuarios=$vus"
k6 run \
  --quiet \
  -e VUS="$vus" \
  -e AUDIO_FILE="$audio_file" \
  --summary-export "$output_dir/warmup.json" \
  tests/performance/week8-audio-performance.js

for run in $(seq 1 "$repetitions"); do
  echo "Medición $run/$repetitions: modo=$mode, perfil=$profile, usuarios=$vus"
  k6 run \
    -e VUS="$vus" \
    -e AUDIO_FILE="$audio_file" \
    --summary-export "$output_dir/run-$run.json" \
    tests/performance/week8-audio-performance.js
done

echo "Resultados guardados en $output_dir"
