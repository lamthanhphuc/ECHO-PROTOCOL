using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local presentation limits for the exploration map.</summary>
public sealed class SceneAAmbientCulling : MonoBehaviour
{
    [SerializeField, Min(1f)] private float effectDistance = 40f;
    private readonly List<ParticleSystem> _particles = new();
    private readonly List<Animator> _animators = new();
    private readonly List<Light> _lights = new();
    private readonly List<AudioSource> _audio = new();
    private float _nextCheck;
    private Camera _viewCamera;
    private float _originalFarClip;

    private void LateUpdate()
    {
        var current = Camera.main;
        if (!current || !current.isActiveAndEnabled) return;
        if (_viewCamera != current)
        {
            RestoreCamera();
            _viewCamera = current;
            _originalFarClip = current.farClipPlane;
            current.farClipPlane = 300f;
        }
    }

    private void RestoreCamera()
    {
        if (_viewCamera) _viewCamera.farClipPlane = _originalFarClip;
        _viewCamera = null;
    }

    private void Awake()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            _particles.AddRange(root.GetComponentsInChildren<ParticleSystem>());
            foreach (var a in root.GetComponentsInChildren<Animator>())
                if (a.enabled && (a.name == "FlyingSaucer_Body" || a.name.StartsWith("Prop_Boat"))
                    && !(EchoProtocol.Networking.FloodedMapMission.Instance
                        && a.transform.IsChildOf(EchoProtocol.Networking.FloodedMapMission.Instance.Escort)))
                    _animators.Add(a);
            foreach (var l in root.GetComponentsInChildren<Light>())
                if (l.enabled && l.type == LightType.Point) _lights.Add(l);
            foreach (var a in root.GetComponentsInChildren<AudioSource>())
                if (a.enabled && a.clip && a.loop && a.playOnAwake && a.spatialBlend > 0.9f)
                    _audio.Add(a);
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.5f;
        var camera = _viewCamera;
        if (!camera || !camera.isActiveAndEnabled) return;
        Evaluate(camera.transform.position);
    }

    public void Evaluate(Vector3 viewer)
    {
        foreach (var p in _particles)
        {
            if (!p) continue;
            bool nearby = Near(viewer, p.transform.position);
            if (nearby && !p.isPlaying) p.Play(false);
            else if (!nearby && !p.isStopped)
                p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (var a in _animators)
            if (a) a.enabled = Near(viewer, a.transform.position);
        foreach (var l in _lights)
            if (l) l.enabled = Near(viewer, l.transform.position);
        foreach (var a in _audio)
        {
            if (!a) continue;
            bool nearby = Near(viewer, a.transform.position);
            if (nearby && !a.isPlaying && !AudioListener.pause) a.Play();
            else if (!nearby && a.isPlaying) a.Stop();
        }
    }

    private bool Near(Vector3 viewer, Vector3 position) =>
        (viewer - position).sqrMagnitude <= effectDistance * effectDistance;

    private void OnDisable()
    {
        RestoreCamera();
        foreach (var a in _animators) if (a) a.enabled = true;
        foreach (var l in _lights) if (l) l.enabled = true;
        foreach (var p in _particles) if (p && p.gameObject.activeInHierarchy && !p.isPlaying) p.Play(false);
        foreach (var a in _audio)
            if (a && a.isActiveAndEnabled && !a.isPlaying && !AudioListener.pause) a.Play();
    }
}
