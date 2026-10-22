# Dataset piloto v1

Se conserva como histórico. El candidato ampliado está en `data/v2`; consultar [preparación del entrenamiento](preparacion_entrenamiento.md). No regenerar v1 para incorporar cambios de v2.

36 ejemplos sintéticos deterministas: 24 entrenamiento, 6 validación y 6 prueba. No contienen pacientes ni etiquetas clínicas. Son un piloto de integración y diseño, no una demostración de suficiencia estadística o clínica.

Cada línea JSONL contiene id, group_id, source, review_status, input y expected_output. Las respuestas se construyen con una plantilla y cálculos verificables; no son salidas del modelo consideradas correctas automáticamente. Su estilo es repetitivo y debe ampliarse con redacciones revisadas antes de entrenar.

Las 12 familias se asignaron completas a una sola partición; las tres variantes de cada familia nunca se distribuyen entre conjuntos. Entrenamiento incluye ascensos, descensos, estabilidad, métricas mixtas, una sesión, dificultad variable, faltantes y base cero. Validación contiene oscilaciones y extremos. Prueba combina faltantes con dificultad variable y ceros estables. La prueba tiene cobertura pequeña y sesgada: debe ampliarse para una entrega final.

Los seis casos históricos de test_cases son material de desarrollo y no se presentan como prueba independiente. Si se agregan datos reales, todas las sesiones de una persona deben pertenecer a una misma partición; evitar también duplicados y variantes hermanas entre particiones.

## Revisión pendiente

Todos los ejemplos llevan review_status=pending. Antes del paso 4, una persona debe revisar unidades, números, redacción, límites y correspondencia con la tarea; registrar identidad/fecha de revisión y cambiar únicamente los ejemplos aceptados a approved. No hay exportador ni entrenador que omita esta revisión en esta etapa. Debe definirse además la escala del índice de temblor.

No usar datos de prueba para entrenar, seleccionar prompts o corregir respuestas específicas. Si se usan para ajustar decisiones, pasan a desarrollo y debe prepararse una nueva prueba independiente.

`data/manifest.json` registra procedencia, particiones y SHA-256. El generador es reproducible, pero **sobrescribe** los JSONL y el manifiesto: no ejecutarlo después de una revisión manual sin guardar una nueva versión. Las referencias pueden editarse tras revisión; actualizar el manifiesto y documentar una nueva versión. La prueba de igualdad con la plantilla corresponde exclusivamente a este piloto original.
