using Fusion;
using UnityEngine;
using EchoProtocol.Networking;

namespace EchoProtocol.Salvage
{
    public sealed partial class SalvageRunState
    {
        private readonly System.Collections.Generic.List<Bounds> _receivingReservations=new();
        private float _nextReceivingCue;
        private float _nextDelivery;
        private readonly float[] _nextGripAdjustment=new float[4];
        private Vector3 BagDeliveryPosition(int index)
        {
            if(Site.RandomMap==null)return Site.ReturnPoint.position;
            var room=Site.RandomMap.FixedBase;
            int owner=Cargo(index).Owner;var player=Player(owner);
            var preferred=player!=null?player.transform.position+player.transform.forward*1.8f:Site.ReturnPoint.position;
            var size=Site.ScrapSize(Cargo(index).Definition);
            for(int ring=0;ring<24;ring++)for(int step=0;step<24;step++)
            {
                float angle=step*Mathf.PI/12;
                var point=preferred+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(.6f+ring*.65f);
                if(!Physics.Raycast(point+Vector3.up*3.5f,Vector3.down,out var floor,6,~0,QueryTriggerInteraction.Ignore)||floor.normal.y<.8f||floor.collider.GetComponentInParent<LobbyPlayerState>()!=null)continue;
                point=floor.point+Vector3.up*.04f;
                var bounds=new Bounds(point+Vector3.up*(size.y*.5f+.02f),size);
                if(!Site.ContainsCargo(bounds))continue;
                if(Physics.CheckBox(bounds.center,size*.49f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))continue;
                bool occupied=false;
                for(int i=0;i<Items.Length;i++)if(i!=index&&_ledger.Cargo[i].State==SalvageCargoState.Stored)
                {
                    var otherSize=Site.ScrapSize(_ledger.Cargo[i].Definition);
                    var other=new Bounds(Items[i].Position+Vector3.up*(otherSize.y*.5f+.02f),otherSize+Vector3.one*.1f);
                    if(other.Intersects(bounds)){occupied=true;break;}
                }
                foreach(var reservation in _receivingReservations)if(reservation.Intersects(bounds)){occupied=true;break;}
                if(!occupied){_receivingReservations.Add(bounds);return point;}
            }
            // A completely full receiving floor keeps the item near the crew, visibly above the floor.
            return new Vector3(float.NaN,0,0);
        }

        public int PhysicsHeldIndex(int owner)
        { for(int i=0;i<Items.Length;i++)if(Cargo(i).State==SalvageCargoState.PhysicsHeld&&Cargo(i).Owner==owner)return i;return -1; }
        public bool BeginGrip(PlayerRef actor,int index,int serial)
        {
            int seat=Seat(actor);
            if(!TryActor(actor,out var player)||seat<0||_ledger==null
                ||!WorldItems.TryGetValue(index,out var item)||item.Serial!=serial)return false;
            Vector3 eye=GripEye(player);
            Vector3 direction=GripLook(player)*Vector3.forward;
            var hits=Physics.RaycastAll(eye,direction,3.5f,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            RaycastHit hit=default;
            foreach(var candidate in hits)
            {
                if(candidate.collider.GetComponentInParent<LobbyPlayerState>()!=null)continue;
                hit=candidate;break;
            }
            if(hit.collider==null||hit.collider.GetComponentInParent<SalvageScrap>()!=item)return false;
            if(!_ledger.TryGrab(index,serial,seat))return false;
            var crew=Crew[seat];crew.GripDistance=Mathf.Clamp(hit.distance,1.1f,2.8f);
            crew.GripRotation=Quaternion.Inverse(GripLook(player))*item.transform.rotation;
            crew.GripLocalPoint=item.transform.InverseTransformPoint(hit.point);
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
            _receivingReservations.Clear();
            bool changed=false,received=false;
            for(int seat=0;seat<4;seat++)
            {
                var player=Player(seat);
                if(player==null||!IsConnected(seat)||!Site.Contains(player.transform.position))continue;
                // Existing bag contents enter the receiving room without a separate terminal deposit action.
                for(int i=0;i<Items.Length;i++)if(Cargo(i).State==SalvageCargoState.Held&&Cargo(i).Owner==seat)
                {
                    var position=BagDeliveryPosition(i);
                    if(!float.IsFinite(position.x))continue;
                    SetCargoPose(i,position,Quaternion.identity,Vector3.zero);
                    bool stored=_ledger.StoreItem(i,seat);changed|=stored;received|=stored;
                }

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
