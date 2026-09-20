# Pruebas de carga con k6

Estas pruebas envian el mismo archivo de audio varias veces al endpoint `POST /api/audio-files`. Cada usuario virtual realiza una sola carga, por lo que `VUS=5`, `10` o `20` representa exactamente esa cantidad de solicitudes concurrentes.

## Requisitos

- API de AudioThread ejecutandose en `http://localhost:5270`.
- FFmpeg disponible en `PATH`.
- Supabase Storage y Postgres configurados.
- Bucket `audio-files` con el MIME type `audio/aac` permitido.
- k6 instalado localmente.

### Windows

```powershell
winget install k6 --source winget
k6 version
```

### Ubuntu

```bash
curl -fsSL https://dl.k6.io/key.gpg \
    | sudo gpg --dearmor -o /usr/share/keyrings/k6-archive-keyring.gpg
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" \
    | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
k6 version
```

## Ejecutar los tres niveles en Windows

Desde la raiz del repositorio, ejecuta:

```powershell
.\tests\performance\run-audio-load-tests.ps1 `
    -AudioFile "C:\ruta\audio-test.wav"
```

El runner ejecuta 5, 10 y 20 solicitudes concurrentes, esperando 15 segundos entre cada nivel para reducir la influencia de la prueba anterior.

Si PowerShell bloquea la ejecucion de scripts:

```powershell
powershell -ExecutionPolicy Bypass `
    -File .\tests\performance\run-audio-load-tests.ps1 `
    -AudioFile "C:\ruta\audio-test.wav"
```

## Ejecutar los tres niveles en Ubuntu

Desde la raiz del repositorio, ejecuta el siguiente bloque en Bash:

```bash
mkdir -p artifacts/k6

for vus in 5 10 20; do
    k6 run \
        -e VUS="$vus" \
        -e AUDIO_FILE="$PWD/tests/performance/audio/audio-test.wav" \
        -e BASE_URL="http://localhost:5270" \
        --summary-export "artifacts/k6/summary-${vus}-vus.json" \
        tests/performance/audio-upload.js

    if [ "$vus" -ne 20 ]; then
        sleep 15
    fi
done
```

El bloque ejecuta los mismos niveles que el runner de PowerShell. Si el audio tiene otro nombre o ubicacion, modifica el valor de `AUDIO_FILE`.

## Ejecutar un solo nivel en Windows

```powershell
k6 run `
    -e VUS=5 `
    -e AUDIO_FILE="C:\ruta\audio-test.wav" `
    -e BASE_URL="http://localhost:5270" `
    .\tests\performance\audio-upload.js
```

Cambia `VUS` a `10` o `20` para los demas niveles.

## Ejecutar un solo nivel en Ubuntu

```bash
k6 run \
    -e VUS=5 \
    -e AUDIO_FILE="$PWD/tests/performance/audio/audio-test.wav" \
    -e BASE_URL="http://localhost:5270" \
    tests/performance/audio-upload.js
```

Cambia `VUS` a `10` o `20` para ejecutar otro nivel.

## Que valida la prueba

Cada respuesta debe:

- Tener estado HTTP `201 Created`.
- Informar `contentType` igual a `audio/aac`.
- Incluir un `storagePath` terminado en `.aac`.
- Incluir el UUID del audio creado.

La prueba falla si alguna solicitud o validacion no cumple estas condiciones.

## Metricas principales

- `http_req_duration`: latencia promedio, minima, maxima y percentiles del endpoint.
- `http_req_failed`: porcentaje de solicitudes HTTP fallidas.
- `audio_upload_success`: porcentaje de cargas que cumplieron todas las validaciones.
- `http_reqs`: cantidad total de solicitudes y throughput por segundo.
- `iteration_duration`: tiempo completo utilizado por cada usuario virtual.

Los resumenes JSON se guardan localmente en `artifacts/k6`. Esa carpeta esta ignorada por Git.

Para el informe tambien se debe registrar manualmente el uso maximo aproximado de CPU y memoria. En Windows puede utilizarse el Administrador de tareas y en Ubuntu se puede usar `top` o `htop`, porque k6 mide al cliente y no los recursos internos del servidor.

## Comparar trabajos en segundo plano

El script `audio-job-concurrency.js` usa el patrón asíncrono del frontend: crea un trabajo con `POST /api/audio-files/jobs` y consulta su estado hasta que termina.

Ejemplo recomendado para el laboratorio 6 con cuatro usuarios:

```bash
k6 run \
    -e VUS=4 \
    -e AUDIO_FILE="$PWD/tests/performance/audio/voice-jfk.wav" \
    -e BASE_URL="http://localhost:5270" \
    tests/performance/audio-job-concurrency.js
```

Para comparar la versión secuencial con la paralela, reinicia la API primero con `AudioProcessing__MaxConcurrentJobs=1` y después con `AudioProcessing__MaxConcurrentJobs=2`. En ambas ejecuciones utiliza `VUS=4`, el mismo archivo y las mismas condiciones. Con un trabajador se procesa un audio y tres esperan; con dos trabajadores se procesan dos audios y dos esperan.
