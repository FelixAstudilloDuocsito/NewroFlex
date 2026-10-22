"""Inferencia local desde el directorio del candidato empaquetado."""
import argparse
import hashlib
import json
import os
import sys
from pathlib import Path


def main():
    root = Path(__file__).resolve().parent
    manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
    for relative, expected in manifest["files"].items():
        path = (root / relative).resolve()
        if not path.is_relative_to(root):
            raise ValueError("Ruta fuera del paquete")
        if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            raise ValueError(f"Archivo modificado: {relative}")
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--cache-dir", type=Path, help="Carpeta HF_HOME con la base ya descargada")
    args = parser.parse_args()
    if args.cache_dir is not None:
        os.environ["HF_HOME"] = str(args.cache_dir.resolve())
    if not os.environ.get("HF_HOME"):
        raise ValueError("Indica --cache-dir o HF_HOME para evitar usar una caché inesperada en el SSD")
    from evaluation.contracts import process
    payload = json.loads(args.input.read_text(encoding="utf-8"))
    expected = process(payload)
    import torch
    from transformers import AutoModelForCausalLM, AutoTokenizer, BitsAndBytesConfig
    from peft import PeftModel, prepare_model_for_kbit_training
    dtype = torch.bfloat16 if torch.cuda.is_bf16_supported() else torch.float16
    tokenizer = AutoTokenizer.from_pretrained(root / "tokenizer", local_files_only=True)
    model = AutoModelForCausalLM.from_pretrained(manifest["base_model"], revision=manifest["base_revision"],
        local_files_only=True,
        quantization_config=BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_quant_type="nf4", bnb_4bit_use_double_quant=True, bnb_4bit_compute_dtype=dtype),
        device_map={"": 0}, dtype=dtype, attn_implementation="sdpa")
    model = prepare_model_for_kbit_training(model, use_gradient_checkpointing=False)
    model = PeftModel.from_pretrained(model, root / "adapter", is_trainable=False).eval()
    messages = [{"role": "system", "content": (root / "system.txt").read_text(encoding="utf-8")},
                {"role": "user", "content": json.dumps(expected, ensure_ascii=False)}]
    inputs = tokenizer.apply_chat_template(messages, tokenize=True, add_generation_prompt=True,
        enable_thinking=False, return_dict=True, return_tensors="pt").to("cuda")
    with torch.inference_mode():
        output = model.generate(**inputs, do_sample=False, max_new_tokens=1200,
            eos_token_id=tokenizer.eos_token_id, pad_token_id=tokenizer.pad_token_id)
    answer = json.loads(tokenizer.decode(output[0, inputs["input_ids"].shape[1]:], skip_special_tokens=True))
    from evaluation.summary_guard import guard
    answer, audit = guard(payload, answer)
    print(json.dumps(audit, ensure_ascii=False), file=sys.stderr)
    print(json.dumps(answer, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
