# Perfiles y resultados locales de NeuroFlex

## Uso desde Unity

1. Abrir `Assets/Scenes/Recepción.unity` y entrar en Play.
2. Pulsar **Gestionar perfiles**. Crear un nombre o alias de 2–40 caracteres,
   usando el teclado físico o los botones del teclado en VR, o seleccionar un
   perfil existente. La lista tiene paginación.
3. Elegir Palomas, Juego de memoria o Biblioteca. El perfil se conserva al cambiar
   de escena. Para cambiar de paciente, volver a Recepción y seleccionar otro.
4. Cada ronda terminada guarda sus métricas automáticamente. Una ronda iniciada
   que se abandona o interrumpe también queda registrada con su estado.
5. En Recepción, **Ver historial** muestra fecha, actividad, dificultad, estado y
   un resumen de las métricas del perfil seleccionado.

Al volver a abrir la aplicación, hay que seleccionar el perfil nuevamente. Los
perfiles y su historial permanecen en disco. Los nombres duplicados se rechazan
sin distinguir mayúsculas/minúsculas; cada perfil tiene un UUID estable.

## Base de datos

**`datos_locales/neuroflex.sqlite3`** es la nueva base SQLite, entregada vacía y
ubicada dentro del repositorio. Unity la abre directamente: no requiere Python,
un servidor ni conexión a Internet.

- `patient_profiles`: identificador, nombre/alias y fecha de creación.
- `activity_results`: identificador único de ronda, perfil, sesión de ejecución,
  actividad (`pigeons`, `memory`, `puzzle`), dificultad, estado, fechas UTC y
  métricas JSON. Una clave foránea impide resultados sin perfil.
- Las escrituras usan parámetros SQL. Un mismo identificador de ronda no genera
  resultados duplicados al repetirse una llamada de cierre o recuperación.

En Editor y en una compilación de escritorio ejecutada dentro del repositorio,
se busca `datos_locales` entre los directorios ascendientes de
`Application.dataPath`. Fuera del repositorio, y en Android/Quest, la ubicación
es `Application.persistentDataPath/NeuroFlex/neuroflex.sqlite3`: una aplicación
instalada no tiene acceso al repositorio del equipo de desarrollo.

Linux usa `libsqlite3.so.0`; macOS usa `libsqlite3.dylib`; Windows usa
`winsqlite3`. Android utiliza la API pública `android.database.sqlite` de Java,
sin depender de una biblioteca SQLite privada del sistema. El backend nativo se
ha probado en Linux; Android y los demás sistemas requieren prueba en dispositivo.

Si SQLite falla, la pantalla de resultados indica el problema y ofrece
**Reintentar guardado**. Se escribe un respaldo en
`Application.persistentDataPath/NeuroFlex/pending-results.json` antes de guardar;
Recepción reintenta importarlo al abrirse. Solo se elimina después del guardado.
El respaldo se conserva entre ejecuciones y mantiene el UUID del perfil original.

La base está dentro del repositorio, pero los perfiles reales no se incluyen en
la entrega. Git trata SQLite como un archivo binario; para reunir bases con datos
se necesita una importación, no una fusión textual. Usar alias si se va a versionar
una base con resultados.

### Métricas guardadas

- **Palomas:** contrato `scenes-v1` completo, respuesta confirmada, cantidad real,
  dificultad, tiempos, pausas, distractores y metadatos de llegadas.
- **Memoria:** parejas encontradas y totales, aciertos, intentos incorrectos,
  cartas totales, vista previa configurada y tiempo activo después de la vista
  previa. Los resultados muestran precisión y duración. Cada dificultad es una
  ronda independiente y se vuelve al menú conservando la selección.
- **Biblioteca:** piezas distintas colocadas correctamente, total de piezas,
  colocaciones incorrectas y tiempo activo por nivel. El flujo de preguntas y
  avance entre niveles se mantiene; las respuestas de esas preguntas todavía
  no tienen telemetría propia.

## Menús

Todos los paneles y sus controles están guardados en las escenas y pueden
editarse **fuera de Play** desde la jerarquía:

```text
Recepción.unity
└── Recepción - Menús
    ├── RecepcionMenuCanvas / RecepcionMenuPanel
    ├── PerfilesCanvas / PerfilesPanel
    └── HistorialCanvas / HistorialPanel

Juego De Memoria.unity
└── Memoria - Menús
    ├── MemoriaMenuCanvas / MemoriaMenuPanel
    └── ResultadosMemoriaCanvas / ResultadosMemoriaPanel
```

`PerfilesPanel` contiene las cinco filas reutilizables, el campo TMP de nombre,
el teclado virtual completo, paginación, mensajes y botones de creación/regreso.
Las filas usan textos de muestra para poder diseñarlas sin datos de pacientes.
`ResultadosMemoriaPanel` incluye el resumen, estado de guardado y sus botones.
La navegación y los mensajes nuevos de Palomas también están en su escena.

