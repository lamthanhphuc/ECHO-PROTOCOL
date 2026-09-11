using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerFirstPersonVisibility : MonoBehaviour
{
    [SerializeField] private bool hideBodyForLocalCamera = true;
    [SerializeField] private bool keepHandsIfSeparateRenderers = true;
    [SerializeField] private string[] firstPersonOnlyRendererNameTokens = { "FirstPersonArms", "FirstPersonWristCuff" };
    [SerializeField] private string[] firstPersonRendererNameTokens = { "hand", "arm", "upperarm", "forearm", "glove", "sleeve" };
    [SerializeField] private string[] alwaysVisibleNameTokens = { "Held_", "Runtime_CoreCarryAnchor", "Runtime_RightHandAnchor" };

    private readonly Dictionary<Renderer, bool> _originalRendererStates = new Dictionary<Renderer, bool>();
    private NetworkObject _networkObject;

    private void Awake()
    {
        _networkObject = GetComponentInParent<NetworkObject>();
        CaptureRenderers();
    }

    private void OnEnable()
    {
        CaptureRenderers();
        ApplyVisibility(IsLocalView());
    }

    private void LateUpdate()
    {
        CaptureRenderers();
        ApplyVisibility(IsLocalView());
    }

    private void OnDisable()
    {
        RestoreVisibility();
    }

    private void CaptureRenderers()
    {
        if (_originalRendererStates.Count > 0)
        {
            var deadKeys = new List<Renderer>();
            foreach (var k in _originalRendererStates.Keys)
            {
                if (k == null) deadKeys.Add(k);
            }
            for (int i = 0; i < deadKeys.Count; i++)
            {
                _originalRendererStates.Remove(deadKeys[i]);
            }
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && !_originalRendererStates.ContainsKey(renderer))
            {
                _originalRendererStates.Add(renderer, renderer.enabled);
            }
        }
    }

    private void ApplyVisibility(bool isLocalView)
    {
        bool hasFirstPersonOnlyRenderer = false;
        if (isLocalView)
        {
            foreach (var r in _originalRendererStates.Keys)
            {
                if (r != null && IsFirstPersonOnlyRenderer(r))
                {
                    hasFirstPersonOnlyRenderer = true;
                    break;
                }
            }
        }

        foreach (var pair in _originalRendererStates)
        {
            Renderer renderer = pair.Key;
            if (renderer == null)
            {
                continue;
            }

            bool shouldShow = pair.Value;
            if (IsFirstPersonOnlyRenderer(renderer))
            {
                shouldShow = isLocalView;
                if (shouldShow)
                {
                    if (renderer is SkinnedMeshRenderer smr && !smr.updateWhenOffscreen)
                    {
                        smr.updateWhenOffscreen = true;
                    }

                    if (renderer.name.Contains("FirstPersonArms"))
                    {
                        if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null || renderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
                        {
                            var suit = FindSuitRenderer();
                            if (suit != null && suit.sharedMaterial != null)
                            {
                                renderer.sharedMaterial = suit.sharedMaterial;
                            }
#if UNITY_EDITOR
                            else
                            {
                                var fallback = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                                    "Assets/Materials/PlayerCharacter/M_PF_PlayerCharacter_P1_Default_Suit.mat");
                                if (fallback != null) renderer.sharedMaterial = fallback;
                            }
#endif
                        }
                    }
                }
            }
            else if (isLocalView && hideBodyForLocalCamera && !IsAlwaysVisible(renderer))
            {
                shouldShow = keepHandsIfSeparateRenderers && IsFirstPersonRenderer(renderer);
                shouldShow = !hasFirstPersonOnlyRenderer && keepHandsIfSeparateRenderers && IsFirstPersonRenderer(renderer);
            }

            if (renderer.enabled != shouldShow)
            {
                renderer.enabled = shouldShow;
            }
        }
    }

    private void RestoreVisibility()
    {
        foreach (var pair in _originalRendererStates)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }
    }

    private bool IsLocalView()
    {
        if (_networkObject == null)
        {
            _networkObject = GetComponentInParent<NetworkObject>();
        }

        if (_networkObject != null && _networkObject.IsValid
            && !_networkObject.HasInputAuthority)
        {
            return false;
        }

        Transform root = transform.root;
        foreach (PlayerCamera cameraController in FindObjectsByType<PlayerCamera>(FindObjectsInactive.Exclude))
        {
            if (cameraController.Target == transform || cameraController.Target == root)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAlwaysVisible(Renderer renderer)
    {
        return NameContainsToken(renderer.transform, alwaysVisibleNameTokens);
    }

    private bool IsFirstPersonOnlyRenderer(Renderer renderer)
    {
        return NameContainsToken(renderer.transform, firstPersonOnlyRendererNameTokens);
    }

    private Renderer FindSuitRenderer()
    {
        Transform root = transform.root;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null && r.name.Equals("suit", System.StringComparison.OrdinalIgnoreCase))
            {
                return r;
            }
        }
        return null;
    }

    private bool IsFirstPersonRenderer(Renderer renderer)
    {
        return NameContainsToken(renderer.transform, firstPersonRendererNameTokens);
    }

    private static bool NameContainsToken(Transform source, string[] tokens)
    {
        if (source == null || tokens == null)
        {
            return false;
        }

        for (Transform current = source; current != null; current = current.parent)
        {
            string currentName = current.name;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && currentName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
