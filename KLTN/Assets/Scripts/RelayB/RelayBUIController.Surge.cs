using EchoProtocol.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.RelayB
{
    public sealed partial class RelayBUIController
    {
        private RectTransform _surgeRoot, _surgeGrid;
        private Button[] _surgeCells;
        private Image[] _surgeEdges, _surgeFills;
        private TMP_Text[] _surgeSymbols;
        private TMP_Text _surgeTitle, _surgeMetrics, _surgeHint, _surgeLegend, _surgeResult;
        private Button _surgeStart;
        private int _surgeWidth, _surgeHeight, _practiceCell=-1;
        private bool _practiceDone;
        private const string SurgeTutorialKey="Echo.RelayB.SurgeTutorial.v1";
        private static readonly Color SurgeRed=new Color(0.65f,0.22f,0.18f), SurgeBlue=new Color(0.19f,0.43f,0.62f),
            SurgeGreen=new Color(0.33f,0.61f,0.43f), SurgeGray=new Color(0.25f,0.29f,0.3f), SurgeWall=new Color(0.06f,0.075f,0.08f);

        private void EnsureSurgeUI(RelayBSurgeBoard board)
        {
            if(spectrumTabPanel==null)return;
            if(_surgeRoot==null) {
                foreach(Transform child in spectrumTabPanel.transform)child.gameObject.SetActive(false);
                _surgeRoot=SurgeRect("SurgeContainment",spectrumTabPanel.transform,0,0,1080,422);
                _surgeGrid=SurgeRect("Circuit",_surgeRoot,0,8,556,400);
                _surgeTitle=SurgeText("Title",_surgeRoot,604,2,476,34,21);
                _surgeMetrics=SurgeText("Metrics",_surgeRoot,604,48,476,90,18);
                _surgeHint=SurgeText("Hint",_surgeRoot,604,150,476,66,15);
                _surgeLegend=SurgeText("Legend",_surgeRoot,604,236,476,80,14);
                _surgeResult=SurgeText("Result",_surgeRoot,604,318,476,32,16);
                _surgeStart=SurgeButton("Start",_surgeRoot,604,366,320,44,out _,out _);
                EchoProtocol.UI.HUD.HUDModalPresentation.StyleRelayAction(_surgeStart,true);
                _surgeStart.onClick.AddListener(StartSurgeClicked);
                _practiceDone=PlayerPrefs.GetInt(SurgeTutorialKey,0)!=0;
                if(tabButtons.Length>0 && tabButtons[0]!=null)SetText(tabButtons[0].GetComponentInChildren<TMP_Text>(),GameLanguage.Choose("01 PHONG TỎA","01 CONTAIN"));
            }
            if(board==null)return;
            if(_surgeWidth==board.Width && _surgeHeight==board.Height)return;
            foreach(Transform child in _surgeGrid)Destroy(child.gameObject);
            _surgeWidth=board.Width;_surgeHeight=board.Height;
            _surgeCells=new Button[board.Count];_surgeEdges=new Image[board.Count];
            _surgeFills=new Image[board.Count];_surgeSymbols=new TMP_Text[board.Count];
            float step=Mathf.Min(54f,396f/board.Height),size=step-5;
            float left=(556f-board.Width*step)/2f;
            for(int i=0;i<board.Count;i++) {
                int cell=i;
                _surgeCells[i]=SurgeButton("Cell_"+i,_surgeGrid,left+i%board.Width*step,i/board.Width*step,size,size,out _surgeEdges[i],out _surgeFills[i]);
                _surgeSymbols[i]=_surgeCells[i].GetComponentInChildren<TMP_Text>();
                _surgeSymbols[i].fontSize=12;
                _surgeCells[i].onClick.AddListener(()=>SurgeCellClicked(cell));
            }
        }
        private RectTransform SurgeRect(string name,Transform parent,float x,float y,float width,float height)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;
        }
        private TMP_Text SurgeText(string name,Transform parent,float x,float y,float w,float h,int size)
        {
            var rect=SurgeRect(name,parent,x,y,w,h);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=referenceProfileText!=null ? referenceProfileText.font : TMP_Settings.defaultFontAsset;
            text.fontSize=size;text.color=new Color(0.85f,0.88f,0.87f);text.raycastTarget=false;
            text.alignment=TextAlignmentOptions.TopLeft;return text;
        }
        private Button SurgeButton(string name,Transform parent,float x,float y,float w,float h,out Image edge,out Image fill)
        {
            var rect=SurgeRect(name,parent,x,y,w,h);edge=rect.gameObject.AddComponent<Image>();edge.color=SurgeGray;
            var button=rect.gameObject.AddComponent<Button>();
            EchoProtocol.Audio.GameAudioRuntime.RegisterButton(button);button.targetGraphic=edge;EchoProtocol.UI.HUD.HUDModalPresentation.StyleRelayControl(button);
            var inset=SurgeRect("Fill",rect,2,2,w-4,h-4);fill=inset.gameObject.AddComponent<Image>();fill.color=SurgeWall;fill.raycastTarget=false;
            var label=SurgeText("Label",rect,2,2,w-4,h-4,16);label.alignment=TextAlignmentOptions.Center;return button;
        }
        private void RefreshSurgePresentation(bool canOperate)
        {
            if(_controller==null || !IsOpen || _controller.Snapshot.ActiveTab!=0)return;
            var sim=_controller.Surge;var b=sim.Board;EnsureSurgeUI(b);if(_surgeRoot==null)return;
            if(b==null) {
                _surgeTitle.text=GameLanguage.Choose("Đang chuẩn bị mạch…","Preparing circuit…");
                _surgeStart.gameObject.SetActive(false);return;
            }
            bool ready=sim.Phase==RelayBSurgePhase.Ready,practice=ready && !_practiceDone;
            bool canPlace=canOperate && !_controller.IsPreparingSurge;
            var frontier=sim.Frontier;
            for(int cell=0;cell<b.Count;cell++) {
                bool wall=b.Walls.Has(cell),core=b.Cores.Has(cell),red=sim.Infected.Has(cell),seal=sim.Insulated.Has(cell) || practice && cell==_practiceCell;
                Color fill=red ? SurgeRed : core ? SurgeGreen : seal ? SurgeBlue : wall ? SurgeWall : SurgeGray;
                _surgeFills[cell].color=fill;
                _surgeEdges[cell].color=frontier.Has(cell) && sim.Phase==RelayBSurgePhase.Running
                    ? Color.Lerp(fill,warningColor,sim.PulseRemaining<1.5f ? 0.65f+0.35f*Mathf.Sin(Time.unscaledTime*7f) : 0.45f) : canPlace && (practice ? !wall && !core && !red : sim.CanPlace(cell)) ? new Color(0.43f,0.57f,0.58f) : fill;
                _surgeSymbols[cell].text=red ? "!" : core ? "●" : seal ? "=" : wall ? "×" : "";
                _surgeCells[cell].interactable=!_controller.IsPreparingSurge && canOperate && (practice ? !wall && !core && !red : sim.CanPlace(cell));
            }
            _surgeTitle.text=GameLanguage.Choose("Bảo vệ lõi","Protect the cores");
            _surgeMetrics.text=GameLanguage.Choose(
                $"Cách điện  {sim.Remaining} / {b.Budget}\nXung tiếp theo  {sim.PulseRemaining:0.0}s   ·   Còn {Mathf.Max(0,b.TimeLimit-sim.Elapsed):0}s\nAn toàn  {sim.Safety*100:0}%   ·   Tối thiểu {b.MinimumSafety*100:0}%",
                $"Insulation  {sim.Remaining} / {b.Budget}\nNext pulse  {sim.PulseRemaining:0.0}s   ·   Left {Mathf.Max(0,b.TimeLimit-sim.Elapsed):0}s\nSafety  {sim.Safety*100:0}%   ·   Minimum {b.MinimumSafety*100:0}%");
            _surgeHint.text=practice ? GameLanguage.Choose("Nhấn một ô xám để thử cách điện. Đồng hồ chưa chạy.","Select a gray cell to try insulation. The timer is paused.")
                : GameLanguage.Choose("Nhấn ô xám để chặn xung đỏ.","Select gray cells to contain red surges.");
            _surgeLegend.text=GameLanguage.Choose("! Quá tải · = Cách điện · ● Lõi · × Không dẫn\nViền vàng: xung tiếp theo · Mỗi lần đặt cách 1,2 giây",
                "! Overload · = Insulated · ● Core · × Nonconductive\nYellow border: next pulse · 1.2s between placements");
            _surgeResult.text=sim.IsFailed ? FailureText(sim.Phase) : sim.Phase==RelayBSurgePhase.Contained ? GameLanguage.Choose("Đã cô lập · Mở giải mã","Surge contained · Decoder unlocked")
                : sim.Cooldown>0 ? GameLanguage.Choose($"Bộ đặt sẵn sàng sau {sim.Cooldown:0.0}s",$"Placement ready in {sim.Cooldown:0.0}s") : "";
            _surgeResult.color=sim.IsFailed ? dangerColor : safeColor;
            _surgeStart.gameObject.SetActive(ready || sim.IsFailed);
            _surgeStart.interactable=!_controller.IsPreparingSurge && canOperate && (!practice || _practiceCell>=0);
            _surgeStart.GetComponentInChildren<TMP_Text>().text=_controller.IsPreparingSurge ? GameLanguage.Choose("Đang chuẩn bị…","Preparing…") : sim.IsFailed ? GameLanguage.Choose("Thử lại · Bản đồ mới","Retry · New board") : GameLanguage.Choose("Bắt đầu","Start");
            if(_controller.Snapshot.ActiveTab==0) {
                SetText(relayLabel,GameLanguage.Choose("Phong tỏa xung điện","Surge containment"));
                SetText(statusLabel,sim.IsFailed ? GameLanguage.Choose("Quá tải","Overload") : ready ? GameLanguage.Choose("Chờ kích hoạt","Awaiting activation")
                    : sim.Phase==RelayBSurgePhase.Contained ? GameLanguage.Choose("Đã cô lập","Contained") : GameLanguage.Choose("Đang phong tỏa","Containment active"));
                if(statusLabel!=null)statusLabel.color=sim.IsFailed ? dangerColor : sim.Phase==RelayBSurgePhase.Contained ? safeColor : warningColor;
            }
        }
        private static string FailureText(RelayBSurgePhase phase)=>phase==RelayBSurgePhase.CoreLost
            ? GameLanguage.Choose("Xung đã chạm lõi · Thử lại","Surge reached a core · Retry")
            : phase==RelayBSurgePhase.SafetyLost ? GameLanguage.Choose("Quá nhiều mạch bị quá tải · Thử lại","Too many circuits overloaded · Retry")
            : GameLanguage.Choose("Hết thời gian · Thử lại","Time expired · Retry");
        private void SurgeCellClicked(int cell)
        {
            if(_controller==null)return;
            if(_controller.Surge.Phase==RelayBSurgePhase.Ready && !_practiceDone) {_practiceCell=cell;RefreshSurgePresentation(true);return;}
            if(TryGetNetworkDirector(out var director))director.RequestRelayBSurge(_controller,cell,_controller.Surge.Attempt);
            else _controller.PlaceSurgeInsulation(cell,_controller.Surge.Attempt);
        }
        private void StartSurgeClicked()
        {
            if(_controller==null)return;
            _practiceDone=true;_practiceCell=-1;PlayerPrefs.SetInt(SurgeTutorialKey,1);PlayerPrefs.Save();
            if(TryGetNetworkDirector(out var director))director.RequestRelayBSurge(_controller,-1,_controller.Surge.Attempt);
            else _controller.StartSurge();
        }
    }
}
