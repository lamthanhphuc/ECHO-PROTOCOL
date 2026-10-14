using System.Collections;
using EchoProtocol.Networking;
using EchoProtocol.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    public sealed class StoreLobbyEquipment : MonoBehaviour
    {
        private Coroutine _pending;
        private float _refreshAt;
        private Button _unequipPet;
        private void EnsurePetRemoval()
        {
            if(_unequipPet!=null)return;
            var panel=transform.Find("Window/Content/PetContent");
            if(panel==null)return;
            var root=new GameObject("UnequipPet",typeof(RectTransform),typeof(Image),typeof(Button));
            root.transform.SetParent(panel,false);
            var rect=root.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0.667f,0.1f);rect.pivot=new Vector2(0,1);rect.sizeDelta=new Vector2(190,42);
            root.GetComponent<Image>().color=new Color(0.13f,0.13f,0.1f);
            _unequipPet=root.GetComponent<Button>();
            var label=new GameObject("Caption",typeof(RectTransform),typeof(Text));label.transform.SetParent(root.transform,false);
            var text=label.GetComponent<Text>();text.font=GetComponentInChildren<Text>(true).font;text.fontSize=22;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(0.8f,0.76f,0.57f);text.raycastTarget=false;
            var textRect=label.GetComponent<RectTransform>();textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=Vector2.zero;textRect.offsetMax=Vector2.zero;
            _unequipPet.onClick.AddListener(()=>{var state=LocalState();if(state!=null && !state.IsReady && !state.IsGameplayPlayer)state.RequestPet(0);});
            EchoProtocol.Audio.GameAudioRuntime.RegisterButton(_unequipPet);
        }
        public static LobbyPlayerState LocalState()
        {
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Lobby")return null;
            var manager=Object.FindAnyObjectByType<LobbyManager>();
            return manager!=null && manager.TryGetLocalPlayerState(out var state,false) ? state : null;
        }
        public static bool ShowAction(Button button,Text price,bool pet,int id,int skin,bool owned)
        {
            var state=LocalState();
            if(state==null || !owned)return false;
            bool equipped=pet ? state.PetId==id && state.PetSkinId==skin
                : state.CharacterId==id && state.CharacterOutfitId==skin;
            price.text=string.Empty;
            button.interactable=!state.IsReady && !equipped && !state.IsGameplayPlayer;
            var caption=button.GetComponentInChildren<Text>(true);
            caption.resizeTextForBestFit=true;caption.resizeTextMinSize=16;caption.resizeTextMaxSize=26;
            caption.text=state.IsReady
                ? GameLanguage.Choose("Bỏ sẵn sàng để đổi","Unready to change")
                : equipped ? GameLanguage.Choose("Đang dùng","Equipped") : GameLanguage.Choose("Trang bị","Equip");
            return true;
        }
        public static bool Equip(Transform root,bool pet,int id,int skin,bool owned)
        {
            var state=LocalState();
            if(state==null || !owned || !root.gameObject.activeInHierarchy)return false;
            if(state.IsReady || state.IsGameplayPlayer)return true;
            var controller=root.GetComponent<StoreLobbyEquipment>() ?? root.gameObject.AddComponent<StoreLobbyEquipment>();
            if(controller._pending!=null)controller.StopCoroutine(controller._pending);
            controller._pending=controller.StartCoroutine(controller.Apply(state,pet,id,skin));
            return true;
        }
        private IEnumerator Apply(LobbyPlayerState state,bool pet,int id,int skin)
        {
            bool change=pet ? state.PetId!=id : state.CharacterId!=id;
            if(change && !(pet ? state.RequestPet(id) : state.RequestCharacter(id)))yield break;
            float until=Time.unscaledTime+8f;
            while(state!=null && Time.unscaledTime<until && (pet ? state.PetId!=id : state.CharacterId!=id))yield return null;
            if(state!=null && !state.IsReady && !state.IsGameplayPlayer && (pet ? state.PetId==id : state.CharacterId==id)) {
                if(pet && state.PetSkinId!=skin)state.RequestPetSkin(skin);
                if(!pet && state.CharacterOutfitId!=skin)state.RequestCharacterOutfit(skin);
            }
            _pending=null;
        }
        private void Update()
        {
            if(Time.unscaledTime<_refreshAt)return;
            _refreshAt=Time.unscaledTime+0.25f;
            EnsurePetRemoval();
            if(_unequipPet!=null){var state=LocalState();_unequipPet.interactable=state!=null && !state.IsReady && !state.IsGameplayPlayer && state.PetId!=0;_unequipPet.GetComponentInChildren<Text>().text=GameLanguage.Choose("Cất thú cưng","Unequip pet");}
            GetComponent<CharacterShopController>()?.SendMessage("SetBuyState",SendMessageOptions.DontRequireReceiver);
            GetComponent<PetShopController>()?.SendMessage("SetBuyState",SendMessageOptions.DontRequireReceiver);
            if(LocalState()!=null)foreach(var button in GetComponentsInChildren<Button>(false)) {
                if(!button.name.Contains("BuyButton"))continue;
                var text=button.GetComponentInChildren<Text>();
                if(text!=null && (text.text=="Mua ngay" || text.text=="Buy Now")) {
                    text.text=GameLanguage.Choose("Mua","Buy");
                    text.resizeTextForBestFit=true;text.resizeTextMinSize=16;text.resizeTextMaxSize=26;
                }
            }
        }
        private void OnDisable(){if(_pending!=null)StopCoroutine(_pending);_pending=null;}
    }
}
