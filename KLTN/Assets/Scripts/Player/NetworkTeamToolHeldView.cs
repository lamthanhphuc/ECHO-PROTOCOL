using EchoProtocol.Networking;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NetworkTeamToolHeldView : MonoBehaviour
{
    [SerializeField] private LobbyPlayerState lobbyState;
    [SerializeField] private PlayerHeldItemAnchor heldItemAnchor;

    [Header("Tool Visuals (gán prefab trong Inspector)")]
    [SerializeField] private GameObject toolVisual_1; // FIELD_SCANNER – giữ placeholder nếu chưa có prefab
    [SerializeField] private GameObject toolVisual_2; // NOISE_MAKER – DistressBeaconClosed
    [SerializeField] private GameObject toolVisual_3; // FIRST_AID_KIT – FirstAidKit_Red
    [SerializeField] private GameObject toolVisual_4; // DOOR_JAMMER – giữ placeholder
    [SerializeField] private GameObject toolVisual_6; // CORE_STABILIZER – PF_CoreStabilizer_Device_Animated

    [Header("Fallback Placeholder Size")]
    [SerializeField] private Vector3 localPosition = new Vector3(0.04f, 0.02f, 0.12f);
    [SerializeField] private Vector3 localEulerAngles = new Vector3(0f, 90f, -18f);
    [SerializeField] private Vector3 localScale = new Vector3(0.12f, 0.22f, 0.34f);
    [Header("Field Scanner Transform")]
    [SerializeField] private Vector3 fieldScannerLocalPosition = new Vector3(-0.003f, 0.291f, 0.118f);
    [SerializeField] private Vector3 fieldScannerLocalEulerAngles = new Vector3(184.192f, 99.672f, -5.550995f);
    [SerializeField] private Vector3 fieldScannerLocalScale = new Vector3(3f, 3f, 3f);
    [SerializeField] private Vector3 fieldScannerChildLocalPosition = new Vector3(0f, 0.04f, 0f);
    [SerializeField] private Vector3 fieldScannerChildLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 fieldScannerChildLocalScale = Vector3.one;
    [SerializeField] private Vector3 noiseMakerLocalPosition = new Vector3(0.018f, 0.132f, -0.065f);
    [SerializeField] private Vector3 noiseMakerLocalEulerAngles = new Vector3(6.176f, 93.2f, 94.562f);
    [SerializeField] private Vector3 noiseMakerLocalScale = new Vector3(0.7f, 0.7f, 0.7f);
    [SerializeField] private Vector3 firstAidLocalPosition = new Vector3(0.035f, -0.015f, 0.155f);
    [SerializeField] private Vector3 firstAidLocalEulerAngles = new Vector3(8f, 92f, 170f);
    [SerializeField] private Vector3 firstAidLocalScale = new Vector3(0.15f, 0.15f, 0.3f);
    [SerializeField] private Vector3 firstAidChildLocalPosition = new Vector3(-0.533528f, -3.405526f, -0.3114559f);
    [SerializeField] private Vector3 firstAidChildLocalEulerAngles = new Vector3(0.12f, -0.416f, 5.923f);
    [SerializeField] private Vector3 firstAidChildLocalScale = Vector3.one;
    [SerializeField] private Vector3 plankLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 plankLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 plankLocalScale = Vector3.one;
    [SerializeField] private Vector3 plankChildLocalPosition = new Vector3(0.0151f, 0.0386f, -0.0076f);
    [SerializeField] private Vector3 plankChildLocalEulerAngles = new Vector3(90f, 0f, 0f);
    [SerializeField] private Vector3 plankChildLocalScale = new Vector3(22.23983f, 0.5456054f, 2.985957f);
    [Header("Core Stabilizer Transform")]
    [SerializeField] private Vector3 coreStabilizerLocalPosition = new Vector3(0.035f, 0.02f, 0.12f);
    [SerializeField] private Vector3 coreStabilizerLocalEulerAngles = new Vector3(10f, 90f, -15f);
    [SerializeField] private Vector3 coreStabilizerLocalScale = new Vector3(0.45f, 0.45f, 0.45f);

    private GameObject _visual;
    private int _shownToolId;
    private NetworkObject _networkObject;
    private PlayerInventory _localInventory;
    private PlayerHeldItemView _localHeldItemView;

    private void Awake()
    {
        if (lobbyState == null) lobbyState = GetComponentInParent<LobbyPlayerState>();
        if (heldItemAnchor == null) heldItemAnchor = GetComponentInParent<PlayerHeldItemAnchor>();
        if (heldItemAnchor == null) heldItemAnchor = gameObject.AddComponent<PlayerHeldItemAnchor>();
        _networkObject = GetComponentInParent<NetworkObject>();
        _localInventory = GetComponentInParent<PlayerInventory>();
        _localHeldItemView = GetComponentInParent<PlayerHeldItemView>();
    }

    private void OnEnable()
    {
        LobbyPlayerState.AnyStateChanged += Refresh;
        if (_localInventory != null) _localInventory.InventoryChanged += Refresh;
        // Không gọi Refresh() ngay ở đây — chờ AnyStateChanged từ Spawned()
        // để tránh truy cập [Networked] property trước khi Fusion khởi tạo
    }

    private void OnDisable()
    {
        LobbyPlayerState.AnyStateChanged -= Refresh;
        if (_localInventory != null) _localInventory.InventoryChanged -= Refresh;
        Clear();
    }

    private void Refresh()
    {
        if (lobbyState == null) lobbyState = GetComponentInParent<LobbyPlayerState>();
        if (lobbyState == null)
        {
            Clear();
            return;
        }

        // Guard: chỉ truy cập [Networked] property sau khi Fusion đã gọi Spawned()
        if (lobbyState.Object == null || !lobbyState.Object.IsValid)
        {
            return;
        }

        int toolId = lobbyState.CarriedCoreId.IsValid ? 0 : lobbyState.ToolId;
        if (ShouldSuppressLocalNetworkToolView(toolId))
        {
            toolId = 0;
        }
        if (toolId == _shownToolId)
        {
            return;
        }

        Clear();
        _shownToolId = toolId;
        if (_shownToolId == 0)
        {
            return;
        }

        Transform anchor = heldItemAnchor != null ? heldItemAnchor.RightHandAnchor : PlayerHeldItemAnchor.ResolveRightHandAnchor(gameObject);
        if (anchor == null)
        {
            return;
        }

        GameObject sourcePrefab = ResolveToolPrefab(_shownToolId);
        if (sourcePrefab != null)
        {
            // Instantiate prefab thực
            _visual = Instantiate(sourcePrefab, anchor);
            _visual.name = "Held_TeamTool_" + _shownToolId;
            _visual.transform.localPosition = ResolveToolPosition(_shownToolId);
            _visual.transform.localRotation = Quaternion.Euler(ResolveToolEulerAngles(_shownToolId));
            _visual.transform.localScale = ResolveToolScale(_shownToolId);

            // Tắt tất cả collider trên held visual để không ảnh hưởng gameplay
            foreach (var c in _visual.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            foreach (var audio in _visual.GetComponentsInChildren<AudioSource>(true))
                audio.enabled = false;
            foreach (var pickup in _visual.GetComponentsInChildren<EchoProtocol.Tools.Scanner.NetworkToolPickup>(true))
                pickup.enabled = false;
            foreach (var netObj in _visual.GetComponentsInChildren<NetworkObject>(true))
                Destroy(netObj);

            if (_shownToolId == 1)
            {
                Transform childVisual = _visual.transform.Find("Visual");
                if (childVisual == null && _visual.transform.childCount > 0)
                {
                    childVisual = _visual.transform.GetChild(0);
                }
                if (childVisual != null)
                {
                    childVisual.localPosition = fieldScannerChildLocalPosition;
                    childVisual.localRotation = Quaternion.Euler(fieldScannerChildLocalEulerAngles);
                    childVisual.localScale = fieldScannerChildLocalScale;
                }
            }
            else if (_shownToolId == 3)
            {
                Transform childVisual = _visual.transform.Find("Visual");
                if (childVisual == null && _visual.transform.childCount > 0)
                {
                    childVisual = _visual.transform.GetChild(0);
                }
                if (childVisual != null)
                {
                    childVisual.localPosition = firstAidChildLocalPosition;
                    childVisual.localRotation = Quaternion.Euler(firstAidChildLocalEulerAngles);
                    childVisual.localScale = firstAidChildLocalScale;
                }
            }
            else if (_shownToolId == 4)
            {
                Transform childVisual = _visual.transform.Find("Visual");
                if (childVisual == null && _visual.transform.childCount > 0)
                {
                    childVisual = _visual.transform.GetChild(0);
                }
                if (childVisual != null)
                {
                    childVisual.localPosition = plankChildLocalPosition;
                    childVisual.localRotation = Quaternion.Euler(plankChildLocalEulerAngles);
                    childVisual.localScale = plankChildLocalScale;
                }
            }
        }
        else
        {
            // Fallback: cube màu như cũ
            _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _visual.name = "Held_TeamTool_" + _shownToolId;
            _visual.transform.SetParent(anchor, false);
            _visual.transform.localPosition = ResolveToolPosition(_shownToolId);
            _visual.transform.localRotation = Quaternion.Euler(ResolveToolEulerAngles(_shownToolId));
            _visual.transform.localScale = ResolveToolScale(_shownToolId);

            Collider visualCollider = _visual.GetComponent<Collider>();
            if (visualCollider != null) visualCollider.enabled = false;

            Renderer renderer = _visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                renderer.material.color = ResolveToolColor(_shownToolId);
            }
        }
    }

    private GameObject ResolveToolPrefab(int toolId)
    {
        switch (toolId)
        {
            case 1: return toolVisual_1;
            case 2: return toolVisual_2;
            case 3: return toolVisual_3;
            case 4: return toolVisual_4;
            case 6: return toolVisual_6;
            default: return null;
        }
    }

    private Vector3 ResolveToolPosition(int toolId)
    {
        switch (toolId)
        {
            case 1: return fieldScannerLocalPosition;
            case 2: return noiseMakerLocalPosition;
            case 3: return firstAidLocalPosition;
            case 4: return plankLocalPosition;
            case 6: return coreStabilizerLocalPosition;
            default: return localPosition;
        }
    }

    private Vector3 ResolveToolEulerAngles(int toolId)
    {
        switch (toolId)
        {
            case 1: return fieldScannerLocalEulerAngles;
            case 2: return noiseMakerLocalEulerAngles;
            case 3: return firstAidLocalEulerAngles;
            case 4: return plankLocalEulerAngles;
            case 6: return coreStabilizerLocalEulerAngles;
            default: return localEulerAngles;
        }
    }

    private Vector3 ResolveToolScale(int toolId)
    {
        switch (toolId)
        {
            case 1: return fieldScannerLocalScale;
            case 2: return noiseMakerLocalScale;
            case 3: return firstAidLocalScale;
            case 4: return plankLocalScale;
            case 6: return coreStabilizerLocalScale;
            default: return localScale;
        }
    }

    private bool ShouldSuppressLocalNetworkToolView(int toolId)
    {
        if (toolId == 0)
        {
            return false;
        }

        if (_localHeldItemView != null && _localHeldItemView.IsShowingTeamTool)
        {
            return true;
        }

        return _networkObject != null
            && _networkObject.IsValid
            && _networkObject.HasInputAuthority;
    }

    private static Color ResolveToolColor(int toolId)
    {
        switch (toolId)
        {
            case 1: return new Color(0.35f, 0.85f, 1f);
            case 2: return new Color(1f, 0.74f, 0.28f);
            case 3: return new Color(0.45f, 1f, 0.55f);
            case 4: return new Color(1f, 0.42f, 0.48f);
            case 6: return new Color(0.15f, 0.65f, 1f);
            default: return Color.white;
        }
    }

    private void Clear()
    {
        if (_visual != null)
        {
            Destroy(_visual);
        }

        _visual = null;
        _shownToolId = 0;
    }
}
