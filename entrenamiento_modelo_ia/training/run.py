"""QLoRA: prueba técnica de un paso o entrenamiento sobre datos aprobados."""
import argparse
import hashlib
import json
import os
import re
import time
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
storage_file = ROOT / "storage.local.json"
STORAGE = json.loads(storage_file.read_text(encoding="utf-8-sig")) if storage_file.exists() else {}
os.environ.setdefault("HF_HOME", STORAGE.get("hf_home", str(ROOT / ".cache-training")))
os.environ.setdefault("HF_HUB_DISABLE_TELEMETRY", "1")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_rows(directory, smoke, experimental=False):
    manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
    if not smoke and not experimental and not manifest.get("training_ready"):
        raise ValueError("Dataset borrador: solo permite prueba técnica; falta revisión documentada")
    result = {}
    for split in ("train", "validation"):
        path = directory / f"{split}.jsonl"
        if sha(path) != manifest["files"][path.name]["sha256"]:
            raise ValueError(f"Hash incorrecto: {split}")
        rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
        if not rows or len(rows) != manifest["files"][path.name]["count"]:
            raise ValueError("Dataset vacío o conteo incorrecto")
        for row in rows:
            if [m["role"] for m in row["messages"]] != ["system", "user", "assistant"]:
                raise ValueError("Se espera un ejemplo system/user/assistant")
        result[split] = rows
    return result


