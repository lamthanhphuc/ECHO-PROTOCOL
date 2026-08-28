using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.Player
{
    public sealed class PlayerRuntimeEntityRegistry
    {
        private readonly SortedDictionary<PlayerId, PlayerRuntimeIdentity> _entitiesById =
            new SortedDictionary<PlayerId, PlayerRuntimeIdentity>();

        private readonly Dictionary<PlayerRuntimeIdentity, PlayerId> _idsByEntity =
            new Dictionary<PlayerRuntimeIdentity, PlayerId>();

        public int Count
        {
            get
            {
                PruneDestroyedEntities();
                return _entitiesById.Count;
            }
        }

        public bool TryRegister(PlayerRuntimeIdentity identity)
        {
            PruneDestroyedEntities();

            if (identity == null || !identity.IsBound || !identity.PlayerId.IsValid)
            {
                return false;
            }

            var playerId = identity.PlayerId;

            if (_entitiesById.TryGetValue(playerId, out var registeredIdentity))
            {
                return registeredIdentity == identity;
            }

            if (_idsByEntity.TryGetValue(identity, out var registeredId))
            {
                return registeredId == playerId;
            }

            _entitiesById.Add(playerId, identity);
            _idsByEntity.Add(identity, playerId);
            return true;
        }

        public bool TryGetEntity(PlayerId playerId, out PlayerRuntimeIdentity identity)
        {
            identity = null;

            if (!playerId.IsValid)
            {
                return false;
            }

            if (!_entitiesById.TryGetValue(playerId, out var registeredIdentity))
            {
                return false;
            }

            if (registeredIdentity == null)
            {
                Remove(playerId, registeredIdentity);
                return false;
            }

            identity = registeredIdentity;
            return true;
        }

        public bool Unregister(PlayerRuntimeIdentity identity)
        {
            PruneDestroyedEntities();

            if (identity == null)
            {
                return false;
            }

            if (!_idsByEntity.TryGetValue(identity, out var playerId))
            {
                return false;
            }

            Remove(playerId, identity);
            return true;
        }

        public bool Unregister(PlayerId playerId)
        {
            if (!TryGetEntity(playerId, out var identity))
            {
                return false;
            }

            Remove(playerId, identity);
            return true;
        }

        public bool TryResolvePlayerId(Transform candidate, out PlayerId playerId)
        {
            playerId = PlayerId.Invalid;

            if (candidate == null)
            {
                return false;
            }

            var identity = candidate.GetComponentInParent<PlayerRuntimeIdentity>();
            if (identity == null || !identity.IsBound || !identity.PlayerId.IsValid)
            {
                return false;
            }

            if (!TryGetEntity(identity.PlayerId, out var registeredIdentity))
            {
                return false;
            }

            if (registeredIdentity != identity)
            {
                return false;
            }

            playerId = identity.PlayerId;
            return true;
        }

        public int CollectActiveEntities(List<PlayerRuntimeIdentity> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            PruneDestroyedEntities();
            results.Clear();

            foreach (var pair in _entitiesById)
            {
                if (pair.Value != null)
                {
                    results.Add(pair.Value);
                }
            }

            return results.Count;
        }

        private void PruneDestroyedEntities()
        {
            PlayerId[] staleIds = null;
            var staleCount = 0;

            foreach (var pair in _entitiesById)
            {
                if (pair.Value != null)
                {
                    continue;
                }

                if (staleIds == null)
                {
                    staleIds = new PlayerId[_entitiesById.Count];
                }

                staleIds[staleCount] = pair.Key;
                staleCount++;
            }

            for (var i = 0; i < staleCount; i++)
            {
                Remove(staleIds[i], null);
            }
        }

        private void Remove(PlayerId playerId, PlayerRuntimeIdentity identity)
        {
            _entitiesById.Remove(playerId);

            if ((object)identity != null)
            {
                _idsByEntity.Remove(identity);
                return;
            }

            PlayerRuntimeIdentity staleIdentity = null;
            var foundStaleIdentity = false;
            foreach (var pair in _idsByEntity)
            {
                if (pair.Value == playerId)
                {
                    staleIdentity = pair.Key;
                    foundStaleIdentity = true;
                    break;
                }
            }

            if (foundStaleIdentity)
            {
                _idsByEntity.Remove(staleIdentity);
            }
        }
    }
}
