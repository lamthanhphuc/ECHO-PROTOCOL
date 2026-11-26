using System;using System.Collections;using System.Reflection;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;
public sealed class SalvagePriceReceiptPlayModeTests
{
    [UnityTest]public IEnumerator FinalLossAfterDespawnReusesItsExistingLabel()
    {
        var type=Type.GetType("EchoProtocol.Salvage.SalvageValuePopup, Assembly-CSharp",true);
        var show=type.GetMethod("Show");MonoBehaviour first=null,other=null;
        try
        {
            first=(MonoBehaviour)show.Invoke(null,new object[]{Vector3.zero,81,0,null,100001});
            var final=(MonoBehaviour)show.Invoke(null,new object[]{Vector3.zero,0,81,null,100001});
            Assert.That(final,Is.SameAs(first),"Late zero-value RPC must update its existing receipt");
            Assert.That(final.transform.childCount,Is.EqualTo(2),"Price is text only, without a reference-object bubble");
            var text=type.GetField("_text",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(final);
            Assert.That(text.GetType().GetProperty("text").GetValue(text).ToString(),Does.StartWith("0<"));
            var color=(Color)text.GetType().GetProperty("color").GetValue(text);
            Assert.That(color.g,Is.GreaterThan(color.r));Assert.That(color.g,Is.GreaterThan(color.b));
            other=(MonoBehaviour)show.Invoke(null,new object[]{Vector3.zero,50,0,null,100002});
            Assert.That(other,Is.Not.SameAs(final),"Distinct cargo serials must keep independent receipts");
        }
        finally{if(first!=null)UnityEngine.Object.Destroy(first.gameObject);if(other!=null)UnityEngine.Object.Destroy(other.gameObject);}
        yield return null;
    }
    [UnityTest]public IEnumerator GripHumStartsSilentBeforeItsFirstFadeIn()
    {
        var prefab=Resources.Load<GameObject>("Salvage/Fx/SalvageGripVfx");
        var instance=UnityEngine.Object.Instantiate(prefab);
        try
        {
            var source=instance.GetComponentInChildren<AudioSource>();
            Assert.That(source,Is.Not.Null);Assert.That(source.volume,Is.Zero);
            Assert.That(source.isPlaying,Is.False);Assert.That(source.outputAudioMixerGroup,Is.Not.Null);
        }
        finally{UnityEngine.Object.Destroy(instance);}
        yield return null;
    }
}
