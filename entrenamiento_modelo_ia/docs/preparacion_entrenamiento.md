# Preparación del entrenamiento — avance del 20 de octubre de 2026

El alcance académico está confirmado por el usuario: ajuste fino para resúmenes descriptivos. No se necesita un clasificador diagnóstico para satisfacer ese alcance.

## Cambios realizados

- Se conservan el piloto v1 y sus informes como referencia histórica.
- `data/v2` contiene 180 ejemplos candidatos: 120 entrenamiento, 30 validación y 30 prueba. Todas las particiones cubren diez escenarios. Los perfiles y todas sus variantes permanecen en una misma partición.
- Cada perfil sintético se genera con parámetros independientes; no representa un paciente real. Compartir generador y plantillas entre particiones limita la independencia semántica. La prueba v2 es una reserva sintética, no una validación externa.
- Las nuevas referencias separan niveles en % de diferencias en puntos porcentuales, expresan unidades originales, describen dirección y magnitud y distinguen cero de ausencia. Son referencias programáticas; no se copiaron respuestas incorrectas de Qwen.
- `data/sft_v2_draft` contiene conversaciones system/user/assistant para SFT. Solo incluye entrenamiento y validación; no exporta test como material para el entrenador.
- Las entradas de esas conversaciones son los datos procesados por Python, iguales a los que se entregan en inferencia. Se mantiene el prompt de referencia para evitar cambiar simultáneamente instrucciones y entrenamiento.
- `revision.csv` permite revisar las referencias de desarrollo y registrar decisión, revisor, fecha y observaciones. El manifiesto de SFT indica training_ready=false hasta completar una revisión documentada.

180 ejemplos no garantizan suficiencia ni calidad fuera de las plantillas. Son una base para un experimento académico; la evaluación decidirá qué ampliar.

## Recursos detectados

`nvidia-smi`: NVIDIA GeForce RTX 3060, 12 288 MiB de VRAM, driver 616.56. Entorno actual: Windows, Python 3.14.7; contiene requests pero no PyTorch, Transformers, PEFT ni TRL.

La GPU permite plantear un ensayo QLoRA, pero no se ha medido la memoria de entrenamiento. La primera prueba debe medir un paso real con batch pequeño y longitud limitada. No se garantiza que Qwen3 8B quepa con cualquier configuración. El modelo de Ollama no es por sí solo el checkpoint de entrenamiento en Transformers.

Se preparará un entorno separado para entrenamiento y se verificará compatibilidad de Python, PyTorch/CUDA y bitsandbytes antes de descargar el checkpoint. El CUDA que muestra nvidia-smi es capacidad del driver; no prueba que exista un toolkit o un PyTorch CUDA funcional.

## Ejecutar comprobaciones

```powershell
.\.venv\Scripts\python.exe -m unittest evaluation.test_pipeline evaluation.test_dataset_v2 -v
```

Para generar una nueva copia candidata o exportar un borrador, usar un destino que no exista:

```powershell
.\.venv\Scripts\python.exe -m evaluation.dataset_v2 --output data/v2_copia
.\.venv\Scripts\python.exe -m evaluation.prepare_sft --dataset data/v2 --output data/sft_v2_copia
```

El exportador audita hashes, estructura, valores, unidades, advertencias, duplicados y separación de grupos. No certifica automáticamente la semántica del texto. `--approved-only` exige review_status=approved, reviewer y reviewed_at en cada ejemplo de entrenamiento y validación. No basta con cambiar el manifiesto training_ready.

## Qué sigue

1. Revisar las referencias de desarrollo en revision.csv junto a las entradas de data/v2; registrar correcciones y aprobación en una nueva versión del dataset y recalcular sus hashes. La aprobación humana aún no se ha realizado y no se simula.
2. Preparar entorno y ejecutar una prueba de carga y un paso de ajuste fino para medir memoria. Elegir el checkpoint compatible y fijar su revisión exacta.
3. Entrenar, seleccionar con validation y comparar el modelo base y ajustado sobre el mismo contrato. Reservar test hasta fijar las decisiones; si se usa para corregirlas, pasa a regresión y se necesita nueva prueba.
4. Evaluar también redacción y errores semánticos; no aceptar únicamente JSON válido. Solo después seleccionar el artefacto para congelamiento.

No se repitió la inferencia de v1: el modelo y el prompt siguen iguales, por lo que una repetición no demostraría progreso. Las pruebas ejecutadas ahora comprueban los cambios del dataset y su preparación.

Referencias: [SFT y formatos conversacionales](https://huggingface.co/docs/trl/sft_trainer), [compatibilidad de bitsandbytes](https://huggingface.co/docs/bitsandbytes/installation).
