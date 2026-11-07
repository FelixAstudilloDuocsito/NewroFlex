using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
        [Header("Perfil y navegación: objetos de la escena")]
        [SerializeField] private TMP_Text profileLabel = null;
        [SerializeField] private Button[] receptionButtons = System.Array.Empty<Button>();
        [SerializeField] private TMP_Text saveStatus = null;
        [SerializeField] private Button retrySave = null;
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
            ThemedSceneMenu.ReplaceAction(startButton, () => { if (PatientSession.IsDifficultyUnlocked("pigeons", controller.difficultyLevel)) controller.StartRound(); });
            if (continueButton != null)
                ThemedSceneMenu.ReplaceAction(continueButton, NextRound);
            if (continueButton != null) continueButton.GetComponentInChildren<TMP_Text>(true).text = "Siguiente";
            AddProfileNavigation();
            initialized = true;
            ShowPhase(true);
            return true;
        }

        private void AddProfileNavigation()
        {
            if (profileLabel != null)
                profileLabel.text = PatientSession.ActiveProfile == null
                    ? "Selecciona un perfil en Recepción" : "Perfil: " + PatientSession.ActiveProfile.alias;
            startButton.interactable = true;
            if (receptionButtons != null)
                foreach (Button button in receptionButtons)
                {
                    ThemedSceneMenu.ReplaceAction(button, () => SceneManager.LoadScene("Recepción"));
                    button.GetComponentInChildren<TMP_Text>(true).text = "Salir";
                }
            ThemedSceneMenu.Bind(retrySave, () => PatientSession.RetryPending());
            PatientSession.SaveStateChanged += RefreshSaveStatus;
            RefreshSaveStatus();
        }

        private void RefreshSaveStatus()
        {
            if (saveStatus != null) saveStatus.text = PatientSession.LastSaveMessage;
            if (retrySave != null) retrySave.gameObject.SetActive(false);
            if (continueButton != null) continueButton.interactable = controller.LastRoundRecord != null &&
                controller.LastRoundRecord.status == "completed" && controller.LastRoundRecord.difficultyLevel < 3 &&
                PatientSession.IsDifficultyUnlocked("pigeons", controller.LastRoundRecord.difficultyLevel + 1);
        }

        private void OnDestroy() { PatientSession.SaveStateChanged -= RefreshSaveStatus; }

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
                    Debug.LogWarning("[NeuroFlex] Falta el borde de selección de " + difficultyButtons[i].name + ". Asígnalo en la escena.", this);
                    continue;
                }
                border.gameObject.SetActive(true);
                selectionBorders[i] = border;
            }
        }

        private void SelectDifficulty(int level)
        {
            if (controller.poolSpawner == null || !PatientSession.IsDifficultyUnlocked("pigeons", level)) return;
            controller.poolSpawner.SelectDifficulty(level);
            RefreshSelection();
        }

        private void NextRound()
        {
            var record = controller.LastRoundRecord;
            if (record == null || record.status != "completed" || record.difficultyLevel >= 3) return;
            int next = record.difficultyLevel + 1;
            if (!PatientSession.IsDifficultyUnlocked("pigeons", next)) return;
            SelectDifficulty(next);
            controller.StartRound();
        }

        private void RefreshSelection()
        {
            PigeonPoolSpawner spawner = controller.poolSpawner;
            int selected = spawner != null
                ? (spawner.SelectedDifficulty > 0 ? spawner.SelectedDifficulty : spawner.initialDifficulty)
                : 0;
            for (int i = 0; i < difficultyButtons.Length; i++)
                difficultyButtons[i].interactable = PatientSession.IsDifficultyUnlocked("pigeons", i + 1);
            if (!PatientSession.IsDifficultyUnlocked("pigeons", selected) && spawner != null)
            {
                spawner.SelectDifficulty(1);
                selected = 1;
            }
            for (int i = 0; i < selectionBorders.Length; i++)
                if (selectionBorders[i] != null) selectionBorders[i].enabled = selected == i + 1;
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
