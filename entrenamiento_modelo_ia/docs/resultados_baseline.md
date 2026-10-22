# Referencia inicial ejecutada — 20 de octubre de 2026

Modelo local: qwen3:8b, digest completo registrado en cada informe. Sin ajuste fino. Prompt y opciones fijos; no se modificaron a partir de la prueba reservada.

| Control | Validación | Prueba |
|---|---:|---:|
| Casos ejecutados | 6 | 6 |
| JSON y esquema válidos | 6/6 | 6/6 |
| Fidelidad de valores y unidades estructurados | 6/6 | 4/6 |
| Todos los controles automáticos aprobados | 6/6 | 4/6 |
| Errores de petición o parseo | 0 | 0 |
| Latencia local observada por caso | 7,18–7,82 s | 8,30–9,78 s |

Informes definitivos:

- [Validación](../results/baseline_validation_20261020T203520498301Z.json)
- [Prueba](../results/baseline_test_20261020T203609594714Z.json)

Existe una ejecución preliminar de validación marcada con superseded_reason, anterior al cierre de las referencias y hashes; no se utiliza en esta tabla.

## Revisión técnica asistida del texto

Esta inspección la realizó el asistente; no sustituye la revisión humana pendiente. No se presenta una tasa de aprobación semántica.

- En validación los resúmenes son genéricos: mencionan cambios sin explicar sus direcciones o magnitudes.
- faltantes_dificultad-1: llama «76 puntos porcentuales» al valor final de precisión; la unidad del nivel es %, no puntos porcentuales.
- faltantes_dificultad-2: introduce la posibilidad de base cero sin que exista en ese caso. No explica la limitación por dificultad variable en el resumen.
- faltantes_dificultad-3: atribuye la imposibilidad de una tendencia a falta de comparación temporal, aunque hay tres sesiones y comparación de extremos.
- ceros_estables-1: usa «significativos» sin prueba estadística y afirma falta de datos iniciales y ausencia de sesiones anteriores. Hay tres sesiones y los ceros son datos presentes.
- ceros_estables-2: cambia el valor final de desviación de mirada de 21 a 25; el control numérico detectó el error.
- ceros_estables-3: cambia el valor final de desviación de mirada de 22 a 23. Además confunde cambios null por base cero con datos ausentes y afirma que existe una sesión única, aunque hay tres.

Conclusión técnica: la referencia inicial NO cumple los criterios propuestos para congelamiento. El esquema limita la forma, no garantiza fidelidad de números ni del texto. El ajuste fino posterior deberá evaluarse sin asumir que resolverá estos fallos.

El conjunto test queda registrado como prueba inicial expuesta. Si sus fallos específicos se utilizan para diseñar datos, prompts o decisiones posteriores, debe tratarse como regresión/desarrollo y construir una nueva prueba independiente para la evaluación final. No copiar estos casos al entrenamiento manteniéndolos como prueba independiente.

## Estado de los pasos 1–3

1. Alcance y contratos implementados; confirmación académica y escala de temblor pendientes.
2. Piloto sintético generado y comprobado: 24/6/6, sin grupos ni entradas duplicadas entre particiones. Revisión humana y ampliación pendientes antes de entrenar.
3. Referencia inicial ejecutada, con respuestas y hashes conservados. Cinco pruebas automatizadas de la infraestructura pasaron.

No se ejecutaron los pasos 4–6: no hay pesos ajustados ni modelo congelado. Tampoco se desplegó una API en AWS.
