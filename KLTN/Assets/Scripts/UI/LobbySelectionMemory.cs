using System.Collections;
using EchoProtocol.Auth;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.UI
{
    public sealed class LobbySelectionMemory : MonoBehaviour
    {
        [System.Serializable]
        private sealed class Selection { public int Character, Outfit, Pet, Skin, Tool; }
        public static string Key(string setting) => "Echo.Lobby." + (string.IsNullOrEmpty(AuthSession.CurrentUserId) ? "guest" : AuthSession.CurrentUserId) + "." + setting;
        public static int Read(string setting, int fallback) => PlayerPrefs.GetInt(Key(setting), fallback);
        public static void Write(string setting, int value) { PlayerPrefs.SetInt(Key(setting), value); PlayerPrefs.Save(); }
        private LobbyPlayerState _state;
        private string _last, _key;
        private bool _restored;
        private float _nextSave;
        private bool _restoring = true;
        private void OnEnable() => LobbyPlayerState.LocalSelectionRequestCompleted += SelectionCompleted;
        private void OnDisable() => LobbyPlayerState.LocalSelectionRequestCompleted -= SelectionCompleted;
        private void SelectionCompleted(LobbySelectionResult result)
        {
            if (!_restoring && result.Accepted) _restored = true;
        }

        private IEnumerator Start()
        {
            _state = GetComponent<LobbyPlayerState>();
            // Fusion can spawn the local player before the Lobby scene is active.
            // Wait for scene and player registration rather than terminating restoration.
            while (_state != null && _state.Object != null && _state.Object.IsValid
                && !Valid()) yield return null;
            if (!Valid()) yield break;
            while (Valid() && (_state.Runner == null || !_state.Runner.TryGetPlayerObject(_state.Object.InputAuthority, out var registered) || registered != _state.Object))
                yield return null;
            if (!Valid()) yield break;
            _key = Key("equipment");
            _last = PlayerPrefs.GetString(_key, "");
            bool migration = NetworkBootstrap.IsHostMigrationReconciliationPending;
            while (Valid() && (!_state.HasVerifiedBackendIdentity
                || NetworkBootstrap.IsHostMigrationReconciliationPending
                || NetworkBootstrap.Instance?.ReconnectIdentityPending == true)) yield return null;
            if (!Valid()) yield break;
            if (migration) { _restoring = false; _restored = true; yield break; }
            if (_last.Length > 0) {
                Selection saved = null;
                try { saved = JsonUtility.FromJson<Selection>(_last); } catch (System.ArgumentException) { }
                if (saved != null) {
                    // Use normal validated selection requests on the registered player.
                    if (_state.CharacterId != saved.Character) _state.RequestCharacter(saved.Character);
                    if (_state.PetId != saved.Pet) _state.RequestPet(saved.Pet);
                    float deadline = Time.unscaledTime + 15f;
                    while (Valid() && Time.unscaledTime < deadline && (_state.CharacterId != saved.Character || _state.PetId != saved.Pet)) yield return null;
                    if (!Valid()) yield break;
                    if (_state.CharacterId == saved.Character) _state.RequestCharacterOutfit(saved.Outfit);
                    if (_state.PetId == saved.Pet && saved.Pet != 0) _state.RequestPetSkin(saved.Skin);
                    if (_state.ToolId != saved.Tool) _state.RequestTool(saved.Tool);
                    // Let remote authority confirm or reject the saved equipment before capturing a new snapshot.
                    deadline = Time.unscaledTime + 15f;
                    while (Valid() && Time.unscaledTime < deadline
                        && (_state.CharacterOutfitId != saved.Outfit || _state.PetSkinId != saved.Skin || _state.ToolId != saved.Tool)) yield return null;
                    if (!Valid()) yield break;
                    if (_state.CharacterId != saved.Character || _state.PetId != saved.Pet
                        || _state.CharacterOutfitId != saved.Outfit || _state.PetSkinId != saved.Skin
                        || _state.ToolId != saved.Tool)
                    {
                        // Preserve the saved choice on rejection/timeout. A later explicit
                        // accepted selection can establish a new saved snapshot.
                        _restoring = false;
                        yield break;
                    }
                }
            }
            _restoring = false;
            _restored = true;
        }
        private bool Valid() => _state != null && _state.HasSpawnedState && _state.Object != null && _state.Object.IsValid
            && _state.Object.HasInputAuthority && !_state.IsGameplayPlayer && SceneManager.GetActiveScene().name == "Lobby";
        private void Update()
        {
            if (!_restored || !Valid() || Time.unscaledTime < _nextSave) return;
            _nextSave = Time.unscaledTime + .25f;
            SaveCurrent();
        }
        private void SaveCurrent()
        {
            if (!_restored || !Valid() || _key != Key("equipment")) return;
            if (string.IsNullOrEmpty(_key)) _key = Key("equipment");
            var selection = new Selection { Character=_state.CharacterId, Outfit=_state.CharacterOutfitId, Pet=_state.PetId, Skin=_state.PetSkinId, Tool=_state.ToolId };
            string snapshot = JsonUtility.ToJson(selection);
            if (snapshot == _last) return;
            _last = snapshot;
            PlayerPrefs.SetString(_key, snapshot);
            PlayerPrefs.Save();
        }
        public static void SaveBeforeLeaving()
        {
            foreach (var memory in Object.FindObjectsByType<LobbySelectionMemory>(FindObjectsSortMode.None))
                memory.SaveCurrent();
        }
    }
}
