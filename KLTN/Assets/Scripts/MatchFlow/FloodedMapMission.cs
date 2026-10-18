using EchoProtocol.MatchFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    /// <summary>Scene-authored bindings for the Flooded Grounds mission sequence.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class FloodedMapMission : MonoBehaviour
    {
        public const string SceneName = "Scene_A";
        [SerializeField] private Transform escort;
        [SerializeField] private Zone3DockArea destination;
        [SerializeField] private Transform[] route;
        [SerializeField] private Collider chargeTarget;
        [SerializeField] private FloodedEscapeInteraction exitTarget;
        [SerializeField] private Animator escortAnimator;
        private bool _poseInitialized;
        [SerializeField, Min(1f)] private float escortRadius = 12f;
        [SerializeField, Min(.1f)] private float speed = 1.8f;
        [SerializeField, Min(1f)] private float chargeSeconds = 25f;
        public static FloodedMapMission Instance { get; private set; }
        public Transform Escort => escort;
        public Zone3DockArea Destination => destination;
        public Collider ChargeTarget => chargeTarget;
        public FloodedEscapeInteraction ExitTarget => exitTarget;
        public float EscortRadius => escortRadius;
        public float Speed => speed;
        public float ChargeSeconds => chargeSeconds;
        public Transform[] Route => route;
        public static bool IsLoaded => SceneManager.GetSceneByName(SceneName).isLoaded;

        public bool IsEscortInDock(Vector3 position)
        {
            var box = destination ? destination.GetComponent<BoxCollider>() : null;
            if (!box) return false;
            var p = box.transform.InverseTransformPoint(position) - box.center;
            return Mathf.Abs(p.x) <= box.size.x * .5f && Mathf.Abs(p.z) <= box.size.z * .5f
                && Mathf.Abs(p.y) <= 2f;
        }

        public static float PresentationBlend(float deltaTime) => 1f - Mathf.Exp(-12f * Mathf.Max(0f, deltaTime));

        public static bool TryDescribe(out string title, out string detail, out float progress)
        {
            title = "Vùng ngập"; detail = "Đang đồng bộ nhiệm vụ…"; progress = 0f;
            var match = NetworkMatchState.Instance;
            if (!Instance || !match || !match.Object || !match.Object.IsValid) return false;
            if (match.IsEnded)
            {
                title = match.Result == NetworkMatchResult.Win ? "Sơ tán thành công" : "Nhiệm vụ thất bại";
                detail = "Trở về phòng chờ…"; progress = match.Result == NetworkMatchResult.Win ? 1f : 0f;
            }
            else if (match.CurrentPhase == NetworkMatchPhase.CoreObjective)
            {
                match.TryGetObjectiveProgress(out int current, out int required);
                title = "Nhiệm vụ 1 · Khôi phục nguồn điện";
                detail = $"Tìm và nạp lõi vào hai trạm cấp điện: {current}/{required}";
                progress = required > 0 ? current / (float)required : 0f;
            }
            else if (match.CurrentPhase == NetworkMatchPhase.Zone2Objective)
            {
                title = "Nhiệm vụ 2 · Khôi phục an ninh";
                if (!match.SecurityTerminalDiscovered) detail = "Tìm và kiểm tra trạm an ninh";
                else if (!match.AreAllRelaysOnline) detail = $"Sửa và đồng bộ relay: {match.CompletedRelayCount}/4";
                else if (!match.SecurityHoldCompleted) detail = $"Trở về terminal · Xác thực: {Mathf.RoundToInt(match.SecurityHoldProgress01 * 100f)}%";
                else detail = $"Mã {match.PowerAuthorizationCode} · Nhập tại bảng điều khiển đĩa bay";
                progress = match.SecurityHoldCompleted ? 1f : match.CompletedRelayCount / 4f;
            }
            else if (match.CurrentPhase == NetworkMatchPhase.Zone3FindFrigate)
            {
                title = "Nhiệm vụ 3 · Cấp điện cho đĩa bay";
                detail = $"Giữ [E] tại trạm nạp: {Mathf.RoundToInt(match.Zone3ChargeAccumulatedSeconds / Instance.chargeSeconds * 100f)}%";
                progress = match.Zone3ChargeAccumulatedSeconds / Instance.chargeSeconds;
            }
            else if (match.CurrentPhase == NetworkMatchPhase.Escape)
            {
                title = "Sơ tán · Lên tàu";
                detail = $"Nhấn [E] lên tàu sơ tán · Còn {Mathf.CeilToInt(match.EscapeRemainingSeconds)} giây";
                progress = 1f;
            }
            else
            {
                title = "Nhiệm vụ 3 · Hộ tống đĩa bay";
                detail = match.FloodedEscortArrived ? "Nhấn [E] lên tàu sơ tán" : "Đi gần đĩa bay để hộ tống tới bến · Không cần tiếp nhiên liệu";
                progress = Instance.route.Length > 0 ? match.FloodedRouteIndex / (float)Instance.route.Length : 0f;
            }
            return true;
        }

        private void Awake()
        {
            Instance = this;
            if (!escortAnimator && escort) escortAnimator = escort.GetComponentInChildren<Animator>(true);
            if (escortAnimator) escortAnimator.enabled = false;
            if (destination)
            {
                destination.gameObject.SetActive(true);
                destination.RefreshLine();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            _poseInitialized = false;
            if (escortAnimator) escortAnimator.enabled = false;
        }

        private void LateUpdate()
        {
            var match = NetworkMatchState.Instance;
            if (!escort || match == null || match.Object == null || !match.Object.IsValid) return;
            if (match.FloodedEscortPoseReady)
            {
                bool snap = !_poseInitialized || match.Object.HasStateAuthority
                    || (escort.position - match.Zone3FrigatePosition).sqrMagnitude > 16f;
                float blend = snap ? 1f : PresentationBlend(Time.deltaTime);
                escort.SetPositionAndRotation(Vector3.Lerp(escort.position, match.Zone3FrigatePosition, blend),
                    Quaternion.Slerp(escort.rotation, match.Zone3FrigateRotation, blend));
                _poseInitialized = true;
            }
            if (escortAnimator) escortAnimator.enabled = !match.IsEnded && match.FloodedEscortMoving;
            if (destination) destination.SetVisualState(
                match.CurrentPhase == NetworkMatchPhase.Zone3PushFrigate || match.CurrentPhase == NetworkMatchPhase.Escape,
                match.FloodedEscortArrived);
        }
    }
}
