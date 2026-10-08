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
 private PetCatalog _catalog;
 private GameObject _visual;
 private Animator _animator;
 private Transform[] _feet = System.Array.Empty<Transform>();
 private int _id = -1, _authorityId = -1;
 private PetMotion _motion = PetMotion.Hidden;
 private readonly NavMeshPath _path = new NavMeshPath();
 private Vector3 _goal, _ground;
 private float _nextPath, _phaseEnd, _nextFlight, _idleTime, _height;
 private bool _initialized;
 private float _followSpeed;
 private float _nextRetry;
 private float _phaseStart, _phaseStartHeight;
 private float _groundingOffset, _groundingVelocity;
 private bool _renderInitialized, _isFollowing;
 private Vector3 _renderPositionVelocity;
 private PetMotion _renderMotion=PetMotion.Hidden;
 private void Awake() {
  _owner=GetComponent<LobbyPlayerState>(); _movement=GetComponent<NetworkPlayerMovement>();
  _life=GetComponent<NetworkPlayerLifeState>(); _catalog=Resources.Load<PetCatalog>("Pets/PetCatalog");
 }
 private bool Visible => _owner.PetId>0 && !_owner.Disconnected &&
  (_movement==null || !_movement.IsHidden) && (_life==null || _life.Status==NetworkPlayerLifeStatus.Alive || _life.Status==NetworkPlayerLifeStatus.Downed);
 public void TickAuthority(float dt) {
  if (_catalog==null) _catalog=Resources.Load<PetCatalog>("Pets/PetCatalog");
  var entry=_catalog!=null ? _catalog.Get(_owner.PetId) : null;
  if(entry==null || !Visible) { _initialized=false; _height=0; _owner.SetPetPose(transform.position,transform.rotation,PetMotion.Hidden); return; }
  if(_authorityId!=_owner.PetId){_authorityId=_owner.PetId;_initialized=false;}
  var time=(float)_owner.Runner.SimulationTime;
  bool inLobby=SceneManager.GetActiveScene().name=="Lobby";
  var target=transform.position+transform.forward*(inLobby ? 1.1f : -1.25f)+transform.right*(inLobby ? 0.55f : 0.85f);
  if (!_initialized || Vector3.Distance(_ground,transform.position)>10f) {
   if(!NavMesh.SamplePosition(target,out var reset,2f,NavMesh.AllAreas)) {
    if(!Physics.Raycast(target+Vector3.up*1.5f,Vector3.down,out var hit,4f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) {
     _owner.SetPetPose(target,transform.rotation,PetMotion.Hidden);return;
    } _ground=hit.point;
   } else _ground=reset.position;
   _initialized=true; _followSpeed=0; _nextPath=0;_height=0;_motion=PetMotion.Idle; _nextFlight=time+FlightDelay();
  }
  if(time>=_nextPath) {
   _nextPath=time+0.3f; _goal=_ground;
   if(NavMesh.SamplePosition(target,out var navGoal,2f,NavMesh.AllAreas) && NavMesh.SamplePosition(_ground,out var navStart,1f,NavMesh.AllAreas)
    && NavMesh.CalculatePath(navStart.position,navGoal.position,NavMesh.AllAreas,_path) && _path.status==NavMeshPathStatus.PathComplete) {
    foreach(var corner in _path.corners) if(Vector3.Distance(corner,_ground)>0.2f){_goal=corner;break;}
   } else if(!Physics.Linecast(_ground+Vector3.up*0.2f,target+Vector3.up*0.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
    && Physics.Raycast(target+Vector3.up,Vector3.down,out var floor,2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) _goal=floor.point;
  }
  float distance=Vector3.Distance(_ground,target);
  bool moving=Vector3.Distance(_ground,_goal)>(_isFollowing ? 0.1f : 0.18f) && distance>(_isFollowing ? 0.4f : 0.8f);
  _isFollowing=moving;
  // Ease into following, and only catch up gradually when the owner is far away.
  bool lifting=_motion==PetMotion.TakeOff || _motion==PetMotion.Land;
  float desiredSpeed=moving ? Mathf.Lerp(1.2f,3.5f,Mathf.InverseLerp(2f,6f,distance)) : 0f;
  if(lifting)desiredSpeed*=0.35f;
  _followSpeed=Mathf.MoveTowards(_followSpeed,desiredSpeed,dt*(desiredSpeed>_followSpeed ? 1.8f : 3f));
  bool airborne=_motion==PetMotion.TakeOff||_motion==PetMotion.FlyIdle||_motion==PetMotion.FlyForward||_motion==PetMotion.FlyGlide||_motion==PetMotion.Land;
  if(_motion==PetMotion.Jump) {
   float progress=1f-(_phaseEnd-time)/Mathf.Max(0.1f,entry.jumpSeconds);
   _height=Mathf.Sin(Mathf.Clamp01(progress)*Mathf.PI)*0.18f;
   if(time>=_phaseEnd){_height=0;_motion=moving ? PetMotion.Walk : PetMotion.Idle;_nextFlight=time+FlightDelay();}
  } else if(!airborne) {
   _idleTime=moving ? 0 : _idleTime+dt;
   _motion=moving ? (_followSpeed>2.2f ? PetMotion.Run : PetMotion.Walk) : (_idleTime>10 ? PetMotion.Sleep : (_owner.PetId==1 && _idleTime>4 ? PetMotion.IdleAlt : PetMotion.Idle));
   if(!entry.canFly && !moving && time>=_nextFlight && ClearFlight()){_motion=PetMotion.Jump;_phaseEnd=time+entry.jumpSeconds;}
   if(entry.canFly && time>=_nextFlight && ClearFlight()) { _motion=PetMotion.TakeOff;_phaseStart=time;_phaseStartHeight=_height;_phaseEnd=time+entry.takeOffSeconds; }
  } else if (_motion==PetMotion.TakeOff) {
   _height=Mathf.Lerp(_phaseStartHeight,0.7f,Mathf.SmoothStep(0,1,(time-_phaseStart)/Mathf.Max(0.1f,entry.takeOffSeconds)));
   if(time>=_phaseEnd){_height=0.7f;_motion=moving ? PetMotion.FlyForward : PetMotion.FlyIdle;_phaseEnd=time+6f+(_owner.TeamId%3)*2f;}
  } else if(_motion==PetMotion.Land) {
   _height=Mathf.Lerp(_phaseStartHeight,0,Mathf.SmoothStep(0,1,(time-_phaseStart)/Mathf.Max(0.1f,entry.landSeconds)));
   if(time>=_phaseEnd){_height=0;_motion=moving ? PetMotion.Walk : PetMotion.Idle;_nextFlight=time+FlightDelay();}
  } else {
   _motion=!moving ? PetMotion.FlyIdle : ((int)(time*0.25f)%2==0 ? PetMotion.FlyForward : PetMotion.FlyGlide);
   if((time>=_phaseEnd || !ClearFlight()) && SafeLanding()){_motion=PetMotion.Land;_phaseStart=time;_phaseStartHeight=_height;_phaseEnd=time+entry.landSeconds;}
  }
  var previous=_ground;
  if(moving && _motion!=PetMotion.Jump) {
   var next=Vector3.MoveTowards(_ground,_goal,dt*_followSpeed);
   var origin=_ground+Vector3.up*(0.2f+_height);
   var displacement=next-_ground;
   bool blocked=_height>0.1f
    ? Physics.SphereCast(origin,0.25f,displacement.normalized,out _,displacement.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
    : Physics.Linecast(origin,next+Vector3.up*0.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
   if(!blocked) _ground=next;
  }
  var direction=Vector3.ProjectOnPlane(_ground-previous,Vector3.up); var rotation=direction.sqrMagnitude>0.00001f ? Quaternion.LookRotation(direction.normalized,Vector3.up) : (Quaternion.Dot(_owner.PetRotation,_owner.PetRotation)>0.1f ? _owner.PetRotation : transform.rotation);
  _owner.SetPetPose(_ground+Vector3.up*_height,rotation,_motion);
 }
 private float FlightDelay()=>15f+Mathf.Abs((_owner.TeamId*13+(int)_owner.Runner.Tick)%11);
 private bool ClearFlight()=>!Physics.CheckCapsule(_ground+Vector3.up*0.4f,_ground+Vector3.up*1.3f,0.3f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
 private bool SafeLanding()=>Physics.Raycast(_ground+Vector3.up*0.3f,Vector3.down,0.6f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
 private void LateUpdate() {
  if(_owner==null || _owner.Object==null || !_owner.Object.IsValid)return;
  if(_catalog==null){if(Time.unscaledTime<_nextRetry)return;_nextRetry=Time.unscaledTime+1;_catalog=Resources.Load<PetCatalog>("Pets/PetCatalog");if(_catalog==null)return;}
  if(_id!=_owner.PetId || (_visual==null && _owner.PetId>0)) {
   if(_visual!=null)Destroy(_visual);_id=_owner.PetId;
   var entry=_catalog.Get(_id);
   if(entry!=null && entry.prefab!=null) {
    _renderInitialized=false;_renderMotion=PetMotion.Hidden;_groundingOffset=0;_groundingVelocity=0;
    _visual=Instantiate(entry.prefab);_visual.name="CosmeticPet_"+entry.displayName;
    _visual.transform.localScale=Vector3.one*(entry.scale*GetVisualScaleMultiplier(_id));
    _animator=_visual.GetComponentInChildren<Animator>();
    var feet=new System.Collections.Generic.List<Transform>();
    foreach(var bone in _visual.GetComponentsInChildren<Transform>())
     if(bone.name.IndexOf("Toe",System.StringComparison.OrdinalIgnoreCase)>=0 || bone.name.EndsWith("Foot",System.StringComparison.OrdinalIgnoreCase))feet.Add(bone);
    _feet=feet.ToArray();
    _animator.runtimeAnimatorController=entry.controller;_animator.applyRootMotion=false;
    foreach(var c in _visual.GetComponentsInChildren<Collider>())c.enabled=false;
    foreach(var a in _visual.GetComponentsInChildren<AudioSource>())a.enabled=false;
    _visual.transform.SetPositionAndRotation(_owner.PetPosition,_owner.PetRotation);
   }
  }
  if(_visual==null)return;
  bool interpolated=_owner.GetPetRenderPose(out var posePosition,out var poseRotation,out var motion,out var startTime,out var renderTime);
  bool show=Visible && motion!=PetMotion.Hidden;
  _visual.SetActive(show);if(!show){_renderInitialized=false;return;}
  var entryForPose=_catalog.Get(_id);
  var position=posePosition+Vector3.up*(entryForPose!=null ? entryForPose.groundOffset*GetVisualScaleMultiplier(_id) : 0f);
  if(!interpolated && _renderInitialized && Vector3.Distance(_visual.transform.position,position)<5f)
   position=Vector3.SmoothDamp(_visual.transform.position-Vector3.up*_groundingOffset,position,ref _renderPositionVelocity,0.08f);
  else _renderPositionVelocity=Vector3.zero;
  bool grounded=motion==PetMotion.Idle || motion==PetMotion.IdleAlt || motion==PetMotion.Walk || motion==PetMotion.Run || motion==PetMotion.Sleep;
  float desiredGrounding=0;
  if(grounded && _feet.Length>0) {
   float floorY=posePosition.y;
   if(Physics.Raycast(posePosition+Vector3.up*0.6f,Vector3.down,out var hit,1.6f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))floorY=hit.point.y;
   float lowest=float.PositiveInfinity;
   foreach(var foot in _feet)if(foot!=null)lowest=Mathf.Min(lowest,foot.position.y-_visual.transform.position.y);
   if(!float.IsInfinity(lowest))desiredGrounding=floorY+0.015f-position.y-lowest;
  }
  if(!_renderInitialized){_groundingOffset=desiredGrounding;_groundingVelocity=0;_renderInitialized=true;}
  else _groundingOffset=Mathf.SmoothDamp(_groundingOffset,desiredGrounding,ref _groundingVelocity,grounded ? 0.12f : 0.35f);
  _visual.transform.SetPositionAndRotation(position+Vector3.up*_groundingOffset,poseRotation);
  if(_renderMotion!=motion) {
   _renderMotion=motion;
   _animator.CrossFadeInFixedTime(motion.ToString(),0.25f,0,Mathf.Max(0,renderTime-startTime));
  }
 }
 private void OnEnable(){SceneManager.activeSceneChanged+=OnSceneChanged;}
 private void OnSceneChanged(Scene previous,Scene current){_initialized=false;_nextPath=0;}
 private void OnDisable(){SceneManager.activeSceneChanged-=OnSceneChanged;if(_visual!=null)_visual.SetActive(false);}
 private void OnDestroy(){if(_visual!=null)Destroy(_visual);}
}
}
