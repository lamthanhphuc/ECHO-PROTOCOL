using EchoProtocol.Audio;
using EchoProtocol.Networking;
using EchoProtocol.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoProtocol.Salvage
{
    // Local presentation of the replicated grip. Never applies forces or emits AI noise.
    [DefaultExecutionOrder(300)]
    public sealed class SalvageGripVfx : MonoBehaviour
    {
        public Material BeamMaterial, OrbMaterial, VolumeMaterial;
        [ColorUsage(true,true)] public Color Energy = new Color(.18f,1.25f,1.1f,1);
        [ColorUsage(true,true)] public Color Contact = new Color(.2f,1.25f,1.1f,1);
        [Range(.005f,.03f)] public float CoreWidth=.012f;
        [Range(0,.15f)] public float HumVolume=.055f;
        private const int Segments=32, Sides=12;
        private SalvageScrap _item;
        private LineRenderer[] _lines;
        private Transform _node, _volume;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private readonly Vector3[] _curve=new Vector3[Segments+1];
        private AudioSource _hum;
        private float _visibility,_strain,_impactUntil;
        private MaterialPropertyBlock _properties;
        private Renderer _nodeRenderer;
        private Vector3 _previousEnd,_motion;
        private int _owner=-1;
        public float Tension=>_strain;
        public void Bind(SalvageScrap item) { _item=item; }
        private void Awake()
        {
            _properties=new MaterialPropertyBlock();
            _lines=new LineRenderer[4];
            for(int i=0;i<4;i++)
            {
                var go=new GameObject(i==0?"Energy core":i==1?"Soft halo":"Braided filament "+i);go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=BeamMaterial;line.useWorldSpace=true;
                line.positionCount=Segments+1;line.numCapVertices=4;line.numCornerVertices=2;line.enabled=false;
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.textureMode=LineTextureMode.Stretch;
                _lines[i]=line;
            }
            _node=CreateBillboard("Magnetic contact",OrbMaterial);
            _node.SetParent(transform,false);_node.localScale=Vector3.zero;_nodeRenderer=_node.GetComponent<Renderer>();
            var volume=new GameObject("Translucent grip field");volume.transform.SetParent(transform,false);_volume=volume.transform;
            _mesh=new Mesh{name="Salvage bounded grip field"};_mesh.MarkDynamic();
            _vertices=new Vector3[(Segments+1)*(Sides+1)];var uv=new Vector2[_vertices.Length];var triangles=new int[Segments*Sides*6];
            for(int i=0;i<=Segments;i++)for(int j=0;j<=Sides;j++)uv[i*(Sides+1)+j]=new Vector2(i/(float)Segments,j/(float)Sides);
            int n=0;for(int i=0;i<Segments;i++)for(int j=0;j<Sides;j++)
            {int a=i*(Sides+1)+j,b=a+Sides+1;triangles[n++]=a;triangles[n++]=b;triangles[n++]=a+1;triangles[n++]=a+1;triangles[n++]=b;triangles[n++]=b+1;}
            _mesh.vertices=_vertices;_mesh.uv=uv;_mesh.triangles=triangles;
            var colors=new Color[_vertices.Length];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;_mesh.colors=colors;
            volume.AddComponent<MeshFilter>().sharedMesh=_mesh;var renderer=volume.AddComponent<MeshRenderer>();renderer.sharedMaterial=VolumeMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var emitter=new GameObject("Grip field hum");emitter.transform.SetParent(transform,false);
            _hum=GameAudioRuntime.CreateSource(emitter,true);_hum.loop=true;_hum.minDistance=.7f;_hum.maxDistance=9;
            _hum.volume=0;
            _hum.clip=GameAudioRuntime.FindClip("energy_core/ambient_hum_loop");
        }
        public static Transform CreateBillboard(string label,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=label;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go.transform;
        }
        public void Impact() { _impactUntil=Time.unscaledTime+.28f; }
        private void LateUpdate()
        {
            var run=SalvageRunState.Instance;
            bool held=_item!=null&&_item.TryPresentationCargo(out var cargo)&&cargo.State==SalvageCargoState.PhysicsHeld
                &&run!=null&&run.Object!=null&&run.Object.IsValid;
            if(!held){_visibility=Mathf.MoveTowards(_visibility,0,Time.unscaledDeltaTime*9);SetAlpha();_hum.volume*=Mathf.Clamp01(1-Time.unscaledDeltaTime*20);if(_visibility<=0)Destroy(gameObject);return;}
            _visibility=Mathf.MoveTowards(_visibility,1,Time.unscaledDeltaTime*10);
            var c=run.Cargo(_item.Index);var player=run.Player(c.Owner);var camera=SalvagePresentation.ViewCamera;
            if(player==null||camera==null){_hum.Stop();return;}
            bool local=player.HasInputAuthority;
            var hand=PlayerHeldItemAnchor.ResolveRightHandAnchor(player.gameObject);
            Vector3 start=hand!=null?hand.position:SalvageRunState.GripEye(player);
            if(local)
            {
                var screen=camera.WorldToViewportPoint(start);
                if(screen.z<.15f||screen.z>1.2f||screen.x<.45f||screen.x>.95f||screen.y<.03f||screen.y>.5f)
                    start=camera.ViewportToWorldPoint(new Vector3(.78f,-.08f,.48f));
            }
            Vector3 end=_item.GripPoint;
            if(_owner!=c.Owner){_previousEnd=end;_motion=Vector3.zero;_owner=c.Owner;}
            float dt=Mathf.Clamp(Time.unscaledDeltaTime,.001f,.05f);
            _motion=Vector3.Lerp(_motion,Vector3.ClampMagnitude((end-_previousEnd)/dt,3),1-Mathf.Exp(-dt*8));_previousEnd=end;
            Vector3 eye=SalvageRunState.GripEye(player),target=eye+SalvageRunState.GripLook(player)*Vector3.forward*run.Crew[c.Owner].GripDistance;
            _strain=Mathf.Lerp(_strain,Mathf.Clamp01(Vector3.Distance(target,_item.WorldBounds.center)/.75f),1-Mathf.Exp(-dt*9));
            bool reduced=PlayerCameraSettings.ReduceMotion;
            Vector3 right=camera.transform.right,up=camera.transform.up;
            float impact=Mathf.Clamp01((_impactUntil-Time.unscaledTime)/.28f);
            Color color=Color.Lerp(Energy,new Color(2.2f,.48f,.12f,1),Mathf.Max(impact,_strain*.5f));
            for(int i=0;i<=Segments;i++)
            {
                float t=i/(float)Segments, envelope=Mathf.Sin(t*Mathf.PI);
                Vector3 bend=Vector3.down*(.045f*(1-_strain))-_motion*(reduced?0:.025f);
                _curve[i]=Vector3.Lerp(start,end,t)+bend*envelope;
                for(int layer=0;layer<4;layer++)
                {
                    float angle=t*Mathf.PI*5-Time.time*4+layer*Mathf.PI;
                    float radius=layer<2||reduced?0:(.015f+.018f*(1-_strain))*envelope;
                    _lines[layer].SetPosition(i,_curve[i]+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius);
                }
                float width=Mathf.Lerp(.008f,.075f,t)*_visibility;
                for(int j=0;j<=Sides;j++)
                {float a=j*Mathf.PI*2/Sides;_vertices[i*(Sides+1)+j]=_curve[i]+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*width;}
            }
            _volume.SetPositionAndRotation(Vector3.zero,Quaternion.identity);_volume.localScale=Vector3.one;
            _mesh.vertices=_vertices;_mesh.RecalculateBounds();
            for(int i=0;i<4;i++)
            {
                _lines[i].enabled=true;
                _lines[i].widthMultiplier=CoreWidth*(i==1?5.5f:i==0?1:.5f)*_visibility;
                var tint=i==0?Color.Lerp(color,Color.white,.55f):color;tint.a=_visibility*(i==1?.16f:i==0?.95f:.65f);
                _lines[i].startColor=tint;_lines[i].endColor=tint;
            }
            _node.position=end-camera.transform.forward*.008f;_node.rotation=camera.transform.rotation;
            _node.localScale=Vector3.one*(.07f+impact*.035f)*_visibility;
            _properties.SetColor("_Tint",Color.Lerp(Contact,new Color(2,.5f,.1f,1),impact));_nodeRenderer.SetPropertyBlock(_properties);
            float desired=Time.timeScale>0&&(Application.isFocused||Application.runInBackground)?HumVolume*Mathf.Lerp(.35f,1,_strain)*_visibility:0;
            if(EchoProtocol.Voice.VoicePlayerBinding.AnyRemoteSpeaking)desired*=.45f;
            _hum.transform.position=start;_hum.volume=Mathf.MoveTowards(_hum.volume,desired,dt*.5f);_hum.pitch=.9f+_strain*.18f;
            if(_hum.clip!=null&&_hum.volume>.001f&&!_hum.isPlaying)_hum.Play();
        }
        private void SetAlpha(){if(_lines==null)return;foreach(var line in _lines)line.widthMultiplier=CoreWidth*_visibility;_node.localScale=Vector3.one*.07f*_visibility;_volume.gameObject.SetActive(false);}
        private void OnDisable(){if(_hum!=null)_hum.Stop();}
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_node!=null)Destroy(_node.gameObject);}
    }
}
