using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class SwapPuzzleManager : MonoBehaviour
{
    [Header("Control de Flujo")]
    [Tooltip("Marca esto si esta dificultad debe esperar a que se cierre el video tutorial.")]
    public bool esperarTutorial = false;

    [Header("Casillas (Slots)")]
    public Transform[] slots;

    [Header("Piezas")]
    public List<PuzzlePiece> pieces = new List<PuzzlePiece>();

    [Header("Distancia de Detección")]
    [Tooltip("Distancia máxima para activar el swap. Si la suelta más lejos, vuelve a su casilla original")]
    public float snapThreshold = 0.4f;

    [Header("Eventos")]
    public UnityEvent onPuzzleSolved;

    [Header("Dificultad y Estado")]
    [Tooltip("Nivel actual de dificultad (1, 2 o 3)")]
    public int nivelActual = 1;
    public int nivelMaximo = 3;

    [Header("UI de Victoria")]
    [Tooltip("Panel que contiene el mensaje de felicitaciones y el botón de salir")]
    public GameObject panelVictoria;

    private bool puzzleCompletado = false;

    private bool isSwapping = false;

    private void OnEnable()
    {
        // Si NO espera tutorial (ej. dificultades intermedia/difícil que se activan solas), arranca directo
        if (!esperarTutorial)
        {
            IniciarMinijuego();
        }
    }

    // Este método lo llama el OnEnable() o el botón del Panel Tutorial
    public void IniciarMinijuego()
    {
        StopAllCoroutines();
        isSwapping = false;

        // Ubicar las piezas inicialmente en cada slot
        for (int i = 0; i < pieces.Count; i++)
        {
            pieces[i].currentSlotIndex = i;
            pieces[i].transform.position = slots[i].position;
            pieces[i].transform.rotation = slots[i].rotation;
        }

        ShufflePieces();
    }

    public void OnPieceReleased(PuzzlePiece releasedPiece)
    {
        if (isSwapping) return;

        // Buscar el slot más cercano al punto donde se soltó
        int nearestSlotIndex = -1;
        float shortestDistance = float.MaxValue;

        for (int i = 0; i < slots.Length; i++)
        {
            float dist = Vector3.Distance(releasedPiece.transform.position, slots[i].position);
            if (dist < shortestDistance)
            {
                shortestDistance = dist;
                nearestSlotIndex = i;
            }
        }

        // Si la soltó cerca de un slot nuevo, hacemos el intercambio
        if (shortestDistance <= snapThreshold && nearestSlotIndex != releasedPiece.currentSlotIndex)
        {
            PuzzlePiece otherPiece = GetPieceInSlot(nearestSlotIndex);

            if (otherPiece != null)
            {
                StartCoroutine(SwapRoutine(releasedPiece, otherPiece, nearestSlotIndex));
                return;
            }
        }

        // Si la soltó lejos o cerca de su propio slot, regresa suavemente a su casilla actual
        releasedPiece.MoveToSlot(slots[releasedPiece.currentSlotIndex].position, slots[releasedPiece.currentSlotIndex].rotation);
    }

    private PuzzlePiece GetPieceInSlot(int slotIndex)
    {
        foreach (var piece in pieces)
        {
            if (piece.currentSlotIndex == slotIndex)
                return piece;
        }
        return null;
    }

    private IEnumerator SwapRoutine(PuzzlePiece pieceA, PuzzlePiece pieceB, int targetSlotIndexForA)
    {
        isSwapping = true;

        int slotA = pieceA.currentSlotIndex;
        int slotB = targetSlotIndexForA;

        // Actualizar los índices lógicos
        pieceA.currentSlotIndex = slotB;
        pieceB.currentSlotIndex = slotA;

        // Mover ambas suavemente
        pieceA.MoveToSlot(slots[slotB].position, slots[slotB].rotation);
        pieceB.MoveToSlot(slots[slotA].position, slots[slotA].rotation);

        yield return new WaitForSeconds(0.4f);

        isSwapping = false;
        CheckWinCondition();
    }

    public void ShufflePieces()
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            int randomIndex = Random.Range(i, pieces.Count);

            int tempSlot = pieces[i].currentSlotIndex;
            pieces[i].currentSlotIndex = pieces[randomIndex].currentSlotIndex;
            pieces[randomIndex].currentSlotIndex = tempSlot;

            pieces[i].transform.position = slots[pieces[i].currentSlotIndex].position;
            pieces[randomIndex].transform.position = slots[pieces[randomIndex].currentSlotIndex].position;
        }
    }

    private void CheckWinCondition()
    {
        foreach (var piece in pieces)
        {
            if (piece.pieceID != piece.currentSlotIndex)
                return;
        }

        Debug.Log("¡Puzzle Completado!");
        onPuzzleSolved?.Invoke();
    }

    private void Start()
    {
        if (panelVictoria != null)
            panelVictoria.SetActive(false);
    }

    // Este método lo llamas cuando el jugador ordena la última pieza con éxito
    public void VerificarVictoria()
    {
        if (EsPuzzleCorrecto())
        {
            puzzleCompletado = true;
            Debug.Log($"¡Nivel {nivelActual} completado!");

            if (nivelActual >= nivelMaximo)
            {
                MostrarPantallaVictoria();
            }
            else
            {
                // Aquí va tu lógica para avanzar al siguiente nivel (ej. Nivel 2 -> Nivel 3)
                AvanzarSiguienteNivel();
            }
        }
    }

    private bool EsPuzzleCorrecto()
    {
        // Tu lógica existente que comprueba si todas las piezas están en su lugar
        return true; 
    }

    private void AvanzarSiguienteNivel()
    {
        nivelActual++;
        // Cargar/Reiniciar las piezas para la nueva dificultad...
    }

    private void MostrarPantallaVictoria()
    {
        if (panelVictoria != null)
        {
            panelVictoria.SetActive(true);
        }
        else
        {
            Debug.LogWarning("No se asignó el Panel de Victoria en el Inspector.");
        }
    }
}