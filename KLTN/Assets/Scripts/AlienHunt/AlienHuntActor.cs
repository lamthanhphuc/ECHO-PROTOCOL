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
        private AlienHuntAudio _audio;
        private bool _poseReady;
        private QuickOutline.Outline _pickupOutline;
        private Transform _meatVisual;
        private Vector3 _meatGroundPosition;
        private Quaternion _meatGroundRotation;
        private Transform _alienVisual;
        private Vector3 _alienGroundPosition;
        private Quaternion _alienGroundRotation;
        private Vector3 _alienGroundScale;
        private Vector3 _alienRigPosition;
        private Quaternion _alienRigRotation;
        private readonly Transform[] _panBones = new Transform[9];
        private AnimatorCullingMode _normalAnimatorCulling;
        private float _panStruggleWeight;
        private float _panBurnElapsed;
        private Transform _jerrycanVisual;
        private Vector3 _jerrycanGroundPosition;
        private Quaternion _jerrycanGroundRotation;
        public const float JerrycanVisualScale = 2f;

        public void Configure(int id, HuntKind type, Animator animation)
        { index = id; kind = type; animator = animation; }

        public void RelocateNestHome(Vector3 newHome)
        {
            if (kind != HuntKind.Alien || !transform.parent)
                return;

            Vector3 delta = newHome - Home;
            if (delta.sqrMagnitude < .0001f)
            {
                if (!_nestGroundAttempted) GroundNestDecoration();
                return;
            }
            if (Agent && Agent.enabled)
                Agent.enabled = false;
            transform.parent.position += delta;
            Home = newHome;
            GroundNestDecoration();
        }

        private bool _nestGroundAttempted;

        public static bool TryNestGround(Transform nest, Vector3 delta, out float floorY)
        {
            floorY = float.NegativeInfinity;
            var decoration = nest.Find("AlienEgg_Decoration");
            if (decoration == null) return false;
            var renderers = decoration.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            var resident = nest.GetComponentInChildren<AlienHuntActor>(true);
            float maximumSupportY = resident != null
                ? (Application.isPlaying ? resident.Home.y : resident.transform.position.y) + delta.y + .4f
                : nest.position.y + delta.y + .4f;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float lowest = float.PositiveInfinity;
            // Check the footprint, not just the Alien's adjacent NavMesh position.
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    var point = bounds.center + delta + new Vector3(x * bounds.extents.x * .7f, 0, z * bounds.extents.z * .7f);
                    var hits = Physics.RaycastAll(new Vector3(point.x, nest.position.y + delta.y + 4, point.z), Vector3.down, 8,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    bool found = false;
                    foreach (var hit in hits)
                    {
                        if (hit.collider.transform.IsChildOf(nest) || !GameplayEnvironmentPhysics.IsEnvironment(hit.collider)
                            || hit.collider.GetComponentInParent<AlienHuntActor>() != null
                            || hit.collider.GetComponentInParent<EchoProtocol.AI.Minions.CreepMinionRuntime>() != null) continue;
                        if (hit.point.y > maximumSupportY) continue;
                        if (hit.normal.y < .9f) return false;
                        lowest = Mathf.Min(lowest, hit.point.y); floorY = Mathf.Max(floorY, hit.point.y);
                        found = true; break;
                    }
                    if (!found) return false;
                }
            return floorY - lowest <= .12f;
        }

        public bool GroundNestDecoration()
        {
            _nestGroundAttempted = true;
            if (kind != HuntKind.Alien || !transform.parent || !TryNestGround(transform.parent, Vector3.zero, out float floor)) return false;
            var decoration = transform.parent.Find("AlienEgg_Decoration");
            float bottom = float.PositiveInfinity;
            foreach (var renderer in decoration.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            decoration.position += Vector3.up * (floor + .015f - bottom);
            return true;
        }

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
                _audio = GetComponent<AlienHuntAudio>();
                if (!_audio) _audio = gameObject.AddComponent<AlienHuntAudio>();
                _audio.Initialize(animator);
                _alienVisual = transform.GetChild(0);
                _alienGroundPosition = _alienVisual.localPosition;
                _alienGroundRotation = _alienVisual.localRotation;
                _alienGroundScale = _alienVisual.localScale;
                _alienRigPosition = animator.transform.localPosition;
                _alienRigRotation = animator.transform.localRotation;
                _normalAnimatorCulling = animator.cullingMode;
                var rig = animator.transform;
                const string upper = "root/root.x/spine_01.x/spine_02.x/spine_03.x/";
                _panBones[0] = rig.Find("root/root.x/spine_01.x");
                _panBones[1] = rig.Find(upper + "shoulder.l");
                _panBones[2] = rig.Find(upper + "shoulder.l/arm_stretch.l/forearm_stretch.l");
                _panBones[3] = rig.Find(upper + "shoulder.r");
                _panBones[4] = rig.Find(upper + "shoulder.r/arm_stretch.r/forearm_stretch.r");
                _panBones[5] = rig.Find("root/root.x/thigh_stretch.l");
                _panBones[6] = rig.Find("root/root.x/thigh_stretch.l/leg_stretch.l");
                _panBones[7] = rig.Find("root/root.x/thigh_stretch.r");
                _panBones[8] = rig.Find("root/root.x/thigh_stretch.r/leg_stretch.r");
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
            }

            if (kind == HuntKind.Meat || kind == HuntKind.Jerrycan)
            {
                _pickupOutline = GetComponent<QuickOutline.Outline>();
                if (!_pickupOutline)
                    _pickupOutline = gameObject.AddComponent<QuickOutline.Outline>();

                _pickupOutline.OutlineMode = QuickOutline.Outline.Mode.OutlineVisible;
                _pickupOutline.OutlineColor = Color.white;
                _pickupOutline.OutlineWidth = 4f;
                _pickupOutline.UpdateMaterialProperties();
            }
        }

        private void LateUpdate()
        {
            var m = AlienHuntMission.Instance;
            if (!m || !m.Ready) { if (_audio) _audio.Silence(); return; }
            var state = m.Read(index);
            Present(state, m.Authority, Time.deltaTime);
            if (_audio) _audio.Present(state, Time.deltaTime);
            if (kind == HuntKind.Alien && animator)
                PresentPanStruggle((AlienStage)state.Stage, Time.deltaTime);
        }

        private void OnDisable() { if (_audio) _audio.Silence(); }

        private void PresentPanStruggle(AlienStage stage, float delta)
        {
            bool onPan = stage == AlienStage.OnPan || stage == AlienStage.Burning;
            animator.cullingMode = onPan ? AnimatorCullingMode.AlwaysAnimate : _normalAnimatorCulling;
            if (stage == AlienStage.Burning) _panBurnElapsed += delta;
            else _panBurnElapsed = 0f;

            float targetWeight = stage == AlienStage.OnPan ? 1f
                : stage == AlienStage.Burning ? Mathf.Exp(-_panBurnElapsed * 0.45f) : 0f;
            _panStruggleWeight = Mathf.MoveTowards(_panStruggleWeight, targetWeight, delta * 3f);
            if (!onPan || _panStruggleWeight < 0.01f) return;

            // Uneven bursts and offset limbs read as resistance, without moving the
            // whole body off the pan or changing the existing animation asset.
            float phase = Time.time * 7.5f + index * 1.37f;
            float burst = 0.35f + 0.65f * Mathf.Pow(Mathf.Max(0f,
                Mathf.Sin(phase * 0.37f + 0.6f)), 2f);
            float left = (Mathf.Sin(phase) + 0.28f * Mathf.Sin(phase * 2.3f)) * burst * _panStruggleWeight;
            float right = (Mathf.Sin(phase * 0.82f + 2.1f)
                + 0.23f * Mathf.Sin(phase * 2.7f)) * burst * _panStruggleWeight;
            RotatePanBone(0, new Vector3(0f, 0f, 3f * (left - right)));
            RotatePanBone(1, new Vector3(-18f * left, 0f, -7f * left));
            RotatePanBone(2, new Vector3(0f, 0f, 22f * left));
            RotatePanBone(3, new Vector3(-18f * right, 0f, 7f * right));
            RotatePanBone(4, new Vector3(0f, 0f, -22f * right));
            RotatePanBone(5, new Vector3(0f, 0f, -18f * right));
            RotatePanBone(6, new Vector3(0f, 0f, 10f * right));
            RotatePanBone(7, new Vector3(0f, 0f, 18f * left));
            RotatePanBone(8, new Vector3(0f, 0f, -10f * left));
        }

        private void RotatePanBone(int boneIndex, Vector3 degrees)
        {
            var bone = _panBones[boneIndex];
            if (bone) bone.localRotation *= Quaternion.Euler(degrees);
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
            if (_pickupOutline)
            {
                var viewer = Camera.main;
                _pickupOutline.enabled = visible && !carried && viewer
                    && (viewer.transform.position - transform.position).sqrMagnitude <= 20f * 20f;
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
