using EchoProtocol.Networking;
using NUnit.Framework;
using UnityEngine;

public sealed class Zone3DockAreaTests
{
    private GameObject _dock;
    private GameObject _ship;

    [SetUp]
    public void SetUp()
    {
        _dock = new GameObject("Dock");
        var area = _dock.AddComponent<Zone3DockArea>();
        var collider = area.GetComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.05f, 0f);
        collider.size = new Vector3(1f, 0.1f, 1f);

        _ship = new GameObject("Ship");
        var shipCollider = _ship.AddComponent<BoxCollider>();
        shipCollider.size = new Vector3(5.36f, 4.35f, 8.86f);
        area.MatchShipFootprint(shipCollider);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_dock);
        Object.DestroyImmediate(_ship);
    }

    [Test]
    public void ShipMustFitEntirelyInsideDock()
    {
        var area = _dock.GetComponent<Zone3DockArea>();
        var shipCollider = _ship.GetComponent<BoxCollider>();

        Assert.IsTrue(area.FullyContains(shipCollider));
        _ship.transform.position = new Vector3(1f, 0f, 0f);
        Assert.IsFalse(area.FullyContains(shipCollider));
        _ship.transform.position = Vector3.zero;
        _ship.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        Assert.IsFalse(area.FullyContains(shipCollider));
    }
}
