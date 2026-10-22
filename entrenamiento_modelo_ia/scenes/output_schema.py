"""Contrato de respuesta listo para serializar en el futuro servidor."""
import json
from scenes.contracts import ROOT, VERSION, obj, enum, text, integer, nullable

NUMBER = {"type": "number", "minimum": 0}
METRICS = {
    'puzzle': {'resolutionTimeSeconds': nullable(NUMBER), 'activeTimeSeconds': NUMBER, 'incorrectMoves': integer(),
               'completedPieces': integer(), 'totalPieces': integer(1), 'aidsUsed': integer()},
    'memory': {'matchingAccuracyPercent': nullable({'type': 'number', 'minimum': 0, 'maximum': 100}),
               'incorrectAttempts': integer(), 'resolutionTimeSeconds': nullable(NUMBER), 'activeTimeSeconds': NUMBER,
               'cardsShown': integer(2), 'pairsFound': integer(), 'totalPairs': integer(1), 'aidsUsed': integer()},
    'pigeons': {'responseCorrect': nullable({'type': 'boolean'}), 'signedCountError': nullable({'type': 'integer'}),
                'absoluteCountError': nullable(integer()), 'actualPigeonCount': integer(), 'userPigeonCount': nullable(integer()),
                'enabledResponseActiveSeconds': nullable(NUMBER), 'responseTimeSeconds': nullable(NUMBER)},
}
WARNING = enum('descriptive_only', 'round_not_completed', 'training_mode', 'distractions_configured', 'no_pair_attempts', 'no_answer')
ROUND = {'anyOf': [obj({'roundId': text(), 'sessionId': text(), 'scenarioId': enum(scene),
    'status': enum('completed', 'abandoned', 'interrupted'), 'metrics': obj(fields),
    'warnings': {'type': 'array', 'minItems': 1, 'maxItems': 6, 'items': WARNING},
    'resumen': {'type': 'string', 'minLength': 1, 'maxLength': 3000},
    'summarySource': enum('model_verified_template', 'deterministic_fallback', 'deterministic_preview')}) for scene, fields in METRICS.items()]}
OUTPUT_SCHEMA = {'$schema': 'https://json-schema.org/draft/2020-12/schema', **obj({
    'contractVersion': enum(VERSION), 'participantId': text(),
    'rounds': {'type': 'array', 'minItems': 1, 'maxItems': 100, 'items': ROUND},
    'comparisons': {'type': 'array', 'maxItems': 100, 'items': obj({'comparisonGroupId': text(),
        'roundIds': {'type': 'array', 'minItems': 1, 'maxItems': 100, 'items': text()}, 'comparable': {'type': 'boolean'},
        'reasons': {'type': 'array', 'maxItems': 4, 'items': enum('insufficient_rounds', 'incomplete_rounds', 'not_comparison_mode', 'different_conditions')}})}})}


if __name__ == '__main__':
    (ROOT / 'schemas/scenes-output.schema.json').write_text(json.dumps(OUTPUT_SCHEMA, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
