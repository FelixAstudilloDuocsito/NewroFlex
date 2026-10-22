"""Descripciones de una ronda; cifras y estados calculados fuera del LLM."""
import json
from scenes.contracts import model_input, process

PROMPT = ('Genera exclusivamente un objeto JSON con una clave resumen, en español, describiendo la ronda y sus métricas. '
          'Conserva cifras y unidades; no diagnostiques, recomiendes ni infieras tendencias. '
          'null significa no disponible, no cero. Una ronda no completada tiene tiempo activo, no tiempo de resolución.')


def n(value):
    return str(value).removesuffix('.0')


def render_input(data):
    scene, state, m = data['scenarioId'], data['status'], data['metrics']
    title = {'puzzle': 'Puzzle', 'memory': 'Memoria de tarjetas', 'pigeons': 'Conteo de palomas'}[scene]
    state_text = {'completed': 'completada', 'abandoned': 'abandonada', 'interrupted': 'interrumpida'}[state]
    sentences = [f'{title}: ronda {state_text}.']
    if scene in ('puzzle', 'memory'):
        if m['resolutionTimeSeconds'] is None:
            sentences.append(f"Tiempo activo registrado: {n(m['activeTimeSeconds'])} s; no se informa tiempo de resolución porque la ronda no se completó.")
        else:
            sentences.append(f"Tiempo de resolución: {n(m['resolutionTimeSeconds'])} s activos.")
        if scene == 'puzzle':
            sentences.append(f"Piezas completadas: {m['completedPieces']} de {m['totalPieces']}. Movimientos incorrectos: {m['incorrectMoves']}.")
        else:
            accuracy = 'no disponible porque no hubo intentos' if m['matchingAccuracyPercent'] is None else n(m['matchingAccuracyPercent']) + ' %'
            sentences.append(f"Precisión de emparejamiento: {accuracy}. Intentos incorrectos: {m['incorrectAttempts']}.")
            sentences.append(f"Cartas mostradas: {m['cardsShown']}. Parejas encontradas: {m['pairsFound']} de {m['totalPairs']}.")
        sentences.append(f"Ayudas utilizadas: {m['aidsUsed']}.")
    else:
        sentences.append(f"Cantidad real: {m['actualPigeonCount']} palomas.")
        if m['responseCorrect'] is None:
            sentences.append('No se registró respuesta; precisión, margen de error y tiempo de respuesta no disponibles.')
        else:
            correct = 'correcta' if m['responseCorrect'] else 'incorrecta'
            sentences.append(f"Respuesta: {m['userPigeonCount']} palomas ({correct}). Error con signo (respuesta menos real): {m['signedCountError']}; margen absoluto: {m['absoluteCountError']} palomas.")
            sentences.append(f"Tiempo de respuesta desde ocultar las palomas: {n(m['responseTimeSeconds'])} s de reloj, incluida la espera y las pausas. Tiempo activo desde habilitar la respuesta: {n(m['enabledResponseActiveSeconds'])} s.")
    sentences.append('Descripción de esta ronda, sin interpretación clínica ni inferencia de tendencia.')
    return {'resumen': ' '.join(sentences)}


def render(row):
    return render_input(model_input(row))


def guard(row, answer):
    expected = render(row)
    accepted = type(answer) is dict and set(answer) == {'resumen'} and answer == expected
    return (answer if accepted else expected), {'summarySource': 'model_verified_template' if accepted else 'deterministic_fallback'}


def response(payload, generator=None):
    result = process(payload)
    for source, target in zip(payload['rounds'], result['rounds']):
        if generator is None:
            answer, trace = render(source), {'summarySource': 'deterministic_preview'}
        else:
            try:
                answer = generator(model_input(source))
            except (json.JSONDecodeError, ValueError):
                answer = None
            answer, trace = guard(source, answer)
        target.update(answer)
        target.update(trace)
    from evaluation.contracts import validate
    from scenes.output_schema import OUTPUT_SCHEMA
    validate(result, OUTPUT_SCHEMA)
    return result
