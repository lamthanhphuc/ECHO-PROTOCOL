using EchoProtocol.Networking;
using UnityEngine;

public sealed class PlayerInteractionControlLock
    {
        private GameObject _player;
        private PlayerMovement _movement;
        private NetworkPlayerMovement _networkMovement;
        private PlayerInteraction _interaction;
        private NetworkPlayerInteractor _networkInteractor;
        private PlayerCamera _camera;
        private PlayerDownState _downState;
        private NetworkPlayerLifeState _networkLifeState;
        private bool _movementWasEnabled;
        private bool _networkMovementWasEnabled;
        private bool _interactionWasEnabled;
        private bool _networkInteractorWasEnabled;
        private bool _cameraWasEnabled;
        private CursorLockMode _previousLockMode;
        private bool _previousCursorVisible;

        public GameObject Player => _player;
        public bool IsLocked => _player != null;

        public void Acquire(GameObject player)
        {
            Release();
            if (player == null)
            {
                return;
            }

            _player = player;
            _movement = player.GetComponentInParent<PlayerMovement>();
            _networkMovement = player.GetComponentInParent<NetworkPlayerMovement>();
            _interaction = player.GetComponentInParent<PlayerInteraction>();
            _networkInteractor = player.GetComponentInParent<NetworkPlayerInteractor>();
            _downState = player.GetComponentInParent<PlayerDownState>();
            _networkLifeState = player.GetComponentInParent<NetworkPlayerLifeState>();
            _camera = Object.FindAnyObjectByType<PlayerCamera>();

            _movementWasEnabled = _movement != null && _movement.enabled;
            _networkMovementWasEnabled = _networkMovement != null && _networkMovement.enabled;
            _interactionWasEnabled = _interaction != null && _interaction.enabled;
            _networkInteractorWasEnabled = _networkInteractor != null && _networkInteractor.enabled;
            _cameraWasEnabled = _camera != null && _camera.enabled;
            _previousLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            if (_movement != null)
            {
                _movement.SetSprintBlocked(true);
                _movement.enabled = false;
            }

            if (_networkMovement != null)
            {
                _networkMovement.enabled = false;
            }

            if (_interaction != null)
            {
                _interaction.SetInteractionPromptSuppressed(true);
                _interaction.enabled = false;
            }

            if (_networkInteractor != null)
            {
                _networkInteractor.SetInteractionPromptSuppressed(true);
                _networkInteractor.enabled = false;
            }

            if (_camera != null)
            {
                _camera.enabled = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public bool ShouldAutoRelease()
        {
            if (_player == null || !_player.activeInHierarchy)
            {
                return true;
            }

            if (_downState != null && (_downState.IsDowned || _downState.IsEliminated))
            {
                return true;
            }

            return _networkLifeState != null
                && (_networkLifeState.IsDowned || _networkLifeState.IsCaught || _networkLifeState.IsEliminated);
        }

        public void Release()
        {
            if (_movement != null)
            {
                _movement.enabled = _movementWasEnabled;
                _movement.SetSprintBlocked(false);
                _movement.SetExternalSpeedMultiplier(1f);
            }

            if (_networkMovement != null)
            {
                _networkMovement.enabled = _networkMovementWasEnabled;
            }

            if (_interaction != null)
            {
                _interaction.SetInteractionPromptSuppressed(false);
                _interaction.enabled = _interactionWasEnabled;
            }

            if (_networkInteractor != null)
            {
                _networkInteractor.SetInteractionPromptSuppressed(false);
                _networkInteractor.enabled = _networkInteractorWasEnabled;
            }

            if (_camera != null)
            {
                _camera.enabled = _cameraWasEnabled;
            }

            if (_player != null)
            {
                Cursor.lockState = _previousLockMode;
                Cursor.visible = _previousCursorVisible;
            }

            _player = null;
            _movement = null;
            _networkMovement = null;
            _interaction = null;
            _networkInteractor = null;
            _camera = null;
            _downState = null;
            _networkLifeState = null;
        }
    }
