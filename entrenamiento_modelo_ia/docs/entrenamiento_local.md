# Ajuste fino local

Actualización 21/10/2026: la caché y nuevos resultados se guardan en el HDD mediante `storage.local.json`; ver [almacenamiento](almacenamiento.md). Los comandos con --output explícito deben apuntar a D, no al workspace en C.

Se utiliza un entorno aislado `.venv-training`, sin modificar el entorno de inferencia `.venv`. La prueba técnica propone `Qwen/Qwen3-4B` para disponer de margen en la RTX 3060 de 12 GB. El baseline histórico usa Qwen3 8B: NO será una comparación controlada del efecto del ajuste fino sobre 4B. Antes de seleccionar una versión, evaluar base 4B y adaptador 4B con las mismas entradas y configuración.

## Instalación

```powershell
.\.venv\Scripts\python.exe -m venv .venv-training
.\.venv-training\Scripts\python.exe -m pip install torch==2.14.1+cu130 --index-url https://download.pytorch.org/whl/cu130
.\.venv-training\Scripts\python.exe -m pip install -r training/requirements.txt
```

La compatibilidad debe verificarse con una operación CUDA real, no solo con nvidia-smi. La caché de Hugging Face se almacena en `.cache-training` dentro del proyecto. Los pesos del modelo y el entorno consumen varios GB y están excluidos de Git.

## Prueba técnica

```powershell
.\.venv-training\Scripts\python.exe -m training.run --smoke --output training/runs/smoke-4b-01
```

El destino debe ser nuevo. El script resuelve la revisión del modelo a un commit exacto, tokeniza todos los ejemplos sin truncamiento y ejecuta un paso sobre el ejemplo más largo de TRAIN. No utiliza test ni validation para actualizar pesos.

QLoRA: NF4 de cuatro bits con doble cuantización, adaptador de rango 8 sobre q_proj y v_proj, alpha 16, dropout 0.05, AdamW con learning rate 0.0001, batch 1, gradient checkpointing y atención SDPA. El prefijo usa la plantilla oficial con enable_thinking=false. Solo los tokens de la respuesta y su fin de secuencia tienen pérdida; instrucciones y entrada tienen etiquetas -100.

`run.json` registra versiones, revisión exacta, huellas de datos/código, pérdida, gradientes, memoria y si se detectó un cambio real en el adaptador. Se guardan adaptador y tokenizer por separado del modelo base. Una prueba de un paso NO es un modelo listo para entrega. `eligible_for_release` permanece false.

## Entrenamiento posterior

El modo sin `--smoke` exige un exportado aprobado, no el borrador actual. Primero completar revisión documentada de referencias, versionar los datos y exportar con `evaluation.prepare_sft --approved-only`.

Excepción explícita de investigación: `--experimental` permite entrenar el borrador para medir viabilidad sin fingir una revisión humana. Registra experimental_training y eligible_for_release=false. No cambia review_status del dataset ni aprueba una entrega. Esta modalidad permite avanzar técnicamente mientras se revisan las referencias.

```powershell
.\.venv-training\Scripts\python.exe -m training.run --dataset data/sft_aprobado --epochs 2 --output training/runs/entrenamiento-4b-01
```

El bucle registra pérdida de entrenamiento por paso y pérdida de validación ponderada por tokens por época. Esto no reemplaza la evaluación de fidelidad y redacción. Todavía faltan comparación base/adaptador, selección final y congelamiento. No se publica ni se sube nada a AWS.

Referencias oficiales: [Qwen3 4B y su plantilla](https://huggingface.co/Qwen/Qwen3-4B), [PyTorch](https://pytorch.org/get-started/locally/), [cuantización con PEFT](https://huggingface.co/docs/peft/developer_guides/quantization).
