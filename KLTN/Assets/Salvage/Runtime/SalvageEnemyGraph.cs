using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.Common.Spatial;
using EchoProtocol.AI.Stalker.Spatial;
using UnityEngine;

namespace EchoProtocol.Salvage
{
    public static class SalvageEnemyGraph
    {
        public static RegionGraphAsset Build(SalvageRandomMap map)
        {
            var spatial=NavMeshSpatialGraphBuilder.Build();
            var points=map.NavigationSamples.Select(t=>t.position).ToList();points.Insert(0,map.FixedBase.transform.position);
            var regions=new RegionId[spatial.NodeCount];
            for(int i=0;i<regions.Length;i++)
            {
                int best=0;float distance=float.MaxValue;
                for(int j=0;j<points.Count;j++) {float d=(spatial.Nodes[i].Position-points[j]).sqrMagnitude;if(d<distance){best=j;distance=d;}}
                regions[i]=new RegionId(best+1);
            }
            var nodes=new List<RegionNode>();
            foreach(var group in Enumerable.Range(0,regions.Length).GroupBy(i=>regions[i]))
            {
                var neighbors=new HashSet<RegionId>();
                foreach(int index in group)foreach(int other in spatial.Nodes[index].NeighborIds)
                    if(regions[other]!=group.Key)neighbors.Add(regions[other]);
                bool home=group.Key.Value==1;
                nodes.Add(new RegionNode(group.Key,neighbors.Select(id=>new RegionEdge(id,DoorId.Invalid)).ToArray(),
                    new RegionSemanticMetadata(group.Key.Value,"Salvage/RuntimeRoom/"+group.Key.Value,
                        home?RegionSemanticZone.Isolated:RegionSemanticZone.Zone01,
                        home?RegionSemanticKind.IsolatedIsland:RegionSemanticKind.Room)));
            }
            var graph=new RegionGraph(nodes,regions,spatial.CompatibilityIdentity,1);
            var failure=RegionGraph.Validate(graph,spatial);
            if(failure!=RegionGraphValidationFailure.None)throw new System.InvalidOperationException("Enemy graph: "+failure);
            var asset=ScriptableObject.CreateInstance<RegionGraphAsset>();asset.ConfigureFromRuntimeGraph(graph,spatial.NodeCount);return asset;
        }
    }
}
