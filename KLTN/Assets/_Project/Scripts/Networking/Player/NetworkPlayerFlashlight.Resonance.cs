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
        public const float ResonanceRange = 24f;
        public const float ResonanceVisualRange = 24f;
        private const float ResonanceHitRadius = 0.75f;
        [Networked] public float ResonanceEnergy { get; private set; }
        [Networked] public NetworkBool ResonanceActive { get; private set; }
        [Networked] public NetworkBool ResonanceLocked { get; private set; }
        [Networked] public NetworkBool ResonanceHasValidHit { get; private set; }
        [Networked] private NetworkBool ResonanceNeedsRelease { get; set; }
        [Networked] private float ResonanceRechargeDelay { get; set; }
        [Networked] public Vector3 ResonanceEndpoint { get; private set; }
        private CreepMinionRuntime _resonanceTarget;
        public EchoProtocol.AI.Stalker.Networking.StalkerFusionRuntime PurgeBossTarget { get; private set; }
        private bool _resonanceWasVisible;
        private float _resonanceStartedAt;
        private float _resonanceDistanceGain = 1f;
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
            _resonanceTarget=null; PurgeBossTarget=null;
            if(ResonanceActive)ResolveResonanceTarget(input);
            // Target references are authority-local; the UI contact signal must also reach the owner client.
            var match = NetworkMatchState.Instance;
            bool bossAffected = PurgeBossTarget != null && match != null && match.Object != null && match.Object.IsValid
                && match.PurgeUvImmunity.ExpiredOrNotRunning(Runner)
                && (match.PurgeMood == PurgeBossMood.Calm || match.PurgeMood == PurgeBossMood.Hunting);
            ResonanceHasValidHit = ResonanceActive && (_resonanceTarget != null || bossAffected);
        }
        private void ResolveResonanceTarget(NetworkPlayerInput input)
        {
            if(float.IsNaN(input.LookYaw) || float.IsInfinity(input.LookYaw)
                || float.IsNaN(input.LookPitch) || float.IsInfinity(input.LookPitch))return;
            var movement=GetComponent<NetworkPlayerMovement>();
            Vector3 origin = transform.position + Vector3.up * (movement != null && movement.IsCrouching ? 1.3f : 1.65f);
            Vector3 forward = Quaternion.Euler(Mathf.Clamp(input.LookPitch, -89f, 89f), input.LookYaw, 0f) * Vector3.forward;
            ResonanceEndpoint = origin + forward * ResonanceRange;
            foreach (var collider in QueryResonanceCandidates(transform, origin, forward))
            {
                if (IsDespawnedMinionCollider(collider)) continue;
                var boss = collider.GetComponentInParent<EchoProtocol.AI.Stalker.Networking.StalkerFusionRuntime>();
                var minion = collider.GetComponentInParent<CreepMinionRuntime>();
                if (!(AlienHuntMission.Instance && boss) && (minion == null || minion.IsDying)) continue;
                if (!ResonanceLineOfSight(transform, origin, collider)) continue;
                ResonanceEndpoint = collider.bounds.center;
                if (AlienHuntMission.Instance && boss) PurgeBossTarget = boss;
                else _resonanceTarget = minion;
                return;
            }
        }

        // Sphere casts miss colliders overlapping their starting sphere. Keep those candidates too.
        internal static Collider[] QueryResonanceCandidates(Transform owner, Vector3 origin, Vector3 forward)
        {
            var candidates = new System.Collections.Generic.HashSet<Collider>();
            foreach (var collider in Physics.OverlapSphere(origin, ResonanceHitRadius, ~0, QueryTriggerInteraction.Ignore))
                candidates.Add(collider);
            foreach (var hit in Physics.SphereCastAll(origin, ResonanceHitRadius, forward, ResonanceRange, ~0, QueryTriggerInteraction.Ignore))
                candidates.Add(hit.collider);
            candidates.RemoveWhere(collider => IgnoreResonanceCollider(owner, collider)
                || Vector3.Dot(collider.bounds.center - origin, forward) < -.01f
                || (collider.ClosestPoint(origin) - origin).sqrMagnitude > ResonanceRange * ResonanceRange);
            var sorted = new Collider[candidates.Count]; candidates.CopyTo(sorted);
            System.Array.Sort(sorted, (a, b) => (a.ClosestPoint(origin) - origin).sqrMagnitude.CompareTo((b.ClosestPoint(origin) - origin).sqrMagnitude));
            return sorted;
        }

        internal static bool ResonanceLineOfSight(Transform owner, Vector3 origin, Collider target)
        {
            var minion = target.GetComponentInParent<CreepMinionRuntime>();
            var boss = target.GetComponentInParent<EchoProtocol.AI.Stalker.Networking.StalkerFusionRuntime>();
            var targetRoot = minion != null ? minion.transform : boss != null ? boss.transform : target.transform;
            Vector3 offset = target.bounds.center - origin;
            foreach (var hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!IgnoreResonanceCollider(owner, hit.collider) && !IsDespawnedMinionCollider(hit.collider)
                    && !hit.collider.transform.IsChildOf(targetRoot)) return false;
            return true;
        }

        private static bool IgnoreResonanceCollider(Transform owner, Collider collider) => collider == null
            || collider.transform.IsChildOf(owner)
            || ((SceneManager.GetActiveScene().name == LobbyMapCatalog.ExplorationScene || AlienHuntMission.Instance)
                && collider.gameObject.layer == 4);
        private static bool IsDespawnedMinionCollider(Collider collider)
        {
            var minion = collider.GetComponentInParent<CreepMinionRuntime>();
            return minion != null && (minion.Object == null || !minion.Object.IsValid);
        }
        private void ApplyResonanceVisuals()
        {
            bool active=Object!=null && Object.IsValid && ResonanceActive;
            if(active && !_resonanceWasVisible)_resonanceStartedAt=Time.time;
            _resonanceWasVisible=active;
            float pulse=1f+.12f*Mathf.Sin(Time.time*13f);
            float ignition=1f+.45f*Mathf.Clamp01(1f-(Time.time-_resonanceStartedAt)/.2f);
            float lowEnergy=active && ResonanceEnergy<=15f ? .55f+.45f*Mathf.Abs(Mathf.Sin(Time.time*30f)):1f;
            _flashlight.color=active ? new Color(.55f,.12f,1f):new Color(1f,.96f,.88f);
            // Spot lights fade well before their range limit. Keep illumination
            // separate from the gameplay query so mid-range targets
            // remain readable without increasing disruption reach.
            _flashlight.range=active ? ResonanceVisualRange:_beamRange;
            _flashlight.spotAngle=active ? 38f:_beamSpotAngle;
            _flashlight.innerSpotAngle=active ? 22f:Mathf.Min(_beamInnerSpotAngle,_beamSpotAngle);
            float distance=active ? Vector3.Distance(_flashlight.transform.position,ResonanceEndpoint):0f;
            float distanceGain=active ? Mathf.Clamp(distance*distance/16f,1f,4f):1f;
            // Reduce immediately near a wall; increase smoothly when aiming farther.
            _resonanceDistanceGain=Mathf.Min(distanceGain,Mathf.MoveTowards(_resonanceDistanceGain,distanceGain,Time.deltaTime*6f));
            _flashlight.intensity=_beamIntensity*(active ? 2.4f*_resonanceDistanceGain*pulse*ignition*lowEnergy:1f);
            // UV is communicated by the purple spot on surfaces, without a laser line.
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
