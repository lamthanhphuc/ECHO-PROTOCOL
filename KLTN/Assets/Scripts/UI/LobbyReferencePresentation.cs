using EchoProtocol.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    public sealed class LobbyReferencePresentation : MonoBehaviour
    {
        public TMP_Dropdown difficulty;
        public Button previousDifficulty, nextDifficulty;
        public TMP_Text difficultyValue, description;
        public LobbyRuntimeBindingsController bindings;
        private float _nextRefresh;
        private void OnEnable()
        {
            previousDifficulty.onClick.AddListener(Previous);
            nextDifficulty.onClick.AddListener(Next);
        }
        private void OnDisable()
        {
            previousDifficulty.onClick.RemoveListener(Previous);
            nextDifficulty.onClick.RemoveListener(Next);
        }
        private void Previous()=>Step(-1);
        private void Next()=>Step(1);
        private void Step(int direction)
        {
            if(!difficulty.interactable || difficulty.options.Count==0)return;
            difficulty.value=(difficulty.value+direction+difficulty.options.Count)%difficulty.options.Count;
        }
        private void Update()
        {
            if(Time.unscaledTime<_nextRefresh)return;
            _nextRefresh=Time.unscaledTime+0.1f;
            previousDifficulty.interactable=nextDifficulty.interactable=difficulty.interactable;
            if(difficulty.options.Count>difficulty.value) difficultyValue.text=System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(GameLanguage.Translate(difficulty.options[difficulty.value].text).ToLowerInvariant());
            if(description!=null && bindings!=null)description.text=bindings.PreviewCharacterId == 2
                ? GameLanguage.Choose("Vị tu sĩ mang lời nguyền từ một nhà nguyện bỏ hoang. Vẫn dẫn đường cho những ai chưa tìm được lối về.", "A priest cursed in an abandoned chapel. Still guides those who have yet to find their way home.")
                : bindings.PreviewCharacterId==1
                ? GameLanguage.Choose("Robot bảo trì thích cứu người hơn sửa máy. Xóa sạch nhật ký lỗi. Giữ lại mọi lời cảm ơn.","A maintenance robot who prefers rescuing people to fixing machines. Deletes every error log. Keeps every thank-you.")
                : GameLanguage.Choose("Cựu kỹ sư cứu hộ quỹ đạo. Trở về từ một trạm mất liên lạc. Vẫn để trống kênh radio cho đồng đội cũ.","A former orbital rescue engineer. Returned from a station that went silent. Still keeps a radio channel open for the old crew.");
        }
    }
}
