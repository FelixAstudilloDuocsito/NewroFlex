# Muestra de referencias v2 para revisi?n

Una referencia de entrenamiento por escenario. Revisi?n t?cnica asistida; pendiente de revisi?n humana. Las 150 referencias de desarrollo est?n en data/sft_v2_draft/revision.csv.

## ascenso

Identificador: `v2-ascenso-p0-v0`

Se registraron 3 sesiones. Precisión: aumentó de 49.579 % a 54.092 %. Cambio: 4.513 puntos porcentuales. Tiempo de respuesta: aumentó de 3801.3 ms a 4258.54 ms. Cambio: 12.03 % respecto al valor inicial. Desviación de mirada: aumentó de 52.589 % a 56.517 %. Cambio: 3.928 puntos porcentuales. Índice de temblor: aumentó de 0.106 unidades de índice a 0.129 unidades de índice. Cambio: 0.023 unidades de índice. Duración: aumentó de 428.702 s a 465.955 s. Cambio: 8.69 % respecto al valor inicial. El resumen describe métricas y no establece conclusiones clínicas. La comparación de extremos no demuestra una tendencia continua.

Entrada original:

```json
{
  "sessions": [
    {
      "session_number": 1,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 49.579,
        "average_response_time_ms": 3801.297,
        "gaze_deviation_percentage": 52.589,
        "controller_tremor_index": 0.106,
        "session_duration_seconds": 428.702
      }
    },
    {
      "session_number": 2,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 51.835,
        "average_response_time_ms": 4029.918,
        "gaze_deviation_percentage": 54.553,
        "controller_tremor_index": 0.118,
        "session_duration_seconds": 447.329
      }
    },
    {
      "session_number": 3,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 54.092,
        "average_response_time_ms": 4258.54,
        "gaze_deviation_percentage": 56.517,
        "controller_tremor_index": 0.129,
        "session_duration_seconds": 465.955
      }
    }
  ]
}
```

## descenso

Identificador: `v2-descenso-p0-v0`

Se registraron 5 sesiones. Precisión: disminuyó de 62.379 % a 45.236 %. Cambio: -17.143 puntos porcentuales. Tiempo de respuesta: disminuyó de 6482.16 ms a 5734.87 ms. Cambio: -11.53 % respecto al valor inicial. Desviación de mirada: disminuyó de 33.457 % a 22.745 %. Cambio: -10.712 puntos porcentuales. Índice de temblor: disminuyó de 0.308 unidades de índice a 0.259 unidades de índice. Cambio: -0.049 unidades de índice. Duración: disminuyó de 536.235 s a 485.689 s. Cambio: -9.43 % respecto al valor inicial. El resumen describe métricas y no establece conclusiones clínicas. La comparación de extremos no demuestra una tendencia continua.

Entrada original:

```json
{
  "sessions": [
    {
      "session_number": 1,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 62.379,
        "average_response_time_ms": 6482.165,
        "gaze_deviation_percentage": 33.457,
        "controller_tremor_index": 0.308,
        "session_duration_seconds": 536.235
      }
    },
    {
      "session_number": 2,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 58.093,
        "average_response_time_ms": 6295.34,
        "gaze_deviation_percentage": 30.779,
        "controller_tremor_index": 0.296,
        "session_duration_seconds": 523.599
      }
    },
    {
      "session_number": 3,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 53.807,
        "average_response_time_ms": 6108.516,
        "gaze_deviation_percentage": 28.101,
        "controller_tremor_index": 0.283,
        "session_duration_seconds": 510.962
      }
    },
    {
      "session_number": 4,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 49.522,
        "average_response_time_ms": 5921.691,
        "gaze_deviation_percentage": 25.423,
        "controller_tremor_index": 0.271,
        "session_duration_seconds": 498.326
      }
    },
    {
      "session_number": 5,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 45.236,
        "average_response_time_ms": 5734.866,
        "gaze_deviation_percentage": 22.745,
        "controller_tremor_index": 0.259,
        "session_duration_seconds": 485.689
      }
    }
  ]
}
```

## estable

Identificador: `v2-estable-p0-v0`

Se registraron 5 sesiones. Precisión: sin variación entre extremos (32.989 %). Cambio: 0 puntos porcentuales. Tiempo de respuesta: sin variación entre extremos (6510.01 ms). Cambio: 0 % respecto al valor inicial. Desviación de mirada: sin variación entre extremos (46.859 %). Cambio: 0 puntos porcentuales. Índice de temblor: sin variación entre extremos (0.356 unidades de índice). Cambio: 0 unidades de índice. Duración: sin variación entre extremos (210.217 s). Cambio: 0 % respecto al valor inicial. El resumen describe métricas y no establece conclusiones clínicas. La comparación de extremos no demuestra una tendencia continua.

