# Contrato de métricas Unity — scenes-v1

Fecha: 21 de octubre de 2026. Alcance: resúmenes descriptivos de rondas de Puzzle, Memoria de tarjetas y Conteo de palomas. No genera diagnósticos, puntajes clínicos, recomendaciones ni afirmaciones de mejoría. Este contrato es independiente del contrato anterior de cinco métricas.

Implementación: `scenes/contracts.py`; esquema: `schemas/scenes-input.schema.json`. Ejemplo completo de las tres escenas: `data/scenes_v1_1/example_request.json`. El esquema valida forma y tipos; Python comprueba además relaciones entre campos, fechas, estados y contadores. No basta con validar únicamente el JSON Schema.

## 1. Estructura de envío

```json
{
  "contractVersion": "scenes-v1",
  "rounds": [
    {
      "participantId": "p-001",
      "sessionId": "s-001",
      "roundId": "r-puzzle-001",
      "scenarioId": "puzzle",
      "difficultyLevel": 1,
      "startedAt": "2026-10-21T14:00:00.000Z",
      "endedAt": "2026-10-21T14:01:05.000Z",
      "status": "completed",
      "pausedDurationMs": 5000,
      "scenarioVersion": "1.0.0",
      "assessmentMode": "comparison",
      "distractionsEnabled": false,
      "distractionType": "none",
      "distractionCount": 0,
      "distractionIntensity": "none",
      "distractionDurationMs": 0,
      "comparisonGroupId": "puzzle-a-d1-v1",
      "totalPieces": 8,
      "correctPlacements": 8,
      "incorrectPlacements": 3,
      "aidsUsed": 1,
      "activeTimeMs": 60000,
      "puzzleId": "puzzle-a",
      "rotationEnabled": false
    }
  ]
}
```

Una solicitud contiene entre 1 y 100 rondas de un único participante, incluso de distintas escenas. Los campos de cada ronda son planos: comunes, distractores y específicos de su escena. Todos son obligatorios; solo los expresamente indicados admiten `null`. No se aceptan campos extra ni campos de otra escena. `roundId` no puede repetirse dentro de la solicitud. La futura API debe añadir idempotencia persistente para evitar duplicados entre solicitudes.

## 2. Convenciones

- `string`: no vacía, máximo 128 caracteres para identificadores. Son valores opacos: no introducir nombres, correos ni instrucciones. No se envían al modelo.
- `int`: entero de 32 bits, de 0 a 2 147 483 647 salvo mínimo distinto. Un booleano no es un entero válido. JSON usa números, no cadenas numéricas.
- `bool`: `true` o `false` de JSON.
- `datetime`: cadena ISO 8601 completa con zona horaria, preferentemente UTC y milisegundos: `2026-10-21T14:00:00.000Z`. No enviar fechas locales sin zona. Sincronizar el reloj; no mezclar relojes de dispositivos en una misma ronda.
- Duraciones: enteros en milisegundos; la salida convierte a segundos sin redondeos innecesarios. Unity debe medir tiempos activos con reloj monotónico, excluyendo pausas; no usar el reloj de pared para calcularlos.
- `0` es un valor observado válido. `null` expresa ausencia/no aplicabilidad únicamente donde el contrato lo permite. No codificar ausencia con `-1`, `0`, cadena vacía o `false`.
- Enviar la ronda al alcanzar un estado terminal. `interrupted` es el cierre definitivo por interrupción; una pausa temporal no termina la ronda. Si se retoma después de un cierre, crear otra ronda. La salida parcial nunca se etiqueta como resolución completa.

## 3. Campos comunes

| Campo | Tipo | Definición y regla |
|---|---|---|
| `participantId` | string | Identificador seudónimo, constante entre sesiones del participante. |
| `sessionId` | string | Identificador único de la sesión VR; varias rondas pueden compartirlo. |
| `roundId` | string | Identificador único de la ronda o ejercicio. |
| `scenarioId` | enum | `puzzle`, `memory` o `pigeons`. Determina los campos específicos permitidos. |
| `difficultyLevel` | int | Nivel configurado, mínimo 0. Su significado depende de escenario y versión; un 2 de Puzzle no equivale a un 2 de Memoria. |
| `startedAt` | datetime | Inicio de la ronda, incluyendo fases iniciales de presentación. |
| `endedAt` | datetime | Cierre de la ronda, incluso abandonada o interrumpida. Debe ser igual o posterior al inicio. |
| `status` | enum | `completed`, `abandoned` o `interrupted`. En Palomas, completar significa confirmar una respuesta, aunque sea incorrecta. |
| `pausedDurationMs` | int | Total de pausas de toda la ronda. No puede exceder `endedAt - startedAt`. |
| `scenarioVersion` | string | Versión de reglas/configuración del escenario; cambiarla si cambia el significado de una medida. |
| `assessmentMode` | enum | `training` o `comparison`. Es modo del ejercicio VR, no indica entrenamiento del modelo de IA. |
| `comparisonGroupId` | string o null | Grupo previsto de rondas comparables. Usar `null` si no se asignó grupo. No otorga comparabilidad por sí solo. |

