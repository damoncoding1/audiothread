# AudioThread

AudioThread es el proyecto final de Programacion 7 para procesar archivos de audio mediante una arquitectura cliente-servidor.

Cuando la API recibe un archivo MP3 o WAV, lanza dos hilos que trabajan en paralelo sobre el mismo audio. Un hilo lo comprime a AAC con FFmpeg y el otro extrae un resumen usando dos modelos de inteligencia artificial que corren localmente, primero uno que convierte la voz a texto y despues uno que resume ese texto. Cuando los dos hilos terminan se sincronizan, el AAC se guarda en Supabase Storage y la metadata junto con el resumen quedan en Supabase Postgres.

Los logs muestran cuanto tarda cada hilo y cuanto tiempo se ahorra al correrlos en paralelo en lugar de uno detras del otro.

## Requisitos

- .NET 9 SDK.
- FFmpeg instalado localmente y disponible en `PATH`.
- Python 3.10 o superior, para los modelos de transcripcion y resumen.
- `uv` para crear el entorno de Python. Las instrucciones estan en `python/README.md`.
- Unos 4 GB libres en disco, porque los modelos se descargan la primera vez que se ejecuta.
- k6 instalado localmente para ejecutar las pruebas de carga.
- Una cuenta y un proyecto de Supabase.
- Un bucket de Supabase Storage llamado `audio-files`.
- Una cadena de conexion de Supabase Postgres.
- `dotnet-ef` para crear y aplicar migraciones.

```powershell
dotnet tool install --global dotnet-ef
winget install Gyan.FFmpeg
winget install k6 --source winget
ffmpeg -version
k6 version
```

Si FFmpeg no esta en `PATH`, configura su ruta completa en `AudioCompression:ExecutablePath`. Las opciones predeterminadas comprimen con AAC a `128k` y aplican un timeout de 120 segundos.

## Configuracion

Crea un archivo local `.env` tomando como base `.env.example`:

```env
SUPABASE_URL=
SUPABASE_SERVICE_ROLE_KEY=
SUPABASE_STORAGE_BUCKET=audio-files
SUPABASE_STORAGE_USE_PUBLIC_URLS=true
SUPABASE_DATABASE_CONNECTION_STRING=
```

El archivo `.env` esta ignorado por Git y no debe subirse. Las claves reales de Supabase se adjuntaran en los comentarios de Canvas para que el profesor pueda ejecutar el proyecto sin exponer secretos.

Para Supabase Storage:

- El bucket debe llamarse `audio-files`.
- Los MIME types permitidos deben incluir `audio/aac`, porque el servidor almacena el resultado comprimido.
- Para obtener una URL publica, usa `SUPABASE_STORAGE_USE_PUBLIC_URLS=true` y configura el bucket como publico.
- Si el bucket es privado, `url` queda en `null`, pero se conservan `storageBucket` y `storagePath`.

## Modelos locales

La extraccion del resumen usa dos modelos que corren en tu maquina, no se llama a ninguna
API externa:

| Etapa | Modelo | Precision |
| :--- | :--- | :--- |
| Voz a texto | `faster-whisper` modelo `tiny` | `int8` |
| Texto a resumen | `Qwen/Qwen2.5-1.5B-Instruct` | `bfloat16` |

Antes de levantar la API hay que preparar el entorno de Python siguiendo
`python/README.md`. En resumen:

```bash
cd python
uv venv --python 3.10
uv pip install -r requirements.txt
cd ..
```

La primera ejecucion descarga unos 3 GB de pesos a `python/model-cache` y despues todo
corre sin internet. Conviene descargarlos antes de la primera peticion para que la API no
pague ese costo mientras atiende:

```bash
./python/.venv/bin/python python/summarize_audio.py \
    --audio tests/performance/audio/voice-librivox.wav \
    --cache-directory "$PWD/python/model-cache"
```

