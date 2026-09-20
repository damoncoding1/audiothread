# Benchmark del laboratorio 8

Este benchmark compara la misma versión funcional de AudioThread en dos modos:

- `Sequential`: compresión, resumen, almacenamiento y jobs se ejecutan uno después de otro.
- `Optimized`: compresión y resumen se solapan, la subida comienza al terminar la compresión y se procesan hasta dos jobs simultáneos.

## Preparación

Desde la raíz del repositorio debe existir un `.env` válido y deben estar disponibles el entorno Python y los modelos locales descritos en `python/README.md`.

Antes de medir, detén cualquier API que utilice el puerto `5270`.

## Servidor

Cada comando realiza una corrida de calentamiento y tres corridas medidas. Los resultados se guardan en `artifacts/performance/week8/server`.

```bash
PROFILE=single scripts/performance/run_week8_server_benchmark.sh sequential
PROFILE=single scripts/performance/run_week8_server_benchmark.sh optimized

PROFILE=concurrent VUS=4 scripts/performance/run_week8_server_benchmark.sh sequential
PROFILE=concurrent VUS=4 scripts/performance/run_week8_server_benchmark.sh optimized
```

Variables opcionales:

| Variable | Valor predeterminado | Uso |
| :--- | ---: | :--- |
| `REPETITIONS` | 3 | Corridas medidas después del calentamiento. |
| `VUS` | 4 | Usuarios del perfil concurrente. |
| `AUDIO_FILE` | `audio-test.mp3` | Audio utilizado por todos los usuarios. La muestra predeterminada dura aproximadamente 3 min 33 s. |

La muestra predeterminada es deliberadamente más exigente que `voice-jfk.wav`, que dura 11 segundos. Esto permite observar mejor el efecto del procesamiento secuencial y optimizado, pero también incrementa considerablemente la duración de las cuatro series del benchmark.

## Cliente

El cliente se perfila sobre la aplicación real mediante Chrome DevTools Performance. `validateAudioFile` agrega una marca User Timing por cada selección:

```text
audio-validation-sequential
audio-validation-worker
```

En cada modo se realiza una validación de calentamiento fuera de la grabación y tres validaciones medidas con el mismo archivo. Los traces exportados por DevTools permiten obtener las duraciones y verificar `CrRendererMain` o `DedicatedWorker` sin incluir la subida al servidor.
