# NeuroFlex: modelo de IA local

La integración nueva de Unity (Puzzle, Memoria y Palomas) utiliza el módulo `scenes` y un adaptador QLoRA independiente. Ver [contrato de métricas](docs/metricas_unity.md) y [estado de entrenamiento y evaluación](docs/estado_escenas.md). El menú Ollama descrito abajo corresponde al prototipo anterior.

Para el integrador: [guía paso a paso del modelo, API, datos en AWS y dashboard](docs/guia_integracion_api_dashboard_aws.md).

Para la entrega nueva con base y adaptador ya fusionados: [guía de despliegue del modelo fusionado en AWS](docs/guia_aws_modelo_fusionado.md).

El proyecto utiliza Python y `qwen3:8b` mediante Ollama.

## Ejecutar en Windows

Con Ollama abierto, ejecuta desde la carpeta del proyecto en PowerShell:

```powershell
.\.venv\Scripts\python.exe testing.py
```

`testing.py` abre el menú interactivo de experimentos. `main.py` contiene
las funciones del modelo y no inicia el menú por sí solo.
Los experimentos guardan resultados en `results/` y actualizan el contador
de usos de las semillas en `prompts/models.json`.

## Preparar otra instalación

```powershell
winget install --id Ollama.Ollama --exact
```

Abre Ollama y una nueva terminal; luego ejecuta desde esta carpeta:

```powershell
ollama pull qwen3:8b
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe testing.py
```

La aplicación se conecta a `http://localhost:11434/api/chat`.
Si Ollama no está ejecutándose, abre la aplicación Ollama o ejecuta
`ollama serve` en otra terminal.

En VS Code, selecciona `.venv\Scripts\python.exe` como intérprete de Python.

Referencias: [Ollama para Windows](https://docs.ollama.com/windows) y
[qwen3:8b](https://ollama.com/library/qwen3:8b).
