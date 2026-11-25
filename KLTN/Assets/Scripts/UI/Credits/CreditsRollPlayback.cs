using System;

namespace EchoProtocol.UI.Credits
{
    public enum CreditsRollPhase { Waiting, Rolling, Closing, Fading, Completed }

    /// <summary>Local presentation clock. Uses caller-supplied unscaled seconds and UI design units.</summary>
    public sealed class CreditsRollPlayback
    {
        public CreditsRollPhase Phase { get; private set; }
        public float Distance { get; private set; }
        public float SkipHeldSeconds { get; private set; }
        public float Opacity { get; private set; }
        private float _phaseSeconds, _rollSeconds, _fadeStartOpacity;

        public static bool CanPresentAfterResult(bool hasResult, bool hasConfirmedReward) => hasResult && hasConfirmedReward;
        public static float HoldProgress(float held, float required) => Math.Min(1f, Math.Max(0f, held / Math.Max(0.01f, required)));

        public void Tick(float delta, bool holdSkip, float speed, float delay, float fade,
            float endDistance, float closingHold, float skipHold = 2f)
        {
            if (Phase == CreditsRollPhase.Completed || float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) return;
            float step = delta;
            fade = Math.Max(0.01f, fade);
            SkipHeldSeconds = holdSkip ? SkipHeldSeconds + step : 0;
            if (Phase != CreditsRollPhase.Fading && SkipHeldSeconds >= Math.Max(0.01f, skipHold))
            {
                BeginFade();
                return;
            }
            _phaseSeconds += step;
            switch (Phase)
            {
                case CreditsRollPhase.Waiting:
                    if (_phaseSeconds >= Math.Max(0, delay)) { Phase = CreditsRollPhase.Rolling; _phaseSeconds = 0; }
                    break;
                case CreditsRollPhase.Rolling:
                    _rollSeconds += step;
                    Opacity = Math.Min(1, _rollSeconds / fade);
                    Distance = Math.Min(Math.Max(0, endDistance), Distance + Math.Max(1, speed) * step);
                    if (Distance >= endDistance) { Phase = CreditsRollPhase.Closing; _phaseSeconds = 0; }
                    break;
                case CreditsRollPhase.Closing:
                    Opacity = Math.Min(1, Opacity + step / fade);
                    if (_phaseSeconds >= Math.Max(0, closingHold)) BeginFade();
                    break;
                case CreditsRollPhase.Fading:
                    Opacity = _fadeStartOpacity * Math.Max(0, 1 - _phaseSeconds / fade);
                    if (_phaseSeconds >= fade) { Opacity = 0; Phase = CreditsRollPhase.Completed; }
                    break;
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
