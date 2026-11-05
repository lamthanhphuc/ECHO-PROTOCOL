using System;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Telemetry;
using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using EchoProtocol.TeamTools;

public sealed class AEDV02WireContractTests
{
    [Test]
    public void PlacementCannotMaterializeInsideAnEnabledSolidBody()
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var guard = typeof(TeamToolWorldSpawn).GetMethod("IsV02Occupied", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(guard);
            Physics.SyncTransforms();
            Assert.IsTrue((bool)guard.Invoke(null, new object[] { Vector3.zero, new[] { body.transform } }));
            Assert.IsFalse((bool)guard.Invoke(null, new object[] { Vector3.right * 5, new[] { body.transform } }));
            body.GetComponent<Collider>().enabled = false;
            Assert.IsFalse((bool)guard.Invoke(null, new object[] { Vector3.zero, new[] { body.transform } }));
        }
        finally { UnityEngine.Object.DestroyImmediate(body); }
    }

    [Test]
    public void DecisionSerializationPreservesAbsentActionsInsteadOfCreatingEmptyPlacements()
    {
        var request = new AEDV02DecisionRequestDto { decisionId = Guid.NewGuid().ToString("D"), planValues = AEDV02Plan.Normal().ToArray() };
        var json = AEDV02SourceJson.Decision(request);
        StringAssert.Contains("\"proposedReservations\":null", json);
        StringAssert.Contains("\"support\":null", json);
        StringAssert.Contains("\"planValues\":[1.25,18,1,2.25,7.5,420,1,2,0,0,0,0]", json);
        request.proposedReservations = Array.Empty<AEDV02ProposedReservationDto>();
        StringAssert.Contains("\"proposedReservations\":[]", AEDV02SourceJson.Decision(request));
        request.support = new AEDV02SupportActionDto { kind = "ORDINARY", toolId = 2, markerId = "marker", zone = 1 };
        StringAssert.Contains("\"support\":{", AEDV02SourceJson.Decision(request));
        StringAssert.Contains("\"toolId\":2", AEDV02SourceJson.Decision(request));
    }

    [Test]
    public void NumericWireArraysRejectNonFiniteValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryJsonObject().AddNumberArray("values", new[] { double.NaN }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryJsonObject().AddNumberMatrix("path", new[] { new[] { double.PositiveInfinity } }));
        Assert.AreEqual("{\"values\":[0.85,1.08]}", new TelemetryJsonObject().AddNumberArray("values", new[] { .85, 1.08 }).ToJson());
    }
}
