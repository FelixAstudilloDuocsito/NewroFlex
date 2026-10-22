# Evaluación inicial

Se mide qwen3:8b sin ajuste fino con un prompt fijo en evaluation/baseline_system.txt y esquema de salida v1. Es una nueva referencia para el contrato v1, no una reproducción idéntica del antiguo menú. Todas las futuras comparaciones deben mantener entradas procesadas, prompt, esquema y opciones equivalentes.

Configuración inicial: temperatura 0, seed 20261020, think=false, contexto 4096, máximo 1500 tokens. Una semilla fija no garantiza identidad bit a bit entre máquinas o versiones. El informe registra digest del modelo local, versión de Ollama, opciones, hashes de datos/prompt/esquema/evaluador/procesamiento, respuestas y latencias.

## Métricas

- JSON y esquema válidos, incluyendo campos obligatorios y rechazo de extras.
- Fidelidad estructurada: cinco métricas únicas con valores y unidades idénticos a la referencia.
- Advertencias: coincidencia exacta con las limitaciones calculadas.
- Errores de petición, parseo y truncamiento separados de resultados válidos; todos cuentan en el denominador.
- Señales léxicas que requieren revisión: no son un detector semántico y pueden marcar negaciones correctas.
- Latencia por petición, incluyendo carga inicial cuando ocurra. No es un benchmark de concurrencia ni de AWS.

El resumen libre necesita revisión humana de fidelidad numérica, omisiones, español, afirmaciones no respaldadas y limitaciones. automatic_pass solo significa que los controles estructurados pasaron; no demuestra calidad integral, seguridad ni aptitud clínica. No se compara el texto literalmente con la plantilla.

## Criterios propuestos para la entrega posterior

En la batería acordada: 100% de esquema, fidelidad estructurada y advertencias; ninguna afirmación clínica no respaldada detectada en revisión. Son objetivos de ingeniería propuestos, no tasas de seguridad poblacionales. No se han fijado objetivos de latencia hasta conocer el entorno. Una persona debe aprobar los criterios y la revisión del texto antes de congelar. Un piloto de seis pruebas no estima fiablemente la generalización.

No modificar el prompt a partir del conjunto test. Registrar un baseline no equivale a aprobar el modelo. Evaluar también el artefacto final si se cuantiza o convierte.

Referencia técnica de la API utilizada: https://docs.ollama.com/api/chat
