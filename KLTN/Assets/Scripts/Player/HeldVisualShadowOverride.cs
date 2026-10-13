using UnityEngine;
using UnityEngine.Rendering;

// Owns temporary materials per renderer; never modifies project/shared materials.
[DisallowMultipleComponent, DefaultExecutionOrder(1000)]
public sealed class HeldVisualShadowOverride : MonoBehaviour
{
    private Renderer _renderer;
    private Material[] _originalMaterials;
    private Material[] _heldMaterials;
    private ShadowCastingMode _originalCasting;
    private bool _originalReceiving;
    private bool _active;

    public static void SetActive(Renderer renderer, bool active)
    {
        if (renderer == null) return;
        var state = renderer.GetComponent<HeldVisualShadowOverride>();
        if (state == null && active) state = renderer.gameObject.AddComponent<HeldVisualShadowOverride>();
        if (state == null) return;
        state._renderer = renderer;
        state._active = active;
        if (active) state.Apply();
        else state.Restore();
    }

    private void LateUpdate()
    {
        if (_active) Apply();
    }

    private void Apply()
    {
        if (_renderer == null) return;
        var current = _renderer.sharedMaterials;
        bool unchanged = _heldMaterials != null && current.Length == _heldMaterials.Length;
        for (int i = 0; unchanged && i < current.Length; i++) unchanged = current[i] == _heldMaterials[i];
        if (!unchanged)
        {
            // A skin change may replace materials while the override is active.
            if (_heldMaterials != null)
                for (int i = 0; i < current.Length && i < _heldMaterials.Length; i++)
                    if (current[i] == _heldMaterials[i]) current[i] = _originalMaterials[i];
            if (_originalMaterials == null)
            {
                _originalCasting = _renderer.shadowCastingMode;
                _originalReceiving = _renderer.receiveShadows;
            }
            ReleaseMaterials();
            _originalMaterials = current;
            _heldMaterials = new Material[current.Length];
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] == null) continue;
                var material = new Material(current[i])
                {
                    name = current[i].name + " (Held No Shadows)",
                    hideFlags = HideFlags.DontSave
                };
                if (material.HasProperty("_ReceiveShadows")) material.SetFloat("_ReceiveShadows", 0f);
                material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                _heldMaterials[i] = material;
            }
            _renderer.sharedMaterials = _heldMaterials;
        }
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
    }

    private void Restore()
    {
        if (_originalMaterials == null) return;
        if (_renderer != null)
        {
            var current = _renderer.sharedMaterials;
            for (int i = 0; i < current.Length && i < _heldMaterials.Length; i++)
                if (current[i] == _heldMaterials[i]) current[i] = _originalMaterials[i];
            _renderer.sharedMaterials = current;
            _renderer.shadowCastingMode = _originalCasting;
            _renderer.receiveShadows = _originalReceiving;
        }
        ReleaseMaterials();
        _originalMaterials = null;
    }

    private void ReleaseMaterials()
    {
        if (_heldMaterials == null) return;
        foreach (var material in _heldMaterials)
        {
            if (material == null) continue;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
        _heldMaterials = null;
    }

    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}
