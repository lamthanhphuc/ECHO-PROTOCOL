using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EchoProtocol.AI.AED
{
    public sealed class AEDPhase8AuthoritySourceSnapshotV1
    {
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public bool CoverageComplete { get; }
        public IReadOnlyList<AEDPhase8ToolSourceV1> Tools { get; }
        public IReadOnlyList<AEDPhase8MinionSourceV1> Minions { get; }

        internal AEDPhase8AuthoritySourceSnapshotV1(Guid matchId, uint phaseOrdinal, bool complete, IEnumerable<AEDPhase8ToolSourceV1> tools, IEnumerable<AEDPhase8MinionSourceV1> minions)
        {
            MatchId = matchId; PhaseOrdinal = phaseOrdinal; CoverageComplete = complete;
            Tools = new ReadOnlyCollection<AEDPhase8ToolSourceV1>(tools.ToArray());
            Minions = new ReadOnlyCollection<AEDPhase8MinionSourceV1>(minions.ToArray());
        }
    }

    public sealed class AEDPhase8ToolSourceV1
    {
        public string InstanceId { get; }
        public string ParentInstanceId { get; }
        public int ToolId { get; }
        public int Zone { get; }
        public string Origin { get; }
        public string Location { get; }
        public string OwnerUserId { get; }
        public int? RemainingCharges { get; }
        public long SourceTick { get; }
        public string SourceOccurrenceKey { get; }
        internal AEDPhase8ToolSourceV1(string id, int tool, int zone, string origin, string location, string owner, int? charges, long tick, string key, string parentInstanceId = null)
        { InstanceId = id; ParentInstanceId = parentInstanceId; ToolId = tool; Zone = zone; Origin = origin; Location = location; OwnerUserId = owner; RemainingCharges = charges; SourceTick = tick; SourceOccurrenceKey = key; }
    }

    public sealed class AEDPhase8MinionSourceV1
    {
        public string NetworkInstanceId { get; }
        public int Zone { get; }
        public string State { get; }
        public long SourceTick { get; }
        public string SourceOccurrenceKey { get; }
        internal AEDPhase8MinionSourceV1(string id, int zone, string state, long tick, string key)
        { NetworkInstanceId = id; Zone = zone; State = state; SourceTick = tick; SourceOccurrenceKey = key; }
    }

    public sealed class AEDPhase8AuthoritySourceCollectorV1
    {
        private readonly List<AEDPhase8ToolSourceV1> _tools = new();
        private readonly List<AEDPhase8MinionSourceV1> _minions = new();
        private readonly HashSet<string> _occurrenceKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _heldToolByOwner = new(StringComparer.Ordinal);
        private readonly HashSet<string> _transferredToolInstances = new(StringComparer.Ordinal);
        private Guid _matchId;

        public void BeginMatch(Guid matchId)
        {
            ResetMatch();
            if (matchId == Guid.Empty) throw new ArgumentException("AED_P8_MATCH_ID_REQUIRED", nameof(matchId));
            _matchId = matchId;
        }

        public void RecordToolSpawn(string instanceId, int toolId, int zone, long tick, string occurrenceKey)
        {
            RecordOriginToolSpawn(instanceId, toolId, zone, tick, occurrenceKey, "WorldSpawn");
        }

        public void RecordSceneToolSpawn(string instanceId, int toolId, long tick, string occurrenceKey)
        {
            RecordOriginToolSpawn(instanceId, toolId, 0, tick, occurrenceKey, "ScenePlaced");
        }

        private void RecordOriginToolSpawn(string instanceId, int toolId, int zone, long tick, string occurrenceKey, string origin)
        {
            RequireActive();
            RequireRow(instanceId, zone, tick, occurrenceKey);
            if (toolId is not (1 or 2 or 3 or 4 or 6))
                throw new ArgumentOutOfRangeException(nameof(toolId));
            if (_tools.Any(x => x.InstanceId == instanceId))
                throw new InvalidOperationException("AED_P8_DUPLICATE_TOOL_INSTANCE");
            RegisterOccurrence(occurrenceKey);
            _tools.Add(new AEDPhase8ToolSourceV1(instanceId, toolId, zone, origin, "World", null, null, tick, occurrenceKey));
        }

        public void RecordLobbyToolLoadout(string instanceId, int toolId, string ownerUserId, int? remainingCharges, long tick, string occurrenceKey)
        {
            RequireActive();
            RequireRow(instanceId, 0, tick, occurrenceKey);
            if (toolId is not (1 or 2 or 3 or 4 or 6) ||
                !Guid.TryParse(ownerUserId, out var owner) || owner == Guid.Empty)
                throw new InvalidOperationException("AED_P8_LOADOUT_INVALID");
            if (toolId is 2 or 4)
            {
                if (!remainingCharges.HasValue || remainingCharges.Value is < 1 or > 3)
                    throw new InvalidOperationException("AED_P8_LOADOUT_CHARGES_INVALID");
            }
            else if (remainingCharges.HasValue)
                throw new InvalidOperationException("AED_P8_LOADOUT_CHARGES_INVALID");
            if (_tools.Any(x => x.InstanceId == instanceId) || _heldToolByOwner.ContainsKey(ownerUserId))
                throw new InvalidOperationException("AED_P8_LOADOUT_IDENTITY_CONFLICT");
            RegisterOccurrence(occurrenceKey);
            _tools.Add(new AEDPhase8ToolSourceV1(instanceId, toolId, 0, "LobbyLoadout", "Player", ownerUserId,
                remainingCharges, tick, occurrenceKey));
            _heldToolByOwner.Add(ownerUserId, instanceId);
        }

        public void RecordToolTransition(string instanceId, string newLocation, string ownerUserId, int? remainingCharges, long tick, string occurrenceKey)
        {
            RequireActive(); RequireRow(instanceId, 0, tick, occurrenceKey);
            var index = _tools.FindLastIndex(x => x.InstanceId == instanceId);
            if (index < 0) throw new InvalidOperationException("AED_P8_TOOL_INSTANCE_MISSING");
            var previous = _tools[index];
            if (_transferredToolInstances.Contains(instanceId))
                throw new InvalidOperationException("AED_P8_TOOL_INSTANCE_TRANSFERRED");
            if (newLocation != "Player")
                throw new InvalidOperationException("AED_P8_TOOL_TRANSITION_UNSUPPORTED");
            if (string.IsNullOrWhiteSpace(newLocation) ||
                remainingCharges < 0 ||
                tick <= previous.SourceTick ||
                (newLocation == "Player" &&
                 (!Guid.TryParse(ownerUserId, out var ownerId) ||
                  ownerId == Guid.Empty))) throw new InvalidOperationException("AED_P8_INVALID_TOOL_TRANSITION");
            if (newLocation == "Player" &&
                (previous.Location != "World" ||
                 (_heldToolByOwner.TryGetValue(ownerUserId, out var held) && held != instanceId)))
                throw new InvalidOperationException("AED_P8_OWNER_INVENTORY_CONFLICT");
            if (previous.ToolId is 2 or 4)
            {
                if (!remainingCharges.HasValue || remainingCharges.Value is < 1 or > 3 ||
                    (previous.RemainingCharges.HasValue && remainingCharges.Value != previous.RemainingCharges.Value))
                    throw new InvalidOperationException("AED_P8_TOOL_PICKUP_CHARGES_INVALID");
            }
            else if (remainingCharges.HasValue)
                throw new InvalidOperationException("AED_P8_TOOL_PICKUP_CHARGES_INVALID");
            RegisterOccurrence(occurrenceKey);
            _tools.Add(new AEDPhase8ToolSourceV1(instanceId, previous.ToolId, previous.Zone, previous.Origin, newLocation, ownerUserId, remainingCharges, tick, occurrenceKey, previous.ParentInstanceId));
            if (newLocation == "Player") _heldToolByOwner[ownerUserId] = instanceId;
        }

        public bool TryGetHeldTool(string ownerUserId, out AEDPhase8ToolSourceV1 source)
        {
            source = null;
            if (_matchId == Guid.Empty || !_heldToolByOwner.TryGetValue(ownerUserId, out var id)) return false;
            var latest = _tools.LastOrDefault(x => x.InstanceId == id);
            if (latest == null || latest.Location != "Player" || latest.OwnerUserId != ownerUserId) return false;
            source = latest;
            return true;
        }

        public AEDPhase8ToolSourceV1 RecordToolUse(string instanceId, string ownerUserId, int toolId, int remainingCharges, bool consumed, long tick, string occurrenceKey)
        {
            RequireActive(); RequireRow(instanceId, 0, tick, occurrenceKey);
            if (!Guid.TryParse(ownerUserId, out var owner) || owner == Guid.Empty || remainingCharges < 0 || consumed != (remainingCharges == 0))
                throw new InvalidOperationException("AED_P8_TOOL_USE_INVALID");
            if (!TryGetHeldTool(ownerUserId, out var previous) || previous.InstanceId != instanceId || previous.ToolId != toolId ||
                _transferredToolInstances.Contains(instanceId) || tick <= previous.SourceTick)
                throw new InvalidOperationException("AED_P8_TOOL_USE_SOURCE_INVALID");
            if (!consumed && toolId is not (2 or 4))
                throw new InvalidOperationException("AED_P8_TOOL_USE_CHARGES_UNKNOWN");
            if ((toolId is 2 or 4) && !previous.RemainingCharges.HasValue)
                throw new InvalidOperationException("AED_P8_TOOL_USE_CHARGES_UNKNOWN");
            if (previous.RemainingCharges.HasValue &&
                previous.RemainingCharges.Value - remainingCharges != 1)
                throw new InvalidOperationException("AED_P8_TOOL_USE_CHARGES_INVALID");
            RegisterOccurrence(occurrenceKey);
            var source = new AEDPhase8ToolSourceV1(instanceId, toolId, previous.Zone, previous.Origin,
                consumed ? "Consumed" : "Player", ownerUserId, remainingCharges, tick, occurrenceKey, previous.ParentInstanceId);
            _tools.Add(source);
            if (consumed) _heldToolByOwner.Remove(ownerUserId);
            return source;
        }

        public void RecordToolDrop(string newInstanceId, int toolId, string ownerUserId, int? remainingCharges, long tick, string occurrenceKey)
        {
            RequireActive();
            RequireRow(newInstanceId, 0, tick, occurrenceKey);
            if (!Guid.TryParse(ownerUserId, out var owner) || owner == Guid.Empty || remainingCharges < 0)
                throw new InvalidOperationException("AED_P8_DROP_INVALID_OWNER");
            if (!_heldToolByOwner.TryGetValue(ownerUserId, out var parentId))
                throw new InvalidOperationException("AED_P8_DROP_PARENT_MISSING");
            var parent = _tools.LastOrDefault(x => x.InstanceId == parentId);
            if (parent == null || parent.Location != "Player" || parent.OwnerUserId != ownerUserId ||
                parent.ToolId != toolId || tick <= parent.SourceTick || newInstanceId == parentId ||
                _tools.Any(x => x.InstanceId == newInstanceId))
                throw new InvalidOperationException("AED_P8_DROP_LINEAGE_INVALID");
            if (parent.RemainingCharges != remainingCharges)
                throw new InvalidOperationException("AED_P8_DROP_CHARGES_INVALID");
            RegisterOccurrence(occurrenceKey);
            _transferredToolInstances.Add(parentId);
            _tools.Add(new AEDPhase8ToolSourceV1(newInstanceId, toolId, parent.Zone, "PlayerDrop", "World", null, remainingCharges, tick, occurrenceKey, parentId));
            _heldToolByOwner.Remove(ownerUserId);
        }

        public void RecordMinionSpawn(string networkInstanceId, int zone, long tick, string occurrenceKey)
        {
            RequireActive(); RequireRow(networkInstanceId, zone, tick, occurrenceKey);
            if (_minions.Any(x => x.NetworkInstanceId == networkInstanceId)) throw new InvalidOperationException("AED_P8_DUPLICATE_MINION_INSTANCE");
            RegisterOccurrence(occurrenceKey);
            _minions.Add(new AEDPhase8MinionSourceV1(networkInstanceId, zone, "SPAWNED", tick, occurrenceKey));
        }

        public void RecordMinionDespawn(string networkInstanceId, int zone, long tick, string occurrenceKey)
        {
            RequireActive(); RequireRow(networkInstanceId, zone, tick, occurrenceKey);
            var spawn = _minions.LastOrDefault(x => x.NetworkInstanceId == networkInstanceId && x.State == "SPAWNED");
            var terminal = _minions.LastOrDefault(x => x.NetworkInstanceId == networkInstanceId && x.State == "DESPAWNED");
            if (spawn == null || terminal != null || spawn.Zone != zone || tick <= spawn.SourceTick) throw new InvalidOperationException("AED_P8_INVALID_MINION_TRANSITION");
            RegisterOccurrence(occurrenceKey);
            _minions.Add(new AEDPhase8MinionSourceV1(networkInstanceId, zone, "DESPAWNED", tick, occurrenceKey));
        }

        public bool TryGetLatestTool(string instanceId, out AEDPhase8ToolSourceV1 source)
        {
            source = _tools.LastOrDefault(x => x.InstanceId == instanceId);
            return _matchId != Guid.Empty && source != null;
        }

        public bool TryFreeze(Guid matchId, uint phaseOrdinal, out AEDPhase8AuthoritySourceSnapshotV1 snapshot)
        {
            snapshot = null;
            if (matchId == Guid.Empty || matchId != _matchId || phaseOrdinal == 0) return false;
            // Lobby, drop, recovery, support spawn, and canonical event reconciliation are not connected yet.
            snapshot = new AEDPhase8AuthoritySourceSnapshotV1(matchId, phaseOrdinal, false, _tools, _minions);
            return true;
        }

        public void ResetMatch()
        { _matchId = Guid.Empty; _tools.Clear(); _minions.Clear(); _occurrenceKeys.Clear(); _heldToolByOwner.Clear(); _transferredToolInstances.Clear(); }

        private void RegisterOccurrence(string occurrenceKey)
        {
            if (!_occurrenceKeys.Add(occurrenceKey))
                throw new InvalidOperationException("AED_P8_DUPLICATE_OCCURRENCE");
        }

        private void RequireActive()
        { if (_matchId == Guid.Empty) throw new InvalidOperationException("AED_P8_MATCH_NOT_STARTED"); }

        private static void RequireRow(string id, int zone, long tick, string key)
        { if (string.IsNullOrWhiteSpace(id) || zone < 0 || zone > 3 || tick < 0 || string.IsNullOrWhiteSpace(key)) throw new ArgumentException("AED_P8_SOURCE_ROW_INVALID"); }
    }
}
