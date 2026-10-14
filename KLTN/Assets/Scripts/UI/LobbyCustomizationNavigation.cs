using EchoProtocol.UI.MainMenu;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    public sealed class LobbyCustomizationNavigation : MonoBehaviour
    {
        [SerializeField] private Button outfitButton;
        [SerializeField] private TeamToolShopPanel store;
        private void OnEnable()
        {
            if(outfitButton==null)foreach(var button in GetComponentsInChildren<Button>(true))
                if(button.name=="OutfitSelectionButton"){outfitButton=button;break;}
            if(store==null)store=Object.FindAnyObjectByType<TeamToolShopPanel>(FindObjectsInactive.Include);
            if(outfitButton!=null)outfitButton.onClick.AddListener(Open);
        }
        private void OnDisable(){if(outfitButton!=null)outfitButton.onClick.RemoveListener(Open);}
        private void Open()
        {
            if(store==null)return;
            if(store.GetComponent<StoreLobbyEquipment>()==null)store.gameObject.AddComponent<StoreLobbyEquipment>();
            store.Open();
        }
    }
}
