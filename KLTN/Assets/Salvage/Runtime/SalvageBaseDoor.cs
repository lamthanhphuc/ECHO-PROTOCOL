using UnityEngine;

namespace EchoProtocol.Salvage
{
    // Driven by the replicated Run phase on every peer, including the host's collision world.
    public sealed class SalvageBaseDoor : MonoBehaviour
    {
        public GameObject ClosedPanels;
        public GameObject OpenPassage;
        public bool IsOpen { get; private set; }
        private void Awake() => SetOpen(false);
        private void LateUpdate()
        {
            var run=SalvageRunState.Instance;
            SetOpen(run!=null && run.Object!=null && run.Object.IsValid
                && run.Phase==SalvagePhase.InDay && run.LocalMapReady);
        }
        private void SetOpen(bool open)
        {
            IsOpen=open;
            if(ClosedPanels!=null && ClosedPanels.activeSelf==open)ClosedPanels.SetActive(!open);
            if(OpenPassage!=null && OpenPassage.activeSelf!=open)OpenPassage.SetActive(open);
        }
    }
}
