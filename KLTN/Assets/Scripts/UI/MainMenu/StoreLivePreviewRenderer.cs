using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Shared Store V2 live 3D preview.
    ///
    /// Only one model is rendered at a time.
    /// Character and Team Tool panels share the same RenderTexture.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoreLivePreviewRenderer : MonoBehaviour
    {
        private const int PreviewLayer = 31;

        private static readonly Vector3 PreviewWorldPosition =
            new Vector3(0f, -10000f, 0f);

        private const int TextureSize = 512;

        [SerializeField]
        private float rotationSpeed = 28f;

        private GameObject _rig;
        private Transform _spinRoot;

        private Camera _camera;

        private Light _keyLight;
        private Light _fillLight;
        private Light _rimLight;

        private RenderTexture _renderTexture;

        private GameObject _currentModel;

        private RawImage _target;

        private string _currentResource;

        public void EnsureInitialized()
        {
            if (_rig != null)
                return;

            _rig =
                new GameObject(
                    "[Store Live Preview Rig]");

            _rig.hideFlags =
                HideFlags.DontSave;

            if (gameObject.scene.IsValid()
                && gameObject.scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(
                    _rig,
                    gameObject.scene);
            }

            _rig.transform.position =
                PreviewWorldPosition;

            var spinOwner =
                CreateRigObject(
                    "Model Spin");

            _spinRoot =
                spinOwner.transform;

            _spinRoot.SetParent(
                _rig.transform,
                false);

            _spinRoot.localPosition =
                Vector3.zero;

            CreateCamera();
            CreateLights();
            CreateRenderTexture();

            _camera.targetTexture =
                _renderTexture;

            _camera.enabled =
                false;
        }

        public bool Show(
            string resourcePath,
            RawImage target)
        {
            EnsureInitialized();

            if (target == null
                || string.IsNullOrWhiteSpace(resourcePath))
            {
                return false;
            }

            _target =
                target;

            _target.texture =
                _renderTexture;

            _target.color =
                Color.white;

            if (_currentModel != null
                && string.Equals(
                    _currentResource,
                    resourcePath,
                    System.StringComparison.Ordinal))
            {
                _camera.enabled =
                    true;

                enabled =
                    true;

                return true;
            }

            DestroyCurrentModel();

            var prefab =
                Resources.Load<GameObject>(
                    resourcePath);

            if (prefab == null)
            {
                Debug.LogWarning(
                    "[StoreLivePreview] Missing preview prefab: " +
                    resourcePath,
                    this);

                _target.texture =
                    null;

                _target.color =
                    Color.clear;

                _camera.enabled =
                    false;

                return false;
            }

            _currentModel =
                Instantiate(
                    prefab,
                    _spinRoot);

            _currentModel.name =
                "Preview_" +
                prefab.name;

            _currentModel.transform.localPosition =
                Vector3.zero;

            _currentModel.transform.localRotation =
                Quaternion.identity;

            SetLayerRecursively(
                _currentModel,
                PreviewLayer);

            foreach (var animator in
                     _currentModel.GetComponentsInChildren<
                         Animator>(true))
            {
                if (animator.runtimeAnimatorController != null)
                {
                    animator.Update(0f);
                }
            }

            if (!CenterAndFrameCurrentModel())
            {
                DestroyCurrentModel();

                _target.texture =
                    null;

                _target.color =
                    Color.clear;

                return false;
            }

            _currentResource =
                resourcePath;

            _camera.enabled =
                true;

            enabled =
                true;

            return true;
        }

        public void Clear(
            RawImage target = null)
        {
            if (target != null)
            {
                target.texture =
                    null;

                target.color =
                    Color.clear;
            }

            if (_target == target
                || target == null)
            {
                _target =
                    null;
            }

            DestroyCurrentModel();

            if (_camera != null)
            {
                _camera.enabled =
                    false;
            }
        }

        private void Update()
        {
            if (_currentModel == null
                || _spinRoot == null)
            {
                return;
            }

            _spinRoot.Rotate(
                0f,
                rotationSpeed *
                Time.unscaledDeltaTime,
                0f,
                Space.Self);
        }

        private bool CenterAndFrameCurrentModel()
        {
            var renderers =
                _currentModel.GetComponentsInChildren<
                    Renderer>(true);

            bool found =
                false;

            Bounds bounds =
                default;

            foreach (var renderer in renderers)
            {
                if (!renderer.enabled)
                    continue;

                if (!found)
                {
                    bounds =
                        renderer.bounds;

                    found =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }

            if (!found)
            {
                Debug.LogWarning(
                    "[StoreLivePreview] Preview model contains no visible renderer.",
                    this);

                return false;
            }

            // Move model geometry so its visual center sits on
            // the spin pivot. This produces natural rotation
            // even when the source prefab pivot is at the feet.
            Vector3 centerDelta =
                _spinRoot.position -
                bounds.center;

            _currentModel.transform.position +=
                centerDelta;

            // Recalculate after centering.
            renderers =
                _currentModel.GetComponentsInChildren<
                    Renderer>(true);

            found =
                false;

            foreach (var renderer in renderers)
            {
                if (!renderer.enabled)
                    continue;

                if (!found)
                {
                    bounds =
                        renderer.bounds;

                    found =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }

            if (!found)
                return false;

            // Bounding sphere remains safe while the object rotates.
            float radius =
                Mathf.Max(
                    0.15f,
                    bounds.extents.magnitude) *
                1.12f;

            float halfFov =
                _camera.fieldOfView *
                0.5f *
                Mathf.Deg2Rad;

            float distance =
                radius /
                Mathf.Sin(
                    halfFov);

            Vector3 viewDirection =
                new Vector3(
                    0.32f,
                    0.10f,
                    -1f)
                .normalized;

            Vector3 targetPoint =
                _spinRoot.position +
                Vector3.up *
                bounds.size.y *
                0.015f;

            _camera.transform.position =
                targetPoint +
                viewDirection *
                distance;

            _camera.transform.LookAt(
                targetPoint);

            _camera.nearClipPlane =
                Mathf.Max(
                    0.01f,
                    distance -
                    radius * 2f);

            _camera.farClipPlane =
                distance +
                radius * 4f;

            return true;
        }

        private void CreateCamera()
        {
            var owner =
                CreateRigObject(
                    "Preview Camera",
                    typeof(Camera));

            owner.transform.SetParent(
                _rig.transform,
                false);

            _camera =
                owner.GetComponent<Camera>();

            _camera.enabled =
                false;

            _camera.clearFlags =
                CameraClearFlags.SolidColor;

            _camera.backgroundColor =
                new Color(
                    0.018f,
                    0.025f,
                    0.027f,
                    1f);

            _camera.fieldOfView =
                28f;

            _camera.cullingMask =
                1 << PreviewLayer;

            _camera.allowHDR =
                true;

            _camera.allowMSAA =
                true;
        }

        private void CreateLights()
        {
            _keyLight =
                CreateDirectionalLight(
                    "Key Light",
                    new Vector3(
                        35f,
                        -35f,
                        0f),
                    2.4f);

            _fillLight =
                CreateDirectionalLight(
                    "Fill Light",
                    new Vector3(
                        330f,
                        145f,
                        0f),
                    1.25f);

            _rimLight =
                CreateDirectionalLight(
                    "Rim Light",
                    new Vector3(
                        20f,
                        155f,
                        0f),
                    0.85f);
        }

        private Light CreateDirectionalLight(
            string name,
            Vector3 rotation,
            float intensity)
        {
            var owner =
                CreateRigObject(
                    name,
                    typeof(Light));

            owner.transform.SetParent(
                _rig.transform,
                false);

            owner.transform.localRotation =
                Quaternion.Euler(
                    rotation);

            var light =
                owner.GetComponent<Light>();

            light.type =
                LightType.Directional;

            light.intensity =
                intensity;

            light.shadows =
                LightShadows.Soft;

            light.cullingMask =
                1 << PreviewLayer;

            return light;
        }

        private void CreateRenderTexture()
        {
            _renderTexture =
                new RenderTexture(
                    TextureSize,
                    TextureSize,
                    24,
                    RenderTextureFormat.ARGB32);

            _renderTexture.name =
                "StoreLivePreviewRT";

            _renderTexture.useMipMap =
                false;

            _renderTexture.autoGenerateMips =
                false;

            _renderTexture.antiAliasing =
                1;

            _renderTexture.Create();
        }

        private GameObject CreateRigObject(
            string name,
            params System.Type[] components)
        {
            var owner =
                new GameObject(
                    name,
                    components);

            owner.hideFlags =
                HideFlags.DontSave;

            if (gameObject.scene.IsValid()
                && gameObject.scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(
                    owner,
                    gameObject.scene);
            }

            owner.layer =
                PreviewLayer;

            return owner;
        }

        private void DestroyCurrentModel()
        {
            _currentResource =
                null;

            if (_currentModel == null)
                return;

            _currentModel.SetActive(
                false);

            DestroyObject(
                _currentModel);

            _currentModel =
                null;

            if (_spinRoot != null)
            {
                _spinRoot.localRotation =
                    Quaternion.identity;
            }
        }

        private static void SetLayerRecursively(
            GameObject root,
            int layer)
        {
            root.layer =
                layer;

            foreach (Transform child
                     in root.transform)
            {
                SetLayerRecursively(
                    child.gameObject,
                    layer);
            }
        }

        private void OnEnable()
        {
            if (_camera != null
                && _currentModel != null
                && _target != null)
            {
                _camera.enabled =
                    true;
            }
        }

        private void OnDisable()
        {
            if (_camera != null)
            {
                _camera.enabled =
                    false;
            }
        }

        private void OnDestroy()
        {
            DestroyCurrentModel();

            if (_renderTexture != null)
            {
                _renderTexture.Release();

                DestroyObject(
                    _renderTexture);

                _renderTexture =
                    null;
            }

            if (_rig != null)
            {
                DestroyObject(
                    _rig);

                _rig =
                    null;
            }
        }

        private static void DestroyObject(
            Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}

