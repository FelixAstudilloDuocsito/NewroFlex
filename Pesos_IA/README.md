# NeuroFlex escenas — candidato experimental

Base requerida: Qwen/Qwen3-4B, revisión 1cfa9a7208912126459214e8b04321603b3df60c. No incluida; usar la caché existente.

Desde esta carpeta, en el entorno CUDA del proyecto: `python -B infer.py --input data/scenes_v1_1/example_request.json --cache-dir D:/NeuroFlex/cache-training`. La ruta de caché es específica de este equipo; cambiarla en otro. No se descarga la base. La salida diferencia texto del modelo y sustitución determinista mediante summarySource.

Instalar PyTorch compatible desde https://download.pytorch.org/whl/cu130 y requirements-lock.txt en un entorno aislado. Entrenamiento real QLoRA con datos sintéticos; revisión técnica de IA, no validación clínica. Ver model_card.md y metricas_unity.md. No es una API desplegada. La integridad del paquete se comprueba contra manifest.json antes de inferir. No editar: crear otra versión.