Entrada original:

```json
{
  "sessions": [
    {
      "session_number": 1,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 32.989,
        "average_response_time_ms": 6510.013,
        "gaze_deviation_percentage": 46.859,
        "controller_tremor_index": 0.356,
        "session_duration_seconds": 210.217
      }
    },
    {
      "session_number": 2,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 32.989,
        "average_response_time_ms": 6510.013,
        "gaze_deviation_percentage": 46.859,
        "controller_tremor_index": 0.356,
        "session_duration_seconds": 210.217
      }
    },
    {
      "session_number": 3,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 32.989,
        "average_response_time_ms": 6510.013,
        "gaze_deviation_percentage": 46.859,
        "controller_tremor_index": 0.356,
        "session_duration_seconds": 210.217
      }
    },
    {
      "session_number": 4,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 32.989,
        "average_response_time_ms": 6510.013,
        "gaze_deviation_percentage": 46.859,
        "controller_tremor_index": 0.356,
        "session_duration_seconds": 210.217
      }
    },
    {
      "session_number": 5,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 32.989,
        "average_response_time_ms": 6510.013,
        "gaze_deviation_percentage": 46.859,
        "controller_tremor_index": 0.356,
        "session_duration_seconds": 210.217
      }
    }
  ]
}
```

## mixto

Identificador: `v2-mixto-p0-v0`

Se registraron 3 sesiones. Precisión: aumentó de 43.655 % a 50.385 %. Cambio: 6.73 puntos porcentuales. Tiempo de respuesta: disminuyó de 2224.72 ms a 2001.88 ms. Cambio: -10.02 % respecto al valor inicial. Desviación de mirada: aumentó de 49.33 % a 52.751 %. Cambio: 3.421 puntos porcentuales. Índice de temblor: disminuyó de 0.159 unidades de índice a 0.139 unidades de índice. Cambio: -0.02 unidades de índice. Duración: aumentó de 601.583 s a 640.447 s. Cambio: 6.46 % respecto al valor inicial. El resumen describe métricas y no establece conclusiones clínicas. La comparación de extremos no demuestra una tendencia continua.

Entrada original:

```json
{
  "sessions": [
    {
      "session_number": 1,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 43.655,
        "average_response_time_ms": 2224.723,
        "gaze_deviation_percentage": 49.33,
        "controller_tremor_index": 0.159,
        "session_duration_seconds": 601.583
      }
    },
    {
      "session_number": 2,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 47.02,
        "average_response_time_ms": 2113.302,
        "gaze_deviation_percentage": 51.041,
        "controller_tremor_index": 0.149,
        "session_duration_seconds": 621.015
      }
    },
    {
      "session_number": 3,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 50.385,
        "average_response_time_ms": 2001.881,
        "gaze_deviation_percentage": 52.751,
        "controller_tremor_index": 0.139,
        "session_duration_seconds": 640.447
      }
    }
  ]
}
```

## unica

Identificador: `v2-unica-p0-v0`

Se registraron 1 sesiones. Precisión: 61.569 %. Tiempo de respuesta: 5097.2 ms. Desviación de mirada: 58.523 %. Índice de temblor: 0.236 unidades de índice. Duración: 759.835 s. Una sola sesión no permite comparación temporal. El resumen describe métricas y no establece conclusiones clínicas.

Entrada original:

```json
{
  "sessions": [
    {
      "session_number": 1,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 61.569,
        "average_response_time_ms": 5097.198,
        "gaze_deviation_percentage": 58.523,
        "controller_tremor_index": 0.236,
        "session_duration_seconds": 759.835
      }
    }
  ]
}
```

## dificultad

Identificador: `v2-dificultad-p0-v0`

Se registraron 4 sesiones. Precisión: aumentó de 43.076 % a 46.876 %. Cambio: 3.8 puntos porcentuales. Tiempo de respuesta: aumentó de 2482.33 ms a 2979.79 ms. Cambio: 20.04 % respecto al valor inicial. Desviación de mirada: aumentó de 51.561 % a 58.003 %. Cambio: 6.442 puntos porcentuales. Índice de temblor: aumentó de 0.159 unidades de índice a 0.175 unidades de índice. Cambio: 0.016 unidades de índice. Duración: aumentó de 489.911 s a 517.304 s. Cambio: 5.59 % respecto al valor inicial. La dificultad varía entre sesiones y limita la comparación. El resumen describe métricas y no establece conclusiones clínicas. La comparación de extremos no demuestra una tendencia continua.

