# Perfiles y resultados locales de NeuroFlex

## Uso desde Unity

1. Abrir `Assets/Scenes/Recepción.unity` y entrar en Play. Aceptar los términos
   con los botones existentes.
2. Girar hacia atrás: `PerfilesCanvas` está detrás del punto de inicio del XR Rig.
   Crear un nombre o alias de 2–40 caracteres con el teclado físico o el teclado
   virtual, o seleccionar un perfil existente en la lista paginada.
3. Girar hacia los accesos originales de Palomas, Juego de memoria y Biblioteca.
   Se habilitan al seleccionar un perfil; el cartel superior indica el perfil
   activo. El perfil se conserva al cambiar de escena y al regresar a Recepción.
4. Para cambiar de paciente, volver a Recepción y usar el panel trasero.
5. Cada ronda terminada guarda sus métricas automáticamente en SQLite con el
   perfil capturado al iniciar. Las rondas abandonadas o interrumpidas también se
   registran con su estado. Los perfiles y resultados pueden consultarse con un
   visor de SQLite o con la consulta del final de este documento.
6. En el panel trasero, **Ver datos guardados** abre la escena **Datos Guardados**.
   Seleccionar un perfil para ver sus rondas. Los filtros recorren actividades,
   niveles y estados; **Métricas** y **Contexto** muestran el detalle de la ronda.
   La consulta no cambia el perfil con el que se juega. **Volver a Recepción**
   conserva la selección del jugador.

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
- **Biblioteca:** piezas que están correctamente colocadas al cerrar la ronda, total de piezas,
  colocaciones incorrectas y tiempo activo por nivel. El flujo de preguntas y
  avance entre niveles se mantiene; las respuestas de esas preguntas todavía
  no tienen telemetría propia.

## Menús

Los paneles y sus controles están guardados en las escenas y se pueden editar
**fuera de Play**:

```text
Recepción.unity
├── Canvas_Escenario
│   ├── Panel_Terminos (controles originales)
│   └── Escenarios (accesos originales y EstadoPerfilRecepcion)
└── PerfilesCanvas (detrás del jugador)
    └── PerfilesPanel

Juego De Memoria.unity
└── Memoria - Menús
    ├── MemoriaMenuCanvas / MemoriaMenuPanel
    └── ResultadosMemoriaCanvas / ResultadosMemoriaPanel

Datos Guardados.unity
└── DatosGuardadosCanvas / DatosGuardadosPanel
    ├── DatosPerfil1…5 y paginación
    ├── FiltrarActividad, FiltrarDificultad, FiltrarEstado
    ├── Ronda1…4 y paginación
    ├── TituloDetalle, FechasDetalle, MetricasDetalle
    └── VolverRecepcion, ActualizarDatos, ReintentarDatos
```

`PerfilesCanvas` está en `(-2.97, 1.94, -64.03)`, con rotación Y de 180° y escala
uniforme de 0.005. El punto de inicio del rig está en `(-2.97, 0.7, -61.53)`,
mirando hacia +Z. El panel está 2.5 metros detrás y orientado hacia el jugador.
Seleccionarlo en la jerarquía y pulsar **F** en Scene permite enfocarlo.

`ReceptionProfileStation` conecta las cinco filas de perfiles, el campo TMP de
alias, las 37 teclas virtuales, Espacio, Borrar, Crear perfil, la paginación y
Actualizar lista. Muestra el perfil activo y habilita los tres accesos existentes;
`ImageSceneLoaderVR.requierePerfil` también comprueba la selección antes de cargar
una escena, incluidos los eventos de puntero XR. Los términos conservan sus
acciones de aceptar y rechazar. No se sustituyen los botones de escenas.

Los scripts no generan controles ni cambian su geometría al iniciar. Recuperan
referencias vacías buscando controles existentes, incluidos los inactivos. Si se
cambia el nombre de un objeto, conservar su referencia en el Inspector.

Los Canvas usan World Space y los raycasters normales y de dispositivos XR con
el `EventSystem` ya existente. El teclado virtual permite escribir con los mandos
sin depender del teclado del sistema operativo del visor. La escala horizontal
del texto es 1 y los RectTransform nuevos tienen escalas uniformes.

