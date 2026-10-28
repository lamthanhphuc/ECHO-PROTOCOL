using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.Networking
{
    [DefaultExecutionOrder(-150)]
    public sealed class AlienHuntMission : MonoBehaviour
    {
        public const string SceneName = "Scene_B";
        public const int AlienCount = 9;
        public const int FurnaceCapacity = 3;
        public const int JerrycanCount = 10;
        public const int MeatCount = 17;
        public const int EntityCount = 27 + MeatCount;
        [SerializeField] private bool showPurgeDiagnostics;
        [SerializeField] private float meatRespawnSeconds = 60f;
        [SerializeField] private float lowSupplyRespawnSeconds = 15f;
        [SerializeField] private AlienHuntActor[] actors;
        [SerializeField] private AlienHuntFurnace[] furnaces;
        [SerializeField] private float roamRadius = 8f;
        [SerializeField] private float baitRadius = 24f;
        [SerializeField] private float eatingSeconds = 4.84f;
        [SerializeField] private float stunnedSeconds = 30f;
        [SerializeField] private float burnSeconds = 8f;
        [SerializeField] private float droppedJerrycanRecoverySeconds = 90f;
        [SerializeField, Min(1f)]
        private float jerrycanRespawnSeconds = 90f;
        private readonly HuntEntityState[] _offline = new HuntEntityState[EntityCount];
        private readonly HuntFurnaceState[] _offlineFurnaces = new HuntFurnaceState[3];
        private bool _offlineReady;
        private int _offlineBurned;
        private GameObject _offlinePlayer;
        private float _dropHintUntil;
        private NetworkMatchState Match => NetworkMatchState.Instance;
        private bool NetworkReady => Match && Match.Object && Match.Object.IsValid;
        public bool Ready => NetworkReady ? Match.HuntReady : _offlineReady;
        public bool Completed => NetworkReady ? Match.IsEnded : _offlineBurned >= AlienCount;
        public bool Authority => NetworkReady ? Match.Object.HasStateAuthority : _offlineReady;
        public int Burned => NetworkReady ? Match.HuntBurned : _offlineBurned;
        public AlienHuntActor[] Actors => actors;
        public AlienHuntFurnace[] Furnaces => furnaces;
        public static AlienHuntMission Instance { get; private set; }
        private PlayerRef LocalPlayer => NetworkReady ? Match.Runner.LocalPlayer : PlayerRef.FromIndex(0);
        public int LocalHeldIndex => Ready ? HeldIndex(LocalPlayer) : -1;
        public bool LocalPlacementRejected => Time.unscaledTime < _dropHintUntil;

        public static bool IsCarrying(GameObject player)
        {
            var m = Instance; if (!m || !m.Ready || !player) return false;
            var obj = player.GetComponentInParent<NetworkObject>();
            return m.HeldIndex(obj && obj.IsValid ? obj.InputAuthority : m.LocalPlayer) >= 0;
        }
        public static string InteractKey => EchoProtocol.Settings.GameplayInputSettings.GetKeyLabel(EchoProtocol.Settings.GameplayAction.Interact);
        private float DifficultyScale => EchoProtocol.Networking.Authority.MatchAuthorityRuntime.Instance
            ? (EchoProtocol.Networking.Authority.MatchAuthorityRuntime.Instance.Difficulty == EchoProtocol.Gameplay.MatchDifficulty.Easy ? .8f
                : EchoProtocol.Networking.Authority.MatchAuthorityRuntime.Instance.Difficulty == EchoProtocol.Gameplay.MatchDifficulty.Hard ? 1.25f : 1f) : 1f;
        private float StunDuration => stunnedSeconds / DifficultyScale;
        public void Configure(AlienHuntActor[] entities, AlienHuntFurnace[] stations)
        { actors = entities; furnaces = stations; }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Start()
        {
            // A direct Editor scene preview uses the same simulation without a Photon session.
            if (!FindAnyObjectByType<NetworkBootstrap>())
            { _offlineReady = true; Initialize(); }
        }
        private void FixedUpdate()
        { if (_offlineReady && !NetworkReady && !Completed) Tick(Time.fixedDeltaTime); }
        private void LateUpdate()
        {
            ApplyNetworkNestHomes();
        }

        public void ApplyNetworkNestHomes()
        {
            if (!NetworkReady || !Match.HuntReady)
                return;

            for (int i = 0; i < AlienCount; i++)
                actors[i].RelocateNestHome(Match.HuntNestPositions[i]);
        }

        private void RandomizeNests()
        {
            var triangulation = NavMesh.CalculateTriangulation();
            int triangleCount = triangulation.indices.Length / 3;
            var selected = new Vector3[AlienCount];
            if (triangleCount == 0)
            {
                for (int i = 0; i < AlienCount; i++)
                    if (NetworkReady)
                        Match.HuntNestPositions.Set(i, actors[i].Home);

                Debug.LogError("[AlienHunt] NavMesh triangulation is empty.");
                return;
            }
            var path = new NavMeshPath();

            for (int i = 0; i < AlienCount; i++)
            {
                bool found = false;
                Vector3 originalHome = actors[i].Home;
                Vector3 originalNest = actors[i].transform.parent.position;
                Vector3 localOffset = originalHome - originalNest;

                for (int attempt = 0; attempt < 512; attempt++)
                {
                    int triangle = Random.Range(0, triangleCount) * 3;
                    Vector3 nestPosition = (triangulation.vertices[triangulation.indices[triangle]]
                        + triangulation.vertices[triangulation.indices[triangle + 1]]
                        + triangulation.vertices[triangulation.indices[triangle + 2]]) / 3f;
                    if (nestPosition.x < 430f || nestPosition.x > 660f || nestPosition.z < 300f || nestPosition.z > 650f || nestPosition.y < 16.15f) continue;
                    if (!NavMesh.SamplePosition(nestPosition + localOffset, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                    Vector3 home = hit.position;
                    Vector3 actualNest = originalNest + home - originalHome;
                    if (Vector3.Distance(actualNest, nestPosition) > 1.5f || Vector3.Distance(home, originalHome) < 10f) continue;
                    if (!AlienHuntActor.TryNestGround(actors[i].transform.parent, home - originalHome, out _)) continue;

                    bool separated = true;
                    for (int j = 0; j < i; j++)
                        if (Vector3.Distance(home, selected[j]) < 18f) { separated = false; break; }
                    if (!separated) continue;
                    // Keep unprocessed authored nests available as safe fallbacks.
                    for (int j = i + 1; j < AlienCount; j++)
                    {
                        if (Vector3.Distance(home, actors[j].Home) < 18f)
                        {
                            separated = false;
                            break;
                        }
                    }

                    if (!separated) continue;
                    foreach (var furnace in furnaces)
                        if (Vector3.Distance(actualNest, furnace.transform.position) < 12f) { separated = false; break; }
                    if (!separated) continue;
                    bool blocked = System.Array.Exists(
                        Physics.OverlapCapsule(
                            home + Vector3.up * .65f,
                            home + Vector3.up * 1.3f,
                            .45f,
                            Physics.DefaultRaycastLayers,
                            QueryTriggerInteraction.Ignore),
                        collider => collider &&
                            !(collider is TerrainCollider) &&
                            !collider.transform.IsChildOf(actors[i].transform.parent));

                    if (blocked) continue;
                    if (!NavMesh.CalculatePath(actors[0].Home, home, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;

                    selected[i] = home;
                    found = true;
                    break;
                }
                if (!found)
                {
                    selected[i] = originalHome;
                    Debug.LogWarning(
                        $"[AlienHunt] Nest {i} kept its original position.");
                }
            }

            for (int i = 0; i < AlienCount; i++)
            {
                if (NetworkReady) Match.HuntNestPositions.Set(i, selected[i]);
                actors[i].RelocateNestHome(selected[i]);
            }
            Physics.SyncTransforms();
        }
        // Input is routed through the existing player drop handler, before normal tools.
        public bool TryDropLocal(GameObject interactor)
        {
            if (!Ready || Completed || LocalHeldIndex < 0) return false;
            if (!PlayerInteractionControlLock.IsGameplayInputBlocked(interactor))
            {
                if (!interactor || !TryDropPose(LocalHeldIndex, interactor.transform, out _, out _))
                    _dropHintUntil = Time.unscaledTime + 3f;
                else Request(-2, -1, interactor);
            }
            return true; // Consume input even if placement fails; never drop a second item.
        }
        public HuntEntityState Read(int i) => NetworkReady ? Match.HuntEntities[i] : _offline[i];
        private void Write(int i, HuntEntityState value)
        { if (NetworkReady) Match.HuntEntities.Set(i, value); else _offline[i] = value; }
        public HuntFurnaceState ReadFurnace(int i) => NetworkReady ? Match.HuntFurnaces[i] : _offlineFurnaces[i];
        private void WriteFurnace(int i, HuntFurnaceState value)
        { if (NetworkReady) Match.HuntFurnaces.Set(i, value); else _offlineFurnaces[i] = value; }
        public void Initialize()
        {
            RandomizeNests();
            for (int i = 0; i < actors.Length; i++) Write(i, new HuntEntityState
            { Position = actors[i].Home, LastGroundPosition = actors[i].Home, Rotation = actors[i].HomeRotation, Target = -1, NestIndex = i < AlienCount ? i : -1 });
            // Keep all 18 authored candidates, but enable only 10 unique locations per match.
            // Initialize runs on state authority; the selected stages are replicated to every client.
            var fuelSlots = new System.Collections.Generic.List<int>();
            for (int i = 0; i < actors.Length; i++)
                if (actors[i].Kind == HuntKind.Jerrycan) fuelSlots.Add(i);
            for (int i = 0; i < fuelSlots.Count; i++)
            {
                int chosen = Random.Range(i, fuelSlots.Count);
                int slot = fuelSlots[chosen]; fuelSlots[chosen] = fuelSlots[i]; fuelSlots[i] = slot;
                if (i < JerrycanCount) continue;
                var unused = Read(slot);
                unused.Stage = 3;
                unused.Remaining = -1f;
                Write(slot, unused);
            }
            for (int i = 0; i < furnaces.Length; i++) WriteFurnace(i, default);
        }
        public int HeldIndex(PlayerRef player)
        {
            if (player.IsNone) return -1;
            for (int i = 0; i < actors.Length; i++) if (Read(i).Carrier == player) return i;
            return -1;
        }
        public bool DebugGive(PlayerRef player, HuntKind kind)
        {
            if (!Authority || !Ready || Completed || HeldIndex(player) >= 0 || kind == HuntKind.Alien) return false;
            for (int i = 0; i < actors.Length; i++)
            {
                if (actors[i].Kind != kind) continue;
                var state = Read(i);
                if (!state.Carrier.IsNone || (state.Stage != 0 && !(kind == HuntKind.Meat && state.Stage == 2 && !IsBaitClaimed(i)))) continue;
                state.Stage = 1;
                state.Carrier = player;
                state.LastGroundPosition = state.Position;
                Write(i, state);
                return true;
            }
            return false;
        }
        public bool IsBaitClaimed(int index)
        {
            for (int i = 0; i < AlienCount; i++)
            {
                var s = Read(i);
                if ((s.Stage == (int)AlienStage.Running || s.Stage == (int)AlienStage.Eating) && s.Target == index) return true;
            }
            return false;
        }
        public void Request(int entity, int furnace, GameObject interactor)
        {
            if (NetworkReady) { Match.RequestHuntAction(entity, furnace); return; }
            if (!_offlineReady) return;
            if (interactor) _offlinePlayer = interactor;
            if (_offlinePlayer) Act(LocalPlayer, _offlinePlayer.transform, entity, furnace);
        }
        private Transform Carrier(PlayerRef player)
        {
            if (NetworkReady) return Match.Runner.TryGetPlayerObject(player, out var obj) && obj ? obj.transform : null;
            return _offlinePlayer ? _offlinePlayer.transform : null;
        }
        public void HeldPose(PlayerRef carrier, HuntKind kind, out Vector3 position, out Quaternion rotation, bool presentation)
        {
            var owner = Carrier(carrier);
            if (!owner) { position = transform.position; rotation = Quaternion.identity; return; }
            var anchor = PlayerHeldItemAnchor.ResolveRightHandAnchor(owner.gameObject);
            // The meshes have different pivots. Keep their grip points at the right
            // hand and rotate from the player's heading so the alien and can stay upright.
            var heading = Quaternion.Euler(0f, owner.eulerAngles.y, 0f);
            Vector3 grip;
            if (kind == HuntKind.Alien)
            {
                rotation = heading;
                grip = new Vector3(.05f, .61f, .15f); // middle of the torso
            }
            else if (kind == HuntKind.Jerrycan)
            {
                rotation = heading;
                grip = new Vector3(0f, .39f * AlienHuntActor.JerrycanVisualScale, 0f); // top handle
            }
            else
            {
                rotation = heading * Quaternion.Euler(15f, 0f, 0f) * Quaternion.Euler(0f, 0f, 75f);
                grip = new Vector3(-.04f, .54f, 0f); // middle of the narrow bone
            }
            var hand = anchor.position + owner.forward * .08f;
            position = hand - rotation * grip;
        }
        public bool Act(PlayerRef player, Transform owner, int entity, int furnace)
        {
            if (!Ready || Completed || !owner) return false;
            int held = HeldIndex(player);
            if (entity == -2) return held >= 0 && Drop(held, owner);
            if (furnace >= 0)
            {
                if (furnace >= furnaces.Length || !Near(owner.position, furnaces[furnace])) return false;
                var f = ReadFurnace(furnace);
                if (f.BurnRemaining > 0 || f.BurnedCount >= FurnaceCapacity) return false;
                // Ignition is a furnace action. A carried item must not block it.
                if (f.Fuel > 0 && f.AlienSlot > 0)
                {
                    f.BurnRemaining = burnSeconds * DifficultyScale;
                    var burningAlien = Read(f.AlienSlot - 1);
                    burningAlien.Stage = (int)AlienStage.Burning;
                    Write(f.AlienSlot - 1, burningAlien);
                    WriteFurnace(furnace, f);
                    return true;
                }
                if (held >= 0)
                {
                    var h = Read(held);
                    if (actors[held].Kind == HuntKind.Jerrycan && f.Fuel == 0)
                    {
                        f.Fuel = 1;
                        h.Stage = 3;
                        h.Remaining = jerrycanRespawnSeconds;
                    }
                    else if (actors[held].Kind == HuntKind.Alien && f.AlienSlot == 0 && f.Fuel > 0)
                    { f.AlienSlot = held + 1; h.Stage = (int)AlienStage.OnPan; h.Position = furnaces[furnace].Pan.position;
                        h.Rotation = furnaces[furnace].Pan.rotation; }
                    else return false;
                    h.Carrier = PlayerRef.None; Write(held, h); WriteFurnace(furnace, f); return true;
                }
                return false;
            }
            if (entity < 0 || entity >= actors.Length || held >= 0 || !Near(owner.position, actors[entity])) return false;
            var state = Read(entity); var kind = actors[entity].Kind;
            bool pickable = kind == HuntKind.Alien ? (state.Stage == (int)AlienStage.Stunned || state.Stage == (int)AlienStage.Eating)
                : state.Stage == 0 || (kind == HuntKind.Meat && state.Stage == 2 && !IsBaitClaimed(entity));
            if (!pickable || !state.Carrier.IsNone) return false;
            if (kind == HuntKind.Alien && state.Stage == (int)AlienStage.Eating)
            {
                // Catching during the meal consumes this bait once and starts its normal refill timer.
                if (state.Target >= 0 && state.Target < actors.Length && actors[state.Target].Kind == HuntKind.Meat)
                {
                    var bait = Read(state.Target);
                    bait.Stage = 3; bait.Remaining = meatRespawnSeconds;
                    Write(state.Target, bait);
                }
                if (actors[entity].Agent) actors[entity].Agent.enabled = false;
            }
            state.Stage = kind == HuntKind.Alien ? (int)AlienStage.Carried : 1;
            state.LastGroundPosition = state.Position; state.Carrier = player; state.Target = -1; Write(entity, state); return true;
        }
        private static bool Near(Vector3 position, Component target)
        {
            float distance = (target.transform.position - position).sqrMagnitude;
            foreach (var c in target.GetComponentsInChildren<Collider>()) if (c.enabled)
            {
                var closest = c is MeshCollider mesh && !mesh.convex ? c.bounds.ClosestPoint(position) : c.ClosestPoint(position);
                distance = Mathf.Min(distance, (closest - position).sqrMagnitude);
            }
            return distance <= 16f;
        }
        private bool TryDropPose(int index, Transform owner, out Vector3 floor, out Quaternion floorRotation)
        {
            ItemDropPlacementUtility.GetFloorSnappedPose(owner, owner, 1.2f, 0f, out floor, out floorRotation);
            if (actors[index].Kind == HuntKind.Alien)
            {
                // Keep a dropped alien on the connected roaming network so it can wake and return.
                bool found = false; var path = new NavMeshPath();
                for (int step = 0; step < 5; step++)
                {
                    var candidate = Vector3.Lerp(floor, owner.position, step / 4f);
                    if (!NavMesh.SamplePosition(candidate,out var nav, .75f, NavMesh.AllAreas)) continue;
                    if (!NavMesh.CalculatePath(nav.position,actors[0].Home,NavMesh.AllAreas,path)
                        || path.status != NavMeshPathStatus.PathComplete) continue;
                    floor = nav.position; found = true; break;
                }
                if (!found) return false; // Keep the alien in hand; never strand it on a disconnected floor.
            }
            return true;
        }
        private bool Drop(int index, Transform owner)
        {
            if (!TryDropPose(index, owner, out var floor, out var floorRotation)) return false;
            var s = Read(index);
            s.Position = floor; s.Rotation = floorRotation; s.Carrier = PlayerRef.None;
            s.Stage = actors[index].Kind == HuntKind.Alien ? (int)AlienStage.Stunned : actors[index].Kind == HuntKind.Meat ? 2 : 0;
            s.LastGroundPosition = s.Position; s.Fleeing = false;
            s.Remaining = actors[index].Kind == HuntKind.Meat ? 120f :
                actors[index].Kind == HuntKind.Jerrycan ? droppedJerrycanRecoverySeconds : StunDuration;
            Write(index, s); return true;
        }
        public void Tick(float delta)
        {
            int availableMeat = 0;
            for (int i = 27; i < actors.Length; i++)
            {
                var bait = Read(i);
                if (bait.Carrier.IsNone && (bait.Stage == 0 || bait.Stage == 2) && !IsBaitClaimed(i)) availableMeat++;
            }
            int activeJerrycans = 0;

            for (int i = 9; i < 27; i++)
            {
                var fuel = Read(i);
                if (fuel.Stage == 0 || fuel.Stage == 1)
                    activeJerrycans++;
            }
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i]; var s = Read(i);
                if (!s.Carrier.IsNone)
                {
                    var owner = Carrier(s.Carrier);
                    bool invalid = !owner || (owner.TryGetComponent<NetworkPlayerLifeState>(out var life) && !life.CanInitiateAction);
                    if (invalid)
                    {
                        var dropPoint = owner ? owner.position : s.Position;
                        s.Position = SafeRecoveryFloor(dropPoint, s.LastGroundPosition);
                        if (actor.Kind == HuntKind.Alien && !CanReturnToNests(s.Position)) s.Position = s.LastGroundPosition;
                        s.LastGroundPosition = s.Position; s.Carrier = PlayerRef.None;
                        s.Stage = actor.Kind == HuntKind.Alien ? (int)AlienStage.Stunned : actor.Kind == HuntKind.Meat ? 2 : 0;
                        s.Remaining = actor.Kind == HuntKind.Meat ? 120f :
                            actor.Kind == HuntKind.Jerrycan ? droppedJerrycanRecoverySeconds : StunDuration;
                    }
                    else
                    {
                        var safe = SafeRecoveryFloor(owner.position, s.LastGroundPosition);
                        if (actor.Kind != HuntKind.Alien || CanReturnToNests(safe)) s.LastGroundPosition = safe;
                        HeldPose(s.Carrier, actor.Kind, out s.Position, out s.Rotation, false);
                    }
                    if (actor.Agent) actor.Agent.enabled = false;
                    Write(i, s); continue;
                }
                if (actor.Kind != HuntKind.Alien)
                {
                    if (actor.Kind == HuntKind.Jerrycan && s.Stage == 0 && s.Remaining > 0f)
                    {
                        s.Remaining -= delta;
                        if (s.Remaining <= 0f)
                        { s.Position = actor.Home; s.Rotation = actor.HomeRotation; s.Remaining = 0f; }
                        Write(i, s);
                    }
                    if (actor.Kind == HuntKind.Jerrycan &&
                        s.Stage == 3 && s.Remaining >= 0f)
                    {
                        s.Remaining = Mathf.Max(0f, s.Remaining - delta);

                        if (s.Remaining <= 0f &&
                            activeJerrycans < JerrycanCount)
                        {
                            s.Stage = 0;
                            s.Position = actor.Home;
                            s.Rotation = actor.HomeRotation;
                            s.LastGroundPosition = s.Position;
                            activeJerrycans++;
                        }

                        Write(i, s);
                    }
                    if (actor.Kind == HuntKind.Meat && s.Stage == 3)
                    {
                        s.Remaining -= delta;
                        if (s.Remaining <= 0 || (availableMeat < 3 && s.Remaining <= meatRespawnSeconds - lowSupplyRespawnSeconds))
                        { s.Stage = 0; s.Position = actor.Home; s.Rotation = actor.HomeRotation; availableMeat++; }
                        Write(i, s);
                    }
                    if (actor.Kind == HuntKind.Meat && s.Stage == 2 && !IsBaitClaimed(i))
                    {
                        s.Remaining -= delta;
                        if (s.Remaining <= 0)
                        { s.Stage = 0; s.Position = actor.Home; s.Rotation = actor.HomeRotation; }
                        Write(i, s);
                    }
                    continue;
                }
                s.BaitCooldown = Mathf.Max(0, s.BaitCooldown - delta);
                var stage = (AlienStage)s.Stage;
                var agent = actor.Agent;
                if (stage >= AlienStage.Stunned)
                {
                    if (agent) agent.enabled = false;
                    if (stage == AlienStage.Stunned)
                    {
                        s.Remaining -= delta;
                        if (s.Remaining <= 0)
                        { s.NestIndex = ChooseVacantNest(i, s); s.Stage = 0; s.Target = -1; s.Returning = true;
                            s.ProgressPosition = s.Position; s.StuckSeconds = 0f; s.TravelSeconds = 0f; }
                    }
                    Write(i, s); continue;
                }
                if (!agent.enabled)
                {
                    actor.transform.SetPositionAndRotation(s.Position, s.Rotation); agent.enabled = true;
                    if (!agent.isOnNavMesh || !agent.Warp(s.Position))
                    {
                        agent.enabled = false;
                        s.StuckSeconds += delta;
                        if (s.StuckSeconds >= 8f) RecoverAlienAtNest(actor, ref s);
                        Write(i, s);
                        continue;
                    }
                }
                if (stage == AlienStage.Eating)
                {
                    agent.isStopped = true; s.Remaining -= delta;
                    if (s.Remaining <= 0)
                    {
                        var bait = Read(s.Target); bait.Stage = 3; bait.Remaining = meatRespawnSeconds; Write(s.Target, bait);
                        s.Stage = (int)AlienStage.Wandering; s.Remaining = 0; s.Target = -1;
                        s.Returning = true; s.Fleeing = true; s.BaitCooldown = 3f;
                        s.ProgressPosition = actor.transform.position; s.StuckSeconds = 0f; s.TravelSeconds = 0f;
                        agent.isStopped = false;
                    }
                }
                else
                {
                    agent.isStopped = false;
                    if (stage == AlienStage.Wandering && s.BaitCooldown <= 0)
                    {
                        int bait = FindBait(i, s.Position);
                        if (bait >= 0)
                        { s.Stage = (int)AlienStage.Running; s.Target = bait; s.Fleeing = false; s.ProgressPosition = actor.transform.position;
                            s.StuckSeconds = 0; s.TravelSeconds = 0; }
                    }
                    if (s.Stage == (int)AlienStage.Running)
                    {
                        var bait = Read(s.Target);
                        s.TravelSeconds += delta;
                        if ((actor.transform.position - s.ProgressPosition).sqrMagnitude > .25f)
                        { s.ProgressPosition = actor.transform.position; s.StuckSeconds = 0; }
                        else s.StuckSeconds += delta;
                        bool blocked = s.StuckSeconds >= 8f || s.TravelSeconds >= 45f
                            || (!agent.pathPending && agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete);
                        if (bait.Stage != 2 || blocked)
                        {
                            s.Stage = 0; s.Target = -1; s.BaitCooldown = 8f; s.Returning = true;
                            s.ProgressPosition = actor.transform.position; s.StuckSeconds = 0f; s.TravelSeconds = 0f;
                            agent.ResetPath(); agent.speed = DifficultyScale;
                        }
                        else
                        {
                            agent.speed = 3.8f * DifficultyScale; agent.stoppingDistance = .65f;
                            if (!agent.pathPending && (!agent.hasPath || (agent.destination - bait.Position).sqrMagnitude > .04f))
                                agent.SetDestination(bait.Position);
                            if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathComplete
                                && (actor.transform.position - bait.Position).sqrMagnitude <= .85f * .85f)
                            {
                                agent.isStopped = true;
                                var facing = Vector3.ProjectOnPlane(bait.Position - actor.transform.position, Vector3.up);
                                if (facing.sqrMagnitude > .001f) actor.transform.rotation = Quaternion.LookRotation(facing);
                                s.Stage = (int)AlienStage.Eating; s.Remaining = eatingSeconds * DifficultyScale;
                            }
                        }
                    }
                    else
                    {
                        var home = actors[Mathf.Clamp(s.NestIndex, 0, AlienCount - 1)].Home;
                        agent.speed = (s.Fleeing ? 3.8f : 1f) * DifficultyScale; agent.stoppingDistance = .3f; s.Remaining -= delta;
                        if (s.Returning && (actor.transform.position - home).sqrMagnitude <= .6f * .6f)
                        { s.Returning = false; s.Fleeing = false; agent.speed = DifficultyScale; }
                        if (s.Returning || (actor.transform.position - home).sqrMagnitude > (roamRadius + 1f) * (roamRadius + 1f))
                        {
                            if (!agent.pathPending && (!agent.hasPath || (agent.destination - home).sqrMagnitude > 1f))
                                agent.SetDestination(home);
                        }
                        else if (s.Remaining <= 0 && (!agent.hasPath || agent.remainingDistance < .6f))
                        {
                            var offset = Random.insideUnitCircle * roamRadius;
                            if (NavMesh.SamplePosition(home + new Vector3(offset.x, 0, offset.y), out var hit, 2f, NavMesh.AllAreas))
                            {
                                var path = new NavMeshPath();
                                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                                {
                                    bool local = true;
                                    foreach (var corner in path.corners)
                                        if ((corner - home).sqrMagnitude > (roamRadius + 1f) * (roamRadius + 1f)) local = false;
                                    if (local) agent.SetPath(path);
                                }
                            }
                            s.Remaining = Random.Range(2f, 4f);
                        }
                        bool shouldMove = s.Returning || (agent.hasPath && agent.remainingDistance > .6f);
                        if (shouldMove)
                        {
                            if ((actor.transform.position - s.ProgressPosition).sqrMagnitude > .25f)
                            { s.ProgressPosition = actor.transform.position; s.StuckSeconds = 0f; s.TravelSeconds = 0f; }
                            else s.StuckSeconds += delta;
                            if (s.StuckSeconds >= 8f)
                            {
                                s.StuckSeconds = 0f;
                                s.TravelSeconds += 1f;
                                if (s.TravelSeconds >= 2f) RecoverAlienAtNest(actor, ref s);
                                else { agent.ResetPath(); agent.SetDestination(home); }
                            }
                        }
                        else { s.ProgressPosition = actor.transform.position; s.StuckSeconds = 0f; }
                    }
                }
                s.Position = actor.transform.position; s.Rotation = actor.transform.rotation; Write(i, s);
            }
            for (int i = 0; i < furnaces.Length; i++)
            {
                var f = ReadFurnace(i); if (f.BurnRemaining <= 0) continue;
                f.BurnRemaining = Mathf.Max(0, f.BurnRemaining - delta);
                if (f.BurnRemaining <= 0)
                {
                    var a = Read(f.AlienSlot - 1); a.Stage = (int)AlienStage.Burned; Write(f.AlienSlot - 1, a);
                    f.AlienSlot = 0; f.Fuel = 0; f.BurnedCount++;
                    if (NetworkReady) Match.RecordHuntBurn(furnaces[i].transform.position); else _offlineBurned++;
                }
                WriteFurnace(i, f);
            }
            EnsureJerrycanSupply();
        }
        private void RecoverAlienAtNest(AlienHuntActor actor, ref HuntEntityState state)
        {
            var home = actors[Mathf.Clamp(state.NestIndex, 0, AlienCount - 1)].Home;
            if (!NavMesh.SamplePosition(home, out var nav, 2f, NavMesh.AllAreas)) return;
            var agent = actor.Agent;
            if (!agent.enabled)
            {
                actor.transform.position = nav.position;
                agent.enabled = true;
            }
            if (!agent.Warp(nav.position)) { agent.enabled = false; return; }
            agent.ResetPath();
            state.Position = nav.position; state.Rotation = actor.HomeRotation;
            state.Stage = (int)AlienStage.Wandering; state.Target = -1;
            state.Returning = false; state.Fleeing = false;
            state.ProgressPosition = nav.position; state.StuckSeconds = 0f; state.TravelSeconds = 0f;
            state.BaitCooldown = Mathf.Max(state.BaitCooldown, 2f);
        }
        private void EnsureJerrycanSupply()
        {
            int needed = AlienCount - Burned;
            if (needed <= 0) return;
            int available = 0;
            for (int i = 0; i < actors.Length; i++)
            {
                if (actors[i].Kind != HuntKind.Jerrycan) continue;
                var state = Read(i);
                if (state.Stage == 0 || state.Stage == 1) available++;
            }
            for (int i = 0; i < furnaces.Length; i++) available += ReadFurnace(i).Fuel;
            if (available >= needed) return;
            var reserves = new System.Collections.Generic.List<int>();
            for (int i = 0; i < actors.Length; i++)
                if (actors[i].Kind == HuntKind.Jerrycan &&
                    Read(i).Stage == 3 &&
                    Read(i).Remaining < 0f)
                {
                    reserves.Add(i);
                }
            while (available < needed && reserves.Count > 0)
            {
                int pick = Random.Range(0, reserves.Count);
                int index = reserves[pick]; reserves.RemoveAt(pick);
                var state = Read(index);
                state.Stage = 0; state.Position = actors[index].Home;
                state.Rotation = actors[index].HomeRotation; state.Remaining = 0f;
                Write(index, state);
                available++;
            }
        }
        private bool CanReturnToNests(Vector3 point)
        {
            if (!NavMesh.SamplePosition(point,out var nav,.3f,NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(nav.position,actors[0].Home,NavMesh.AllAreas,path)
                && path.status == NavMeshPathStatus.PathComplete;
        }
        private static Vector3 SafeRecoveryFloor(Vector3 position, Vector3 lastSafe)
        {
            // Prefer the nearby floor, keeping the last authoritative floor if the carrier disappears off-map.
            if (NavMesh.SamplePosition(position, out var hit, 4f, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - position.y) < 3f) return hit.position;
            return lastSafe;
        }
        private int ChooseVacantNest(int alien, HuntEntityState state)
        {
            var choices = new System.Collections.Generic.List<int>();
            for (int nest = 0; nest < AlienCount; nest++)
            {
                if (nest == state.NestIndex) continue;
                bool occupied = false;
                for (int other = 0; other < AlienCount; other++)
                {
                    if (other == alien) continue;
                    var resident = Read(other);
                    // Captured aliens release their nest; walking/lured aliens reserve theirs.
                    if (resident.Stage < (int)AlienStage.Carried && resident.NestIndex == nest)
                    { occupied = true; break; }
                }
                if (occupied) continue;
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(state.Position, actors[nest].Home, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete) choices.Add(nest);
            }
            // If every other nest is occupied, return home rather than overlap another alien.
            return choices.Count > 0 ? choices[Random.Range(0, choices.Count)] : state.NestIndex;
        }
        private int FindBait(int alien, Vector3 position)
        {
            int best = -1; float distance = (baitRadius / DifficultyScale) * (baitRadius / DifficultyScale);
            for (int i = AlienCount; i < actors.Length; i++)
            {
                if (actors[i].Kind != HuntKind.Meat) continue;
                var bait = Read(i); float d = (position-bait.Position).sqrMagnitude;
                if (bait.Stage != 2 || d >= distance || IsBaitClaimed(i)) continue;
                var path = new NavMeshPath();
                if (!actors[alien].Agent.CalculatePath(bait.Position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                distance = d; best = i;
            }
            return best;
        }
        public static bool TryDescribe(out string title, out string detail, out float progress)
        {
            title = detail = ""; progress = 0;
            var m = Instance; if (!m) return false;
            title = "Săn alien non · " + m.Burned + "/9";
            detail = m.Completed ? (m.Burned >= AlienCount ? "Đã tiêu diệt đủ 9 alien non" : "Nhiệm vụ kết thúc")
                : "Đặt thịt [G] → Bắt alien khi đang ăn [E] → Mang tới chảo\nĐổ nhiên liệu [E] · Đặt alien [E] · Châm lửa [E]";
            if(m.NetworkReady && !m.Completed)
            {
                var match=m.Match;
                string mood=match.PurgeMood==PurgeBossMood.Hunting ? "BOSS ĐANG SĂN" : match.PurgeMood==PurgeBossMood.Warning
                    ? "CẢNH BÁO: BOSS SẮP SĂN" : match.PurgeMood==PurgeBossMood.Staggered ? "BOSS BỊ UV KHỐNG CHẾ" : "BOSS TẠM BÌNH TĨNH";
                detail += "\n" + mood;
                if (m.showPurgeDiagnostics)
                    detail += " | UV: " + match.PurgeStaggers + "/" + match.PurgeStaggersRequired;
                if (match.PurgeCoopAidEnabled)
                    detail += " | Sơ cứu: "+match.PurgeAidAvailable+"/9";
            }
            if (Time.unscaledTime < m._dropHintUntil)
                detail = "Không thể đặt alien ở đây. Hãy tìm mặt đất có lối về khu vực ổ trứng.";
            detail = detail.Replace("[E]", "[" + InteractKey + "]");
            progress = m.Burned / 9f; return true;
        }
    }
}
