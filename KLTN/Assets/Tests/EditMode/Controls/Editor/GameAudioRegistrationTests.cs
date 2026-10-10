using System.Collections.Generic;
using System.Reflection;
using EchoProtocol.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class GameAudioRegistrationTests
    {
        private GameObject _runtimeObject,_owner;
        private GameAudioRuntime _runtime;
        private object _previousInstance;
        private static readonly FieldInfo InstanceField=typeof(GameAudioRuntime).GetField("_instance",BindingFlags.Static|BindingFlags.NonPublic);
        private HashSet<GameAudioEnvironmentSource> Sources => (HashSet<GameAudioEnvironmentSource>)typeof(GameAudioRuntime)
            .GetField("_environmentSources",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_runtime);
        [SetUp] public void Setup()
        {
            _previousInstance=InstanceField.GetValue(null);
            _runtimeObject=new GameObject("Audio registration test");
            _runtime=_runtimeObject.AddComponent<GameAudioRuntime>();
            InstanceField.SetValue(null,_runtime);
            _owner=new GameObject("Audio owner");
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(_owner);Object.DestroyImmediate(_runtimeObject);
            InstanceField.SetValue(null,_previousInstance);
        }
        private static void Dispatch(GameAudioEnvironmentSource source,string method) => typeof(GameAudioEnvironmentSource).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(source,null);
        [Test] public void EnvironmentRegistration_FollowsPoolingAndDestructionWithoutDuplicates()
        {
            GameAudioRuntime.RegisterEnvironmentOwner(_owner.transform);
            GameAudioRuntime.RegisterEnvironmentOwner(_owner.transform);
            var source=_owner.GetComponent<GameAudioEnvironmentSource>();
            // EditMode does not dispatch ordinary MonoBehaviour enable callbacks.
            Dispatch(source,"Awake");Dispatch(source,"OnEnable");Dispatch(source,"OnEnable");
            Assert.That(Sources.Count,Is.EqualTo(1));
            Assert.That(_owner.GetComponents<GameAudioEnvironmentSource>().Length,Is.EqualTo(1));
            _owner.SetActive(false);Dispatch(source,"OnDisable");Assert.That(Sources.Count,Is.Zero);
            _owner.SetActive(true);Dispatch(source,"OnEnable");Assert.That(Sources.Count,Is.EqualTo(1));
            Dispatch(source,"OnDisable");Object.DestroyImmediate(_owner);Assert.That(Sources.Count,Is.Zero);
        }
        [Test] public void ButtonRegistration_HandlesInactivePanelsAndRepeatedRegistration()
        {
            _owner.SetActive(false);
            var button=_owner.AddComponent<Button>();
            GameAudioRuntime.RegisterButton(button);GameAudioRuntime.RegisterButton(button);
            _owner.SetActive(true);_owner.SetActive(false);_owner.SetActive(true);
            Assert.That(_owner.GetComponents<GameAudioButton>().Length,Is.EqualTo(1));
        }
    }
}
