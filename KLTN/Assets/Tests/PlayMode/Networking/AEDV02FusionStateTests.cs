using System;
using System.Collections;
using System.Reflection;
using EchoProtocol.AI.Common.AED;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDV02FusionStateTests
    {
        private GameObject _runnerObject;
        private NetworkRunner _runner;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_runner != null && _runner.IsRunning)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
            }
            if (_runnerObject != null) UnityEngine.Object.Destroy(_runnerObject);
            Type.GetType("EchoProtocol.AI.AED.AEDv2Authority, Assembly-CSharp", true).GetMethod("Reset").Invoke(null, new object[] { Guid.Empty });
        }

        [UnityTest]
        public IEnumerator AEDV02_SingleRunner_UsesFusionStorageAndRejectsCorruptReconstruction()
        {
            _runnerObject = new GameObject("AEDV02SingleRunnerTest");
            _runner = _runnerObject.AddComponent<NetworkRunner>();
            var start = _runner.StartGame(new StartGameArgs { GameMode = GameMode.Single });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ToString());
            var prefab = Resources.Load<GameObject>("Network/NetworkMatchState");
            Assert.That(prefab, Is.Not.Null);
            var type = Type.GetType("EchoProtocol.Networking.NetworkMatchState, Assembly-CSharp", true);
            var spawned = _runner.Spawn(prefab.GetComponent<NetworkObject>(), onBeforeSpawned: (runner, obj) =>
                type.GetField("_teamToolPickupCatalog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj.GetComponent(type), null));
            Assert.That(spawned.HasStateAuthority, Is.True);
            var state = spawned.GetComponent(type);
            Assert.That(state, Is.Not.Null);
            Assert.That((bool)(NetworkBool)type.GetProperty("TeamToolWorldSpawnInitialized", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state), Is.False);
            var match = Guid.NewGuid(); var plan = AEDV02Plan.Normal().With(AEDV02Key.ChaseSpeed, 7);
            var array = type.GetProperty("AEDV02Values", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
            for (int i = 0; i < plan.ToArray().Length; i++) array.GetType().GetMethod("Set").Invoke(array, new object[] { i, plan.ToArray()[i] });
            Set(state, "AEDV02MatchId", match.ToString("D")); Set(state, "AEDV02PlanFingerprint", plan.Fingerprint()); Set(state, "AEDV02PlanRevision", 2);
            var read = new object[] { match, null, 0 };
            Assert.That(type.GetMethod("TryReadV02Plan").Invoke(state, read), Is.EqualTo(true));
            Assert.That(((AEDV02Plan)read[1]).Fingerprint(), Is.EqualTo(plan.Fingerprint()));
            Assert.That(((AEDV02Plan)read[1]).Get(AEDV02Key.ChaseSpeed), Is.EqualTo(7));
            Assert.That(read[2], Is.EqualTo(2));
            Set(state, "AEDV02PlanFingerprint", AEDV02Plan.Hash("corrupt"));
            Assert.That(type.GetMethod("TryReadV02Plan").Invoke(state, new object[] { match, null, 0 }), Is.EqualTo(false));
            Set(state, "AEDV02PlanFingerprint", plan.Fingerprint());
            Assert.That(type.GetMethod("TryReadV02Plan").Invoke(state, new object[] { Guid.NewGuid(), null, 0 }), Is.EqualTo(false));
            _runner.Despawn(spawned);
        }

        private static void Set(Component state, string name, object value)
        {
            var property = state.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property.PropertyType != value.GetType())
            {
                value = property.PropertyType.GetMethod("op_Implicit", new[] { typeof(string) }).Invoke(null, new[] { value });
            }
            property.SetValue(state, value);
        }
    }
}
