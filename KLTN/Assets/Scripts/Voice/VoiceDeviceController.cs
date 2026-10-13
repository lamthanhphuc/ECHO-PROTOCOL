using System;
using UnityEngine;

namespace EchoProtocol.Voice
{
    /// <summary>Local device selection and a microphone test which never enters the Voice transport.</summary>
    public sealed class VoiceDeviceController : MonoBehaviour
    {
        public const string SystemDefault = "System default";
        public event Action DeviceChanged;
        public string CaptureDevice => Selected == SystemDefault ? null : Selected;
        private float _retryAfter;
        public string[] Devices { get; private set; } = Array.Empty<string>();
        public string Selected { get; private set; }
        public string Error { get; private set; } = "";
        public bool Ready => !string.IsNullOrEmpty(Selected) && Array.IndexOf(Devices, Selected) >= 0 && string.IsNullOrEmpty(Error);
        public bool Testing => _test != null;
        public float TestLevel { get; private set; }
        private AudioClip _test;
        private float _nextRefresh, _testStarted;
        private readonly float[] _samples = new float[256];

        private void Awake()
        {
            Selected = PlayerPrefs.GetString("Echo.Voice.Device", "");
            Refresh();
        }

        public void Refresh()
        {
            string[] actual;
            try { actual = Microphone.devices; }
            catch (Exception) { Devices = Array.Empty<string>(); ReportCaptureFailure(); return; }
            string signature = string.Join("|", Devices);
            Devices = actual.Length == 0 ? Array.Empty<string>() : new string[actual.Length + 1];
            if (actual.Length > 0) { Devices[0] = SystemDefault; Array.Copy(actual, 0, Devices, 1, actual.Length); }
            string preferred = PlayerPrefs.GetString("Echo.Voice.Device", SystemDefault);
            string next = Array.IndexOf(Devices, preferred) >= 0 ? preferred : actual.Length > 0 ? SystemDefault : "";
            if (Selected != next || signature != string.Join("|", Devices))
            {
                StopTest(); Selected = next; Error = ""; _retryAfter = 0; DeviceChanged?.Invoke();
            }
            if (actual.Length == 0) Error = "No microphone available. Connect a device and check microphone permissions.";
            else if (Time.unscaledTime >= _retryAfter) Error = "";
        }

        public void Select(string device)
        {
            StopTest();
            if (Array.IndexOf(Devices, device) < 0) return;
            Selected = device;
            Error = "";
            PlayerPrefs.SetString("Echo.Voice.Device", device);
            _retryAfter = 0;
            DeviceChanged?.Invoke();
        }

        public void StartTest()
        {
            StopTest();
            if (!Ready) return;
            try
            {
                _test = Microphone.Start(CaptureDevice, true, 1, 16000);
                _testStarted = Time.unscaledTime;
                if (_test == null) Error = "Microphone could not start. Check device and Windows permissions.";
            }
            catch (Exception) { Error = "Microphone could not start. Check device and Windows permissions."; StopTest(); }
        }

        public void StopTest()
        {
            if (_test != null)
            {
                Microphone.End(CaptureDevice);
                Destroy(_test);
                _test = null;
            }
            TestLevel = 0;
        }

        public void ReportCaptureFailure()
        {
            _retryAfter = Time.unscaledTime + 5f;
            Error = "No microphone capture. Check Windows permissions, then select the device to retry.";
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefresh) { _nextRefresh = Time.unscaledTime + 1; Refresh(); }
            if (!Testing) return;
            int position = Microphone.GetPosition(CaptureDevice);
            if (position >= _samples.Length && _test.GetData(_samples, position - _samples.Length))
            {
                float peak = 0;
                foreach (float sample in _samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                TestLevel = peak;
            }
            if (position <= 0 && Time.unscaledTime - _testStarted > 3) { StopTest(); ReportCaptureFailure(); }
            else if (Time.unscaledTime - _testStarted > 10 && !VoiceSettingsPanel.IsOpen) StopTest();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) StopTest(); }
        private void OnDisable() => StopTest();
    }
}
