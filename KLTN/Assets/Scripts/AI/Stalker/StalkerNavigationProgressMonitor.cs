using System;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public enum NavigationProgressState
    {
        Moving,
        NoProgress,
        Stuck
    }

    public readonly struct NavigationProgressSettings
    {
        public NavigationProgressSettings(
            float sampleInterval,
            float minimumDisplacement,
            float minimumRemainingDistanceImprovement,
            float noProgressDuration,
            float stuckDuration)
        {
            if (sampleInterval <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleInterval), "Sample interval must be greater than zero.");
            }

            if (minimumDisplacement < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumDisplacement), "Minimum displacement must not be negative.");
            }

            if (minimumRemainingDistanceImprovement < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumRemainingDistanceImprovement), "Minimum remaining-distance improvement must not be negative.");
            }

            if (noProgressDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(noProgressDuration), "No-progress duration must be greater than zero.");
            }

            if (stuckDuration <= noProgressDuration)
            {
                throw new ArgumentException("Stuck duration must be greater than no-progress duration.", nameof(stuckDuration));
            }

            SampleInterval = sampleInterval;
            MinimumDisplacement = minimumDisplacement;
            MinimumRemainingDistanceImprovement = minimumRemainingDistanceImprovement;
            NoProgressDuration = noProgressDuration;
            StuckDuration = stuckDuration;
        }

        public static NavigationProgressSettings Default => new NavigationProgressSettings(
            0.25f,
            0.05f,
            0.05f,
            0.75f,
            2.0f);

        public float SampleInterval { get; }

        public float MinimumDisplacement { get; }

        public float MinimumRemainingDistanceImprovement { get; }

        public float NoProgressDuration { get; }

        public float StuckDuration { get; }
    }

    public sealed class NavigationProgressMonitor
    {
        private readonly NavigationProgressSettings _settings;
        private bool _progressInitialized;
        private Vector3 _lastSamplePosition;
        private float _lastSampleRemainingDistance;
        private float _sampleAccumulator;
        private float _timeSinceMeaningfulProgress;
        private NavigationProgressState _state;

        public NavigationProgressMonitor(NavigationProgressSettings settings)
        {
            _settings = settings;
            Reset();
        }

        public NavigationProgressState State => _state;

        public void Observe(Vector3 position, float remainingDistance, float deltaTime)
        {
            if (!_progressInitialized)
            {
                _lastSamplePosition = position;
                _lastSampleRemainingDistance = remainingDistance;
                _progressInitialized = true;
                _sampleAccumulator = 0f;
                _timeSinceMeaningfulProgress = 0f;
                _state = NavigationProgressState.Moving;
                return;
            }

            if (deltaTime > 0f)
            {
                _sampleAccumulator += deltaTime;
            }

            if (_sampleAccumulator < _settings.SampleInterval)
            {
                return;
            }

            var elapsedSampleTime = _sampleAccumulator;
            var hasMeaningfulProgress = HasMeaningfulDisplacement(position)
                || HasMeaningfulRemainingDistanceImprovement(remainingDistance);

            if (hasMeaningfulProgress)
            {
                _timeSinceMeaningfulProgress = 0f;
                _state = NavigationProgressState.Moving;
            }
            else
            {
                _timeSinceMeaningfulProgress += elapsedSampleTime;
                if (_timeSinceMeaningfulProgress >= _settings.StuckDuration)
                {
                    _state = NavigationProgressState.Stuck;
                }
                else if (_timeSinceMeaningfulProgress >= _settings.NoProgressDuration)
                {
                    _state = NavigationProgressState.NoProgress;
                }
                else
                {
                    _state = NavigationProgressState.Moving;
                }
            }

            _lastSamplePosition = position;
            _lastSampleRemainingDistance = remainingDistance;
            _sampleAccumulator = 0f;
        }

        public void Reset()
        {
            _progressInitialized = false;
            _lastSamplePosition = default;
            _lastSampleRemainingDistance = 0f;
            _sampleAccumulator = 0f;
            _timeSinceMeaningfulProgress = 0f;
            _state = NavigationProgressState.Moving;
        }

        private bool HasMeaningfulDisplacement(Vector3 position)
        {
            return Vector3.Distance(_lastSamplePosition, position) >= _settings.MinimumDisplacement;
        }

        private bool HasMeaningfulRemainingDistanceImprovement(float remainingDistance)
        {
            return IsFinite(_lastSampleRemainingDistance)
                && IsFinite(remainingDistance)
                && _lastSampleRemainingDistance - remainingDistance >= _settings.MinimumRemainingDistanceImprovement;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
