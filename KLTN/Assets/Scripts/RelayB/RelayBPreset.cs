using System;
using UnityEngine;

namespace EchoProtocol.RelayB
{
    [Serializable]
    public sealed class RelayBChannelDef
    {
        [SerializeField] private string channelLabel = "CHANNEL 01";
        [SerializeField] private WaveformType waveform = WaveformType.Sine;
        [SerializeField] private float harmonicDistortion = 0f;
        [SerializeField] private float frequencyMultiplier = 1f;

        public RelayBChannelDef() { }

        public RelayBChannelDef(string label, WaveformType wave, float distortion = 0f, float freqMult = 1f)
        {
            channelLabel = label;
            waveform = wave;
            harmonicDistortion = distortion;
            frequencyMultiplier = freqMult;
        }

        public string ChannelLabel => channelLabel;
        public WaveformType Waveform => waveform;
        public float HarmonicDistortion => harmonicDistortion;
        public float FrequencyMultiplier => frequencyMultiplier;
    }

    [Serializable]
    public sealed class RelayBPreset
    {
        [SerializeField] private string presetName = "Default Carrier";
        [SerializeField, Range(0, 3)] private int correctChannelIndex = 0;
        [SerializeField] private WaveformType referenceWaveform = WaveformType.Sine;
        [SerializeField, Range(10f, 100f)] private float targetFrequency = 50f;
        [SerializeField, Range(0f, 360f)] private float targetPhase = 180f;
        [SerializeField] private RelayBChannelDef[] channels = new RelayBChannelDef[4];

        public RelayBPreset() { }

        public RelayBPreset(
            string name,
            int correctIndex,
            WaveformType wave,
            float freq,
            float phase,
            RelayBChannelDef[] channelDefs)
        {
            presetName = name;
            correctChannelIndex = Mathf.Clamp(correctIndex, 0, 3);
            referenceWaveform = wave;
            targetFrequency = freq;
            targetPhase = phase;
            channels = channelDefs;
        }

        public string PresetName => presetName;
        public int CorrectChannelIndex => correctChannelIndex;
        public WaveformType ReferenceWaveform => referenceWaveform;
        public float TargetFrequency => targetFrequency;
        public float TargetPhase => targetPhase;
        public RelayBChannelDef[] Channels => channels;

        public RelayBChannelDef GetChannel(int index)
        {
            if (channels == null || channels.Length == 0)
            {
                return null;
            }

            int clamped = Mathf.Clamp(index, 0, channels.Length - 1);
            return channels[clamped];
        }
    }
}