Memoria y Palomas ya guardan por perfil. `PuzzleProfileRecorder`, añadido a cada
gestor de Biblioteca con dificultad 1/2/3, observa el inicio, las colocaciones y
la resolución. Captura el perfil al comenzar y guarda antes de disparar el evento
de victoria. El intercambio de piezas, el tutorial, las preguntas y el avance
originales se mantienen. Sin perfil, las actividades todavía pueden probarse
directamente en Editor, pero sus resultados no se atribuyen a un paciente.

### Consulta de métricas y contrato del entrenamiento

`SavedDataSceneMenu` lee perfiles e historiales con paginación y filtros desde
SQLite. Puede consultar más de 1.000 rondas sin truncar el historial. Muestra
fechas locales, dificultad y estado; las fechas guardadas siguen en UTC. Abrir
la escena con la base vacía presenta un mensaje y permite volver a Recepción.
Una ronda con JSON inválido muestra un error en su detalle y conserva el resto
de la navegación. El botón de reintento aparece si hay escrituras pendientes.

`SavedRoundMetrics` implementa los cálculos de
`entrenamiento_modelo_ia/scenes/contracts.py`, descritos en
`entrenamiento_modelo_ia/docs/metricas_unity.md`, contrato **scenes-v1**:

- Biblioteca: piezas completadas y totales, movimientos incorrectos, ayudas,
  tiempo activo y tiempo de resolución solo en rondas completadas.
- Memoria: precisión sobre intentos correctos + incorrectos, intentos erróneos,
  parejas encontradas y totales, cartas, ayudas y tiempos. Sin intentos, la
  precisión es ausente; no se muestra 0 % como si fuera una medida observada.
- Palomas: respuesta correcta, error firmado y absoluto, conteos real y
  confirmado, tiempo activo desde habilitar respuesta y tiempo de reloj desde
  ocultar hasta confirmar. Una respuesta de cero conserva su significado y se
  distingue de una respuesta ausente.

Se admiten las métricas planas de los registros locales de Memoria/Biblioteca
y el envoltorio `request.rounds[0]` de Palomas, además de rondas planas y solicitudes
`scenes-v1` de una sola ronda. Los números ausentes se muestran como **No registrado**,
sin convertirlos en cero. El contexto incluye versión, modo, pausas, distractores,
grupo de comparación y la configuración específica de la actividad cuando se
guardaron. Los registros locales antiguos de Memoria/Biblioteca no contienen todo
el contexto del contrato; la vista lo indica y no inventa esos campos.

El visor usa Newtonsoft JSON, que ya estaba instalado como dependencia de Unity
y ahora está declarado directamente en `Packages/manifest.json`. Conserva los
valores JSON `null`. Esta escena consulta datos y calcula métricas descriptivas;
no ejecuta el modelo de IA ni genera comparaciones longitudinales.

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

`test_saved_data.py` compara los cálculos C# con el módulo Python original para
los 144 ejemplos de entrenamiento/validación/prueba de `scenes_v1_1` y dos casos
adicionales con respuesta cero, en los tres formatos JSON admitidos. También
ejecuta el controlador real con una jerarquía extraída de la escena: consulta de
otro perfil sin cambiar el jugador, filtros combinados, rondas inválidas, base
vacía, actualización y paginación más allá de 1.000 resultados. Usa bases temporales.

`test_menu_bindings.py` comprueba las referencias y el módulo de entrada de las
escenas, y ejecuta los controladores C# de producción con referencias inicialmente
vacías. Usa una jerarquía extraída de las escenas y reemplazos mínimos del árbol
de objetos/eventos de Unity para verificar navegación, teclado, creación y
selección y paginación de pacientes, retorno a Recepción, flujo de Memoria y
registro de Biblioteca con el perfil inicial y sin duplicaciones. Esta prueba no simula los
raycasts, renderizado ni mandos VR; esas comprobaciones requieren Unity.

Verificación manual pendiente en Unity/VR: crear varios perfiles, navegar las
páginas, escribir con el teclado virtual, jugar cada dificultad dos veces,
volver a Recepción, revisar el perfil activo y los datos en SQLite, reiniciar la aplicación y
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
