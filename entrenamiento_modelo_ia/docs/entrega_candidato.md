# Entrega del candidato entrenado

Versión actual del sistema: **v0.2**. Mantiene el adaptador de v0.1 y corrige la prosa mediante un control estricto con sustitución determinista. Ver [revisión asistida](revision_asistida_v0_2.md). La versión v0.1 se conserva como evidencia histórica.

El entrenamiento QLoRA ya actualizó 2 949 120 parámetros del modelo Qwen3-4B. Los pesos ajustados se mantienen en el HDD, junto con la caché de la base. El entorno Python permanece en el SSD.

Carpeta congelada: `D:\NeuroFlex\releases\neuroflex-resumen-v0.2-candidate`. Incluye adaptador, tokenizer, código de inferencia, referencias de entrenamiento, informes de validación y prueba, revisión asistida, ficha y manifiesto. No incluye los pesos base: requiere la revisión exacta indicada en su README, ya disponible en `D:\NeuroFlex\cache-training`.

Desde el repositorio, ejecutar en PowerShell:

```powershell
.\.venv-training\Scripts\python.exe -B D:\NeuroFlex\releases\neuroflex-resumen-v0.2-candidate\infer.py --input D:\NeuroFlex\releases\neuroflex-resumen-v0.2-candidate\example_input.json --cache-dir D:\NeuroFlex\cache-training
```

El modo local no descarga la base. El manifiesto se verifica antes de cargar el modelo; se conserva otra copia en [results/candidate_manifest_v0_2.json](../results/candidate_manifest_v0_2.json). Este candidato es una entrega experimental, no una aprobación para producción. El JSON va por stdout y la procedencia del resumen por stderr.

La evidencia de ejecución de v0.2 se registra en [verificación](../results/package_verification_v0_2.json) y [respuesta del caso de regresión](../results/package_smoke_v0_2.json). Las 19 pruebas automatizadas pasaron, incluyendo seis pruebas nuevas del control de prosa. La evidencia original de v0.1 se conserva en los archivos con sufijo `v0_1`.

Prueba GPU completada: el caso `v2-base_cero-p5-v1` produjo la descripción correcta de duración mediante sustitución determinista. Se comprobaron los 39 archivos del paquete v0.2 y todos los archivos del manifiesto de v0.1, sin cambios. Los pesos del adaptador son idénticos en ambas versiones.

## Qué significa congelar esta entrega

Se conserva una versión fija del adaptador, tokenizer, instrucciones, contrato JSON, código de procesamiento y dependencias, con hashes SHA-256. La base se identifica por una revisión exacta. La inferencia carga el adaptador sin parámetros entrenables y no actualiza pesos. Cualquier modificación posterior debe producir otra versión.

Congelar no impide que un modelo responda a instrucciones distintas si alguien lo carga con otro programa. La API futura debe validar el JSON de métricas, controlar las instrucciones en el servidor y rechazar entradas ajenas al contrato. Mantener un prompt fijo en el servidor es compatible con el ajuste fino y con una entrega congelada.

## Evidencia y alcance

- [Registro del entrenamiento](../results/training_experimental_4b_v2_01.json).
- [Comparación en validación](../results/validation_4b_v2_01.json): base y ajustado pasan 30/30 comprobaciones automáticas.
- [Comparación en prueba reservada](../results/test_4b_v2_01.json): base y ajustado pasan 30/30 comprobaciones automáticas; existe un error de prosa fuera de esos controles.
- [Ficha del modelo](ficha_modelo.md): configuración, datos, limitaciones y estado de revisión.
- [Almacenamiento](almacenamiento.md): rutas y migración al HDD.

Los datos son sintéticos. Las comprobaciones automáticas verifican estructura, valores y advertencias; no sustituyen la revisión humana de los resúmenes. La pérdida de validación disminuyó, pero no se ha demostrado una ventaja numérica frente a la base en validación.

Hallazgo original en `v2-base_cero-p5-v1`: el resumen ajustado atribuye una base cero a precisión, cuando corresponde a duración. v0.2 sustituye ese texto por una descripción calculada que atribuye correctamente la base cero a duración. Los informes históricos no se alteraron. El replay de v0.2 no se presenta como una nueva prueba reservada ni como una mejora de los pesos.

## Trabajo posterior

La revisión técnica solicitada al asistente está documentada. Falta la aceptación académica institucional; el siguiente trabajo de desarrollo es implementar la API con este contrato y preparar su despliegue en AWS. Este paquete todavía no constituye un servicio desplegado. Si se busca reducir el uso de plantillas de sustitución, habrá que entrenar y evaluar otro adaptador con datos nuevos.

La tabla [revisión de validación](../results/revision_validacion_v0_1.csv) contiene entrada, referencia, salida base y salida ajustada. Registrar revisor, fecha, estado y observaciones. Comprobar cifras y unidades en la prosa, dirección de los cambios, datos faltantes, dificultad y ausencia de conclusiones clínicas. Examinar también las referencias: que una respuesta coincida con ellas no las convierte en correctas. Esta revisión se conserva fuera del paquete congelado; modificar la tabla no altera los pesos entregados.

La tabla [revisión de prueba](../results/revision_prueba_v0_1.csv) contiene los otros 30 casos e incluye el hallazgo de base cero para su revisión humana.
