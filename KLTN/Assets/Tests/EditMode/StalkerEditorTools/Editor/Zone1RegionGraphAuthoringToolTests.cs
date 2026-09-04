#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using EchoProtocol.AI.Common.Spatial;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.AI.Stalker.Spatial.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.EditorTools.Tests
{
    public sealed class FullStationRegionGraphAuthoringToolTests
    {
        private const string TempAssetPath = "Assets/AI/Stalker/Station/__Test_Invalid_RegionGraph.asset";
        private const string ProtectedAssetPath = "Assets/AI/Stalker/Phase3/AI_Stalker_SpatialV3_RegionGraph.asset";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempAssetPath);
        }

        [Test]
        public void STK_FullStation_OneConnectedGraphCanContainMultipleSemanticRegions()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(1, 0), 0, 2),
                Node(2, V(2, 0), 1, 3),
                Node(3, V(3, 0), 2));

            var report = Build(graph, Source(0, SemanticZone.Zone01, SemanticKind.Room, "A", 0), Source(1, SemanticZone.Zone01, SemanticKind.Room, "B", 3));

            AssertValid(report);
            Assert.That(report.Regions.Count, Is.EqualTo(2));
        }

        [Test]
        public void STK_FullStation_SameEvidenceNodeByTwoSourcesReportsSeedOverlapOnly()
        {
            var graph = Graph(Node(0, new Vector3(2f, 0.5f, 4f)));

            var report = Build(graph,
                SourceWithEvidenceObjects(0, SemanticZone.Zone01, SemanticKind.Room, "A", new[] { 0 }, EvidenceObjects(0, "Root/A/Floor")),
                SourceWithEvidenceObjects(2, SemanticZone.Zone03, SemanticKind.Route, "C", new[] { 0, 0 }, EvidenceObjects(0, "Root/C/Floor")),
                SourceWithEvidenceObjects(1, SemanticZone.Zone02, SemanticKind.Room, "B", new[] { 0 }, EvidenceObjects(0, "Root/B/Floor")));

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.SeedOverlapNodeIds, Is.EqualTo(new[] { 0 }));
            Assert.That(report.BoundaryTieNodeIds, Is.Empty);
            Assert.That(report.Errors, Has.Some.EqualTo("Node 0 has a geometrically unresolved semantic seed evidence overlap."));
            Assert.That(report.SeedEvidenceOverlapDetails.Count, Is.EqualTo(1));
            Assert.That(report.SeedEvidenceOverlapDetails[0].NodeId, Is.EqualTo(0));
            Assert.That(report.SeedEvidenceOverlapDetails[0].Position, Is.EqualTo(new Vector3(2f, 0.5f, 4f)));
            Assert.That(report.SeedEvidenceOverlapDetails[0].ClaimantCount, Is.EqualTo(3));
            Assert.That(SeedClaimantKeys(report.SeedEvidenceOverlapDetails[0]), Is.EqualTo(new[]
            {
                "0|Zone01|Room|A|FloorEvidence|Root/A/Floor",
                "1|Zone02|Room|B|FloorEvidence|Root/B/Floor",
                "2|Zone03|Route|C|FloorEvidence|Root/C/Floor"
            }));
            Assert.That(report.ToDisplayString(), Does.Contain("Seed Evidence Overlap Details:"));
            Assert.That(report.ToDisplayString(), Does.Contain("- Node 0 at (2, 0.5, 4) — 3 claimants"));
            Assert.That(report.ToDisplayString(), Does.Contain("  - [1] Zone02/Room: B"));
            Assert.That(report.ToDisplayString(), Does.Contain("    evidence object: Root/B/Floor"));
        }

        [Test]
        public void STK_FullStation_SeedOverlapSupportingEvidenceOnlyIncludesBoundsThatSupportNode()
        {
            var graph = Graph(Node(0, new Vector3(0f, 0f, 0f)));
            var supported = EvidenceBound("Root/A/Floor", new Bounds(Vector3.zero, new Vector3(2f, 0.2f, 2f)));
            var unsupported = EvidenceBound("Root/A/FarFloor", new Bounds(new Vector3(10f, 0f, 0f), new Vector3(2f, 0.2f, 2f)));

            var report = Build(graph,
                SourceWithEvidenceBounds(0, SemanticZone.Zone01, SemanticKind.Room, "A", new[] { 0 }, EvidenceBounds(0, unsupported, supported)),
                SourceWithEvidenceBounds(1, SemanticZone.Zone02, SemanticKind.Room, "B", new[] { 0 }, EvidenceBounds(0, supported)));

            var claimant = report.SeedEvidenceOverlapDetails[0].Claimants[0];
            Assert.That(claimant.SupportingEvidence.Count, Is.EqualTo(1));
            Assert.That(claimant.SupportingEvidence[0].HierarchyPath, Is.EqualTo("Root/A/Floor"));
            Assert.That(report.ToDisplayString(), Does.Contain("bounds center: (0, 0, 0)"));
            Assert.That(report.ToDisplayString(), Does.Contain("bounds size: (2, 0.2, 2)"));
        }

        [Test]
        public void STK_FullStation_SeedOverlapHorizontalInteriorMarginIsComputedFromBoundsInterior()
        {
            var nodePosition = new Vector3(1.25f, 0.75f, -0.5f);
            var graph = Graph(Node(0, nodePosition));
            var evidence = EvidenceBound("Root/A/Floor", new Bounds(new Vector3(0f, 0.25f, 0f), new Vector3(4f, 0.5f, 6f)));

            var report = Build(graph,
                SourceWithEvidenceBounds(0, SemanticZone.Zone01, SemanticKind.Room, "A", new[] { 0 }, EvidenceBounds(0, evidence)),
                SourceWithEvidenceBounds(1, SemanticZone.Zone02, SemanticKind.Room, "B", new[] { 0 }, EvidenceBounds(0, evidence)));

            var support = report.SeedEvidenceOverlapDetails[0].Claimants[0].SupportingEvidence[0];
            Assert.That(support.HorizontalInteriorMargin, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(report.ToDisplayString(), Does.Contain("horizontal interior margin: 0.75m"));
        }

        [Test]
        public void STK_FullStation_SeedOverlapVerticalDeltaIsComputedFromBoundsCenter()
        {
            var nodePosition = new Vector3(1.25f, 0.75f, -0.5f);
            var graph = Graph(Node(0, nodePosition));
            var evidence = EvidenceBound("Root/A/Floor", new Bounds(new Vector3(0f, 0.25f, 0f), new Vector3(4f, 0.5f, 6f)));

            var report = Build(graph,
                SourceWithEvidenceBounds(0, SemanticZone.Zone01, SemanticKind.Room, "A", new[] { 0 }, EvidenceBounds(0, evidence)),
                SourceWithEvidenceBounds(1, SemanticZone.Zone02, SemanticKind.Room, "B", new[] { 0 }, EvidenceBounds(0, evidence)));

            var support = report.SeedEvidenceOverlapDetails[0].Claimants[0].SupportingEvidence[0];
            Assert.That(support.VerticalDeltaToBoundsCenter, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(report.ToDisplayString(), Does.Contain("vertical delta to bounds center: 0.5m"));
        }

        [Test]
        public void STK_FullStation_RendererColliderEquivalentEvidenceDoesNotDuplicateSupportingDiagnostic()
        {
            var graph = Graph(Node(0, Vector3.zero));
            var equivalent = EvidenceBound("Root/A/Floor", new Bounds(Vector3.zero, new Vector3(2f, 0.2f, 2f)));

            var report = Build(graph,
                SourceWithEvidenceBounds(0, SemanticZone.Zone01, SemanticKind.Room, "A", new[] { 0 }, EvidenceBounds(0, equivalent, equivalent)),
                SourceWithEvidenceBounds(1, SemanticZone.Zone02, SemanticKind.Room, "B", new[] { 0 }, EvidenceBounds(0, equivalent)));

            Assert.That(report.SeedEvidenceOverlapDetails[0].Claimants[0].SupportingEvidence.Count, Is.EqualTo(1));
        }

        [Test]
        public void STK_FullStation_SeedOverlapSupportingDiagnosticOrderingIsDeterministic()
        {
            var graph = Graph(Node(0, Vector3.zero), Node(1, new Vector3(0f, 0f, 1f)));
            var floorB = EvidenceBound("Root/B/Floor", new Bounds(Vector3.zero, new Vector3(8f, 0.2f, 8f)));
            var floorA2 = EvidenceBound("Root/A/Floor", new Bounds(new Vector3(2f, 0f, 0f), new Vector3(6f, 0.2f, 6f)));
            var floorA1 = EvidenceBound("Root/A/Floor", new Bounds(new Vector3(1f, 0f, 0f), new Vector3(4f, 0.2f, 4f)));

            var report = Build(graph,
                SourceWithEvidenceBounds(2, SemanticZone.Zone03, SemanticKind.Route, "C", new[] { 1, 0 }, EvidenceBounds(0, floorB), EvidenceBounds(1, floorB)),
                SourceWithEvidenceBounds(0, SemanticZone.Zone01, SemanticKind.Room, "A", new[] { 1, 0 }, EvidenceBounds(0, floorB, floorA2, floorA1), EvidenceBounds(1, floorB, floorA2, floorA1)));

            Assert.That(report.SeedEvidenceOverlapDetails.ConvertAll(detail => detail.NodeId), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(report.SeedEvidenceOverlapDetails[0].Claimants.ConvertAll(claimant => claimant.SourceIndex), Is.EqualTo(new[] { 0, 2 }));
            Assert.That(SupportingEvidenceKeys(report.SeedEvidenceOverlapDetails[0].Claimants[0]), Is.EqualTo(new[]
            {
                "Root/A/Floor|1,0,0|4,0.2,4",
                "Root/A/Floor|2,0,0|6,0.2,6",
                "Root/B/Floor|0,0,0|8,0.2,8"
            }));
        }

        [Test]
        public void STK_FullStation_BoundaryTieDoesNotCreateSeedOverlapSupportingEvidence()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(1, 0), 0, 2),
                Node(2, V(2, 0), 1));
            var left = EvidenceBound("Root/A/Floor", new Bounds(V(0, 0), new Vector3(1f, 0.2f, 1f)));
            var right = EvidenceBound("Root/B/Floor", new Bounds(V(2, 0), new Vector3(1f, 0.2f, 1f)));

            var report = Build(graph,
                SourceWithEvidenceBounds(0, SemanticZone.Zone01, SemanticKind.Room, "A", new[] { 0 }, EvidenceBounds(0, left)),
                SourceWithEvidenceBounds(1, SemanticZone.Zone01, SemanticKind.Room, "B", new[] { 2 }, EvidenceBounds(2, right)));

            Assert.That(report.BoundaryTieNodeIds, Is.EqualTo(new[] { 1 }));
            Assert.That(report.SeedEvidenceOverlapDetails, Is.Empty);
            Assert.That(SeedOverlapSupportingEvidenceCount(report), Is.EqualTo(0));
        }

        [Test]
        public void STK_FullStation_DiagnosticEvidenceDoesNotChangeBuildDryRunOwnershipOrResult()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(1, 0), 0, 2),
                Node(2, V(2, 0), 1, 3),
                Node(3, V(3, 0), 2));

            var baseline = Build(graph,
                Source(0, SemanticZone.Zone01, SemanticKind.Room, "A", 0),
                Source(1, SemanticZone.Zone02, SemanticKind.Room, "B", 3));

            var withDiagnostic = Build(graph,
                SourceWithEvidenceBounds(0, SemanticZone.Zone01, SemanticKind.Room, "A", new[] { 0 },
                    EvidenceBounds(0, EvidenceBound("Root/A/Floor", new Bounds(V(0, 0), new Vector3(2f, 0.2f, 2f))))),
                SourceWithEvidenceBounds(1, SemanticZone.Zone02, SemanticKind.Room, "B", new[] { 3 },
                    EvidenceBounds(3, EvidenceBound("Root/B/Floor", new Bounds(V(3, 0), new Vector3(2f, 0.2f, 2f))))));

            Assert.That(withDiagnostic.IsValid, Is.EqualTo(baseline.IsValid));
            Assert.That(NodeToRegionIds(withDiagnostic), Is.EqualTo(NodeToRegionIds(baseline)));
            Assert.That(RegionIds(withDiagnostic), Is.EqualTo(RegionIds(baseline)));
            Assert.That(BoundsKeys(withDiagnostic), Is.EqualTo(BoundsKeys(baseline)));
            Assert.That(EdgeKeys(withDiagnostic), Is.EqualTo(EdgeKeys(baseline)));
            Assert.That(withDiagnostic.SeedOverlapNodeIds, Is.EqualTo(baseline.SeedOverlapNodeIds));
            Assert.That(withDiagnostic.BoundaryTieNodeIds, Is.EqualTo(baseline.BoundaryTieNodeIds));
            Assert.That(withDiagnostic.MultiplyMappedNodeIds, Is.EqualTo(baseline.MultiplyMappedNodeIds));
            Assert.That(withDiagnostic.Errors, Is.EqualTo(baseline.Errors));
        }

        [Test]
        public void STK_FullStation_EqualDistanceMiddleNodeReportsBoundaryTieOnly()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(1, 0), 0, 2),
                Node(2, V(2, 0), 1));

            var report = Build(graph,
                Source(0, SemanticZone.Zone01, SemanticKind.Room, "A", 0),
                Source(1, SemanticZone.Zone01, SemanticKind.Room, "B", 2));

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.SeedOverlapNodeIds, Is.Empty);
            Assert.That(report.BoundaryTieNodeIds, Is.EqualTo(new[] { 1 }));
            Assert.That(report.SeedEvidenceOverlapDetails, Is.Empty);
            Assert.That(report.Errors, Has.Some.EqualTo("Node 1 is an equal-distance BFS semantic boundary tie."));
        }

        [Test]
        public void STK_FullStation_DiagnosticNodeListsAreSortedAndDeduplicated()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1, 2),
                Node(1, V(1, 0), 0, 3),
                Node(2, V(1, 1), 0, 7),
                Node(3, V(2, 0), 1, 4, 5),
                Node(4, V(3, 0), 3, 10),
                Node(5, V(3, 1), 3, 10),
                Node(6, V(3, 2), 7, 10),
                Node(7, V(2, 1), 2, 6, 8),
                Node(8, V(3, 3), 7, 10),
                Node(9, V(20, 0)),
                Node(10, V(4, 1), 4, 5, 6, 8),
                Node(11, V(21, 0)));

            var report = Build(graph,
                SourceWithEvidenceObjects(0, SemanticZone.Zone01, SemanticKind.Room, "A", new[] { 0, 9, 11 },
                    EvidenceObjects(9, "Root/A/FloorB", "Root/A/FloorA"),
                    EvidenceObjects(11, "Root/A/FloorC")),
                SourceWithEvidenceObjects(1, SemanticZone.Zone01, SemanticKind.Room, "B", new[] { 11, 10, 9, 9 },
                    EvidenceObjects(9, "Root/B/FloorA"),
                    EvidenceObjects(11, "Root/B/FloorB", "Root/B/FloorA")));

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.SeedOverlapNodeIds, Is.EqualTo(new[] { 9, 11 }));
            Assert.That(report.BoundaryTieNodeIds, Is.EqualTo(new[] { 3, 7 }));
            Assert.That(report.MultiplyMappedNodeIds, Is.EqualTo(new[] { 3, 7, 9, 11 }));
            Assert.That(SeedOverlapDetailKeys(report), Is.EqualTo(new[]
            {
                "9|0:Root/A/FloorA,Root/A/FloorB;1:Root/B/FloorA",
                "11|0:Root/A/FloorC;1:Root/B/FloorA,Root/B/FloorB"
            }));
        }

        [Test]
        public void STK_FullStation_OneSemanticCorridorCanGenerateMultipleGeometryPieceIds()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(1, 0), 0),
                Node(2, V(10, 0), 3),
                Node(3, V(11, 0), 2));

            var report = Build(graph, Source(0, SemanticZone.Zone02, SemanticKind.Route, "Route", 0, 2));

            AssertValid(report);
            Assert.That(report.Regions.Count, Is.EqualTo(2));
        }

        [Test]
        public void STK_FullStation_DisconnectedSameSourceOwnershipCreatesMultipleRuntimeRegionIds()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(1, 0), 0),
                Node(2, V(10, 0), 3),
                Node(3, V(11, 0), 2));

            var report = Build(graph, Source(0, SemanticZone.Zone02, SemanticKind.Route, "Route", 0, 2));

            AssertValid(report);
            Assert.That(report.Regions.Count, Is.EqualTo(2));
            Assert.That(report.RuntimeSemanticRegionCount, Is.EqualTo(2));
            Assert.That(RuntimeNodeToRegionIds(report), Is.EqualTo(new[] { 1, 1, 2, 2 }));
            Assert.That(RuntimeRegionKeys(report), Is.EqualTo(new[]
            {
                "1|0|Zone02|Route|Route|2|0|1",
                "2|0|Zone02|Route|Route|2|2|3"
            }));
        }

        [Test]
        public void STK_FullStation_DifferentSemanticSourcesProduceDifferentRuntimeRegionIds()
        {
            var graph = Graph(Node(0, V(0, 0), 1), Node(1, V(1, 0), 0));

            var report = Build(graph,
                Source(10, SemanticZone.Zone01, SemanticKind.Room, "A", 0),
                Source(20, SemanticZone.Zone01, SemanticKind.Room, "B", 1));

            AssertValid(report);
            Assert.That(report.RuntimeSemanticRegionCount, Is.EqualTo(2));
            Assert.That(RuntimeRegionKeys(report), Is.EqualTo(new[]
            {
                "1|10|Zone01|Room|A|1|0|0",
                "2|20|Zone01|Room|B|1|1|1"
            }));
            Assert.That(RuntimeNodeToRegionIds(report), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void STK_FullStation_RuntimeRegionIdsUseSourceIndexThenComponentMinNodeOrdering()
        {
            var graph = Graph(Node(0, V(0, 0)), Node(1, V(10, 0)));

            var report = Build(graph,
                Source(20, SemanticZone.Zone01, SemanticKind.Room, "B", 1),
                Source(10, SemanticZone.Zone01, SemanticKind.Room, "A", 0));

            AssertValid(report);
            Assert.That(RuntimeRegionKeys(report), Is.EqualTo(new[]
            {
                "1|10|Zone01|Room|A|1|0|0",
                "2|20|Zone01|Room|B|1|1|1"
            }));
            Assert.That(RuntimeNodeToRegionIds(report), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void STK_FullStation_SameSemanticSourceGeometryPieceAdjacencyDoesNotCreateRuntimeEdge()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(0, 1), 0, 2),
                Node(2, V(0, 2), 1, 3),
                Node(3, V(1, 2), 2, 4),
                Node(4, V(2, 2), 3, 5),
                Node(5, V(2, 1), 4, 6),
                Node(6, V(2, 0), 5),
                Node(7, V(1, 1)));

            var report = Build(graph,
                Source(0, SemanticZone.Zone02, SemanticKind.Route, "URoute", 0, 1, 2, 3, 4, 5, 6),
                Source(1, SemanticZone.Zone02, SemanticKind.Room, "RoomInsideU", 7));

            AssertValid(report);
            Assert.That(RegionCountFor(report, "URoute"), Is.GreaterThan(1));
            Assert.That(report.RuntimeSemanticRegionCount, Is.EqualTo(2));
            Assert.That(RuntimeNodeToRegionIds(report), Is.EqualTo(new[] { 1, 1, 1, 1, 1, 1, 1, 2 }));
            Assert.That(RuntimeRegionKeys(report), Is.EqualTo(new[]
            {
                "1|0|Zone02|Route|URoute|7|0|6",
                "2|1|Zone02|Room|RoomInsideU|1|7|7"
            }));
            Assert.That(HasGeometryEdgeBetweenRegionsFromSource(report, "URoute"), Is.True);
            Assert.That(RuntimeEdgesContainSelfTransition(report), Is.False);
            Assert.That(RuntimeRegionNodeSetsAreConnected(graph, report), Is.True);
        }

        [Test]
        public void STK_FullStation_DifferentSemanticSourceAdjacencyCreatesRuntimeEdges()
        {
            var graph = Graph(Node(0, V(0, 0), 1), Node(1, V(1, 0), 0));

            var report = Build(graph,
                Source(0, SemanticZone.Zone01, SemanticKind.Room, "A", 0),
                Source(1, SemanticZone.Zone02, SemanticKind.Route, "B", 1));

            AssertValid(report);
            Assert.That(RuntimeEdgeKeys(report), Is.EqualTo(new[] { "1->2", "2->1" }));
        }

        [Test]
        public void STK_FullStation_RuntimeSemanticGraphPreservesCompatibilityIdentity()
        {
            var graph = Graph(Node(0, V(0, 0), 1), Node(1, V(1, 0), 0));

            var report = Build(graph,
                Source(0, SemanticZone.Zone01, SemanticKind.Room, "A", 0),
                Source(1, SemanticZone.Zone02, SemanticKind.Route, "B", 1));

            AssertValid(report);
            Assert.That(report.RuntimeGraph.CompatibilityIdentity, Is.EqualTo(graph.CompatibilityIdentity));
            Assert.That(report.BakeDiagnostic.IsSuccess, Is.True);
        }

        [Test]
        public void STK_FullStation_SameInputTwiceGivesIdenticalRuntimeSemanticMappingAndEdges()
        {
            var first = BasicValidReport();
            var second = BasicValidReport();

            Assert.That(RuntimeNodeToRegionIds(first), Is.EqualTo(RuntimeNodeToRegionIds(second)));
            Assert.That(RuntimeRegionKeys(first), Is.EqualTo(RuntimeRegionKeys(second)));
            Assert.That(RuntimeEdgeKeys(first), Is.EqualTo(RuntimeEdgeKeys(second)));
        }

        [Test]
        public void STK_FullStation_UShapedCorridorDecomposesWithoutAabbContamination()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(0, 1), 0, 2),
                Node(2, V(0, 2), 1, 3),
                Node(3, V(1, 2), 2, 4),
                Node(4, V(2, 2), 3, 5),
                Node(5, V(2, 1), 4, 6),
                Node(6, V(2, 0), 5),
                Node(7, V(1, 1)));

            var report = Build(graph,
                Source(0, SemanticZone.Zone02, SemanticKind.Route, "URoute", 0, 1, 2, 3, 4, 5, 6),
                Source(1, SemanticZone.Zone02, SemanticKind.Room, "RoomInsideU", 7));

            AssertValid(report);
            Assert.That(report.ForeignNodeContamination, Is.Empty);
            Assert.That(RegionCountFor(report, "URoute"), Is.GreaterThan(1));
        }

        [Test]
        public void STK_FullStation_LShapedCorridorDecomposesCorrectly()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(1, 0), 0, 2),
                Node(2, V(2, 0), 1, 3),
                Node(3, V(2, 1), 2, 4),
                Node(4, V(2, 2), 3),
                Node(5, V(1, 1)));

            var report = Build(graph,
                Source(0, SemanticZone.Zone02, SemanticKind.Route, "LRoute", 0, 1, 2, 3, 4),
                Source(1, SemanticZone.Zone02, SemanticKind.Room, "RoomAtCorner", 5));

            AssertValid(report);
            Assert.That(report.ForeignNodeContamination, Is.Empty);
            Assert.That(RegionCountFor(report, "LRoute"), Is.GreaterThan(1));
        }

        [Test]
        public void STK_FullStation_VerticallyStackedFloorsDoNotOverlap()
        {
            var graph = Graph(Node(0, new Vector3(0f, 0f, 0f)), Node(1, new Vector3(0f, 3f, 0f)));

            var report = Build(graph,
                Source(0, SemanticZone.Zone01, SemanticKind.Room, "Lower", 0),
                Source(1, SemanticZone.Zone01, SemanticKind.Room, "Upper", 1));

            AssertValid(report);
            Assert.That(report.Regions[0].WorldBounds.Contains(graph.Nodes[1].Position), Is.False);
            Assert.That(report.Regions[1].WorldBounds.Contains(graph.Nodes[0].Position), Is.False);
        }

        [Test]
        public void STK_FullStation_StairConnectedLowerUpperRegionsRetainGraphAdjacency()
        {
            var graph = Graph(Node(0, new Vector3(0f, 0f, 0f), 1), Node(1, new Vector3(0f, 3f, 0f), 0));

            var report = Build(graph,
                Source(0, SemanticZone.Zone01, SemanticKind.Room, "Lower", 0),
                Source(1, SemanticZone.Zone01, SemanticKind.Room, "Upper", 1));

            AssertValid(report);
            Assert.That(HasEdge(report, 1, 2), Is.True);
            Assert.That(HasEdge(report, 2, 1), Is.True);
        }

        [Test]
        public void STK_FullStation_DisconnectedNodeClustersAreSplit()
        {
            var graph = Graph(Node(0, V(0, 0)), Node(1, V(4, 0)));

            var report = Build(graph, Source(0, SemanticZone.Zone01, SemanticKind.Room, "SplitRoom", 0, 1));

            AssertValid(report);
            Assert.That(report.Regions.Count, Is.EqualTo(2));
        }

        [Test]
        public void STK_FullStation_UnclaimedCorridorIslandClassifiedDeterministically()
        {
            var graph = Graph(Node(0, V(0, 0)), Node(1, V(10, 0)));
            var options = new FullStationRegionGraphAuthoringCore.BuildOptions(
                FullStationRegionGraphAuthoringCore.MaxDecompositionDepth,
                UnclaimedComponentClassification.IsolatedNavigableIsland,
                new[] { new FullStationRegionGraphAuthoringCore.ComponentClassification(1, UnclaimedComponentClassification.CorridorRoute) });

            var report = FullStationRegionGraphAuthoringCore.BuildDryRunReport(
                graph,
                new[] { Source(0, SemanticZone.Zone02, SemanticKind.Route, "Route", 0) },
                options);

            AssertValid(report);
            Assert.That(report.CorridorRouteNodeCount, Is.EqualTo(2));
        }

        [Test]
        public void STK_FullStation_GenuinelyUnresolvedIslandFailsClosed()
        {
            var graph = Graph(Node(0, V(0, 0)), Node(1, V(10, 0)));
            var options = new FullStationRegionGraphAuthoringCore.BuildOptions(
                FullStationRegionGraphAuthoringCore.MaxDecompositionDepth,
                UnclaimedComponentClassification.Unresolved,
                null);

            var report = FullStationRegionGraphAuthoringCore.BuildDryRunReport(
                graph,
                new[] { Source(0, SemanticZone.Zone01, SemanticKind.Room, "Room", 0) },
                options);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.UnresolvedNodeIds, Contains.Item(1));
        }

        [Test]
        public void STK_FullStation_RecursionLimitFailsClosed()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(0, 2), 0, 2),
                Node(2, V(2, 2), 1, 3),
                Node(3, V(2, 0), 2),
                Node(4, V(1, 1)));
            var options = new FullStationRegionGraphAuthoringCore.BuildOptions(0, UnclaimedComponentClassification.IsolatedNavigableIsland, null);

            var report = FullStationRegionGraphAuthoringCore.BuildDryRunReport(
                graph,
                new[]
                {
                    Source(0, SemanticZone.Zone02, SemanticKind.Route, "Route", 0, 1, 2, 3),
                    Source(1, SemanticZone.Zone02, SemanticKind.Room, "Room", 4)
                },
                options);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.DecompositionFailures, Is.Not.Empty);
        }

        [Test]
        public void STK_FullStation_EverySpatialNodeGetsExactlyOneRegionId()
        {
            var report = BasicValidReport();

            AssertValid(report);
            for (var i = 0; i < report.NodeToRegion.Length; i++)
            {
                Assert.That(report.NodeToRegion[i].IsValid, Is.True);
            }
        }

        [Test]
        public void STK_FullStation_NoForeignNodeContamination()
        {
            var report = BasicValidReport();

            AssertValid(report);
            Assert.That(report.ForeignNodeContamination, Is.Empty);
        }

        [Test]
        public void STK_FullStation_SameInputTwiceGivesIdenticalRegionIds()
        {
            var first = BasicValidReport();
            var second = BasicValidReport();

            Assert.That(RegionIds(first), Is.EqualTo(RegionIds(second)));
        }

        [Test]
        public void STK_FullStation_SameInputTwiceGivesIdenticalBounds()
        {
            var first = BasicValidReport();
            var second = BasicValidReport();

            Assert.That(BoundsKeys(first), Is.EqualTo(BoundsKeys(second)));
        }

        [Test]
        public void STK_FullStation_SameInputTwiceGivesIdenticalEdges()
        {
            var first = BasicValidReport();
            var second = BasicValidReport();

            Assert.That(EdgeKeys(first), Is.EqualTo(EdgeKeys(second)));
        }

        [Test]
        public void STK_FullStation_CompatibilityIdentityPreserved()
        {
            var graph = Graph(Node(0, V(0, 0), 1), Node(1, V(1, 0), 0));
            var report = Build(graph, Source(0, SemanticZone.Zone01, SemanticKind.Room, "A", 0), Source(1, SemanticZone.Zone01, SemanticKind.Room, "B", 1));
            var bake = FullStationRegionGraphAuthoringCore.BakeProposal(graph, report.Regions, report.Edges);

            AssertValid(report);
            Assert.That(bake.Succeeded, Is.True);
            Assert.That(bake.Graph.CompatibilityIdentity, Is.EqualTo(graph.CompatibilityIdentity));
        }

        [Test]
        public void STK_FullStation_FailedDryRunCannotCreateAsset()
        {
            var report = new FullStationRegionGraphAuthoringCore.DryRunReport();
            report.Errors.Add("invalid");

            var returned = FullStationRegionGraphAuthoringCore.BakeAsset(TempAssetPath, report);

            Assert.That(returned, Is.SameAs(report));
            Assert.That(AssetDatabase.LoadAssetAtPath<RegionGraphAsset>(TempAssetPath), Is.Null);
        }

        [Test]
        public void STK_FullStation_SpatialV3ProtectedAssetCannotBeOverwritten()
        {
            var graph = Graph(Node(0, V(0, 0)));
            var report = Build(graph, Source(0, SemanticZone.Zone01, SemanticKind.Room, "A", 0));

            var returned = FullStationRegionGraphAuthoringCore.BakeAsset(ProtectedAssetPath, report);

            Assert.That(returned.IsValid, Is.False);
            Assert.That(returned.Errors, Has.Some.Contains("Refusing to overwrite protected asset"));
        }

        private static FullStationRegionGraphAuthoringCore.DryRunReport BasicValidReport()
        {
            var graph = Graph(
                Node(0, V(0, 0), 1),
                Node(1, V(1, 0), 0, 2),
                Node(2, V(2, 0), 1, 3),
                Node(3, V(3, 0), 2));
            return Build(graph, Source(0, SemanticZone.Zone01, SemanticKind.Room, "A", 0), Source(1, SemanticZone.Zone02, SemanticKind.Route, "Route", 3));
        }

        private static FullStationRegionGraphAuthoringCore.DryRunReport Build(
            NavMeshSpatialGraph graph,
            params FullStationRegionGraphAuthoringCore.SemanticSource[] sources)
        {
            return FullStationRegionGraphAuthoringCore.BuildDryRunReport(graph, sources, FullStationRegionGraphAuthoringCore.BuildOptions.Default);
        }

        private static FullStationRegionGraphAuthoringCore.SemanticSource Source(
            int sourceIndex,
            SemanticZone zone,
            SemanticKind kind,
            string path,
            params int[] evidenceNodeIds)
        {
            return new FullStationRegionGraphAuthoringCore.SemanticSource(
                sourceIndex,
                zone,
                kind,
                path,
                Vector3.zero,
                FloorEvidenceKind.FloorEvidence,
                evidenceNodeIds);
        }

        private static FullStationRegionGraphAuthoringCore.SemanticSource SourceWithEvidenceObjects(
            int sourceIndex,
            SemanticZone zone,
            SemanticKind kind,
            string path,
            int[] evidenceNodeIds,
            params KeyValuePair<int, List<string>>[] evidenceObjects)
        {
            var evidenceObjectPathsByNode = new Dictionary<int, List<string>>();
            for (var i = 0; i < evidenceObjects.Length; i++)
            {
                evidenceObjectPathsByNode.Add(evidenceObjects[i].Key, evidenceObjects[i].Value);
            }

            return new FullStationRegionGraphAuthoringCore.SemanticSource(
                sourceIndex,
                zone,
                kind,
                path,
                Vector3.zero,
                FloorEvidenceKind.FloorEvidence,
                evidenceNodeIds,
                evidenceObjectPathsByNode);
        }

        private static KeyValuePair<int, List<string>> EvidenceObjects(int nodeId, params string[] paths)
        {
            return new KeyValuePair<int, List<string>>(nodeId, new List<string>(paths));
        }

        private static FullStationRegionGraphAuthoringCore.SemanticSource SourceWithEvidenceBounds(
            int sourceIndex,
            SemanticZone zone,
            SemanticKind kind,
            string path,
            int[] evidenceNodeIds,
            params KeyValuePair<int, List<FullStationRegionGraphAuthoringCore.FloorEvidenceBoundsDiagnostic>>[] evidenceBounds)
        {
            var evidenceBoundsByNode = new Dictionary<int, List<FullStationRegionGraphAuthoringCore.FloorEvidenceBoundsDiagnostic>>();
            var evidenceObjectPathsByNode = new Dictionary<int, List<string>>();
            for (var i = 0; i < evidenceBounds.Length; i++)
            {
                var bounds = evidenceBounds[i].Value != null
                    ? new List<FullStationRegionGraphAuthoringCore.FloorEvidenceBoundsDiagnostic>(evidenceBounds[i].Value)
                    : new List<FullStationRegionGraphAuthoringCore.FloorEvidenceBoundsDiagnostic>();
                evidenceBoundsByNode.Add(evidenceBounds[i].Key, bounds);

                var paths = new List<string>();
                for (var boundIndex = 0; boundIndex < bounds.Count; boundIndex++)
                {
                    if (!paths.Contains(bounds[boundIndex].HierarchyPath))
                    {
                        paths.Add(bounds[boundIndex].HierarchyPath);
                    }
                }

                paths.Sort();
                evidenceObjectPathsByNode.Add(evidenceBounds[i].Key, paths);
            }

            return new FullStationRegionGraphAuthoringCore.SemanticSource(
                sourceIndex,
                zone,
                kind,
                path,
                Vector3.zero,
                FloorEvidenceKind.FloorEvidence,
                evidenceNodeIds,
                evidenceObjectPathsByNode,
                evidenceBoundsByNode);
        }

        private static KeyValuePair<int, List<FullStationRegionGraphAuthoringCore.FloorEvidenceBoundsDiagnostic>> EvidenceBounds(
            int nodeId,
            params FullStationRegionGraphAuthoringCore.FloorEvidenceBoundsDiagnostic[] bounds)
        {
            return new KeyValuePair<int, List<FullStationRegionGraphAuthoringCore.FloorEvidenceBoundsDiagnostic>>(
                nodeId,
                new List<FullStationRegionGraphAuthoringCore.FloorEvidenceBoundsDiagnostic>(bounds));
        }

        private static FullStationRegionGraphAuthoringCore.FloorEvidenceBoundsDiagnostic EvidenceBound(string hierarchyPath, Bounds bounds)
        {
            return new FullStationRegionGraphAuthoringCore.FloorEvidenceBoundsDiagnostic(bounds, hierarchyPath);
        }

        private static int RegionCountFor(FullStationRegionGraphAuthoringCore.DryRunReport report, string sourcePath)
        {
            var count = 0;
            for (var i = 0; i < report.Regions.Count; i++)
            {
                if (report.Regions[i].SourcePath == sourcePath)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasEdge(FullStationRegionGraphAuthoringCore.DryRunReport report, int from, int to)
        {
            for (var i = 0; i < report.Edges.Count; i++)
            {
                if (report.Edges[i].FromRegionId == new RegionId(from) && report.Edges[i].ToRegionId == new RegionId(to))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasGeometryEdgeBetweenRegionsFromSource(FullStationRegionGraphAuthoringCore.DryRunReport report, string sourcePath)
        {
            for (var edgeIndex = 0; edgeIndex < report.Edges.Count; edgeIndex++)
            {
                var edge = report.Edges[edgeIndex];
                if (edge.FromRegionId == edge.ToRegionId)
                {
                    continue;
                }

                if (TryGetGeometryRegionSourcePath(report, edge.FromRegionId, out var fromSourcePath)
                    && TryGetGeometryRegionSourcePath(report, edge.ToRegionId, out var toSourcePath)
                    && fromSourcePath == sourcePath
                    && toSourcePath == sourcePath)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetGeometryRegionSourcePath(FullStationRegionGraphAuthoringCore.DryRunReport report, RegionId regionId, out string sourcePath)
        {
            for (var i = 0; i < report.Regions.Count; i++)
            {
                if (report.Regions[i].RegionId == regionId)
                {
                    sourcePath = report.Regions[i].SourcePath;
                    return true;
                }
            }

            sourcePath = null;
            return false;
        }

        private static string[] SeedOverlapDetailKeys(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            var keys = new string[report.SeedEvidenceOverlapDetails.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                var detail = report.SeedEvidenceOverlapDetails[i];
                var claimantKeys = new string[detail.Claimants.Count];
                for (var claimantIndex = 0; claimantIndex < claimantKeys.Length; claimantIndex++)
                {
                    var claimant = detail.Claimants[claimantIndex];
                    claimantKeys[claimantIndex] = $"{claimant.SourceIndex}:{string.Join(",", claimant.EvidenceObjectPaths)}";
                }

                keys[i] = $"{detail.NodeId}|{string.Join(";", claimantKeys)}";
            }

            return keys;
        }

        private static string[] SeedClaimantKeys(FullStationRegionGraphAuthoringCore.SeedEvidenceOverlapDetail detail)
        {
            var keys = new string[detail.Claimants.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                var claimant = detail.Claimants[i];
                keys[i] = $"{claimant.SourceIndex}|{claimant.Zone}|{claimant.Kind}|{claimant.SourcePath}|{claimant.EvidenceKind}|{string.Join(",", claimant.EvidenceObjectPaths)}";
            }

            return keys;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string[] SupportingEvidenceKeys(FullStationRegionGraphAuthoringCore.SeedEvidenceOverlapClaimant claimant)
        {
            var keys = new string[claimant.SupportingEvidence.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                var evidence = claimant.SupportingEvidence[i];
                keys[i] = $"{evidence.HierarchyPath}|{FormatFloat(evidence.BoundsCenter.x)},{FormatFloat(evidence.BoundsCenter.y)},{FormatFloat(evidence.BoundsCenter.z)}|{FormatFloat(evidence.BoundsSize.x)},{FormatFloat(evidence.BoundsSize.y)},{FormatFloat(evidence.BoundsSize.z)}";
            }

            return keys;
        }

        private static int SeedOverlapSupportingEvidenceCount(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            var count = 0;
            for (var detailIndex = 0; detailIndex < report.SeedEvidenceOverlapDetails.Count; detailIndex++)
            {
                var detail = report.SeedEvidenceOverlapDetails[detailIndex];
                for (var claimantIndex = 0; claimantIndex < detail.Claimants.Count; claimantIndex++)
                {
                    count += detail.Claimants[claimantIndex].SupportingEvidence.Count;
                }
            }

            return count;
        }

        private static int[] NodeToRegionIds(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            var ids = new int[report.NodeToRegion.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = report.NodeToRegion[i].Value;
            }

            return ids;
        }

        private static int[] RuntimeNodeToRegionIds(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            var ids = new int[report.RuntimeNodeToRegion.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = report.RuntimeNodeToRegion[i].Value;
            }

            return ids;
        }

        private static string[] RuntimeRegionKeys(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            var keys = new string[report.RuntimeRegions.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                var region = report.RuntimeRegions[i];
                keys[i] = $"{region.RegionId.Value}|{region.SourceIndex}|{region.Zone}|{region.Kind}|{region.SourcePath}|{region.SpatialNodeCount}|{region.MinNodeId}|{region.MaxNodeId}";
            }

            return keys;
        }

        private static string[] RuntimeEdgeKeys(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            var keys = new string[report.RuntimeEdges.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = $"{report.RuntimeEdges[i].FromRegionId.Value}->{report.RuntimeEdges[i].ToRegionId.Value}";
            }

            return keys;
        }

        private static bool RuntimeEdgesContainSelfTransition(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            for (var i = 0; i < report.RuntimeEdges.Count; i++)
            {
                if (report.RuntimeEdges[i].FromRegionId == report.RuntimeEdges[i].ToRegionId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RuntimeRegionNodeSetsAreConnected(NavMeshSpatialGraph graph, FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            for (var regionIndex = 0; regionIndex < report.RuntimeRegions.Count; regionIndex++)
            {
                var region = report.RuntimeRegions[regionIndex];
                var nodeIds = new List<int>();
                for (var nodeId = 0; nodeId < report.RuntimeNodeToRegion.Length; nodeId++)
                {
                    if (report.RuntimeNodeToRegion[nodeId] == region.RegionId)
                    {
                        nodeIds.Add(nodeId);
                    }
                }

                if (!NodeSetIsConnected(graph, nodeIds))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool NodeSetIsConnected(NavMeshSpatialGraph graph, List<int> nodeIds)
        {
            if (nodeIds.Count == 0)
            {
                return false;
            }

            var allowed = new HashSet<int>(nodeIds);
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(nodeIds[0]);
            visited.Add(nodeIds[0]);
            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                if (!graph.TryGetNode(nodeId, out var node))
                {
                    continue;
                }

                for (var i = 0; i < node.NeighborIds.Count; i++)
                {
                    var neighborId = node.NeighborIds[i];
                    if (allowed.Contains(neighborId) && visited.Add(neighborId))
                    {
                        queue.Enqueue(neighborId);
                    }
                }
            }

            return visited.Count == nodeIds.Count;
        }

        private static int[] RegionIds(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            var ids = new int[report.Regions.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = report.Regions[i].RegionId.Value;
            }

            return ids;
        }

        private static string[] BoundsKeys(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            var keys = new string[report.Regions.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                var b = report.Regions[i].WorldBounds;
                keys[i] = $"{FormatFloat(b.center.x)},{FormatFloat(b.center.y)},{FormatFloat(b.center.z)}|{FormatFloat(b.size.x)},{FormatFloat(b.size.y)},{FormatFloat(b.size.z)}";
            }

            return keys;
        }

        private static string[] EdgeKeys(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            var keys = new string[report.Edges.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = $"{report.Edges[i].FromRegionId.Value}->{report.Edges[i].ToRegionId.Value}";
            }

            return keys;
        }

        private static void AssertValid(FullStationRegionGraphAuthoringCore.DryRunReport report)
        {
            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
        }

        private static NavMeshSpatialGraph Graph(params SpatialNode[] nodes)
        {
            return new NavMeshSpatialGraph(nodes);
        }

        private static SpatialNode Node(int id, Vector3 position, params int[] neighbors)
        {
            return new SpatialNode(id, position, 0, id, id * 3, id * 3 + 1, id * 3 + 2, new List<int>(neighbors));
        }

        private static Vector3 V(float x, float z)
        {
            return new Vector3(x, 0f, z);
        }
    }
}
#endif
