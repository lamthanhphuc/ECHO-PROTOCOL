using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
public class SalvageCartPhysicsPlayModeTests
{
 [UnityTest] public IEnumerator SmallCargoRestsInsideOpenTray()
 {
  var cart=Object.Instantiate(Resources.Load<GameObject>("Salvage/World/TransportCart"),new Vector3(0,50,0),Quaternion.identity);
  var cargo=GameObject.CreatePrimitive(PrimitiveType.Cube);cargo.transform.position=new Vector3(0,51,0);cargo.transform.localScale=Vector3.one*.2f;
  var body=cargo.AddComponent<Rigidbody>();body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
  try {
   for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
   Assert.That(cargo.transform.position.y,Is.InRange(50.4f,50.6f));
   Assert.That(Mathf.Abs(cargo.transform.position.x),Is.LessThan(.4f));
  }finally{Object.Destroy(cart);Object.Destroy(cargo);}
 }
}
