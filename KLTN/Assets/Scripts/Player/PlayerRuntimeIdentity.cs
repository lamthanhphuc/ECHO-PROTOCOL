using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.Player
{
    public sealed class PlayerRuntimeIdentity : MonoBehaviour
    {
        [SerializeField] private Transform visionTargetPoint;

        private PlayerId _playerId;

        public PlayerId PlayerId => _playerId;

        public bool IsBound => _playerId.IsValid;

        public Transform EntityRoot => transform;

        public Transform VisionTargetPoint
        {
            get
            {
                if (visionTargetPoint != null
                    && (visionTargetPoint == EntityRoot || visionTargetPoint.IsChildOf(EntityRoot)))
                {
                    return visionTargetPoint;
                }

                return EntityRoot;
            }
        }

        public bool TryBind(PlayerId playerId)
        {
            if (!playerId.IsValid)
            {
                return false;
            }

            if (!_playerId.IsValid)
            {
                _playerId = playerId;
                return true;
            }

            return _playerId == playerId;
        }

        public void ClearBinding()
        {
            _playerId = PlayerId.Invalid;
        }
    }
}
