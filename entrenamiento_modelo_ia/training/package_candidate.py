"""Snapshot verificable de un candidato experimental; no aprueba su uso final."""
import argparse
import json
import shutil
from pathlib import Path
from training.run import ROOT, sha


def package(run_dir, evaluation, output, review_path=None):
    run = json.loads((run_dir / "run.json").read_text(encoding="utf-8"))
    report = json.loads(evaluation.read_text(encoding="utf-8"))
    if output.exists():
        raise ValueError("No se sobrescriben candidatos congelados")
    if run["status"] != "completed" or not run.get("adapter_changed") or run["mode"] == "technical_smoke":
        raise ValueError("Falta entrenamiento completo con actualización verificada")
    if report["status"] != "completed" or report["run_sha256"] != sha(run_dir / "run.json"):
        raise ValueError("Evaluación ausente o de otro entrenamiento")
    if report["adapter_sha256"] != sha(run_dir / "adapter" / "adapter_model.safetensors"):
        raise ValueError("El adaptador cambió después de evaluarse")
    if not report.get("results") or any(report["summary"][name]["total"] != len(report["results"]) for name in ("base", "adapted")):
        raise ValueError("Informe vacío o conteos inconsistentes")
    if report["model"] != run["model"] or report["revision"] != run["revision"]:
        raise ValueError("Modelo base inconsistente")
    if report["prompt_sha256"] != sha(ROOT / "evaluation" / "baseline_system.txt") or report["schema_sha256"] != sha(ROOT / "schemas" / "salida.schema.json"):
        raise ValueError("El contrato o prompt cambió después de evaluarse")
    review = None
    if review_path is not None:
        review = json.loads(review_path.read_text(encoding="utf-8"))
        if review.get("status") != "completed" or review.get("guard_sha256") != sha(ROOT / "evaluation/summary_guard.py"):
            raise ValueError("Revisión ausente o control de texto modificado")
        if review.get("source_reports", {}).get(report["split"]) != sha(evaluation):
            raise ValueError("La revisión no corresponde a la evaluación")
    output.mkdir(parents=True)
    for name in ("adapter", "tokenizer"):
        shutil.copytree(run_dir / name, output / name)
    shutil.copy2(run_dir / "run.json", output / "training.json")
    shutil.copy2(evaluation, output / "evaluation.json")
    validation_file = run_dir / "validation_batched.json"
    if validation_file.exists() and validation_file != evaluation:
        validation = json.loads(validation_file.read_text(encoding="utf-8"))
        if validation.get("status") != "completed" or validation.get("adapter_sha256") != report["adapter_sha256"]:
            raise ValueError("Informe de validación inconsistente")
        shutil.copy2(validation_file, output / "validation.json")
    if (ROOT / "docs" / "ficha_modelo.md").exists():
        shutil.copy2(ROOT / "docs" / "ficha_modelo.md", output / "model_card.md")
    shutil.copy2(ROOT / "evaluation" / "baseline_system.txt", output / "system.txt")
    shutil.copytree(ROOT / "schemas", output / "schemas")
    shutil.copy2(ROOT / "training" / "requirements-lock.txt", output / "requirements-lock.txt")
    example = json.loads((ROOT / "data" / "v2" / "validation.jsonl").read_text(encoding="utf-8").splitlines()[0])["input"]
    (output / "example_input.json").write_text(json.dumps(example, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (output / "README.md").write_text(
        "# Candidato experimental NeuroFlex\n\n"
        "Snapshot de pesos ajustados para revisión académica. No aprobado para despliegue final.\n\n"
        f"Base requerida: {run['model']}\nRevisión exacta: {run['revision']}\n\n"
        "El adaptador NO es un modelo independiente: cargar esa base y después adapter/ con PEFT. "
        "Usar tokenizer/, enable_thinking=false y system.txt. Los JSON originales deben procesarse "
        "con evaluation.contracts.process del proyecto; no enviar sesiones crudas directamente.\n\n"
        "No se incluyen los pesos base, disponibles en la caché del HDD o en su repositorio oficial. "
        "Los hashes del manifiesto permiten verificar el contenido. No modificar este directorio: "
        "cualquier cambio requiere otra versión. Los datos son sintéticos y su revisión humana "
        "sigue pendiente. La evaluación automática no certifica el significado del texto.\n\n"
        "## Ejecutar\n\n"
        "En un entorno con CUDA compatible, instalar torch desde https://download.pytorch.org/whl/cu130 "
        "y después requirements-lock.txt. Definir HF_HOME apuntando a la caché existente si se desea evitar "
        "otra descarga de la base. Ejecutar `python infer.py --input example_input.json --cache-dir D:/NeuroFlex/cache-training` "
        "(adaptar la ruta al equipo). La inferencia solo usa archivos locales: si la base falta, falla sin descargar. "
        "infer.py verifica los hashes del paquete y rechaza inconsistencias numéricas o de advertencias; "
        "el resumen se conserva solo si coincide con una de las plantillas verificables, o se sustituye "
        "por texto determinista calculado desde la entrada. Se informa summary_source en stderr. "
        "Esta corrección pertenece al procesamiento de salida: los pesos no se reentrenaron. "
        "No es una API ni un producto clínico validado.\n",
        encoding="utf-8")
    # Incluye el procesamiento exacto utilizado para construir la entrada.
    (output / "evaluation").mkdir()
    shutil.copy2(ROOT / "evaluation" / "contracts.py", output / "evaluation" / "contracts.py")
    shutil.copy2(ROOT / "evaluation" / "summary_guard.py", output / "evaluation" / "summary_guard.py")
    shutil.copy2(ROOT / "evaluation" / "__init__.py", output / "evaluation" / "__init__.py")
    shutil.copy2(ROOT / "training" / "candidate_infer.py", output / "infer.py")
    (output / "training").mkdir()
    for name in ("__init__.py", "run.py", "evaluate_adapter.py"):
        shutil.copy2(ROOT / "training" / name, output / "training" / name)
    shutil.copy2(ROOT / "evaluation" / "evaluate.py", output / "evaluation" / "evaluate.py")
    shutil.copytree(ROOT / "data" / "v2", output / "data" / "v2")
    shutil.copytree(ROOT / "data" / "sft_v2_draft", output / "data" / "sft_v2_draft")
    (output / "evaluation" / "baseline_system.txt").write_bytes((output / "system.txt").read_bytes())
    if review is not None:
        shutil.copy2(review_path, output / "assistant_review.json")
        shutil.copytree(ROOT / "data/v2_reviewed_ai", output / "data/v2_reviewed_ai")
        shutil.copy2(ROOT / "docs/revision_asistida_v0_2.md", output / "assistant_review.md")
    manifest = {"stage": "experimental_frozen_candidate", "approved_for_deployment": False,
                "human_review": "pending", "base_model": run["model"], "base_revision": run["revision"],
                "summary_policy": "exact_template_v1", "new_training": False,
                "assistant_review": "completed" if review is not None else "not_included",
                "training_source_sha256": sha(run_dir / "run.json"), "evaluation_summary": report["summary"],
                "files": {str(p.relative_to(output)).replace('\\', '/'): sha(p) for p in output.rglob('*') if p.is_file()}}
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", type=Path, required=True)
    parser.add_argument("--evaluation", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--review", type=Path)
    args = parser.parse_args()
    print(json.dumps(package(args.run, args.evaluation, args.output, args.review), ensure_ascii=False, indent=2))
