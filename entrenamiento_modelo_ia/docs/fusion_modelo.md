# Entrega del modelo fusionado en un ZIP

El comando nuevo fusiona Qwen3-4B y el adaptador del candidato `scenes-v1`.
Genera un único `model.safetensors` con los pesos completos FP16 y un ZIP con
tokenizer, configuración, inferencia, métricas y control de resúmenes. El receptor
extrae el ZIP: no necesita descargar la base ni recibir el adaptador por separado.
Sí necesita instalar un entorno Python compatible y disponer de una GPU NVIDIA
con CUDA para el ejecutor incluido. No es un ejecutable autónomo ni incluye API HTTP.

La fusión real se ejecutó el 29 de octubre de 2026 usando el paquete `Pesos_IA`,
cuyos 39 archivos coinciden con el manifiesto original. La entrega local está en
`releases-ia/neuroflex-scenes-v1-merged-fp16.zip`, en la raíz del repositorio.
El ZIP incluye el modelo completo, código, tokenizer e informes de verificación.
Los pasos Windows siguientes permiten reproducir la exportación; la ejecución
real se realizó en Linux con Python 3.14.8 y RTX 3060.

## Resultado de la ejecución real

- PyTorch `2.14.1+cu130`, Transformers `5.20.2`, PEFT `0.21.2`, Accelerate `1.15.0`.
- Fusión sobre CPU en FP32, pesos guardados en FP16, inferencia con GPU CUDA 13.0.
- Se comprobaron los 398 tensores frente a la base exacta más la suma LoRA;
  las 72 matrices ajustadas y los restantes pesos coinciden exactamente con el
  cálculo esperado en FP16. No se encontraron valores no finitos.
- Validación: 24/24 JSON válidos; 12/24 resúmenes originales idénticos a referencia;
  12 sustituciones; 24/24 salidas finales coinciden con referencia.
- Prueba: mismos conteos que validación. En el candidato original NF4 eran
  18/24 coincidencias originales y 6 sustituciones en cada conjunto.
- El modelo fusionado aumenta las sustituciones bajo el control de coincidencia
  literal. Este conteo no mide por sí solo corrección semántica. La fusión es
  numéricamente correcta, pero no conserva todas las respuestas originales.
- Comprobación CRC del ZIP completada sin errores antes de añadir informes.
  Los informes agregados se comprueban por separado al cerrar la entrega.

La suma de las salidas finales es 48/48 con el control activo; no se presenta como
exactitud autónoma de la IA ni como validación de AWS. La entrega no contiene API.
El manifiesto del modelo se conserva como se generó antes de evaluar, por eso su
etapa sigue indicando evaluación pendiente; el informe externo vincula la
evaluación completada con el SHA-256 exacto de ese manifiesto. Los informes y
`ENTREGA.md` se incluyen en la raíz del ZIP, fuera de la carpeta congelada.

## 1. Recursos y origen

Usar el entorno de entrenamiento existente con PyTorch, Transformers y PEFT.
La fusión carga la revisión exacta de la base **sin cuantización, en FP32 sobre CPU**;
suma el adaptador con `merge_and_unload(safe_merge=True)` y guarda en FP16.
Esto evita fusionar directamente sobre los pesos NF4 de inferencia.
El control `safe_merge` busca valores no finitos; no garantiza calidad de respuestas.

Los pesos FP32 de 4B parámetros ocupan aproximadamente 16 GB, más memoria
temporal y librerías: disponer preferentemente de 32 GB de RAM o más y comprobar
el consumo real. Los pesos FP16 ocupan aproximadamente 8 GB; carpeta exportada y
ZIP conservan cada uno una copia. Reservar al menos unos 20 GB libres adicionales,
sin contar la caché y el entorno. El tamaño exacto depende del modelo.

El candidato original y su caché deben estar disponibles y sin modificaciones:

```text
D:\NeuroFlex\releases\neuroflex-scenes-v1-candidate
D:\NeuroFlex\cache-training
```

## 2. Exportar

Desde `entrenamiento_modelo_ia`, en PowerShell, con el repositorio actualizado:

