using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerFirstPersonWristCuffs : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Color cuffColor = new Color(0.02f, 0.025f, 0.03f, 1f);
    [SerializeField] private Vector3 leftLocalPosition = new Vector3(0f, 0.015f, 0f);
    [SerializeField] private Vector3 rightLocalPosition = new Vector3(0f, 0.015f, 0f);
    [SerializeField] private Vector3 localEulerAngles = new Vector3(90f, 0f, 0f);
    [SerializeField] private Vector3 localScale = new Vector3(0.055f, 0.018f, 0.055f);

    private NetworkObject _networkObject;
    private Renderer[] _cuffRenderers;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        _networkObject = GetComponentInParent<NetworkObject>();
        EnsureCuffs();
    }

    private void LateUpdate()
    {
        bool visible = IsLocalView();
        if (_cuffRenderers == null)
        {
            return;
        }

        foreach (Renderer cuffRenderer in _cuffRenderers)
        {
            if (cuffRenderer != null && cuffRenderer.enabled != visible)
            {
                cuffRenderer.enabled = visible;
            }
        }
    }

    private void EnsureCuffs()
    {
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        Renderer left = EnsureCuff(
            "FirstPersonWristCuff_Left",
            animator.GetBoneTransform(HumanBodyBones.LeftHand),
            leftLocalPosition);
        Renderer right = EnsureCuff(
            "FirstPersonWristCuff_Right",
            animator.GetBoneTransform(HumanBodyBones.RightHand),
            rightLocalPosition);
        _cuffRenderers = new[] { left, right };
    }

    private Renderer EnsureCuff(string cuffName, Transform parent, Vector3 localPosition)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find(cuffName);
        GameObject cuff = existing != null
            ? existing.gameObject
            : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cuff.name = cuffName;
        cuff.transform.SetParent(parent, false);
        cuff.transform.localPosition = localPosition;
        cuff.transform.localRotation = Quaternion.Euler(localEulerAngles);
        cuff.transform.localScale = localScale;

        Collider collider = cuff.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Renderer renderer = cuff.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateCuffMaterial();
            renderer.enabled = false;
        }

        return renderer;
    }

    private Material CreateCuffMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = cuffColor;
        return material;
    }

    private bool IsLocalView()
    {
        if (_networkObject != null && _networkObject.IsValid)
        {
            return _networkObject.HasInputAuthority;
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
}
