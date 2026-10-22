import unittest
from evaluation.contracts import ROOT
from training.run import encode, load_rows


class FakeTokenizer:
    eos_token_id = 9
    def apply_chat_template(self, messages, **kwargs):
        return [1, 2, 3]
    def encode(self, text, **kwargs):
        return [4, 5]


class TrainingTests(unittest.TestCase):
    def test_only_response_has_loss_and_eos(self):
        row = {"messages": [{"role": "system", "content": "x"}, {"role": "user", "content": "y"}, {"role": "assistant", "content": "{}"}]}
        result = encode(FakeTokenizer(), row, 6)
        self.assertEqual(result["input_ids"], [1, 2, 3, 4, 5, 9])
        self.assertEqual(result["labels"], [-100, -100, -100, 4, 5, 9])
        with self.assertRaises(ValueError):
            encode(FakeTokenizer(), row, 5)

    def test_draft_allows_only_smoke(self):
        directory = ROOT / "data" / "sft_v2_draft"
        self.assertEqual(len(load_rows(directory, True)["train"]), 120)
        self.assertEqual(len(load_rows(directory, False, experimental=True)["train"]), 120)
        with self.assertRaises(ValueError):
            load_rows(directory, False)


if __name__ == "__main__":
    unittest.main()
