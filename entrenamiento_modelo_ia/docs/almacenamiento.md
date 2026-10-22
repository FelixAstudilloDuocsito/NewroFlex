# Almacenamiento local

El 21 de octubre de 2026 se trasladó la caché a `D:\NeuroFlex\cache-training` en el HDD Toshiba. Se verificó SHA-256 de cada archivo copiado antes de eliminar el origen. Los restos `.incomplete` de las descargas interrumpidas no se conservaron. Evidencia: `results/storage_migration.json`.

Espacio libre inmediatamente después: C aproximadamente 22,4 GiB y D aproximadamente 81,1 GiB. Puede variar por actividad de otras aplicaciones.

`storage.local.json` registra `hf_home` y `runs_dir`. Está excluido de Git porque depende del equipo. `training.run` y `training.evaluate_adapter` lo leen automáticamente. El entorno `.venv-training` permanece en el SSD para evitar ralentizar sus numerosas lecturas pequeñas. No se movieron modelos de Ollama ni archivos personales.

Sin `--output`, el entrenador crea una carpeta fechada en `D:\NeuroFlex\runs`. Un `--output` explícito tiene prioridad; usar una ruta en D para evitar volver a llenar C. La variable HF_HOME del proceso, si existe, tiene prioridad sobre storage.local.json.

En otro equipo, crear su propia configuración o definir HF_HOME. No es necesario reproducir la letra D en AWS.

El script `training/migrate_storage.ps1` conserva el procedimiento ejecutado, pero no debe repetirse: se niega a sobrescribir el destino existente.

Durante el inicio de la evaluación del 21/10/2026, el usuario indicó que estaba usando la GPU para otra tarea y que luego la liberó. Las latencias de esa ejecución no deben interpretarse como un benchmark con recursos dedicados. Los resultados de contenido se conservan; el rendimiento de despliegue deberá medirse por separado.
