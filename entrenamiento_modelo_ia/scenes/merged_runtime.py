"""Inferencia del modelo fusionado local, sin PEFT ni descargas."""
import json
from pathlib import Path


class Generator:
    def __init__(self, run_dir):
        import torch
        from transformers import AutoModelForCausalLM, AutoTokenizer

        if not torch.cuda.is_available():
            raise RuntimeError('La entrega fusionada requiere GPU NVIDIA con CUDA')
        self.torch = torch
        self.run_dir = Path(run_dir)
        self.tokenizer = AutoTokenizer.from_pretrained(self.run_dir, local_files_only=True)
        self.tokenizer.padding_side = 'left'
        self.model = AutoModelForCausalLM.from_pretrained(
            self.run_dir, local_files_only=True, device_map={'': 0},
            dtype=torch.float16, attn_implementation='sdpa',
        ).eval()
        self.model.config.use_cache = True

    def batch(self, data, base=False):
        from scenes.summaries import PROMPT

        if base:
            raise ValueError('El modelo fusionado no permite desactivar el adaptador')
        messages = [[{'role': 'system', 'content': PROMPT},
                     {'role': 'user', 'content': json.dumps(row, ensure_ascii=False)}]
                    for row in data]
        inputs = self.tokenizer.apply_chat_template(
            messages, tokenize=True, padding=True, add_generation_prompt=True,
            enable_thinking=False, return_dict=True, return_tensors='pt',
        ).to('cuda')
        with self.torch.inference_mode():
            tokens = self.model.generate(
                **inputs, do_sample=False, max_new_tokens=512,
                pad_token_id=self.tokenizer.pad_token_id,
                eos_token_id=self.tokenizer.eos_token_id,
            )
        return [self.tokenizer.decode(row[inputs['input_ids'].shape[1]:], skip_special_tokens=True)
                for row in tokens]

    def __call__(self, data):
        return json.loads(self.batch([data])[0])
