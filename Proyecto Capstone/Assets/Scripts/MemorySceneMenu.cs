using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeuroFlexVR
{
    [DefaultExecutionOrder(-1000)]
    public sealed class MemorySceneMenu : MonoBehaviour
    {
        [Header("Paneles guardados en la escena")]
        public ThemedSceneMenu difficultyView;
        public ThemedSceneMenu resultsView;
        public TMP_Text activeProfileText;
        public TMP_Text resultSummary;
        public TMP_Text saveStatus;
        public Button[] receptionButtons;
        public Button tutorialButton;
        public Button retryButton;
        [Header("Actividad")]
        public GameObject[] levels;
        public MemoryGameManager[] managers;
        public TutorialPanelController tutorial;
        public GameObject legacyVictoryCanvas;
        private int selected = 1;

        private void Awake()
        {
            if (!RecoverSceneReferences()) { enabled = false; return; }
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i].SetActive(false);
                managers[i].esperarTutorial = true;
                managers[i].difficultyLevel = i + 1;
                int index = i;
                managers[i].onGameWon = new UnityEvent();
                managers[i].onGameWon.AddListener(() => ShowResults(index));
            }
            if (legacyVictoryCanvas != null) legacyVictoryCanvas.SetActive(false);
            if (tutorial != null)
            {
                tutorial.gameObject.SetActive(false);
                tutorial.onTutorialCerrado = new UnityEvent();
                tutorial.onTutorialCerrado.AddListener(ShowMenu);
            }
        }

        private bool RecoverSceneReferences()
        {
            if (difficultyView == null) difficultyView = SceneMenuReferences.Find<ThemedSceneMenu>(this, "MemoriaMenuCanvas");
            if (resultsView == null) resultsView = SceneMenuReferences.Find<ThemedSceneMenu>(this, "ResultadosMemoriaCanvas");
            if (difficultyView == null || resultsView == null) return MissingBindings();
            difficultyView.RecoverReferences();
            resultsView.RecoverReferences();
            if (activeProfileText == null) activeProfileText = SceneMenuReferences.Find<TMP_Text>(this, "PerfilSeleccionado", difficultyView);
            if (resultSummary == null) resultSummary = SceneMenuReferences.Find<TMP_Text>(this, "ResumenDeResultados", resultsView);
            if (saveStatus == null) saveStatus = SceneMenuReferences.Find<TMP_Text>(this, "EstadoGuardado", resultsView);
            if (tutorialButton == null) tutorialButton = SceneMenuReferences.Find<Button>(this, "VerTutorial", difficultyView);
            if (retryButton == null) retryButton = SceneMenuReferences.Find<Button>(this, "ReintentarGuardado", resultsView);
            if (receptionButtons == null || receptionButtons.Length != 2) receptionButtons = new Button[2];
            if (receptionButtons[0] == null) receptionButtons[0] = SceneMenuReferences.Find<Button>(this, "VolverARecepcion", difficultyView);
            if (receptionButtons[1] == null) receptionButtons[1] = SceneMenuReferences.Find<Button>(this, "VolverARecepcion", resultsView);
            if (tutorial == null) tutorial = SceneMenuReferences.Find<TutorialPanelController>(this, "Panel_Tutorial");
            if (levels == null || levels.Length != 3) levels = new GameObject[3];
            if (managers == null || managers.Length != 3) managers = new MemoryGameManager[3];
            string[] levelNames = { "Juego_Memoria_Facil", "Juego_Memoria_Intermedio", "Juego_Memoria_Dificil" };
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null)
                {
                    Transform level = SceneMenuReferences.Find<Transform>(this, levelNames[i]);
                    if (level != null) levels[i] = level.gameObject;
                }
                if (managers[i] == null && levels[i] != null)
                    managers[i] = levels[i].GetComponentInChildren<MemoryGameManager>(true);
            }
            if (activeProfileText == null || resultSummary == null || saveStatus == null || tutorialButton == null || retryButton == null ||
                difficultyView.primaryButton == null || resultsView.primaryButton == null ||
                !SceneMenuReferences.Complete(difficultyView.optionButtons, 3) ||
                !SceneMenuReferences.Complete(levels, 3) || !SceneMenuReferences.Complete(managers, 3) ||
                !SceneMenuReferences.Complete(receptionButtons, 2)) return MissingBindings();
            return true;
        }

        private bool MissingBindings()
        {
            Debug.LogError("[NeuroFlex] Faltan referencias del menú de Memoria. Reabre la escena guardada o revisa MemorySceneMenu en el Inspector.", this);
            return false;
        }

        private void Start()
        {
            for (int i = 0; i < difficultyView.optionButtons.Length; i++)
            {
                int difficulty = i + 1;
                ThemedSceneMenu.Bind(difficultyView.optionButtons[i], () => { selected = difficulty; RefreshSelection(); });
            }
            ThemedSceneMenu.Bind(difficultyView.primaryButton, StartRound);
            ThemedSceneMenu.Bind(resultsView.primaryButton, ShowMenu);
            foreach (Button button in receptionButtons)
                ThemedSceneMenu.Bind(button, () => SceneManager.LoadScene("Recepción"));
            ThemedSceneMenu.Bind(tutorialButton, ShowTutorial);
            ThemedSceneMenu.Bind(retryButton, () => PatientSession.RetryPending());
            PatientSession.SaveStateChanged += RefreshSaveStatus;
            ShowMenu();
        }

        private void OnDestroy() { PatientSession.SaveStateChanged -= RefreshSaveStatus; }

        public void ShowMenu()
        {
            difficultyView.gameObject.SetActive(true);
            resultsView.gameObject.SetActive(false);
            if (tutorial != null) tutorial.gameObject.SetActive(false);
            foreach (GameObject level in levels) level.SetActive(false);
            activeProfileText.text = PatientSession.ActiveProfile == null
                ? "Selecciona un perfil en Recepción" : "Perfil: " + PatientSession.ActiveProfile.alias;
            difficultyView.primaryButton.interactable = true;
            tutorialButton.interactable = tutorial != null;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < difficultyView.optionButtons.Length; i++)
                foreach (var border in difficultyView.optionButtons[i].GetComponentsInChildren<PigeonDifficultyHighlight>(true))
                    border.enabled = selected == i + 1;
        }

        private void StartRound()
        {
            difficultyView.gameObject.SetActive(false);
            resultsView.gameObject.SetActive(false);
            levels[selected - 1].SetActive(true);
            managers[selected - 1].IniciarJuego();
        }

        private void ShowResults(int index)
        {
            var manager = managers[index];
            levels[index].SetActive(false);
            difficultyView.gameObject.SetActive(false);
            resultsView.gameObject.SetActive(true);
            int attempts = manager.PairsFound + manager.IncorrectAttempts;
            float accuracy = attempts > 0 ? 100f * manager.PairsFound / attempts : 0;
            resultSummary.text = "Parejas encontradas: " + manager.PairsFound + " / " + manager.TotalPairs +
                "\nIntentos incorrectos: " + manager.IncorrectAttempts +
                "\nPrecisión: " + accuracy.ToString("F1") + "%" +
                "\nTiempo activo: " + manager.ActiveSeconds.ToString("F1") + " s";
            RefreshSaveStatus();
        }

        private void RefreshSaveStatus()
        {
            saveStatus.text = PatientSession.LastSaveMessage;
            retryButton.gameObject.SetActive(PatientSession.HasPendingResults);
        }

        private void ShowTutorial()
        {
            if (tutorial == null) return;
            difficultyView.gameObject.SetActive(false);
            resultsView.gameObject.SetActive(false);
            tutorial.gameObject.SetActive(true);
            if (tutorial.videoPlayer != null) tutorial.videoPlayer.Play();
        }
    }
}
