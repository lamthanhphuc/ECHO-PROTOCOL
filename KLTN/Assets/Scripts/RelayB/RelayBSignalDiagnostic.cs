using System;
using UnityEngine;

namespace EchoProtocol.RelayB
{
    public enum RelayBModuleType
    {
        None,
        BandPass,
        Notch,
        Gain,
        Attenuator,
        NoiseSuppressor
    }

    [Serializable]
    public sealed class RelayBCandidate
    {
        [SerializeField] private int channelIndex;
        [SerializeField] private float[] peaks;
        [SerializeField] private float distortionPercent;
        [SerializeField] private float symbolRateKbaud;
        [SerializeField] private WaveformType waveform = WaveformType.Sine;
        [SerializeField] private string pilotFrame = "10110100";
        [SerializeField] private bool hasSpur;
        [SerializeField] private float spurFrequencyKhz;
        [SerializeField] private string notes = "";

        public RelayBCandidate() { }

        public RelayBCandidate(
            int ch,
            float[] peakList,
            float dist,
            float baud,
            WaveformType wave,
            string pilot,
            bool spur,
            float spurFreq,
            string noteText)
        {
            channelIndex = ch;
            peaks = peakList ?? Array.Empty<float>();
            distortionPercent = dist;
            symbolRateKbaud = baud;
            waveform = wave;
            pilotFrame = pilot;
            hasSpur = spur;
            spurFrequencyKhz = spurFreq;
            notes = noteText;
        }

        public int ChannelIndex => channelIndex;
        public float[] Peaks => peaks ?? Array.Empty<float>();
        public float DistortionPercent => distortionPercent;
        public float SymbolRateKbaud => symbolRateKbaud;
        public WaveformType Waveform => waveform;
        public string PilotFrame => pilotFrame ?? "00000000";
        public bool HasSpur => hasSpur;
        public float SpurFrequencyKhz => spurFrequencyKhz;
        public string Notes => notes;
    }

    [Serializable]
    public sealed class RelayBReferenceProfile
    {
        [SerializeField] private float fundamentalMinKhz = 65f;
        [SerializeField] private float fundamentalMaxKhz = 70f;
        [SerializeField] private float targetFundamentalKhz = 68f;
        [SerializeField] private float targetPhaseDegrees = 90f;
        [SerializeField] private bool secondHarmonicRequired = true;
        [SerializeField] private float maxDistortionPercent = 10f;
        [SerializeField] private float symbolRateMin = 8f;
        [SerializeField] private float symbolRateMax = 10f;
        [SerializeField] private string expectedPilot = "10110100";

        public RelayBReferenceProfile() { }

        public RelayBReferenceProfile(
            float minFund, float maxFund, float targetFund, float targetPhase,
            bool harmonic, float maxDist, float minBaud, float maxBaud, string pilot)
        {
            fundamentalMinKhz = minFund;
            fundamentalMaxKhz = maxFund;
            targetFundamentalKhz = targetFund;
            targetPhaseDegrees = targetPhase;
            secondHarmonicRequired = harmonic;
            maxDistortionPercent = maxDist;
            symbolRateMin = minBaud;
            symbolRateMax = maxBaud;
            expectedPilot = pilot;
        }

        public float FundamentalMinKhz => fundamentalMinKhz;
        public float FundamentalMaxKhz => fundamentalMaxKhz;
        public float TargetFundamentalKhz => targetFundamentalKhz;
        public float TargetPhaseDegrees => targetPhaseDegrees;
        public bool SecondHarmonicRequired => secondHarmonicRequired;
        public float MaxDistortionPercent => maxDistortionPercent;
        public float SymbolRateMin => symbolRateMin;
        public float SymbolRateMax => symbolRateMax;
        public string ExpectedPilot => expectedPilot;
    }

    public readonly struct RelayBOutputDiagnostic
    {
        public RelayBOutputDiagnostic(
            float carrierLevelDb,
            float noiseFloorDb,
            float snrDb,
            float distortionPercent,
            bool isClipping,
            string clippingMessage,
            float pilotCorrelation,
            string pilotDecodedFrame,
            bool pilotVerified,
            string summary)
        {
            CarrierLevelDb = carrierLevelDb;
            NoiseFloorDb = noiseFloorDb;
            SnrDb = snrDb;
            DistortionPercent = distortionPercent;
            IsClipping = isClipping;
            ClippingMessage = clippingMessage ?? (isClipping ? "OVERDRIVE CLIPPING" : "CLEAR");
            PilotCorrelation = pilotCorrelation;
            PilotDecodedFrame = pilotDecodedFrame ?? string.Empty;
            PilotVerified = pilotVerified;
            Summary = summary ?? string.Empty;
        }

        public float CarrierLevelDb { get; }
        public float NoiseFloorDb { get; }
        public float SnrDb { get; }
        public float DistortionPercent { get; }
        public bool IsClipping { get; }
        public string ClippingMessage { get; }
        public float PilotCorrelation { get; }
        public string PilotDecodedFrame { get; }
        public bool PilotVerified { get; }
        public string Summary { get; }

        public bool IsQualityAcceptable => SnrDb >= 20f && DistortionPercent <= 10f && !IsClipping;
        public bool IsValid => IsQualityAcceptable && PilotVerified;
    }

    // Legacy compatibility types
    public enum RelayBFilterMode
    {
        None,
        BandPass,
        Notch,
        BandPassThenNotch
    }

    public enum RelayBGainMode
    {
        Low,
        High,
        Limited
    }

    public enum RelayBPilotMode
    {
        Unchecked,
        FalseLock,
        Matched
    }

    public readonly struct RelayBChannelDiagnostic
    {
        public RelayBChannelDiagnostic(
            bool waveformMatches,
            bool carrierInBand,
            bool distortionAcceptable,
            string summary)
        {
            WaveformMatches = waveformMatches;
            CarrierInBand = carrierInBand;
            DistortionAcceptable = distortionAcceptable;
            Summary = summary ?? string.Empty;
        }

        public bool WaveformMatches { get; }
        public bool CarrierInBand { get; }
        public bool DistortionAcceptable { get; }
        public string Summary { get; }
        public bool IsValid => WaveformMatches && CarrierInBand && DistortionAcceptable;
    }
}
