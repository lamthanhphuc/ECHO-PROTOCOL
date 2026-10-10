using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class HUDZoneMissions : MonoBehaviour
    {
        private GameObject _panel;
        private Text _body;
        private Text _title;
        private Text _closeHint;
        private GameObject _compactPanel;
        private Text _compactText;
        private EnergyCoreObjectiveProgress _cores;
        private SecurityTerminalDownload _terminal;
        private MatchFlowController _flow;
        private EscapeDoorCountdown _escape;

        private void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _panel = new GameObject("ZoneMissionsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)_panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 560f);
            _panel.GetComponent<Image>().color = new Color(0.045f, 0.055f, 0.055f, 0.96f);

            var label = new GameObject("MissionsText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(_panel.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(32f, 24f);
            labelRect.offsetMax = new Vector2(-32f, -76f);
            _body = label.GetComponent<Text>();
            _body.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _body.fontSize = 17;
            _body.color = HUDPresentationStyle.Ink;
            _body.alignment = TextAnchor.UpperLeft;
            _body.supportRichText = true;
            _body.raycastTarget = false;
            _title = CreateHeader("MissionsTitle", false);
            _closeHint = CreateHeader("MissionsCloseHint", true);
            _panel.SetActive(false);

            _compactPanel = new GameObject("ZoneMissionCompact", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _compactPanel.transform.SetParent(canvas.transform, false);
            var compactRect = (RectTransform)_compactPanel.transform;
            compactRect.anchorMin = compactRect.anchorMax = new Vector2(0f, 1f);
            compactRect.pivot = new Vector2(0f, 1f);
            compactRect.anchoredPosition = new Vector2(20f, -20f);
            compactRect.sizeDelta = new Vector2(310f, 76f);
            var compactBackground = _compactPanel.GetComponent<Image>();
            compactBackground.color = new Color(0.025f, 0.045f, 0.06f, 0.82f);
            compactBackground.raycastTarget = false;

            var compactLabel = new GameObject("ProgressText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            compactLabel.transform.SetParent(_compactPanel.transform, false);
            var compactLabelRect = (RectTransform)compactLabel.transform;
            compactLabelRect.anchorMin = Vector2.zero;
            compactLabelRect.anchorMax = Vector2.one;
            compactLabelRect.offsetMin = new Vector2(12f, 8f);
            compactLabelRect.offsetMax = new Vector2(-12f, -8f);
            _compactText = compactLabel.GetComponent<Text>();
            _compactText.font = _body.font;
            _compactText.fontSize = 14;
            _compactText.alignment = TextAnchor.MiddleLeft;
            _compactText.color = Color.white;
            _compactText.supportRichText = true;
            _compactText.raycastTarget = false;
        }

        private Text CreateHeader(string name, bool rightAligned)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(_panel.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(rightAligned ? 1f : 0f, 1f);
            rect.pivot = new Vector2(rightAligned ? 1f : 0f, 1f);
            rect.anchoredPosition = new Vector2(rightAligned ? -32f : 32f, -24f);
            rect.sizeDelta = new Vector2(rightAligned ? 180f : 340f, 32f);
            var text = go.GetComponent<Text>();
            text.font = _body.font;
            text.fontSize = rightAligned ? 13 : 22;
            text.fontStyle = rightAligned ? FontStyle.Normal : FontStyle.Bold;
            text.color = rightAligned ? HUDPresentationStyle.Muted : HUDPresentationStyle.Ink;
            text.alignment = rightAligned ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        private void Update()
        {
            if (_panel == null) return;
            RefreshText();
            if (PlayerInteractionControlLock.HasModal || PlayerInteractionControlLock.IsGameplayInputBlocked())
            {
                _panel.SetActive(false);
                _compactPanel.SetActive(false);
                return;
            }

            // The objective tracker owns the compact overview; J retains the full checklist.
            _compactPanel.SetActive(GetComponentInChildren<HUDObjectiveTracker>(true) == null);

            if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
                _panel.SetActive(!_panel.activeSelf);

        }

        private void RefreshText()
        {
            _title.text = EchoProtocol.Settings.GameLanguage.Choose("Nhiệm vụ", "Objectives");
            _closeHint.text = EchoProtocol.Settings.GameLanguage.Choose("[J] Đóng", "[J] Close");
            if (_cores == null) _cores = FindAnyObjectByType<EnergyCoreObjectiveProgress>();
            if (_terminal == null) _terminal = FindAnyObjectByType<SecurityTerminalDownload>();
            if (_flow == null) _flow = FindAnyObjectByType<MatchFlowController>();
            if (_escape == null) _escape = FindAnyObjectByType<EscapeDoorCountdown>();

            var match = NetworkMatchState.Instance;
            bool hasNetworkMatch = match != null
                && match.Object != null
                && match.Object.IsValid
                && match.Runner != null
                && match.Runner.IsRunning;

            int placed = 0;
            int required = 4;
            if (hasNetworkMatch
                && match.TryGetObjectiveProgress(out int networkPlaced, out int networkRequired))
            {
                placed = networkPlaced;
                required = Mathf.Max(1, networkRequired);
            }
            else if (_cores != null)
            {
                placed = _cores.PlacedCoreCount;
                required = Mathf.Max(1, _cores.RequiredCoreCount);
            }

            var director = Zone2MissionDirector.Instance;
            var stage = hasNetworkMatch
                ? match.Zone2Stage
                : director != null
                    ? director.CurrentStage
                    : Zone2MissionStage.Zone1CoreObjective;
            int relays = hasNetworkMatch
                ? match.CompletedRelayCount
                : director != null
                    ? director.CompletedRelayCount
                    : 0;
            int security = hasNetworkMatch
                ? Mathf.RoundToInt(match.SecurityHoldProgress01 * 100f)
                : _terminal != null
                    ? Mathf.RoundToInt(_terminal.Progress01 * 100f)
                    : 0;
            bool zone2Available = stage > Zone2MissionStage.Zone1CoreObjective;

            if (!zone2Available)
                _compactText.text = $"<color=#7EA6A4><b>Khu vực 01</b></color>  [J] Nhiệm vụ\nCore {placed}/{required}";
            else if (stage == Zone2MissionStage.FindSecurityTerminal)
                _compactText.text = "<color=#7EA6A4><b>Khu vực 02</b></color>  [J] Nhiệm vụ\nTìm trạm an ninh";
            else if (stage == Zone2MissionStage.RepairRelays)
                _compactText.text = $"<color=#7EA6A4><b>Khu vực 02</b></color>  [J] Nhiệm vụ\nRelay {relays}/4";
            else if (stage == Zone2MissionStage.SecurityHoldReady || stage == Zone2MissionStage.SecurityHold)
                _compactText.text = $"<color=#7EA6A4><b>Khu vực 02</b></color>  [J] Nhiệm vụ\nXác thực {security}%";
            else if (stage < Zone2MissionStage.Zone2Completed)
                _compactText.text = "<color=#7EA6A4><b>Khu vực 02</b></color>  [J] Nhiệm vụ\nMở bảng mở cửa";
            else
                _compactText.text = "<color=#7EA6A4><b>Khu vực 02</b></color>  [J] Nhiệm vụ\nHoàn thành";

            _body.text = $"<color=#7EA6A4><b>Khu vực 01 · Khôi phục nguồn điện</b></color>\n"
                + $"Tìm và lắp lõi năng lượng vào trạm cấp điện: {placed}/{required}"
                + (zone2Available ? "  ✓" : "");

            if (!zone2Available) return;

            _body.text += "\n\n<color=#7EA6A4><b>Khu vực 02 · An ninh</b></color>\n"
                + $"Tìm trạm an ninh: {Done(stage >= Zone2MissionStage.RepairRelays)}\n"
                + $"Sửa 4 relay: {relays}/4\n"
                + $"Xác thực bảo mật: {security}% {Done(stage >= Zone2MissionStage.AuthorizationCodeGranted)}\n"
                + $"Nhập mã tại bảng mở cửa: {Done(stage >= Zone2MissionStage.Zone2Completed)}";

            var zone3 = Zone3MissionDirector.Instance;
            MatchPhase phase = _flow != null ? _flow.Phase : MatchPhase.ExploreCore;
            bool escaping = hasNetworkMatch
                ? match.CurrentPhase == NetworkMatchPhase.FinalHunt || match.CurrentPhase == NetworkMatchPhase.Escape
                : phase == MatchPhase.FinalHunt || phase == MatchPhase.ExitCountdown;
            bool won = hasNetworkMatch ? match.Result == NetworkMatchResult.Win : phase == MatchPhase.Win;
            bool zone3Started = escaping || won || (hasNetworkMatch
                ? match.CurrentPhase == NetworkMatchPhase.Zone3FindFrigate || match.CurrentPhase == NetworkMatchPhase.Zone3PushFrigate
                : phase == MatchPhase.Zone3FindFrigate || phase == MatchPhase.Zone3PushFrigate);
            if (!zone3Started && stage < Zone2MissionStage.Zone2Completed) return;
            bool found = escaping || won || (hasNetworkMatch
                ? match.CurrentPhase == NetworkMatchPhase.Zone3PushFrigate
                : phase == MatchPhase.Zone3PushFrigate);
            bool docked = escaping || won || (zone3 != null && zone3.IsFrigateAtDestination);
            float charge = hasNetworkMatch ? match.Zone3ChargeProgress01
                : zone3 != null && zone3.ChargeStation != null ? zone3.ChargeStation.Progress01 : 0f;
            bool transferred = escaping || won || charge >= 0.999f;
            _body.text += "\n\n<color=#7EA6A4><b>Khu vực 03 · Nguồn dự phòng</b></color>\n"
                + $"Tìm tàu: {Done(found)} · Đưa tàu về bến: {Done(docked)}\n"
                + $"Chuyển nguồn điện: {(transferred ? "Hoàn tất" : Mathf.RoundToInt(charge * 100f) + "%")}";
            if (zone3 != null && zone3.Convoy != null && found && !docked)
                _body.text += "\n" + zone3.Convoy.FuelDisplay;
            _body.text += "\n\n<color=#7EA6A4><b>THOÁT HIỂM</b></color>\n";
            if (won) _body.text += "Đội đã sơ tán thành công.";
            else if ((hasNetworkMatch && match.IsEnded) || phase == MatchPhase.Lose)
                _body.text += "Nhiệm vụ đã kết thúc.";
            else if (escaping)
            {
                float remaining = hasNetworkMatch ? match.EscapeRemainingSeconds : _escape != null ? _escape.RemainingSeconds : 0f;
                int seconds = Mathf.CeilToInt(remaining);
                _body.text += seconds > 0 ? $"Trở về cửa thoát · Còn {seconds / 60:00}:{seconds % 60:00}"
                    : "Trở về cửa thoát để sơ tán.";
            }
            else _body.text += "Cấp điện để mở lối sơ tán.";
            if (zone3Started) _compactText.text = escaping ? "THOÁT HIỂM\nTrở về cửa thoát"
                : won ? "HOÀN THÀNH\nĐội đã sơ tán" : "Khu vực 03\n" + (docked ? "Chuyển nguồn điện" : "Hộ tống tàu");
        }

        private static string Done(bool completed) => completed ? "✓" : "Chưa hoàn thành";

        private void OnDestroy()
        {
            if (_panel != null) Destroy(_panel);
            if (_compactPanel != null) Destroy(_compactPanel);
        }
    }
}