`endedAt - startedAt` es tiempo de reloj. Puede incluir presentación, espera, animaciones y pausas. **No sustituye a `activeTimeMs`.**

## 4. Escena Puzzle

| Campo | Tipo | Definición y regla |
|---|---|---|
| `totalPieces` | int ≥ 1 | Cantidad total de piezas. |
| `correctPlacements` | int | Piezas distintas colocadas correctamente, entre 0 y `totalPieces`. Una misma pieza no se cuenta otra vez. |
| `incorrectPlacements` | int | Intentos de colocar una pieza donde no corresponde; pueden superar el total de piezas. |
| `aidsUsed` | int | Número de ayudas activadas. Definir un evento por pista/intervención, sin duplicar conteos por fotograma. |
| `activeTimeMs` | int | Tiempo activo con interacción habilitada hasta completar, abandonar o interrumpir; excluye pausas y presentación previa. |
| `puzzleId` | string | Identificador de puzzle/imagen/configuración de piezas. |
| `rotationEnabled` | bool | Si se exige rotar piezas para colocarlas. |

Si `status = completed`, `correctPlacements` debe ser igual a `totalPieces`. El tiempo activo no puede superar tiempo de reloj menos pausas, salvo 1 ms de tolerancia de representación.

| Métrica devuelta | Cálculo | Unidad |
|---|---|---|
| `resolutionTimeSeconds` | `activeTimeMs / 1000` solo si completada; en otro caso `null` | s activos |
| `activeTimeSeconds` | `activeTimeMs / 1000`, en cualquier estado | s activos |
| `incorrectMoves` | `incorrectPlacements` | intentos |
| `completedPieces` | `correctPlacements` | piezas |
| `totalPieces` | Total recibido | piezas |
| `aidsUsed` | Ayudas recibidas | ayudas |

## 5. Escena Memoria de tarjetas

| Campo | Tipo | Definición y regla |
|---|---|---|
| `totalCards` | int ≥ 2 | Total de cartas de la ronda, par. |
| `totalPairs` | int ≥ 1 | Número de parejas: `totalCards = 2 × totalPairs`. Este contrato contempla exactamente dos cartas por pareja. |
| `correctPairAttempts` | int | Intentos confirmados que encuentran una pareja. El usuario confirmó que cada pareja queda bloqueada al encontrarse; debe coincidir con `pairsFound`. |
| `incorrectPairAttempts` | int | Intentos confirmados de dos tarjetas que no coinciden. Mostrar solo una tarjeta no constituye un intento. |
| `pairsFound` | int | Parejas distintas descubiertas y completadas, entre 0 y `totalPairs`. |
| `activeTimeMs` | int | Tiempo activo desde habilitar interacción, después de la presentación inicial, hasta cierre; excluye pausas. |
| `aidsUsed` | int | Número de pistas/intervenciones utilizadas, contado por evento. |
| `previewDurationMs` | int | Duración configurada de la presentación inicial de tarjetas. Si se abandona durante la presentación, puede no haber transcurrido completa. No se suma al tiempo activo. |
| `deckId` | string | Identificador del conjunto y configuración de tarjetas. |

Si `status = completed`, `pairsFound = totalPairs`. Un intento incorrecto no resta parejas encontradas. Si Unity permite acertar de nuevo parejas ya encontradas, habrá que versionar esta regla antes de enviar esos datos; no inflar `pairsFound`.

| Métrica devuelta | Cálculo | Unidad |
|---|---|---|
| `matchingAccuracyPercent` | `100 × correctPairAttempts / (correctPairAttempts + incorrectPairAttempts)`, redondeado a 2 decimales; `null` si denominador 0 | % |
| `incorrectAttempts` | `incorrectPairAttempts` | intentos |
| `resolutionTimeSeconds` | `activeTimeMs / 1000` si completada; de lo contrario `null` | s activos |
| `activeTimeSeconds` | `activeTimeMs / 1000` | s activos |
| `cardsShown` | `totalCards`, total de cartas de la ronda; no número acumulado de veces que se voltearon | cartas |
| `pairsFound`, `totalPairs` | Valores recibidos | parejas |
| `aidsUsed` | Valor recibido | ayudas |

