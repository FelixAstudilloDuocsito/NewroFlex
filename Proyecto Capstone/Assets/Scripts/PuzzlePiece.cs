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

    private SwapPuzzleManager puzzleManager;
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Coroutine moveCoroutine;

    private void Awake()
    {
        puzzleManager = FindAnyObjectByType<SwapPuzzleManager>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

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