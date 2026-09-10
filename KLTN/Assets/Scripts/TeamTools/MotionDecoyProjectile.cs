using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public sealed class MotionDecoyProjectile : MonoBehaviour
{
    [SerializeField] private Rigidbody body;
    [SerializeField] private Collider bodyCollider;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform projectionOrigin;
    [SerializeField] private GameObject hologramPrefab;
    [SerializeField, Min(0.05f)] private float settleSpeed = 0.8f;
    [SerializeField, Min(0.05f)] private float minimumFlightTime = 0.12f;
    [SerializeField, Min(1f)] private float failSafeLifetime = 18f;

    private float launchedAt;
    private bool launched;
    private bool deployed;
    private GameObject owner;

    private void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider>();
    }

    public void Launch(Vector3 velocity, GameObject newOwner)
    {
        owner = newOwner;
        launched = true;
        launchedAt = Time.time;
        body.isKinematic = false;
        body.linearVelocity = velocity;
        body.angularVelocity = Random.onUnitSphere * 5f;
        Destroy(gameObject, failSafeLifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!launched || deployed || Time.time - launchedAt < minimumFlightTime) return;
        if (collision.contactCount == 0 || collision.GetContact(0).normal.y < 0.35f) return;
        Deploy();
    }

    private void FixedUpdate()
    {
        if (!launched || deployed || Time.time - launchedAt < 0.4f) return;
        if (body.linearVelocity.sqrMagnitude <= settleSpeed * settleSpeed) Deploy();
    }

    private void Deploy()
    {
        deployed = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (animator != null) animator.SetBool("IsDeployed", true);

        Transform origin = projectionOrigin != null ? projectionOrigin : transform;
        if (hologramPrefab != null)
        {
            GameObject hologram = Instantiate(hologramPrefab, origin.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            MotionDecoyHologram runtime = hologram.GetComponent<MotionDecoyHologram>();
            if (runtime != null) runtime.Initialize(owner, gameObject);
        }
    }
}
