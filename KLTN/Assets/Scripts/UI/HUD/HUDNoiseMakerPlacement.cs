using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoProtocol.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class HUDNoiseMakerPlacement : MonoBehaviour
    {
        [SerializeField]
        private Color previewColor = new Color(1f, 0.55f, 0.12f, 0.8f);

        [SerializeField, Min(0f)]
        private float previewLift = 0.03f;

        private NetworkPlayerInteractor _interactor;
        private GameObject _previewRoot;
        private GameObject _previewVisual;
        private MaterialPropertyBlock _propertyBlock;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        public void BindPlayer(NetworkPlayerInteractor interactor)
        {
            if (_interactor == interactor) return;
            _interactor = interactor;
            DestroyPreview();
        }

        public void Unbind()
        {
            _interactor = null;
            DestroyPreview();
        }

        private void LateUpdate()
        {
            if (_interactor == null
                || !_interactor.TryGetNoiseMakerPlacementPreview(out var position))
            {
                SetVisible(false);
                return;
            }

            EnsurePreview();
            if (_previewRoot == null) return;

            _previewRoot.transform.SetPositionAndRotation(
                position + Vector3.up * previewLift, Quaternion.identity);
            SetVisible(true);
        }

        private void EnsurePreview()
        {
            if (_previewRoot != null) return;

            var source = _interactor != null ? _interactor.NoiseMakerPreviewPrefab : null;
            if (source == null) return;

            _previewRoot = new GameObject("NoiseMakerPlacementPreview");
            _previewVisual = NetworkTeamToolHeldView.InstantiateHeldVisualSafely(
                source, _previewRoot.transform);
            if (_previewVisual == null)
            {
                DestroyPreview();
                return;
            }

            ConfigurePreviewRenderers();
            SetVisible(false);
        }

        private void ConfigurePreviewRenderers()
        {
            foreach (var renderer in _previewVisual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var material = renderer.sharedMaterial;
                if (material == null) continue;

                _propertyBlock.Clear();
                if (material.HasProperty("_BaseColor"))
                    _propertyBlock.SetColor("_BaseColor", previewColor);
                if (material.HasProperty("_Color"))
                    _propertyBlock.SetColor("_Color", previewColor);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void SetVisible(bool visible)
        {
            if (_previewRoot != null && _previewRoot.activeSelf != visible)
                _previewRoot.SetActive(visible);
        }

        private void DestroyPreview()
        {
            if (_previewRoot != null) Destroy(_previewRoot);
            _previewRoot = null;
            _previewVisual = null;
        }

        private void OnDisable() => SetVisible(false);
        private void OnDestroy() => DestroyPreview();
    }
}
