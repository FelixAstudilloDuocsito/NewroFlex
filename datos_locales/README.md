# Datos locales de Palomas

El pool es **`palomas.sqlite3`**, una base SQLite versionada en este repositorio.
Se entrega vacía. `pool.py` usa Python 3.9+ y su biblioteca estándar; no requiere
servidor, conexión a Internet ni instalar paquetes.

**Estado actual:** el registro de pacientes se pospuso para recepción. Palomas
no solicita nick ni guarda archivos al terminar. La base y las herramientas de
importación se conservan para retomar ese flujo después; sirven para registros
ya exportados, pero la escena actual no genera nuevas exportaciones.

## Qué hace el juego

1. Permite elegir Fácil, Medio o Difícil. Un borde verde menta con brillo indica
   la opción seleccionada y permanece aunque cambie el foco del puntero. El menú
   no muestra cantidades de palomas ni información de llegadas.
2. Sortea una nueva cantidad al iniciar cada ronda, incluyendo ambos extremos.
   En Difícil, 2 del total entran después de 2 segundos de observación del primer
   grupo. Fácil y Medio no tienen llegadas tardías.
3. Muestra los resultados y un botón **Volver al inicio**, que permite jugar otra
   ronda conservando la dificultad seleccionada.
4. Conserva únicamente la última ronda en memoria, en `LastRoundRecord` y
   `LastArrivalMetadata`. Al iniciar otra ronda se reemplaza; al cerrar la
   aplicación se pierde. No se escribe telemetría en disco desde esta escena.

No se envía nada a la nube. La integración de paciente, aceptación y guardado en
recepción queda pendiente.

Los rangos, movimiento, tiempo de observación, cantidad de palomas tardías y su
demora se editan en **PigeonPoolSpawner → Presets**, en la escena Palomas. El total
de Difícil incluye las tardías: con estos valores son 8–12 iniciales más 2 tardías.
Los menús están guardados en la escena, bajo **Palomas - Menús**, y conservan el
fondo, blur, hojas, icono y estilo de botones del menú original. La escena usa
tiempos de observación de 8, 8 y 15 segundos.

Para ajustar su apariencia, fuera de Play, activa temporalmente
`DificultadCanvas` y edita sus textos, RectTransform,
imágenes y botones. Desactiva los otros Canvas mientras previsualizas para evitar
superposiciones. El Canvas queda desactivado en la escena guardada;
el controlador lo activa al jugar. El botón `VolverAlInicioButton` también
queda guardado dentro de `ResultadoPanel`.

`PalomaTrialManager → PigeonRoundMenu` contiene las referencias de esos objetos.
Los botones se conectan automáticamente al iniciar: no necesitan eventos OnClick
manuales. Se pueden cambiar posiciones, tamaños, fuentes, colores y textos
estáticos desde Unity. Cada botón de dificultad tiene un hijo `BordeSeleccion`:
en su componente `PigeonDifficultyHighlight` se ajustan color, grosor, radio de
esquina y ancho del brillo. El controlador activa solo el borde elegido.
Al iniciar, el menú recupera referencias antiguas y reconstruye bordes faltantes.
Si no encuentra el Canvas de dificultad completo, mantiene visible el inicio
original para evitar quedarse sin interfaz.

## Archivos que se copian desde cada equipo

Formato preparado para el futuro flujo de recepción y para registros exportados
por la versión anterior con aceptación. Actualmente Palomas no crea estos archivos.

```text
Application.persistentDataPath/NeuroFlexTelemetry/accepted/<UUID>.json
```

La ruta exacta aparece en la consola de Unity después de guardar. En un equipo
se encuentra en la carpeta persistente de la aplicación; en Quest puede copiarse
con ADB desde `Android/data/<identificador-de-la-app>/files/NeuroFlexTelemetry/accepted`.
El identificador se consulta en Player Settings de Unity, en Android.

Cada JSON incluye:

