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
        private const uint HeldMeatRenderingLayer = 1u << 1;
        private uint[] _originalRenderingLayerMasks;
        private string _animation;
        private bool _poseReady;
        private QuickOutline.Outline _meatOutline;
        private Transform _meatVisual;
        private Vector3 _meatGroundPosition;
        private Quaternion _meatGroundRotation;
        private Transform _alienVisual;
        private Vector3 _alienGroundPosition;
        private Quaternion _alienGroundRotation;
        private Vector3 _alienGroundScale;
        private Vector3 _alienRigPosition;
        private Quaternion _alienRigRotation;
        private Transform _jerrycanVisual;
        private Vector3 _jerrycanGroundPosition;
        private Quaternion _jerrycanGroundRotation;
        public const float JerrycanVisualScale = 2f;

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
            if (kind == HuntKind.Jerrycan)
            {
                _jerrycanVisual = transform.GetChild(0);
                if (_jerrycanVisual.localScale.x != JerrycanVisualScale)
                {
                    _jerrycanVisual.localScale = Vector3.one * JerrycanVisualScale;
                    _jerrycanVisual.position += Vector3.up * (transform.position.y - _renderers[0].bounds.min.y);
                    var bounds = _renderers[0].bounds;
                    var box = GetComponent<BoxCollider>();
                    box.center = transform.InverseTransformPoint(bounds.center);
                    box.size = bounds.size;
                }
                _jerrycanGroundPosition = _jerrycanVisual.localPosition;
                _jerrycanGroundRotation = _jerrycanVisual.localRotation;
            }
            if (kind == HuntKind.Alien)
            {
                _alienVisual = transform.GetChild(0);
                _alienGroundPosition = _alienVisual.localPosition;
                _alienGroundRotation = _alienVisual.localRotation;
                _alienGroundScale = _alienVisual.localScale;
                _alienRigPosition = animator.transform.localPosition;
                _alienRigRotation = animator.transform.localRotation;
            }
            if (kind == HuntKind.Meat)
            {
                _originalRenderingLayerMasks = new uint[_renderers.Length];

                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i])
                        _originalRenderingLayerMasks[i] =
                            _renderers[i].renderingLayerMask;
                }
            }
            if (kind == HuntKind.Meat)
            {
                _meatVisual = transform.GetChild(0);
                _meatGroundPosition = _meatVisual.localPosition;
                _meatGroundRotation = _meatVisual.localRotation;
                _meatOutline = GetComponent<QuickOutline.Outline>();
                if (!_meatOutline) _meatOutline = gameObject.AddComponent<QuickOutline.Outline>();
                _meatOutline.OutlineMode = QuickOutline.Outline.Mode.OutlineVisible;
                _meatOutline.OutlineColor = new Color(1f, .7f, .25f, 1f);
                _meatOutline.OutlineWidth = 3.5f;
                _meatOutline.UpdateMaterialProperties();
            }
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
            return kind == HuntKind.Alien ? (state.Stage == (int)AlienStage.Stunned || state.Stage == (int)AlienStage.Eating)
                : state.Stage == 0 || (kind == HuntKind.Meat && state.Stage == 2 && !mission.IsBaitClaimed(index));
        }

        public void Interact(GameObject interactor)
        { if (CanInteract(interactor)) AlienHuntMission.Instance.Request(index, -1, interactor); }

        public void Present(HuntEntityState state, bool authoritative, float delta)
        {
            bool visible = kind == HuntKind.Alien ? state.Stage != (int)AlienStage.Burned : state.Stage != 3;
            bool carried = kind == HuntKind.Alien ? state.Stage == (int)AlienStage.Carried : state.Stage == 1;
            if (_alienVisual)
            {
                _alienVisual.localPosition = carried ? new Vector3(-.037f, .145f, .244f) : _alienGroundPosition;
                _alienVisual.localRotation = carried ? Quaternion.identity : _alienGroundRotation;
                _alienVisual.localScale = carried ? Vector3.one * .6f : _alienGroundScale;
                bool onPan = state.Stage == (int)AlienStage.OnPan || state.Stage == (int)AlienStage.Burning;
                animator.transform.localPosition = onPan ? new Vector3(-.172f, 1.687f, .13873f) : _alienRigPosition;
                animator.transform.localRotation = onPan ? Quaternion.Euler(-200f, -90f, 0f) : _alienRigRotation;
            }
            if (_meatVisual)
            {
                _meatVisual.localPosition = carried ? new Vector3(-.111f, .588f, .12f) : _meatGroundPosition;
                _meatVisual.localRotation = carried ? Quaternion.Euler(10.643f, -141.51f, -117.218f) : _meatGroundRotation;
            }
            if (_jerrycanVisual)
            {
                _jerrycanVisual.localPosition = carried ? new Vector3(.0667f, .142f, .7438f) : _jerrycanGroundPosition;
                _jerrycanVisual.localRotation = carried ? Quaternion.Euler(64.147f, 169.87f, -15.506f) : _jerrycanGroundRotation;
            }
            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (!renderer) continue;

                renderer.enabled = visible;

                if (kind == HuntKind.Meat)
                {
                    uint mask = carried
                        ? HeldMeatRenderingLayer
                        : _originalRenderingLayerMasks[i];

                    if (renderer.renderingLayerMask != mask)
                        renderer.renderingLayerMask = mask;
                }
            }
            foreach (var c in _colliders) if (c) c.enabled = visible && !carried;
            if (_meatOutline)
            {
                var viewer = Camera.main;
                _meatOutline.enabled = visible && !carried && viewer
                    && (viewer.transform.position - transform.position).sqrMagnitude <= 16f * 16f;
            }
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
                stage == AlienStage.Wandering && state.Fleeing && moving ? "Run" :
                stage == AlienStage.Wandering && !moving ? "Idle" :
                stage == AlienStage.Wandering ? "Walk" : "Idle";
            if (_animation == animation) return;
            _animation = animation;
            animator.CrossFadeInFixedTime(animation, .15f);
        }

    }
}
