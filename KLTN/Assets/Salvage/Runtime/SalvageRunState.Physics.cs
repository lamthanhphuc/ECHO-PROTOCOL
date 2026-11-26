using Fusion;
using UnityEngine;
using EchoProtocol.Networking;

namespace EchoProtocol.Salvage
{
    public sealed partial class SalvageRunState
    {
        private float _nextReceivingCue;
        private float _nextDelivery;
        private readonly float[] _nextGripAdjustment=new float[4];
        private Vector3 BagDeliveryPosition(int index)
        {
            if(Site.RandomMap==null)return Site.ReturnPoint.position;
            var room=Site.RandomMap.FixedBase;int slot=0;
            // Distinct receiving slots for all 128 serials; keep the terminal and crew spawn clear.
            for(int row=0;row<13;row++)for(int column=0;column<13;column++)
            {
                var point=room.transform.TransformPoint(new Vector3((column-6)*1.8f,.03f,(row-6)*1.8f));
                var terminal=Site.TerminalPoint.position;
                if(Vector2.Distance(new Vector2(point.x,point.z),new Vector2(terminal.x,terminal.z))<2.2f)continue;
                bool nearCrew=false;
                foreach(var crew in Site.CrewSpawns)if(Vector2.Distance(new Vector2(point.x,point.z),new Vector2(crew.position.x,crew.position.z))<1.6f){nearCrew=true;break;}
                if(nearCrew)continue;
                if(slot++==index)return point;
            }
            return room.transform.position+Vector3.up*.03f;
        }
        public int PhysicsHeldIndex(int owner)
        { for(int i=0;i<Items.Length;i++)if(Cargo(i).State==SalvageCargoState.PhysicsHeld&&Cargo(i).Owner==owner)return i;return -1; }
        public bool BeginGrip(PlayerRef actor,int index,int serial)
        {
            int seat=Seat(actor);
            if(!TryActor(actor,out var player)||seat<0||_ledger==null
                ||!WorldItems.TryGetValue(index,out var item)||item.Serial!=serial)return false;
            Vector3 eye=GripEye(player);
            Vector3 point=item.WorldBounds.ClosestPoint(eye);
            if(Vector3.Distance(eye,point)>3.5f)return false;
            Vector3 direction=point-eye;
            if(Physics.Raycast(eye,direction.normalized,out var hit,direction.magnitude+.05f,~0,QueryTriggerInteraction.Ignore)
                &&hit.collider.GetComponentInParent<SalvageScrap>()!=item&&hit.collider.GetComponentInParent<LobbyPlayerState>()==null)return false;
            if(!_ledger.TryGrab(index,serial,seat))return false;
            var crew=Crew[seat];crew.GripDistance=Mathf.Clamp(Vector3.Distance(eye,item.WorldBounds.center),1.1f,2.8f);
            crew.GripRotation=Quaternion.Inverse(GripLook(player))*item.transform.rotation;
            crew.GripLocalPoint=item.transform.InverseTransformPoint(hit.collider!=null&&hit.collider.GetComponentInParent<SalvageScrap>()==item?hit.point:point);
            Crew.Set(seat,crew);
            item.LastHandler=actor;item.PhysicsHandled=true;Publish();RPC_Cue(9,item.GripPoint,(int)SalvageLedger.Types[Cargo(index).Definition].Material);return true;
        }
        public bool ReleaseGrip(int seat)
        {
            if(_ledger==null)return false;int index=_ledger.PhysicsHeld(seat);
            if(index<0||!_ledger.TryRelease(index,Cargo(index).Serial,seat))return false;
            if(WorldItems.TryGetValue(index,out var item)){SetCargoPose(index,item.transform.position,item.transform.rotation,item.BodyVelocity);RPC_Cue(10,item.GripPoint,(int)SalvageLedger.Types[Cargo(index).Definition].Material);}
            Publish();return true;
        }
        public bool PocketGrip(PlayerRef actor)
        {
            int seat=Seat(actor);int index=seat>=0?PhysicsHeldIndex(seat):-1;
            if(index<0||!TryActor(actor,out var player)||!SalvageLedger.Types[Cargo(index).Definition].Pocketable)return false;
            int serial=Cargo(index).Serial;
            // Keep grip ownership if the bag has no room.
            var original=_ledger.Cargo[index];_ledger.TryRelease(index,serial,seat);
            if(!_ledger.TryPickup(index,serial,seat,out int selected)){_ledger.Cargo[index]=original;return false;}
            var crew=Crew[seat];crew.Selected=selected;Crew.Set(seat,crew);Publish();return true;
        }
        public static Quaternion GripLook(NetworkObject player)
        {
            var movement=player.GetComponent<NetworkPlayerMovement>();
            return Quaternion.Euler(movement!=null?movement.CurrentPitch:0,player.transform.eulerAngles.y,0);
        }
        public static Vector3 GripEye(NetworkObject player)
        {
            var capsule=player.GetComponent<CharacterController>();
            var movement=player.GetComponent<NetworkPlayerMovement>();
            float offset=capsule!=null?capsule.center.y-capsule.height*.5f:0;
            return player.transform.position+Vector3.up*(offset+(movement!=null&&movement.IsCrouching?1.3f:1.65f));
        }
        public void SetCargoPose(int index,Vector3 position,Quaternion rotation,Vector3 velocity)
        {
            if(!Object.HasStateAuthority||index<0||index>=Items.Length)return;
            var data=Items[index];data.Position=position;data.Rotation=rotation;data.Velocity=velocity;Items.Set(index,data);
        }
        public void AdjustGrip(float distance,int yaw,int pitch)
        {if(Object!=null&&Object.IsValid)RPC_AdjustGrip(distance,yaw,pitch);}
        [Rpc(RpcSources.All,RpcTargets.StateAuthority,HostMode=RpcHostMode.SourceIsHostPlayer)]
        private void RPC_AdjustGrip(float distance,int yaw,int pitch,RpcInfo info=default)
        {
            int seat=Seat(info.Source);
            if(seat<0||!TryActor(info.Source,out _)||PhysicsHeldIndex(seat)<0||!float.IsFinite(distance)
                ||Time.time<_nextGripAdjustment[seat])return;
            _nextGripAdjustment[seat]=Time.time+.08f;
            var crew=Crew[seat];crew.GripDistance=Mathf.Clamp(distance,1.1f,2.8f);
            crew.GripRotation=Quaternion.Euler(Mathf.Clamp(pitch,-1,1)*15,Mathf.Clamp(yaw,-1,1)*15,0)*crew.GripRotation;Crew.Set(seat,crew);
        }
        private void UpdateDeliveries(bool finishDay=false)
        {
            if(_ledger==null||Site==null||!finishDay&&Time.time<_nextDelivery)return;_nextDelivery=Time.time+.2f;
            bool changed=false,received=false;
            for(int seat=0;seat<4;seat++)
            {
                var player=Player(seat);
                if(player==null||!IsConnected(seat)||!Site.Contains(player.transform.position))continue;
                // Existing bag contents enter the receiving room without a separate terminal deposit action.
                for(int i=0;i<Items.Length;i++)if(Cargo(i).State==SalvageCargoState.Held&&Cargo(i).Owner==seat)
                {
                    var position=BagDeliveryPosition(i);
                    SetCargoPose(i,position,Quaternion.identity,Vector3.zero);
                }
                bool stored=_ledger.Store(seat)>0;changed|=stored;received|=stored;
            }
            for(int i=0;i<Items.Length;i++)
            {
                var cargo=_ledger.Cargo[i];
                bool inside=cargo.State==SalvageCargoState.Stored
                    ||(cargo.State==SalvageCargoState.World||cargo.State==SalvageCargoState.PhysicsHeld)
                        &&WorldItems.TryGetValue(i,out var worldItem)&&Site.ContainsCargo(worldItem.WorldBounds);
                var data=Items[i];
                if(data.InReceivingRoom!=inside){data.InReceivingRoom=inside;Items.Set(i,data);_ledger.ReceivingChanged();changed=true;}
                if(cargo.State!=SalvageCargoState.World||!WorldItems.TryGetValue(i,out var item)||!finishDay&&!item.ReadyForDelivery||!Site.ContainsCargo(item.WorldBounds))continue;
                bool accepted=_ledger.AcceptDelivery(i,cargo.Serial);changed|=accepted;received|=accepted;
            }
            if(changed)Publish();
            if(received&&Time.time>=_nextReceivingCue){_nextReceivingCue=Time.time+.6f;RPC_Cue(1,Site.ReturnPoint.position,0);}
        }
        public bool IsSaleEligible(int index)
        {
            if(Cargo(index).Definition==SalvageLedger.CartDefinition)return false;
            var state=Cargo(index).State;
            return state==SalvageCargoState.Stored||(state==SalvageCargoState.World||state==SalvageCargoState.PhysicsHeld)&&Items[index].InReceivingRoom;
        }
        public int ReceivedValue()
        {int total=0;for(int i=0;i<Items.Length;i++)if(IsSaleEligible(i))total+=Cargo(i).Value;return total;}
        private bool PrepareReceivedSale(int index,int serial)
        {
            if(!_ledger.IsItem(index,serial)||Cargo(index).Definition==SalvageLedger.CartDefinition)return false;var cargo=_ledger.Cargo[index];
            if(cargo.State==SalvageCargoState.Stored)return true;
            if((cargo.State!=SalvageCargoState.World&&cargo.State!=SalvageCargoState.PhysicsHeld)
                ||!WorldItems.TryGetValue(index,out var item)||!Site.ContainsCargo(item.WorldBounds))return false;
            if(cargo.State==SalvageCargoState.PhysicsHeld)ReleaseGrip(cargo.Owner);
            return _ledger.AcceptDelivery(index,serial);
        }
        private bool SellAllReceived(int expectedRevision,int expectedValue)
        {
            if(expectedRevision!=Revision||expectedValue<=0||ReceivedValue()!=expectedValue)return false;
            for(int i=0;i<Items.Length;i++)if(IsSaleEligible(i)&&Cargo(i).State!=SalvageCargoState.Stored
                &&(!WorldItems.TryGetValue(i,out var item)||!Site.ContainsCargo(item.WorldBounds)))return false;
            for(int i=0;i<Items.Length;i++)if(IsSaleEligible(i))PrepareReceivedSale(i,Cargo(i).Serial);
            return _ledger.TrySellAll(_ledger.Revision,expectedValue);
        }
    }
}