```powershell
.\.venv-training\Scripts\python.exe -B -m training.merge_candidate `
  --package D:\NeuroFlex\releases\neuroflex-scenes-v1-candidate `
  --cache-dir D:\NeuroFlex\cache-training `
  --output D:\NeuroFlex\releases\neuroflex-scenes-v1-merged-fp16 `
  --expected-manifest-sha256 b0242203bbbb4d286513221045da8206f2891786d6c839be17edb90873902491
```

El hash corresponde al candidato documentado en la guía de integración. Para otro
candidato, usar su hash de entrega conocido. El comando comprueba el manifiesto y
los archivos antes de cargar pesos, usa exclusivamente la base local y rechaza
destinos existentes. No modifica el paquete congelado original.

Los hashes se comprueban sobre los bytes exactos, incluidos los finales de línea.
En esta copia Git, `data/scenes_v1_1/train.jsonl` usa LF y su manifiesto corresponde
a CRLF, por lo que una prueba existente de integridad falla incluso sin cambios.
Usar el paquete original conservado en Windows; no reconstruirlo desde esta copia
ni actualizar los hashes automáticamente para ocultar diferencias.

Produce una carpeta nueva y **`D:\NeuroFlex\releases\neuroflex-scenes-v1-merged-fp16.zip`**.
Al terminar imprime el SHA-256 del ZIP para comprobar la transferencia. Si falla,
no usar un ZIP parcial; conservar el diagnóstico y elegir otra ruta para reintentar.

## 3. Comprobar antes de entregar

La fusión FP16 usa una configuración numérica diferente del candidato original
NF4. Puede cambiar respuestas y consumo de GPU. Los resultados originales se
conservan en `provenance/` como evidencia histórica, no como evaluación del fusionado.

En la carpeta exportada, ejecutar con el Python del entorno preparado:

```powershell
cd D:\NeuroFlex\releases\neuroflex-scenes-v1-merged-fp16
python -B infer.py --input data/scenes_v1_1/example_request.json
python -B verify_merged.py --output D:\NeuroFlex\releases\verificacion-fusion-fp16.json
```

`python` debe ser el del entorno de entrenamiento/inferencia; activarlo o usar su
ruta completa. El verificador procesa validación y prueba, registra respuestas
originales, JSON válido, coincidencia literal y sustituciones deterministas. Una
coincidencia después de sustituir texto no demuestra exactitud autónoma de la IA.
Revisar el informe y comparar con los resultados originales, especialmente los
resúmenes originales y el número de sustituciones. Repetir en AWS y medir memoria
y latencia. Completar el corpus conocido es una regresión, no una nueva prueba
independiente de generalización ni aprobación automática para producción.

También puede comprobarse contrato y empaquetado sin GPU usando `infer.py --preview`;
ese modo no ejecuta el modelo. El manifiesto usa hashes calculados por bloques para
verificar pesos grandes sin leerlos íntegramente en RAM.

## 4. Entregar y desplegar

Entregar el ZIP y el informe de verificación; comunicar el SHA-256 por el canal
de entrega. El receptor extrae el paquete e instala el entorno. `requirements-lock.txt`
es el registro del entorno **original**, no una promesa de compatibilidad Linux.
Registrar las versiones efectivamente probadas en AWS. Inferir con el paquete
fusionado no requiere PEFT ni bitsandbytes, aunque figuren en ese registro histórico.

La API debe importar `Generator` desde `scenes.runtime` **de este nuevo paquete** y
usarlo con la carpeta `model`. El modelo se carga una sola vez. Se conservan los
contratos, cálculos y controles de resúmenes. Deben actualizarse la versión y el hash
de manifiesto del despliegue. No usar el hash del candidato original para el fusionado.
Los apartados de descarga de base y `--cache-dir` de la guía original corresponden
a la entrega con adaptador; el nuevo `infer.py` no los necesita.

Referencias oficiales: [formato PEFT y fusión](https://huggingface.co/docs/peft/main/developer_guides/checkpoint),
[cuantización y adaptadores](https://huggingface.co/docs/peft/main/developer_guides/quantization).
