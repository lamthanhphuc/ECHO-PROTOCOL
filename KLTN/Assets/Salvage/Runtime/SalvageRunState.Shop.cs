using EchoProtocol.Networking;
using Fusion;
using UnityEngine;
namespace EchoProtocol.Salvage
{
    public sealed partial class SalvageRunState
    {
        public int EquipmentRemaining(int product)
        {
            if(product<1||product>6)return 0;
            if(product==6 && (CartPurchaseLevel==Cycle || HasCart()))return 0;
            return Mathf.Max(0,SalvageLedger.EquipmentLimit(product)-(ShopDay==Cycle*3+Day?ShopBought[product-1]:0));
        }
        private bool HasCart()
        {
            for(int i=0;i<Items.Length;i++)if(Cargo(i).Definition==SalvageLedger.CartDefinition &&
                (Cargo(i).State==SalvageCargoState.Stored||Cargo(i).State==SalvageCargoState.World||Cargo(i).State==SalvageCargoState.PhysicsHeld))return true;
            return false;
        }
        private bool BuyEquipment(NetworkObject player,int product)
        {
            if(!AtTerminal(player)||EquipmentRemaining(product)<=0 || _ledger==null)return false;
            var state=player.GetComponent<LobbyPlayerState>();
            bool cart=product==6;
            Vector3 cartPosition=default;
            if(cart && !FindCartDelivery(out cartPosition))return false;
            if(cart)
            {
                bool free=false;foreach(var cargo in _ledger.Cargo)if(cargo.State==SalvageCargoState.Empty||cargo.State==SalvageCargoState.Sold||cargo.State==SalvageCargoState.Destroyed){free=true;break;}
                if(!free)return false;
            }
            if(!_ledger.TryPurchase(product,cart||state.ToolId==0))return false;
            if(ShopDay!=Cycle*3+Day){for(int i=0;i<6;i++)ShopBought.Set(i,0);ShopDay=Cycle*3+Day;}
            ShopBought.Set(product-1,ShopBought[product-1]+1);
            if(!cart)state.SetGameplayToolId(product==5?3:product,product==5?2:product==3?1:LobbyPlayerState.ConfiguredMultiUseTeamToolUses);
            else
            {
                _ledger.TryCreateCart(out int index);CartPurchaseLevel=Cycle;Publish();
                SetCargoPose(index,cartPosition,Quaternion.identity,Vector3.zero);
                if(WorldItems.TryGetValue(index,out var previous)&&previous!=null)Runner.Despawn(previous.Object);
                int serial=Cargo(index).Serial;
                Runner.Spawn(Site.ScrapPrefab,cartPosition,Quaternion.identity,null,(_,obj)=>{var s=obj.GetComponent<SalvageScrap>();s.Index=index;s.Serial=serial;});
            }
            return true;
        }
        private bool FindCartDelivery(out Vector3 result)
        {
            var terminal=Site.TerminalPoint.position;
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8;
                var candidate=terminal+new Vector3(Mathf.Cos(angle)*2.2f,0,Mathf.Sin(angle)*2.2f);
                if(!Physics.Raycast(candidate+Vector3.up*2,Vector3.down,out var floor,4,~0,QueryTriggerInteraction.Ignore))continue;
                candidate=floor.point+Vector3.up*.04f;
                if(!Site.ContainsCargo(new Bounds(candidate+Vector3.up*.6f,new Vector3(1.2f,1.2f,1.6f))))continue;
                if(Physics.CheckBox(candidate+Vector3.up*.6f,new Vector3(.6f,.55f,.8f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore))continue;
                result=candidate;return true;
            }
            result=default;return false;
        }
    }
}
