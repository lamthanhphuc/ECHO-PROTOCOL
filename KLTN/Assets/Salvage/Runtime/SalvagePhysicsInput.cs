using UnityEngine;
using UnityEngine.InputSystem;
using EchoProtocol.Networking;

namespace EchoProtocol.Salvage
{
    // Lives on the existing HUD, scoped to Salvage. Existing Field Scanner/tool bindings stay intact.
    public sealed class SalvagePhysicsInput : MonoBehaviour
    {
        private bool _mouseGrip;
        private float _adjustAt;
        private void Update()
        {
            var run=SalvageRunState.Instance;
            if(run==null||run.Object==null||!run.Object.IsValid)return;
            int seat=run.Seat(run.Runner.LocalPlayer);var player=run.Player(seat);if(seat<0||player==null)return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;int held=run.PhysicsHeldIndex(seat);
            if(PlayerInteractionControlLock.IsGameplayInputBlocked(player.gameObject))
            {if(held>=0)run.Request(SalvageCommand.Release);_mouseGrip=false;return;}
            if(held>=0)
            {
                if(keyboard!=null&&keyboard.eKey.wasPressedThisFrame)run.Request(SalvageCommand.Pocket);
                if(mouse!=null&&_mouseGrip&&!mouse.leftButton.isPressed){run.Request(SalvageCommand.Release);_mouseGrip=false;}
                if(mouse!=null&&Time.unscaledTime>=_adjustAt&&Mathf.Abs(mouse.scroll.ReadValue().y)>.01f)
                {
                    int step=mouse.scroll.ReadValue().y>0?1:-1;bool rotate=mouse.rightButton.isPressed;
                    bool pitch=keyboard!=null&&keyboard.leftShiftKey.isPressed;
                    run.AdjustGrip(run.Crew[seat].GripDistance+(rotate?0:step*.2f),rotate&&!pitch?step:0,rotate&&pitch?step:0);_adjustAt=Time.unscaledTime+.1f;
                }
            }
            else if(mouse!=null&&mouse.leftButton.wasPressedThisFrame&&!(run.Crew[seat].Selected==4&&run.Crew[seat].Tool!=0))
            {
                var camera=run.Site.GameplayCamera.GetComponent<Camera>();
                if(camera!=null&&Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,3.5f,~0,QueryTriggerInteraction.Ignore))
                {
                    var scrap=hit.collider.GetComponentInParent<SalvageScrap>();
                    if(scrap!=null&&scrap.IsAvailableInWorld){run.Request(SalvageCommand.Grab,scrap.Index,scrap.Serial);_mouseGrip=true;}
                }
            }
            else if(mouse!=null&&!mouse.leftButton.isPressed)_mouseGrip=false;
        }
    }
}
