using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.Networking
{
    [DefaultExecutionOrder(200)]
    public sealed class AlienHuntActor : MonoBehaviour, IInteractable
    {
        [SerializeField] private int index;
        [SerializeField] private HuntKind kind;
        [SerializeField] private Animator animator;
        public int Index => index;
        public HuntKind Kind => kind;
        public Vector3 Home { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        private Collider[] _colliders;
        private Renderer[] _renderers;
        private string _animation;
        private bool _poseReady;

        public void Configure(int id, HuntKind type, Animator animation)
        { index = id; kind = type; animator = animation; }

        private void Awake()
        {
            Home = transform.position;
            HomeRotation = transform.rotation;
            Agent = GetComponent<NavMeshAgent>();
            if (Agent) Agent.enabled = false;
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void LateUpdate()
        {
            var m = AlienHuntMission.Instance;
            if (m && m.Ready) Present(m.Read(index), m.Authority, Time.deltaTime);
        }
        public string InteractionPrompt => kind == HuntKind.Alien ? "[" + AlienHuntMission.InteractKey + "] BẮT ALIEN NON" :
            kind == HuntKind.Meat ? "[" + AlienHuntMission.InteractKey + "] NHẶT THỊT · [G] ĐẶT MỒI" : "[" + AlienHuntMission.InteractKey + "] NHẶT CAN NHIÊN LIỆU";

        public bool CanInteract(GameObject interactor)
        {
            var mission = AlienHuntMission.Instance;
            if (!mission || !mission.Ready || mission.Completed || !interactor || mission.LocalHeldIndex >= 0) return false;
            var state = mission.Read(index);
            return kind == HuntKind.Alien ? state.Stage == (int)AlienStage.Stunned
                : state.Stage == 0 || (kind == HuntKind.Meat && state.Stage == 2 && !mission.IsBaitClaimed(index));
        }

        public void Interact(GameObject interactor)
        { if (CanInteract(interactor)) AlienHuntMission.Instance.Request(index, -1, interactor); }

        public void Present(HuntEntityState state, bool authoritative, float delta)
        {
            bool visible = kind == HuntKind.Alien ? state.Stage != (int)AlienStage.Burned : state.Stage != 3;
            bool carried = kind == HuntKind.Alien ? state.Stage == (int)AlienStage.Carried : state.Stage == 1;
            foreach (var r in _renderers) if (r) r.enabled = visible;
            foreach (var c in _colliders) if (c) c.enabled = visible && !carried;
            if (!visible) { if (Agent) Agent.enabled = false; return; }
            Vector3 position = state.Position; Quaternion rotation = state.Rotation;
            if (carried) AlienHuntMission.Instance.HeldPose(state.Carrier, kind, out position, out rotation, true);
            bool agentOwnsPose = authoritative && Agent && Agent.enabled && Agent.isOnNavMesh;
            bool moving = agentOwnsPose ? Agent.velocity.sqrMagnitude > .03f : (position - transform.position).sqrMagnitude > .0002f;
            if (!agentOwnsPose)
            {
                float blend = !_poseReady || carried || authoritative || (position-transform.position).sqrMagnitude > 9f
                    ? 1f : 1f - Mathf.Exp(-15f * delta);
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, blend),
                    Quaternion.Slerp(transform.rotation, rotation, blend));
            }
            _poseReady = true;
            if (!animator) return;
            var stage = (AlienStage)state.Stage;
            string animation = stage == AlienStage.Running ? "Run" : stage == AlienStage.Eating ? "Eating" :
                stage == AlienStage.Stunned || stage == AlienStage.Carried ? "Stunned" :
                stage == AlienStage.Wandering && !moving ? "Idle" :
                stage == AlienStage.Wandering ? "Walk" : "Idle";
            if (_animation == animation) return;
            _animation = animation;
            animator.CrossFadeInFixedTime(animation, .15f);
        }
    }
}
