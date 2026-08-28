using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;
using Fusion;

namespace EchoProtocol.Networking
{
    public sealed class FusionPlayerIdentityRegistry
    {
        private readonly Dictionary<PlayerRef, PlayerId> _idsByPlayerRef =
            new Dictionary<PlayerRef, PlayerId>(PlayerRef.Comparer);

        private readonly Dictionary<PlayerId, PlayerRef> _playerRefsById =
            new Dictionary<PlayerId, PlayerRef>();

        private int _nextPlayerIdValue = 1;

        public int Count => _idsByPlayerRef.Count;

        public bool TryRegister(PlayerRef playerRef, out PlayerId playerId)
        {
            playerId = PlayerId.Invalid;

            if (!IsValidPlayerRef(playerRef))
            {
                return false;
            }

            if (_idsByPlayerRef.TryGetValue(playerRef, out playerId))
            {
                return true;
            }

            if (_nextPlayerIdValue <= 0)
            {
                playerId = PlayerId.Invalid;
                return false;
            }

            var allocatedId = new PlayerId(_nextPlayerIdValue);
            if (_playerRefsById.ContainsKey(allocatedId))
            {
                return false;
            }

            _idsByPlayerRef.Add(playerRef, allocatedId);
            _playerRefsById.Add(allocatedId, playerRef);
            playerId = allocatedId;

            _nextPlayerIdValue = _nextPlayerIdValue == int.MaxValue
                ? 0
                : _nextPlayerIdValue + 1;

            return true;
        }

        public bool TryGetPlayerId(PlayerRef playerRef, out PlayerId playerId)
        {
            playerId = PlayerId.Invalid;

            if (!IsValidPlayerRef(playerRef))
            {
                return false;
            }

            return _idsByPlayerRef.TryGetValue(playerRef, out playerId);
        }

        public bool TryGetPlayerRef(PlayerId playerId, out PlayerRef playerRef)
        {
            playerRef = PlayerRef.None;

            if (!playerId.IsValid)
            {
                return false;
            }

            return _playerRefsById.TryGetValue(playerId, out playerRef);
        }

        public bool Unregister(PlayerRef playerRef)
        {
            if (!IsValidPlayerRef(playerRef))
            {
                return false;
            }

            if (!_idsByPlayerRef.TryGetValue(playerRef, out var playerId))
            {
                return false;
            }

            _idsByPlayerRef.Remove(playerRef);
            _playerRefsById.Remove(playerId);
            return true;
        }

        public int CollectActivePlayerIds(List<PlayerId> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            for (var value = 1; value != _nextPlayerIdValue; value++)
            {
                var playerId = new PlayerId(value);
                if (_playerRefsById.ContainsKey(playerId))
                {
                    results.Add(playerId);
                }

                if (value == int.MaxValue)
                {
                    break;
                }
            }

            return results.Count;
        }

        private static bool IsValidPlayerRef(PlayerRef playerRef)
        {
            return playerRef.IsRealPlayer;
        }
    }
}
