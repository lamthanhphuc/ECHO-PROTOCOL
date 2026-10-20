using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
namespace EchoProtocol.Pets {
// Detached cosmetic visual. It has no NetworkObject, collider, audio or gameplay target.
public sealed class PetCompanionController : MonoBehaviour {
 private const float VisualScaleMultiplier = 1.3225f;
 // Terror Bringer's broad imported bounds shrink its body compared with Soul Eater.
 private static float GetVisualScaleMultiplier(int petId) => VisualScaleMultiplier * (petId == 3 ? 1.625f : 1f);
 private LobbyPlayerState _owner;
 private NetworkPlayerMovement _movement;
 private NetworkPlayerLifeState _life;
 private CharacterController _ownerController;
 private PetCatalog _catalog;
 private GameObject _visual;
 private Animator _animator;
 private int _id = -1, _authorityId = -1;
 private int _skinId = -1;
 private PetMotion _motion = PetMotion.Hidden;
 private NavMeshPath _path;
 private Vector3 _goal, _ground;
 private float _nextPath, _phaseEnd, _nextFlight, _idleTime, _height;
 private bool _initialized;
 private float _followSpeed;
 private Vector3 _previousOwnerPosition;
 private float _stuckTime;
 private float _nextRetry;
 private float _phaseStart, _phaseStartHeight;
 private float _groundingOffset, _groundingVelocity;
 private float _takeOffGroundingOffset;
 private float _restContactOffset, _lastRenderTime;
 private bool _hasRestContactOffset;
 private int _flightLoops, _flightCycle;
 private bool _renderInitialized, _isFollowing;
 private Vector3 _renderPositionVelocity;
 private PetMotion _renderMotion=PetMotion.Hidden;
 private void Awake() {
  _path=new NavMeshPath();
  _owner=GetComponent<LobbyPlayerState>(); _movement=GetComponent<NetworkPlayerMovement>();
  _life=GetComponent<NetworkPlayerLifeState>(); _ownerController=GetComponent<CharacterController>();
  _catalog=Resources.Load<PetCatalog>("Pets/PetCatalog");
 }
 private bool Visible => _owner.PetId>0 && !_owner.Disconnected &&
  (_movement==null || !_movement.IsHidden) && (_life==null || _life.Status==NetworkPlayerLifeStatus.Alive || _life.Status==NetworkPlayerLifeStatus.Downed);
 public void TickAuthority(float dt) {
  if (_catalog==null) _catalog=Resources.Load<PetCatalog>("Pets/PetCatalog");
  var entry=_catalog!=null ? _catalog.Get(_owner.PetId) : null;
  if(entry==null || !Visible) { _initialized=false; _height=0; _owner.SetPetPose(transform.position,transform.rotation,PetMotion.Hidden); return; }
  if(_authorityId!=_owner.PetId){_authorityId=_owner.PetId;_initialized=false;}
  var time=(float)_owner.Runner.SimulationTime;
  bool ownerRunning=_movement!=null && _movement.IsAnimationSprinting;
  bool inLobby=SceneManager.GetActiveScene().name=="Lobby";
  // Use the exact midpoint between neighboring lobby lineup positions.
  // In gameplay, keep the pet beside the right leg.
  var target=inLobby
   ? transform.position+(LobbyLineupLayout.Position(1)-LobbyLineupLayout.Position(0))*0.5f
   : transform.position+transform.right*1.6f+transform.forward*.35f;
  bool ownerTeleported=_initialized && Vector3.Distance(_previousOwnerPosition,transform.position)>5f;
  _previousOwnerPosition=transform.position;
  if (!_initialized || ownerTeleported || _stuckTime>=8f) {
   if(!TryResolveGroundPosition(target,inLobby,out _ground))
    _ground=target;
   _initialized=true; _followSpeed=0; _stuckTime=0; _nextPath=0;_height=0;_motion=PetMotion.Idle; _nextFlight=time+FlightDelay();
  }
  if(time>=_nextPath || ownerRunning && Vector3.Distance(_ground,_goal)<0.15f) {
   _nextPath=time+(ownerRunning ? 0.1f : 0.3f); _goal=_ground;
   if(inLobby) {
    if(TryResolveGroundPosition(target,true,out var lobbyFloor)) _goal=lobbyFloor;
   }
   else if(NavMesh.SamplePosition(target,out var navGoal,2f,NavMesh.AllAreas) && NavMesh.SamplePosition(_ground,out var navStart,1f,NavMesh.AllAreas)
    && NavMesh.CalculatePath(navStart.position,navGoal.position,NavMesh.AllAreas,_path) && _path.status==NavMeshPathStatus.PathComplete) {
    foreach(var corner in _path.corners) if(Vector3.Distance(corner,_ground)>0.2f){_goal=corner;break;}
   } else if(!GameplayEnvironmentPhysics.Linecast(_ground+Vector3.up*0.2f,target+Vector3.up*0.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
    && GameplayEnvironmentPhysics.Raycast(target+Vector3.up,Vector3.down,out var floor,2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) _goal=floor.point;
  }
  if(inLobby) _ground=_goal;
  float distance=Vector3.Distance(_ground,target);
  bool moving=Vector3.Distance(_ground,_goal)>(_isFollowing ? 0.1f : 0.18f) && distance>(_isFollowing ? 0.4f : 0.8f);
  _isFollowing=moving;
  // Ease into following, and only catch up gradually when the owner is far away.
  bool lifting=_motion==PetMotion.TakeOff || _motion==PetMotion.Land;
  float desiredSpeed=moving ? Mathf.Lerp(1.2f,3.5f,Mathf.InverseLerp(2f,6f,distance)) : 0f;
  if(moving && ownerRunning) desiredSpeed=Mathf.Lerp(4.5f,8.5f,Mathf.InverseLerp(1f,6f,distance));
  if(lifting) {
   float progress=Mathf.InverseLerp(_phaseStart,_phaseEnd,time);
   // Slow in the middle of the transition, with no speed step at either end.
   desiredSpeed*=1f-0.65f*Mathf.Sin(progress*Mathf.PI);
  }
  _followSpeed=Mathf.MoveTowards(_followSpeed,desiredSpeed,dt*(desiredSpeed>_followSpeed ? (ownerRunning ? 6f : 1.8f) : (ownerRunning || _followSpeed>3.5f ? 10f : 3f)));
  bool airborne=_motion==PetMotion.TakeOff||_motion==PetMotion.FlyIdle||_motion==PetMotion.FlyForward||_motion==PetMotion.FlyGlide||_motion==PetMotion.Land;
  if(inLobby) {
   // Keep flight and jumps gameplay-only, while retaining ground idle animations.
   _height=0;
   _idleTime=moving ? 0 : _idleTime+dt;
   _motion=moving ? PetMotion.Walk : (_idleTime>10 ? PetMotion.Sleep : (_owner.PetId==1 && _idleTime>4 ? PetMotion.IdleAlt : PetMotion.Idle));
  } else if(_motion==PetMotion.Jump) {
   float progress=1f-(_phaseEnd-time)/Mathf.Max(0.1f,entry.jumpSeconds);
   _height=Mathf.Sin(Mathf.Clamp01(progress)*Mathf.PI)*0.18f;
   if(time>=_phaseEnd){_height=0;_motion=moving ? PetMotion.Walk : PetMotion.Idle;_nextFlight=time+FlightDelay();}
  } else if(!airborne) {
   _idleTime=moving ? 0 : _idleTime+dt;
   _motion=moving ? (ownerRunning ? PetMotion.Run : PetMotion.Walk) : (_idleTime>10 ? PetMotion.Sleep : (_owner.PetId==1 && _idleTime>4 ? PetMotion.IdleAlt : PetMotion.Idle));
   if(!entry.canFly && !moving && time>=_nextFlight && ClearFlight()){_motion=PetMotion.Jump;_phaseEnd=time+entry.jumpSeconds;}
   if(entry.canFly && time>=_nextFlight && ClearFlight()) { _motion=PetMotion.TakeOff;_phaseStart=time;_phaseStartHeight=_height;_phaseEnd=time+entry.takeOffSeconds; }
  } else if (_motion==PetMotion.TakeOff) {
   _height=Mathf.Lerp(_phaseStartHeight,0.7f,Mathf.SmoothStep(0,1,(time-_phaseStart)/Mathf.Max(0.1f,entry.takeOffSeconds)));
   if(time>=_phaseEnd){_height=0.7f;_motion=moving ? PetMotion.FlyForward : PetMotion.FlyIdle;_phaseEnd=time+FlightDuration();_flightLoops=0;_flightCycle=0;}
  } else if(_motion==PetMotion.Land) {
   _height=Mathf.Lerp(_phaseStartHeight,0,Mathf.SmoothStep(0,1,(time-_phaseStart)/Mathf.Max(0.1f,entry.landSeconds)));
   if(time>=_phaseEnd){_height=0;_motion=moving ? (ownerRunning ? PetMotion.Run : PetMotion.Walk) : PetMotion.Idle;_nextFlight=time+FlightDelay();}
  } else {
   bool blockedFlight=!ClearFlight();
   // Finish a wingbeat before changing flight clips or starting a scheduled landing.
   float loopSeconds=GetFlightClipLength(entry,_motion);
   float elapsed=Mathf.Max(0,time-_owner.PetAnimationStartTime);
   int cycle=Mathf.FloorToInt(elapsed/loopSeconds);
   bool loopEnd=cycle>_flightCycle;
   _flightCycle=cycle;
   if(loopEnd) {
    _flightLoops++;
    PetMotion next=!moving ? PetMotion.FlyIdle : (_motion==PetMotion.FlyIdle ? PetMotion.FlyForward : (_flightLoops>=3 ? (_motion==PetMotion.FlyForward ? PetMotion.FlyGlide : PetMotion.FlyForward) : _motion));
    if(next!=_motion){_motion=next;_flightLoops=0;_flightCycle=0;}
   }
   if((blockedFlight || time>=_phaseEnd && loopEnd) && SafeLanding()){_motion=PetMotion.Land;_phaseStart=time;_phaseStartHeight=_height;_phaseEnd=time+entry.landSeconds;}
  }
  var previous=_ground;
  if(moving && _motion!=PetMotion.Jump) {
   var next=Vector3.MoveTowards(_ground,_goal,dt*_followSpeed);
   var origin=_ground+Vector3.up*(0.2f+_height);
   var displacement=next-_ground;
   bool blocked=_height>0.1f
    ? GameplayEnvironmentPhysics.SphereCast(origin,0.25f,displacement.normalized,out _,displacement.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
    : GameplayEnvironmentPhysics.Linecast(origin,next+Vector3.up*0.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
   if(!blocked) _ground=next;
  }
  _stuckTime=distance>10f && Vector3.Distance(_ground,previous)<0.001f ? _stuckTime+dt : 0f;
  var direction=Vector3.ProjectOnPlane(_ground-previous,Vector3.up); var rotation=direction.sqrMagnitude>0.00001f ? Quaternion.LookRotation(direction.normalized,Vector3.up) : (Quaternion.Dot(_owner.PetRotation,_owner.PetRotation)>0.1f ? _owner.PetRotation : transform.rotation);
  _owner.SetPetPose(_ground+Vector3.up*_height,rotation,_motion);
 }
 private float FlightDelay() {
  bool canFly=_catalog!=null && _catalog.Get(_owner.PetId)!=null && _catalog.Get(_owner.PetId).canFly;
  return (canFly ? 18f : 15f)+Mathf.Abs((_owner.TeamId*13+(int)_owner.Runner.Tick)%11);
 }
 private float FlightDuration()=>30f+Mathf.Abs((_owner.TeamId*17+(int)_owner.Runner.Tick)%16);
 private static float GetFlightClipLength(PetCatalog.Entry entry,PetMotion motion) {
  string clipName=motion==PetMotion.FlyIdle ? "Fly Float" : (motion==PetMotion.FlyGlide ? "Fly Glide" : "Fly Forward");
  if(entry.controller!=null)foreach(var clip in entry.controller.animationClips)
   if(clip.name==clipName)return Mathf.Max(0.1f,clip.length);
  return 1f;
 }
 private static bool IsOneShot(PetMotion motion)=>motion==PetMotion.TakeOff || motion==PetMotion.Land || motion==PetMotion.Jump;
 private bool ClearFlight()=>!GameplayEnvironmentPhysics.CheckCapsule(_ground+Vector3.up*0.4f,_ground+Vector3.up*1.3f,0.3f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
 private bool SafeLanding()=>GameplayEnvironmentPhysics.Raycast(_ground+Vector3.up*0.3f,Vector3.down,0.6f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
 private void LateUpdate() {
  if(_owner==null || _owner.Object==null || !_owner.Object.IsValid)return;
  if(_catalog==null){if(Time.unscaledTime<_nextRetry)return;_nextRetry=Time.unscaledTime+1;_catalog=Resources.Load<PetCatalog>("Pets/PetCatalog");if(_catalog==null)return;}
  if(_id!=_owner.PetId || (_visual==null && _owner.PetId>0)) {
   if(_visual!=null)Destroy(_visual);_id=_owner.PetId;_skinId=-1;
   var entry=_catalog.Get(_id);
   if(entry!=null && entry.prefab!=null) {
    _renderInitialized=false;_renderMotion=PetMotion.Hidden;_groundingOffset=0;_groundingVelocity=0;
    _visual=Instantiate(entry.prefab);_visual.name="CosmeticPet_"+entry.displayName;
    SetLayerRecursively(_visual,gameObject.layer);
    _visual.transform.localScale=Vector3.one*(entry.scale*GetVisualScaleMultiplier(_id));
    _animator=_visual.GetComponentInChildren<Animator>();
    _animator.runtimeAnimatorController=entry.controller;_animator.applyRootMotion=false;
    // Evaluate once at rest, then advance explicitly on the network render clock.
    _animator.enabled=false;_animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    _animator.Play("Idle",0,0);_animator.Update(0);
    // Mesh bounds include a tail/body underside below the toes. Ground the
    // character by its actual feet so its walking pose does not float.
    _restContactOffset=float.PositiveInfinity;
    foreach(var bone in _visual.GetComponentsInChildren<Transform>(true))
     if(bone.name.IndexOf("Toe",System.StringComparison.OrdinalIgnoreCase)>=0 ||
      bone.name.EndsWith("Foot",System.StringComparison.OrdinalIgnoreCase))
      _restContactOffset=Mathf.Min(_restContactOffset,bone.position.y-_visual.transform.position.y);
    if(float.IsInfinity(_restContactOffset))
     foreach(var renderer in _visual.GetComponentsInChildren<Renderer>(true))
      _restContactOffset=Mathf.Min(_restContactOffset,renderer.bounds.min.y-_visual.transform.position.y);
    _hasRestContactOffset=!float.IsInfinity(_restContactOffset);
    foreach(var c in _visual.GetComponentsInChildren<Collider>())c.enabled=false;
    foreach(var a in _visual.GetComponentsInChildren<AudioSource>())a.enabled=false;
    _visual.transform.SetPositionAndRotation(_owner.PetPosition,_owner.PetRotation);
   }
  }
  if(_visual==null)return;
  ApplySelectedSkin();
  bool interpolated=_owner.GetPetRenderPose(out var posePosition,out var poseRotation,out var motion,out var startTime,out var renderTime);
  bool show=Visible && motion!=PetMotion.Hidden;
  _visual.SetActive(show);if(!show){_renderInitialized=false;return;}
  float renderDelta=_renderInitialized ? Mathf.Max(0,renderTime-_lastRenderTime) : 0;
  _lastRenderTime=renderTime;
  // Animator and replicated height now use the same clock on host and clients.
  _animator.Update(renderDelta);
  if(_renderMotion!=motion) {
   var state=_animator.GetCurrentAnimatorStateInfo(0);
   bool completed=!IsOneShot(_renderMotion) || (!_animator.IsInTransition(0) && state.normalizedTime>=1f);
   if(!_renderInitialized || completed) {
    bool transition=IsOneShot(motion) || IsOneShot(_renderMotion);
    if(motion==PetMotion.TakeOff)_takeOffGroundingOffset=_groundingOffset;
    float offset=Mathf.Max(0,renderTime-startTime);
    if(!_renderInitialized) {
     _animator.Play(motion.ToString(),0,0);_animator.Update(0);
     float length=Mathf.Max(0.1f,_animator.GetCurrentAnimatorStateInfo(0).length);
     _animator.Play(motion.ToString(),0,IsOneShot(motion) ? Mathf.Min(offset/length,0.9999f) : offset/length);
    }
    else _animator.CrossFadeInFixedTime(motion.ToString(),transition ? 0.3f : 0.2f,0,offset);
    _renderMotion=motion;_animator.Update(0);
   }
  }
  var entryForPose=_catalog.Get(_id);
  var position=posePosition+Vector3.up*(entryForPose!=null ? entryForPose.groundOffset*GetVisualScaleMultiplier(_id) : 0f);
  if(!interpolated && _renderInitialized && Vector3.Distance(_visual.transform.position,position)<5f)
   position=Vector3.SmoothDamp(_visual.transform.position-Vector3.up*_groundingOffset,position,ref _renderPositionVelocity,0.08f);
  else _renderPositionVelocity=Vector3.zero;
  bool grounded=motion==PetMotion.Idle || motion==PetMotion.IdleAlt || motion==PetMotion.Walk || motion==PetMotion.Run || motion==PetMotion.Sleep;
  float transitionSeconds=entryForPose==null ? 1f : (motion==PetMotion.Land ? entryForPose.landSeconds : entryForPose.takeOffSeconds);
  float transitionProgress=Mathf.SmoothStep(0,1,(renderTime-startTime)/Mathf.Max(0.1f,transitionSeconds));
  float desiredGrounding=0;
  if(motion==PetMotion.TakeOff)desiredGrounding=_takeOffGroundingOffset*(1f-transitionProgress);
  if((grounded || motion==PetMotion.Land) && _hasRestContactOffset) {
   float floorY=posePosition.y;
   if(GameplayEnvironmentPhysics.Raycast(posePosition+Vector3.up*0.6f,Vector3.down,out var hit,1.6f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))floorY=hit.point.y;
   {
    // Restore floor contact gradually during landing, instead of at its final frame.
    float flightHeight=motion==PetMotion.Land ? 0.7f*(1f-transitionProgress) : 0f;
    desiredGrounding=(floorY+flightHeight-position.y-_restContactOffset)*(motion==PetMotion.Land ? transitionProgress : 1f);
   }
  }
  if(!_renderInitialized){_groundingOffset=desiredGrounding;_groundingVelocity=0;_renderInitialized=true;}
  else _groundingOffset=Mathf.SmoothDamp(_groundingOffset,desiredGrounding,ref _groundingVelocity,0.2f);
  _visual.transform.SetPositionAndRotation(position+Vector3.up*_groundingOffset,poseRotation);
 }
 private bool TryResolveGroundPosition(Vector3 target,bool inLobby,out Vector3 position) {
  if(inLobby && GameplayEnvironmentPhysics.Raycast(target+Vector3.up*1.5f,Vector3.down,out var lobbyFloor,4f,
   Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) { position=lobbyFloor.point;return true; }
  // The flooded lobby backdrop is visual-only and has no collider. Use the
  // lineup player's feet instead of its root height when the SciFi floor is hidden.
  if(inLobby && _ownerController!=null) {
   position=target;
   position.y=transform.TransformPoint(_ownerController.center).y-
    _ownerController.height*Mathf.Abs(transform.lossyScale.y)*0.5f;
   return true;
  }
  if(!inLobby && NavMesh.SamplePosition(target,out var hit,2f,NavMesh.AllAreas)) { position=hit.position;return true; }
  // Some Map2 spawn markers sit beside a narrow NavMesh edge. Fall back to the
  // owner's reachable polygon before using the physical floor so the pet stays visible.
  if(!inLobby && NavMesh.SamplePosition(transform.position,out var ownerNav,4f,NavMesh.AllAreas)) { position=ownerNav.position;return true; }
  if(GameplayEnvironmentPhysics.Raycast(target+Vector3.up*1.5f,Vector3.down,out var floor,4f,
   Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) { position=floor.point;return true; }
  position=default;return false;
 }
 private static void SetLayerRecursively(GameObject root,int layer) {
  foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;
 }
 private void ApplySelectedSkin() {
  if(_visual==null || _owner==null || _id<=0)return;
  int skinId=Mathf.Clamp(_owner.PetSkinId,0,PetSkinLibrary.SkinCount-1);
  if(_skinId==skinId)return;
  var material=PetSkinLibrary.Load(_id,skinId);
  if(material==null)return;
  foreach(var renderer in _visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
   var materials=renderer.sharedMaterials;
   if(materials==null || materials.Length==0)continue;
   for(int i=0;i<materials.Length;i++)materials[i]=material;
   renderer.sharedMaterials=materials;
  }
  _skinId=skinId;
 }
 private void OnEnable(){SceneManager.activeSceneChanged+=OnSceneChanged;}
 private void OnSceneChanged(Scene previous,Scene current){
  _initialized=false;_nextPath=0;_renderInitialized=false;
  // Recreate the detached visual in the new active scene. This also reapplies
  // the gameplay camera layer used by Scene_A and Scene_B.
  if(_visual!=null)Destroy(_visual);
  _visual=null;_id=-1;
 }
 private void OnDisable(){SceneManager.activeSceneChanged-=OnSceneChanged;if(_visual!=null)_visual.SetActive(false);}
 private void OnDestroy(){if(_visual!=null)Destroy(_visual);}
}
}
