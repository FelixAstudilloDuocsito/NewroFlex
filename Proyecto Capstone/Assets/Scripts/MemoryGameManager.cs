using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using NeuroFlexVR;

public class MemoryGameManager : MonoBehaviour
{
    [Header("Configuración de Cartas")]
    public List<MemoryCard> allCards = new List<MemoryCard>();

    [Header("Tiempos de Juego")]
    [Tooltip("Tiempo en segundos que se muestran las cartas al iniciar la partida")]
    public float tiempoVistaPrevia = 2.0f;
    [Tooltip("Tiempo que permanecen volteadas si el par es incorrecto")]
    public float tiempoMemorizacion = 1.0f;

    [Header("Efecto de Acierto")]
    public GameObject prefabEstrella;

    [Header("Eventos")]
    public UnityEvent onPairMatched;
    public UnityEvent onGameWon;

    private MemoryCard firstCard;
    private MemoryCard secondCard;
    private bool isEvaluating = false;
    private bool isPreviewActive = false; // Bloquea la interacción durante la muestra inicial
    private int pairsFound = 0;
    private int totalPairs;
    public int difficultyLevel = 1;
    public int PairsFound => pairsFound;
    public int TotalPairs => totalPairs;
    public int IncorrectAttempts { get; private set; }
    public float ActiveSeconds { get; private set; }
    private bool roundActive;
    private bool applicationPaused;
    private string roundId;
    private string startedAt;
    private string profileId;

    [System.Serializable]
    private class RoundMetrics
    {
        public int correctPairAttempts, incorrectPairAttempts, pairsFound, totalPairs, totalCards, activeTimeMs, aidsUsed, previewDurationMs;
        public string deckId;
    }

    private void Update()
    {
        if (roundActive && !isPreviewActive && !applicationPaused && Time.timeScale > 0)
            ActiveSeconds += Time.unscaledDeltaTime;
    }

    private void OnApplicationPause(bool paused) { applicationPaused = paused; }
    private void OnDisable() { FinishRecord("abandoned"); StopAllCoroutines(); }
    private void OnApplicationQuit() { FinishRecord("interrupted"); }

    [Header("Control de Flujo")]
    [Tooltip("Marcar si este nivel espera a que se cierre el video tutorial. Desmarcar si debe arrancar solo al activarse.")]
    public bool esperarTutorial = false;

    private void OnEnable()
    {
        totalPairs = allCards.Count / 2;

        // Si NO necesita esperar tutorial, arranca de inmediato al activarse el objeto
        if (!esperarTutorial)
        {
            IniciarJuego();
        }
    }

    public void IniciarJuego()
    {
        FinishRecord("abandoned");
        totalPairs = allCards.Count / 2;
        if (allCards.Count == 0 || allCards.Count % 2 != 0 || allCards.Exists(card => card == null))
        {
            Debug.LogError("[NeuroFlex] Configura una lista completa de parejas de cartas.");
            return;
        }
        firstCard = secondCard = null;
        pairsFound = 0;
        IncorrectAttempts = 0;
        ActiveSeconds = 0;
        isEvaluating = false;
        profileId = PatientSession.ActiveProfile?.id;
        roundId = "r-" + System.Guid.NewGuid().ToString("N");
        startedAt = LocalProfileDatabase.UtcNow();
        roundActive = true;
        // Detener corrutinas previas por seguridad para evitar solapamientos
        StopAllCoroutines();
        StartCoroutine(RutinaInicioJuego());
    }

    private IEnumerator RutinaInicioJuego()
    {
        isPreviewActive = true;

        // 1. Inicializar cartas
        for (int i = 0; i < allCards.Count; i++)
        {
            allCards[i].InitCard(this, allCards[i].cardID);
        }

        // 2. Barajar posiciones
        ShufflePositions();

        // Pequeña pausa opcional (0.3s) para que el jugador se acomode
        yield return new WaitForSeconds(0.3f);

        // 3. Voltear todas las cartas boca arriba (mostrar caras)
        for (int i = 0; i < allCards.Count; i++)
        {
            allCards[i].FlipCard(true);
        }

        // 4. Esperar el tiempo configurable (por defecto 2 segundos)
        yield return new WaitForSeconds(tiempoVistaPrevia);

        // 5. Voltear todas las cartas boca abajo (ocultar caras)
        for (int i = 0; i < allCards.Count; i++)
        {
            allCards[i].FlipCard(false);
        }

        // Esperar a que terminen de voltearse boca abajo antes de habilitar los clics
        yield return new WaitForSeconds(0.4f);

        // 6. El jugador ya puede interactuar libremente
        isPreviewActive = false;
    }

