using System.Collections.Generic;
using EchoProtocol.MatchFlow;
using EchoProtocol.Gameplay;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoProtocol.Networking
{
    [DisallowMultipleComponent]
    public sealed class Zone3ConvoyController : MonoBehaviour, IInteractable
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float normalSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float acceleration = 2.2f;
        [SerializeField, Min(0.1f)] private float deceleration = 3.5f;
        [SerializeField, Min(1f)] private float rotationSpeed = 45f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.18f;
        [SerializeField, Min(0.1f)] private float replicatedPoseLerp = 12f;
        [SerializeField, Min(0.25f)] private float replicatedPoseSnapDistance = 4f;
        [SerializeField, Min(0.1f)] private float routeMarkerScale = 0.65f;

        [Header("Escort")]
        [SerializeField, Min(1f)] private float escortRadius = 6f;
        [SerializeField, Range(0.1f, 1f)] private float soloEscortSpeedMultiplier = 0.7f;

        private readonly Dictionary<Zone3ConvoyRoutePoint, Transform> _points = new Dictionary<Zone3ConvoyRoutePoint, Transform>();
        private readonly Dictionary<Zone3ConvoyRoutePoint, Zone3RouteChoicePoint> _routeChoiceMarkers =
            new Dictionary<Zone3ConvoyRoutePoint, Zone3RouteChoicePoint>();
        private PushableObject _pushable;
        private Rigidbody _rigidbody;
        private Zone3ConvoyRoutePoint _currentPoint = Zone3ConvoyRoutePoint.Point00;
        private Zone3ConvoyRoutePoint _targetPoint = Zone3ConvoyRoutePoint.Point01;
        private float _currentSpeed;
        private float _cruiseHeight;
        private Vector3 _segmentDirection;
        private bool _initialized;
        private bool _routeLocked;
        private bool _waitingForRouteChoice;
        private bool _hasSegmentDirection;
        private bool _hasReplicatedPose;

        private enum RouteTurn
        {
            Left,
            Straight,
            Right,
            Back
        }

        public bool IsInitialized => _initialized;
        public bool IsMoving => _initialized && _routeLocked && !_waitingForRouteChoice && _currentSpeed > 0.05f;
        public bool IsWaitingForRouteChoice => _waitingForRouteChoice;
        private int _fuelPointsRemaining = Zone3FuelRules.Capacity;
        private float _fuelRestoredUntil;
        public int FuelPointsRemaining => _fuelPointsRemaining;
        public bool IsFuelEmpty => _initialized && _fuelPointsRemaining == 0;
        public float Fuel01 => _fuelPointsRemaining / (float)Zone3FuelRules.Capacity;
        public float LowFuelThreshold01 => 0.5f;
        public bool RouteLocked => _routeLocked;
        public bool WasFuelRestoredRecently => Time.unscaledTime < _fuelRestoredUntil;
        public string FuelDisplay => "FUEL " + (_fuelPointsRemaining > 0 ? "■" : "□") + " " + (_fuelPointsRemaining > 1 ? "■" : "□");
        public Zone3ConvoyRoutePoint CurrentPoint => _currentPoint;
        public Zone3ConvoyRoutePoint TargetPoint => _targetPoint;

        public string InteractionPrompt
        {
            get
            {
                if (!_initialized) return "SPACEFRIGATE\nINITIALIZE TRANSPORT";
                if (IsFuelEmpty && _currentPoint != Zone3ConvoyRoutePoint.Final) return "FUEL REQUIRED\nFIND A CONVOY FUEL CELL";
                if (_waitingForRouteChoice) return BuildRouteChoicePrompt();
                return "SPACEFRIGATE - ESCORT ACTIVE";
            }
        }

        private void Awake()
        {
            _pushable = GetComponent<PushableObject>();
            _rigidbody = GetComponent<Rigidbody>();
            ResolveRoutePoints();
        }

        private void Update()
        {
            HandleRouteChoiceInput();
        }

        public void BindForZone3()
        {
            ResolveRoutePoints();
            _cruiseHeight = transform.position.y;
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }
            if (_points.TryGetValue(Zone3ConvoyRoutePoint.Point00, out var start))
            {
                transform.SetPositionAndRotation(AtCruiseHeight(start.position), start.rotation);
            }
            RefreshRouteChoiceMarkers();
        }

        public bool CanInteract(GameObject interactor)
        {
            if (interactor == null) return false;
            var zone3 = Zone3MissionDirector.Instance;
            if (zone3 == null || !zone3.IsPushAvailable) return false;
            return Vector3.Distance(interactor.transform.position, transform.position) <= zone3.PushInteractionDistance + 1f;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
            {
                match.RequestZone3Push(true);
                return;
            }

            if (!_initialized) Activate();
        }

        public void Activate()
        {
            if (_initialized) return;
            ResolveRoutePoints();
            _initialized = true;
            _fuelPointsRemaining = Zone3FuelRules.Capacity;
            _currentPoint = FindNearestPoint();
            TryAutoSelectNextPoint();
            RefreshRouteChoiceMarkers();
        }

        public bool SelectNextPoint(Zone3ConvoyRoutePoint nextPoint)
        {
            if (!_initialized || IsFuelEmpty || !Zone3ConvoyRouteGraph.CanTravel(_currentPoint, nextPoint)) return false;
            if (!_points.ContainsKey(nextPoint)) return false;
            _targetPoint = nextPoint;
            CacheSegmentDirection(nextPoint);
            _routeLocked = true;
            _waitingForRouteChoice = false;
            RefreshRouteChoiceMarkers();
            return true;
        }

        public bool CanSelectRoute(GameObject interactor, Zone3ConvoyRoutePoint nextPoint)
        {
            if (!_initialized || IsFuelEmpty || !_waitingForRouteChoice || interactor == null) return false;
            var zone3 = Zone3MissionDirector.Instance;
            if (zone3 == null || !zone3.IsPushAvailable) return false;
            if (!Zone3ConvoyRouteGraph.CanTravel(_currentPoint, nextPoint)) return false;
            if (!_points.ContainsKey(nextPoint)) return false;
            return IsNearConvoy(interactor.transform.position);
        }

        public void RequestRouteSelection(GameObject interactor, Zone3ConvoyRoutePoint nextPoint)
        {
            if (!CanSelectRoute(interactor, nextPoint)) return;
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
            {
                match.RequestZone3ConvoyRouteChoice(nextPoint);
                return;
            }

            SelectNextPoint(nextPoint);
        }

        private void HandleRouteChoiceInput()
        {
            if (!_initialized || IsFuelEmpty || !_waitingForRouteChoice) return;
            if (!IsLocalViewerNearConvoy()) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (WasPressed(keyboard, Key.Digit1, Key.Numpad1))
                TrySelectRouteTurn(RouteTurn.Left);
            else if (WasPressed(keyboard, Key.Digit2, Key.Numpad2))
                TrySelectRouteTurn(RouteTurn.Straight);
            else if (WasPressed(keyboard, Key.Digit3, Key.Numpad3))
                TrySelectRouteTurn(RouteTurn.Right);
            else if (WasPressed(keyboard, Key.Digit4, Key.Numpad4))
                TrySelectRouteTurn(RouteTurn.Back);
        }

        private static bool WasPressed(Keyboard keyboard, Key mainKey, Key numpadKey)
        {
            return keyboard[mainKey].wasPressedThisFrame
                || keyboard[numpadKey].wasPressedThisFrame;
        }

        private bool TrySelectRouteTurn(RouteTurn turn)
        {
            if (!TryGetRoutePointForTurn(turn, out var nextPoint)) return false;

            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
            {
                match.RequestZone3ConvoyRouteChoice(nextPoint);
                return true;
            }

            return SelectNextPoint(nextPoint);
        }

        public void Refuel()
        {
            if (!_initialized || !IsFuelEmpty || _currentPoint == Zone3ConvoyRoutePoint.Final) return;
            _fuelPointsRemaining = Zone3FuelRules.Capacity;
            _fuelRestoredUntil = Time.unscaledTime + 3f;
            if (!_routeLocked) TryAutoSelectNextPoint();
            RefreshRouteChoiceMarkers();
        }

        public void ApplyReplicatedState(bool initialized, int fuel, Zone3ConvoyRoutePoint current,
            Zone3ConvoyRoutePoint target, bool routeLocked, bool waiting)
        {
            if (_initialized && _fuelPointsRemaining == 0 && fuel > 0)
                _fuelRestoredUntil = Time.unscaledTime + 3f;
            bool changed = _initialized != initialized || _fuelPointsRemaining != fuel
                || _currentPoint != current || _targetPoint != target || _routeLocked != routeLocked
                || _waitingForRouteChoice != waiting;
            _initialized = initialized;
            _fuelPointsRemaining = Mathf.Clamp(fuel, 0, Zone3FuelRules.Capacity);
            _currentPoint = current;
            _targetPoint = target;
            _routeLocked = routeLocked;
            _waitingForRouteChoice = waiting;
            if (changed) RefreshRouteChoiceMarkers();
        }

        public void TickAuthoritative(float deltaTime)
        {
            if (!_initialized || deltaTime <= 0f) return;
            if (IsFuelEmpty) { _currentSpeed = 0f; return; }
            if (!_routeLocked)
            {
                Decelerate(deltaTime);
                return;
            }

            int escortCount = CountNearbyAlivePlayers();
            float targetSpeed = escortCount <= 0 ? 0f
                : normalSpeed * (escortCount == 1 ? soloEscortSpeedMultiplier : 1f);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed,
                (targetSpeed > _currentSpeed ? acceleration : deceleration) * deltaTime);
            MoveTowardTarget(deltaTime);
        }

        public void ApplyReplicatedPose(Vector3 position, Quaternion rotation)
        {
            if (_rigidbody != null && !_rigidbody.isKinematic) _rigidbody.isKinematic = true;
            if (!_hasReplicatedPose
                || Vector3.Distance(transform.position, position) > replicatedPoseSnapDistance)
            {
                transform.SetPositionAndRotation(position, rotation);
                _hasReplicatedPose = true;
                return;
            }

            float lerp = 1f - Mathf.Exp(-replicatedPoseLerp * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, position, lerp),
                Quaternion.Slerp(transform.rotation, rotation, lerp));
        }

        private void MoveTowardTarget(float deltaTime)
        {
            if (!_points.TryGetValue(_targetPoint, out var target))
            {
                _routeLocked = false;
                return;
            }

            Vector3 position = transform.position;
            Vector3 targetPosition = AtCruiseHeight(target.position);
            Vector3 toTarget = targetPosition - position;
            float distance = toTarget.magnitude;
            if (distance <= arrivalDistance)
            {
                ArriveAtTarget(target);
                return;
            }

            Vector3 direction = _hasSegmentDirection ? _segmentDirection : toTarget / Mathf.Max(0.0001f, distance);
            float step = Mathf.Min(distance, _currentSpeed * deltaTime);
            Vector3 nextPosition = Vector3.MoveTowards(position, targetPosition, step);
            if (_rigidbody != null) _rigidbody.MovePosition(nextPosition);
            else transform.position = nextPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
                Quaternion nextRotation = Quaternion.RotateTowards(transform.rotation, desired, rotationSpeed * deltaTime);
                if (_rigidbody != null) _rigidbody.MoveRotation(nextRotation);
                else transform.rotation = nextRotation;
            }

        }

        private void ArriveAtTarget(Transform target)
        {
            Vector3 targetPosition = AtCruiseHeight(target.position);
            if (_rigidbody != null) _rigidbody.MovePosition(targetPosition);
            else transform.position = targetPosition;
            _currentSpeed = 0f;
            _currentPoint = _targetPoint;
            _fuelPointsRemaining = Zone3FuelRules.ConsumeArrival(_fuelPointsRemaining);
            _routeLocked = false;
            _hasSegmentDirection = false;
            if (_currentPoint == Zone3ConvoyRoutePoint.Final) return;
            TryAutoSelectNextPoint();
            RefreshRouteChoiceMarkers();
        }

        private void TryAutoSelectNextPoint()
        {
            var next = Zone3ConvoyRouteGraph.GetNextPoints(_currentPoint);
            if (next.Count == 1 && !IsFuelEmpty) SelectNextPoint(next[0]);
            else
            {
                _waitingForRouteChoice = next.Count > 1;
                RefreshRouteChoiceMarkers();
            }
        }

        private string BuildRouteChoicePrompt()
        {
            var options = new List<string>(4);
            if (TryGetRoutePointForTurn(RouteTurn.Left, out _)) options.Add("1 Trai");
            if (TryGetRoutePointForTurn(RouteTurn.Straight, out _)) options.Add("2 Thang");
            if (TryGetRoutePointForTurn(RouteTurn.Right, out _)) options.Add("3 Phai");
            if (TryGetRoutePointForTurn(RouteTurn.Back, out _)) options.Add("4 Lui");
            return "SPACEFRIGATE: " + string.Join(" | ", options);
        }

        private bool TryGetRoutePointForTurn(RouteTurn turn, out Zone3ConvoyRoutePoint point)
        {
            point = _currentPoint;
            var options = Zone3ConvoyRouteGraph.GetNextPoints(_currentPoint);
            if (options.Count == 0) return false;

            float bestScore = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < options.Count; i++)
            {
                var candidate = options[i];
                if (!_points.TryGetValue(candidate, out var target)) continue;
                if (!TryClassifyTurn(target.position, out var candidateTurn, out float score)) continue;
                if (candidateTurn != turn || score >= bestScore) continue;

                bestScore = score;
                point = candidate;
                found = true;
            }

            return found;
        }

        private bool TryClassifyTurn(Vector3 targetPosition, out RouteTurn turn, out float score)
        {
            turn = RouteTurn.Straight;
            score = float.PositiveInfinity;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 direction = AtCruiseHeight(targetPosition) - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f) return false;
            direction.Normalize();

            float angle = Vector3.SignedAngle(forward, direction, Vector3.up);
            float absAngle = Mathf.Abs(angle);

            if (absAngle <= 45f)
            {
                turn = RouteTurn.Straight;
                score = absAngle;
                return true;
            }

            if (absAngle >= 135f)
            {
                turn = RouteTurn.Back;
                score = 180f - absAngle;
                return true;
            }

            if (angle < 0f)
            {
                turn = RouteTurn.Left;
                score = Mathf.Abs(absAngle - 90f);
                return true;
            }

            turn = RouteTurn.Right;
            score = Mathf.Abs(absAngle - 90f);
            return true;
        }

        private bool IsLocalViewerNearConvoy()
        {
            Transform viewer = Camera.main != null ? Camera.main.transform : null;
            if (viewer == null) return false;
            return IsNearConvoy(viewer.position);
        }

        private bool IsNearConvoy(Vector3 position)
        {
            var zone3 = Zone3MissionDirector.Instance;
            float maxDistance = zone3 != null ? zone3.PushInteractionDistance + 1f : escortRadius;
            return Vector3.Distance(position, transform.position) <= maxDistance;
        }

        private int CountNearbyAlivePlayers()
        {
            int count = 0;
            // Online: use authoritative NetworkPlayerLifeState (Alive only, gameplay players only).
            var players = FindObjectsByType<NetworkPlayerLifeState>(FindObjectsInactive.Exclude);
            float radiusSqr = escortRadius * escortRadius;
            for (int i = 0; i < players.Length; i++)
            {
                var life = players[i];
                if (life == null || life.Status != NetworkPlayerLifeStatus.Alive) continue;
                var lobby = life.GetComponent<LobbyPlayerState>()
                    ?? life.GetComponentInParent<LobbyPlayerState>();
                if (lobby != null && !lobby.IsGameplayPlayer) continue;
                if ((life.transform.position - transform.position).sqrMagnitude <= radiusSqr) count++;
            }

            // Offline-only fallback: PlayerDownState.IsActive means "alive and not downed" in
            // offline sessions. Explicitly reject any that also have DownedState active.
            if (count == 0 && players.Length == 0)
            {
                var localPlayers = FindObjectsByType<PlayerDownState>(FindObjectsInactive.Exclude);
                for (int i = 0; i < localPlayers.Length; i++)
                {
                    var player = localPlayers[i];
                    if (player == null || !player.IsActive || player.IsDown) continue;
                    if ((player.transform.position - transform.position).sqrMagnitude <= radiusSqr) count++;
                }
            }

            return count;
        }

        private void Decelerate(float deltaTime) =>
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, deceleration * deltaTime);

        private void ResolveRoutePoints()
        {
            _points.Clear();
            for (int i = 0; i < Zone3ConvoyRouteGraph.OrderedPoints.Length; i++)
            {
                var point = Zone3ConvoyRouteGraph.OrderedPoints[i];
                var routePoint = FindRoutePoint(point);
                if (routePoint != null) _points[point] = routePoint;
            }
            EnsureRouteChoiceMarkers();
        }

        private Vector3 AtCruiseHeight(Vector3 position)
        {
            position.y = _cruiseHeight;
            return position;
        }

        private void CacheSegmentDirection(Zone3ConvoyRoutePoint nextPoint)
        {
            _hasSegmentDirection = false;
            if (!_points.TryGetValue(nextPoint, out var target)) return;

            Vector3 from = transform.position;
            Vector3 to = AtCruiseHeight(target.position);
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f) return;

            _segmentDirection = direction.normalized;
            _hasSegmentDirection = true;
        }

        private void EnsureRouteChoiceMarkers()
        {
            foreach (var pair in _points)
            {
                if (_routeChoiceMarkers.ContainsKey(pair.Key)) continue;

                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = $"Zone3_RouteChoice_{pair.Key}";
                marker.layer = gameObject.layer;
                marker.transform.SetParent(pair.Value, false);
                marker.transform.localPosition = Vector3.up * 1.25f;
                marker.transform.localRotation = Quaternion.identity;
                marker.transform.localScale = Vector3.one * routeMarkerScale;
                if (marker.TryGetComponent<Renderer>(out var renderer))
                {
                    var markerColor = new MaterialPropertyBlock();
                    markerColor.SetColor("_BaseColor", new Color(0.1f, 0.95f, 1f, 1f));
                    markerColor.SetColor("_Color", new Color(0.1f, 0.95f, 1f, 1f));
                    markerColor.SetColor("_EmissionColor", new Color(0.05f, 0.7f, 1f, 1f));
                    renderer.SetPropertyBlock(markerColor);
                }
                var choice = marker.AddComponent<Zone3RouteChoicePoint>();
                choice.Bind(this, pair.Key);
                _routeChoiceMarkers[pair.Key] = choice;
                marker.SetActive(false);
            }
        }

        private void RefreshRouteChoiceMarkers()
        {
            foreach (var pair in _routeChoiceMarkers)
            {
                pair.Value.gameObject.SetActive(false);
            }

            if (!_initialized || IsFuelEmpty || !_waitingForRouteChoice) return;

            var options = Zone3ConvoyRouteGraph.GetNextPoints(_currentPoint);
            for (int i = 0; i < options.Count; i++)
            {
                if (_routeChoiceMarkers.TryGetValue(options[i], out var marker))
                    marker.gameObject.SetActive(true);
            }
        }

        private static Transform FindRoutePoint(Zone3ConvoyRoutePoint point)
        {
            string canonicalName = Zone3ConvoyRouteGraph.GetSceneObjectName(point);
            var go = GameObject.Find(canonicalName);
            if (go != null) return go.transform;

            string shortName = GetShortSceneObjectName(point);
            if (!string.IsNullOrEmpty(shortName))
            {
                go = GameObject.Find(shortName);
                if (go != null) return go.transform;
            }

            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate == null) continue;
                string candidateName = candidate.name.Trim();
                if (candidateName == canonicalName || candidateName == shortName)
                    return candidate;
            }

            Debug.LogWarning($"[Zone3Convoy] Route point '{canonicalName}' was not found in scene.");
            return null;
        }

        private static string GetShortSceneObjectName(Zone3ConvoyRoutePoint point)
        {
            return point switch
            {
                Zone3ConvoyRoutePoint.Point00 => "Z3_Route_00",
                Zone3ConvoyRoutePoint.Point01 => "Z3_Route_01",
                Zone3ConvoyRoutePoint.Point02 => "Z3_Route_02",
                Zone3ConvoyRoutePoint.Point03 => "Z3_Route_03",
                Zone3ConvoyRoutePoint.Point04 => "Z3_Route_04",
                Zone3ConvoyRoutePoint.Point05 => "Z3_Route_05",
                Zone3ConvoyRoutePoint.Point06 => "Z3_Route_06",
                Zone3ConvoyRoutePoint.Point07 => "Z3_Route_07",
                Zone3ConvoyRoutePoint.Point08 => "Z3_Route_08",
                Zone3ConvoyRoutePoint.Point09 => "Z3_Route_09",
                Zone3ConvoyRoutePoint.Point10 => "Z3_Route_10",
                Zone3ConvoyRoutePoint.Final => "Z3_Route_Final",
                _ => string.Empty
            };
        }

        private Zone3ConvoyRoutePoint FindNearestPoint()
        {
            Zone3ConvoyRoutePoint nearest = Zone3ConvoyRoutePoint.Point00;
            float best = float.PositiveInfinity;
            foreach (var pair in _points)
            {
                float distance = (pair.Value.position - transform.position).sqrMagnitude;
                if (distance >= best) continue;
                nearest = pair.Key;
                best = distance;
            }
            return nearest;
        }
    }
}
