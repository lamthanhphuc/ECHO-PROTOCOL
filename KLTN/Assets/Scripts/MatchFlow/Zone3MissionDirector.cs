using System.Collections.Generic;
using EchoProtocol.Gameplay;
using EchoProtocol.Visuals;
using Fusion;
using QuickOutline;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    // Binds the existing SciFi room and prop instances without changing their prefab assets.
    public sealed class Zone3MissionDirector : MonoBehaviour
    {
        private const string SceneName = "SciFi";
        [SerializeField, Min(1f)] private float pushInteractionDistance = 5f;
        [SerializeField, Min(1f)] private float chargeInteractionDistance = 4f;
        [SerializeField, Min(1f)] private float exitInteractionDistance = 4f;
        [SerializeField, Min(1f)] private float outlineVisibleDistance = 30f;

        private Zone3DockArea _dockArea;
        private Zone3ChargeStation _chargeStation;
        private Transform _exit;
        private Outline _frigateOutline;
        private Outline _exitOutline;
        private readonly Dictionary<PlayerRef, GameObject> _authoritativePushers = new Dictionary<PlayerRef, GameObject>();
        private readonly List<PlayerRef> _invalidPusherBuffer = new List<PlayerRef>();
        private MatchFlowController _offlineFlow;
        private readonly HashSet<PlayerDownState> _offlineEscaped = new HashSet<PlayerDownState>();

        public static Zone3MissionDirector Instance { get; private set; }
        public static bool IsSciFiSceneLoaded => SceneManager.GetSceneByName(SceneName).isLoaded;
        public PushableObject Frigate { get; private set; }
        public Zone3ConvoyController Convoy { get; private set; }
        public Zone3ChargeStation ChargeStation => _chargeStation;
        public Vector3 FrigatePosition => Convoy != null ? Convoy.transform.position : Frigate != null ? Frigate.transform.position : Vector3.zero;
        public Vector3 ExitPosition => _exit != null ? _exit.position : Vector3.zero;
        public Transform ExitTransform => _exit;
        public float PushInteractionDistance => pushInteractionDistance;
        public float ExitInteractionDistance => exitInteractionDistance;
        public bool IsFrigateAtDestination
        {
            get
            {
                if (Frigate == null || _dockArea == null) return false;
                if (Convoy != null && Convoy.CurrentPoint == Zone3ConvoyRoutePoint.Final) return true;
                return _dockArea.FullyContains(Frigate.GetComponent<Collider>());
            }
        }

        public bool IsPushAvailable
        {
            get
            {
                var match = NetworkMatchState.Instance;
                if (match != null && match.Object != null && match.Object.IsValid)
                    return !match.IsEnded && (match.CurrentPhase == NetworkMatchPhase.Zone3FindFrigate
                        || match.CurrentPhase == NetworkMatchPhase.Zone3PushFrigate);
                return _offlineFlow != null && (_offlineFlow.Phase == MatchPhase.Zone3FindFrigate
                    || _offlineFlow.Phase == MatchPhase.Zone3PushFrigate);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneBinding()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureSceneBinding(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode _) => EnsureSceneBinding(scene);

        private static void EnsureSceneBinding(Scene scene)
        {
            if (scene.name != SceneName || Instance != null) return;
            var frigate = GameObject.Find("Spacefrigate");
            var covey = GameObject.Find("06_Covey");
            var exit = GameObject.Find("Doorexit");
            if (frigate == null || covey == null || exit == null)
            {
                Debug.LogError("[Zone3] SciFi requires Spacefrigate, 06_Covey and Doorexit.");
                return;
            }
            var owner = GameObject.Find("GameMode_ResearchFacility");
            if (owner == null) owner = new GameObject("GameMode_ResearchFacility");
            var director = owner.GetComponent<Zone3MissionDirector>();
            if (director == null) director = owner.AddComponent<Zone3MissionDirector>();
            director.Bind(frigate, covey.transform, exit.transform);
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Bind(GameObject frigate, Transform covey, Transform exit)
        {
            Frigate = frigate.GetComponent<PushableObject>();
            Convoy = frigate.GetComponent<Zone3ConvoyController>();
            if (Convoy == null) Convoy = frigate.AddComponent<Zone3ConvoyController>();
            Convoy.BindForZone3();
            _exit = exit;
            _dockArea = covey.GetComponentInChildren<Zone3DockArea>(true)
                ?? GameObject.Find("Zone3_DockArea")?.GetComponent<Zone3DockArea>();
            _chargeStation = covey.GetComponentInChildren<Zone3ChargeStation>(true)
                ?? GameObject.Find("PF_ScifiCharge")?.GetComponent<Zone3ChargeStation>();
            if (_dockArea == null || _chargeStation == null)
                Debug.LogError("[Zone3] 06_Covey requires Zone3_DockArea and PF_ScifiCharge.", covey);
            _offlineFlow = GetComponent<MatchFlowController>() ?? FindAnyObjectByType<MatchFlowController>();
            if (Frigate == null)
            {
                Debug.LogError("[Zone3] Scene Spacefrigate has no PushableObject.", frigate);
            }
            _dockArea?.MatchShipFootprint(frigate.GetComponent<Collider>());
            _frigateOutline = GetOrAddOutline(frigate);
            _exitOutline = GetOrAddOutline(exit.gameObject);
            var doorRoot = exit.parent != null ? exit.parent.gameObject : exit.gameObject;
            if (doorRoot.GetComponent<Zone3ExitDoor>() == null)
                doorRoot.AddComponent<Zone3ExitDoor>();
            _frigateOutline.enabled = false;
            _exitOutline.enabled = false;
            var presentation = GetComponent<Zone3FacilityPresentation>();
            if (presentation == null) presentation = gameObject.AddComponent<Zone3FacilityPresentation>();
            presentation.Bind(covey, exit);
        }

        private static Outline GetOrAddOutline(GameObject target)
        {
            var outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.OutlineColor = new Color(0.1f, 0.95f, 1f);
            outline.OutlineWidth = 4f;
            return outline;
        }

        private void LateUpdate()
        {
            if (Frigate == null || _dockArea == null || _chargeStation == null || _exit == null) return;
            var match = NetworkMatchState.Instance;
            bool online = match != null && match.Object != null && match.Object.IsValid;
            var networkPhase = online ? match.CurrentPhase : default;
            var offlinePhase = _offlineFlow != null ? _offlineFlow.Phase : MatchPhase.ExploreCore;
            bool find = online ? networkPhase == NetworkMatchPhase.Zone3FindFrigate
                : offlinePhase == MatchPhase.Zone3FindFrigate;
            bool push = online ? networkPhase == NetworkMatchPhase.Zone3PushFrigate
                : offlinePhase == MatchPhase.Zone3PushFrigate;
            bool hunt = online ? networkPhase == NetworkMatchPhase.FinalHunt
                    || networkPhase == NetworkMatchPhase.Escape
                : offlinePhase == MatchPhase.FinalHunt
                    || offlinePhase == MatchPhase.ExitCountdown;

            SetOutline(_frigateOutline, find ? Outline.Mode.OutlineAll : Outline.Mode.OutlineVisible,
                find || (push && IsOutlineWithinDistance(Frigate.transform)));
            SetOutline(_exitOutline, Outline.Mode.OutlineVisible,
                hunt && IsOutlineWithinDistance(_exit));
            _dockArea.SetVisualState(push, push && IsFrigateAtDestination);

            if (online && !match.Object.HasStateAuthority
                && match.Zone3FrigatePosition.sqrMagnitude > 1f)
            {
                if (Convoy != null) Convoy.ApplyReplicatedPose(match.Zone3FrigatePosition, match.Zone3FrigateRotation);
                else Frigate.transform.SetPositionAndRotation(match.Zone3FrigatePosition, match.Zone3FrigateRotation);
            }
            if (!online && push) Convoy?.TickAuthoritative(Time.deltaTime);
            if (!online && _offlineEscaped.Count > 0 && _offlineFlow != null
                && _offlineFlow.Phase == MatchPhase.FinalHunt)
                CheckOfflineExitCompletion();
        }

        public void NotifyOfflinePushStarted() => _offlineFlow?.NotifyZone3PushStarted();

        public bool CanActivateCharge(GameObject player)
        {
            if (player == null || _chargeStation == null || !IsFrigateAtDestination
                || !IsPlayerNearCharge(player.transform.position)) return false;
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
                return !match.IsEnded && match.CurrentPhase == NetworkMatchPhase.Zone3PushFrigate
                    && player.GetComponentInParent<NetworkPlayerLifeState>()?.Status == NetworkPlayerLifeStatus.Alive;
            return _offlineFlow != null && _offlineFlow.Phase == MatchPhase.Zone3PushFrigate
                && player.GetComponentInParent<PlayerDownState>()?.IsActive == true;
        }

        public bool IsPlayerNearCharge(Vector3 position)
        {
            var collider = _chargeStation != null ? _chargeStation.GetComponent<Collider>() : null;
            return collider != null && Vector3.Distance(position, collider.ClosestPoint(position))
                <= chargeInteractionDistance;
        }

        public void ActivateCharge(GameObject player)
        {
            if (!CanActivateCharge(player)) return;
            var match = NetworkMatchState.Instance;
            if (match == null || match.Object == null || !match.Object.IsValid)
                _offlineFlow?.NotifyZone3Complete();
        }

        private static void SetOutline(Outline outline, Outline.Mode mode, bool visible)
        {
            if (outline.OutlineMode != mode) outline.OutlineMode = mode;
            if (outline.enabled != visible) outline.enabled = visible;
        }

        private bool IsOutlineWithinDistance(Transform target)
        {
            if (target == null) return false;
            Transform viewer = Camera.main != null ? Camera.main.transform : ObjectiveGlowHighlight.GetLocalPlayerTransform();
            if (viewer == null) return true;

            float maxDistance = outlineVisibleDistance * outlineVisibleDistance;
            return (viewer.position - target.position).sqrMagnitude <= maxDistance;
        }

        public void StartAuthoritativePush(PlayerRef actor, GameObject player)
        {
            if (player == null) return;
            Convoy?.Activate();
        }

        public void StopAuthoritativePush(PlayerRef actor)
        {
            if (!_authoritativePushers.TryGetValue(actor, out var player)) return;
            Frigate?.EndAuthoritativePush(player);
            _authoritativePushers.Remove(actor);
        }

        public void StopAllAuthoritativePushers()
        {
            foreach (var player in _authoritativePushers.Values)
            {
                Frigate?.EndAuthoritativePush(player);
            }
            _authoritativePushers.Clear();
        }

        public bool SelectConvoyRoute(Zone3ConvoyRoutePoint nextPoint) =>
            Convoy != null && Convoy.SelectNextPoint(nextPoint);

        public void TickConvoyAuthoritative(float deltaTime) =>
            Convoy?.TickAuthoritative(deltaTime);

        public void ReleaseInvalidAuthoritativePushers(System.Func<PlayerRef, bool> isValid)
        {
            if (isValid == null || _authoritativePushers.Count == 0) return;
            _invalidPusherBuffer.Clear();
            foreach (var actor in _authoritativePushers.Keys)
            {
                if (!isValid(actor)) _invalidPusherBuffer.Add(actor);
            }
            for (int i = 0; i < _invalidPusherBuffer.Count; i++)
            {
                StopAuthoritativePush(_invalidPusherBuffer[i]);
            }
            _invalidPusherBuffer.Clear();
        }

        public bool CanExit(GameObject player)
        {
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
                return player != null && !match.IsEnded
                    && player.GetComponentInParent<NetworkPlayerLifeState>()?.Status == NetworkPlayerLifeStatus.Alive
                    && (match.CurrentPhase == NetworkMatchPhase.FinalHunt
                        || match.CurrentPhase == NetworkMatchPhase.Escape);
            return _offlineFlow != null && !(_offlineFlow.IsMatchEnded)
                && (_offlineFlow.Phase == MatchPhase.FinalHunt
                    || _offlineFlow.Phase == MatchPhase.ExitCountdown)
                && player != null && player.GetComponentInParent<PlayerDownState>()?.IsActive == true
                && !_offlineEscaped.Contains(player.GetComponentInParent<PlayerDownState>());
        }

        public void RegisterExit(GameObject player)
        {
            if (!CanExit(player)) return;
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
            {
                match.RequestZone3Exit();
                return;
            }
            var state = player.GetComponentInParent<PlayerDownState>();
            if (state == null || !_offlineEscaped.Add(state)) return;
            CheckOfflineExitCompletion();
        }

        private void CheckOfflineExitCompletion()
        {
            var players = FindObjectsByType<PlayerDownState>(FindObjectsInactive.Exclude);
            foreach (var candidate in players)
                if (candidate.IsActive && !_offlineEscaped.Contains(candidate)) return;
            _offlineFlow.WinMatch();
        }

    }

    public sealed class Zone3ExitDoor : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => "EXIT ONLINE - ESCAPE";
        public bool CanInteract(GameObject interactor) => Zone3MissionDirector.Instance?.CanExit(interactor) == true;
        public void Interact(GameObject interactor) => Zone3MissionDirector.Instance?.RegisterExit(interactor);
    }
}
