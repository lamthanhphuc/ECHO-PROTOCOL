using UnityEngine;

namespace EchoProtocol.Salvage
{
    // Pooled, bounded local presentation. The Run decides which gameplay events occurred.
    // No quota, inventory, extraction, networking, player, or Monster dependencies.
    public sealed class SalvageEffects : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Material particleMaterial;
        public Camera ViewCamera {get;set;}
        private sealed class PulseState
        {
            public LineRenderer Line;
            public Vector3 Origin;
            public Color Color;
            public float Started,Duration,Radius;
            public bool Billboard;
        }
        private readonly PulseState[] _pulses=new PulseState[24];
        private readonly ParticleSystem[] _bursts=new ParticleSystem[8];
        private int _pulseIndex,_burstIndex;
        private SalvageRunState _run;
        private SalvagePhase _phase;
        private int _lastCountdown=-1;
        private void Awake()
        {
            for(int i=0;i<_pulses.Length;i++)
            {
                var go=new GameObject("Salvage pulse "+i);go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;
                line.positionCount=49;line.useWorldSpace=true;line.widthMultiplier=.025f;
                line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;
                _pulses[i]=new PulseState{Line=line};
            }
            for(int i=0;i<_bursts.Length;i++)
            {
                var go=new GameObject("Salvage contact burst "+i);go.transform.SetParent(transform,false);
                var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=1;main.startLifetime=new ParticleSystem.MinMaxCurve(.18f,.55f);
                main.startSpeed=new ParticleSystem.MinMaxCurve(.4f,2);main.startSize=new ParticleSystem.MinMaxCurve(.015f,.04f);main.gravityModifier=.6f;
                main.maxParticles=64;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=ps.emission;emission.enabled=false;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Hemisphere;shape.radius=.025f;
                var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
                go.GetComponent<ParticleSystemRenderer>().sharedMaterial=particleMaterial;_bursts[i]=ps;
            }
        }
        public void Pulse(Vector3 position,float radius,Color color,float duration=.8f,bool billboard=false)
        {
            if(_pulses[0]==null || !_pulses[0].Line || !Finite(position) || !float.IsFinite(radius) || !float.IsFinite(duration) || radius<=0 || duration<=0)return;
            var pulse=_pulses[_pulseIndex++%_pulses.Length];pulse.Origin=position;pulse.Radius=Mathf.Min(radius,100);
            pulse.Color=color;pulse.Duration=Mathf.Min(duration,10);pulse.Started=Time.time;pulse.Billboard=billboard;pulse.Line.enabled=true;
        }
        public void Burst(Vector3 position,Color color,int count=12)
        {
            if(!_bursts[0] || !Finite(position) || count<=0)return;
            var ps=_bursts[_burstIndex++%_bursts.Length];ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.transform.position=position;ps.Play();
            ps.Emit(new ParticleSystem.EmitParams{startColor=color},Mathf.Min(count,64));
        }
        private void LateUpdate()
        {
            var camera=ViewCamera?ViewCamera:SalvagePresentation.ViewCamera;
            foreach(var pulse in _pulses)
            {
                if(pulse==null || !pulse.Line.enabled)continue;
                float t=(Time.time-pulse.Started)/pulse.Duration;if(t>=1){pulse.Line.enabled=false;continue;}
                Vector3 right=pulse.Billboard && camera?camera.transform.right:Vector3.right;
                Vector3 up=pulse.Billboard && camera?camera.transform.up:Vector3.forward;
                float radius=Mathf.Lerp(.05f,pulse.Radius,t);pulse.Line.widthMultiplier=.035f*(1-t)+.002f;
                var tint=pulse.Color;tint.a*=1-t;pulse.Line.startColor=pulse.Line.endColor=tint;
                for(int i=0;i<49;i++){float angle=i*Mathf.PI*2/48;pulse.Line.SetPosition(i,pulse.Origin+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius);}
            }
            var run=SalvageRunState.Instance;
            if(run==null||run.Object==null||!run.Object.IsValid)return;
            if(_run!=run){_run=run;_phase=run.Phase;_lastCountdown=-1;}
            if(run.Phase!=_phase)
            {
                if(run.Phase==SalvagePhase.Failed)EchoProtocol.Audio.GameAudioRuntime.UI("escape_endgame/mission_failure");
                if(run.Phase==SalvagePhase.CycleSummary)EchoProtocol.Audio.GameAudioRuntime.UI("ui/confirm");
                _phase=run.Phase;_lastCountdown=-1;
            }
            if(run.Phase==SalvagePhase.InDay)
            {
                int second=Mathf.CeilToInt(run.SecondsRemaining);
                if(second!=_lastCountdown)
                {
                    if(second==60)EchoProtocol.Audio.GameAudioRuntime.UI("escape_endgame/countdown_start");
                    else if(second>=1&&second<=10)EchoProtocol.Audio.GameAudioRuntime.UI("escape_endgame/countdown_tick");
                    _lastCountdown=second;
                }
            }
        }
        private void OnDisable()
        {
            foreach(var pulse in _pulses)if(pulse!=null && pulse.Line)pulse.Line.enabled=false;
            foreach(var burst in _bursts)if(burst)burst.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
    }
}
