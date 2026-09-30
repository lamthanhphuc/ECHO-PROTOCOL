using System.Collections.Generic;
using EchoProtocol.Gameplay;
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

        private Zone3DockArea _dockArea;
        private Zone3ChargeStation _chargeStation;
        private Transform _exit;
        private Outline _frigateOutline;
        private Outline _exitOutline;
        private PlayerRef _authoritativePusher;
        private GameObject _authoritativePusherObject;
        private MatchFlowController _offlineFlow;
        private readonly HashSet<PlayerDownState> _offlineEscaped = new HashSet<PlayerDownState>();

        public static Zone3MissionDirector Instance { get; private set; }
        public PushableObject Frigate { get; private set; }
        public Vector3 FrigatePosition => Frigate != null ? Frigate.transform.position : Vector3.zero;
        public Vector3 ExitPosition => _exit != null ? _exit.position : Vector3.zero;
        public float PushInteractionDistance => pushInteractionDistance;
        public float ExitInteractionDistance => exitInteractionDistance;
        public bool IsFrigateAtDestination
        {
            get
            {
                return Frigate != null && _dockArea != null
                    && _dockArea.FullyContains(Frigate.GetComponent<Collider>());
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
                return;
            }
            _dockArea?.MatchShipFootprint(frigate.GetComponent<Collider>());
            _frigateOutline = GetOrAddOutline(frigate);
            _exitOutline = GetOrAddOutline(exit.gameObject);
            var doorRoot = exit.parent != null ? exit.parent.gameObject : exit.gameObject;
            if (doorRoot.GetComponent<Zone3ExitDoor>() == null)
                doorRoot.AddComponent<Zone3ExitDoor>();
            _frigateOutline.enabled = false;
            _exitOutline.enabled = false;
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
            bool hunt = online ? networkPhase == NetworkMatchPhase.FinalHunt || networkPhase == NetworkMatchPhase.Escape
                : offlinePhase == MatchPhase.FinalHunt || offlinePhase == MatchPhase.ExitCountdown;

            SetOutline(_frigateOutline, find ? Outline.Mode.OutlineAll : Outline.Mode.OutlineVisible,
                find || push);
            SetOutline(_exitOutline, Outline.Mode.OutlineVisible, hunt);
            _dockArea.SetVisualState(push, push && IsFrigateAtDestination);

            if (online && !match.Object.HasStateAuthority
                && match.Zone3FrigatePosition.sqrMagnitude > 1f)
            {
                Frigate.transform.SetPositionAndRotation(match.Zone3FrigatePosition, match.Zone3FrigateRotation);
            }
            if (!online && _offlineEscaped.Count > 0 && _offlineFlow != null
                && _offlineFlow.Phase == MatchPhase.ExitCountdown)
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
            if (match != null && match.Object != null && match.Object.IsValid)
                match.RequestZone3Charge();
            else
                _offlineFlow?.NotifyZone3Complete();
        }

        private static void SetOutline(Outline outline, Outline.Mode mode, bool visible)
        {
            if (outline.OutlineMode != mode) outline.OutlineMode = mode;
            if (outline.enabled != visible) outline.enabled = visible;
        }

        public void StartAuthoritativePush(PlayerRef actor, GameObject player)
        {
            if (Frigate == null || player == null) return;
            if (_authoritativePusherObject != null && _authoritativePusherObject != player) return;
            _authoritativePusher = actor;
            _authoritativePusherObject = player;
            Frigate.BeginAuthoritativePush(player);
        }

        public void StopAuthoritativePush(PlayerRef actor)
        {
            if (_authoritativePusher != actor) return;
            Frigate?.EndAuthoritativePush(_authoritativePusherObject);
            _authoritativePusher = PlayerRef.None;
            _authoritativePusherObject = null;
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
            if (_offlineFlow.Phase == MatchPhase.FinalHunt) _offlineFlow.StartExitCountdown();
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
        public string InteractionPrompt => "Escape through Doorexit";
        public bool CanInteract(GameObject interactor) => Zone3MissionDirector.Instance?.CanExit(interactor) == true;
        public void Interact(GameObject interactor) => Zone3MissionDirector.Instance?.RegisterExit(interactor);
    }
}
