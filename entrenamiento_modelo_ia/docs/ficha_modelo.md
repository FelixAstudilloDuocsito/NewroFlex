# NeuroFlex Resumen — candidato experimental v0.2

v0.2 conserva los pesos entrenados de v0.1 y añade un control de salida. No hubo un segundo entrenamiento. Revisión técnica realizada por Codex a petición del usuario; no es revisión humana ni clínica. Los detalles y hallazgos están en `revision_asistida_v0_2.md`.

## Uso previsto

Resumen descriptivo en español de métricas de sesiones. No predice diagnósticos ni interpreta mejoría clínica. La aceptación académica de este alcance fue confirmada por el usuario. No se presenta como un producto clínico validado.

## Modelo y entrenamiento

- Base: Qwen/Qwen3-4B, revisión 1cfa9a7208912126459214e8b04321603b3df60c.
- QLoRA NF4 con doble cuantización, cómputo bfloat16 en la RTX 3060.
- LoRA: rango 8, alpha 16, dropout 0.05, módulos q_proj y v_proj.
- 2 949 120 parámetros entrenables; actualización de pesos verificada.
- Una época sobre 120 ejemplos sintéticos; 120 pasos, batch 1, AdamW y learning rate 0.0001.
- Solo los tokens de respuesta y fin de secuencia participan en la pérdida; entrada e instrucciones están enmascaradas.
- Pérdida de validación: 0.535367 antes, 0.005701 después, sobre 30 ejemplos reservados de validación.
- Máximo de memoria GPU asignada observado: 5.6373 GiB.
- Datos v2: 120 entrenamiento, 30 validación, 30 prueba; perfiles separados entre particiones. Todas las particiones comparten generador y plantillas: no constituyen validación externa.

El entrenamiento se ejecutó en modo experimental sobre las referencias originales. En v0.2 se auditaron los 180 ejemplos y se generaron referencias corregidas en una copia separada, no utilizada para entrenar. Se mantiene la distinción entre revisión técnica del asistente y revisión humana no realizada.

## Evaluación

Validación generativa completa: modelo base 30/30 y ajustado 30/30 en estructura, cifras, unidades y advertencias. Cero errores de parseo en ambos. Esto no demuestra una ventaja numérica del ajuste fino en esta batería; se observa adaptación al estilo descriptivo de las referencias. La disminución de pérdida no equivale a una tasa de exactitud clínica ni semántica.

Prueba generativa reservada completa: modelo base 30/30 y ajustado 30/30 en estructura, cifras, unidades y advertencias, con cero errores de parseo. Informe: `results/test_4b_v2_01.json`. Estos controles no verifican toda la prosa: en `v2-base_cero-p5-v1`, el ajustado dice «Una base cero impide calcular el cambio porcentual de precisión», aunque la base cero corresponde a duración. Los campos estructurados sí son correctos. Este hallazgo impide presentar el 30/30 automático como exactitud semántica y requiere corrección y nueva evaluación antes de aceptación final.

El comienzo de la evaluación coincidió con otro uso de la GPU informado por el usuario. Las latencias no son un benchmark con recursos dedicados. En lotes, latency_seconds representa la duración del lote, no tiempo exclusivo por respuesta.

## Contrato e inferencia

Python valida sesiones y calcula diferencias. El modelo recibe el resultado procesado, no las sesiones crudas. Produce resumen, cambios_observados y advertencias. La inferencia del paquete rechaza estructura, valores o advertencias inconsistentes. En v0.2, solo conserva un resumen que coincide con una de tres plantillas completas calculadas desde la entrada; en otro caso lo sustituye por texto determinista e informa su procedencia. Esto limita el lenguaje a esas plantillas, sin validar lenguaje natural arbitrario.

Replay v0.2 de los 60 casos ya observados: todas las salidas finales cumplen estructura y plantillas. Se sustituyeron 56/60 resúmenes del ajustado y 60/60 de la base. No es una mejora de los pesos ni una nueva prueba reservada. Algunas sustituciones se deben a paráfrasis correctas, no a errores.

El artefacto consta de adaptador, tokenizer, referencia exacta a la base, prompt, esquemas, procesamiento, registros, dependencias y hashes. El adaptador requiere la base indicada y no funciona por sí solo. Se ejecuta únicamente para inferencia; una nueva actualización debe producir otra versión.

## Limitaciones conocidas

- Corpus pequeño, sintético y basado en plantillas; resultados no extrapolables a datos reales sin evaluación adicional.
- Los resúmenes pueden ser largos y repetitivos. El adaptador puede producir «Se registraron 1 sesiones»; el control v0.2 lo sustituye por la concordancia correcta.
- Las referencias originales usan seis cifras significativas en la prosa. El control v0.2 conserva la representación de los números de entrada y el redondeo de los cambios definido por el contrato.
- No se estudiaron todos los cruces posibles de condiciones ni datos reales.
- Error semántico de v0.1: atribución de base cero a precisión en lugar de duración (`v2-base_cero-p5-v1`). El control v0.2 sustituye ese texto; el adaptador cargado sin el control todavía puede cometerlo.
- El esquema y el ajuste fino no garantizan inmunidad a instrucciones maliciosas. La futura API deberá aceptar únicamente el contrato de métricas y mantener el prompt bajo control del servidor.
- Pendientes: aceptación académica final, API y despliegue AWS. La revisión técnica solicitada al asistente está documentada; no se declara validación clínica ni revisión humana.

Estado: candidato experimental; no aprobado para despliegue final.
