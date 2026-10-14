using NUnit.Framework;
using UnityEngine;

public sealed class GameplayEnvironmentPhysicsTests
{
    [Test]
    public void GroundQuery_IgnoresPortableCoreAndKeepsFloor()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            floor.transform.position = new Vector3(10000, -0.5f, 10000);
            core.transform.position = new Vector3(10000, 0.4f, 10000);
            core.transform.localScale = Vector3.one * 0.2f;
            core.AddComponent<EnergyCorePickup>();
            core.GetComponent<Collider>().isTrigger = false;
            Physics.SyncTransforms();
            Assert.That(GameplayEnvironmentPhysics.Raycast(new Vector3(10000, 1, 10000), Vector3.down,
                out var hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(hit.collider, Is.SameAs(floor.GetComponent<Collider>()));
        }
        finally { Object.DestroyImmediate(core); Object.DestroyImmediate(floor); }
    }

    [Test]
    public void PathQuery_KeepsSolidWallButIgnoresPortableCore()
    {
        var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            prop.transform.position = new Vector3(10000, 1, 10000);
            Physics.SyncTransforms();
            var from = prop.transform.position + Vector3.left * 2;
            var to = prop.transform.position + Vector3.right * 2;
            Assert.That(GameplayEnvironmentPhysics.Linecast(from, to, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore), Is.True);
            prop.AddComponent<EnergyCorePickup>();
            prop.GetComponent<Collider>().isTrigger = false;
            Physics.SyncTransforms();
            Assert.That(GameplayEnvironmentPhysics.Linecast(from, to, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore), Is.False);
        }
        finally { Object.DestroyImmediate(prop); }
    }
}
