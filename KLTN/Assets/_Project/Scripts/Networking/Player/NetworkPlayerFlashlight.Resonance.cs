using EchoProtocol.AI.Minions;
using EchoProtocol.Settings;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    public sealed partial class NetworkPlayerFlashlight
    {
        public const float ResonanceRange = 10f;
        [Networked] public float ResonanceEnergy { get; private set; }
        [Networked] public NetworkBool ResonanceActive { get; private set; }
        [Networked] public NetworkBool ResonanceLocked { get; private set; }
        [Networked] private NetworkBool ResonanceNeedsRelease { get; set; }
        [Networked] private float ResonanceRechargeDelay { get; set; }
        [Networked] public Vector3 ResonanceEndpoint { get; private set; }
        private CreepMinionRuntime _resonanceTarget;
        private LineRenderer _resonanceBeam;
        private Material _resonanceMaterial;
        public CreepMinionRuntime ResonanceTarget => _resonanceTarget;
        public static bool IsResonating(GameObject player)
        {
            var flashlight = player.GetComponent<NetworkPlayerFlashlight>();
            if(flashlight==null || flashlight.Object==null || !flashlight.Object.IsValid)return false;
            if(flashlight.Object.HasStateAuthority && flashlight.GetInput(out NetworkPlayerInput input)
                && input.ResonanceHeld && !flashlight.ResonanceLocked && !flashlight.ResonanceNeedsRelease
                && flashlight.ResonanceEnergy>0f && flashlight.CanUseFlashlight())return true;
            return flashlight != null && flashlight.Object != null && flashlight.Object.IsValid
                && (flashlight.ResonanceActive || flashlight.Object.HasInputAuthority
                    && Application.isFocused && Keyboard.current != null && Keyboard.current.qKey.isPressed
                    && !flashlight.ResonanceLocked && flashlight.ResonanceEnergy > 0f
                    && SceneManager.GetActiveScene().name != "Lobby");
        }
        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            bool hasInput=GetInput(out NetworkPlayerInput input);
            bool held=hasInput && input.ResonanceHeld;
            var movement=GetComponent<NetworkPlayerMovement>();
            bool allowed=CanUseFlashlight() && (movement==null || !movement.IsHidden)
                && !PlayerInteractionControlLock.IsGameplayInputBlocked(gameObject);
            var next=ResonanceRules.Tick(new ResonanceEnergyState{Energy=ResonanceEnergy,Active=ResonanceActive,
                Locked=ResonanceLocked,NeedsRelease=ResonanceNeedsRelease,RechargeDelay=ResonanceRechargeDelay},held,allowed,Runner.DeltaTime);
            ResonanceEnergy=next.Energy;ResonanceActive=next.Active;ResonanceLocked=next.Locked;
            ResonanceNeedsRelease=next.NeedsRelease;ResonanceRechargeDelay=next.RechargeDelay;
            _resonanceTarget=null;
            if(ResonanceActive)ResolveResonanceTarget(input);
        }
        private void ResolveResonanceTarget(NetworkPlayerInput input)
        {
            if(float.IsNaN(input.LookYaw) || float.IsInfinity(input.LookYaw)
                || float.IsNaN(input.LookPitch) || float.IsInfinity(input.LookPitch))return;
            var movement=GetComponent<NetworkPlayerMovement>();
            Vector3 origin=transform.position+Vector3.up*(movement!=null && movement.IsCrouching ? 1f:1.6f);
            Vector3 forward=Quaternion.Euler(Mathf.Clamp(input.LookPitch,-89f,89f),input.LookYaw,0f)*Vector3.forward;
            ResonanceEndpoint=origin+forward*ResonanceRange;
            var hits=Physics.SphereCastAll(origin,.18f,forward,ResonanceRange,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits) {
                if(hit.collider.transform.IsChildOf(transform))continue;
                ResonanceEndpoint=hit.point;
                var minion=hit.collider.GetComponentInParent<CreepMinionRuntime>();
                if(minion==null || minion.IsDying)return;
                // A camera-visible target must also be reachable from the flashlight muzzle.
                Vector3 muzzle=BeamTransform.position;
                Vector3 target=hit.collider.bounds.center;
                var obstruction=Physics.RaycastAll(muzzle,(target-muzzle).normalized,(target-muzzle).magnitude,~0,QueryTriggerInteraction.Ignore);
                foreach(var block in obstruction)
                    if(!block.collider.transform.IsChildOf(transform)
                        && block.collider.GetComponentInParent<CreepMinionRuntime>()!=minion)return;
                _resonanceTarget=minion;ResonanceEndpoint=target;return;
            }
        }
        private void ApplyResonanceVisuals()
        {
            bool active=Object!=null && Object.IsValid && ResonanceActive;
            _flashlight.color=active ? new Color(.25f,.9f,1f):new Color(1f,.96f,.88f);
            _flashlight.range=active ? ResonanceRange:_beamRange;
            _flashlight.intensity=_beamIntensity*(active && ResonanceEnergy<=15f ? .7f+.3f*Mathf.Abs(Mathf.Sin(Time.time*30f)):1f);
            if(active && _resonanceBeam==null) {
                var beam=new GameObject("ResonanceBeam");beam.transform.SetParent(_flashlight.transform,false);
                _resonanceBeam=beam.AddComponent<LineRenderer>();_resonanceBeam.positionCount=2;
                _resonanceBeam.startWidth=.025f;_resonanceBeam.endWidth=.07f;
                _resonanceBeam.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                _resonanceBeam.receiveShadows=false;
                var shader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if(shader!=null){_resonanceMaterial=new Material(shader);_resonanceMaterial.color=new Color(.2f,1f,1f);_resonanceBeam.sharedMaterial=_resonanceMaterial;}
            }
            if(_resonanceBeam!=null){_resonanceBeam.enabled=active;if(active){_resonanceBeam.SetPosition(0,_flashlight.transform.position);_resonanceBeam.SetPosition(1,ResonanceEndpoint);}}
        }
    }

    public sealed class ResonanceHUD : MonoBehaviour
    {
        private NetworkPlayerFlashlight _flashlight;
        private AudioSource _audio;
        private AudioClip _beep;
        private float _nextBeep;
        private void Awake()=>_flashlight=GetComponent<NetworkPlayerFlashlight>();
        private void Update()
        {
            if(_flashlight==null || _flashlight.Object==null || !_flashlight.Object.IsValid
                || !_flashlight.Object.HasInputAuthority || !_flashlight.ResonanceActive
                || _flashlight.ResonanceEnergy>15f || Time.unscaledTime<_nextBeep)return;
            _nextBeep=Time.unscaledTime+.8f;
            if(_audio==null){_audio=gameObject.AddComponent<AudioSource>();_audio.playOnAwake=false;_audio.spatialBlend=0f;_audio.volume=.1f;}
            if(_beep==null){int count=4410;var samples=new float[count];for(int i=0;i<count;i++)samples[i]=Mathf.Sin(i*2f*Mathf.PI*880f/44100f)*(1f-i/(float)count);_beep=AudioClip.Create("ResonanceLowEnergy",count,1,44100,false);_beep.SetData(samples,0);}
            _audio.PlayOneShot(_beep);
        }
        private void OnDestroy(){if(_beep!=null)Destroy(_beep);if(_audio!=null)Destroy(_audio);}
    }
}