Ejemplo: 3 intentos correctos y 2 incorrectos → 60 %. Sin intentos → `null`, no 0 %. No se calcula como `pairsFound / totalPairs`: eso mediría avance, no precisión.

## 6. Escena Conteo de palomas

| Campo | Tipo | Definición y regla |
|---|---|---|
| `actualPigeonCount` | int | Cantidad real que debe recordar; 0 es válido si el diseño incluye rondas sin palomas. |
| `userPigeonCount` | int o null | Cantidad confirmada por el participante. `null` si no confirmó; 0 es una respuesta real. |
| `displayDurationMs` | int | Duración configurada de exposición de las palomas, previa a ocultarlas. No hay `pigeonsShownAt` en el contrato: no puede verificarse la exposición efectiva exacta. |
| `responseTimeMs` | int o null | Tiempo **activo** desde habilitar respuesta hasta confirmarla, excluyendo pausas de ese intervalo. `null` si no hubo confirmación. |
| `pigeonsHiddenAt` | datetime o null | Instante de ocultación; `null` si la ronda terminó antes de ese evento. |
| `answerEnabledAt` | datetime o null | Instante de habilitación de respuesta; `null` si no ocurrió. |
| `answerSubmittedAt` | datetime o null | Instante de confirmación; `null` si no ocurrió. |
| `movementLevel` | enum | `static`, `low`, `medium`, `high`. |
| `retentionDelayMs` | int | Intervalo de **reloj** entre ocultación y habilitación, si ambos eventos ocurrieron. Antes de que ocurran, conserva la demora prevista. Si hubo pausas durante la espera, el valor efectivo incluye esas pausas. |

Secuencia válida: `startedAt ≤ pigeonsHiddenAt ≤ answerEnabledAt ≤ answerSubmittedAt ≤ endedAt`, omitiendo únicamente un sufijo de eventos aún no ocurridos. No puede existir envío sin habilitación ni habilitación sin ocultación. Respuesta, `responseTimeMs` y evento de confirmación existen juntos o son todos `null`. Una ronda `completed` requiere confirmación, pero no requiere acierto.

Se verifica `retentionDelayMs` contra las fechas cuando existen, con tolerancia de 1 ms. Para `responseTimeMs`, el intervalo de reloj pone un máximo; las pausas totales de la ronda permiten una cota inferior. **No es posible localizar exactamente las pausas en la fase de respuesta con un único contador global.** Unity debe medir directamente el tiempo activo; si se necesita una auditoría exacta por fase, una versión futura debe añadir eventos o pausas por fase.

| Métrica devuelta | Cálculo | Unidad/tipo |
|---|---|---|
| `responseCorrect` | `userPigeonCount == actualPigeonCount`; `null` sin respuesta | bool o null |
| `signedCountError` | `userPigeonCount - actualPigeonCount`; `null` sin respuesta | palomas; positivo = sobreconteo, negativo = subconteo |
| `absoluteCountError` | Valor absoluto del error; `null` sin respuesta | palomas |
| `actualPigeonCount`, `userPigeonCount` | Valores recibidos | palomas |
| `responseTimeSeconds` | `(answerSubmittedAt - pigeonsHiddenAt)` en segundos; `null` sin respuesta | **Métrica principal**, s de reloj desde ocultación, incluye retención y pausas |
| `enabledResponseActiveSeconds` | `responseTimeMs / 1000`; `null` sin respuesta | Métrica secundaria, s activos desde habilitación |

La precisión individual es un booleano, no un porcentaje. El porcentaje de aciertos de varias rondas requeriría definir una agregación sobre rondas respondidas; esta versión no mezcla rondas para calcularlo.

El usuario confirmó que la métrica principal empieza **al ocultar las palomas**. Por eso `responseTimeSeconds` se obtiene de las fechas de ocultación y confirmación; **no es `responseTimeMs / 1000`**. Se mantiene el campo de entrada `responseTimeMs` con su significado original (activo desde habilitación) y se publica como `enabledResponseActiveSeconds`. Esta asimetría de nombres es intencional y debe reflejarse en el mapeo de Unity. Si se requiere tiempo activo desde ocultación, hacen falta pausas de ese intervalo: no se pueden deducir exactamente de las pausas globales.

## 7. Distracciones en todas las escenas

