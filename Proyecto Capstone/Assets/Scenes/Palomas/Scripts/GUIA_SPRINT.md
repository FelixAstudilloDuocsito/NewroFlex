# NeuroFlex VR — Sprint 1: Conteo de Palomas

Versión MVP para Unity 2022.3 LTS+ / Quest 2, Quest 3 y 3S. **No requiere API, AWS, NavMesh ni scripts adicionales**. Sí necesita TextMeshPro y, para apuntar a botones en VR, una UI XR ya configurada.

## Objetivo y definición de terminado

Una persona pulsa **Iniciar**, observa N palomas durante unos segundos (algunas pueden caminar o volar), ve un distractor opcional, deja de ver las palomas, responde mediante `− / + / Confirmar`, recibe una devolución descriptiva y Unity genera **un JSON `scenes-v1` por ronda**. Puede interrumpir y pausar una ronda.

## Tareas (en orden)

### 1. Instalar los tres scripts

Copia la carpeta `Assets/NeuroFlex/Pigeons/` dentro de la carpeta `Assets` de tu proyecto. Incluye:

- `PigeonAgent.cs`: caminar, volar y quedar estática según dificultad.
- `PigeonRoundController.cs`: controla fases, botones, respuesta, pausas y un distractor opcional.
- `PigeonTelemetry.cs`: guarda JSON plano, fiel al contrato `scenes-v1`.

**Evita mover o renombrar clases sin actualizar sus componentes.**

### 2. Preparar las palomas

- Deja colocadas las palomas en posiciones visibles desde el punto de vista del usuario sentado.
- A cada objeto raíz de paloma agrégale `PigeonAgent`.
- Si tus animaciones `Walk` y `Fly` son clips importados, crea un `Animator Controller` con estados `Walk` y `Fly`, y asígnalo al `Animator` del modelo. Asegúrate de que los clips se reproduzcan en bucle si corresponde.
- Desactiva `Apply Root Motion` en el Animator (el script mueve la raíz). No hace falta Rigidbody ni NavMesh.
- Si el modelo no tiene `Idle`, deja el campo `Idle State` vacío. Si lo tiene, escribe `Base Layer.Idle`. Los otros dos campos deben coincidir exactamente con tus estados (`Base Layer.Walk`, `Base Layer.Fly`).
- Los objetos importados a veces apuntan hacia otra dirección. Si pasa, gira **el modelo hijo** bajo la raíz controlada por `PigeonAgent`, no el script.
- Prueba inicialmente `Walking Radius = 0.6–1.0`, con terreno despejado; el movimiento **no esquiva obstáculos** y la altura de vuelo se mide desde el punto inicial.

### 3. Preparar controlador

- Crea un objeto vacío **PigeonTrialManager**, mantenlo activo y agrega `PigeonRoundController`.
- Arrastra TODAS las palomas colocadas desde la jerarquía a su lista `Pigeons` (no prefabs del Project).
- Configura `Pigeon Count = 5`, `Movement Level = Low`, `Display Seconds = 8`, `Retention Delay Seconds = 0`, `Maximum Answer = 30`.
- `participantId`: utiliza un código seudónimo (`p-001`, no nombre real). `assessmentMode = Training` para primeras pruebas.
- `comparisonGroupId` puede quedar vacío (se exporta `null`). Solo asígnalo cuando el protocolo de comparación esté definido.
- Es importante no desactivar PigeonTrialManager y no ponerlo como hijo de una paloma, ya que el script desactiva las palomas al ocultarlas.

### 4. Preparar UI del usuario (World Space Canvas)

Crea un `Canvas` con `Render Mode = World Space`, legible y frente a una persona sentada. Dentro crea cuatro objetos separados:

```text
Canvas
├── StartPanel
│   └── BtnIniciar
├── ObservingPanel
│   └── TextInstruccion
├── AnswerPanel
│   ├── BtnMenos
│   ├── TxtNumero
│   ├── BtnMas
│   └── BtnConfirmar
├── ResultPanel
│   └── TxtResultado
└── TxtEstado (opcional, fuera de paneles)
```

Todos los textos pueden ser TextMeshPro - Text (UI). Conecta los cuatro paneles y `TxtNumero`, `TxtResultado`, `TxtEstado` a los campos del controlador.

En `Button > OnClick` arrastra **PigeonTrialManager** y selecciona:

| Botón | Método público |
|---|---|
| BtnIniciar | `PigeonRoundController.StartRound()` |
| BtnMenos | `PigeonRoundController.DecreaseAnswer()` |
| BtnMas | `PigeonRoundController.IncreaseAnswer()` |
| BtnConfirmar | `PigeonRoundController.ConfirmAnswer()` |

