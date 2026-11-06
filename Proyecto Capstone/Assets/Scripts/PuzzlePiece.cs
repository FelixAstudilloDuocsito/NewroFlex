using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PuzzlePiece : MonoBehaviour
{
    [Header("Identificación")]
    public int pieceID; // 0 al 5

    [HideInInspector] public int currentSlotIndex;

    [Header("Velocidad de Movimiento Suave")]
    public float returnSpeed = 8f;

    [Header("Efecto al Agarrar (Z-Fighting)")]
    [Tooltip("Distancia extra que se desplaza la pieza hacia el usuario (en el eje X local) al tomarla.")]
    public float desplazamientoAdelante = 0.02f; // Puedes subirlo o bajarlo desde el Inspector

    private SwapPuzzleManager puzzleManager;
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Coroutine moveCoroutine;
    private Transform boardParent; // Referencia al contenedor del tablero

    private void Awake()
    {
        puzzleManager = FindAnyObjectByType<SwapPuzzleManager>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        // Guardar la referencia al tablero
        boardParent = puzzleManager != null && puzzleManager.slots.Length > 0
            ? puzzleManager.slots[0].parent
            : null;

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);
        }
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        // Cancelar cualquier impulso de lanzamiento del mando
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Consultar al gestor a qué casilla debe ir o si hay intercambio
        if (puzzleManager != null)
        {
            puzzleManager.OnPieceReleased(this);
        }
    }

    private void LateUpdate()
    {
        if (grabInteractable != null && grabInteractable.isSelected
            && puzzleManager != null && boardParent != null
            && currentSlotIndex >= 0 && currentSlotIndex < puzzleManager.slots.Length
            && puzzleManager.slots[currentSlotIndex] != null)
        {
            // Pasar a espacio local del tablero
            Transform slot = puzzleManager.slots[currentSlotIndex];
            Vector3 local = boardParent.InverseTransformPoint(transform.position);
            Vector3 slotLocal = boardParent.InverseTransformPoint(slot.position);

            // Fijar la profundidad (X local) de la casilla, pero aplicarle un pequeño desplazamiento hacia adelante al estar agarrada
            local.x = slotLocal.x - desplazamientoAdelante;  

            transform.position = boardParent.TransformPoint(local);
        }
    }

    // Desplazamiento limpio y suave hacia una posición objetivo
    public void MoveToSlot(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (gameObject.activeInHierarchy)
        {
            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
            moveCoroutine = StartCoroutine(SmoothMoveRoutine(targetPosition, targetRotation));
        }
    }

    private IEnumerator SmoothMoveRoutine(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        while (Vector3.Distance(transform.position, targetPosition) > 0.005f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * returnSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * returnSpeed);
            yield return null;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;
        moveCoroutine = null;
    }
}