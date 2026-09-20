# Origen y licencia de los audios de prueba

Todos los audios de esta carpeta son de dominio publico. No se usa ningun audio con
derechos de autor, tal como lo pide la definicion del capstone.

| Archivo | Duracion | Contiene voz | Origen | Licencia |
| :--- | ---: | :--- | :--- | :--- |
| `voice-librivox.wav` | 105 s | Si | LibriVox, "Farewell to his army" de Robert E. Lee, leido por un voluntario. Descargado desde Internet Archive, coleccion `nonfiction023_librivox` | Dominio publico |
| `voice-librivox.mp3` | 105 s | Si | El mismo archivo anterior, en su formato original | Dominio publico |
| `voice-jfk.wav` | 11 s | Si | Fragmento del discurso inaugural de John F. Kennedy, 1961. Obra del gobierno de Estados Unidos | Dominio publico |
| `audio-test.wav` | 7.6 s | No | Archivo de prueba usado desde el laboratorio 1 | Generado localmente |
| `test.mp3` | 99 s | No | Archivo de prueba usado desde el laboratorio 1 | Generado localmente |

## Para que sirve cada uno

Los dos primeros son los que sirven para probar el extractor de resumen, porque tienen
voz de verdad. `voice-librivox.wav` es el que use para todas las mediciones del reporte
del laboratorio 3, porque al durar casi dos minutos genera una transcripcion de 1368
caracteres, que ya es suficiente para que el resumen tenga sentido.

`voice-jfk.wav` es mas corto y sirve para pruebas rapidas cuando no se quiere esperar.

`audio-test.wav` y `test.mp3` no contienen voz, el detector de actividad de voz los
descarta por completo y la transcripcion sale vacia. Siguen siendo utiles para dos cosas,
para las pruebas de carga con k6 donde solo importa el tamaño del archivo, y para
comprobar el caso borde en el que el sistema devuelve el texto por defecto
`No speech detected in the audio`.
