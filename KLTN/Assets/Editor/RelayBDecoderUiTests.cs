using EchoProtocol.RelayB;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.RelayB.Tests
{
    public sealed class RelayBDecoderUiTests
    {
        private Scene _scene;
        private GameObject _root;
        private RelayBController _controller;
        private RelayBUIController _ui;
        [SetUp] public void Setup()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Imported/RelayB.prefab");
            _root = Object.Instantiate(prefab.GetComponentInChildren<RelayBUIController>(true).gameObject);
            SceneManager.MoveGameObjectToScene(_root, _scene);
            var owner = new GameObject("DecoderUiTestOwner"); SceneManager.MoveGameObjectToScene(owner, _scene);
            _controller = owner.AddComponent<RelayBController>();
            var so = new SerializedObject(_controller); so.FindProperty("config").objectReferenceValue = prefab.GetComponent<RelayBController>().Config;
            so.ApplyModifiedPropertiesWithoutUndo();
            _controller.Simulation.Initialize(_controller.Config, 2, true, 441);
            _controller.Simulation.ScanSpectrum();
            _controller.Simulation.SelectChannel(_controller.Simulation.GetCurrentPreset().CorrectChannelIndex);
            _controller.Simulation.SetActiveTab(1);
            _ui = _root.GetComponent<RelayBUIController>(); _ui.Bind(_controller); _ui.Refresh(_controller.Snapshot);
        }
        [TearDown] public void Cleanup() { if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }
        private Button Button(string name)
        {
            foreach (var button in _root.GetComponentsInChildren<Button>(true)) if (button.name == name) return button;
            throw new System.Exception("Missing button " + name);
        }
        private string Digit(int slot) => Button("CodeSlot_" + slot).transform.Find("Digit").GetComponent<TMP_Text>().text;
        private void Fill() { foreach (int digit in new[] { 8, 6, 3, 1, 5, 4 }) Button("Signal_" + digit).onClick.Invoke(); }

        [Test] public void AutoAdvanceDisablesOnlyCurrentlyUsedSignalsAndEnablesTransmitAtSix()
        {
            Assert.IsFalse(Button("Transmit").interactable);
            Button("Signal_8").onClick.Invoke(); Button("Signal_6").onClick.Invoke();
            Assert.That(Digit(0), Is.EqualTo("8")); Assert.That(Digit(1), Is.EqualTo("6")); Assert.That(Digit(2), Is.EqualTo("?"));
            Assert.IsFalse(Button("Signal_8").interactable); Assert.IsTrue(Button("Signal_9").interactable);
            Assert.That(Button("Signal_8").GetComponent<CanvasGroup>().alpha, Is.EqualTo(0.35f));
            foreach (int digit in new[] { 3, 1, 5, 4 }) Button("Signal_" + digit).onClick.Invoke();
            Assert.IsTrue(Button("Transmit").interactable);
        }
        [Test] public void EditingFilledSlotReturnsDigitToBankAndDoesNotSpendAttempt()
        {
            Fill(); Button("CodeSlot_2").onClick.Invoke();
            Assert.That(Digit(2), Is.EqualTo("?")); Assert.IsTrue(Button("Signal_3").interactable);
            Assert.IsFalse(Button("Transmit").interactable); Button("Signal_9").onClick.Invoke();
            Assert.That(Digit(2), Is.EqualTo("9")); Assert.IsTrue(Button("Transmit").interactable);
            Assert.That(_controller.Snapshot.Decoder.Attempts, Is.Zero);
        }
        [Test] public void ResetClearsInputNotHistoryAndHistoryDoesNotEliminateBank()
        {
            var codes = new[] { 0x654321, 0, 0, 0, 0 };
            _controller.Simulation.Decoder.ApplyAuthoritative(new RelayBDecodeSnapshot(11, 1, 0, 0, RelayBDecodePhase.Editing, 0, codes, new int[5]));
            _ui.Refresh(_controller.Snapshot); Fill(); Button("ResetInput").onClick.Invoke();
            for (int i = 0; i < 6; i++) Assert.That(Digit(i), Is.EqualTo("?"));
            for (int i = 1; i <= 9; i++) Assert.IsTrue(Button("Signal_" + i).interactable);
            Assert.That(_controller.Snapshot.Decoder.Codes[0], Is.EqualTo(codes[0])); Assert.That(_controller.Snapshot.Decoder.Attempts, Is.EqualTo(1));
        }
        [Test] public void TransmitLocksInputAndKeepsSixDigitsDuringReveal()
        {
            Fill(); Button("Transmit").onClick.Invoke();
            Assert.That(_controller.Snapshot.Decoder.Attempts, Is.EqualTo(1)); Assert.IsFalse(Button("Transmit").interactable);
            Assert.IsFalse(Button("ResetInput").interactable); Assert.IsFalse(Button("CodeSlot_0").interactable);
            Assert.That(Digit(0), Is.EqualTo("8")); _controller.Simulation.Tick(1.21f); _ui.Refresh(_controller.Snapshot);
            Assert.That(Digit(5), Is.EqualTo("4")); Assert.That(_controller.Snapshot.Decoder.RevealedCount, Is.EqualTo(3));
        }
        [Test] public void CompletionOffersContinueWithoutAutoSyncOrAutoNavigation()
        {
            _controller.Simulation.Decoder.ApplyAuthoritative(new RelayBDecodeSnapshot(11, 1, 0x491638, 0xaaa,
                RelayBDecodePhase.Complete, 0, new int[5], new int[5]));
            _ui.Refresh(_controller.Snapshot);
            Assert.IsTrue(Button("DecodeContinue").gameObject.activeSelf); Assert.IsFalse(Button("Transmit").gameObject.activeSelf);
            Assert.That(_controller.Snapshot.ActiveTab, Is.EqualTo(1)); Assert.IsFalse(_controller.Simulation.IsSynchronizing);
            Button("DecodeContinue").onClick.Invoke(); _ui.Refresh(_controller.Snapshot);
            Assert.That(_controller.Snapshot.ActiveTab, Is.EqualTo(2)); Assert.IsFalse(_controller.Simulation.IsSynchronizing);
        }
        [Test] public void PrefabHasCompactTerminalAndNoObsoleteDspSurface()
        {
            var panel = (GameObject)new SerializedObject(_ui).FindProperty("panelRoot").objectReferenceValue;
            Assert.That(panel.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(1160, 680)));
            Assert.IsNull(_root.transform.Find("PanelRoot/ProcessingTabPanel/PipelineModulesBox"));
            Assert.That(_root.GetComponentsInChildren<RelayBFeedbackGraphic>(true).Length, Is.EqualTo(39));
        }
    }
}
