"""Carga local del adaptador de escenas; sin descargas implícitas."""
import json
from pathlib import Path


class Generator:
    def __init__(self, run_dir):
        import torch
        from transformers import AutoModelForCausalLM, AutoTokenizer, BitsAndBytesConfig
        from peft import PeftModel, prepare_model_for_kbit_training
        self.torch = torch
        self.run_dir = Path(run_dir)
        run = json.loads((self.run_dir / 'run.json').read_text(encoding='utf-8'))
        if run['status'] != 'completed' or not run.get('adapter_changed'):
            raise ValueError('Se requiere entrenamiento completado')
        dtype = torch.bfloat16 if torch.cuda.is_bf16_supported() else torch.float16
        self.tokenizer = AutoTokenizer.from_pretrained(self.run_dir / 'tokenizer', local_files_only=True)
        self.tokenizer.padding_side = 'left'
        model = AutoModelForCausalLM.from_pretrained(run['model'], revision=run['revision'], local_files_only=True,
            quantization_config=BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_quant_type='nf4', bnb_4bit_use_double_quant=True, bnb_4bit_compute_dtype=dtype),
            device_map={'': 0}, dtype=dtype, attn_implementation='sdpa')
        model = prepare_model_for_kbit_training(model, use_gradient_checkpointing=False)
        self.model = PeftModel.from_pretrained(model, self.run_dir / 'adapter', is_trainable=False).eval()
        self.model.config.use_cache = True

    def batch(self, data, base=False):
        from contextlib import nullcontext
        from scenes.summaries import PROMPT
        messages = [[{'role': 'system', 'content': PROMPT}, {'role': 'user', 'content': json.dumps(row, ensure_ascii=False)}] for row in data]
        inputs = self.tokenizer.apply_chat_template(messages, tokenize=True, padding=True, add_generation_prompt=True,
            enable_thinking=False, return_dict=True, return_tensors='pt').to('cuda')
        with self.torch.inference_mode(), (self.model.disable_adapter() if base else nullcontext()):
            tokens = self.model.generate(**inputs, do_sample=False, max_new_tokens=512,
                pad_token_id=self.tokenizer.pad_token_id, eos_token_id=self.tokenizer.eos_token_id)
        return [self.tokenizer.decode(row[inputs['input_ids'].shape[1]:], skip_special_tokens=True) for row in tokens]

    def __call__(self, data):
        return json.loads(self.batch([data])[0])
