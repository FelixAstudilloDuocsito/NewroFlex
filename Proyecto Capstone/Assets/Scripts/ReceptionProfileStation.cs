using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeuroFlexVR
{
    /// <summary>Connects the rear profile desk to the existing scene entrances.</summary>
    public sealed class ReceptionProfileStation : MonoBehaviour
    {
        public TMP_InputField aliasInput;
        public TMP_Text activeProfileText;
        public TMP_Text frontProfileText;
        public TMP_Text feedback;
        public TMP_Text pageText;
        public Button[] profileRows;
        public Button[] keyboardButtons;
        public Button[] sceneButtons;
        public Button previous;
        public Button next;
        public Button createProfileButton;
        public Button refreshButton;
        public Button spaceButton;
        public Button backspaceButton;
        public Button dataButton;
        public string keyboardCharacters = "ABCDEFGHIJKLMNÑOPQRSTUVWXYZ0123456789";

        private List<PatientProfile> profiles = new List<PatientProfile>();
        private int page;

        private void Start()
        {
            if (!RecoverReferences())
            {
                Debug.LogError("[NeuroFlex] Faltan controles del panel de perfiles de Recepción.", this);
                return;
            }
            for (int i = 0; i < profileRows.Length; i++)
            {
                int row = i;
                ThemedSceneMenu.Bind(profileRows[i], () => SelectProfile(page * profileRows.Length + row));
            }
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
            ThemedSceneMenu.Bind(createProfileButton, CreateProfile);
            ThemedSceneMenu.Bind(refreshButton, ReloadProfiles);
            ThemedSceneMenu.Bind(dataButton, () => SceneManager.LoadScene("Datos Guardados"));
            ThemedSceneMenu.Bind(previous, () => { page--; RefreshRows(); });
            ThemedSceneMenu.Bind(next, () => { page++; RefreshRows(); });
            RefreshActiveProfile();
            PatientSession.RetryPending();
            ReloadProfiles();
        }

        private bool RecoverReferences()
        {
            if (aliasInput == null) aliasInput = SceneMenuReferences.Find<TMP_InputField>(this, "NombreOAlias", this);
            if (activeProfileText == null) activeProfileText = SceneMenuReferences.Find<TMP_Text>(this, "PerfilActivo", this);
            if (frontProfileText == null) frontProfileText = SceneMenuReferences.Find<TMP_Text>(this, "PerfilActivoRecepcion");
            if (feedback == null) feedback = SceneMenuReferences.Find<TMP_Text>(this, "EstadoPerfiles", this);
            if (pageText == null) pageText = SceneMenuReferences.Find<TMP_Text>(this, "Pagina", this);
            if (previous == null) previous = SceneMenuReferences.Find<Button>(this, "Anterior", this);
            if (next == null) next = SceneMenuReferences.Find<Button>(this, "Siguiente", this);
            if (createProfileButton == null) createProfileButton = SceneMenuReferences.Find<Button>(this, "CrearPerfil", this);
            if (refreshButton == null) refreshButton = SceneMenuReferences.Find<Button>(this, "ActualizarPerfiles", this);
            if (spaceButton == null) spaceButton = SceneMenuReferences.Find<Button>(this, "Espacio", this);
            if (backspaceButton == null) backspaceButton = SceneMenuReferences.Find<Button>(this, "Borrar", this);
            if (dataButton == null) dataButton = SceneMenuReferences.Find<Button>(this, "VerDatosGuardados", this);
            if (profileRows == null || profileRows.Length != 5) profileRows = new Button[5];
            for (int i = 0; i < profileRows.Length; i++)
                if (profileRows[i] == null) profileRows[i] = SceneMenuReferences.Find<Button>(this, "Perfil" + (i + 1), this);
            if (keyboardButtons == null || keyboardButtons.Length != keyboardCharacters.Length)
                keyboardButtons = new Button[keyboardCharacters.Length];
            for (int i = 0; i < keyboardButtons.Length; i++)
                if (keyboardButtons[i] == null)
                    keyboardButtons[i] = SceneMenuReferences.Find<Button>(this, "Tecla" + keyboardCharacters[i], this);
            if (sceneButtons == null || sceneButtons.Length != 3) sceneButtons = new Button[3];
            string[] entrances = { "Boton_Parque_Palomas", "Boton_Juego_Memoria", "Boton_Puzzle_Libreria" };
            for (int i = 0; i < sceneButtons.Length; i++)
                if (sceneButtons[i] == null) sceneButtons[i] = SceneMenuReferences.Find<Button>(this, entrances[i]);
            return aliasInput != null && activeProfileText != null && frontProfileText != null && feedback != null &&
                pageText != null && previous != null && next != null && createProfileButton != null &&
                refreshButton != null && spaceButton != null && backspaceButton != null && dataButton != null &&
                SceneMenuReferences.Complete(profileRows, 5) && SceneMenuReferences.Complete(sceneButtons, 3) &&
                SceneMenuReferences.Complete(keyboardButtons, keyboardCharacters.Length);
        }

        private void AppendAlias(string key)
        {
            if (aliasInput.text.Length < aliasInput.characterLimit) aliasInput.text += key;
        }

        private void ReloadProfiles()
        {
            try
            {
                using (var database = PatientSession.OpenDatabase()) profiles = database.ListProfiles();
                feedback.text = profiles.Count == 0 ? "Aún no hay perfiles. Crea el primero." : "Elige tu perfil en la lista o crea uno nuevo.";
                RefreshRows();
            }
            catch (Exception exception)
            {
                feedback.text = "No se pudo abrir la base local. Pulsa Actualizar lista para reintentar.";
                Debug.LogError("[NeuroFlex] " + exception.Message);
            }
        }

        private void RefreshRows()
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
                profileRows[i].GetComponentInChildren<TMP_Text>(true).text = profile.alias;
                foreach (var border in profileRows[i].GetComponentsInChildren<PigeonDifficultyHighlight>(true))
                    border.enabled = PatientSession.ActiveProfile?.id == profile.id;
            }
        }

        private void CreateProfile()
        {
            try
            {
                PatientProfile profile;
                using (var database = PatientSession.OpenDatabase()) profile = database.CreateProfile(aliasInput.text);
                aliasInput.text = "";
                PatientSession.Select(profile);
                ReloadProfiles();
                int index = profiles.FindIndex(item => item.id == profile.id);
                if (index >= 0) page = index / profileRows.Length;
                RefreshRows();
                RefreshActiveProfile();
                feedback.text = "Perfil creado y seleccionado. Gira para elegir una escena.";
            }
            catch (Exception exception)
            {
                feedback.text = exception is ArgumentException ? exception.Message : "No se pudo crear el perfil. Intenta de nuevo.";
                Debug.LogWarning("[NeuroFlex] " + exception.Message);
            }
        }

        private void SelectProfile(int index)
        {
            if (index < 0 || index >= profiles.Count) return;
            PatientSession.Select(profiles[index]);
            RefreshRows();
            RefreshActiveProfile();
            feedback.text = "Perfil seleccionado. Gira para elegir una escena.";
        }

        private void RefreshActiveProfile()
        {
            var profile = PatientSession.ActiveProfile;
            activeProfileText.text = profile == null ? "Selecciona un perfil o crea uno nuevo" : "Perfil activo: " + profile.alias;
            frontProfileText.text = profile == null
                ? "Gira: crea o elige tu perfil en el panel detrás de ti para jugar."
                : "Perfil: " + profile.alias + " · Elige una escena. Puedes cambiar de perfil detrás de ti.";
            foreach (Button button in sceneButtons) button.interactable = profile != null;
        }
    }
}
