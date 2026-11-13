using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeuroFlexVR
{
    /// <summary>Read-only profile/result browser. Browsing never changes the active player.</summary>
    public sealed class SavedDataSceneMenu : MonoBehaviour
    {
        public Button[] profileRows;
        public Button[] roundRows;
        public TMP_Text summary;
        public TMP_Text statusText;
        public TMP_Text profilePageText;
        public TMP_Text roundPageText;
        public TMP_Text detailTitle;
        public TMP_Text detailMeta;
        public TMP_Text detailText;
        public Button previousProfiles, nextProfiles, previousRounds, nextRounds;
        public Button scenarioFilter, difficultyFilter, statusFilter;
        public Button metricsTab, contextTab, refreshButton, retrySaveButton, backButton;

        private List<PatientProfile> profiles = new List<PatientProfile>();
        private List<PatientResult> rounds = new List<PatientResult>();
        private PatientProfile viewedProfile;
        private PatientResult viewedRound;
        private int profilePage, roundPage, scenarioIndex, difficulty, statusIndex;
        private bool showContext;
        private static readonly string[] Scenarios = { "", "pigeons", "memory", "puzzle" };
        private static readonly string[] ScenarioLabels = { "Todas las actividades", "Palomas", "Memoria", "Biblioteca" };
        private static readonly string[] Statuses = { "", "completed", "abandoned", "interrupted" };
        private static readonly string[] StatusLabels = { "Todos los estados", "Completadas", "Abandonadas", "Interrumpidas" };

        private void Start()
        {
            if (!RecoverReferences())
            {
                Debug.LogError("[NeuroFlex] Faltan controles de la escena Datos Guardados.", this);
                return;
            }
            for (int i = 0; i < profileRows.Length; i++)
            {
                int row = i;
                ThemedSceneMenu.Bind(profileRows[i], () => SelectProfile(profilePage * profileRows.Length + row));
            }
            for (int i = 0; i < roundRows.Length; i++)
            {
                int row = i;
                ThemedSceneMenu.Bind(roundRows[i], () => SelectRound(row));
            }
            ThemedSceneMenu.Bind(previousProfiles, () => { profilePage--; RenderProfiles(); });
            ThemedSceneMenu.Bind(nextProfiles, () => { profilePage++; RenderProfiles(); });
            ThemedSceneMenu.Bind(previousRounds, () => { roundPage--; ReloadRounds(); });
            ThemedSceneMenu.Bind(nextRounds, () => { roundPage++; ReloadRounds(); });
            ThemedSceneMenu.Bind(scenarioFilter, () => { scenarioIndex = (scenarioIndex + 1) % Scenarios.Length; ResetRoundPage(); });
            ThemedSceneMenu.Bind(difficultyFilter, () => { difficulty = (difficulty + 1) % 4; ResetRoundPage(); });
            ThemedSceneMenu.Bind(statusFilter, () => { statusIndex = (statusIndex + 1) % Statuses.Length; ResetRoundPage(); });
            ThemedSceneMenu.Bind(metricsTab, () => { showContext = false; RenderDetail(); });
            ThemedSceneMenu.Bind(contextTab, () => { showContext = true; RenderDetail(); });
            ThemedSceneMenu.Bind(refreshButton, Reload);
            ThemedSceneMenu.Bind(retrySaveButton, () => PatientSession.RetryPending());
            ThemedSceneMenu.Bind(backButton, NeuroFlexSceneNavigation.LeaveActivity);
            PatientSession.SaveStateChanged += Reload;
            Reload();
        }

        private void OnDestroy() { PatientSession.SaveStateChanged -= Reload; }

        private bool RecoverReferences()
        {
            if (profileRows == null || profileRows.Length != 5) profileRows = new Button[5];
            if (roundRows == null || roundRows.Length != 4) roundRows = new Button[4];
            for (int i = 0; i < profileRows.Length; i++)
                if (profileRows[i] == null) profileRows[i] = Find<Button>("DatosPerfil" + (i + 1));
            for (int i = 0; i < roundRows.Length; i++)
                if (roundRows[i] == null) roundRows[i] = Find<Button>("Ronda" + (i + 1));
            if (summary == null) summary = Find<TMP_Text>("ResumenDatos");
            if (statusText == null) statusText = Find<TMP_Text>("EstadoDatos");
            if (profilePageText == null) profilePageText = Find<TMP_Text>("PaginaPerfiles");
            if (roundPageText == null) roundPageText = Find<TMP_Text>("PaginaRondas");
            if (detailTitle == null) detailTitle = Find<TMP_Text>("TituloDetalle");
            if (detailMeta == null) detailMeta = Find<TMP_Text>("FechasDetalle");
            if (detailText == null) detailText = Find<TMP_Text>("MetricasDetalle");
            if (previousProfiles == null) previousProfiles = Find<Button>("PerfilesAnteriores");
            if (nextProfiles == null) nextProfiles = Find<Button>("PerfilesSiguientes");
            if (previousRounds == null) previousRounds = Find<Button>("RondasAnteriores");
            if (nextRounds == null) nextRounds = Find<Button>("RondasSiguientes");
            if (scenarioFilter == null) scenarioFilter = Find<Button>("FiltrarActividad");
            if (difficultyFilter == null) difficultyFilter = Find<Button>("FiltrarDificultad");
            if (statusFilter == null) statusFilter = Find<Button>("FiltrarEstado");
            if (metricsTab == null) metricsTab = Find<Button>("VerMetricas");
            if (contextTab == null) contextTab = Find<Button>("VerContexto");
            if (refreshButton == null) refreshButton = Find<Button>("ActualizarDatos");
            if (retrySaveButton == null) retrySaveButton = Find<Button>("ReintentarDatos");
            if (backButton == null) backButton = Find<Button>("VolverRecepcion");
            return SceneMenuReferences.Complete(profileRows, 5) && SceneMenuReferences.Complete(roundRows, 4) &&
                summary != null && statusText != null && profilePageText != null && roundPageText != null &&
                detailTitle != null && detailMeta != null && detailText != null && previousProfiles != null &&
                nextProfiles != null && previousRounds != null && nextRounds != null && scenarioFilter != null &&
                difficultyFilter != null && statusFilter != null && metricsTab != null && contextTab != null &&
                refreshButton != null && retrySaveButton != null && backButton != null;
        }

        private T Find<T>(string name) where T : Component => SceneMenuReferences.Find<T>(this, name, this);
        private static void Label(Button button, string text) { button.GetComponentInChildren<TMP_Text>(true).text = text; }
        private static void Highlight(Button button, bool selected)
        {
            foreach (var border in button.GetComponentsInChildren<PigeonDifficultyHighlight>(true)) border.enabled = selected;
        }

        private void Reload()
        {
            retrySaveButton.gameObject.SetActive(PatientSession.HasPendingResults);
            try
            {
                using (var database = PatientSession.OpenDatabase()) profiles = database.ListProfiles();
                string id = viewedProfile?.id ?? PatientSession.ActiveProfile?.id;
                viewedProfile = profiles.Find(profile => profile.id == id) ?? (profiles.Count > 0 ? profiles[0] : null);
                if (viewedProfile != null) profilePage = profiles.FindIndex(profile => profile.id == viewedProfile.id) / profileRows.Length;
                RenderProfiles();
                ReloadRounds();
            }
            catch (Exception exception)
            {
                profiles.Clear(); rounds.Clear(); viewedProfile = null; viewedRound = null;
                RenderProfiles(); RenderRounds(0, 0);
                statusText.text = "No se pudo leer la base local. Pulsa Actualizar para reintentar.";
                Debug.LogError("[NeuroFlex] " + exception.Message);
            }
        }

        private void RenderProfiles()
        {
            int pages = Math.Max(1, (profiles.Count + profileRows.Length - 1) / profileRows.Length);
            profilePage = Mathf.Clamp(profilePage, 0, pages - 1);
            profilePageText.text = profiles.Count == 0 ? "0 / 0" : (profilePage + 1) + " / " + pages;
            previousProfiles.interactable = profilePage > 0;
            nextProfiles.interactable = profilePage + 1 < pages;
            for (int i = 0; i < profileRows.Length; i++)
            {
                int index = profilePage * profileRows.Length + i;
                profileRows[i].gameObject.SetActive(index < profiles.Count);
                if (index >= profiles.Count) continue;
                Label(profileRows[i], profiles[index].alias);
                Highlight(profileRows[i], viewedProfile?.id == profiles[index].id);
            }
        }

        private void SelectProfile(int index)
        {
            if (index < 0 || index >= profiles.Count) return;
            viewedProfile = profiles[index];
            RenderProfiles();
            ResetRoundPage();
        }

        private void ResetRoundPage() { roundPage = 0; viewedRound = null; ReloadRounds(); }

        private void ReloadRounds()
        {
            Label(scenarioFilter, ScenarioLabels[scenarioIndex]);
            Label(difficultyFilter, difficulty == 0 ? "Todos los niveles" : "Nivel " + difficulty);
            Label(statusFilter, StatusLabels[statusIndex]);
            try
            {
                using (var database = PatientSession.OpenDatabase())
                {
                    int count = viewedProfile == null ? 0 : database.CountResults(viewedProfile.id, Scenarios[scenarioIndex], difficulty, Statuses[statusIndex]);
                    int total = viewedProfile == null ? 0 : database.CountResults(viewedProfile.id);
                    int lastPage = Math.Max(0, (count - 1) / roundRows.Length);
                    roundPage = Mathf.Clamp(roundPage, 0, lastPage);
                    rounds = viewedProfile == null ? new List<PatientResult>() : database.ListResultsPage(viewedProfile.id,
                        roundPage * roundRows.Length, roundRows.Length, Scenarios[scenarioIndex], difficulty, Statuses[statusIndex]);
                    RenderRounds(count, total);
                }
                statusText.text = PatientSession.HasPendingResults
                    ? "Hay resultados pendientes de guardar. Puedes reintentar el guardado."
                    : "Datos locales · No registrado indica un dato ausente; cero es un valor observado.";
            }
            catch (Exception exception)
            {
                rounds.Clear(); viewedRound = null; RenderRounds(0, 0);
                statusText.text = "No se pudieron cargar las rondas. Pulsa Actualizar para reintentar.";
                Debug.LogError("[NeuroFlex] " + exception.Message);
            }
        }

        private void RenderRounds(int count, int total)
        {
            summary.text = viewedProfile == null ? "Aún no hay perfiles guardados. Vuelve a Recepción para crear uno."
                : "Perfil consultado: " + viewedProfile.alias + " · " + count + " de " + total + " rondas";
            roundPageText.text = count == 0 ? "0 / 0" : (roundPage + 1) + " / " + ((count + roundRows.Length - 1) / roundRows.Length);
            previousRounds.interactable = roundPage > 0;
            nextRounds.interactable = (roundPage + 1) * roundRows.Length < count;
            string selectedId = viewedRound?.id;
            viewedRound = rounds.Find(round => round.id == selectedId) ?? (rounds.Count > 0 ? rounds[0] : null);
            for (int i = 0; i < roundRows.Length; i++)
            {
                roundRows[i].gameObject.SetActive(i < rounds.Count);
                if (i >= rounds.Count) continue;
                var round = rounds[i];
                Label(roundRows[i], Activity(round.scenarioId) + " · Nivel " + round.difficulty + "\n" + State(round.status) + " · " + Date(round.endedAt));
                Highlight(roundRows[i], round.id == viewedRound.id);
            }
            RenderDetail();
        }

        private void SelectRound(int index)
        {
            if (index < 0 || index >= rounds.Count) return;
            viewedRound = rounds[index];
            for (int i = 0; i < rounds.Count; i++) Highlight(roundRows[i], rounds[i].id == viewedRound.id);
            RenderDetail();
        }

        private void RenderDetail()
        {
            metricsTab.interactable = contextTab.interactable = viewedRound != null;
            if (viewedRound == null)
            {
                detailTitle.text = "Sin rondas para mostrar"; detailMeta.text = "";
                detailText.text = viewedProfile == null ? "Selecciona un perfil guardado para consultar sus datos."
                    : "Este perfil no tiene rondas que coincidan con los filtros.";
                return;
            }
            detailTitle.text = Activity(viewedRound.scenarioId) + " · Nivel " + viewedRound.difficulty + " · " + State(viewedRound.status);
            detailMeta.text = "Inicio: " + Date(viewedRound.startedAt) + "\nCierre: " + Date(viewedRound.endedAt);
            try
            {
                var metrics = SavedRoundMetrics.Read(viewedRound);
                detailText.text = showContext ? metrics.ContextText() : metrics.MetricsText();
            }
            catch (Exception exception)
            {
                detailText.text = "Las métricas de esta ronda no están disponibles o contienen datos inválidos.\nEl registro se conserva y puedes consultar las demás rondas.";
                Debug.LogWarning("[NeuroFlex] Métricas inválidas: " + exception.Message);
            }
        }

        public static string Activity(string id) => id == "pigeons" ? "Palomas" : id == "memory" ? "Memoria" : id == "puzzle" ? "Biblioteca" : id;
        public static string State(string status) => status == "completed" ? "Completada" : status == "abandoned" ? "Abandonada" : status == "interrupted" ? "Interrumpida" : status;
        private static string Date(string value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) : "Fecha no registrada";
    }
}
