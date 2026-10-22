"""Comparación reproducible base/adaptador sobre una partición, sin actualizar pesos."""
import argparse
import json
import time
from contextlib import nullcontext
from pathlib import Path
from training.run import ROOT, sha  # configura HF_HOME antes de importar Transformers
from evaluation.contracts import process
from evaluation.evaluate import score


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", type=Path, required=True)
    parser.add_argument("--split", choices=["validation", "test"], default="validation")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--batch-size", type=int, choices=[1, 2, 4], default=4)
    args = parser.parse_args()
    run = json.loads((args.run / "run.json").read_text(encoding="utf-8"))
    if run["status"] != "completed" or run["mode"] == "technical_smoke":
        raise ValueError("Se requiere entrenamiento completo")
    if args.output.exists():
        raise ValueError("El informe ya existe")
    import torch
    from transformers import AutoModelForCausalLM, AutoTokenizer, BitsAndBytesConfig
    from peft import PeftModel, prepare_model_for_kbit_training
    source = ROOT / "data" / "v2" / f"{args.split}.jsonl"
    rows = [json.loads(line) for line in source.read_text(encoding="utf-8").splitlines()]
    prompt_file = ROOT / "evaluation" / "baseline_system.txt"
    prompt = prompt_file.read_text(encoding="utf-8")
    report = {"status": "running", "split": args.split, "model": run["model"], "revision": run["revision"],
              "evaluator_sha256": sha(Path(__file__)),
              "schema_sha256": sha(ROOT / "schemas" / "salida.schema.json"),
              "run_sha256": sha(args.run / "run.json"), "dataset_sha256": sha(source), "prompt_sha256": sha(prompt_file),
              "adapter_sha256": sha(args.run / "adapter" / "adapter_model.safetensors"),
              "generation": {"do_sample": False, "max_new_tokens": 1200, "enable_thinking": False, "batch_size": args.batch_size},
              "human_review": "pending", "results": []}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    def save():
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    save()
    try:
        dtype = torch.bfloat16 if torch.cuda.is_bf16_supported() else torch.float16
        tokenizer = AutoTokenizer.from_pretrained(args.run / "tokenizer", local_files_only=True)
        tokenizer.padding_side = "left"
        model = AutoModelForCausalLM.from_pretrained(run["model"], revision=run["revision"], local_files_only=True,
            quantization_config=BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_quant_type="nf4", bnb_4bit_use_double_quant=True, bnb_4bit_compute_dtype=dtype),
            device_map={"": 0}, dtype=dtype, attn_implementation="sdpa")
        model = prepare_model_for_kbit_training(model, use_gradient_checkpointing=False)
        model = PeftModel.from_pretrained(model, args.run / "adapter", is_trainable=False)
        model.eval()
        model.config.use_cache = True
        for offset in range(0, len(rows), args.batch_size):
            group = rows[offset:offset + args.batch_size]
            messages = [[{"role": "system", "content": prompt}, {"role": "user", "content": json.dumps(process(row["input"]), ensure_ascii=False)}] for row in group]
            batch = tokenizer.apply_chat_template(messages, tokenize=True, padding=True, add_generation_prompt=True, enable_thinking=False, return_dict=True, return_tensors="pt").to("cuda")
            results = [{"id": row["id"]} for row in group]
            for name in ("base", "adapted"):
                started = time.perf_counter()
                with torch.inference_mode(), (model.disable_adapter() if name == "base" else nullcontext()):
                    generated = model.generate(**batch, do_sample=False, max_new_tokens=1200, pad_token_id=tokenizer.pad_token_id, eos_token_id=tokenizer.eos_token_id)
                elapsed = time.perf_counter() - started
                for index, row in enumerate(group):
                    tokens = generated[index, batch["input_ids"].shape[1]:].tolist()
                    if tokenizer.eos_token_id in tokens:
                        tokens = tokens[:tokens.index(tokenizer.eos_token_id) + 1]
                    raw = tokenizer.decode(tokens, skip_special_tokens=True)
                    entry = {"raw_output": raw, "latency_seconds": elapsed, "latency_scope": "batch", "generated_tokens": len(tokens)}
                    try:
                        entry["output"] = json.loads(raw)
                        entry["checks"] = score(entry["output"], row["expected_output"])
                        if len(tokens) == 1200 and tokens[-1] != tokenizer.eos_token_id:
                            entry["checks"]["errors"].append("Respuesta truncada")
                    except (ValueError, TypeError) as exc:
                        entry["error"] = str(exc)
                    results[index][name] = entry
            report["results"].extend(results)
            save()
            print(f"Completados {len(report['results'])}/{len(rows)} casos", flush=True)
        report["summary"] = {name: {"total": len(rows), "automatic_pass": sum("checks" in r[name] and not r[name]["checks"]["errors"] for r in report["results"]), "parse_errors": sum("error" in r[name] for r in report["results"])} for name in ("base", "adapted")}
        report["status"] = "completed"
        save()
    except Exception as exc:
        report["status"] = "failed"
        report["error"] = str(exc)
        save()
        raise


if __name__ == "__main__":
    main()