Métodos opcionales para otros botones: `PauseRound()`, `ResumeRound()`, `AbandonRound()`, `InterruptRound()`.

**Para Quest con XR Interaction Toolkit:** asegúrate de que el Canvas tenga `Tracked Device Graphic Raycaster`, el `EventSystem` tenga `XR UI Input Module` y tus controladores tengan ray interactors/UI interaction activados. Evita varios módulos UI simultáneos. Si ya tienes la UI XR operativa, reutilízala.

### 5. Agregar 1 distractor sencillo

Empieza con **solo uno**, durante la fase de observación:

- **Visual:** coloca un objeto independiente (por ejemplo, una persona que camina o un animal) bajo la escena y arrástralo a `Visual Distractor`. El controlador lo activa y desactiva; si debe moverse, agrégale su propia animación.
- **Auditivo:** agrega un `AudioSource` dedicado y un `AudioClip`; conéctalos a `Audio Source` y `Distractor Audio`.
- Activa `Distractions Enabled` y escoge `Distractor Type` (`Visual`, `Audio` o `Mixed`), `Distractor Intensity = Low`, `Distractor Starts At Second = 3`, `Distractor Duration Seconds = 2`.
- Para pruebas **sin distracción**, desmarca `Distractions Enabled`. El tipo e intensidad exportados serán automáticamente `none`.

`Mixed` requiere **ambos** objetos configurados. El sprint soporta **un episodio por ronda**. No ajusta automáticamente el volumen o la dificultad según `Intensity`: ese valor es solo una etiqueta configurada para documentar el protocolo y deberá normalizarse en una fase futura. En esta versión, audio y visual presentados simultáneamente forman un único evento (`distractionCount = 1`).

### 6. Probar y revisar JSON

Prueba estas situaciones:

1. 5 palomas, 8 segundos, sin distracción, respuesta **5**: `status=completed`, `userPigeonCount=5`.
2. Con distracción visual: `distractionsEnabled=true`, `distractionCount=1`, `distractionDurationMs>0`.
3. Respuesta **0**: debe guardarse el entero `0`, no `null`.
4. Abandonar antes de ocultar palomas: `status=abandoned`, `userPigeonCount=null`, tiempos de respuesta `null`.
5. Pulsar Pausa durante la respuesta y luego Reanudar: `pausedDurationMs > 0`; `responseTimeMs` excluye la pausa.
6. Reiniciar tras completar: crea otro `roundId`, pero conserva el `sessionId` en la ejecución de la aplicación.

Los JSON se guardan por ronda en:

```
Application.persistentDataPath/NeuroFlexTelemetry/r-<guid>.json
```

La ruta concreta aparece en la Consola de Unity. En el Editor, la ruta depende del nombre y compañía del proyecto; en Quest queda en el almacenamiento privado de la aplicación y puede extraerse para inspección con ADB. **No se envía nada por Internet.**

Si trabajas en el repositorio del procesador de métricas, puedes probar un archivo copiado en Windows con:

```powershell
.\.venv-training\Scripts\python.exe -m scenes.infer --preview --input RUTA_AL_JSON_GENERADO.json
```

**Ojo:** `scenes-v1` devuelve resúmenes individuales y grupos potencialmente comparables; **no calcula todavía la diferencia entre rondas con y sin distracciones**. De hecho, el validador del contrato exige condiciones de distractor iguales para considerar un grupo comparable. Para medir ese efecto después, necesitarán una agregación específica del dashboard/backend que empareje condiciones controladas con y sin distractores. No fuerces ambas condiciones bajo un mismo grupo para hacer pasar la validación.

## Sobre los tiempos: respeta esta distinción del contrato

- `displayDurationMs`: duración **configurada**, no una medición exacta de exposición efectiva.
- `responseTimeMs`: tiempo activo desde que se permite responder hasta confirmar, excluyendo pausas.
- La métrica **principal** `responseTimeSeconds` se obtiene en el procesador como `answerSubmittedAt - pigeonsHiddenAt`: desde que se ocultan las palomas hasta que responde, **incluye** retención y pausas.
- `retentionDelayMs`: si se alcanzaron ambas fases, es la duración de reloj entre ocultación y habilitación; si no, conserva la demora prevista.
- `distractionCount` y `distractionDurationMs`: eventos realmente mostrados/escuchados y tiempo efectivo con un distractor activo (no la duración planificada). La medición de duración se aproxima a la resolución de fotograma.

## Próximo sprint (no incluido)

Múltiples distractores programados, diferentes conteos y patrones, contrabalanceo de pruebas, selector de dificultad durante ejecución, control de colisiones/NavMesh, y comparaciones estadísticas longitudinales. No presentar los datos como diagnóstico ni puntaje clínico.
