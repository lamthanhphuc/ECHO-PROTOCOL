using UnityEngine;

namespace EchoProtocol.Gameplay
{
    /// <summary>
    /// Pure mathematical calculations for push direction, lever arm, torque, and dead zone.
    /// </summary>
    public static class PushablePhysics
    {
        public struct Result
        {
            public Vector3 PushDirection;
            public float RawTorque;
            public float NormalizedTorque;
            public float EffectiveTorque;
        }

        public static Result Calculate(
            Vector3 playerPosition,
            Vector3 pushPoint,
            Vector3 centerOfMass,
            float maxLeverArm,
            float rotationDeadZone)
        {
            // 1. Push direction: horizontal vector pointing from player to the push point on the object
            Vector3 pushDirection = pushPoint - playerPosition;
            pushDirection.y = 0f;
            if (pushDirection.sqrMagnitude > 0.0001f)
            {
                pushDirection.Normalize();
            }
            else
            {
                pushDirection = Vector3.zero;
            }

            // 2. Lever arm: vector from center of mass to push point on horizontal plane
            Vector3 leverArm = pushPoint - centerOfMass;
            leverArm.y = 0f;

            // 3. Torque around vertical Y axis: (leverArm x pushDirection).y
            float rawTorque = Vector3.Cross(leverArm, pushDirection).y;

            // 4. Normalize torque based on max lever arm
            float normalizedTorque = 0f;
            if (maxLeverArm > 0.001f)
            {
                normalizedTorque = Mathf.Clamp(rawTorque / maxLeverArm, -1f, 1f);
            }

            // 5. Apply dead zone
            float effectiveTorque = 0f;
            float absTorque = Mathf.Abs(normalizedTorque);
            if (absTorque > rotationDeadZone && rotationDeadZone < 0.999f)
            {
                float sign = Mathf.Sign(normalizedTorque);
                effectiveTorque = sign * Mathf.Clamp01((absTorque - rotationDeadZone) / (1f - rotationDeadZone));
            }

            return new Result
            {
                PushDirection = pushDirection,
                RawTorque = rawTorque,
                NormalizedTorque = normalizedTorque,
                EffectiveTorque = effectiveTorque
            };
        }
    }
}
