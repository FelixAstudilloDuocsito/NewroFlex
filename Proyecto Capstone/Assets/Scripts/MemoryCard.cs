using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class MemoryCard : MonoBehaviour
{
    [Header("Identificador de la Carta")]
    public int cardID;

    [Header("Caras Visuales")]
    public GameObject caraFrontal;
    public GameObject caraDorsal;

    [Header("Ajustes de Giro")]
    public float velocidadGiro = 0.3f;
    [Tooltip("Eje sobre el cual da la vuelta (ej: 180 en X para voltearse hacia arriba/abajo)")]
    public Vector3 ejeGiro = new Vector3(180f, 0, 0);

    [HideInInspector] public bool isFlipped = false;
    [HideInInspector] public bool isMatched = false;

    private bool isAnimating = false;
    private MemoryGameManager gameManager;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
    
    // Guardamos la rotación original del editor (con tus -25 en Y incluidos)
    private Quaternion rotacionOriginal;

    private void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        rotacionOriginal = transform.localRotation;
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnCardSelected);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveAllListeners();
    }

    public void InitCard(MemoryGameManager manager, int id)
    {
        gameManager = manager;
        cardID = id;
        isFlipped = false;
        isMatched = false;
        
        // Mantiene la rotación que le diste en la escena (-25 en Y)
        transform.localRotation = rotacionOriginal;
    }

    private void OnCardSelected(SelectEnterEventArgs args)
    {
        if (isAnimating || isFlipped || isMatched) return;

        if (gameManager != null && gameManager.CanFlipCard(this))
        {
            FlipCard(true);
            gameManager.OnCardFlipped(this);
        }
    }

    public void FlipCard(bool showFace)
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(RutinaGiro(showFace));
    }

    private IEnumerator RutinaGiro(bool showFace)
    {
        isAnimating = true;
        isFlipped = showFace;

        Quaternion rotInicial = transform.localRotation;
        
        // Aplica el giro de 180° sobre su propia orientación local
        Quaternion rotFinal = showFace 
            ? rotacionOriginal * Quaternion.Euler(ejeGiro) 
            : rotacionOriginal;

        float tiempo = 0;
        while (tiempo < velocidadGiro)
        {
            tiempo += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(rotInicial, rotFinal, tiempo / velocidadGiro);
            yield return null;
        }

        transform.localRotation = rotFinal;
        isAnimating = false;
    }
}