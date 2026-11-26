using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using EchoProtocol.AI.Listener.Noise;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Salvage
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider)), DefaultExecutionOrder(150)]
    public sealed class SalvageScrap : NetworkInteractable, ICoreScanCandidate
    {
        [Networked] public int Index { get; set; }
        [Networked] public int Serial { get; set; }
        [Networked] public PlayerRef LastHandler { get; set; }
        [Networked] public NetworkBool PhysicsHandled { get; set; }
        private bool _showedGrip;
        private SalvageValuePopup _valuePopup;
        private SalvageGripVfx _gripVfx;
        private Rigidbody _body;
        private Collider _collider;
        private GameObject _visual;
        private EchoProtocol.Visuals.ObjectiveGlowHighlight _highlight;
        private Vector3 _worldVisualScale, _worldVisualPosition;
        private GameObject _heldVisual;
        private Transform _heldModel, _heldOwner, _fuelHand;
        private NetworkTeamToolHeldView _heldToolView;
        private Renderer[] _heldRenderers;
        private Vector3 _heldSourceScale, _heldChildScale, _fuelLocalPoint, _fuelGrip;
        private Quaternion _fuelLocalRotation;
        private int _heldToolId;
        private float _pocketGripToCenter;
        private Collider[] _ignoredPlayerColliders;
        private int _physicsOwner=-1;
        public Bounds WorldBounds => _collider!=null&&_collider.enabled?_collider.bounds:new Bounds(transform.position,Vector3.one*.1f);
        public Vector3 BodyVelocity=>_body!=null&&!_body.isKinematic?_body.linearVelocity:Vector3.zero;
        private float _restingContactTime=-100;
        private bool _storedSettled;
        public bool ReadyForDelivery=>_body!=null&&!_body.isKinematic&&(_body.IsSleeping()||Time.time-_restingContactTime<.2f&&_body.linearVelocity.sqrMagnitude<1);
        private string SourceVisual => TryCargo(out var c) ? SalvageLedger.Types[c.Definition].Visual : "";
        public bool UsesFuelCellGrip => SourceVisual == "FuelCell" || TryCargo(out var cargo)&&cargo.Definition>=12&&SalvageLedger.Types[cargo.Definition].Pocketable;
        public HuntKind? LegacyHuntKind => SourceVisual switch
        { "AlienSpecimen" => HuntKind.Alien, "OrganicSample" => HuntKind.Meat, "FuelCanister" => HuntKind.Jerrycan, _ => null };
        private SalvageCargoState _lastState = SalvageCargoState.Empty;
        private int _definition = -1;
        private float _nextPose, _nextImpact, _nextNoise, _spawnGrace;
        public int TargetId => Serial;
        public Vector3 WorldPosition => transform.position;
        public bool IsAvailableInWorld => TryCargo(out var c) && c.Definition!=SalvageLedger.CartDefinition && (c.State == SalvageCargoState.World||c.State==SalvageCargoState.Stored);
        public override string InteractionPrompt => TryCargo(out var c)
            ? $"{SalvageLedger.Types[c.Definition].Name} • {c.Value} RV • {SalvageLedger.Types[c.Definition].Weight:0.#} kg • {(SalvageLedger.Types[c.Definition].Pocketable?"Nhặt / cất túi":"Kéo / cầm") }"
            : "Scrap";
        public override bool EmitsRuntimeInteractionNoise => false;

        public override void Spawned()
        {
            _body = GetComponent<Rigidbody>(); _collider = GetComponent<Collider>(); _spawnGrace = Time.time + 2;
            _nextNoise = Time.time + 8; Register();
        }
        private void Register()
        {
            var run = SalvageRunState.Instance;
            if (run != null && run.Object != null && run.Object.IsValid && run.Runner == Runner) run.WorldItems[Index] = this;
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ClearHeldDisplay();
            var run = SalvageRunState.Instance;
            if (run != null && run.WorldItems.TryGetValue(Index, out var current) && current == this) run.WorldItems.Remove(Index);
        }
        private bool TryCargo(out SalvageCargo cargo)
        {
            cargo = default; var run = SalvageRunState.Instance;
            if (run == null || run.Object == null || !run.Object.IsValid || Index < 0 || Index >= run.Items.Length) return false;
            cargo = run.Cargo(Index); return cargo.Serial == Serial && cargo.Definition >= 0 && cargo.Definition < SalvageLedger.Types.Length;
        }
        protected override InteractionValidationResult ValidateCurrentState(in InteractionContext context)
            => IsAvailableInWorld ? InteractionValidationResult.Accepted : InteractionValidationResult.InvalidTargetState;
        public bool TryPresentationCargo(out SalvageCargo cargo)=>TryCargo(out cargo);
        public Vector3 GripPoint
        {
            get
            {
                var run=SalvageRunState.Instance;
                return TryCargo(out var cargo)&&cargo.State==SalvageCargoState.PhysicsHeld&&cargo.Owner>=0&&cargo.Owner<4
                    ?transform.TransformPoint(run.Crew[cargo.Owner].GripLocalPoint):WorldBounds.center;
            }
        }
        protected override void ExecuteInteraction(in InteractionContext context)
            => SalvageRunState.Instance?.Pickup(context.Player, Index, Serial);

        public override void FixedUpdateNetwork()
        {
            if (!TryCargo(out var cargo)) return;
            Register(); ApplyState(cargo);
            if (!Object.HasStateAuthority || (cargo.State != SalvageCargoState.World&&cargo.State!=SalvageCargoState.PhysicsHeld&&cargo.State!=SalvageCargoState.Stored) || cargo.State==SalvageCargoState.Stored&&_storedSettled) return;
            if (Time.time >= _nextPose)
            { _nextPose = Time.time + .05f; SalvageRunState.Instance.SetCargoPose(Index, transform.position,transform.rotation,_body.linearVelocity); }
            if (transform.position.y < -15)
                SalvageRunState.Instance.LoseWorldCargo(Index, Serial);
        }
        public override void Render()
        {
            if (!TryCargo(out var c)) return;
            ApplyState(c);
            var run = SalvageRunState.Instance;
            if(c.State==SalvageCargoState.PhysicsHeld&&!_showedGrip)
            {
                if(c.Definition!=SalvageLedger.CartDefinition)ShowCargoValue(c.Value,0);
            }
            _showedGrip=c.State==SalvageCargoState.PhysicsHeld;
            if(_showedGrip&&_gripVfx==null)
            {
                var prefab=Resources.Load<SalvageGripVfx>("Salvage/Fx/SalvageGripVfx");
                if(prefab!=null){_gripVfx=Instantiate(prefab);_gripVfx.Bind(this);}
            }
            if (c.State == SalvageCargoState.Held) ApplyHeldPose(c);
            else ClearHeldDisplay();
            if ((c.State == SalvageCargoState.World||c.State==SalvageCargoState.PhysicsHeld||c.State==SalvageCargoState.Stored) && !Object.HasStateAuthority)
            {
                var pose=run.Items[Index];transform.position=Vector3.Lerp(transform.position,pose.Position,20*Time.deltaTime);
                var rotation=pose.Rotation;if(rotation.x*rotation.x+rotation.y*rotation.y+rotation.z*rotation.z+rotation.w*rotation.w>.1f)
                    transform.rotation=Quaternion.Slerp(transform.rotation,rotation,20*Time.deltaTime);
            }
            if (Object.HasStateAuthority && SalvageLedger.Types[c.Definition].Noisy
                && (c.State == SalvageCargoState.Held || c.State == SalvageCargoState.World || c.State==SalvageCargoState.PhysicsHeld) && Time.time >= _nextNoise)
            {
                _nextNoise = Time.time + 8;
                var actor = c.State == SalvageCargoState.Held || c.State==SalvageCargoState.PhysicsHeld ? run.Crew[c.Owner].Actor : Runner.LocalPlayer;
                run.Noise(actor, RuntimeNoiseType.NOISE_MAKER, transform.position, "resonator:" + Serial);
                RPC_Resonance(transform.position);
            }
        }
        private void LateUpdate()
        {
            // The shared arm IK runs at order 50. Follow its corrected grip in this frame.
            if (Object != null && Object.IsValid && TryCargo(out var c) && c.State == SalvageCargoState.Held)
                ApplyHeldPose(c);
        }
        public void AttachFuelGrip(Transform hand, Vector3 point, Quaternion rotation)
        {
            _fuelHand = hand; _fuelLocalPoint = hand.InverseTransformPoint(point);
            _fuelLocalRotation = Quaternion.Inverse(hand.rotation) * rotation;
        }
        public Vector3 ResolvePocketHandle(Vector3 fallback)
        {
            if(!TryCargo(out var cargo)||cargo.Definition<12||!SalvageLedger.Types[cargo.Definition].Pocketable)return fallback;
            var run=SalvageRunState.Instance;var owner=run.Player(cargo.Owner);
            if(owner==null||!owner.HasInputAuthority||run.Site.GameplayCamera==null)return fallback;
            var camera=run.Site.GameplayCamera.GetComponent<Camera>();
            return camera!=null?camera.ViewportToWorldPoint(new Vector3(.78f,.2f,.45f))-Vector3.up*_pocketGripToCenter:fallback;
        }
        private void OnDisable() { ClearHeldDisplay();IgnoreOwner(-1);if(_gripVfx!=null)Destroy(_gripVfx.gameObject); }
        private void ClearHeldDisplay()
        {
            if (_heldVisual != null) Destroy(_heldVisual);
            _heldVisual = null; _heldModel = null; _heldOwner = null; _fuelHand = null; _heldToolView = null;
        }
        private void ApplyHeldPose(SalvageCargo cargo)
        {
            var run = SalvageRunState.Instance; var owner = run.Player(cargo.Owner);
            if (owner == null || run.PhysicsHeldIndex(cargo.Owner)>=0 || run.HeldIndex(cargo.Owner, run.Crew[cargo.Owner].Selected) != Index)
            { ClearHeldDisplay(); return; }
            var anchor = PlayerHeldItemAnchor.ResolveRightHandAnchor(owner.gameObject);
            if (_heldVisual == null || _heldOwner != owner.transform)
            {
                ClearHeldDisplay(); _heldOwner = owner.transform;
                _heldToolId = SourceVisual == "SurveyScanner" ? 1 : SourceVisual == "Resonator" ? 6 : 0;
                if (_heldToolId != 0)
                {
                    _heldToolView = owner.GetComponentInChildren<NetworkTeamToolHeldView>(true);
                    if (_heldToolView != null) _heldVisual = _heldToolView.CreateCargoDisplay(_heldToolId, anchor);
                }
                else
                {
                    var source = Resources.Load<GameObject>("Salvage/Held/" + SourceVisual);
                    if (source == null && cargo.Definition < run.Site.ScrapVisuals.Length) source = run.Site.ScrapVisuals[cargo.Definition];
                    if (source == null) return;
                    _heldVisual = Instantiate(source);
                    if(cargo.Definition>=12)
                    {
                        var rs=_heldVisual.GetComponentsInChildren<Renderer>();
                        if(rs.Length>0){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);_heldVisual.transform.localScale*=Mathf.Min(1,.3f/Mathf.Max(.01f,Mathf.Max(b.size.x,b.size.y,b.size.z)));}
                    }
                    _heldModel = SourceVisual!="FuelCell" ? _heldVisual.transform
                        : _heldVisual.transform.childCount > 0 ? _heldVisual.transform.GetChild(0) : _heldVisual.transform;
                    _heldSourceScale = _heldModel.localScale;
                    _heldChildScale = _heldModel.childCount > 0 ? _heldModel.GetChild(0).localScale : Vector3.one;
                    foreach (var part in _heldModel.GetComponentsInChildren<Transform>(true))
                        if (part.name == "RightGrip") _fuelGrip = _heldModel.InverseTransformPoint(part.position);
                    if(cargo.Definition>=12)
                    {
                        var rs=_heldModel.GetComponentsInChildren<Renderer>();
                        if(rs.Length>0){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);_pocketGripToCenter=b.extents.y-.02f;_fuelGrip=_heldModel.InverseTransformPoint(new Vector3(b.center.x,b.min.y+.02f,b.center.z));}
                    }
                }
                if (_heldVisual == null) return;
                _heldVisual.name = "Held_Salvage_" + SourceVisual;
                _heldRenderers = _heldVisual.GetComponentsInChildren<Renderer>(true);
                foreach (var outline in _heldVisual.GetComponentsInChildren<QuickOutline.Outline>(true)) outline.enabled = false;
                if (LegacyHuntKind == HuntKind.Alien)
                    foreach (var animator in _heldVisual.GetComponentsInChildren<Animator>(true))
                        if (animator.runtimeAnimatorController != null) animator.CrossFadeInFixedTime("Stunned", .15f);
            }
            if (_heldToolId != 0)
            {
                _heldToolView.ApplyCargoDisplayPose(_heldVisual.transform, _heldToolId);
                transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            }
            else if (LegacyHuntKind is HuntKind kind)
            {
                AlienHuntMission.ResolveHeldPose(owner.transform, kind, out var position, out var rotation, true);
                _heldModel.SetPositionAndRotation(position, rotation);
                var character = owner.GetComponentInChildren<PlayerCharacterPresenter>();
                if (_heldModel.childCount > 0) AlienHuntActor.ApplyCarriedVisualPose(_heldModel.GetChild(0), kind,
                    character != null && character.IsMercenary, character != null && character.UsesHumanoidGrip, _heldChildScale);
                if (kind == HuntKind.Meat) foreach (var renderer in _heldRenderers) renderer.renderingLayerMask = AlienHuntActor.HeldMeatRenderingLayer;
                transform.SetPositionAndRotation(position, rotation);
            }
            else if (UsesFuelCellGrip)
            {
                var rotation = _fuelHand != null ? _fuelHand.rotation * _fuelLocalRotation : owner.transform.rotation * Quaternion.Euler(0,90,0);
                if(SourceVisual=="FramedPainting"||SourceVisual=="AntiqueClock")rotation*=Quaternion.Euler(0,90,0);
                var point = _fuelHand != null ? _fuelHand.TransformPoint(_fuelLocalPoint) : anchor.position;
                EchoProtocol.MatchFlow.Zone3FuelCell.ApplySingleHandGripPose(_heldModel, _heldSourceScale, _fuelGrip, point, rotation);
                foreach (var renderer in _heldRenderers) HeldVisualShadowOverride.SetActive(renderer, true);
                transform.SetPositionAndRotation(_heldModel.position, _heldModel.rotation);
            }
            else
            {
                // New pocket props use the existing hand anchor and a low, side-on grip.
                var position=anchor.position+owner.transform.right*.08f-owner.transform.up*.12f;
                _heldModel.SetPositionAndRotation(position,anchor.rotation*Quaternion.Euler(0,90,0));
                transform.SetPositionAndRotation(position,_heldModel.rotation);
            }
        }
        private void ApplyState(SalvageCargo c)
        {
            if (_body == null) return;
            if (_definition != c.Definition)
            {
                if (_visual != null) Destroy(_visual);
                var site = SalvageRunState.Instance.Site;
                if(c.Definition==SalvageLedger.CartDefinition)
                {
                    _visual=Instantiate(Resources.Load<GameObject>("Salvage/World/TransportCart"),transform);
                    ((BoxCollider)_collider).center=new Vector3(0,.18f,0);
                    ((BoxCollider)_collider).size=new Vector3(1.1f,.36f,1.5f);
                    _body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
                }
                if (c.Definition!=SalvageLedger.CartDefinition && site != null && site.ScrapVisuals != null && c.Definition < site.ScrapVisuals.Length && site.ScrapVisuals[c.Definition] != null)
                {
                    _visual = Instantiate(site.ScrapVisuals[c.Definition], transform);
                    _visual.name = "CargoVisual";
                    var renderers = _visual.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        var center=Vector3.up*(bounds.extents.y+.02f);
                        _visual.transform.position += transform.position + center - bounds.center;
                        if(_collider is BoxCollider box){box.center=center;box.size=bounds.size;}
                    }
                }
                _definition = c.Definition;
                if (_visual != null)
                {
                    _worldVisualScale = _visual.transform.localScale;
                    _worldVisualPosition = _visual.transform.localPosition;
                    _highlight = _visual.AddComponent<EchoProtocol.Visuals.ObjectiveGlowHighlight>();
                }
            }
            bool world = c.State == SalvageCargoState.World||c.State==SalvageCargoState.PhysicsHeld||c.State==SalvageCargoState.Stored;
            if (_highlight != null)
            {
                _highlight.OutlineMode = QuickOutline.Outline.Mode.OutlineVisible;
                _highlight.SetHighlightActive(world);
            }
            if (world && _visual != null)
            {
                _visual.transform.localScale = _worldVisualScale;
                _visual.transform.localPosition = _worldVisualPosition;
            }
            _collider.enabled = world;
            if(c.State!=SalvageCargoState.Stored)_storedSettled=false;
            else if(ReadyForDelivery)_storedSettled=true;
            _body.isKinematic = !world || c.State==SalvageCargoState.Stored&&_storedSettled || !Object.HasStateAuthority;
            _body.mass=Mathf.Clamp(SalvageLedger.Types[c.Definition].Weight,.5f,40);
            IgnoreOwner(c.State==SalvageCargoState.PhysicsHeld?c.Owner:-1);
            if (_lastState != c.State)
            {
                if (world)
                {
                    transform.position = SalvageRunState.Instance.Items[Index].Position;
                    var rotation=SalvageRunState.Instance.Items[Index].Rotation;
                    if(rotation.x*rotation.x+rotation.y*rotation.y+rotation.z*rotation.z+rotation.w*rotation.w>.1f)transform.rotation=rotation;
                    if (Object.HasStateAuthority&&!_body.isKinematic && (_lastState==SalvageCargoState.Held||_lastState==SalvageCargoState.Empty))
                    { _body.linearVelocity = Vector3.zero; _body.angularVelocity = Vector3.zero; _body.WakeUp(); }
                    _spawnGrace = Time.time + .15f;
                }
                _lastState = c.State;
            }
            if (_visual != null) _visual.SetActive(world);
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority || Time.time < _spawnGrace || Time.time < _nextImpact
                || !TryCargo(out var c) || (c.State != SalvageCargoState.World&&c.State!=SalvageCargoState.PhysicsHeld) || collision.collider.GetComponentInParent<LobbyPlayerState>() != null) return;
            float speed=0;
            foreach(var point in collision.contacts)speed=Mathf.Max(speed,Mathf.Abs(Vector3.Dot(collision.relativeVelocity,point.normal)));
            if (speed < 1) return;
            _nextImpact = Time.time + .3f;
            var position=collision.contactCount>0?collision.GetContact(0).point:transform.position;
            if(!SalvageRunState.Instance.Impact(Index, Serial, speed, position,PhysicsHandled))
                SalvageRunState.Instance.Contact(Index, Serial, position);
        }
        private void OnCollisionStay(Collision collision)
        {
            if(collision.collider.GetComponentInParent<LobbyPlayerState>()!=null)return;
            foreach(var contact in collision.contacts)if(contact.normal.y>.4f){_restingContactTime=Time.time;break;}
        }
        public void ShowCargoValue(int value,int loss)
        {
            if(loss>0&&_gripVfx!=null)_gripVfx.Impact();
            if(_valuePopup!=null)_valuePopup.Refresh(value,loss);
            else _valuePopup=SalvageValuePopup.Show(WorldBounds.center+Vector3.up*(WorldBounds.extents.y+.15f),value,loss,transform,Serial);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Resonance(Vector3 position) => SalvagePresentation.Cue(8, position, SalvageCargoMaterial.Metal);
        private void IgnoreOwner(int seat)
        {
            if(seat==_physicsOwner)return;
            if(_ignoredPlayerColliders!=null&&_collider!=null)
                foreach(var c in _ignoredPlayerColliders)if(c!=null)Physics.IgnoreCollision(_collider,c,false);
            _physicsOwner=seat;_ignoredPlayerColliders=null;
            var run=SalvageRunState.Instance;var owner=run!=null&&seat>=0?run.Player(seat):null;
            if(owner==null||_collider==null)return;
            _ignoredPlayerColliders=owner.GetComponentsInChildren<Collider>(true);
            foreach(var c in _ignoredPlayerColliders)if(c!=null)Physics.IgnoreCollision(_collider,c,true);
        }
        private void FixedUpdate()
        {
            if(Object==null||!Object.IsValid||!Object.HasStateAuthority||_body==null||_body.isKinematic
                ||!TryCargo(out var c)||c.State!=SalvageCargoState.PhysicsHeld)return;
            var run=SalvageRunState.Instance;var player=run.Player(c.Owner);
            var life=player!=null?player.GetComponent<NetworkPlayerLifeState>():null;
            if(player==null||life==null||!life.CanInitiateAction||player.GetComponent<LobbyPlayerState>().Disconnected
                ||player.GetComponent<NetworkPlayerMovement>().CurrentHideSpotId!=0)
            {run.ReleaseGrip(c.Owner);return;}
            var look=SalvageRunState.GripLook(player);var eye=SalvageRunState.GripEye(player);
            var crew=run.Crew[c.Owner];var target=eye+look*Vector3.forward*Mathf.Clamp(crew.GripDistance,1.1f,2.8f);
            if(Vector3.Distance(eye,_body.worldCenterOfMass)>4.5f){run.ReleaseGrip(c.Owner);return;}
            // Real dynamic-body spring: walls stop the body. No teleport/parenting while physically held.
            float strength=SalvageRunState.PowerMultiplier(player.gameObject,SalvagePower.Strength);
            Vector3 acceleration=(target-_body.worldCenterOfMass)*45*strength-_body.linearVelocity*(12*Mathf.Sqrt(strength))-Physics.gravity;
            if(c.Definition==SalvageLedger.CartDefinition){acceleration.y=0;acceleration.x*=.5f;acceleration.z*=.5f;}
            _body.AddForce(Vector3.ClampMagnitude(acceleration,55*strength),ForceMode.Acceleration);
            var delta=look*crew.GripRotation*Quaternion.Inverse(_body.rotation);
            delta.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;
            if(float.IsFinite(axis.x))_body.AddTorque(Vector3.ClampMagnitude(axis*(angle*Mathf.Deg2Rad*18)-_body.angularVelocity*7,35),ForceMode.Acceleration);
            _body.linearVelocity=Vector3.ClampMagnitude(_body.linearVelocity,7);_body.angularVelocity=Vector3.ClampMagnitude(_body.angularVelocity,7);
        }
    }
}