- `recordId`: UUID aleatorio de la ronda, independiente del equipo y del nick.
- `patientNick`, `deviceId`, `savedAt` y `consentGranted`.
- `request`: ronda completa compatible con `scenes-v1`, con respuesta, dificultad,
  tiempos, pausas y distractores.
- `arrivalMetadata`: rango configurado, cantidad inicial/tardía, cantidades
  planificadas y liberadas, y marcas de tiempo de las llegadas.

`roundId` contiene el mismo UUID que `recordId`, con prefijo `r-`; `sessionId`
identifica una ejecución de la aplicación. `deviceId` se genera al primer
guardado y se conserva entre ejecuciones mediante PlayerPrefs. Reinstalar o
borrar los datos de la aplicación puede cambiarlo. El valor predeterminado
`participantId = p-demo` se reemplaza por un UUID de participante por ronda;
también se admite configurar un código explícito en el Inspector.

**El nick no es una clave única**: dos sedes pueden usar el mismo nick y sus
registros se mantienen separados. Para seguir a una misma persona entre sedes,
acuerden un código de participante explícito; el programa no deduce que dos
personas sean la misma solo porque sus nicks coinciden.

## Reunir las pruebas

Ejecutar desde la raíz del repositorio (`python` en Windows, `python3` en Linux/macOS):

```bash
# Copiar antes las carpetas accepted a datos_locales/entradas/sede-a y sede-b.
python3 datos_locales/pool.py import datos_locales/entradas/sede-a datos_locales/entradas/sede-b

# Consultar cantidad de registros por dificultad y estado.
python3 datos_locales/pool.py stats

# Exportar una tabla para analizarla en una planilla.
mkdir -p datos_locales/exportaciones
python3 datos_locales/pool.py export datos_locales/exportaciones/resultados.csv

# Exportar únicamente el contrato scenes-v1 para el procesador de métricas.
python3 datos_locales/pool.py export datos_locales/exportaciones/rondas.json --format scenes-v1
```

La importación busca JSON recursivamente. Volver a importar el mismo registro no
lo duplica. Si un UUID ya existe con contenido diferente, informa el conflicto y
conserva el original. Los archivos inválidos se reportan, los válidos se importan,
y el comando termina con código 1 si hubo errores. No borra los archivos originales.
Los JSON del sistema antiguo que se guardaban sin preguntar no se importan como
registros aceptados; necesitan el formato `pigeon-local-v1` y aceptación explícita.

También se pueden mantener pools independientes y reunirlos después:

```bash
python3 datos_locales/pool.py --db datos_locales/entradas/sede-b.sqlite3 init
python3 datos_locales/pool.py --db datos_locales/entradas/sede-b.sqlite3 import RUTA_A_ACCEPTED
python3 datos_locales/pool.py import datos_locales/entradas/sede-b.sqlite3
```

Los datos completos están en la tabla `submissions`; la vista `results` expone
las columnas de análisis e incluye el error absoluto de las rondas completadas.
Una respuesta `0` se conserva como cero; una respuesta ausente se conserva como
`NULL`. El puntaje del juego puede derivarse de la respuesta y del total y no es
una evaluación clínica.

Git no fusiona bases SQLite binarias. Designen un equipo para importar los
archivos o pools de las distintas sedes al `palomas.sqlite3` central y luego
versionar ese archivo. Conserven las carpetas originales como respaldo. Las
carpetas `entradas/` y `exportaciones/` están excluidas de Git; la base central sí
queda dentro del repositorio. Usen nicks seudónimos: el historial del repositorio
conservará los registros de las versiones que incorporen.

## Verificación

```bash
python3 -m unittest discover -s datos_locales -v
```

En Unity, probar que solo el botón elegido tenga borde, que este permanezca al
apuntar a otros controles y que se conserve al volver desde los resultados.
Probar varias rondas seguidas: no debe aparecer ningún formulario de paciente ni
crearse un archivo de telemetría.
