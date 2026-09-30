using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ReturnToSlot : MonoBehaviour
{
    [Header("Velocidad de Retorno")]
    public float returnSpeed = 8f;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Coroutine returnCoroutine;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        // Guardar la posicion inicial al empezar
        SaveCurrentPosition();

        if (grabInteractable != null)
        {
            // Evento cuando se agarra el objeto
            grabInteractable.selectEntered.AddListener(OnGrab);
            // Evento cuando se suelta el objeto
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

    // Guarda el punto exacto donde la pieza debe regresar
    public void SaveCurrentPosition()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        // Si estaba regresando y lo vuelven a agarrar, cancela el regreso
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        // Iniciar el regreso suave
        if (gameObject.activeInHierarchy)
        {
            returnCoroutine = StartCoroutine(SmoothReturnRoutine());
        }
    }

    private IEnumerator SmoothReturnRoutine()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; // Desactivar fisicas para que flote de vuelta sin chocar
        }

        while (Vector3.Distance(transform.position, originalPosition) > 0.005f)
        {
            transform.position = Vector3.Lerp(transform.position, originalPosition, Time.deltaTime * returnSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, originalRotation, Time.deltaTime * returnSpeed);
            yield return null;
        }

        transform.position = originalPosition;
        transform.rotation = originalRotation;

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        returnCoroutine = null;
    }
}