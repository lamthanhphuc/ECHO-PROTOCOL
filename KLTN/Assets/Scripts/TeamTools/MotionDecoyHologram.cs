using System.Collections;
using System.Collections.Generic;
using EchoProtocol.AI.Stalker;
using UnityEngine;

public sealed class MotionDecoyHologram : MonoBehaviour
{
    [SerializeField] private Animator vfxAnimator;
    [SerializeField] private Animator bodyAnimator;
    [SerializeField, Min(1f)] private float lifetime = 8f;
    [SerializeField, Min(0.2f)] private float stalkerShutdownDistance = 1.25f;
    [SerializeField, Min(0.05f)] private float sensorRefreshInterval = 0.2f;
    [SerializeField, Min(0f)] private float dissolveDuration = 0.65f;

    private readonly List<StalkerVisionSensor> claimedSensors = new List<StalkerVisionSensor>();
    private float expiresAt;
    private float nextSensorRefresh;
    private bool shuttingDown;
    private GameObject device;

    public bool IsActive => !shuttingDown;

    public void Initialize(GameObject owner, GameObject sourceDevice)
    {
        device = sourceDevice;
        expiresAt = Time.time + lifetime;
        if (vfxAnimator != null) vfxAnimator.SetBool("IsVisible", true);
        if (bodyAnimator != null) bodyAnimator.SetBool("IsMoving", true);
    }

    private void Start()
    {
        if (expiresAt <= 0f) expiresAt = Time.time + lifetime;
        if (vfxAnimator != null) vfxAnimator.SetBool("IsVisible", true);
    }

    private void Update()
    {
        if (shuttingDown) return;
        if (Time.time >= expiresAt)
        {
            BeginShutdown();
            return;
        }

        StalkerController[] stalkers = FindObjectsByType<StalkerController>();
        foreach (StalkerController stalker in stalkers)
        {
            if (stalker != null && Vector3.Distance(stalker.transform.position, transform.position) <= stalkerShutdownDistance)
            {
                BeginShutdown();
                return;
            }
        }

        if (Time.time >= nextSensorRefresh)
        {
            nextSensorRefresh = Time.time + sensorRefreshInterval;
            RefreshStalkerVisionClaims();
        }
    }

    private void RefreshStalkerVisionClaims()
    {
        StalkerVisionSensor[] sensors = FindObjectsByType<StalkerVisionSensor>();
        foreach (StalkerVisionSensor sensor in sensors)
        {
            if (sensor == null) continue;
            bool visible = sensor.TryEvaluateCandidate(transform, out _);
            if (visible && sensor.TrySetTemporaryCandidate(transform, this))
            {
                if (!claimedSensors.Contains(sensor)) claimedSensors.Add(sensor);
            }
            else if (!visible)
            {
                sensor.ClearTemporaryCandidate(this);
                claimedSensors.Remove(sensor);
            }
        }
    }

    public void BeginShutdown()
    {
        if (shuttingDown) return;
        shuttingDown = true;
        ReleaseSensors();
        if (vfxAnimator != null) vfxAnimator.SetBool("IsVisible", false);
        if (bodyAnimator != null) bodyAnimator.SetBool("IsMoving", false);
        StartCoroutine(DestroyAfterDissolve());
    }

    private IEnumerator DestroyAfterDissolve()
    {
        yield return new WaitForSeconds(dissolveDuration);
        if (device != null) Destroy(device);
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        ReleaseSensors();
    }

    private void ReleaseSensors()
    {
        foreach (StalkerVisionSensor sensor in claimedSensors)
            if (sensor != null) sensor.ClearTemporaryCandidate(this);
        claimedSensors.Clear();
    }
}
