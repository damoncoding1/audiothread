# Modelos locales

La extraccion del resumen usa dos modelos que corren dentro de un subproceso de Python que
lanza `AudioThread.Infrastructure.Summarization.PythonAudioSummarizer`:

| Etapa | Modelo | Valor por defecto |
| :--- | :--- | :--- |
| Voz a texto | faster-whisper (CTranslate2) | `tiny` con `int8` |
| Generacion del resumen | Transformers causal LLM | `Qwen/Qwen2.5-1.5B-Instruct` en `bfloat16` |

No se usa ninguna API externa. Los pesos se descargan una sola vez a `python/model-cache`
y se reutilizan en cada ejecucion.

## Preparar el entorno

El entorno se crea con [uv](https://docs.astral.sh/uv/), que resuelve e instala las
dependencias mucho mas rapido que `pip`. En esta maquina la instalacion completa tardo
34 segundos.

### 1. Instalar uv

Linux y macOS:

```bash
curl -LsSf https://astral.sh/uv/install.sh | sh
```

Windows (PowerShell):

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
```

Despues de instalarlo hay que abrir una terminal nueva para que `uv` quede en el `PATH`.
Para comprobarlo:

```bash
uv --version
```

### 2. Crear el entorno e instalar las dependencias

Desde la carpeta `python` del proyecto:

```bash
cd python
uv venv --python 3.10
uv pip install --index-strategy unsafe-best-match -r requirements.txt
```

No hace falta activar el entorno. `--index-strategy unsafe-best-match` permite resolver
las dependencias generales desde PyPI y conservar la version CPU de PyTorch indicada en
`requirements.txt`, sin arrastrar las dependencias de CUDA.

Para verificar que quedo bien:

```bash
./.venv/bin/python -c "import torch, transformers, faster_whisper, df; print(torch.__version__)"
```

Tiene que imprimir `2.5.1+cpu`.

### Nota para Windows

`uv` crea el entorno en `.venv`, pero Windows usa otra estructura de carpetas. En ese
caso hay que apuntar `AudioSummarization:PythonExecutablePath` a
`python/.venv/Scripts/python.exe` en lugar de `python/.venv/bin/python`.

### Modelo de reduccion de ruido

La etapa adicional usa el modelo local `DeepFilterNet3` para mejorar voz y atenuar
ruido de fondo. El adaptador .NET ejecuta `enhance_audio.py`, que recibe un MP3 o WAV y
genera una version filtrada en WAV. Los pesos se guardan en el mismo directorio local de
cache y se reutilizan en las siguientes ejecuciones.

Prueba directa:

```bash
./.venv/bin/python enhance_audio.py \
    --input ../tests/performance/audio/audio-test.mp3 \
    --output /tmp/audio-test-filtered.wav \
    --model DeepFilterNet3 \
    --cache-directory "$PWD/model-cache" \
    --threads 2 \
    --post-filter
```

### Alternativa sin uv

Si no se quiere instalar `uv`, el entorno tambien se puede crear con el modulo `venv` que
viene con Python. Es bastante mas lento pero produce exactamente el mismo resultado,
porque `uv venv` genera un entorno estandar:

```bash
cd python
python3 -m venv .venv
./.venv/bin/pip install --upgrade pip
./.venv/bin/pip install -r requirements.txt
```

La API no distingue entre los dos, en ambos casos termina ejecutando
`python/.venv/bin/python`.

## Descargar los pesos por adelantado

La primera peticion descarga alrededor de 3 GB. Conviene ejecutar el script una vez para
que la API no pague ese costo mientras atiende una carga:

```bash
./.venv/bin/python summarize_audio.py --audio ../tests/performance/audio/voice-librivox.wav
```

## Ejecutar el script directamente

```bash
./.venv/bin/python summarize_audio.py \
    --audio /ruta/al/audio.wav \
    --max-summary-length 150 \
    --threads 4 \
    --torch-dtype bfloat16 \
    --cache-directory "$PWD/model-cache"
```

El script escribe los mensajes de progreso en `stderr` y un unico documento JSON en
`stdout`:

```json
{
  "transcript": "...",
  "summary": "...",
  "transcriptionMilliseconds": 2186.9,
  "summarizationMilliseconds": 10961.7
}
```

Cuando la grabacion no contiene voz detectable, la transcripcion y el resumen vuelven
vacios, y la API guarda el texto por defecto `No speech detected in the audio`.
