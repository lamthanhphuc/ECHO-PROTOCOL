using System;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Common.Spatial;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct SearchEpisodeId : IEquatable<SearchEpisodeId>, IComparable<SearchEpisodeId>
    {
        private readonly long _value;

        public SearchEpisodeId(long value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Search episode id must be greater than zero.");
            }

            _value = value;
        }

        public static SearchEpisodeId Invalid => default;
        public bool IsValid => _value > 0;
        public long Value => _value;
        public int CompareTo(SearchEpisodeId other) => _value.CompareTo(other._value);
        public bool Equals(SearchEpisodeId other) => _value == other._value;
        public override bool Equals(object obj) => obj is SearchEpisodeId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => IsValid ? $"SearchEpisodeId({_value})" : "SearchEpisodeId.Invalid";
        public static bool operator ==(SearchEpisodeId left, SearchEpisodeId right) => left.Equals(right);
        public static bool operator !=(SearchEpisodeId left, SearchEpisodeId right) => !left.Equals(right);
    }

    public sealed class StalkerSearchContext
    {
        private const int HistoryCapacity = 16;
        private readonly int[] _candidateHistory = new int[HistoryCapacity];
        private readonly int[] _visitedSearchNodes = new int[HistoryCapacity];
        private int _candidateHistoryCount;
        private int _visitedSearchNodeCount;

        public StalkerSearchContext(
            SearchEpisodeId episodeId,
            Vector3 originLastKnownPosition,
            Vector3 originDirection,
            AiSimulationTime searchStartTime,
            RegionId originRegionId)
        {
            if (!episodeId.IsValid)
            {
                throw new ArgumentException("Search context requires a valid episode id.", nameof(episodeId));
            }

            if (!searchStartTime.IsValid)
            {
                throw new ArgumentException("Search context requires a valid start time.", nameof(searchStartTime));
            }

            EpisodeId = episodeId;
            SearchOriginLKP = originLastKnownPosition;
            SearchOriginDirection = originDirection.sqrMagnitude > 0f ? originDirection.normalized : Vector3.forward;
            SearchStartTime = searchStartTime;
            SearchOriginRegionId = originRegionId;
            CurrentCandidateNodeId = -1;
        }

        public SearchEpisodeId EpisodeId { get; }
        public Vector3 SearchOriginLKP { get; }
        public Vector3 SearchOriginDirection { get; }
        public AiSimulationTime SearchStartTime { get; }
        public RegionId SearchOriginRegionId { get; }
        public int CurrentCandidateNodeId { get; private set; }
        public int CandidateAttemptCount { get; private set; }
        public int CandidateHistoryCount => _candidateHistoryCount;
        public int VisitedSearchNodeCount => _visitedSearchNodeCount;

        public void RecordCandidateAttempt(int nodeId)
        {
            CurrentCandidateNodeId = nodeId;
            CandidateAttemptCount++;
            Push(_candidateHistory, ref _candidateHistoryCount, nodeId);
        }

        public void RecordPhysicalCandidateArrival(int nodeId)
        {
            Push(_visitedSearchNodes, ref _visitedSearchNodeCount, nodeId);
            if (CurrentCandidateNodeId == nodeId)
            {
                CurrentCandidateNodeId = -1;
            }
        }

        public bool HasAttemptedCandidate(int nodeId)
        {
            return Contains(_candidateHistory, _candidateHistoryCount, nodeId);
        }

        public bool HasVisitedSearchNode(int nodeId)
        {
            return Contains(_visitedSearchNodes, _visitedSearchNodeCount, nodeId);
        }

        private static void Push(int[] buffer, ref int count, int value)
        {
            for (var i = Math.Min(count, buffer.Length - 1); i > 0; i--)
            {
                buffer[i] = buffer[i - 1];
            }

            buffer[0] = value;
            if (count < buffer.Length)
            {
                count++;
            }
        }

        private static bool Contains(int[] buffer, int count, int value)
        {
            for (var i = 0; i < count; i++)
            {
                if (buffer[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
