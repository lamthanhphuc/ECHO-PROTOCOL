using UnityEngine;

namespace EchoProtocol.Networking
{
    public sealed class AlienHuntFurnace : MonoBehaviour, IInteractable
    {
        [SerializeField] private int index;
        [SerializeField] private Transform pan;
        [SerializeField] private Material flameMaterial;
        private AlienHuntFurnaceEffects effects;
        public int Index => index;
        public Transform Pan => pan;
        public void Configure(int id, Transform anchor) { index = id; pan = anchor; }
        public void ConfigureEffects(Material material) { flameMaterial = material; }
        private void Start()
        { effects = gameObject.AddComponent<AlienHuntFurnaceEffects>(); effects.Initialize(flameMaterial); }
        private void LateUpdate()
        {
            var mission = AlienHuntMission.Instance;
            if (effects) effects.Present(mission && mission.Ready ? mission.ReadFurnace(index) : default);
        }
        public bool OilVisible => effects && effects.OilVisible;
        public bool FireVisible => effects && effects.FireVisible;
        public string InteractionPrompt
        {
            get
            {
                var m = AlienHuntMission.Instance;
                if (!m || !m.Ready) return "ĐANG CHUẨN BỊ LÒ ĐỐT";
                var state = m.ReadFurnace(index);
                if (state.BurnRemaining > 0) return "ĐANG ĐỐT ALIEN NON…";
                int held = m.LocalHeldIndex;
                if (held >= 0) return m.Actors[held].Kind == HuntKind.Jerrycan
                    ? state.Fuel == 0 ? "[" + AlienHuntMission.InteractKey + "] ĐỔ NHIÊN LIỆU VÀO CHẢO" : "CHẢO ĐÃ CÓ NHIÊN LIỆU"
                    : m.Actors[held].Kind == HuntKind.Alien ? state.AlienSlot == 0
                        ? "[" + AlienHuntMission.InteractKey + "] ĐẶT ALIEN LÊN CHẢO" : "CHẢO ĐÃ CÓ ALIEN" : "CẦN CAN NHIÊN LIỆU HOẶC ALIEN ĐÃ BẮT";
                return state.AlienSlot == 0 ? "MANG ALIEN ĐÃ BẮT TỚI ĐÂY" : state.Fuel == 0
                    ? "CẦN ĐỔ NHIÊN LIỆU VÀO CHẢO" : "[" + AlienHuntMission.InteractKey + "] CHÂM LỬA ĐỐT ALIEN";
            }
        }
        public bool CanInteract(GameObject interactor) => interactor && AlienHuntMission.Instance
            && AlienHuntMission.Instance.Ready && !AlienHuntMission.Instance.Completed;
        public void Interact(GameObject interactor)
        { if (CanInteract(interactor)) AlienHuntMission.Instance.Request(-1, index, interactor); }
    }
}