Las opciones del resumen se configuran en `appsettings.json` bajo `AudioSummarization`.
Las mas utiles son `MaxSummaryLength` para el largo maximo del resumen, que por defecto
es 150 caracteres, y `TorchDataType` para la precision del modelo, que acepta `bfloat16`
o `float32`. La segunda genera casi al doble de velocidad pero necesita unos 8 GB de
memoria en lugar de 3.4 GB.

## Procesamiento paralelo en segundo plano

Los trabajos creados mediante `POST /api/audio-files/jobs` se consumen con
`Parallel.ForEachAsync`. Por defecto el servidor procesa dos audios al mismo tiempo y deja
como máximo ocho esperando en la cola:

```json
"AudioProcessing": {
  "MaxConcurrentJobs": 2,
  "QueueCapacity": 8
}
```

El límite debe ajustarse de acuerdo con la CPU y la memoria disponibles, porque cada trabajo
puede iniciar un proceso de FFmpeg y otro de Python con los modelos locales.

## Base de datos

Para crear una migracion dentro de la carpeta correcta:

```powershell
dotnet ef migrations add NombreMigracion --project src/AudioThread.Infrastructure --startup-project src/AudioThread.Api --output-dir Persistence/Migrations
```

Para aplicar las migraciones en Supabase Postgres:

```powershell
dotnet ef database update --project src/AudioThread.Infrastructure --startup-project src/AudioThread.Api
```

La actividad del laboratorio 3 si agrega una migracion llamada `AddAudioSummary`, que crea las columnas `summary` y `summarized_at_utc` en la tabla `audio_files`. Hay que aplicarla con el comando de arriba antes de ejecutar la API, si no las cargas van a fallar al momento de guardar.

## Frontend

La interfaz web vive en `frontend/` y esta hecha con React 19, TypeScript, Vite y Tailwind
CSS 4. Permite subir un audio, seguir el estado de la subida y ver el resumen generado,
ademas de listar y reproducir los audios ya procesados.

```bash
cd frontend
npm install
cp .env.example .env
npm run dev
```

Queda en `http://localhost:5173`. Los detalles de estructura, scripts y pruebas estan en
`frontend/README.md`.

Para que el navegador pueda llamar a la API hace falta que el origen del frontend este en
la lista `Cors:AllowedOrigins` de `appsettings.json`. Por defecto ya incluye los puertos
5173 y 4173 de Vite.

---

## Ejecutar con Docker

Esta es la forma mas simple de levantar el proyecto, porque no hace falta instalar .NET,
ni Python, ni FFmpeg, ni descargar los modelos a mano. La imagen ya trae todo adentro.

Requisitos: Docker y Docker Compose.

### 1. Crear el archivo `.env`

```bash
cp .env.example .env
```

Y completar los cinco valores de Supabase. Si estas corrigiendo este proyecto, las
credenciales se entregan por separado, fuera del repositorio.

### 2. Construir y levantar

```bash
docker compose up --build
```

La primera vez el build descarga los dos modelos de inteligencia artificial y tarda entre
10 y 20 minutos segun la conexion, la imagen final pesa alrededor de 6 GB. Las veces
siguientes arranca en segundos porque Docker reutiliza las capas.

Cuando termine, la API queda en `http://localhost:5270`, el frontend en
`http://localhost:8080`, y las migraciones de la base de datos se aplican solas al
arrancar, no hay que ejecutar `dotnet ef`.

### 3. Comprobar que responde

```bash
curl http://localhost:5270/health
```

### 4. Cargar un audio

```bash
curl -X POST http://localhost:5270/api/audio-files \
     -F "file=@tests/performance/audio/voice-librivox.wav;type=audio/wav"
```

Los logs del pipeline con los dos hilos se ven en la consola donde quedo corriendo
`docker compose up`.

### Para detenerlo

```bash
docker compose down
```

### Notas

- El contenedor necesita al menos 4 GB de memoria disponible para cargar el modelo de
  resumen. En Docker Desktop hay que revisar el limite en Settings y subirlo si hace falta.
- Una carga completa tarda alrededor de 20 segundos con un audio de dos minutos, la mayor
  parte se va en generar el resumen.
- El `docker-compose.yml` fija `ModelThreads` en 4. En una maquina con menos nucleos
  conviene bajarlo.

