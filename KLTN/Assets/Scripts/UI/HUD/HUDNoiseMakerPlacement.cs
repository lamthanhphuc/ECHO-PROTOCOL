using System.Collections.Generic;
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

        [SerializeField]
        private Color doorJammerPreviewColor =
            new Color(0f, 0.9f, 1f, 0.25f);

        [SerializeField, Min(0f)]
        private float previewLift = 0.03f;

        private NetworkPlayerInteractor _interactor;
        private GameObject _previewRoot;
        private GameObject _previewVisual;
        private GameObject _previewSource;
        private MaterialPropertyBlock _propertyBlock;
        private readonly List<Material> _runtimePreviewMaterials =
            new List<Material>();

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
            if (_interactor == null)
            {
                SetVisible(false);
                return;
            }

            if (_interactor.TryGetNoiseMakerPlacementPreview(
                    out var noiseMakerPosition))
            {
                var source =
                    _interactor.NoiseMakerPreviewPrefab;

                EnsurePreview(
                    source,
                    "NoiseMakerPlacementPreview");

                if (_previewRoot == null)
                {
                    return;
                }

                _previewRoot.transform.SetPositionAndRotation(
                    noiseMakerPosition
                    + Vector3.up * previewLift,
                    Quaternion.identity);

                SetVisible(true);
                return;
            }

            if (_interactor.TryGetDoorJammerPlacementPreview(
                    out var jammerPosition,
                    out var jammerRotation))
            {
                var source =
                    _interactor.DoorJammerPreviewPrefab;

                EnsurePreview(
                    source,
                    "DoorJammerPlacementPreview");

                if (_previewRoot == null)
                {
                    return;
                }

                _previewRoot.transform.SetPositionAndRotation(
                    jammerPosition,
                    jammerRotation);

                SetVisible(true);
                return;
            }

            SetVisible(false);
        }

        private void EnsurePreview(
            GameObject source,
            string previewName)
        {
            if (source == null)
            {
                DestroyPreview();
                return;
            }

            if (_previewRoot != null
                && _previewSource == source)
            {
                return;
            }

            DestroyPreview();

            _previewSource = source;

            _previewRoot =
                new GameObject(previewName);

            _previewVisual =
                NetworkTeamToolHeldView
                    .InstantiateHeldVisualSafely(
                        source,
                        _previewRoot.transform);

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
            bool isDoorJammerPreview =
                _interactor != null
                && _previewSource
                    == _interactor.DoorJammerPreviewPrefab;

            Color activePreviewColor =
                isDoorJammerPreview
                    ? doorJammerPreviewColor
                    : previewColor;

            foreach (var renderer in _previewVisual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                if (isDoorJammerPreview)
                {
                    var sourceMaterials = renderer.sharedMaterials;
                    var previewMaterials =
                        new Material[sourceMaterials.Length];

                    for (int i = 0;
                         i < sourceMaterials.Length;
                         i++)
                    {
                        Material sourceMaterial =
                            sourceMaterials[i];

                        if (sourceMaterial == null)
                        {
                            continue;
                        }

                        var previewMaterial =
                            new Material(sourceMaterial)
                            {
                                name =
                                    sourceMaterial.name
                                    + " (Door Jammer Preview)",
                                renderQueue =
                                    (int)RenderQueue.Transparent,
                            };

                        previewMaterial.SetOverrideTag(
                            "RenderType",
                            "Transparent");

                        if (previewMaterial.HasProperty("_Surface"))
                            previewMaterial.SetFloat("_Surface", 1f);
                        if (previewMaterial.HasProperty("_SrcBlend"))
                            previewMaterial.SetFloat(
                                "_SrcBlend",
                                (float)BlendMode.SrcAlpha);
                        if (previewMaterial.HasProperty("_DstBlend"))
                            previewMaterial.SetFloat(
                                "_DstBlend",
                                (float)BlendMode.OneMinusSrcAlpha);
                        if (previewMaterial.HasProperty("_ZWrite"))
                            previewMaterial.SetFloat("_ZWrite", 0f);

                        previewMaterial.EnableKeyword(
                            "_SURFACE_TYPE_TRANSPARENT");
                        previewMaterial.DisableKeyword(
                            "_ALPHAPREMULTIPLY_ON");

                        previewMaterials[i] =
                            previewMaterial;
                        _runtimePreviewMaterials.Add(
                            previewMaterial);
                    }

                    renderer.sharedMaterials =
                        previewMaterials;
                }

                var material = renderer.sharedMaterial;
                if (material == null) continue;

                _propertyBlock.Clear();
                if (material.HasProperty("_BaseColor"))
                    _propertyBlock.SetColor(
                        "_BaseColor",
                        activePreviewColor);
                if (material.HasProperty("_Color"))
                    _propertyBlock.SetColor(
                        "_Color",
                        activePreviewColor);
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
            if (_previewRoot != null)
            {
                Destroy(_previewRoot);
            }

            _previewRoot = null;
            _previewVisual = null;
            _previewSource = null;

            foreach (Material material
                     in _runtimePreviewMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            _runtimePreviewMaterials.Clear();
        }

        private void OnDisable() => SetVisible(false);
        private void OnDestroy() => DestroyPreview();
    }
}
