# Ejecutar los pasos 1–3

Desde la raíz del repositorio, usando el entorno existente y Ollama con qwen3:8b instalado:

```powershell
.\.venv\Scripts\python.exe -m unittest evaluation.test_pipeline -v
.\.venv\Scripts\python.exe -m evaluation.evaluate --split validation
.\.venv\Scripts\python.exe -m evaluation.evaluate --split test
```

Cada evaluación escribe un informe independiente en results/baseline_*.json, con resultados parciales persistidos tras cada caso. No modifica el registro de prompts ni los experimentos existentes. Las llamadas van únicamente al Ollama local por defecto. Usa la dependencia requests del proyecto; no requiere instalar librerías de entrenamiento.

Para regenerar el piloto original (sobrescribe datos y revisión):

```powershell
.\.venv\Scripts\python.exe -m evaluation.prepare
```

Documentos: alcance_modelo.md, dataset.md y evaluacion.md. Los esquemas están en schemas y las particiones en data.

Antes del paso 4: confirmar aceptación académica del alcance descriptivo, revisar/aprobar las respuestas sintéticas, ampliar diversidad y cobertura según los resultados, y elegir modelo/configuración de entrenamiento con el hardware disponible. No hay entrenamiento ni despliegue en esta etapa.
