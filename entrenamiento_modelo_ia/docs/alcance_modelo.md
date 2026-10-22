# NeuroFlex: alcance descriptivo v1

**Integración Unity actual:** se añadió un contrato independiente para Puzzle, Memoria y Palomas. Consultar [métricas y JSON de Unity](metricas_unity.md) y [estado del adaptador de escenas](estado_escenas.md). Las cinco métricas descritas a continuación pertenecen a la versión anterior y no son el contrato de las tres escenas.

Estado: entrenamiento QLoRA completado; sistema v0.2 congelado y comprobado con GPU, con revisión técnica asistida y control estricto de prosa. Mantiene los pesos entrenados de v0.1; ver `revision_asistida_v0_2.md`. El usuario confirmó que la exigencia académica acepta el ajuste fino para resúmenes descriptivos sin diagnóstico clínico.

Objetivo: generar un resumen en español de métricas de sesiones, con cambios numéricos y limitaciones explícitas. No clasifica enfermedades, diagnostica, prescribe ni atribuye causas. Los comentarios clínicos en investigación están fuera del alcance.

La aplicación entrega el contrato `schemas/entrada.schema.json`. Python valida y calcula los cambios; el LLM recibe ese resultado mediante `evaluation.contracts.process`. El contrato de salida es `schemas/salida.schema.json`. Este contrato v1 es nuevo y no sustituye silenciosamente el del menú experimental anterior.

## Entrada

- Entre 1 y 100 sesiones, números enteros positivos únicos y ordenados.
- Dificultad obligatoria: easy, medium o hard.
- Cinco métricas obligatorias; null indica dato faltante. No se admiten campos adicionales ni texto libre.
- Precisión y desviación de mirada: porcentajes entre 0 y 100.
- Tiempo de respuesta: milisegundos; duración: segundos. Ambos no negativos.
- Índice de temblor: no negativo, sin máximo impuesto porque el repositorio no documenta su escala. Debe confirmarse con el productor de telemetría.
- No se incluye un puntaje diagnóstico: no hay una fórmula validada definida en el proyecto.

## Transformación

Se comparan exclusivamente primera y última sesión. Precisión y desviación usan diferencia en puntos porcentuales; temblor usa diferencia absoluta; tiempo y duración usan variación porcentual respecto al primer valor. Redondeo: tres decimales para diferencias absolutas y dos para porcentajes. Una sesión, extremos faltantes o denominador cero producen cambio null, no cero.

Se conserva el número de sesiones y se detecta dificultad variable y datos faltantes en cualquier sesión. No se entregan valores intermedios al LLM ni se permiten afirmaciones de tendencia continua. Las diferencias con dificultad variable son aritméticas, no evidencia de cambios comparables de capacidad.

## Salida

`resumen`: texto descriptivo en español. `cambios_observados`: exactamente las cinco métricas, con inicial, final, valor del cambio y unidad. `advertencias`: códigos obligatorios calculados por Python (sesion_unica, dificultad_variable, datos_faltantes, base_cero cuando corresponda, y siempre sin_inferencia_clinica).

La validación estructural no certifica el significado del texto. A petición del usuario, la revisión técnica de esta etapa la realiza el asistente y se identifica como tal; no es una revisión humana ni clínica. En v0.2, el texto no verificable se sustituye por una descripción determinista. Los prompts son configuración versionada, no una barrera de seguridad.

## Fuera de esta etapa

El entrenamiento, la selección de GPU y el primer congelamiento ya se realizaron. API y AWS siguen pendientes. No se expondrá directamente el endpoint de Ollama. El contrato actual no es una API pública implementada.
