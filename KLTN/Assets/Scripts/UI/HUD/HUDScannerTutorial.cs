using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace EchoProtocol.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class HUDScannerTutorial : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Behaviour")]
        [SerializeField, Min(0f)] private float dismissInputDelay = 0.2f;

        private readonly PlayerInteractionControlLock _controlLock = new PlayerInteractionControlLock();

        private PlayerInventory _inventory;
        private GameObject _playerRoot;
        private bool _hadScanner;
        private bool _shownThisMatch;
        private bool _isOpen;
        private float _dismissAllowedAt;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            SetVisual(false);
        }

        private void Update()
        {
            if (!_isOpen) return;

            if (_controlLock.ShouldAutoRelease())
            {
                Hide();
                return;
            }

            if (Time.unscaledTime < _dismissAllowedAt) return;
            if (WasAnyButtonPressedThisFrame()) Hide();
        }

        public void BindPlayer(PlayerInventory inventory, GameObject playerRoot)
        {
            if (_inventory == inventory && _playerRoot == playerRoot) return;

            Unbind();

            _inventory = inventory;
            _playerRoot = playerRoot;

            if (_inventory == null) return;

            _hadScanner = HasScanner();
            _inventory.InventoryChanged += HandleInventoryChanged;
        }

        public void Unbind()
        {
            if (_inventory != null)
            {
                _inventory.InventoryChanged -= HandleInventoryChanged;
            }

            Hide();
            _inventory = null;
            _playerRoot = null;
            _hadScanner = false;
        }

        private void HandleInventoryChanged()
        {
            bool hasScanner = HasScanner();
            if (hasScanner && !_hadScanner && !_shownThisMatch) Show();
            _hadScanner = hasScanner;
        }

        private bool HasScanner()
        {
            return _inventory != null && PlayerInventory.ResolveToolId(_inventory.TeamToolSlot) == 1;
        }

        private void Show()
        {
            if (_isOpen || _shownThisMatch || _playerRoot == null) return;

            _shownThisMatch = true;
            _isOpen = true;
            SetVisual(true);
            _controlLock.Acquire(_playerRoot, Hide);
            _dismissAllowedAt = Time.unscaledTime + dismissInputDelay;
        }

        public void Hide()
        {
            if (!_isOpen) return;

            _isOpen = false;
            SetVisual(false);
            _controlLock.Release();
        }

        private void SetVisual(bool visible)
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = visible;
        }

        private static bool WasAnyButtonPressedThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;

            if (Mouse.current != null
                && (Mouse.current.leftButton.wasPressedThisFrame
                    || Mouse.current.rightButton.wasPressedThisFrame
                    || Mouse.current.middleButton.wasPressedThisFrame
                    || Mouse.current.forwardButton.wasPressedThisFrame
                    || Mouse.current.backButton.wasPressedThisFrame))
            {
                return true;
            }

            if (Gamepad.current != null)
            {
                foreach (var control in Gamepad.current.allControls)
                {
                    if (control is ButtonControl button && button.wasPressedThisFrame) return true;
                }
            }

            return false;
        }

        private void OnDisable()
        {
            Unbind();
        }
    }
}
