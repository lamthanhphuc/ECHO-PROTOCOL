using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.Settings;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    /// <summary>Shared gameplay presentation. All gameplay values remain owned by their existing systems.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(600)]
    public sealed class HUDPurgePresentation : MonoBehaviour
    {
        private sealed class Card
        {
            public CanvasGroup Group;
            public Text Label;
            public HUDPurgeIcon Icon;
            public Image Fill;
        }

        private GameplayHUDManager _manager;
        private GameObject _root, _player;
        private Font _font;
        private PlayerDownState _down;
        private PlayerMovement _movement;
        private NetworkPlayerMovement _networkMovement;
        private NetworkPlayerFlashlight _uv;
        private NetworkPlayerInteractor _interactor;
        private PlayerInteraction _interaction;
        private CanvasGroup _equipment, _spectate, _scanner, _tutorial, _placement, _markers;
        private Card _objective, _uvMeter, _health, _stamina, _held, _uvHit, _missions, _notice;
        private readonly List<PlayerDownState> _teammates = new List<PlayerDownState>();
        private readonly List<Card> _teamRows = new List<Card>();
        private readonly PlayerInteractionControlLock _missionLock = new PlayerInteractionControlLock();
        private int _lastBurned = -1;
        private bool _pendingProgress;
        private float _objectiveUntil, _uvUntil, _healthUntil, _staminaUntil, _teamUntil, _nextTeamRefresh;
        private float _lastHealth = -1f;
        private string _teamSignature;
        private int _missionPage;

        public static bool IsPurgeScene => AlienHuntMission.Instance != null
            || SceneManager.GetActiveScene().name == AlienHuntMission.SceneName;
        public bool CanOpenExpeditionMissions => _player != null && _down != null && _down.IsActive
            && !(NetworkMatchState.Instance != null && NetworkMatchState.Instance.Object != null
                && NetworkMatchState.Instance.Object.IsValid && NetworkMatchState.Instance.CurrentPhase == NetworkMatchPhase.MatchEnded);
        public static bool CanShowGameplay(bool bound, bool modal, bool ended, bool spectating)
            => bound && !modal && !ended && !spectating;
        public static bool MeterVisible(float ratio, bool active, float now, float until)
            => ratio < .999f || active || now < until;
        public static bool ProgressAdvanced(int previous, int current) => previous >= 0 && current > previous;

        private void Awake()
        {
            _manager = GetComponent<GameplayHUDManager>();
            if (_manager == null) { enabled = false; return; }
            Build();
            if (IsPurgeScene) HideLegacy(_manager.ObjectiveTracker);
            else if (_manager.ObjectiveTracker != null)
                foreach (var graphic in _manager.ObjectiveTracker.GetComponentsInChildren<Graphic>(true))
                    graphic.enabled = false; // Keep the objective producer running behind the shared card.
            HideLegacy(_manager.PlayerVitals);
            HideLegacy(_manager.TeammateStatus);
            var oldMissions = GetComponent<HUDZoneMissions>();
            if (IsPurgeScene && oldMissions != null) oldMissions.enabled = false;
            if (_manager.Hotbar != null) _equipment = Group(_manager.Hotbar.gameObject);
            if (_manager.FieldScannerHUD != null) _scanner = Group(_manager.FieldScannerHUD.gameObject);
            if (_manager.ScannerTutorial != null) _tutorial = Group(_manager.ScannerTutorial.gameObject);
            if (_manager.NoiseMakerPlacement != null) _placement = Group(_manager.NoiseMakerPlacement.gameObject);
            if (_manager.WorldMarker != null) _markers = Group(_manager.WorldMarker.gameObject);
            if (_manager.SpectateOverlay != null)
            {
                _spectate = Group(_manager.SpectateOverlay.gameObject);
                var rect = _spectate.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
                rect.anchoredPosition = new Vector2(0, 40);
                AddIcon(rect, PurgeIcon.Spectate, new Vector2(18, -18), HUDPresentationStyle.Ink);
            }
        }

        public void BindPlayer(GameObject player)
        {
            if (_player == player) return;
            CloseMissions();
            _player = player;
            _down = player != null ? player.GetComponentInChildren<PlayerDownState>(true) : null;
            _movement = player != null ? player.GetComponentInChildren<PlayerMovement>(true) : null;
            _networkMovement = player != null ? player.GetComponentInChildren<NetworkPlayerMovement>(true) : null;
            _uv = player != null ? player.GetComponentInChildren<NetworkPlayerFlashlight>(true) : null;
            _interactor = player != null ? player.GetComponentInChildren<NetworkPlayerInteractor>(true) : null;
            _interaction = player != null ? player.GetComponentInChildren<PlayerInteraction>(true) : null;
            _lastHealth = -1;
        }

        public void ToggleMissions()
        {
            if (_missionLock.IsLocked) { CloseMissions(); return; }
            var mission = AlienHuntMission.Instance;
            if (_player == null || _down == null || !_down.IsActive || mission == null || !mission.Ready
                || mission.Completed || PlayerInteractionControlLock.IsGameplayInputBlocked(_player)) return;
            _missionLock.Acquire(_player, CloseMissions, unlockCursor: false);
            _missionPage = 0;
        }

        private void Update()
        {
            if (_missions == null || !IsPurgeScene) return;
            var mission = AlienHuntMission.Instance;
            bool ended = IsPurgeScene ? mission != null && mission.Completed
                : NetworkMatchState.Instance != null && NetworkMatchState.Instance.Object != null
                    && NetworkMatchState.Instance.Object.IsValid && NetworkMatchState.Instance.CurrentPhase == NetworkMatchPhase.MatchEnded;
            if (_missionLock.IsLocked)
            {
                if (_missionLock.ShouldAutoRelease() || ended || !_missionLock.IsTopmost) CloseMissions();
                else if (_missionLock.ConsumeEscape() || Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
                    CloseMissions();
                else if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                    _missionPage = (_missionPage + 1) % 2;
            }
            else if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame) ToggleMissions();
            _missions.Group.alpha = _missionLock.IsLocked ? 1f : 0f;
        }

        private void LateUpdate()
        {
            if (_root == null) return;
            var mission = AlienHuntMission.Instance;
            bool ended = IsPurgeScene ? mission != null && mission.Completed
                : NetworkMatchState.Instance != null && NetworkMatchState.Instance.Object != null
                    && NetworkMatchState.Instance.Object.IsValid && NetworkMatchState.Instance.CurrentPhase == NetworkMatchPhase.MatchEnded;
            bool spectating = _down != null && (_down.IsSpectating || _down.IsEliminated);
            bool visible = CanShowGameplay(_player != null && _down != null, PlayerInteractionControlLock.HasModal,
                ended, spectating);
            bool active = visible && _down.IsActive;
            if (!active && _manager.InteractionPrompt != null) _manager.InteractionPrompt.SuppressForPurgeFrame();
            if (_equipment != null) _equipment.alpha = active ? 1f : 0f;
            if (_markers != null) _markers.alpha = active ? 1f : 0f;
            if (_spectate != null && (ended || PlayerInteractionControlLock.HasModal)) _spectate.alpha = 0f;
            if (!active)
            {
                if (_scanner != null) _scanner.alpha = 0f;
                if (_tutorial != null) _tutorial.alpha = 0f;
                if (_placement != null) _placement.alpha = 0f;
            }

            float now = Time.unscaledTime;
            if (!IsPurgeScene)
            {
                _objective.Icon.Kind = PurgeIcon.Missions;
                _objective.Label.text = (_manager.ObjectiveTracker != null ? _manager.ObjectiveTracker.PresentationText : "EXPEDITION")
                    + "\n" + L("[J] Nhiệm vụ", "[J] Objectives");
                Fade(_objective, active);
                var carrier = _player != null ? _player.GetComponentInChildren<PlayerEnergyCoreCarrier>(true) : null;
                var identity = _player != null ? _player.GetComponent<LobbyPlayerState>() : null;
                bool networkBound = identity != null && identity.Object != null && identity.Object.IsValid;
                bool core = networkBound ? identity.CarriedCoreId.IsValid : carrier != null && carrier.IsCarrying;
                bool fuel = _player != null && EchoProtocol.MatchFlow.Zone3FuelCell.FindCarried(_player) != null;
                _held.Icon.Kind = fuel ? PurgeIcon.Fuel : PurgeIcon.Core;
                _held.Label.text = (fuel ? L("PIN NHIÊN LIỆU", "FUEL CELL") : L("LÕI NĂNG LƯỢNG", "ENERGY CORE"))
                    + "\n" + L("[G] Thả", "[G] Drop");
                Fade(_held, active && (core || fuel));
                // The legacy tracker can create progress graphics after Awake.
                // Hide its entire canvas while continuing to read its live instructions.
                if (_manager.ObjectiveTracker != null) Group(_manager.ObjectiveTracker.gameObject).alpha = 0f;
            }
            else if (mission != null && mission.Ready)
            {
                if (_lastBurned < 0) _objectiveUntil = now + 5f;
                if (ProgressAdvanced(_lastBurned, mission.Burned)) _pendingProgress = true;
                _lastBurned = mission.Burned;
                if (_pendingProgress && active) { _objectiveUntil = now + 4f; _pendingProgress = false; }
                _objective.Label.text = (_lastBurned == 0 ? L("PURGE PROTOCOL", "PURGE PROTOCOL") : L("ĐÃ THANH TẨY", "PURGED"))
                    + $"  {_lastBurned}/{AlienHuntMission.AlienCount}\n"
                    + L("[J] Nhiệm vụ", "[J] Objectives");
                Fade(_objective, active && now < _objectiveUntil);
                UpdateHeld(mission, active);
                UpdateMissionDetails(mission);
            }
            else { Fade(_objective, false); Fade(_held, false); }

            UpdateVitals(active, visible, now);
            bool showTeam = _player != null && (visible || spectating) && !ended && !PlayerInteractionControlLock.HasModal;
            UpdateTeam(showTeam, now);
            bool hit = active && ValidUv && _uv.ResonanceActive && _uv.ResonanceHasValidHit;
            _uvHit.Label.text = L("UV · CONTACT", "UV · CONTACT");
            Fade(_uvHit, hit);
            bool revive = active && _interactor != null && _interactor.CurrentReviveTarget != null;
            bool placementRejected = active && mission != null && mission.LocalPlacementRejected;
            _notice.Icon.Kind = placementRejected ? PurgeIcon.Danger : PurgeIcon.Medkit;
            _notice.Label.text = placementRejected ? L("Không thể thả · Tìm mặt đất có lối về ổ", "Cannot drop · Find ground connected to a nest")
                : L("CỨU TRỢ ĐỒNG ĐỘI", "REVIVING TEAMMATE");
            Fade(_notice, placementRejected || revive && _interactor.CurrentReviveTarget.IsReviveInProgress);
            // Priority transitions are immediate; ordinary contextual changes still fade.
            if (!visible)
                foreach (var group in _root.GetComponentsInChildren<CanvasGroup>())
                    if (group != _missions.Group && !(showTeam && _teamRows.Exists(row => row.Group == group)))
                        group.alpha = 0f;
            LayoutCorners();
        }

        private void LayoutCorners()
        {
            const float edge = 12f;
            const float gap = 8f;
            float equipmentHeight = 0f;
            if (_equipment != null)
            {
                var rect = _equipment.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-edge, edge);
                if (_equipment.gameObject.activeInHierarchy && _equipment.alpha > .01f)
                    foreach (Transform slot in _equipment.transform)
                        if (slot.gameObject.activeInHierarchy && (slot.name == "Slot1" || slot.name == "Slot2" || slot.name == "ToolSlot"))
                            equipmentHeight = Mathf.Max(equipmentHeight, slot.GetComponent<RectTransform>().rect.height + gap);
            }
            _held.Group.GetComponent<RectTransform>().anchoredPosition = new Vector2(-edge, edge + equipmentHeight);

            float bottom = edge;
            foreach (var card in new[] { _stamina, _health })
            {
                var rect = card.Group.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(edge, bottom);
                if (card.Group.alpha > .01f) bottom += rect.rect.height + gap;
            }
            foreach (var card in _teamRows)
            {
                var rect = card.Group.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(edge, bottom);
                if (card.Group.alpha > .01f) bottom += rect.rect.height + gap;
            }
        }

        private bool ValidUv => _uv != null && _uv.Object != null && _uv.Object.IsValid;

        private void UpdateVitals(bool active, bool visible, float now)
        {
            if (ValidUv && (_uv.ResonanceActive || _uv.ResonanceEnergy < 99.9f || _uv.ResonanceLocked)) _uvUntil = now + 2f;
            float energy = ValidUv ? Mathf.Clamp01(_uv.ResonanceEnergy / 100f) : 1f;
            _uvMeter.Label.text = _uv != null && ValidUv && _uv.ResonanceLocked
                ? L("UV · ĐANG HỒI", "UV · RECOVERING") : $"UV [Q]   {Mathf.RoundToInt(energy * 100)}%";
            Meter(_uvMeter, energy, energy <= .15f ? HUDPresentationStyle.Danger : new Color(.75f,.63f,.91f));
            Fade(_uvMeter, active && ValidUv && MeterVisible(energy, _uv.ResonanceActive || _uv.ResonanceLocked, now, _uvUntil));

            float stamina = _networkMovement != null ? _networkMovement.CurrentStamina / Mathf.Max(1f, _networkMovement.MaxStamina)
                : _movement != null ? _movement.CurrentStamina / Mathf.Max(1f, _movement.MaxStamina) : 1f;
            if (stamina < .999f) _staminaUntil = now + 2f;
            _stamina.Label.text = L("THỂ LỰC", "STAMINA");
            Meter(_stamina, stamina, stamina < .15f ? HUDPresentationStyle.Warning : HUDPresentationStyle.Accent);
            Fade(_stamina, active && MeterVisible(stamina, false, now, _staminaUntil));

            if (_down == null) { Fade(_health, false); return; }
            float health = _down.Health / Mathf.Max(1f, _down.MaxHealth);
            if (_lastHealth >= 0 && !Mathf.Approximately(_lastHealth, health)) _healthUntil = now + 4f;
            _lastHealth = health;
            bool downed = _down.IsDowned;
            _health.Icon.Kind = downed ? PurgeIcon.Downed : PurgeIcon.Health;
            _health.Label.text = downed ? L("CẦN CỨU", "DOWNED") + $"   {Mathf.CeilToInt(_down.BleedoutRemaining)}s"
                : (health <= .35f ? L("NGUY KỊCH", "LOW HEALTH") : L("SỨC KHỎE", "HEALTH")) + $"   {Mathf.RoundToInt(health*100)}%";
            Meter(_health, downed ? _down.Bleedout01 : health, downed || health <= .35f ? HUDPresentationStyle.Danger : HUDPresentationStyle.Ink);
            Fade(_health, visible && (downed || active && MeterVisible(health, false, now, _healthUntil)));
        }

        private void UpdateHeld(AlienHuntMission mission, bool active)
        {
            int index = mission.LocalHeldIndex;
            bool carrying = index >= 0 && index < mission.Actors.Length && mission.Actors[index] != null;
            if (carrying)
            {
                var kind = mission.Actors[index].Kind;
                _held.Icon.Kind = kind == HuntKind.Meat ? PurgeIcon.Meat : kind == HuntKind.Jerrycan ? PurgeIcon.Fuel : PurgeIcon.Alien;
                string name = kind == HuntKind.Meat ? L("THỊT MỒI", "BAIT") : kind == HuntKind.Jerrycan ? L("NHIÊN LIỆU", "FUEL") : L("ALIEN NON", "YOUNG ALIEN");
                _held.Label.text = name + "\n" + (kind == HuntKind.Meat ? L("[G] Đặt mồi", "[G] Place bait") : L("[G] Thả", "[G] Drop"));
            }
            Fade(_held, active && carrying);
        }

        private void UpdateTeam(bool visible, float now)
        {
            if (now >= _nextTeamRefresh)
            {
                _nextTeamRefresh = now + .75f;
                _teammates.Clear();
                foreach (var down in FindObjectsByType<PlayerDownState>())
                {
                    if (down == _down) continue;
                    var obj = down.GetComponentInParent<NetworkObject>();
                    var identity = down.GetComponent<LobbyPlayerState>();
                    if (obj != null && (!obj.IsValid || identity == null || !identity.IsGameplayPlayer)) continue;
                    _teammates.Add(down);
                }
                _teammates.Sort((a,b) => string.CompareOrdinal(a.GetEntityId().ToString(), b.GetEntityId().ToString()));
            }
            string signature = "";
            for (int i = 0; i < _teamRows.Count; i++)
            {
                var row = _teamRows[i];
                var down = i < _teammates.Count ? _teammates[i] : null;
                if (down == null) { Fade(row, false); continue; }
                var identity = down.GetComponent<LobbyPlayerState>();
                string name = identity != null && identity.Object != null && identity.Object.IsValid ? identity.OperatorName.ToString() : L("Đồng đội", "Teammate");
                bool critical = down.IsDowned || down.State == PlayerLifeState.Caught;
                row.Icon.Kind = down.IsDowned ? PurgeIcon.Downed : PurgeIcon.Team;
                row.Label.text = name + "  ·  " + (down.IsDowned ? L("Cần cứu", "Downed") + $" {Mathf.CeilToInt(down.BleedoutRemaining)}s"
                    : down.IsEliminated ? L("Đã bị loại", "Eliminated") : down.State == PlayerLifeState.Caught ? L("Bị bắt", "Caught") : L("Đang hoạt động", "Active"));
                row.Icon.color = critical ? HUDPresentationStyle.Danger : HUDPresentationStyle.Muted;
                signature += down.GetEntityId() + ":" + down.State + ";";
            }
            if (signature != _teamSignature) { _teamSignature = signature; _teamUntil = now + 3f; }
            for (int i = 0; i < _teamRows.Count && i < _teammates.Count; i++)
            {
                var down = _teammates[i];
                if (down != null) Fade(_teamRows[i], visible && (now < _teamUntil || down.IsDowned || down.State == PlayerLifeState.Caught));
            }
        }

        private void UpdateMissionDetails(AlienHuntMission mission)
        {
            if (!_missionLock.IsLocked) return;
            string interact = AlienHuntMission.InteractKey;
            string title = $"PURGE PROTOCOL    {mission.Burned}/{AlienHuntMission.AlienCount}\n\n";
            if (_missionPage == 0)
            {
                _missions.Label.text = title + L("THANH TẨY 9 ALIEN NON", "PURGE 9 YOUNG ALIENS") + "\n\n"
                    + L($"1. Nhặt thịt [{interact}], đặt mồi [G].\n2. Bắt alien khi đang ăn hoặc bị choáng [{interact}].\n3. Mang alien tới một lò còn chỗ.\n4. Nạp can nhiên liệu và đặt alien [{interact}].\n5. Châm lửa [{interact}] rồi tìm mục tiêu tiếp theo.",
                        $"1. Pick up meat [{interact}], place bait [G].\n2. Catch an eating or stunned alien [{interact}].\n3. Carry it to a furnace with space.\n4. Add fuel and place the alien [{interact}].\n5. Ignite [{interact}], then find the next target.")
                    + "\n\n" + L("[Q] UV khống chế mục tiêu · Theo dõi năng lượng.\n[G] Thả vật · Mỗi lò thanh tẩy tối đa 3 alien.",
                        "[Q] UV disrupts targets · Watch its charge.\n[G] Drop item · Each furnace purges up to 3 aliens.");
                if (_teammates.Count > 0) _missions.Label.text += "\n" + L("Nhặt hộp sơ cứu để giúp đồng đội bị hạ.", "Find a first aid kit to help a downed teammate.");
            }
            else
            {
                _missions.Label.text = title + L("TRẠNG THÁI LÒ", "FURNACE STATUS") + "\n\n";
                for (int i = 0; i < mission.Furnaces.Length; i++)
                {
                    var state = mission.ReadFurnace(i);
                    string status = state.BurnedCount >= AlienHuntMission.FurnaceCapacity ? L("Đã đầy", "Complete")
                        : state.BurnRemaining > 0 ? L("Đang đốt", "Burning") + $" · {Mathf.CeilToInt(state.BurnRemaining)}s"
                        : (state.Fuel > 0 ? L("Có nhiên liệu", "Fueled") : L("Thiếu nhiên liệu", "Needs fuel"))
                            + " · " + (state.AlienSlot > 0 ? L("Có alien", "Alien ready") : L("Chưa có alien", "Needs alien"));
                    _missions.Label.text += L("Lò ", "Furnace ") + (i+1) + $"   {state.BurnedCount}/{AlienHuntMission.FurnaceCapacity}\n{status}\n\n";
                }
            }
            _missions.Label.text += "\n\n" + L("[Tab] Đổi trang    [J / Esc] Đóng", "[Tab] Next page    [J / Esc] Close");
        }

        private void CloseMissions()
        {
            _missionLock.Release();
            if (_missions != null) _missions.Group.alpha = 0f;
        }
        private void OnDisable() => CloseMissions();
        private void OnDestroy()
        {
            CloseMissions();
            if (_root == null) return;
            if (Application.isPlaying) Destroy(_root);
            else DestroyImmediate(_root);
        }

        private void Build()
        {
            _font = GetComponentInChildren<Text>(true)?.font;
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _root = new GameObject("PurgeHUD", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var root = (RectTransform)_root.transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            _objective = MakeCard("PurgeProgress", new Vector2(0,1), new Vector2(12,-12), new Vector2(280,70), PurgeIcon.Purge);
            if (!IsPurgeScene)
            {
                _objective.Group.GetComponent<RectTransform>().sizeDelta = new Vector2(390, 126);
                _objective.Label.rectTransform.sizeDelta = new Vector2(330, 110);
            }
            _objective.Icon.color = HUDPresentationStyle.Warning;
            _uvMeter = MakeCard("PurgeUV", new Vector2(1,1), new Vector2(-12,-12), new Vector2(216,66), PurgeIcon.UV, true);
            _health = MakeCard("PurgeHealth", Vector2.zero, new Vector2(32,96), new Vector2(280,62), PurgeIcon.Health, true);
            _stamina = MakeCard("PurgeStamina", Vector2.zero, new Vector2(32,26), new Vector2(280,60), PurgeIcon.Stamina, true);
            _held = MakeCard("PurgeHeldItem", new Vector2(1,0), new Vector2(-32,138), new Vector2(240,64), PurgeIcon.Item);
            _uvHit = MakeCard("PurgeUVContact", new Vector2(.5f,.5f), new Vector2(0,-92), new Vector2(190,38), PurgeIcon.UV);
            _uvHit.Icon.color = new Color(.75f,.63f,.91f);
            _notice = MakeCard("PurgeReviving", new Vector2(.5f,0), new Vector2(0,230), new Vector2(440,42), PurgeIcon.Medkit);
            for (int i = 0; i < 3; i++)
                _teamRows.Add(MakeCard("PurgeTeammate" + i, Vector2.zero, new Vector2(32,184 + i*48), new Vector2(300,42), PurgeIcon.Team));
            _missions = MakeCard("PurgeMissions", new Vector2(.5f,.5f), Vector2.zero, new Vector2(610,590), PurgeIcon.Missions);
            _missions.Label.rectTransform.anchoredPosition = new Vector2(28,-28);
            _missions.Label.rectTransform.sizeDelta = new Vector2(554,530);
            _missions.Label.alignment = TextAnchor.UpperLeft;
            _missions.Label.fontSize = 18;
            _missions.Label.lineSpacing = 1.15f;
            _missions.Icon.rectTransform.anchoredPosition = new Vector2(554,-28);
            _root.transform.SetAsLastSibling();
        }

        private Card MakeCard(string name, Vector2 anchor, Vector2 position, Vector2 size, PurgeIcon icon, bool meter = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(_root.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var background = go.GetComponent<Image>(); background.color = new Color(.025f,.04f,.038f,.86f); background.raycastTarget = false;
            var card = new Card { Group = Group(go) };
            card.Group.alpha = 0f;
            card.Icon = AddIcon(rect, icon, new Vector2(14,-14), HUDPresentationStyle.Ink);
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(rect, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = textRect.anchorMax = textRect.pivot = new Vector2(0,1);
            textRect.anchoredPosition = new Vector2(48,-8);
            textRect.sizeDelta = new Vector2(size.x-60, meter ? 34 : size.y-16);
            card.Label = textGo.GetComponent<Text>();
            card.Label.font = _font; card.Label.fontSize = 15; card.Label.color = HUDPresentationStyle.Ink;
            card.Label.alignment = TextAnchor.MiddleLeft; card.Label.raycastTarget = false;
            card.Label.verticalOverflow = VerticalWrapMode.Truncate;
            if (meter)
            {
                var track = new GameObject("Track", typeof(RectTransform), typeof(Image)); track.transform.SetParent(rect,false);
                var tr = (RectTransform)track.transform; tr.anchorMin = tr.anchorMax = tr.pivot = Vector2.zero;
                tr.anchoredPosition = new Vector2(14,12); tr.sizeDelta = new Vector2(size.x-28,4);
                track.GetComponent<Image>().color = HUDPresentationStyle.Track; track.GetComponent<Image>().raycastTarget = false;
                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(tr,false);
                var fr = (RectTransform)fill.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
                fr.offsetMin = fr.offsetMax = Vector2.zero; fr.pivot = Vector2.zero;
                card.Fill = fill.GetComponent<Image>(); card.Fill.raycastTarget = false;
            }
            return card;
        }

        internal static HUDPurgeIcon AddIcon(Transform parent, PurgeIcon kind, Vector2 position, Color color)
        {
            var go = new GameObject("Icon_" + kind, typeof(RectTransform), typeof(CanvasRenderer), typeof(HUDPurgeIcon));
            go.transform.SetParent(parent,false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(24,24);
            var icon = go.GetComponent<HUDPurgeIcon>(); icon.Kind = kind; icon.color = color; icon.raycastTarget = false;
            return icon;
        }
        private static CanvasGroup Group(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false; return group;
        }
        private static void HideLegacy(Component module) { if (module != null) module.gameObject.SetActive(false); }
        private static void Meter(Card card, float amount, Color color)
        {
            card.Fill.rectTransform.localScale = new Vector3(Mathf.Clamp01(amount),1,1);
            card.Fill.color = color; card.Icon.color = color;
        }
        private static void Fade(Card card, bool visible)
            => card.Group.alpha = Mathf.MoveTowards(card.Group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        private static string L(string vi, string en) => GameLanguage.Choose(vi,en);
    }
}
