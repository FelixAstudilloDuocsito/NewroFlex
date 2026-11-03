using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeuroFlexVR
{
    /// <summary>Editable scene menus with a persistent difficulty selection.</summary>
    [DisallowMultipleComponent]
    public class PigeonRoundMenu : MonoBehaviour
    {
        [Header("Canvas de dificultad (incluye fondo y blur)")]
        [SerializeField] private GameObject difficultyMenu;

        [Header("Dificultad")]
        [SerializeField] private Button[] difficultyButtons;
        [SerializeField] private Graphic[] selectionBorders;
        [SerializeField] private Button startButton;

        [Header("Resultados")]
        [SerializeField] private Button continueButton;

        private PigeonRoundController controller;
        private bool initialized;
        public bool IsInitialized => initialized;

        public bool Initialize(PigeonRoundController owner)
        {
            if (initialized) return true;
            controller = owner;
            RecoverSceneReferences();
            if (difficultyMenu == null || startButton == null ||
                difficultyButtons == null || difficultyButtons.Length != 3 ||
                System.Array.Exists(difficultyButtons, button => button == null))
            {
                Debug.LogWarning("[NeuroFlex] No se encontró el menú de dificultad completo. " +
                    "Se mantendrá visible el menú de inicio original.", this);
                return false;
            }

            RecoverSelectionBorders();
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                int level = i + 1;
                difficultyButtons[i].onClick.AddListener(() => SelectDifficulty(level));
            }
            startButton.onClick.AddListener(controller.StartRound);
            if (continueButton != null)
                continueButton.onClick.AddListener(controller.ReturnToStart);
            initialized = true;
            ShowPhase(true);
            return true;
        }

        private void RecoverSceneReferences()
        {
            // An open Editor scene can still have the bindings from before a script reload.
            // Include inactive canvases and ignore assets/other loaded scenes.
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas.gameObject.scene != gameObject.scene) continue;
                if (canvas.name == "DificultadCanvas" && difficultyMenu == null)
                    difficultyMenu = canvas.gameObject;
                if (canvas.name == "GuardarDatosCanvas")
                    canvas.gameObject.SetActive(false);
            }
            if (difficultyMenu != null)
            {
                if (difficultyButtons == null || difficultyButtons.Length != 3)
                    difficultyButtons = new Button[3];
                string[] names = { "Fácil", "Medio", "Difícil" };
                foreach (Button button in difficultyMenu.GetComponentsInChildren<Button>(true))
                {
                    for (int i = 0; i < names.Length; i++)
                        if (difficultyButtons[i] == null && button.name == names[i])
                            difficultyButtons[i] = button;
                    if (startButton == null && button.name == "IniciarButton")
                        startButton = button;
                }
                foreach (TMP_Text text in difficultyMenu.GetComponentsInChildren<TMP_Text>(true))
                    if (text.name == "DetalleDificultad") text.gameObject.SetActive(false);
            }
            if (continueButton == null && controller.resultPanel != null)
                foreach (Button button in controller.resultPanel.GetComponentsInChildren<Button>(true))
                    if (button.name == "VolverAlInicioButton" || button.name == "Continuar")
                    {
                        continueButton = button;
                        break;
                    }
            if (continueButton != null)
            {
                TMP_Text label = continueButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null && label.text.Trim() == "Continuar") label.text = "Volver al inicio";
            }
        }

        private void RecoverSelectionBorders()
        {
            if (selectionBorders == null || selectionBorders.Length != 3)
                selectionBorders = new Graphic[3];
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                if (selectionBorders[i] != null) continue;
                PigeonDifficultyHighlight border = difficultyButtons[i].GetComponentInChildren<PigeonDifficultyHighlight>(true);
                if (border == null)
                {
                    GameObject obj = new GameObject("BordeSeleccion", typeof(RectTransform), typeof(PigeonDifficultyHighlight));
                    obj.layer = difficultyButtons[i].gameObject.layer;
                    obj.transform.SetParent(difficultyButtons[i].transform, false);
                    RectTransform rect = (RectTransform)obj.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    border = obj.GetComponent<PigeonDifficultyHighlight>();
                    border.color = new Color(0.4f, 1f, 0.8f, 1f);
                    border.raycastTarget = false;
                }
                border.gameObject.SetActive(true);
                selectionBorders[i] = border;
            }
        }

        private void SelectDifficulty(int level)
        {
            if (controller.poolSpawner == null) return;
            controller.poolSpawner.SelectDifficulty(level);
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            PigeonPoolSpawner spawner = controller.poolSpawner;
            int selected = spawner != null
                ? (spawner.SelectedDifficulty > 0 ? spawner.SelectedDifficulty : spawner.initialDifficulty)
                : 0;
            for (int i = 0; i < selectionBorders.Length; i++)
                selectionBorders[i].enabled = selected == i + 1;
        }

        public void ShowPhase(bool ready)
        {
            if (!initialized) return;
            if (ready)
                for (Transform parent = difficultyMenu.transform.parent; parent != null; parent = parent.parent)
                    parent.gameObject.SetActive(true);
            difficultyMenu.SetActive(ready);
            if (ready) RefreshSelection();
        }
    }
}
