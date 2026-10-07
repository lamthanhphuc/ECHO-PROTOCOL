using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EchoProtocol.RelayB
{
    public sealed partial class RelayBUIController
    {
        [Header("Terminal / Decoder")]
        [SerializeField] private TMP_Text stageLabel;
        [SerializeField] private TMP_Text findNotice;
        [SerializeField] private Button findContinue;
        [SerializeField] private Button[] codeSlots = new Button[6];
        [SerializeField] private TMP_Text[] codeDigits = new TMP_Text[6];
        [SerializeField] private TMP_Text[] codeFeedback = new TMP_Text[6];
        [SerializeField] private Image[] codeEdges = new Image[6];
        [SerializeField] private RelayBFeedbackGraphic[] codeIcons = new RelayBFeedbackGraphic[6];
        [SerializeField] private RelayBFeedbackGraphic[] historyIcons = new RelayBFeedbackGraphic[30];
        [SerializeField] private Image[] attemptDots = new Image[5];
        [SerializeField] private Button[] signalBank = new Button[9];
        [SerializeField] private TMP_Text[] historyDigits = new TMP_Text[30];
        [SerializeField] private TMP_Text[] historyFeedback = new TMP_Text[30];
        [SerializeField] private Image[] historyCells = new Image[30];
        [SerializeField] private Button transmitButton;
        [SerializeField] private Button resetInputButton;
        [SerializeField] private Button decodeContinue;
        [SerializeField] private TMP_Text decodeNotice;
        [SerializeField] private RelayBWaveformRenderer decodeAnalyzer;
        [SerializeField] private Image decodeConnection;
        private readonly int[] _guess = new int[6];
        private int _selectedSlot;
        private int _decodeRound = -1;
        private int _decodeAttempts = -1;
        private int _lastRevealed;
        private RelayBDecodePhase _lastDecodePhase;
        private float _transmitPendingUntil;

        private void FitTerminal()
        {
            if (panelRoot == null || !(panelRoot.transform.parent is RectTransform parent)) return;
            var rect = panelRoot.GetComponent<RectTransform>();
            if (parent.rect.width <= 0 || parent.rect.height <= 0) return;
            float scale = Mathf.Min(1f, parent.rect.width * 0.92f / 1160f, parent.rect.height * 0.88f / 680f);
            rect.localScale = new Vector3(scale, scale, 1f);
        }

        private void HookDecoderControls()
        {
            for (int i = 0; i < codeSlots.Length; i++)
            {
                int slot = i;
                if (codeSlots[i] == null) continue;
                codeSlots[i].onClick.RemoveAllListeners();
                codeSlots[i].onClick.AddListener(() => SelectCodeSlot(slot));
            }
            for (int i = 0; i < signalBank.Length; i++)
            {
                int digit = i + 1;
                if (signalBank[i] == null) continue;
                signalBank[i].onClick.RemoveAllListeners();
                signalBank[i].onClick.AddListener(() => InsertCodeDigit(digit));
            }
            BindDecoderButton(transmitButton, TransmitGuess);
            BindDecoderButton(resetInputButton, ResetInput);
            BindDecoderButton(findContinue, () => _controller?.SetActiveTab(1));
            BindDecoderButton(decodeContinue, () => _controller?.SetActiveTab(2));
        }

        private static void BindDecoderButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(callback);
        }

        private bool CanEditCode => _controller != null && _controller.Simulation.ActiveTab == 1
            && _controller.Simulation.IsSignalFound && _controller.Simulation.Decoder.CanEdit
            && Time.unscaledTime >= _transmitPendingUntil
            && (!TryGetNetworkDirector(out var director) || director.CanLocalPlayerOperateRelay(_controller));

        private void SelectCodeSlot(int slot)
        {
            if (!CanEditCode) return;
            _selectedSlot = slot;
            _guess[slot] = 0;
            _controller.PlayDecoderTick();
            RefreshDecoderPresentation(_controller.Snapshot);
        }

        private void InsertCodeDigit(int digit)
        {
            if (!CanEditCode || digit < 1 || digit > 9 || Array.IndexOf(_guess, digit) >= 0) return;
            _guess[_selectedSlot] = digit;
            for (int offset = 1; offset <= 6; offset++)
            {
                int next = (_selectedSlot + offset) % 6;
                if (_guess[next] != 0) continue;
                _selectedSlot = next; break;
            }
            _controller.PlayDecoderTick();
            RefreshDecoderPresentation(_controller.Snapshot);
        }

        private void ResetInput()
        {
            if (!CanEditCode) return;
            Array.Clear(_guess, 0, 6); _selectedSlot = 0;
            _controller.PlayDecoderTick(0);
            RefreshDecoderPresentation(_controller.Snapshot);
        }

        private void TransmitGuess()
        {
            if (!CanEditCode) return;
            int packed = 0;
            for (int i = 0; i < 6; i++) packed |= _guess[i] << (i * 4);
            if (!RelayBDecoder.IsValidCode(packed)) return;
            var state = _controller.Snapshot.Decoder;
            if (TryGetNetworkDirector(out var director))
            {
                if (!director.RequestRelayBTransmit(_controller, packed, state.Round, state.Attempts)) return;
                _transmitPendingUntil = Time.unscaledTime + 2f;
            }
            else _controller.TransmitCode(packed);
            RefreshDecoderPresentation(_controller.Snapshot);
        }

        private void RefreshProcessingTab(RelayBSnapshot snapshot, bool readOnly, bool canOperate) => RefreshDecoderPresentation(snapshot);

        private void RefreshDecoderPresentation(RelayBSnapshot snapshot)
        {
            var state = snapshot.Decoder;
            if (!snapshot.HasScanned || snapshot.SelectedChannelIndex < 0)
            {
                Array.Clear(_guess, 0, 6); _decodeRound = -1; _transmitPendingUntil = 0;
            }
            if (_decodeRound != state.Round)
            {
                Array.Clear(_guess, 0, 6); _selectedSlot = 0; _decodeRound = state.Round;
                _decodeAttempts = -1; _transmitPendingUntil = 0; _lastRevealed = 0;
            }
            if (state.Attempts != _decodeAttempts || state.Phase != _lastDecodePhase)
            {
                if (state.Phase == RelayBDecodePhase.Editing) { Array.Clear(_guess, 0, 6); _selectedSlot = 0; }
                _decodeAttempts = state.Attempts; _lastDecodePhase = state.Phase;
                if (!state.CanEdit) _transmitPendingUntil = 0;
                if (state.Phase == RelayBDecodePhase.Transmitting) _lastRevealed = 0;
            }
            bool stage2 = snapshot.ActiveTab == 1;
            bool edit = CanEditCode;
            int revealed = state.RevealedCount;
            if (stage2 && revealed > _lastRevealed && state.Phase != RelayBDecodePhase.Editing)
            {
                for (int i = _lastRevealed; i < revealed; i++) _controller?.PlayDecoderTick((int)RelayBDecoder.FeedbackAt(state.Feedback, i));
                _lastRevealed = revealed;
            }
            int packed = state.Current;
            bool completeInput = true;
            for (int i = 0; i < 6; i++)
            {
                int digit = state.CanEdit ? _guess[i] : RelayBDecoder.DigitAt(packed, i);
                completeInput &= digit != 0;
                bool showFeedback = !state.CanEdit && i < revealed;
                var feedback = RelayBDecoder.FeedbackAt(state.Feedback, i);
                Color accent = showFeedback ? DecodeColor(feedback) : referenceColor;
                SetText(codeDigits[i], digit == 0 ? "?" : digit.ToString());
                SetText(codeFeedback[i], showFeedback ? "" : (i + 1).ToString("00"));
                if (codeIcons[i] != null) { codeIcons[i].gameObject.SetActive(showFeedback); codeIcons[i].SetFeedback(feedback, accent); }
                if (codeDigits[i] != null) codeDigits[i].color = digit == 0 ? offlineColor : Color.white;
                if (codeFeedback[i] != null) codeFeedback[i].color = showFeedback ? accent : offlineColor;
                if (codeEdges[i] != null) codeEdges[i].color = showFeedback ? accent
                    : state.CanEdit && i == _selectedSlot ? referenceColor : new Color(0.18f, 0.23f, 0.25f);
                SetInteractable(codeSlots[i], edit);
                if (codeSlots[i] != null)
                {
                    if (codeSlots[i].TryGetComponent<Outline>(out var outline))
                    {
                        Color border = showFeedback ? accent : state.CanEdit && i == _selectedSlot ? referenceColor : offlineColor;
                        border.a = showFeedback || state.CanEdit && i == _selectedSlot ? 0.5f : 0.12f;
                        outline.effectColor = border;
                    }
                    bool pulse = state.Phase == RelayBDecodePhase.Transmitting && i == Mathf.Min(5, (int)(state.Elapsed / 0.15f));
                    codeSlots[i].GetComponent<Image>().color = pulse ? new Color(0.12f, 0.31f, 0.34f)
                        : showFeedback ? Color.Lerp(new Color(0.04f, 0.05f, 0.055f), accent, 0.16f) : new Color(0.065f, 0.082f, 0.09f);
                    float flip = state.Phase == RelayBDecodePhase.Revealing
                        ? Mathf.Clamp01(Mathf.Abs((state.Elapsed - i * 0.12f) / 0.12f - 0.5f) * 2f) : 1f;
                    codeDigits[i].rectTransform.localScale = new Vector3(state.Phase == RelayBDecodePhase.Revealing && i == revealed - 1 ? Mathf.Max(0.05f, flip) : 1f, 1f, 1f);
                }
            }
            for (int i = 0; i < 9; i++)
            {
                bool available = edit && Array.IndexOf(_guess, i + 1) < 0;
                SetInteractable(signalBank[i], available);
                if (signalBank[i] != null && signalBank[i].TryGetComponent<CanvasGroup>(out var group)) group.alpha = available ? 1f : 0.35f;
            }
            for (int i = 0; i < 5; i++)
            {
                if (attemptDots[i] != null)
                {
                    attemptDots[i].gameObject.SetActive(stage2);
                    attemptDots[i].color = i < 5 - state.Attempts ? referenceColor : new Color(0.18f, 0.23f, 0.25f);
                }
                for (int slot = 0; slot < 6; slot++)
                {
                    int index = i * 6 + slot;
                    int code = state.Codes?[i] ?? 0;
                    var result = RelayBDecoder.FeedbackAt(state.Results?[i] ?? 0, slot);
                    SetText(historyDigits[index], code == 0 ? "-" : RelayBDecoder.DigitAt(code, slot).ToString());
                    SetText(historyFeedback[index], "");
                    if (historyIcons[index] != null) { historyIcons[index].gameObject.SetActive(code != 0); historyIcons[index].SetFeedback(result, DecodeColor(result)); }
                    if (historyFeedback[index] != null) historyFeedback[index].color = DecodeColor(result);
                    if (historyDigits[index] != null) historyDigits[index].color = code == 0 ? offlineColor : Color.white;
                    if (historyCells[index] != null) historyCells[index].color = code == 0 ? new Color(0.07f, 0.08f, 0.085f)
                        : Color.Lerp(new Color(0.055f, 0.065f, 0.07f), DecodeColor(result), 0.12f);
                }
            }
            SetInteractable(transmitButton, edit && completeInput);
            if (resetInputButton != null) { resetInputButton.gameObject.SetActive(stage2 && !state.IsComplete); resetInputButton.interactable = edit; }
            if (transmitButton != null) transmitButton.gameObject.SetActive(!state.IsComplete);
            if (decodeContinue != null) decodeContinue.gameObject.SetActive(state.IsComplete);
            string notice = state.Phase == RelayBDecodePhase.Failed ? "OUT OF ATTEMPTS - NEW CODE NEXT"
                : state.Phase == RelayBDecodePhase.Solved || state.IsComplete ? "SIGNAL DECODED"
                : state.Phase == RelayBDecodePhase.Transmitting ? "TRANSMITTING"
                : state.Phase == RelayBDecodePhase.Revealing || state.Phase == RelayBDecodePhase.Holding ? "READING SIGNAL"
                : $"6 UNIQUE DIGITS / {RelayBDecoder.MaxAttempts - state.Attempts} ATTEMPTS LEFT";
            SetText(decodeNotice, notice);
            if (decodeNotice != null) { decodeNotice.enableAutoSizing = true; decodeNotice.fontSizeMin = 10f; }
            if (processingTabPanel != null)
            {
                RefreshDecodeLegend("LegendRight", "RIGHT\nCORRECT POSITION");
                RefreshDecodeLegend("LegendPlace", "PLACE\nMOVE THIS DIGIT");
                RefreshDecodeLegend("LegendUnused", "UNUSED\nNOT IN CODE");
            }
            if (decodeNotice != null) decodeNotice.color = state.Phase == RelayBDecodePhase.Failed ? dangerColor
                : state.IsComplete || state.Phase == RelayBDecodePhase.Solved ? safeColor : offlineColor;
            if (statusLabel != null && stage2) statusLabel.gameObject.SetActive(false);
            else if (statusLabel != null) statusLabel.gameObject.SetActive(true);
            int right = 0;
            for (int i = 0; i < 5; i++)
            {
                int count = 0;
                if ((state.Codes?[i] ?? 0) != 0)
                    for (int slot = 0; slot < 6; slot++)
                        if (RelayBDecoder.FeedbackAt(state.Results[i], slot) == RelayBCodeFeedback.Right) count++;
                right = Mathf.Max(right, count);
            }
            if (!state.CanEdit && revealed == 6) right = state.RightCount;
            right = Mathf.Clamp(right, 0, 6);
            if (decodeAnalyzer != null)
            {
                decodeAnalyzer.SetWaveParameters(WaveformType.Sine, 45f, 0f, 0.75f);
                decodeAnalyzer.SetNoise(state.Phase == RelayBDecodePhase.Failed ? 1f : 1f - right / 6f);
                decodeAnalyzer.SetWaveformColor(state.IsComplete || state.Phase == RelayBDecodePhase.Solved ? safeColor : referenceColor);
            }
            if (decodeConnection != null)
            {
                decodeConnection.gameObject.SetActive(state.Phase == RelayBDecodePhase.Solved || state.IsComplete);
                decodeConnection.fillAmount = state.IsComplete ? 1f : Mathf.Clamp01(state.Elapsed / 0.55f);
            }
        }

        private Color DecodeColor(RelayBCodeFeedback feedback) => feedback == RelayBCodeFeedback.Right ? safeColor
            : feedback == RelayBCodeFeedback.WrongPlace ? warningColor : offlineColor;

        private void RefreshDecodeLegend(string name, string explanation)
        {
            var label = processingTabPanel.transform.Find(name)?.GetComponent<TMP_Text>();
            if (label == null) return;
            label.text = explanation;
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 36f);
        }

        private void HandleDecoderKeyboard()
        {
            var keyboard = Keyboard.current;
            if (!CanEditCode || keyboard == null) return;
            var keys = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key,
                keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key };
            for (int i = 0; i < keys.Length; i++) if (keys[i].wasPressedThisFrame) InsertCodeDigit(i + 1);
            if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame) SelectCodeSlot(_selectedSlot);
            if (keyboard.leftArrowKey.wasPressedThisFrame) _selectedSlot = (_selectedSlot + 5) % 6;
            if (keyboard.rightArrowKey.wasPressedThisFrame) _selectedSlot = (_selectedSlot + 1) % 6;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) TransmitGuess();
        }
    }
}
