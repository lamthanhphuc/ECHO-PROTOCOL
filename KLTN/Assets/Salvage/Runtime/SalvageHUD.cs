using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.UI.HUD;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EchoProtocol.Salvage
{
    // Adds only Salvage information and terminal panels to the existing ECHO HUD canvas.
    public sealed class SalvageHUD : MonoBehaviour
    {
        public static SalvageHUD Instance { get; private set; }
        private Text _mission, _journalText;
        private RectTransform _missionRoot, _journal;
        private Text _countdown, _title, _summary, _feedback, _pageLabel;
        private RectTransform _terminal, _rows, _countdownRoot;
        private Button _departure, _sellAll, _endRun, _previous, _next;
        private Font _font;
        private PlayerInteractionControlLock _inputLock;
        private SalvageRunState _run;
        private int _tab, _page, _lastRevision = -1, _lastPhase = -1, _lastVotes = -1;
        private float _refreshAt, _feedbackUntil;
        private readonly List<Button> _tabs = new();
        private readonly List<GameObject> _rowObjects = new();

        private void Awake()
        {
            Instance = this;
            gameObject.AddComponent<SalvagePhysicsInput>();
            var source = GetComponentInChildren<Text>(true);
            _font = source != null ? source.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _inputLock = new PlayerInteractionControlLock();
            _countdownRoot = Rect("SalvageCountdown", transform, new Vector2(.5f,1), new Vector2(0,-24), new Vector2(128,40));
            _countdown = Label(_countdownRoot, "Time", Vector2.zero, new Vector2(128,40), 26);
            _countdown.alignment = TextAnchor.MiddleCenter;
            _countdownRoot.gameObject.SetActive(false);
            _missionRoot = Panel("SalvageMission", transform, new Vector2(0,1), new Vector2(24,-24), new Vector2(360,112));
            _mission = Label(_missionRoot,"Mission",new Vector2(14,-10),new Vector2(332,96),16);
            _journal = Panel("SalvageJournal",transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(650,420));
            _journal.pivot=new Vector2(.5f,.5f);
            Label(_journal,"Title",new Vector2(24,-18),new Vector2(600,40),22).text="NHIỆM VỤ THU HỒI";
            _journalText=Label(_journal,"Details",new Vector2(24,-70),new Vector2(600,272),17);
            Button(_journal,"ĐÓNG [J]",466,360,160,CloseTerminal);_journal.gameObject.SetActive(false);
            _terminal = Panel("SalvageTerminal", transform, new Vector2(.5f,.5f), Vector2.zero, new Vector2(860,630));
            _terminal.pivot = new Vector2(.5f,.5f);
            _title = Label(_terminal, "Title", new Vector2(24,-14), new Vector2(700,42), 20);
            _summary = Label(_terminal, "Summary", new Vector2(24,-58), new Vector2(812,32), 15);
            _tabs.Add(Button(_terminal, "BÁN HÀNG / KHO", 24, 100, 250, () => { _tab=0; _page=0; RefreshTerminal(); }));
            _tabs.Add(Button(_terminal, "TRANG BỊ", 290, 100, 250, () => { _tab=1; _page=0; RefreshTerminal(); }));
            _tabs.Add(Button(_terminal, "NÂNG CẤP CÁ NHÂN", 556, 100, 278, () => { _tab=2; _page=0; RefreshTerminal(); }));
            _rows = Rect("Rows", _terminal, new Vector2(0,1), new Vector2(24,-154), new Vector2(812,342));
            _previous = Button(_terminal, "‹", 24, 508, 50, () => { _page=Mathf.Max(0,_page-1); RefreshTerminal(); });
            _pageLabel = Label(_terminal, "Page", new Vector2(86,-515), new Vector2(220,24), 14);
            _next = Button(_terminal, "›", 310, 508, 50, () => { _page++; RefreshTerminal(); });
            _sellAll = Button(_terminal, "BÁN TẤT CẢ", 380, 508, 180, ConfirmSellAll);
            _departure = Button(_terminal, "KHỞI HÀNH", 24, 570, 200, DepartOrReturn);
            _endRun = Button(_terminal, "ĐỒNG Ý KẾT THÚC CHUYẾN", 238, 570, 276, () => _run?.Request(SalvageCommand.VoteEnd));
            Button(_terminal, "ĐÓNG", 674, 570, 160, CloseTerminal);
            _feedback = Label(_terminal, "Feedback", new Vector2(570,-512), new Vector2(260,45), 13);
            _feedback.color = HUDPresentationStyle.Warning; _terminal.gameObject.SetActive(false);
        }
        private void OnDestroy()
        {
            if (_run != null) _run.LocalFeedback -= Feedback;
            if (_inputLock != null) _inputLock.Release();
            if (Instance == this) Instance = null;
        }
        private void OnDisable() => CloseTerminal();
        private void Update()
        {
            if (_run != SalvageRunState.Instance)
            {
                if (_run != null) _run.LocalFeedback -= Feedback;
                _run = SalvageRunState.Instance;
                if (_run != null) _run.LocalFeedback += Feedback;
            }
            if (_run == null || _run.Object == null || !_run.Object.IsValid) return;
            int seat = _run.Seat(_run.Runner.LocalPlayer); if (seat < 0) return;
            if ((_terminal.gameObject.activeSelf || _journal.gameObject.activeSelf) && _inputLock.ShouldAutoRelease()) { CloseTerminal(); return; }
            var keyboard = Keyboard.current;
            if (_journal.gameObject.activeSelf && (_inputLock.ConsumeEscape() || keyboard != null && keyboard.jKey.wasPressedThisFrame)) { CloseTerminal(); return; }
            if (_terminal.gameObject.activeSelf && _inputLock.ConsumeEscape()) { CloseTerminal(); return; }
            if (!_terminal.gameObject.activeSelf && !_journal.gameObject.activeSelf && keyboard != null)
            {
                var player = _run.Player(seat);
                if (player != null && !PlayerInteractionControlLock.IsGameplayInputBlocked(player.gameObject))
                {
                    if (keyboard.jKey.wasPressedThisFrame) OpenJournal();
                    if (keyboard.digit1Key.wasPressedThisFrame) _run.Request(SalvageCommand.Select,0);
                    if (keyboard.digit2Key.wasPressedThisFrame) _run.Request(SalvageCommand.Select,1);
                    if (keyboard.digit3Key.wasPressedThisFrame) _run.Request(SalvageCommand.Select,2);
                    if (keyboard.digit4Key.wasPressedThisFrame) _run.Request(SalvageCommand.Select,3);
                    if (keyboard.digit5Key.wasPressedThisFrame) _run.Request(SalvageCommand.Select,4);
                    if (keyboard.bKey.wasPressedThisFrame && _run.Site.Contains(player.transform.position)) OpenTerminal(false);
                }
            }
            if (Time.unscaledTime < _refreshAt) return;
            _refreshAt = Time.unscaledTime + .1f;
            _missionRoot.gameObject.SetActive(!_terminal.gameObject.activeSelf && !_journal.gameObject.activeSelf);
            _mission.text=$"THU HỒI • Level {_run.Cycle} • Ngày {_run.Day}/3\nTiền phải kiếm: {_run.SoldThisCycle}/{_run.Quota}\nTiền dư: {_run.Funds}  •  [J] Chi tiết";
            int local = _run.Seat(_run.Runner.LocalPlayer);
            _journalText.text=$"Level {_run.Cycle} — Ngày {_run.Day}/3\nTiền phải kiếm: {_run.SoldThisCycle}/{_run.Quota}\nTiền dư: {_run.Funds} — chỉ phần bán vượt mục tiêu mới dùng mua đồ.\n\nChuyến vừa qua: mang về {_run.LastDayRecovered}, mất {_run.LastDayLost}.\nĐang mang: {_run.Weight(local):0.#} kg.\nĐưa hàng về căn cứ, mở terminal để bán trước hết ngày 3.\nNâng cấp chỉ dành cho người mua, giữ đến hết chuyến chơi.";
            _journalText.text+="\n\nThưởng tài khoản cuối Run: 40 Credits + 100 XP mỗi Level hoàn thành, tối đa 5 Level; máy chủ xác nhận theo thời gian tham gia. Bản test/cheat không có thưởng.";
            _journalText.text+=$"\n\nNâng cấp của bạn: tốc độ {_run.PowerRank(local,0)}/3 • máu +{SalvageRunState.FlatPowerBonus(SalvagePower.Health,_run.PowerRank(local,1)):0}\nThể lực +{SalvageRunState.FlatPowerBonus(SalvagePower.Stamina,_run.PowerRank(local,2)):0} • hồi thể lực {_run.PowerRank(local,3)}/3 • sức nâng {_run.PowerRank(local,4)}/3";
            var account=EchoProtocol.Networking.Authority.MatchAuthorityRuntime.Instance;
            if(account!=null && (_run.Phase==SalvagePhase.Ended||_run.Phase==SalvagePhase.Failed))
            {
                var reward=account.LastLocalReward;
                _journalText.text+=reward!=null?$"\nMáy chủ đã cấp: +{reward.currencyAmount} Credits, +{reward.experiencePointsAwarded} XP.":"\nThưởng tài khoản: đang chờ máy chủ xác nhận; chưa cộng trên máy.";
                _journalText.text+="\n"+account.ResultSaveStatus;
            }
            int seconds = Mathf.CeilToInt(_run.SecondsRemaining);
            _countdownRoot.gameObject.SetActive(_run.Phase == SalvagePhase.InDay);
            _countdown.text = $"{seconds/60:00}:{seconds%60:00}";
            if (_lastPhase != _run.PhaseCode)
            {
                _lastPhase = _run.PhaseCode;
                CloseTerminal();
            }
            int votes = 0; for (int i = 0; i < 4; i++) if (_run.Crew[i].EndVote != 0) votes |= 1 << i;
            if (_terminal.gameObject.activeSelf && (_lastRevision != _run.Revision || _lastVotes != votes)) RefreshTerminal();
            if (_feedback != null && Time.unscaledTime > _feedbackUntil) _feedback.text = "";
        }
        public void OpenJournal()
        {
            _run = SalvageRunState.Instance;
            if (_run == null || _run.Object == null || !_run.Object.IsValid) return;
            var player = _run.Player(_run.Seat(_run.Runner.LocalPlayer));
            if (player == null || PlayerInteractionControlLock.IsGameplayInputBlocked(player.gameObject)) return;
            _journal.gameObject.SetActive(true);
            _inputLock.Acquire(player.gameObject, CloseTerminal);
        }
        public void OpenTerminal(bool departure)
        {
            _run = SalvageRunState.Instance;
            if (_run == null || _run.Object == null || !_run.Object.IsValid) return;
            var player = _run.Player(_run.Seat(_run.Runner.LocalPlayer)); if (player == null) return;
            if (!_run.Site.Contains(player.transform.position)) return;
            _journal.gameObject.SetActive(false); _terminal.gameObject.SetActive(true); _inputLock.Acquire(player.gameObject, CloseTerminal);
            _tab = 0; _page = 0; RefreshTerminal();
        }
        public void CloseTerminal() { if (_terminal != null) _terminal.gameObject.SetActive(false); if (_journal != null) _journal.gameObject.SetActive(false); _inputLock?.Release(); }
        private void Feedback(string message)
        { if (_feedback != null) { _feedback.text = message; _feedbackUntil = Time.unscaledTime + 5; } }
        private void DepartOrReturn()
        {
            if (_run == null) return;
            if (_run.Phase == SalvagePhase.Failed || _run.Phase == SalvagePhase.Ended)
            { CloseTerminal(); _ = NetworkBootstrap.Instance.ShutdownRunnerAsync(); return; }
            _run.Request(_run.Phase == SalvagePhase.InDay ? SalvageCommand.Return : SalvageCommand.Depart);
        }
        private void ConfirmSellAll()
        {
            if (_run == null) return;
            int total = 0, count = 0;
            for (int i = 0; i < _run.Items.Length; i++) if (_run.IsSaleEligible(i))
            { total += _run.Cargo(i).Value; count++; }
            if (count == 0 || total <= 0) { Feedback("Kho chưa có hàng để bán."); return; }
            int revision = _run.Revision;
            EchoProtocol.UI.GameUIFeedback.Instance.Confirm("BÁN TẤT CẢ HÀNG?",
                () => $"Bán {count} món tại căn cứ với tổng giá trị {total} tiền.\nTiền bán được tính vào mục tiêu trước; chỉ phần vượt mục tiêu cộng vào tiền dư.",
                () => _run?.Request(SalvageCommand.SellAll, revision, total), "BÁN " + total + " tiền");
        }
        private void RefreshTerminal()
        {
            if (_run == null || _run.Object == null || !_run.Object.IsValid || _rows == null) return;
            _lastRevision = _run.Revision;
            _previous.gameObject.SetActive(_tab == 0);
            _next.gameObject.SetActive(_tab == 0);
            _sellAll.gameObject.SetActive(_tab == 0);
            for(int i=0;i<_tabs.Count;i++) _tabs[i].targetGraphic.color=i==_tab?new Color(.18f,.22f,.19f,1):new Color(.035f,.045f,.045f,.96f);
            foreach (var row in _rowObjects) Destroy(row); _rowObjects.Clear();
            _title.text = "SALVAGE • " + (_run.Phase switch
            {
                SalvagePhase.InDay => "ĐANG THU HỒI",
                SalvagePhase.DaySummary => "TỔNG KẾT NGÀY",
                SalvagePhase.CycleSummary => "HOÀN THÀNH LEVEL",
                SalvagePhase.Failed => "CHƯA ĐẠT MỤC TIÊU",
                SalvagePhase.Ended => "KẾT THÚC CHUYẾN",
                _ => "CĂN CỨ"
            });
            _summary.text = $"Tiền dư: {_run.Funds}  •  " + (_tab == 2 ? "Nâng cấp chỉ áp dụng cho bạn; tiền trừ từ tiền dư của đội." : _tab == 1 ? "Chọn một trang bị để mang theo." : "Hàng đã đưa về căn cứ có thể bán tại đây.");
            var text = _departure.GetComponentInChildren<Text>();
            bool ended = _run.Phase == SalvagePhase.Ended || _run.Phase == SalvagePhase.Failed;
            int localSeat = _run.Seat(_run.Runner.LocalPlayer);
            bool voted = localSeat >= 0 && _run.Crew[localSeat].EndVote != 0;
            _lastVotes = 0; for (int i = 0; i < 4; i++) if (_run.Crew[i].EndVote != 0) _lastVotes |= 1 << i;
            _endRun.interactable = !ended && _run.Phase != SalvagePhase.InDay && !voted;
            _endRun.GetComponentInChildren<Text>().text = voted ? "ĐÃ ĐỒNG Ý • CHỜ ĐỘI" : "ĐỒNG Ý KẾT THÚC CHUYẾN";
            bool stored = false; for (int i = 0; i < _run.Items.Length; i++) stored |= _run.IsSaleEligible(i);
            _sellAll.interactable = _tab == 0 && stored && !ended;
            text.text = ended ? "VỀ MENU" : _run.Phase == SalvagePhase.InDay ? "TRỞ VỀ CĂN CỨ" : "KHỞI HÀNH";
            _departure.interactable = ended || _run.Phase == SalvagePhase.InDay || _run.Runner.IsServer;
            if (_tab == 2)
            {
                int bought=_run.PowerPurchasesToday(localSeat);
                _pageLabel.text=$"Đã mua {bought}/{SalvageRunState.PowerDailyLimit} lần hôm nay";
                for(int i=0;i<SalvageRunState.PowerNames.Length;i++)
                {
                    int power=i,rank=_run.PowerRank(localSeat,i),price=SalvageRunState.PowerPrice(i,rank);
                    bool unlimited=SalvageRunState.UnlimitedPower(i), available=SalvageRunState.CanUpgradePower(i,rank);
                    var buyer=_run.Player(localSeat);
                    float current=i==1&&buyer!=null?buyer.GetComponent<NetworkPlayerLifeState>().MaximumHealth:i==2&&buyer!=null?buyer.GetComponent<NetworkPlayerMovement>().MaxStamina:0;
                    string progress=unlimited?$"{current:0} → {current+(i==1?20:10):0}":$"Bậc {rank}/3";
                    string benefit=i==1?"+20 máu tối đa / lần":i==2?"+10 thể lực tối đa / lần":$"+{SalvageRunState.PowerSteps[i]*100:0}% mỗi bậc";
                    AddRow(i,$"{SalvageRunState.PowerNames[i]} • {progress} • {benefit}",!available?"TỐI ĐA":$"MUA {price}",()=>
                    {
                        EchoProtocol.UI.GameUIFeedback.Instance.Confirm("NÂNG CẤP CÁ NHÂN?",()=> $"{SalvageRunState.PowerNames[power]}: {benefit}. Đã mua {rank} lần.\nTrừ {price} tiền dư. Chỉ bạn nhận nâng cấp này.",()=>_run.Request(SalvageCommand.BuyPower,power,rank),"MUA");
                    },!ended&&_run.Phase!=SalvagePhase.InDay&&available&&bought<SalvageRunState.PowerDailyLimit&&_run.Funds>=price);
                }
                return;
            }
            if (_tab == 1)
            {
                int[] tools = { 1,2,3,4,5,6 }; string[] names = { "Máy quét", "Thiết bị tạo tiếng động • 3 lần", "Cứu thương đỏ • hồi 50 máu", "Thiết bị chặn cửa • 3 lần", "Cứu thương xanh • hồi 100 máu", "Xe hàng • xuất hiện cạnh máy" };
                for (int i = 0; i < tools.Length; i++)
                {
                    int tool = tools[i]; int stock=_run.EquipmentRemaining(tool);
                    AddRow(i, $"{names[i]} • còn {stock}   —   {SalvageLedger.EquipmentPrice(tool)} tiền dư", "MUA", () => _run.Request(SalvageCommand.Buy,tool), _run.Phase != SalvagePhase.InDay && !ended && _run.Funds >= SalvageLedger.EquipmentPrice(tool) && localSeat >= 0 && stock>0 && (tool==6 || _run.Crew[localSeat].Tool == 0));
                }
                _pageLabel.text = "Trang bị mang theo"; return;
            }
            var indices = new List<int>();
            for (int i = 0; i < _run.Items.Length; i++) if (_run.IsSaleEligible(i)) indices.Add(i);
            if(indices.Count==0) { var empty=Label(_rows,"Empty",new Vector2(12,-40),new Vector2(780,90),17); empty.text="Chưa có hàng để bán.\nĐưa vật phẩm về phòng căn cứ, sau đó mở mục này."; _rowObjects.Add(empty.gameObject); }
            int pages = Mathf.Max(1,(indices.Count+6)/7); _page = Mathf.Clamp(_page,0,pages-1); _pageLabel.text = $"Căn cứ {indices.Count} món • {_page+1}/{pages}";
            for (int row = 0; row < 7 && _page*7+row < indices.Count; row++)
            {
                int index = indices[_page*7+row]; var cargo = _run.Cargo(index); var type = SalvageLedger.Types[cargo.Definition];
                AddRow(row, $"#{cargo.Serial} {type.Name} • {cargo.Value} tiền • {cargo.Integrity}% • {type.Weight:0.#}kg", "BÁN",
                    () =>
                    {
                        EchoProtocol.UI.GameUIFeedback.Instance.Confirm("BÁN HÀNG?",
                            () => $"{type.Name}\nGiá trị {cargo.Value} tiền • Tiền bán được tính vào mục tiêu trước; chỉ phần vượt mục tiêu cộng vào tiền dư.",
                            () => _run?.Request(SalvageCommand.Sell,index,cargo.Serial), "BÁN " + cargo.Value + " tiền");
                    }, !ended);
            }
        }
        private void AddRow(int row, string description, string action, UnityEngine.Events.UnityAction click, bool enabled)
        {
            var root = Rect("CargoRow",_rows,new Vector2(0,1),new Vector2(0,-row*48),new Vector2(812,44)); _rowObjects.Add(root.gameObject);
            Label(root,"Item",new Vector2(6,-8),new Vector2(640,36),14).text=description;
            Button(root,action,652,0,154,click).interactable=enabled;
        }
        private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        { var rect = Rect(name,parent,anchor,position,size); var image=rect.gameObject.AddComponent<Image>();
          image.color=new Color(.035f,.045f,.045f,.96f); var outline=rect.gameObject.AddComponent<Outline>(); outline.effectColor=HUDPresentationStyle.Track; return rect; }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        { var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); go.layer=parent.gameObject.layer;
          var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=rect.pivot=anchor; rect.anchoredPosition=position; rect.sizeDelta=size; return rect; }
        private Text Label(Transform parent,string name,Vector2 position,Vector2 size,int fontSize)
        { var rect=Rect(name,parent,new Vector2(0,1),position,size); var text=rect.gameObject.AddComponent<Text>();
          text.font=_font; text.fontSize=fontSize; text.color=HUDPresentationStyle.Ink; text.raycastTarget=false; text.supportRichText=true; text.verticalOverflow=VerticalWrapMode.Overflow; return text; }
        private Button Button(Transform parent,string label,float x,float y,float width,UnityEngine.Events.UnityAction action)
        {
            var rect=Panel(label,parent,new Vector2(0,1),new Vector2(x,-y),new Vector2(width,36));
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>(); button.onClick.AddListener(action);
            var text=Label(rect,"Label",new Vector2(4,-4),new Vector2(width-8,28),13); text.text=label; text.alignment=TextAnchor.MiddleCenter;
            return button;
        }
    }
}


