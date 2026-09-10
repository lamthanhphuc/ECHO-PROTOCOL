using System.Collections;
using UnityEngine;

public sealed class MotionDecoyTeamTool : MonoBehaviour, ITeamToolGameplay
{
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(1f)] private float throwSpeed = 9f;
    [SerializeField, Min(0f)] private float upwardSpeed = 2.2f;
    [SerializeField, Min(0f)] private float cooldown = 10f;

    private GameObject owner;
    private Transform aimOrigin;
    private float readyTime;
    private bool deploying;

    public float CooldownRemaining => Mathf.Max(0f, readyTime - Time.time);

    public void Equip(GameObject newOwner, Transform newAimOrigin)
    {
        owner = newOwner;
        aimOrigin = newAimOrigin;
    }

    public void Unequip()
    {
        owner = null;
        aimOrigin = null;
    }

    public bool TryUse()
    {
        if (deploying || projectilePrefab == null || Time.time < readyTime) return false;
        PlayerDownState life = owner != null ? owner.GetComponentInParent<PlayerDownState>() : null;
        if (life != null && !life.IsActive) return false;
        StartCoroutine(ThrowRoutine());
        return true;
    }

    private IEnumerator ThrowRoutine()
    {
        deploying = true;
        if (animator != null) animator.SetBool("IsDeployed", true);
        yield return new WaitForSeconds(0.28f);

        Transform origin = aimOrigin != null ? aimOrigin : transform;
        Vector3 position = origin.position + origin.forward * 0.65f - origin.up * 0.12f;
        GameObject projectile = Instantiate(projectilePrefab, position, Quaternion.LookRotation(origin.forward));
        MotionDecoyProjectile behaviour = projectile.GetComponent<MotionDecoyProjectile>();
        if (behaviour != null) behaviour.Launch(origin.forward * throwSpeed + Vector3.up * upwardSpeed, owner);

        readyTime = Time.time + cooldown;
        yield return new WaitForSeconds(0.38f);
        if (animator != null) animator.SetBool("IsDeployed", false);
        deploying = false;
    }
}
