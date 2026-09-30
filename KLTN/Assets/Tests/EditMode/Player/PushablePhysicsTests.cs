using System;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Player.Tests
{
    [TestFixture]
    public sealed class PushablePhysicsTests
    {
        private readonly Vector3 _centerOfMass = Vector3.zero;
        private const float MaxLeverArm = 4.43f;
        private const float DeadZone = 0.12f;

        private struct TestResult
        {
            public Vector3 PushDirection;
            public float RawTorque;
            public float NormalizedTorque;
            public float EffectiveTorque;
        }

        private static Type ResolvePushablePhysicsType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("EchoProtocol.Gameplay.PushablePhysics", false);
                if (type != null) return type;
            }

            Assert.Fail("Missing production type 'EchoProtocol.Gameplay.PushablePhysics'.");
            return null;
        }

        private static TestResult Calculate(
            Vector3 playerPosition,
            Vector3 pushPoint,
            Vector3 centerOfMass,
            float maxLeverArm,
            float rotationDeadZone)
        {
            var type = ResolvePushablePhysicsType();
            var method = type.GetMethod("Calculate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Method 'PushablePhysics.Calculate' not found.");

            var rawResult = method.Invoke(null, new object[] { playerPosition, pushPoint, centerOfMass, maxLeverArm, rotationDeadZone });
            var resultType = rawResult.GetType();

            return new TestResult
            {
                PushDirection = (Vector3)resultType.GetField("PushDirection").GetValue(rawResult),
                RawTorque = (float)resultType.GetField("RawTorque").GetValue(rawResult),
                NormalizedTorque = (float)resultType.GetField("NormalizedTorque").GetValue(rawResult),
                EffectiveTorque = (float)resultType.GetField("EffectiveTorque").GetValue(rawResult)
            };
        }

        [Test]
        public void CenterPush_ProducesZeroTorque()
        {
            // Player standing at left center, pushing rightwards into center of vehicle
            Vector3 playerPos = new Vector3(-3.5f, 0f, 0f);
            Vector3 pushPoint = new Vector3(-2.68f, 0f, 0f);

            var result = Calculate(playerPos, pushPoint, _centerOfMass, MaxLeverArm, DeadZone);

            Assert.AreEqual(1f, result.PushDirection.x, 0.001f);
            Assert.AreEqual(0f, result.PushDirection.z, 0.001f);
            Assert.AreEqual(0f, result.RawTorque, 0.001f);
            Assert.AreEqual(0f, result.EffectiveTorque, 0.001f);
        }

        [Test]
        public void FrontOffset_ProducesPositiveTorque()
        {
            // Player standing at left side near front (Z = +3), pushing rightwards
            Vector3 playerPos = new Vector3(-3.5f, 0f, 3.0f);
            Vector3 pushPoint = new Vector3(-2.68f, 0f, 3.0f);

            var result = Calculate(playerPos, pushPoint, _centerOfMass, MaxLeverArm, DeadZone);

            Assert.That(result.RawTorque, Is.GreaterThan(0f));
            Assert.That(result.EffectiveTorque, Is.GreaterThan(0f));
        }

        [Test]
        public void RearOffset_ProducesOppositeSignTorque()
        {
            // Player standing at left side near rear (Z = -3), pushing rightwards
            Vector3 playerPos = new Vector3(-3.5f, 0f, -3.0f);
            Vector3 pushPoint = new Vector3(-2.68f, 0f, -3.0f);

            var result = Calculate(playerPos, pushPoint, _centerOfMass, MaxLeverArm, DeadZone);

            Assert.That(result.RawTorque, Is.LessThan(0f));
            Assert.That(result.EffectiveTorque, Is.LessThan(0f));
        }

        [Test]
        public void FrontAndRearOffsets_HaveSymmetricOppositeTorques()
        {
            Vector3 playerFront = new Vector3(-3.5f, 0f, 2.5f);
            Vector3 pushFront = new Vector3(-2.68f, 0f, 2.5f);
            var resultFront = Calculate(playerFront, pushFront, _centerOfMass, MaxLeverArm, DeadZone);

            Vector3 playerRear = new Vector3(-3.5f, 0f, -2.5f);
            Vector3 pushRear = new Vector3(-2.68f, 0f, -2.5f);
            var resultRear = Calculate(playerRear, pushRear, _centerOfMass, MaxLeverArm, DeadZone);

            Assert.AreEqual(resultFront.RawTorque, -resultRear.RawTorque, 0.001f);
            Assert.AreEqual(resultFront.EffectiveTorque, -resultRear.EffectiveTorque, 0.001f);
        }

        [Test]
        public void RearCenterPush_ProducesStraightForwardPush_ZeroTorque()
        {
            // Player directly behind vehicle at center (X = 0, Z = -5), pushing forward
            Vector3 playerPos = new Vector3(0f, 0f, -5.5f);
            Vector3 pushPoint = new Vector3(0f, 0f, -4.43f);

            var result = Calculate(playerPos, pushPoint, _centerOfMass, MaxLeverArm, DeadZone);

            Assert.AreEqual(0f, result.PushDirection.x, 0.001f);
            Assert.AreEqual(1f, result.PushDirection.z, 0.001f);
            Assert.AreEqual(0f, result.RawTorque, 0.001f);
            Assert.AreEqual(0f, result.EffectiveTorque, 0.001f);
        }

        [Test]
        public void RearOffsetPush_ProducesForwardAndSteeringTorque()
        {
            // Player behind vehicle, offset to the right (X = 1.5, Z = -5)
            Vector3 playerPos = new Vector3(1.5f, 0f, -5.5f);
            Vector3 pushPoint = new Vector3(1.5f, 0f, -4.43f);

            var result = Calculate(playerPos, pushPoint, _centerOfMass, MaxLeverArm, DeadZone);

            Assert.AreEqual(1f, result.PushDirection.z, 0.001f);
            // Pushing forward on right rear steers nose left (negative torque)
            Assert.That(result.RawTorque, Is.LessThan(0f));
            Assert.That(result.EffectiveTorque, Is.LessThan(0f));
        }

        [Test]
        public void RotationDeadZone_SuppressesSmallTorqueNearCenter()
        {
            // Small offset near center (Z = 0.2m)
            Vector3 playerPos = new Vector3(-3.5f, 0f, 0.2f);
            Vector3 pushPoint = new Vector3(-2.68f, 0f, 0.2f);

            var result = Calculate(playerPos, pushPoint, _centerOfMass, MaxLeverArm, rotationDeadZone: 0.12f);

            // Raw torque is non-zero (0.2), but normalized is 0.2 / 4.43 = 0.045 < 0.12
            Assert.That(Mathf.Abs(result.NormalizedTorque), Is.LessThan(0.12f));
            Assert.AreEqual(0f, result.EffectiveTorque, 0.0001f);
        }

        [Test]
        public void NormalizedTorque_IsClampedToMinusOneAndPlusOne()
        {
            // Lever arm beyond max lever arm
            Vector3 playerPos = new Vector3(-3.5f, 0f, 10.0f);
            Vector3 pushPoint = new Vector3(-2.68f, 0f, 10.0f);

            var result = Calculate(playerPos, pushPoint, _centerOfMass, maxLeverArm: 2.0f, rotationDeadZone: 0.1f);

            Assert.AreEqual(1.0f, result.NormalizedTorque, 0.001f);
            Assert.AreEqual(1.0f, result.EffectiveTorque, 0.001f);
        }
    }
}
