using System.Collections.Generic;
using UnityEngine;

public sealed class CoreStabilizerTeamTool : MonoBehaviour, ITeamToolGameplay
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform supportFieldVfx;
    [SerializeField, Min(0.5f)] private float supportRadius = 2.5f;
    [SerializeField, Min(0.02f)] private float scanInterval = 0.2f;
    [SerializeField] private LayerMask playerMask = ~0;
    [SerializeField] private bool activeOnEquip;

    private readonly HashSet<PlayerEnergyCoreCarrier> affected = new HashSet<PlayerEnergyCoreCarrier>();
    private readonly Collider[] scanResults = new Collider[32];
    private GameObject owner;
    private PlayerDownState ownerLife;
    private float nextScan;
    private bool isActive;

    public bool IsActive => isActive;
    public float SupportRadius => supportRadius;

    public void Equip(GameObject newOwner, Transform aimOrigin)
    {
        owner = newOwner;
        ownerLife = owner != null ? owner.GetComponentInParent<PlayerDownState>() : null;
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
        if (ownerLife != null && !ownerLife.IsActive) return false;
        SetActive(!isActive);
        return true;
    }

    private void Update()
    {
        PositionFieldAtOwnerFeet();
        if (!isActive) return;
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
        if (isActive == value) return;
        isActive = value;
        if (animator != null) animator.SetBool("IsActive", value);
        if (!value) ClearAffectedCarriers();
        else nextScan = 0f;
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
        supportFieldVfx.position = owner.transform.position + Vector3.up * 0.03f;
        supportFieldVfx.rotation = Quaternion.identity;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.65f);
        Gizmos.DrawWireSphere(owner != null ? owner.transform.position : transform.position, supportRadius);
    }
}
