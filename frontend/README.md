# Frontend de AudioThread

Aplicacion web que consume la API de AudioThread para subir grabaciones de voz, seguir el
estado de la subida y ver el resumen generado por el servidor.

## Stack

| Componente | Tecnologia                      |
| :--------- | :------------------------------ |
| Lenguaje   | TypeScript                      |
| Libreria   | React 19                        |
| Build      | Vite                            |
| Estilos    | Tailwind CSS 4                  |
| Pruebas    | Vitest, Testing Library y jsdom |
| Calidad    | oxlint y Prettier               |

## Requisitos

- Node 20 o superior.
- La API de AudioThread ejecutandose y accesible.

## Puesta en marcha

```bash
cd frontend
npm install
cp .env.example .env
npm run dev
```

La aplicacion queda en `http://localhost:5173`.

La unica variable de entorno es `VITE_API_BASE_URL`, que apunta a la API. Si no se define,
se usa `http://localhost:5270`.

## Scripts

| Script               | Que hace                                             |
| :------------------- | :--------------------------------------------------- |
| `npm run dev`        | Servidor de desarrollo con recarga en caliente       |
| `npm run build`      | Verifica los tipos y genera la version de produccion |
| `npm run preview`    | Sirve la version compilada                           |
| `npm test`           | Ejecuta las pruebas una vez                          |
| `npm run test:watch` | Pruebas en modo observador                           |
| `npm run lint`       | Analisis estatico                                    |
| `npm run format`     | Formatea el codigo                                   |

## Estructura

```
src/
├── api/                    contrato con el backend, sin React
│   ├── audioFilesClient.ts
│   ├── apiError.ts
│   └── types.ts
├── features/
│   ├── audio-upload/       formulario, estados de carga y resultado
│   │   ├── hooks/
│   │   ├── components/
│   │   └── validation.ts
│   └── audio-library/      listado y reproduccion
│       ├── hooks/
│       └── components/
├── shared/                 componentes y utilidades reutilizables
└── test/setup.ts
```

La separacion sigue tres reglas: `api/` no importa React, los hooks no devuelven JSX y los
componentes no llaman a `fetch`. Cada capa se puede probar por separado.

## Validacion paralela del audio

Antes de crear un trabajo en la API, el formulario valida el archivo mediante un Web
Worker dedicado. La validacion comprueba la extension, el tipo MIME, el limite de 50 MB y
la firma binaria `RIFF/WAVE` o `ID3/MPEG`. Mientras el worker trabaja, el formulario sigue
respondiendo y el boton de subida permanece bloqueado.

Solo se leen los primeros 64 KiB del archivo y su `ArrayBuffer` se transfiere al worker para
evitar copiar el audio completo entre hilos. Si la validacion falla, el archivo se marca con
un mensaje y nunca se envia al servidor.

## Manejo de la asincronia

El estado de una subida se modela con una union discriminada:

```typescript
type UploadState =
  | { status: 'idle' }
  | { status: 'uploading'; fileName: string }
  | { status: 'success'; result: AudioFileResponse }
  | { status: 'error'; message: string; details: string[] };
```

Esto hace imposible mostrar el indicador de carga y el resultado al mismo tiempo, y obliga
a manejar los cuatro casos.

Toda la comunicacion usa `fetch` con `async` y `await`, sin callbacks anidados. Cada
peticion lleva un `AbortController` que se cancela si el componente se desmonta o si el
usuario pulsa Cancelar.

El indicador de carga es indeterminado y no una barra de porcentaje. Mostrar el progreso
real de una subida requiere `XMLHttpRequest`, que es una API basada en callbacks, y la
actividad solo pide mostrar un estado de carga.

## Pruebas

```bash
npm test
```

| Archivo                    | Que verifica                                                                                                               |
| :------------------------- | :------------------------------------------------------------------------------------------------------------------------- |
| `AudioUploadForm.test.tsx` | Estados de validacion, bloqueo de archivos invalidos, carga, procesamiento y errores                                       |
| `validation.test.ts`       | Reglas de firma binaria, extension, MIME y tamano ejecutadas por el worker                                                 |
| `App.test.tsx`             | Que la biblioteca se actualiza cuando una nueva carga termina                                                              |
| `audioFilesClient.test.ts` | Que el archivo viaja como `multipart/form-data` bajo la clave `file` y que los errores de la API se traducen correctamente |

La prueba principal usa una promesa que se resuelve a mano, asi se puede observar el estado
intermedio de la subida sin temporizadores ni esperas arbitrarias.

## Ejecucion con Docker

El `docker-compose.yml` de la raiz levanta **las dos partes del proyecto**, la API y el
frontend, con un solo comando. No hace falta instalar .NET, Python, FFmpeg ni Node.

```bash
docker compose up --build
```

| Servicio | Direccion               |
| :------- | :---------------------- |
| Frontend | `http://localhost:8081` |
| API      | `http://localhost:5270` |

La imagen del frontend compila la aplicacion y la sirve con Nginx, pesa alrededor de 50 MB.
La de la API pesa unos 5 GB porque trae los modelos de inteligencia artificial adentro.

El origen del frontend ya esta incluido en la lista de origenes permitidos por CORS de la
API, y las migraciones de la base de datos se aplican solas al arrancar, asi que no hay
pasos adicionales. Lo unico que hace falta es el archivo `.env` de la raiz con las
credenciales de Supabase.