---

## Ejecutar localmente

Desde la raiz de `audio-thread`:

```powershell
dotnet restore
dotnet build AudioThread.sln
dotnet run --project src/AudioThread.Api
```

La API queda disponible por defecto en `http://localhost:5270`.

## Ejecutar las pruebas

```powershell
dotnet test AudioThread.sln
```

La prueba comprueba que el caso de uso envia a Storage el contenido AAC producido por el compresor y no el stream original. No necesita ejecutar FFmpeg ni conectarse a Supabase porque usa implementaciones controladas para esas dependencias.

### Pruebas de carga

Con la API ejecutandose, se pueden probar 5, 10 y 20 cargas simultaneas mediante k6:

```powershell
.\tests\performance\run-audio-load-tests.ps1 `
    -AudioFile "C:\ruta\audio-test.wav"
```

En Ubuntu se pueden ejecutar los tres niveles directamente con Bash y k6. Los comandos de instalacion y ejecucion para Windows y Ubuntu, junto con las validaciones y la explicacion de las metricas, estan documentados en `tests/performance/README.md`.

## Ejemplos de API

### Cargar y comprimir un audio

```http
POST http://localhost:5270/api/audio-files
Content-Type: multipart/form-data
```

En Postman usa `Body > form-data`, crea la llave `file`, cambia su tipo a `File` y selecciona un archivo `.wav` o `.mp3`.

Respuesta esperada:

```json
{
  "id": "b5296978-ddc5-4859-aa0c-1082f09c77b3",
  "originalFileName": "audio-test.wav",
  "contentType": "audio/aac",
  "sizeInBytes": 182304,
  "storageBucket": "audio-files",
  "storagePath": "uploads/b5296978ddc54859aa0c1082f09c77b3/c560692cf58440edbeb8d930c0e91099.aac",
  "url": "https://example.supabase.co/storage/v1/object/public/audio-files/uploads/b5296978ddc54859aa0c1082f09c77b3/c560692cf58440edbeb8d930c0e91099.aac",
  "summary": "Lee consents to Confederate surrender; offers soldiers safe passage home.",
  "createdAtUtc": "2026-07-18T14:30:00+00:00"
}
```

Los indicadores principales son `contentType: audio/aac`, la extension `.aac` de `storagePath`, el tamano del archivo procesado y el campo `summary` con el texto generado por los modelos.

La respuesta tarda mas que en el laboratorio anterior porque hay que esperar a que el modelo genere el resumen. Con un audio de 105 segundos el pipeline completo tarda alrededor de 36 segundos en esta maquina. Si el audio no contiene voz detectable, `summary` devuelve `No speech detected in the audio`.

### Obtener metadata por UUID

```http
GET http://localhost:5270/api/audio-files/b5296978-ddc5-4859-aa0c-1082f09c77b3
```

La respuesta devuelve la misma metadata AAC guardada durante la carga.

## Flujo actual

```text
Cliente
   -> validacion MP3 o WAV
   -> dos hilos en paralelo:
         hilo audio-compression:   FFmpeg convierte a AAC
         hilo audio-summarization: Whisper transcribe y Qwen resume
   -> punto de sincronizacion, se espera a que los dos terminen
   -> Supabase Storage (solo el AAC)
   -> Supabase Postgres (metadata y resumen)
```

## Documentacion

| Documento | Contenido |
| :--- | :--- |
| `docs/week2-analysis.md` | Reporte del laboratorio 2, compresion con FFmpeg |
| `docs/week3-threading-explanation.md` | Reporte del laboratorio 3, extraccion de resumen y los dos hilos |
| `docs/week3-guia-mediciones.md` | Comandos paso a paso para reproducir las mediciones |
| `docs/postman/` | Coleccion de Postman lista para importar |
| `frontend/README.md` | Estructura, scripts y pruebas del frontend |
| `python/README.md` | Como preparar el entorno de Python y los modelos |
| `tests/performance/audio/CREDITS.md` | Origen y licencia de los audios de prueba |
