using UnityEngine;

using UnityEngine.Rendering.Universal;

namespace EchoProtocol.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class StoreV2PreviewLighting : MonoBehaviour
    {
        [Header("Live Preview Lighting")]

        [SerializeField]
        [Range(0.5f, 8f)]
        private float keyIntensity = 4.7f;

        [SerializeField]
        [Range(0.5f, 8f)]
        private float fillIntensity = 3.2f;

        [SerializeField]
        [Range(0.5f, 8f)]
        private float rimIntensity = 2.1f;

        [SerializeField]
        [Range(0f, 6f)]
        private float frontFillIntensity = 2.0f;

        [SerializeField]
        private Color previewBackground =
            new Color(
                0.025f,
                0.028f,
                0.031f,
                1f);

        private const string RigName =
            "[Store Live Preview Rig]";

        private const string FrontFillName =
            "Store Front Fill";

        private GameObject _rig;

        private float _nextApply;

        private void OnEnable()
        {
            _rig = null;
            _nextApply = 0f;
        }
        private void OnDisable()
        {
            ResolveRig();
            if (_rig == null) return;
            var front = FindRecursive(_rig.transform, FrontFillName)?.GetComponent<Light>();
            if (front != null) front.enabled = false;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextApply)
                return;

            _nextApply =
                Time.unscaledTime + 0.35f;

            ApplyNow();
        }

        public void ApplyNow()
        {
            ResolveRig();

            if (_rig == null)
                return;

            Camera camera =
                _rig.GetComponentInChildren<
                    Camera>(
                    true);

            if (camera != null)
            {
                // Keep the model composited over the Store background.
                camera.backgroundColor = new Color(previewBackground.r,previewBackground.g,previewBackground.b,0f);

                camera.allowHDR = true;
                camera.allowMSAA = true;
            }

            Light[] lights =
                _rig.GetComponentsInChildren<
                    Light>(
                    true);

            for (int i = 0;
                 i < lights.Length;
                 i++)
            {
                Light light =
                    lights[i];

                if (light == null)
                    continue;

                FixRenderingLayer(
                    light);

                if (light.name == "Key Light")
                {
                    light.intensity =
                        keyIntensity * 4f;

                    light.color =
                        new Color(
                            1f,
                            0.94f,
                            0.86f);

                    light.shadows =
                        LightShadows.Soft;
                }
                else if (light.name == "Fill Light")
                {
                    light.intensity =
                        fillIntensity * 4f;

                    light.color =
                        new Color(
                            0.72f,
                            0.82f,
                            1f);

                    light.shadows =
                        LightShadows.None;
                }
                else if (light.name == "Rim Light")
                {
                    light.intensity =
                        rimIntensity * 4f;

                    light.color =
                        new Color(
                            1f,
                            0.36f,
                            0.28f);

                    light.shadows =
                        LightShadows.None;
                }
            }

            Light front =
                ResolveFrontFill(
                    camera);

            if (front != null)
            {
                front.intensity =
                    frontFillIntensity * 4f;
                front.enabled = camera != null && camera.enabled && camera.gameObject.activeInHierarchy;
            }

            Renderer[] renderers =
                _rig.GetComponentsInChildren<
                    Renderer>(
                    true);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                FixRenderingLayer(
                    renderers[i]);
            }
        }

        private void ResolveRig()
        {
            if (_rig != null)
                return;

            if (!gameObject.scene.IsValid()
                || !gameObject.scene.isLoaded)
            {
                return;
            }

            GameObject[] roots =
                gameObject.scene
                    .GetRootGameObjects();

            for (int i = 0;
                 i < roots.Length;
                 i++)
            {
                if (roots[i].name == RigName)
                {
                    _rig = roots[i];
                    return;
                }
            }
        }

        private Light ResolveFrontFill(
            Camera camera)
        {
            Transform existing =
                FindRecursive(
                    _rig.transform,
                    FrontFillName);

            Light light =
                existing != null
                    ? existing.GetComponent<Light>()
                    : null;

            if (light == null)
            {
                GameObject owner =
                    new GameObject(
                        FrontFillName,
                        typeof(Light));

                owner.hideFlags =
                    HideFlags.DontSave;

                owner.transform.SetParent(
                    _rig.transform,
                    false);

                owner.transform.localRotation =
                    Quaternion.Euler(
                        10f,
                        180f,
                        0f);

                light =
                    owner.GetComponent<Light>();

                light.enabled = false;

                light.color =
                    Color.white;

                light.shadows =
                    LightShadows.None;
            }

            if (camera != null)
            {
                light.cullingMask = camera.cullingMask;
                light.transform.position = camera.transform.position + camera.transform.forward * 2f;
            }
            // A preview fill must never illuminate the world, even when URP
            // ignores GameObject culling masks or the shop is closed.
            light.type = LightType.Point;
            light.range = 12f;

            FixRenderingLayer(
                light);

            return light;
        }

        private static Transform FindRecursive(
            Transform root,
            string target)
        {
            if (root == null)
                return null;

            if (root.name == target)
                return root;

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform result =
                    FindRecursive(
                        root.GetChild(i),
                        target);

                if (result != null)
                    return result;
            }

            return null;
        }

        private static void FixRenderingLayer(
            Component component)
        {
            if (component == null)
                return;

            const uint previewMask = 1u << 7;
            if (component is Light light)
            {
                light.renderingLayerMask = (int)previewMask;
                light.GetUniversalAdditionalLightData().renderingLayers = previewMask;
            }
            else if (component is Renderer renderer) renderer.renderingLayerMask = previewMask;

        }
    }
}
