# Estado del modelo de escenas

Contrato: `scenes-v1`. Datos actuales: `data/scenes_v1_1`. Entrenamiento: `D:\NeuroFlex\runs\scenes-v1-02`. La primera corrida `scenes-v1-01` fue sustituida al confirmar el usuario que el tiempo principal de Palomas comienza al ocultarse; no es la entrega actual.

La definición confirmada usa tiempo de reloj desde ocultación hasta confirmación como principal y conserva por separado tiempo activo desde habilitación. En Memoria, cada pareja se cuenta una sola vez y queda bloqueada.

## Entrenamiento completado

- Base: Qwen/Qwen3-4B, revisión `1cfa9a7208912126459214e8b04321603b3df60c`.
- Ajuste nuevo desde la base, no continuación del adaptador de las cinco métricas anteriores.
- QLoRA NF4, doble cuantización, bfloat16, rango 8, alpha 16, dropout 0.05, módulos q_proj y v_proj.
- 2 949 120 parámetros ajustables; cambio de pesos comprobado.
- 96 ejemplos de entrenamiento, 24 validación y 24 prueba. Seis participantes sintéticos: cuatro entrenamiento, uno validación y uno prueba. Cada uno incluye las tres escenas y ocho variantes. Todos los conjuntos comparten generador y plantillas: no hay validación externa.
- Una época, 96 pasos, AdamW, learning rate 0.0001, semilla 20261020, batch 1. Solo se supervisan tokens de respuesta y EOS.
- Pérdida de validación: 2.804003 antes y 0.024720 después. No es una tasa de exactitud ni evidencia clínica.
- Bucle de entrenamiento más validación final: 105.23 s; no incluye carga ni validación inicial. Memoria asignada máxima: 3.94 GiB en RTX 3060.
- Registro: `results/training_scenes_v1_02.json`.

## Evaluación

La evaluación generativa compara base y adaptador con el mismo procesamiento e instrucciones. Se registran por separado JSON parseable, coincidencia literal del resumen original con la referencia, sustituciones por el control y coincidencia de la salida entregada. Una paráfrasis correcta puede contar como no coincidente y activar sustitución; no interpretar ese conteo como error semántico.

La inferencia acepta únicamente texto idéntico a la plantilla de la ronda; en caso contrario entrega la referencia determinista y lo declara mediante `summarySource`. La coincidencia posterior al control mide el sistema de salida, no la exactitud autónoma del modelo.

Validación completada: base 0/24 y adaptador 18/24 en coincidencia literal, ambos 24/24 JSON parseable. El control sustituyó 24 textos de la base y 6 del adaptador; las 24 salidas entregadas por cada variante coinciden con la referencia.

Revisión de diferencias en validación: un texto de Memoria omitió explicar que no hubo intentos; varios textos de Palomas omitieron tiempo activo o añadieron frases imprecisas como «no registrada precisión de reloj». El control sustituye esas respuestas. No se afirma que el adaptador por sí solo esté libre de errores. Informe: `results/validation_scenes_v1_02.json`.

Prueba reservada completada: base 0/24 y adaptador 18/24 en coincidencia literal; ambos 24/24 JSON parseable. Sustituciones: base 24, adaptador 6. Salidas entregadas: 24/24 coinciden con las referencias en ambos casos. Las diferencias del adaptador son omisiones y frases adicionales similares a validación; el resultado no demuestra exactitud del modelo sin el control. Informe: `results/test_scenes_v1_02.json`.

## Entrega

Candidato congelado: `D:\NeuroFlex\releases\neuroflex-scenes-v1-candidate`. Incluye adaptador nuevo, tokenizer, referencia exacta de la base, contratos, inferencia, datos sintéticos, informes, documentación, dependencias y hashes SHA-256. La base de varios GB permanece en `D:\NeuroFlex\cache-training` y no se duplica en el paquete.

Desde el repositorio se puede ejecutar el paquete independiente:

```powershell
.\.venv-training\Scripts\python.exe -B D:\NeuroFlex\releases\neuroflex-scenes-v1-candidate\infer.py --input D:\NeuroFlex\releases\neuroflex-scenes-v1-candidate\data\scenes_v1_1\example_request.json --cache-dir D:\NeuroFlex\cache-training
```

La evidencia de comprobación del paquete se registra fuera de la carpeta congelada en `results/package_verification_scenes_v1.json`, con la respuesta en `results/package_smoke_scenes_v1.json`. No se modifican las versiones anteriores.

Comprobación final completada con GPU: se cargó el paquete independiente y se procesaron Puzzle, Memoria y Palomas. El JSON cumple el esquema y las métricas y resúmenes coinciden con las referencias. En esta muestra, Puzzle y Memoria conservaron el texto del modelo y Palomas utilizó sustitución determinista. Se verificaron los 39 archivos del manifiesto sin modificaciones.

## Alcance y limitaciones

La revisión es técnica y realizada por Codex (IA), no una revisión humana o clínica. Corpus sintético pequeño; se requieren datos y evaluaciones adicionales para generalizar a rondas reales. Las referencias revisadas describen solo una ronda. El modo `comparison` verifica condiciones de comparabilidad, pero no produce todavía tendencias ni diferencias entre rondas.

Los IDs y cadenas de configuración permanecen en Python y no se envían al modelo. El control de texto pertenece al sistema: cargar directamente el adaptador permite eludirlo. Los resultados numéricos de la API futura deben calcularse con el mismo módulo de contratos.

Se completaron 30 pruebas automatizadas del proyecto, incluidas 11 del nuevo contrato, resúmenes y separación del corpus. API HTTP, autenticación, almacenamiento de sesiones y despliegue AWS siguen pendientes. No se ha modificado el código de un proyecto Unity: este repositorio proporciona el contrato y la inferencia para su integración.
