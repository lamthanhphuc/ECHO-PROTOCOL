using System.Collections.Generic;
using EchoProtocol.Tools.Scanner;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.Salvage
{
    // Presentation only: the host placement manifest and existing scan results remain authoritative.
    public sealed class SalvageScannerMap : MaskableGraphic
    {
        private struct Room { public Bounds Bounds; }
        private struct Link { public Vector3 A,B; public float Width; }
        private readonly List<Room> _rooms=new();
        private readonly List<Link> _links=new();
        private readonly HashSet<int> _visited=new();
        private readonly List<Vector3> _loot=new(),_crew=new();
        private bool _hasPose,_motion;
        private Vector3 _basePosition;
        private NetworkFieldScanner _scanner;
        private SalvageRunState _run;
        private int _revision=-1;
        private float _nextRefresh;
        private Vector3 _position;
        private Quaternion _rotation;
        private const float Radius=48;
        private static readonly Color Green=new(.28f,1,.12f,1);
        public static SalvageScannerMap Attach(RectTransform parent,NetworkFieldScanner scanner)
        {
            var existing=parent.Find("SalvageMapViewport");
            if(existing==null)
            {
                var viewport=new GameObject("SalvageMapViewport",typeof(RectTransform),typeof(RectMask2D));
                viewport.layer=parent.gameObject.layer;viewport.transform.SetParent(parent,false);Stretch((RectTransform)viewport.transform);
                var go=new GameObject("SeedMap",typeof(RectTransform),typeof(CanvasRenderer));go.layer=parent.gameObject.layer;go.transform.SetParent(viewport.transform,false);
                Stretch((RectTransform)go.transform);go.AddComponent<SalvageScannerMap>();
                existing=viewport.transform;
            }
            existing.gameObject.SetActive(true);
            var map=existing.GetComponentInChildren<SalvageScannerMap>(true);map._scanner=scanner;return map;
        }
        private static void Stretch(RectTransform rect)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.pivot=Vector2.one*.5f;}
        protected override void Awake(){base.Awake();raycastTarget=false;}
        private void Update()
        {
            if(Time.unscaledTime<_nextRefresh)return;_nextRefresh=Time.unscaledTime+.1f;
            var run=SalvageRunState.Instance;
            if(run!=_run||run!=null&&run.MapRevision!=_revision)
            {_run=run;_revision=run!=null?run.MapRevision:-1;_visited.Clear();BuildManifest();}
            _hasPose=_scanner!=null&&_scanner.Object!=null&&_scanner.Object.IsValid&&_run!=null&&_run.Object!=null&&_run.Object.IsValid;
            if(!_hasPose){SetVerticesDirty();return;}
            _motion=_scanner.CurrentMode==FieldScannerMode.Motion&&!_scanner.IsAlienHuntMode;
            _loot.Clear();_crew.Clear();
            if(_scanner.IsScanActive&&_scanner.HasActiveResult)foreach(var offset in _scanner.RadarOffsets)_loot.Add(_scanner.transform.position+offset);
            for(int i=0;i<4;i++){var player=_run.Player(i);if(player!=null&&player!=_scanner.Object)_crew.Add(player.transform.position);}
            _basePosition=_run.Site.ReturnPoint.position;
            _position=_scanner.transform.position;_rotation=Quaternion.Euler(0,-_scanner.transform.eulerAngles.y,0);
            for(int i=0;i<_rooms.Count;i++)
            {var b=_rooms[i].Bounds;var point=_position;point.y=b.center.y;if(b.Contains(point))_visited.Add(i);}
            SetVerticesDirty();
        }
        private void BuildManifest()
        {
            _rooms.Clear();_links.Clear();
            if(_run==null||_run.Object==null||!_run.Object.IsValid||_run.Site?.RandomMap==null)return;
            var map=_run.Site.RandomMap;int count=_run.MapPlacementCount;
            var used=new HashSet<(int,int)>();
            for(int i=0;i<count;i++)
            {
                var p=_run.MapPlacements[i].Decode();var module=i==0?map.FixedBase:map.Rooms[p.Module];
                _rooms.Add(new Room {Bounds=SalvageMapLayout.WorldBounds(p,module.Footprint)});
                if(i==0)continue;
                var parent=_run.MapPlacements[p.Parent].Decode();var parentModule=p.Parent==0?map.FixedBase:map.Rooms[parent.Module];
                _links.Add(new Link {A=SalvageMapLayout.SocketPoint(parent,parentModule.Sockets[p.ParentSocket]),B=SalvageMapLayout.SocketPoint(p,module.Sockets[p.EntrySocket]),Width=map.Passage.Sockets[0].Width});
                used.Add((p.Parent,p.ParentSocket));used.Add((i,p.EntrySocket));
            }
            for(int i=0;i<count;i++)
            {
                var p=_run.MapPlacements[i].Decode();var module=i==0?map.FixedBase:map.Rooms[p.Module];
                for(int s=0;s<module.Sockets.Length;s++)if(!used.Contains((i,s)))
                {var a=SalvageMapLayout.SocketPoint(p,module.Sockets[s]);_links.Add(new Link {A=a,B=a+SalvageMapLayout.Direction(SalvageMapLayout.SocketDirection(p,module.Sockets[s]))*6,Width=map.Passage.Sockets[0].Width});}
            }
        }
        private Vector2 Project(Vector3 world)
        {var p=_rotation*(world-_position);float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/(Radius*2);return new Vector2(p.x,p.z)*scale;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=rectTransform.rect;
            Quad(vh,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax),new Color(.005f,.025f,.005f,.97f));
            if(!_hasPose)return;
            float scale=Mathf.Clamp(Mathf.Min(rect.width,rect.height)/180,.35f,1.5f);
            foreach(var link in _links)
            {
                var d=(link.B-link.A).normalized;var side=Vector3.Cross(Vector3.up,d)*link.Width*.5f;
                Quad(vh,Project(link.A-side),Project(link.A+side),Project(link.B+side),Project(link.B-side),new Color(.2f,.65f,.1f,.35f));
            }
            for(int i=0;i<_rooms.Count;i++)
            {
                var b=_rooms[i].Bounds;var a=Project(new Vector3(b.min.x,0,b.min.z));var c=Project(new Vector3(b.max.x,0,b.max.z));
                var bb=Project(new Vector3(b.max.x,0,b.min.z));var d=Project(new Vector3(b.min.x,0,b.max.z));
                bool seen=_visited.Contains(i);var ink=seen?Green:new Color(.3f,.55f,.12f,.45f);
                Quad(vh,a,bb,c,d,seen?new Color(.2f,.85f,.06f,.25f):new Color(.08f,.2f,.04f,.15f));
                Line(vh,a,bb,scale,ink);Line(vh,bb,c,scale,ink);Line(vh,c,d,scale,ink);Line(vh,d,a,scale,ink);
            }
            foreach(var point in _loot)Dot(vh,Project(point),2.5f*scale,_motion?new Color(1,.35f,.15f):new Color(.2f,1,1));
            foreach(var point in _crew)Dot(vh,Project(point),2*scale,new Color(.9f,.95f,.6f));
            Dot(vh,Project(_basePosition),3*scale,new Color(1,.75f,.2f));
            for(float y=rect.yMin;y<rect.yMax;y+=5*scale)Line(vh,new Vector2(rect.xMin,y),new Vector2(rect.xMax,y),.4f*scale,new Color(0,0,0,.12f));
            Triangle(vh,new Vector2(0,5)*scale,new Vector2(-3,-3)*scale,new Vector2(3,-3)*scale,Color.white);
        }
        private static void Dot(VertexHelper vh,Vector2 p,float r,Color c)
        {Quad(vh,p+Vector2.up*r,p+Vector2.right*r,p+Vector2.down*r,p+Vector2.left*r,c);}
        private static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
        {var d=(b-a).normalized;var s=new Vector2(-d.y,d.x)*width*.5f;Quad(vh,a-s,a+s,b+s,b-s,c);}
        private static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {int n=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
        private static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color color)
        {int n=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);}
    }
}