**Recepción se guarda con `PerfilesCanvas` visible para facilitar la edición**;
al entrar en Play, el controlador muestra la pantalla de recepción. Para editar
otra pantalla, desactivar el Canvas visible y activar el deseado en el Inspector.
Seleccionar el Panel y pulsar **F** en Scene permite enfocarlo: son Canvas en
World Space y están ubicados frente a la cámara de cada escenario.

Se pueden cambiar RectTransform, fuentes, colores, sprites y textos estáticos
directamente desde Unity. Los scripts no crean controles ni cambian tamaños,
posiciones o fuentes al iniciar. Solo conectan acciones, alternan pantallas y
actualizan los datos variables (perfil, filas, métricas y estado de guardado).
`ThemedSceneMenu` conserva referencias a los controles de cada pantalla; los
controladores están en los objetos padre `Recepción - Menús` y `Memoria - Menús`,
para seguir activos aunque se oculte un Canvas.

Los controladores recuperan referencias vacías buscando los controles existentes
de su propia escena, incluso en Canvas inactivos. Esto evita que una referencia
perdida tras recargar scripts detenga la conexión de todos los botones. No se
crean controles de reemplazo ni se modifica su diseño. Si se cambia el nombre de
un objeto, conservar su referencia asignada en el Inspector.

Recepción y Memoria tienen un `EventSystem` con `XRUIInputModule`, configurado
para XR y ratón, igual que Palomas. No se añade un segundo módulo de entrada
competidor. El simulador XR puede alternar la entrada del ratón al cambiar entre
sus modos de interacción.

El Canvas antiguo de Recepción se conserva desactivado como referencia. El
video tutorial de Memoria sigue disponible mediante **Ver tutorial**. Las tres
variantes del tablero y sus cartas se conservan; el nuevo controlador gestiona
su activación y sustituye el avance automático por el resumen de la ronda.
Palomas incluye acceso de vuelta a Recepción y estado del guardado.

Los menús nuevos requieren un perfil antes de comenzar. Para probar una escena
de actividad directamente en Editor, entrar primero a Recepción y seleccionarlo.

## Verificación

```bash
python3 -m unittest discover -s datos_locales -v
```

`test_profiles.py` compila y ejecuta el código C# de producción con las
herramientas incluidas en Unity, sin abrir el proyecto. Por defecto busca
`~/Applications/Unity/<versión-del-proyecto>/Editor`; se puede ajustar mediante
`UNITY_EDITOR_PATH`. Los reemplazos mínimos de Unity están fuera de `Assets`
y solo los usa el ejecutable de pruebas. SQLite es la implementación real.

Se comprueban persistencia entre procesos, alias Unicode y duplicados, claves
foráneas, separación entre pacientes, restricciones de actividad/estado/nivel,
reintentos sin duplicación y recuperación después de una falla de escritura.
La suite anterior de `pool.py` se mantiene.

`test_menu_bindings.py` comprueba las referencias y el módulo de entrada de las
escenas, y ejecuta los controladores C# de producción con referencias inicialmente
vacías. Usa una jerarquía extraída de las escenas y reemplazos mínimos del árbol
de objetos/eventos de Unity para verificar navegación, teclado, creación y
selección de pacientes, historial y flujo de Memoria. Esta prueba no simula los
raycasts, renderizado ni mandos VR; esas comprobaciones requieren Unity.

Verificación manual pendiente en Unity/VR: crear varios perfiles, navegar las
páginas, escribir con el teclado virtual, jugar cada dificultad dos veces,
volver al inicio, revisar los bordes y el historial, reiniciar la aplicación y
seleccionar el mismo perfil. Revisar también el tutorial de Memoria y las
transiciones de nivel de Biblioteca.

## Pool antiguo de Palomas

`palomas.sqlite3`, `pool.py` y `test_pool.py` se conservan para los registros
antiguos `pigeon-local-v1`. Esta base es independiente: los nuevos perfiles y
resultados no se escriben allí. El contrato de importación antiguo sigue
requiriendo aceptación explícita en los archivos exportados.

```bash
python3 datos_locales/pool.py import datos_locales/entradas/sede-a
python3 datos_locales/pool.py stats
mkdir -p datos_locales/exportaciones
python3 datos_locales/pool.py export datos_locales/exportaciones/resultados.csv
python3 datos_locales/pool.py export datos_locales/exportaciones/rondas.json --format scenes-v1
```

Los comandos anteriores exportan únicamente el pool antiguo, no la base de
perfiles. Para consultar la base nueva se puede abrir `neuroflex.sqlite3` con un
visor de SQLite o ejecutar, por ejemplo:

```bash
sqlite3 datos_locales/neuroflex.sqlite3 \
  'SELECT p.alias, r.scenario_id, r.difficulty, r.status, r.ended_at FROM activity_results r JOIN patient_profiles p ON p.profile_id = r.profile_id ORDER BY r.ended_at DESC;'
```