| Campo | Tipo | Definición y regla |
|---|---|---|
| `distractionsEnabled` | bool | Si se configuraron distractores para la ronda. Puede ser true aunque una interrupción temprana impida presentarlos. |
| `distractionType` | enum | `none`, `visual`, `audio`, `mixed`. Es configuración de la ronda. |
| `distractionCount` | int | Eventos de distracción efectivamente presentados. No contar cada fotograma. |
| `distractionIntensity` | enum | `none`, `low`, `medium`, `high`; intensidad configurada, no una escala clínica. |
| `distractionDurationMs` | int | Duración efectiva de la unión de intervalos con alguna distracción activa. Los solapamientos cuentan una sola vez; no sumar audio y visual simultáneos dos veces. No excede la duración de reloj de la ronda. |
| `comparisonGroupId` | string o null | Campo común descrito arriba; no duplicarlo en otro objeto. |

Con `distractionsEnabled = false`: tipo e intensidad `none`, cantidad y duración 0. Con true: tipo e intensidad distintos de `none`. Si no se presentó ninguna, duración 0. Una duración configurada distinta de la efectiva debe conservarse en telemetría adicional, no introducirse bajo este nombre.

## 8. Comparabilidad

La respuesta incluye grupos y motivos de no comparabilidad; **no calcula todavía tendencias ni diferencias longitudinales**. Se requiere al menos dos rondas del grupo, del mismo participante, todas completadas y en modo `comparison`.

Deben coincidir escenario, dificultad, versión, modo y todos los campos de distractores, incluidas exposición y cantidad observadas. También:

- Puzzle: `totalPieces`, `puzzleId`, `rotationEnabled`.
- Memoria: `totalCards`, `totalPairs`, `previewDurationMs`, `deckId`.
- Palomas: `actualPigeonCount`, `displayDurationMs`, `retentionDelayMs`, `movementLevel`.

Es un criterio conservador: puede excluir rondas que un protocolo futuro decida comparar. No certifica equivalencia clínica. Los intentos, ayudas, aciertos y tiempo activo son resultados, no criterios de igualdad. Ni `sessionId` ni las fechas deben coincidir para comparar rondas de sesiones distintas. No se compara un participante con otro.

Motivos: `insufficient_rounds`, `incomplete_rounds`, `not_comparison_mode`, `different_conditions`. Un grupo inválido no descarta sus resúmenes individuales.

## 9. Respuesta y conexión con la IA

El procesamiento devuelve `contractVersion`, `participantId`, `rounds` y `comparisons`. Cada ronda devuelve `roundId`, `sessionId`, `scenarioId`, `status`, `metrics`, `warnings`, `resumen` y `summarySource`.

Esquema de salida: `schemas/scenes-output.schema.json`. Ejemplo de respuesta sin modelo: `data/scenes_v1_1/example_response.json`.

El modelo recibe únicamente `scenarioId`, `status` y métricas calculadas para una ronda, y genera `{"resumen": "..."}`. Los identificadores, fechas y cadenas de configuración no pasan al LLM. El servidor adjunta cifras y advertencias; no pide al modelo que las invente ni que realice aritmética.

`summarySource` indica `model_verified_template` si la respuesta coincide con la descripción verificable, `deterministic_fallback` si fue sustituida, o `deterministic_preview` si se ejecutó sin modelo. El fallback no prueba que la respuesta original fuera falsa: también puede reemplazar paráfrasis correctas.

Advertencias: siempre `descriptive_only`; además `round_not_completed`, `training_mode`, `distractions_configured`, `no_pair_attempts` o `no_answer`, según corresponda. Una advertencia es contexto, no una conclusión sobre la persona.

## 10. Probar antes de implementar la API

Desde el repositorio:

```powershell
.\.venv-training\Scripts\python.exe -m scenes.infer --preview --input data/scenes_v1_1/example_request.json
```

La vista previa es determinista y **no usa IA**. Permite a Unity acordar las formas de entrada y salida antes del servidor. La inferencia con el nuevo adaptador utiliza el mismo contrato:

```powershell
.\.venv-training\Scripts\python.exe -m scenes.infer --input data/scenes_v1_1/example_request.json --run D:\NeuroFlex\runs\scenes-v1-02 --cache-dir D:\NeuroFlex\cache-training
```

La API HTTP y AWS no están implementados en esta etapa. El entrenamiento nuevo es experimental y sintético; no constituye validación con participantes reales. Sus resultados y la ubicación del candidato se registran en `docs/estado_escenas.md` al finalizar.
