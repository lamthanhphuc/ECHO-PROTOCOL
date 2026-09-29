using System.Collections.Generic;
using EchoProtocol.Networking;
using UnityEngine;

public sealed class CoreStabilizerTeamTool : MonoBehaviour, ITeamToolGameplay
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform supportFieldVfx;
    [SerializeField] private LineRenderer supportRing;
    [SerializeField, Min(0.5f)] private float supportRadius = CoreStabilizerRules.SupportRadius;
    [SerializeField, Min(0.02f)] private float ringWidth = 0.12f;
    [SerializeField, Range(0f, 1f)] private float ringAlpha = 0.7f;
    [SerializeField, Min(16)] private int ringSegments = 128;
    [SerializeField, Min(0.02f)] private float scanInterval = 0.2f;
    [SerializeField] private LayerMask playerMask = ~0;
    [SerializeField] private bool activeOnEquip;

    private readonly HashSet<PlayerEnergyCoreCarrier> affected = new HashSet<PlayerEnergyCoreCarrier>();
    private readonly Collider[] scanResults = new Collider[32];
    private GameObject owner;
    private PlayerDownState ownerLife;
    private float nextScan;
    private float activeUntil;
    private float cooldownUntil;
    private bool isActive;

    public bool IsActive => isActive;
    public float SupportRadius => supportRadius;

    public void Equip(GameObject newOwner, Transform aimOrigin)
    {
        owner = newOwner;
        ownerLife = owner != null ? owner.GetComponentInParent<PlayerDownState>() : null;
        ConfigureSupportFieldVisual();
        SetActive(activeOnEquip);
    }

    public void Unequip()
    {
        SetActive(false);
        owner = null;
        ownerLife = null;
    }

    public bool TryUse()
    {
        if (isActive || Time.time < cooldownUntil || (ownerLife != null && !ownerLife.IsActive)) return false;
        cooldownUntil = Time.time + CoreStabilizerRules.CooldownSeconds;
        SetActive(true);
        return true;
    }

    private void Update()
    {
        PositionFieldAtOwnerFeet();
        if (!isActive) return;
        if (Time.time >= activeUntil)
        {
            SetActive(false);
            return;
        }
        if (ownerLife != null && !ownerLife.IsActive)
        {
            SetActive(false);
            return;
        }
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + scanInterval;
            RefreshAffectedCarriers();
        }
    }

    private void OnDisable()
    {
        ClearAffectedCarriers();
    }

    public void SetActive(bool value)
    {
        if (isActive == value)
        {
            if (!value)
            {
                SetFieldVisible(false);
                SetDeviceVfxActive(false);
            }
            return;
        }
        isActive = value;
        if (animator != null) animator.SetBool("IsActive", value);
        SetFieldVisible(value);
        SetDeviceVfxActive(value);
        if (!value) ClearAffectedCarriers();
        else
        {
            activeUntil = Time.time + CoreStabilizerRules.DurationSeconds;
            nextScan = 0f;
        }
    }

    private void ConfigureSupportFieldVisual()
    {
        if (supportFieldVfx == null) return;
        if (supportRing == null) supportRing = supportFieldVfx.GetComponentInChildren<LineRenderer>(true);

        if (supportRing != null)
        {
            supportRing.useWorldSpace = false;
            supportRing.loop = true;
            supportRing.positionCount = ringSegments;
            for (int i = 0; i < ringSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ringSegments;
                supportRing.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * supportRadius, 0f, Mathf.Sin(angle) * supportRadius));
            }
            supportRing.startWidth = supportRing.endWidth = ringWidth;
            supportRing.startColor = supportRing.endColor = new Color(0.08f, 0.85f, 1f, ringAlpha);
            supportRing.sortingOrder = 5;
        }

    }

    private void SetFieldVisible(bool visible)
    {
        if (supportRing != null) supportRing.enabled = visible;
    }

    private void SetDeviceVfxActive(bool value)
    {
        if (animator == null) return;
        Transform origin = animator.transform.Find("Visual/DeviceVFXOrigin");
        if (origin == null) return;
        foreach (ParticleSystem particles in origin.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (value) particles.Play(true);
            else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void RefreshAffectedCarriers()
    {
        Vector3 center = owner != null ? owner.transform.position : transform.position;
        int count = Physics.OverlapSphereNonAlloc(center, supportRadius, scanResults, playerMask,
            QueryTriggerInteraction.Collide);
        var seen = new HashSet<PlayerEnergyCoreCarrier>();
        for (int i = 0; i < count; i++)
        {
            PlayerEnergyCoreCarrier carrier = scanResults[i] != null
                ? scanResults[i].GetComponentInParent<PlayerEnergyCoreCarrier>()
                : null;
            if (carrier == null || !carrier.IsCarrying
                || (owner != null && (carrier.gameObject == owner
                    || carrier.transform.IsChildOf(owner.transform)))) continue;
            seen.Add(carrier);
            if (affected.Add(carrier)) carrier.SetCoreStabilized(this, true);
        }

        affected.RemoveWhere(carrier =>
        {
            if (carrier != null && seen.Contains(carrier)) return false;
            if (carrier != null) carrier.SetCoreStabilized(this, false);
            return true;
        });
    }

    private void ClearAffectedCarriers()
    {
        foreach (PlayerEnergyCoreCarrier carrier in affected)
            if (carrier != null) carrier.SetCoreStabilized(this, false);
        affected.Clear();
    }

    private void PositionFieldAtOwnerFeet()
    {
        if (supportFieldVfx == null || owner == null) return;
        supportFieldVfx.position = owner.transform.position + Vector3.up * 0.08f;
        supportFieldVfx.rotation = Quaternion.identity;
        Vector3 parentScale = supportFieldVfx.parent.lossyScale;
        supportFieldVfx.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? 1f : 1f / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? 1f : 1f / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? 1f : 1f / parentScale.z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.65f);
        Gizmos.DrawWireSphere(owner != null ? owner.transform.position : transform.position, supportRadius);
    }
}