def encode(tokenizer, row, limit):
    messages = row["messages"]
    prefix = tokenizer.apply_chat_template(messages[:2], tokenize=True, add_generation_prompt=True, enable_thinking=False, return_dict=False)
    # El prefijo incluye los delimitadores del asistente; solo su respuesta recibe pérdida.
    answer = tokenizer.encode(messages[2]["content"], add_special_tokens=False) + [tokenizer.eos_token_id]
    ids = prefix + answer
    if len(ids) > limit:
        raise ValueError(f"Ejemplo de {len(ids)} tokens excede {limit}; no se trunca la respuesta")
    return {"input_ids": ids, "labels": [-100] * len(prefix) + answer}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--smoke", action="store_true")
    parser.add_argument("--experimental", action="store_true", help="Entrena borradores sin presentarlos como revisados ni aptos para entrega")
    parser.add_argument("--model", default="Qwen/Qwen3-4B")
    parser.add_argument("--revision", default="main")
    parser.add_argument("--dataset", type=Path, default=ROOT / "data" / "sft_v2_draft")
    parser.add_argument("--output", type=Path, default=Path(STORAGE.get("runs_dir", str(ROOT / "training" / "runs"))) / datetime.now(timezone.utc).strftime("run-%Y%m%dT%H%M%S%fZ"))
    parser.add_argument("--max-length", type=int, default=4096)
    parser.add_argument("--epochs", type=int, default=2)
    args = parser.parse_args()
    rows = load_rows(args.dataset, args.smoke, args.experimental)
    if args.output.exists():
        raise ValueError("El destino ya existe; no se sobrescriben entrenamientos")
    args.output.mkdir(parents=True)
    record = {"status": "starting", "mode": "technical_smoke" if args.smoke else ("experimental_training" if args.experimental else "training"),
              "created_at_utc": datetime.now(timezone.utc).isoformat(), "model": args.model,
              "dataset_manifest_sha256": sha(args.dataset / "manifest.json"), "code_sha256": sha(Path(__file__)),
              "seed": 20261020, "max_length": args.max_length, "steps": [], "eligible_for_release": False}
    def save():
        (args.output / "run.json").write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    save()
    try:
        import importlib.metadata
        import torch
        from huggingface_hub import HfApi
        from transformers import AutoModelForCausalLM, AutoTokenizer, BitsAndBytesConfig, set_seed
        from peft import LoraConfig, get_peft_model, prepare_model_for_kbit_training
        record["packages"] = {p: importlib.metadata.version(p) for p in ("torch", "transformers", "peft", "accelerate", "bitsandbytes")}
        if not torch.cuda.is_available():
            raise RuntimeError("PyTorch no detecta CUDA")
        record["gpu"] = torch.cuda.get_device_name(0)
        record["cuda"] = torch.version.cuda
        revision = args.revision if re.fullmatch(r"[a-f0-9]{40}", args.revision) else HfApi().model_info(args.model, revision=args.revision).sha
        record["revision"] = revision
        save()
        set_seed(record["seed"])
        tokenizer = AutoTokenizer.from_pretrained(args.model, revision=revision, trust_remote_code=False)
        encoded = {split: [encode(tokenizer, r, args.max_length) for r in values] for split, values in rows.items()}
        record["token_lengths"] = {split: {"min": min(len(r["input_ids"]) for r in values), "max": max(len(r["input_ids"]) for r in values)} for split, values in encoded.items()}
        save()
        dtype = torch.bfloat16 if torch.cuda.is_bf16_supported() else torch.float16
        model = AutoModelForCausalLM.from_pretrained(args.model, revision=revision, trust_remote_code=False,
            quantization_config=BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_quant_type="nf4", bnb_4bit_use_double_quant=True, bnb_4bit_compute_dtype=dtype),
            device_map={"": 0}, dtype=dtype, attn_implementation="sdpa")
        model.config.use_cache = False
        model = prepare_model_for_kbit_training(model, use_gradient_checkpointing=True)
        model = get_peft_model(model, LoraConfig(r=8, lora_alpha=16, lora_dropout=0.05,
                              target_modules=["q_proj", "v_proj"], task_type="CAUSAL_LM"))
        trainable = [p for p in model.parameters() if p.requires_grad]
        record["trainable_parameters"] = sum(p.numel() for p in trainable)
        record["lora"] = {"r": 8, "alpha": 16, "dropout": .05, "targets": ["q_proj", "v_proj"]}
        record["learning_rate"] = 1e-4
        record["epochs_requested"] = args.epochs
        record["optimizer"] = "AdamW"
        record["batch_size"] = 1
        optimizer = torch.optim.AdamW(trainable, lr=1e-4)
        torch.cuda.reset_peak_memory_stats()
        before = [p.detach().clone() for p in trainable]
        def validation_loss():
            model.eval()
            weighted, tokens = 0.0, 0
            with torch.no_grad():
                for row in encoded["validation"]:
                    batch = {k: torch.tensor([v], device="cuda") for k, v in row.items()}
                    count = sum(v != -100 for v in row["labels"])
                    weighted += model(**batch).loss.item() * count
                    tokens += count
            return weighted / tokens
        if not args.smoke:
            record["validation_before"] = validation_loss()
            save()
        # Smoke: el ejemplo más largo de TRAIN; validation no participa en gradientes.
        batches = [max(encoded["train"], key=lambda r: len(r["input_ids"]))] if args.smoke else encoded["train"]
        epochs = 1 if args.smoke else args.epochs
        started = time.perf_counter()
        for epoch in range(epochs):
            model.train()
            order = torch.randperm(len(batches)).tolist()
            for index in order:
                row = batches[index]
                optimizer.zero_grad(set_to_none=True)
                inputs = {k: torch.tensor([v], device="cuda") for k, v in row.items()}
                inputs["attention_mask"] = torch.ones_like(inputs["input_ids"])
                loss = model(**inputs).loss
                if not torch.isfinite(loss):
                    raise RuntimeError("Pérdida no finita")
                loss.backward()
                norm = torch.nn.utils.clip_grad_norm_(trainable, 1.0, error_if_nonfinite=True)
                optimizer.step()
                record["steps"].append({"epoch": epoch + 1, "loss": loss.item(), "gradient_norm": norm.item()})
                save()
                print(f"Paso {len(record['steps'])}: loss={loss.item():.4f}", flush=True)
            if not args.smoke:
                record.setdefault("validation", []).append({"epoch": epoch + 1, "loss": validation_loss()})
        if before is not None:
            record["adapter_changed"] = any(not torch.equal(old, new.detach()) for old, new in zip(before, trainable))
            if not record["adapter_changed"]:
                raise RuntimeError("No se detectó actualización del adaptador")
        model.save_pretrained(args.output / "adapter")
        tokenizer.save_pretrained(args.output / "tokenizer")
        record["elapsed_seconds"] = time.perf_counter() - started
        record["peak_allocated_gib"] = torch.cuda.max_memory_allocated() / 1024 ** 3
        record["peak_reserved_gib"] = torch.cuda.max_memory_reserved() / 1024 ** 3
        record["status"] = "completed"
        save()
    except Exception as exc:
        record["status"] = "failed"
        record["error"] = f"{type(exc).__name__}: {exc}"
        save()
        raise


if __name__ == "__main__":
    main()
