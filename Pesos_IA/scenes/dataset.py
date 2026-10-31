"""Corpus sintético separado por participantes; sin copiar los datos anteriores."""
import json
import random
from pathlib import Path
from datetime import datetime, timedelta, timezone
from scenes.contracts import ROOT, VERSION, INPUT_SCHEMA, model_input, process
from scenes.summaries import render, PROMPT
from training.run import sha


def example(scene, profile, variant, rng=None):
    rng = rng or random.Random(profile * 101 + variant)
    start = datetime(2026, 10, 1, tzinfo=timezone.utc) + timedelta(days=profile, minutes=variant * 4)
    stamp = lambda t: t.isoformat(timespec='milliseconds').replace('+00:00', 'Z')
    status = ['completed', 'completed', 'abandoned', 'interrupted', 'completed', 'abandoned', 'completed', 'interrupted'][variant]
    active = rng.randint(10000, 80000)
    row = {'participantId': f'synthetic-p{profile}', 'sessionId': f'synthetic-s{profile}-{variant}',
        'roundId': f'{scene}-p{profile}-v{variant}', 'scenarioId': scene, 'difficultyLevel': variant % 3,
        'startedAt': stamp(start), 'endedAt': stamp(start + timedelta(seconds=120)), 'status': status,
        'pausedDurationMs': 2000 if variant % 2 else 0, 'scenarioVersion': '1.0.0',
        'assessmentMode': 'comparison' if variant % 2 else 'training',
        'distractionsEnabled': variant % 3 == 1, 'distractionType': 'visual' if variant % 3 == 1 else 'none',
        'distractionCount': 2 if variant % 3 == 1 else 0, 'distractionIntensity': 'low' if variant % 3 == 1 else 'none',
        'distractionDurationMs': 1500 if variant % 3 == 1 else 0, 'comparisonGroupId': None}
    if scene == 'puzzle':
        total = rng.randint(5, 24)
        row.update(totalPieces=total, correctPlacements=total if status == 'completed' else rng.randrange(total),
                   incorrectPlacements=rng.randint(0, 9), aidsUsed=rng.randint(0, 3), activeTimeMs=active,
                   puzzleId=f'puzzle-{profile}', rotationEnabled=bool(variant % 2))
    elif scene == 'memory':
        total = rng.randint(3, 10)
        found = total if status == 'completed' else (0 if variant == 5 else rng.randrange(total))
        row.update(totalCards=total * 2, totalPairs=total, correctPairAttempts=found,
                   incorrectPairAttempts=0 if variant == 5 else rng.randint(0, 10), pairsFound=found,
                   activeTimeMs=active, aidsUsed=rng.randint(0, 3), previewDurationMs=3000, deckId=f'deck-{profile}')
    else:
        actual = rng.randint(0, 20)
        answered = status == 'completed' or variant == 2
        hidden = start + timedelta(seconds=3)
        enabled = hidden + timedelta(seconds=2)
        elapsed = rng.randint(500, 15000)
        submitted = enabled + timedelta(milliseconds=elapsed)
        row.update(actualPigeonCount=actual, userPigeonCount=(actual if variant % 3 == 0 else max(0, actual + (2 if variant % 2 else -1))) if answered else None,
                   displayDurationMs=3000, responseTimeMs=elapsed if answered else None,
                   pigeonsHiddenAt=stamp(hidden), answerEnabledAt=stamp(enabled), answerSubmittedAt=stamp(submitted) if answered else None,
                   movementLevel=['static', 'low', 'medium', 'high'][variant % 4], retentionDelayMs=2000)
    return row


def main():
    target, sft = ROOT / 'data/scenes_v1_1', ROOT / 'data/sft_scenes_v1_1'
    if target.exists() or sft.exists(): raise ValueError('No se sobrescriben datasets')
    target.mkdir(); sft.mkdir()
    (ROOT / 'schemas/scenes-input.schema.json').write_text(json.dumps(INPUT_SCHEMA, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    manifest = {'version': VERSION, 'source': 'synthetic', 'reviewer': 'Codex (AI)', 'human_review': 'not_performed',
                'generator_sha256': sha(Path(__file__)), 'files': {}}
    training = {'training_ready': False, 'status': 'experimental_ai_reviewed', 'files': {}}
    rng = random.Random(20261021)
    partitions = {key: [] for key in ('train', 'validation', 'test')}
    for profile in range(6):
        split = 'train' if profile < 4 else ('validation' if profile == 4 else 'test')
        for scene in ('puzzle', 'memory', 'pigeons'):
            for variant in range(8):
                row = example(scene, profile, variant, rng)
                process({'contractVersion': VERSION, 'rounds': [row]})
                partitions[split].append({'id': row['roundId'], 'group_id': row['participantId'], 'input': row,
                    'expected_output': render(row), 'source': 'synthetic', 'review_status': 'reviewed_by_ai'})
    for split, rows in partitions.items():
        path = target / f'{split}.jsonl'
        path.write_text(''.join(json.dumps(row, ensure_ascii=False) + '\n' for row in rows), encoding='utf-8')
        manifest['files'][path.name] = {'count': len(rows), 'sha256': sha(path)}
        if split != 'test':
            path = sft / f'{split}.jsonl'
            conversations = [{'messages': [{'role': 'system', 'content': PROMPT},
                {'role': 'user', 'content': json.dumps(model_input(row['input']), ensure_ascii=False)},
                {'role': 'assistant', 'content': json.dumps(row['expected_output'], ensure_ascii=False)}]} for row in rows]
            path.write_text(''.join(json.dumps(row, ensure_ascii=False) + '\n' for row in conversations), encoding='utf-8')
            training['files'][path.name] = {'count': len(rows), 'sha256': sha(path)}
    (target / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    training['source_manifest_sha256'] = sha(target / 'manifest.json')
    (sft / 'manifest.json').write_text(json.dumps(training, indent=2) + '\n', encoding='utf-8')
    examples = {'contractVersion': VERSION, 'rounds': [example(scene, 0, 0) for scene in ('puzzle', 'memory', 'pigeons')]}
    (target / 'example_request.json').write_text(json.dumps(examples, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print({split: len(rows) for split, rows in partitions.items()})


if __name__ == '__main__': main()
