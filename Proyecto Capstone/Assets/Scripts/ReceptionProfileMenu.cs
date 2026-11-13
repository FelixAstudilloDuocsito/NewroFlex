using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeuroFlexVR
{
    /// <summary>Connects authored scene controls to profiles; never creates or lays out UI.</summary>
    public sealed class ReceptionProfileMenu : MonoBehaviour
    {
        [Header("Paneles guardados en la escena")]
        public ThemedSceneMenu homeView;
        public ThemedSceneMenu profilesView;
        public ThemedSceneMenu historyView;
        [Header("Recepción")]
        public TMP_Text activeProfileText;
        public Button historyButton;
        [Header("Perfiles")]
        public Button[] profileRows;
        public Graphic[] profileSelectionBorders;
        public TMP_InputField aliasInput;
        public TMP_Text feedback;
        public TMP_Text pageText;
        public Button previous;
        public Button next;
        public Button createProfileButton;
        public Button[] keyboardButtons;
        public string keyboardCharacters = "ABCDEFGHIJKLMNÑOPQRSTUVWXYZ0123456789";
        public Button spaceButton;
        public Button backspaceButton;
        [Header("Historial")]
        public TMP_Text historyText;
        public TMP_Text historyCount;
        public Button historyPrevious;
        public Button historyNext;

        private List<PatientProfile> profiles = new List<PatientProfile>();
        private int page;
        private int historyPage;
        private bool loading;

        // JsonUtility assigns these fields when reading the stored metrics.
#pragma warning disable 0649
        [Serializable] private class Metrics
        {
            public int pairsFound, totalPairs, incorrectPairAttempts, activeTimeMs, correctPlacements, totalPieces;
        }
        [Serializable] private class PigeonMetrics { public PigeonRequest request; }
        [Serializable] private class PigeonRequest { public PigeonCount[] rounds; }
        [Serializable] private class PigeonCount { public int actualPigeonCount, userPigeonCount; }
#pragma warning restore 0649

        private static string DescribeResult(PatientResult result)
        {
            if (result.scenarioId == "pigeons")
            {
                var payload = JsonUtility.FromJson<PigeonMetrics>(result.metricsJson);
                var rounds = payload?.request?.rounds;
                if (rounds == null || rounds.Length == 0) return "";
                return result.status == "completed"
                    ? "Real: " + rounds[0].actualPigeonCount + " · Respuesta: " + rounds[0].userPigeonCount +
                        " · Diferencia: " + Math.Abs(rounds[0].actualPigeonCount - rounds[0].userPigeonCount)
                    : "Palomas mostradas: " + rounds[0].actualPigeonCount;
            }
            var metrics = JsonUtility.FromJson<Metrics>(result.metricsJson);
            return (result.scenarioId == "memory"
                ? "Parejas: " + metrics.pairsFound + "/" + metrics.totalPairs + " · Errores: " + metrics.incorrectPairAttempts
                : "Piezas: " + metrics.correctPlacements + "/" + metrics.totalPieces) +
                " · " + (metrics.activeTimeMs / 1000f).ToString("F1") + " s";
        }

        private void Start()
        {
            if (!RecoverSceneReferences()) return;
            PatientSession.RetryPending();
            string[] scenes = { "Palomas", "Juego De Memoria", "Puzzle Libreria" };
            for (int i = 0; i < homeView.optionButtons.Length; i++)
            {
                string scene = scenes[i];
                ThemedSceneMenu.Bind(homeView.optionButtons[i], () => EnterScene(scene));
            }
            ThemedSceneMenu.Bind(homeView.primaryButton, ShowProfiles);
            ThemedSceneMenu.Bind(historyButton, ShowHistory);
            ThemedSceneMenu.Bind(profilesView.primaryButton, ShowHome);
            ThemedSceneMenu.Bind(historyView.primaryButton, ShowHome);
            for (int i = 0; i < profileRows.Length; i++)
            {
                int row = i;
                ThemedSceneMenu.Bind(profileRows[i], () => SelectProfile(page * profileRows.Length + row));
            }
            ThemedSceneMenu.Bind(previous, () => { page--; RefreshProfiles(); });
            ThemedSceneMenu.Bind(next, () => { page++; RefreshProfiles(); });
            ThemedSceneMenu.Bind(createProfileButton, CreateProfile);
            for (int i = 0; i < keyboardButtons.Length; i++)
            {
                string key = keyboardCharacters[i].ToString();
                ThemedSceneMenu.Bind(keyboardButtons[i], () => AppendAlias(key));
            }
            ThemedSceneMenu.Bind(spaceButton, () => AppendAlias(" "));
            ThemedSceneMenu.Bind(backspaceButton, () =>
            {
                if (aliasInput.text.Length > 0) aliasInput.text = aliasInput.text.Substring(0, aliasInput.text.Length - 1);
            });
            ThemedSceneMenu.Bind(historyPrevious, () => { historyPage = Math.Max(0, historyPage - 1); RefreshHistory(); });
            ThemedSceneMenu.Bind(historyNext, () => { historyPage++; RefreshHistory(); });
            ShowHome();
        }

        private bool RecoverSceneReferences()
        {
            if (homeView == null) homeView = SceneMenuReferences.Find<ThemedSceneMenu>(this, "RecepcionMenuCanvas");
            if (profilesView == null) profilesView = SceneMenuReferences.Find<ThemedSceneMenu>(this, "PerfilesCanvas");
            if (historyView == null) historyView = SceneMenuReferences.Find<ThemedSceneMenu>(this, "HistorialCanvas");
            if (homeView == null || profilesView == null || historyView == null)
                return MissingBindings();
            homeView.RecoverReferences();
            profilesView.RecoverReferences();
            historyView.RecoverReferences();
            if (activeProfileText == null) activeProfileText = SceneMenuReferences.Find<TMP_Text>(this, "PerfilSeleccionado", homeView);
            if (historyButton == null) historyButton = SceneMenuReferences.Find<Button>(this, "VerHistorial", homeView);
            if (aliasInput == null) aliasInput = SceneMenuReferences.Find<TMP_InputField>(this, "NombreOAlias", profilesView);
            if (feedback == null) feedback = SceneMenuReferences.Find<TMP_Text>(this, "EstadoPerfiles", profilesView);
            if (pageText == null) pageText = SceneMenuReferences.Find<TMP_Text>(this, "Pagina", profilesView);
            if (previous == null) previous = SceneMenuReferences.Find<Button>(this, "Anterior", profilesView);
            if (next == null) next = SceneMenuReferences.Find<Button>(this, "Siguiente", profilesView);
            if (createProfileButton == null) createProfileButton = SceneMenuReferences.Find<Button>(this, "CrearPerfil", profilesView);
            if (spaceButton == null) spaceButton = SceneMenuReferences.Find<Button>(this, "Espacio", profilesView);
            if (backspaceButton == null) backspaceButton = SceneMenuReferences.Find<Button>(this, "Borrar", profilesView);
            if (historyText == null) historyText = SceneMenuReferences.Find<TMP_Text>(this, "Resultados", historyView);
            if (historyCount == null) historyCount = SceneMenuReferences.Find<TMP_Text>(this, "CantidadDeRondas", historyView);
            if (historyPrevious == null) historyPrevious = SceneMenuReferences.Find<Button>(this, "MasRecientes", historyView);
            if (historyNext == null) historyNext = SceneMenuReferences.Find<Button>(this, "MasAntiguos", historyView);
            if (profileRows == null || profileRows.Length != 5) profileRows = new Button[5];
            if (profileSelectionBorders == null || profileSelectionBorders.Length != 5) profileSelectionBorders = new Graphic[5];
            for (int i = 0; i < profileRows.Length; i++)
            {
                if (profileRows[i] == null) profileRows[i] = SceneMenuReferences.Find<Button>(this, "Perfil" + (i + 1), profilesView);
                if (profileSelectionBorders[i] == null && profileRows[i] != null)
                    profileSelectionBorders[i] = profileRows[i].GetComponentInChildren<PigeonDifficultyHighlight>(true);
            }
            if (keyboardButtons == null || keyboardButtons.Length != keyboardCharacters.Length)
                keyboardButtons = new Button[keyboardCharacters.Length];
            for (int i = 0; i < keyboardButtons.Length; i++)
                if (keyboardButtons[i] == null)
                    keyboardButtons[i] = SceneMenuReferences.Find<Button>(this, "Tecla" + keyboardCharacters[i], profilesView);
            if (activeProfileText == null || historyButton == null || aliasInput == null || feedback == null || pageText == null ||
                previous == null || next == null || createProfileButton == null || spaceButton == null || backspaceButton == null ||
                historyText == null || historyCount == null || historyPrevious == null || historyNext == null ||
                homeView.primaryButton == null || profilesView.primaryButton == null || historyView.primaryButton == null ||
                !SceneMenuReferences.Complete(homeView.optionButtons, 3) || !SceneMenuReferences.Complete(profileRows, 5) ||
                !SceneMenuReferences.Complete(keyboardButtons, keyboardCharacters.Length)) return MissingBindings();
            return true;
        }

        private bool MissingBindings()
        {
            Debug.LogError("[NeuroFlex] Faltan referencias del menú de Recepción. Reabre la escena guardada o revisa ReceptionProfileMenu en el Inspector.", this);
            return false;
        }

        private void AppendAlias(string text)
        {
            if (aliasInput.text.Length < aliasInput.characterLimit) aliasInput.text += text;
        }

        private void ShowOnly(ThemedSceneMenu selected)
        {
            homeView.gameObject.SetActive(selected == homeView);
            profilesView.gameObject.SetActive(selected == profilesView);
            historyView.gameObject.SetActive(selected == historyView);
        }

        public void ShowHome()
        {
            ShowOnly(homeView);
            activeProfileText.text = PatientSession.ActiveProfile == null
                ? "Sesión anónima" : "Perfil local: " + PatientSession.ActiveProfile.alias;
            string[] scenarios = { "pigeons", "memory", "puzzle" };
            for (int i = 0; i < homeView.optionButtons.Length; i++)
                homeView.optionButtons[i].interactable = i < scenarios.Length &&
                    NeuroFlexSessionProgress.IsSceneAvailable(scenarios[i]);
            historyButton.interactable = PatientSession.ActiveProfile != null;
        }

        public void ShowProfiles()
        {
            ShowOnly(profilesView);
            feedback.text = "";
            try { using (var db = PatientSession.OpenDatabase()) profiles = db.ListProfiles(); RefreshProfiles(); }
            catch (Exception ex) { feedback.text = "No se pudo abrir la base de datos."; Debug.LogError(ex); }
        }

        private void RefreshProfiles()
        {
            int pages = Math.Max(1, (profiles.Count + profileRows.Length - 1) / profileRows.Length);
            page = Mathf.Clamp(page, 0, pages - 1);
            pageText.text = (page + 1) + " / " + pages;
            previous.interactable = page > 0;
            next.interactable = page + 1 < pages;
            for (int i = 0; i < profileRows.Length; i++)
            {
                int index = page * profileRows.Length + i;
                profileRows[i].gameObject.SetActive(index < profiles.Count);
                if (index >= profiles.Count) continue;
                var profile = profiles[index];
                profileRows[i].GetComponentInChildren<TMP_Text>().text = profile.alias;
                if (i < profileSelectionBorders.Length && profileSelectionBorders[i] != null)
                    profileSelectionBorders[i].enabled = PatientSession.ActiveProfile?.id == profile.id;
            }
            if (profiles.Count == 0) feedback.text = "Aún no hay perfiles. Crea el primero.";
        }

        private void CreateProfile()
        {
            try
            {
                using (var db = PatientSession.OpenDatabase()) PatientSession.Select(db.CreateProfile(aliasInput.text));
                aliasInput.text = "";
                ShowHome();
            }
            catch (Exception ex) { feedback.text = ex is ArgumentException ? ex.Message : "No se pudo crear el perfil. Intenta de nuevo."; Debug.LogWarning(ex.Message); }
        }

        private void SelectProfile(int index)
        {
            if (index < 0 || index >= profiles.Count) return;
            PatientSession.Select(profiles[index]);
            ShowHome();
        }

        public void ShowHistory()
        {
            if (PatientSession.ActiveProfile == null) return;
            ShowOnly(historyView);
            historyView.description.text = PatientSession.ActiveProfile.alias;
            historyCount.text = "";
            historyPage = 0;
            RefreshHistory();
        }

        private void RefreshHistory()
        {
            try
            {
                using (var db = PatientSession.OpenDatabase())
                {
                    var results = db.ListResults(PatientSession.ActiveProfile.id, 1000);
                    int lastPage = Math.Max(0, (results.Count - 1) / 5);
                    historyPage = Mathf.Clamp(historyPage, 0, lastPage);
                    historyPrevious.interactable = historyPage > 0;
                    historyNext.interactable = historyPage < lastPage;
                    var lines = new List<string>();
                    for (int i = historyPage * 5; i < Math.Min(results.Count, historyPage * 5 + 5); i++)
                    {
                        var r = results[i];
                        string scene = r.scenarioId == "pigeons" ? "Palomas" : r.scenarioId == "memory" ? "Memoria" : "Biblioteca";
                        string status = r.status == "completed" ? "Completada" : r.status == "abandoned" ? "Abandonada" : "Interrumpida";
                        lines.Add(DateTime.Parse(r.endedAt, null, System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime().ToString("dd/MM/yyyy HH:mm") +
                            "  ·  " + scene + "  ·  Nivel " + r.difficulty + "\n" + status + " · " + DescribeResult(r));
                    }
                    historyText.text = lines.Count == 0 ? "Este perfil todavía no tiene resultados." : string.Join("\n\n", lines);
                    historyCount.text = "Rondas registradas: " + db.CountResults(PatientSession.ActiveProfile.id);
                }
            }
            catch (Exception ex) { historyText.text = "No se pudo cargar el historial."; Debug.LogError(ex); }
        }

        private void EnterScene(string scene)
        {
            if (loading) return;
            string scenario = scene == "Palomas" ? "pigeons" : scene == "Juego De Memoria" ? "memory" : "puzzle";
            if (!NeuroFlexSessionProgress.IsSceneAvailable(scenario)) return;
            if (!Application.CanStreamedLevelBeLoaded(scene)) { activeProfileText.text = "La escena no está incluida en la compilación."; return; }
            loading = true;
            SceneManager.LoadSceneAsync(scene);
        }
    }
}