    public bool CanFlipCard(MemoryCard card)
    {
        if (!roundActive || card == null || card.isMatched || applicationPaused) return false;
        // No permite voltear si la vista previa inicial está activa o si se está evaluando un par
        if (isPreviewActive || isEvaluating) return false;
        if (card == firstCard) return false;
        if (secondCard != null) return false;

        return true;
    }

    public void OnCardFlipped(MemoryCard card)
    {
        if (!CanFlipCard(card)) return;
        if (firstCard == null)
        {
            firstCard = card;
        }
        else if (secondCard == null)
        {
            secondCard = card;
            StartCoroutine(EvaluarJugada());
        }
    }

    private IEnumerator EvaluarJugada()
    {
        isEvaluating = true;

        yield return new WaitForSeconds(0.4f);

        if (firstCard.cardID == secondCard.cardID)
        {
            firstCard.isMatched = true;
            secondCard.isMatched = true;
            pairsFound++;

            LanzarEstrellas(firstCard.transform);
            LanzarEstrellas(secondCard.transform);

            onPairMatched?.Invoke();

            if (pairsFound >= totalPairs)
            {
                Debug.Log("¡Juego de Memoria Completado!");
                FinishRecord("completed");
                onGameWon?.Invoke();
            }
        }
        else
        {
            IncorrectAttempts++;
            yield return new WaitForSeconds(tiempoMemorizacion);

            firstCard.FlipCard(false);
            secondCard.FlipCard(false);
        }

        firstCard = null;
        secondCard = null;
        isEvaluating = false;
    }

    private void FinishRecord(string status)
    {
        if (!roundActive) return;
        roundActive = false;
        PatientSession.Save(new PatientResult
        {
            id = roundId, profileId = profileId, sessionId = NeuroFlexSession.SessionId,
            scenarioId = "memory", difficulty = difficultyLevel, status = status,
            startedAt = startedAt, endedAt = LocalProfileDatabase.UtcNow(),
            metricsJson = JsonUtility.ToJson(new RoundMetrics
            {
                correctPairAttempts = pairsFound, incorrectPairAttempts = IncorrectAttempts,
                pairsFound = pairsFound, totalPairs = totalPairs,
                totalCards = allCards.Count, previewDurationMs = Mathf.RoundToInt(tiempoVistaPrevia * 1000),
                deckId = "memory-level-" + difficultyLevel,
                activeTimeMs = Mathf.RoundToInt(ActiveSeconds * 1000), aidsUsed = 0
            })
        });
    }

    private void ShufflePositions()
    {
        List<Vector3> originalPositions = new List<Vector3>();
        for (int i = 0; i < allCards.Count; i++)
        {
            originalPositions.Add(allCards[i].transform.position);
        }

        for (int i = 0; i < originalPositions.Count; i++)
        {
            int randomIndex = Random.Range(i, originalPositions.Count);
            Vector3 temp = originalPositions[i];
            originalPositions[i] = originalPositions[randomIndex];
            originalPositions[randomIndex] = temp;
        }

        for (int i = 0; i < allCards.Count; i++)
        {
            allCards[i].transform.position = originalPositions[i];
        }
    }

    private void LanzarEstrellas(Transform cartaTransform)
    {
        if (prefabEstrella == null || cartaTransform == null) return;

        Vector3 spawnPos = cartaTransform.position + Vector3.up * 0.1f;

        Vector3 dirIzquierda = (Vector3.up + Vector3.left * 0.6f).normalized;
        Vector3 dirCentro    = Vector3.up;
        Vector3 dirDerecha   = (Vector3.up + Vector3.right * 0.6f).normalized;

        Vector3[] direcciones = new Vector3[] { dirIzquierda, dirCentro, dirDerecha };

        for (int i = 0; i < direcciones.Length; i++)
        {
            GameObject estrella = Instantiate(prefabEstrella, spawnPos, Quaternion.identity);

            StarFeedback script = estrella.GetComponent<StarFeedback>();
            if (script != null)
            {
                script.IniciarEfecto(direcciones[i]);
            }
        }
    }
}
