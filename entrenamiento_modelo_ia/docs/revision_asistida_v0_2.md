# Revisión técnica asistida v0.2

El usuario solicitó que Codex revisara y corrigiera los resultados. La revisión está realizada por un asistente de IA: no se registra como una revisión humana ni como validación clínica.

## Hallazgos y corrección

Se revisaron el procesamiento de las cinco métricas, las ramas del generador de referencias y las diferencias entre las 60 respuestas ajustadas y sus referencias. Los 180 ejemplos pasaron la auditoría estructural y se generó una copia de referencias corregidas en `data/v2_reviewed_ai`. Esa copia no se usó para entrenar y no altera los datos del entrenamiento original.

- La referencia original usa «Se registraron 1 sesiones». Se corrigió a «Se registró 1 sesión».
- El formato `:g` de las referencias redondeaba valores a seis cifras significativas. El nuevo texto conserva la representación del número de entrada; los cambios calculados mantienen el redondeo del contrato.
- El caso `v2-base_cero-p5-v1` atribuye la base cero a precisión, cuando corresponde a duración. La nueva descripción vincula expresamente la advertencia con la métrica.
- Hay pequeñas discrepancias numéricas en la prosa, además del error ya conocido: por ejemplo, `v2-ascenso-p5-v1`, `v2-descenso-p5-v2`, `v2-faltante_inicial-p5-v0`, `v2-faltante_intermedio-p4-v2` y `v2-faltante_intermedio-p5-v2`. El control anterior solo comparaba los campos estructurados.
- Otras diferencias son paráfrasis u omisiones de detalles; no se consideran automáticamente afirmaciones falsas.

## Decisión técnica

Se mantiene el adaptador realmente entrenado y se agrega `evaluation.summary_guard`. El control rechaza estructura, cifras o advertencias incorrectas. Para la prosa admite únicamente tres realizaciones completas calculadas desde la entrada. Si no hay coincidencia exacta, sustituye todo el resumen por una descripción determinista; no modifica palabras aisladas ni permite pasar afirmaciones adicionales. La procedencia se registra en stderr como `summary_source`, sin cambiar el JSON de salida.

Esta es una versión del sistema de inferencia, **no un modelo reentrenado**. No se afirma que el modelo haya aprendido a evitar el fallo. Reentrenar podría reducir errores o sustituciones, pero no reemplaza un control de salida. El entrenamiento académico ya existe y sus pesos se conservan. Con el objetivo actual de descripciones estrictas, el resultado depende mucho de las plantillas; debe declararse así en el informe académico.

## Comprobaciones y límites

El replay de las respuestas históricas procesa 60 casos para cada variante. Las salidas finales cumplen el contrato y una plantilla verificable. En el modelo ajustado se sustituyen 56/60 resúmenes; en la base, 60/60. **No son tasas de error semántico ni métricas de mejora del modelo**: incluso una paráfrasis correcta puede ser sustituida. El replay usa casos ya observados y no es una nueva evaluación reservada.

Se agregaron pruebas para el error real de atribución, afirmaciones adicionales, cifras estructuradas falsas, conservación de estilos permitidos, precisión numérica, datos ausentes, ceros y dificultad variable. La prueba GPU del paquete se registra por separado en `results/package_verification_v0_2.json`.

El control admite menos variedad de redacción a cambio de una descripción verificable. Su corrección depende del procesamiento y de las plantillas revisadas; no certifica utilidad clínica ni rendimiento en AWS. La API deberá llamar a esta inferencia completa. Cargar el adaptador directamente permite eludir este control.

Evidencia reproducible: `results/review_v0_2.json`, `evaluation/review_candidate.py`, `evaluation/test_summary_guard.py`. La revisión no obliga al usuario a revisar manualmente cada fila para continuar el desarrollo técnico. La aceptación académica institucional y cualquier uso clínico están fuera de esta revisión.