Entrada original:

```json
{
  "sessions": [
    {
      "session_number": 1,
      "difficulty": "easy",
      "metrics": {
        "accuracy_percentage": 43.076,
        "average_response_time_ms": 2482.327,
        "gaze_deviation_percentage": 51.561,
        "controller_tremor_index": 0.159,
        "session_duration_seconds": 489.911
      }
    },
    {
      "session_number": 2,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 44.343,
        "average_response_time_ms": 2648.149,
        "gaze_deviation_percentage": 53.708,
        "controller_tremor_index": 0.164,
        "session_duration_seconds": 499.042
      }
    },
    {
      "session_number": 3,
      "difficulty": "hard",
      "metrics": {
        "accuracy_percentage": 45.61,
        "average_response_time_ms": 2813.971,
        "gaze_deviation_percentage": 55.856,
        "controller_tremor_index": 0.17,
        "session_duration_seconds": 508.173
      }
    },
    {
      "session_number": 4,
      "difficulty": "hard",
      "metrics": {
        "accuracy_percentage": 46.876,
        "average_response_time_ms": 2979.794,
        "gaze_deviation_percentage": 58.003,
        "controller_tremor_index": 0.175,
        "session_duration_seconds": 517.304
      }
    }
  ]
}
```

## faltante_inicial

Identificador: `v2-faltante_inicial-p0-v0`

Se registraron 3 sesiones. Precisión: valor final 71.302 %; comparación no disponible por datos faltantes. Tiempo de respuesta: aumentó de 3820.65 ms a 4246.29 ms. Cambio: 11.14 % respecto al valor inicial. Desviación de mirada: aumentó de 48.805 % a 53.724 %. Cambio: 4.919 puntos porcentuales. Índice de temblor: aumentó de 0.473 unidades de índice a 0.487 unidades de índice. Cambio: 0.014 unidades de índice. Duración: aumentó de 474.942 s a 503.902 s. Cambio: 6.1 % respecto al valor inicial. Hay datos faltantes en las sesiones registradas. El resumen describe métricas y no establece conclusiones clínicas. La comparación de extremos no demuestra una tendencia continua.

Entrada original:

```json
{
  "sessions": [
    {
      "session_number": 1,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": null,
        "average_response_time_ms": 3820.654,
        "gaze_deviation_percentage": 48.805,
        "controller_tremor_index": 0.473,
        "session_duration_seconds": 474.942
      }
    },
    {
      "session_number": 2,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 70.184,
        "average_response_time_ms": 4033.473,
        "gaze_deviation_percentage": 51.265,
        "controller_tremor_index": 0.48,
        "session_duration_seconds": 489.422
      }
    },
    {
      "session_number": 3,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 71.302,
        "average_response_time_ms": 4246.292,
        "gaze_deviation_percentage": 53.724,
        "controller_tremor_index": 0.487,
        "session_duration_seconds": 503.902
      }
    }
  ]
}
```

## faltante_final

Identificador: `v2-faltante_final-p0-v0`

Se registraron 4 sesiones. Precisión: valor inicial 36.865 %; comparación no disponible por datos faltantes. Tiempo de respuesta: aumentó de 3152.67 ms a 3477.04 ms. Cambio: 10.29 % respecto al valor inicial. Desviación de mirada: aumentó de 16.369 % a 20.687 %. Cambio: 4.318 puntos porcentuales. Índice de temblor: aumentó de 0.595 unidades de índice a 0.619 unidades de índice. Cambio: 0.024 unidades de índice. Duración: aumentó de 206.732 s a 228.76 s. Cambio: 10.66 % respecto al valor inicial. Hay datos faltantes en las sesiones registradas. El resumen describe métricas y no establece conclusiones clínicas. La comparación de extremos no demuestra una tendencia continua.

Entrada original:

