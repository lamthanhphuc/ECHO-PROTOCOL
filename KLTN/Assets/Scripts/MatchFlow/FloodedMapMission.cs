using EchoProtocol.MatchFlow;
using EchoProtocol.Settings;
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
        private AudioSource _escortAudio;
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

        // This map has mission steps, not zone entry gates. Resolve debug destinations
        // from the actual interaction target and stand beside it instead of inside it.
        public bool TryGetDebugMissionPose(NetworkMatchPhase phase, int playerIndex, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            Component target = phase == NetworkMatchPhase.Zone2Objective
                ? Zone2MissionDirector.Instance?.SecurityTerminal
                : phase == NetworkMatchPhase.Zone3FindFrigate ? chargeTarget : null;
            if (!target) return false;
            var collider = target as Collider ?? target.GetComponentInChildren<Collider>();
            if (!collider) return false;
            return TryGetDebugApproachPose(collider, playerIndex, out position, out rotation);
        }

        public static bool TryGetDebugApproachPose(Collider target, int playerIndex, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!target || !target.enabled || !target.gameObject.activeInHierarchy) return false;
            var bounds = target.bounds;
            for (int i = 0; i < 16; i++)
            {
                float angle = (playerIndex * 2 + i) * Mathf.PI / 8f;
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var candidate = bounds.center + new Vector3(direction.x * (bounds.extents.x + 1.2f), 0f,
                    direction.z * (bounds.extents.z + 1.2f));
                candidate.y = bounds.min.y;
                if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)
                    || Mathf.Abs(hit.position.y - bounds.min.y) > 2f
                    || (target.ClosestPoint(hit.position) - hit.position).sqrMagnitude > 2.7f * 2.7f) continue;
                var facing = bounds.center - hit.position;
                facing.y = 0f;
                position = hit.position + Vector3.up * .05f;
                rotation = facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
                return true;
            }
            return false;
        }

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
            title = GameLanguage.Choose("Vùng ngập", "Flooded Grounds");
            detail = GameLanguage.Choose("Đang đồng bộ nhiệm vụ…", "Synchronizing objectives…"); progress = 0f;
            var match = NetworkMatchState.Instance;
            if (!Instance || !match || !match.Object || !match.Object.IsValid) return false;
            if (match.IsEnded)
            {
                title = match.Result == NetworkMatchResult.Win ? GameLanguage.Choose("Sơ tán thành công", "Evacuation successful")
                    : GameLanguage.Choose("Nhiệm vụ thất bại", "Mission failed");
                detail = GameLanguage.Choose("Trở về phòng chờ…", "Returning to lobby…"); progress = match.Result == NetworkMatchResult.Win ? 1f : 0f;
            }
            else if (match.CurrentPhase == NetworkMatchPhase.CoreObjective)
            {
                match.TryGetObjectiveProgress(out int current, out int required);
                title = GameLanguage.Choose("Nhiệm vụ 1 · Khôi phục nguồn điện", "Objective 1 · Restore power");
                detail = GameLanguage.Choose($"Tìm và nạp lõi vào hai trạm cấp điện: {current}/{required}", $"Find and insert cores into both power stations: {current}/{required}");
                progress = required > 0 ? current / (float)required : 0f;
            }
            else if (match.CurrentPhase == NetworkMatchPhase.Zone2Objective)
            {
                title = GameLanguage.Choose("Nhiệm vụ 2 · Khôi phục an ninh", "Objective 2 · Restore security");
                if (!match.SecurityTerminalDiscovered) detail = GameLanguage.Choose("Tìm và kiểm tra trạm an ninh", "Find and inspect the security terminal");
                else if (!match.AreAllRelaysOnline) detail = GameLanguage.Choose($"Sửa và đồng bộ relay: {match.CompletedRelayCount}/4", $"Repair and synchronize relays: {match.CompletedRelayCount}/4");
                else if (!match.SecurityHoldCompleted) detail = GameLanguage.Choose($"Trở về terminal · Xác thực: {Mathf.RoundToInt(match.SecurityHoldProgress01 * 100f)}%", $"Return to the terminal · Authentication: {Mathf.RoundToInt(match.SecurityHoldProgress01 * 100f)}%");
                else detail = GameLanguage.Choose($"Mã {match.PowerAuthorizationCode} · Nhập tại bảng điều khiển đĩa bay", $"Code {match.PowerAuthorizationCode} · Enter at the saucer control panel");
                progress = match.SecurityHoldCompleted ? 1f : match.CompletedRelayCount / 4f;
            }
            else if (match.CurrentPhase == NetworkMatchPhase.Zone3FindFrigate)
            {
                title = GameLanguage.Choose("Nhiệm vụ 3 · Cấp điện cho đĩa bay", "Objective 3 · Charge the saucer");
                detail = GameLanguage.Choose($"Giữ [E] tại trạm nạp: {Mathf.RoundToInt(match.Zone3ChargeAccumulatedSeconds / Instance.chargeSeconds * 100f)}%", $"Hold [E] at the charging station: {Mathf.RoundToInt(match.Zone3ChargeAccumulatedSeconds / Instance.chargeSeconds * 100f)}%");
                progress = match.Zone3ChargeAccumulatedSeconds / Instance.chargeSeconds;
            }
            else if (match.CurrentPhase == NetworkMatchPhase.Escape)
            {
                title = GameLanguage.Choose("Sơ tán · Lên tàu", "Evacuation · Board the saucer");
                detail = GameLanguage.Choose($"Nhấn [E] lên tàu sơ tán · Còn {Mathf.CeilToInt(match.EscapeRemainingSeconds)} giây", $"Press [E] to evacuate · {Mathf.CeilToInt(match.EscapeRemainingSeconds)} seconds remaining");
                progress = 1f;
            }
            else
            {
                title = GameLanguage.Choose("Nhiệm vụ 3 · Hộ tống đĩa bay", "Objective 3 · Escort the saucer");
                detail = match.FloodedEscortArrived ? GameLanguage.Choose("Nhấn [E] lên tàu sơ tán", "Press [E] to evacuate")
                    : GameLanguage.Choose("Đi gần đĩa bay để hộ tống tới bến · Không cần tiếp nhiên liệu", "Stay near the saucer to escort it to the dock · No refueling required");
                progress = Instance.route.Length > 0 ? match.FloodedRouteIndex / (float)Instance.route.Length : 0f;
            }
            return true;
        }

        private void Awake()
        {
            Instance = this;
            if (!escortAnimator && escort) escortAnimator = escort.GetComponentInChildren<Animator>(true);
            if (escortAnimator) escortAnimator.enabled = false;
            if (Application.isPlaying && escort)
                _escortAudio = EchoProtocol.Audio.GameAudioRuntime.CreateSource(escort.gameObject, true);
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
            if (_escortAudio != null) _escortAudio.Stop();
            _poseInitialized = false;
            if (escortAnimator) escortAnimator.enabled = false;
        }

        private void LateUpdate()
        {
            var match = NetworkMatchState.Instance;
            if (!escort || match == null || match.Object == null || !match.Object.IsValid)
            { if (_escortAudio != null) _escortAudio.Stop(); return; }
            EchoProtocol.Audio.GameAudioRuntime.Loop(_escortAudio,
                !match.IsEnded && match.FloodedEscortMoving ? "objectives/frigate_grind_loop" : null, .35f);
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
