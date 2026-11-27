using System;

namespace EchoProtocol.UI.Credits
{
    public enum CreditsRollPhase { Opening, Rolling, Closing, Fading, Completed }

    /// <summary>Local presentation clock, driven by unscaled seconds.</summary>
    public sealed class CreditsRollPlayback
    {
        public CreditsRollPhase Phase { get; private set; }
        public float RollSeconds { get; private set; }
        public float SkipHeldSeconds { get; private set; }
        public float Opacity { get; private set; }
        private float _phaseSeconds;
        private float _fadeStartOpacity;

        public static bool CanPresentAfterResult(bool hasResult, bool hasConfirmedReward) => hasResult && hasConfirmedReward;
        public static float HoldProgress(float held, float required) => Math.Min(1f, Math.Max(0f, held / Math.Max(0.01f, required)));

        public void Tick(float delta, bool holdSkip, float opening, float fade, float roll, float closing, float skip = 2f)
        {
            if (Phase == CreditsRollPhase.Completed || float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) return;
            fade = Math.Max(0.01f, fade);
            skip = Math.Max(0.01f, skip);
            if (Phase == CreditsRollPhase.Fading)
            {
                Advance(delta, opening, fade, roll, closing);
                return;
            }

            if (!holdSkip) SkipHeldSeconds = 0;
            float untilSkip = holdSkip ? Math.Max(0, skip - SkipHeldSeconds) : float.PositiveInfinity;
            float beforeSkip = Math.Min(delta, untilSkip);
            if (holdSkip) SkipHeldSeconds += beforeSkip;
            Advance(beforeSkip, opening, fade, roll, closing);
            if (delta < untilSkip || Phase >= CreditsRollPhase.Fading) return;

            BeginFade();
            Advance(delta - beforeSkip, opening, fade, roll, closing);
        }

        private void Advance(float remaining, float opening, float fade, float roll, float closing)
        {
            while (Phase != CreditsRollPhase.Completed)
            {
                float duration = Math.Max(0, Phase switch
                {
                    CreditsRollPhase.Opening => opening,
                    CreditsRollPhase.Rolling => roll,
                    CreditsRollPhase.Closing => closing,
                    _ => fade
                });
                float step = Math.Min(remaining, duration - _phaseSeconds);
                _phaseSeconds += step;
                remaining -= step;
                switch (Phase)
                {
                    case CreditsRollPhase.Opening:
                        Opacity = Math.Min(1, _phaseSeconds / fade);
                        break;
                    case CreditsRollPhase.Rolling:
                        Opacity = 1;
                        RollSeconds = _phaseSeconds;
                        break;
                    case CreditsRollPhase.Fading:
                        Opacity = _fadeStartOpacity * (1 - _phaseSeconds / fade);
                        break;
                }
                if (_phaseSeconds < duration) return;
                if (Phase == CreditsRollPhase.Closing) BeginFade();
                else { Phase++; _phaseSeconds = 0; }
            }
        }

        private void BeginFade()
        {
            _fadeStartOpacity = Opacity;
            _phaseSeconds = 0;
            Phase = CreditsRollPhase.Fading;
        }
    }
}
