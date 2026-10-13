using System.Reflection;
using UnityEngine;

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
        private Color previewBackground =
            new Color(
                0.025f,
                0.028f,
                0.031f,
                1f);

        private const string RigName =
            "[Store Live Preview Rig]";
private GameObject _rig;

        private float _nextApply;

        private void OnEnable()
        {
            _rig = null;
            _nextApply = 0f;
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
                camera.backgroundColor =
                    previewBackground;

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
                        keyIntensity;

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
                        fillIntensity;

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
                        rimIntensity;

                    light.color =
                        new Color(
                            1f,
                            0.36f,
                            0.28f);

                    light.shadows =
                        LightShadows.None;
                }
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

            PropertyInfo property =
                component.GetType()
                    .GetProperty(
                        "renderingLayerMask",
                        BindingFlags.Instance
                        | BindingFlags.Public);

            if (property == null
                || !property.CanWrite)
            {
                return;
            }

            try
            {
                if (property.PropertyType
                    == typeof(uint))
                {
                    property.SetValue(
                        component,
                        1u << 30);
                }
                else if (
                    property.PropertyType
                    == typeof(int))
                {
                    property.SetValue(
                        component,
                        1 << 30);
                }
            }
            catch
            {
            }
        }
    }
}