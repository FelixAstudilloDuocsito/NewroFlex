using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MemoryGameManager : MonoBehaviour
{
    [Header("Lista de Cartas en Tablero")]
    public List<MemoryCard> allCards = new List<MemoryCard>();

    [Header("Tiempos")]
    [Tooltip("Tiempo que se quedan visibles si son distintas antes de voltearse de nuevo")]
    public float tiempoMemorizacion = 1.2f;

    [Header("Eventos")]
    public UnityEvent onPairMatched;
    public UnityEvent onGameWon;

    private MemoryCard firstCard = null;
    private MemoryCard secondCard = null;
    private bool isEvaluating = false;
    private int pairsFound = 0;
    private int totalPairs;

    private void Start()
    {
        totalPairs = allCards.Count / 2;

        // 1. Inicializar cartas con sus IDs
        for (int i = 0; i < allCards.Count; i++)
        {
            allCards[i].InitCard(this, allCards[i].cardID);
        }

        // 2. Barajar sus posiciones en el tablero
        ShufflePositions();
    }


    public bool CanFlipCard(MemoryCard card)
    {
        // No permitir voltear si ya estamos comparando dos cartas o si es la misma primera carta
        return !isEvaluating && card != firstCard;
    }

    public void OnCardFlipped(MemoryCard card)
    {
        if (firstCard == null)
        {
            // Primera carta seleccionada
            firstCard = card;
        }
        else if (secondCard == null)
        {
            // Segunda carta seleccionada: evaluar par
            secondCard = card;
            StartCoroutine(EvaluarJugada());
        }
    }


    [Header("Efecto de Acierto")]
    public GameObject prefabEstrella;

    private IEnumerator EvaluarJugada()
    {
        isEvaluating = true;

        // Esperar a que termine de girar
        yield return new WaitForSeconds(0.4f);

        if (firstCard.cardID == secondCard.cardID)
        {
            // ¡Son iguales!
            firstCard.isMatched = true;
            secondCard.isMatched = true;
            pairsFound++;

            // Lanzar las estrellas en ambas cartas
            LanzarEstrellas(firstCard.transform);
            LanzarEstrellas(secondCard.transform);

            onPairMatched?.Invoke();

            if (pairsFound >= totalPairs)
            {
                Debug.Log("¡Juego de Memoria Completado!");
                onGameWon?.Invoke();
            }
        }
        else
        {
            // Son distintas: dejamos que las memoricen un momento y las volteamos de nuevo
            yield return new WaitForSeconds(tiempoMemorizacion);

            firstCard.FlipCard(false);
            secondCard.FlipCard(false);
        }

        // Reiniciar turno
        firstCard = null;
        secondCard = null;
        isEvaluating = false;
    }
    
    private void ShufflePositions()
    {
        // Guardamos todas las posiciones originales de la cuadrícula
        List<Vector3> originalPositions = new List<Vector3>();
        for (int i = 0; i < allCards.Count; i++)
        {
            originalPositions.Add(allCards[i].transform.position);
        }

        // Barajamos la lista de posiciones (Fisher-Yates)
        for (int i = 0; i < originalPositions.Count; i++)
        {
            int randomIndex = Random.Range(i, originalPositions.Count);
            Vector3 temp = originalPositions[i];
            originalPositions[i] = originalPositions[randomIndex];
            originalPositions[randomIndex] = temp;
        }

        // Asignamos las nuevas posiciones aleatorias a cada carta
        for (int i = 0; i < allCards.Count; i++)
        {
            allCards[i].transform.position = originalPositions[i];
        }
    }

    private void LanzarEstrellas(Transform cartaTransform)
    {
        if (prefabEstrella == null || cartaTransform == null) return;

        // Aparece ligeramente arriba de la carta
        Vector3 spawnPos = cartaTransform.position + Vector3.up * 0.1f;

        // Direcciones relativas a la vista del mundo / cámara (Izquierda, Centro, Derecha)
        Vector3 dirIzquierda = (Vector3.up + Vector3.left * 0.6f).normalized;
        Vector3 dirCentro    = Vector3.up;
        Vector3 dirDerecha   = (Vector3.up + Vector3.right * 0.6f).normalized;

        Vector3[] direcciones = new Vector3[] { dirIzquierda, dirCentro, dirDerecha };

        // Rotación inicial mirando a la cámara
        Quaternion rotInicial = Quaternion.identity;
        if (Camera.main != null)
        {
            rotInicial = Quaternion.LookRotation(spawnPos - Camera.main.transform.position);
        }

        for (int i = 0; i < direcciones.Length; i++)
        {
            GameObject estrella = Instantiate(prefabEstrella, spawnPos, rotInicial);

            StarFeedback script = estrella.GetComponent<StarFeedback>();
            if (script != null)
            {
                script.IniciarEfecto(direcciones[i]);
            }
        }
    }
}