```json
{
  "sessions": [
    {
      "session_number": 1,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 36.865,
        "average_response_time_ms": 3152.669,
        "gaze_deviation_percentage": 16.369,
        "controller_tremor_index": 0.595,
        "session_duration_seconds": 206.732
      }
    },
    {
      "session_number": 2,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 40.465,
        "average_response_time_ms": 3260.793,
        "gaze_deviation_percentage": 17.809,
        "controller_tremor_index": 0.603,
        "session_duration_seconds": 214.075
      }
    },
    {
      "session_number": 3,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 44.065,
        "average_response_time_ms": 3368.916,
        "gaze_deviation_percentage": 19.248,
        "controller_tremor_index": 0.611,
        "session_duration_seconds": 221.417
      }
    },
    {
      "session_number": 4,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": null,
        "average_response_time_ms": 3477.04,
        "gaze_deviation_percentage": 20.687,
        "controller_tremor_index": 0.619,
        "session_duration_seconds": 228.76
      }
    }
  ]
}
```

## faltante_intermedio

Identificador: `v2-faltante_intermedio-p0-v0`

Se registraron 3 sesiones. Precisión: aumentó de 36.518 % a 43.873 %. Cambio: 7.355 puntos porcentuales. Tiempo de respuesta: aumentó de 4989.47 ms a 5432.86 ms. Cambio: 8.89 % respecto al valor inicial. Desviación de mirada: aumentó de 35.974 % a 38.385 %. Cambio: 2.411 puntos porcentuales. Índice de temblor: aumentó de 0.671 unidades de índice a 0.688 unidades de índice. Cambio: 0.017 unidades de índice. Duración: aumentó de 252.703 s a 290.122 s. Cambio: 14.81 % respecto al valor inicial. Hay datos faltantes en las sesiones registradas. El resumen describe métricas y no establece conclusiones clínicas. La comparación de extremos no demuestra una tendencia continua.

Entrada original:

```json
{
  "sessions": [
    {
      "session_number": 1,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 36.518,
        "average_response_time_ms": 4989.468,
        "gaze_deviation_percentage": 35.974,
        "controller_tremor_index": 0.671,
        "session_duration_seconds": 252.703
      }
    },
    {
      "session_number": 2,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": null,
        "average_response_time_ms": 5211.164,
        "gaze_deviation_percentage": 37.18,
        "controller_tremor_index": 0.679,
        "session_duration_seconds": 271.412
      }
    },
    {
      "session_number": 3,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 43.873,
        "average_response_time_ms": 5432.859,
        "gaze_deviation_percentage": 38.385,
        "controller_tremor_index": 0.688,
        "session_duration_seconds": 290.122
      }
    }
  ]
}
```

## base_cero

Identificador: `v2-base_cero-p0-v0`

Se registraron 5 sesiones. Precisión: aumentó de 28.598 % a 41.649 %. Cambio: 13.051 puntos porcentuales. Tiempo de respuesta: aumentó de 0 ms a 3998.26 ms. Cambio porcentual no calculable porque el valor inicial es cero. Desviación de mirada: aumentó de 56.995 % a 61.157 %. Cambio: 4.162 puntos porcentuales. Índice de temblor: aumentó de 0.178 unidades de índice a 0.232 unidades de índice. Cambio: 0.054 unidades de índice. Duración: aumentó de 336.074 s a 398.216 s. Cambio: 18.49 % respecto al valor inicial. Una base cero impide calcular el cambio porcentual; no equivale a dato ausente. El resumen describe métricas y no establece conclusiones clínicas. La comparación de extremos no demuestra una tendencia continua.

Entrada original:

```json
{
  "sessions": [
    {
      "session_number": 1,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 28.598,
        "average_response_time_ms": 0,
        "gaze_deviation_percentage": 56.995,
        "controller_tremor_index": 0.178,
        "session_duration_seconds": 336.074
      }
    },
    {
      "session_number": 2,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 31.861,
        "average_response_time_ms": 3505.764,
        "gaze_deviation_percentage": 58.035,
        "controller_tremor_index": 0.191,
        "session_duration_seconds": 351.61
      }
    },
    {
      "session_number": 3,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 35.124,
        "average_response_time_ms": 3669.931,
        "gaze_deviation_percentage": 59.076,
        "controller_tremor_index": 0.205,
        "session_duration_seconds": 367.145
      }
    },
    {
      "session_number": 4,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 38.386,
        "average_response_time_ms": 3834.098,
        "gaze_deviation_percentage": 60.116,
        "controller_tremor_index": 0.218,
        "session_duration_seconds": 382.681
      }
    },
    {
      "session_number": 5,
      "difficulty": "medium",
      "metrics": {
        "accuracy_percentage": 41.649,
        "average_response_time_ms": 3998.265,
        "gaze_deviation_percentage": 61.157,
        "controller_tremor_index": 0.232,
        "session_duration_seconds": 398.216
      }
    }
  ]
}
```
