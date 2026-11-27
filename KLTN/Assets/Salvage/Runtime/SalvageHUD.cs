using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.UI.HUD;
using EchoProtocol.Settings;
using static EchoProtocol.Settings.GameLanguage;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EchoProtocol.Salvage
{
    // Adds only Salvage information and terminal panels to the existing ECHO HUD canvas.
    public sealed class SalvageHUD : MonoBehaviour
    {
        public static SalvageHUD Instance { get; private set; }
        private Text _mission, _journalText, _resultText;
        private RectTransform _result, _aim, _recovery;
        private Text _recoveryText;
        private Button _freshRecovery;
        private Image _aimDot;
        private RectTransform _missionRoot, _journal;
        private Text _countdown, _title, _summary, _feedback, _pageLabel;
        private RectTransform _terminal, _rows, _countdownRoot;
        private Button _departure, _sellAll, _endRun, _previous, _next;
        private Font _font;
        private PlayerInteractionControlLock _inputLock;
        private SalvageRunState _run;
        private int _tab, _page, _lastRevision = -1, _lastPhase = -1, _lastVotes = -1;
        private float _refreshAt, _feedbackUntil;
        private readonly List<(Text Label, string Vietnamese, string English)> _localizedLabels = new();
        private string _feedbackMessage;
        private readonly List<Button> _tabs = new();
        private readonly List<GameObject> _rowObjects = new();

        private void Awake()
        {
            Instance = this;
            gameObject.AddComponent<SalvagePhysicsInput>();
            var source = GetComponentInChildren<Text>(true);
            _font = source != null ? source.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _inputLock = new PlayerInteractionControlLock();
            _recovery=Panel("SalvageRecovery",transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(650,330));_recovery.pivot=Vector2.one*.5f;
            BindLocalizedLabel(Label(_recovery,"Title",new Vector2(24,-18),new Vector2(600,40),22), "KHÔI PHỤC CHUYẾN THU HỒI", "RECOVER SALVAGE RUN");
            _recoveryText=Label(_recovery,"Status",new Vector2(24,-72),new Vector2(600,105),17);
            LocalizedButton(_recovery, "THỬ KHÔI PHỤC LẠI", "RETRY RECOVERY",24,210,280,()=>SalvageRunState.Instance?.InitializeSavedRun());
            LocalizedButton(_recovery, "VỀ PHÒNG CHỜ", "RETURN TO LOBBY",346,210,280,()=>{_ = NetworkBootstrap.Instance.ShutdownRunnerAsync();});
            _freshRecovery=LocalizedButton(_recovery, "GIỮ BẢN LƯU CŨ · TẠO CHUYẾN MỚI", "KEEP OLD SAVE · START NEW RUN",24,268,602,()=>SalvageRunState.Instance?.StartFreshCloudRunConfirmed());
            _recovery.gameObject.SetActive(false);

            _aim=Rect("SalvageAim",transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(2,2));
            _aimDot=_aim.gameObject.AddComponent<Image>();_aimDot.color=HUDPresentationStyle.Ink;_aimDot.raycastTarget=false;
            foreach(var offset in new[]{new Vector2(-7,0),new Vector2(7,0),new Vector2(0,-7),new Vector2(0,7)})
            {
                var tick=Rect("Tick",_aim,new Vector2(.5f,.5f),offset,offset.x!=0?new Vector2(4,1):new Vector2(1,4));
                var graphic=tick.gameObject.AddComponent<Image>();graphic.color=new Color(.85f,.85f,.8f,.6f);graphic.raycastTarget=false;
            }
            _aim.gameObject.SetActive(false);
            _countdownRoot = Rect("SalvageCountdown", transform, new Vector2(.5f,1), new Vector2(0,-24), new Vector2(128,40));
            _countdown = Label(_countdownRoot, "Time", Vector2.zero, new Vector2(128,40), 26);
            _countdown.alignment = TextAnchor.MiddleCenter;
            _countdownRoot.gameObject.SetActive(false);
            _missionRoot = Panel("SalvageMission", transform, new Vector2(0,1), new Vector2(24,-24), new Vector2(360,112));
            _mission = Label(_missionRoot,"Mission",new Vector2(14,-10),new Vector2(332,96),16);
            _journal = Panel("SalvageJournal",transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(650,420));
            _journal.pivot=new Vector2(.5f,.5f);
            BindLocalizedLabel(Label(_journal,"Title",new Vector2(24,-18),new Vector2(600,40),22), "NHIỆM VỤ THU HỒI", "SALVAGE OBJECTIVES");
            _journalText=Label(_journal,"Details",new Vector2(24,-70),new Vector2(600,272),17);
            LocalizedButton(_journal, "ĐÓNG [J]", "CLOSE [J]",466,360,160,CloseTerminal);_journal.gameObject.SetActive(false);
            _result=Panel("SalvageResult",transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(650,330));
            _result.pivot=new Vector2(.5f,.5f);
            BindLocalizedLabel(Label(_result,"Title",new Vector2(24,-20),new Vector2(600,40),22), "KẾT QUẢ CHUYẾN THU HỒI", "SALVAGE RUN RESULTS");
            _resultText=Label(_result,"Details",new Vector2(24,-80),new Vector2(600,170),18);
            LocalizedButton(_result, "XEM CĂN CỨ", "VIEW BASE",24,270,220,CloseTerminal);
            LocalizedButton(_result, "RỜI CHUYẾN", "LEAVE RUN",406,270,220,DepartOrReturn);_result.gameObject.SetActive(false);
            _terminal = Panel("SalvageTerminal", transform, new Vector2(.5f,.5f), Vector2.zero, new Vector2(860,630));
            _terminal.pivot = new Vector2(.5f,.5f);
            _title = Label(_terminal, "Title", new Vector2(24,-14), new Vector2(700,42), 20);
            _summary = Label(_terminal, "Summary", new Vector2(24,-58), new Vector2(812,32), 15);
            _tabs.Add(LocalizedButton(_terminal, "BÁN HÀNG / KHO", "SELL / STORAGE", 24, 100, 250, () => { _tab=0; _page=0; RefreshTerminal(); }));
            _tabs.Add(LocalizedButton(_terminal, "TRANG BỊ", "EQUIPMENT", 290, 100, 250, () => { _tab=1; _page=0; RefreshTerminal(); }));
            _tabs.Add(LocalizedButton(_terminal, "NÂNG CẤP CÁ NHÂN", "PERSONAL UPGRADES", 556, 100, 278, () => { _tab=2; _page=0; RefreshTerminal(); }));
            _rows = Rect("Rows", _terminal, new Vector2(0,1), new Vector2(24,-154), new Vector2(812,342));
            _previous = Button(_terminal, "‹", 24, 508, 50, () => { _page=Mathf.Max(0,_page-1); RefreshTerminal(); });
            _pageLabel = Label(_terminal, "Page", new Vector2(86,-515), new Vector2(220,24), 14);
            _next = Button(_terminal, "›", 310, 508, 50, () => { _page++; RefreshTerminal(); });
            _sellAll = LocalizedButton(_terminal, "BÁN TẤT CẢ", "SELL ALL", 380, 508, 180, ConfirmSellAll);
            _departure = LocalizedButton(_terminal, "KHỞI HÀNH", "DEPART", 24, 570, 200, DepartOrReturn);
            _endRun = LocalizedButton(_terminal, "BỎ PHIẾU DỪNG CHUYẾN", "VOTE TO END RUN", 238, 570, 276, () => _run?.Request(SalvageCommand.VoteEnd));
            LocalizedButton(_terminal, "ĐÓNG", "CLOSE", 674, 570, 160, CloseTerminal);
            _feedback = Label(_terminal, "Feedback", new Vector2(570,-512), new Vector2(260,45), 13);
            _feedback.color = HUDPresentationStyle.Warning; _terminal.gameObject.SetActive(false);
            GameLanguage.Changed += RefreshLanguage;
        }
        private void OnDestroy()
        {
            GameLanguage.Changed -= RefreshLanguage;
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
            bool recovering=_run.CloudLoading||_run.CloudRecoveryRequired;
            _recovery.gameObject.SetActive(recovering);_freshRecovery.gameObject.SetActive(_run.CanStartFreshCloudRun&&!_run.CloudLoading);
            if(recovering){_recoveryText.text=_run.CloudLoading?Choose("Đang tải bản lưu được xác nhận…", "Loading the verified save…")
                : _run.CheckpointStatus + Choose("\nBản lưu trên máy vẫn được giữ nguyên.", "\nYour local save is still intact.");Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return;}
            int seat = _run.Seat(_run.Runner.LocalPlayer); if (seat < 0) return;
            if ((_terminal.gameObject.activeSelf || _journal.gameObject.activeSelf || _result.gameObject.activeSelf) && _inputLock.ShouldAutoRelease()) { CloseTerminal(); return; }
            var keyboard = Keyboard.current;
            if (_journal.gameObject.activeSelf && (_inputLock.ConsumeEscape() || keyboard != null && keyboard.jKey.wasPressedThisFrame)) { CloseTerminal(); return; }
            if (_result.gameObject.activeSelf && _inputLock.ConsumeEscape()) { CloseTerminal(); return; }
            if (_terminal.gameObject.activeSelf && _inputLock.ConsumeEscape()) { CloseTerminal(); return; }
            if (!_terminal.gameObject.activeSelf && !_journal.gameObject.activeSelf && !_result.gameObject.activeSelf && keyboard != null)
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
            var localPlayer=_run.Player(seat);
            bool aimVisible=!_terminal.gameObject.activeSelf&&!_journal.gameObject.activeSelf&&!_result.gameObject.activeSelf
                &&localPlayer!=null&&localPlayer.GetComponent<NetworkPlayerLifeState>()?.CanInitiateAction==true;
            _aim.gameObject.SetActive(aimVisible);
            _aimDot.color=_run.PhysicsHeldIndex(seat)>=0?new Color(.3f,1,.6f):HUDPresentationStyle.Ink;
            _missionRoot.gameObject.SetActive(!_terminal.gameObject.activeSelf && !_journal.gameObject.activeSelf && !_result.gameObject.activeSelf);
            _mission.text = Choose($"THU HỒI • Cấp {_run.Cycle} • Ngày {_run.Day}/3\nTiền phải kiếm: {_run.SoldThisCycle:N0}/{_run.Quota:N0}\n$ {_run.Funds:N0}  •  [J] Chi tiết",
                $"SALVAGE • Level {_run.Cycle} • Day {_run.Day}/3\nSales target: {_run.SoldThisCycle:N0}/{_run.Quota:N0}\n$ {_run.Funds:N0}  •  [J] Details");
            int local = _run.Seat(_run.Runner.LocalPlayer);
            _journalText.text = Choose(
                $"Cấp {_run.Cycle} — Ngày {_run.Day}/3\nTiền phải kiếm: {_run.SoldThisCycle:N0}/{_run.Quota:N0}\n$ {_run.Funds:N0}\n\nĐưa hàng về căn cứ và bán trước khi hết ngày 3.\nĐạt mục tiêu để sang cấp tiếp theo.\n\nNgày vừa qua: mang về $ {_run.LastDayRecovered:N0} • mất $ {_run.LastDayLost:N0}\nĐang mang: {_run.Weight(local):0.#} kg.",
                $"Level {_run.Cycle} — Day {_run.Day}/3\nSales target: {_run.SoldThisCycle:N0}/{_run.Quota:N0}\n$ {_run.Funds:N0}\n\nBring cargo back to base and sell it before Day 3 ends.\nMeet the sales target to advance to the next level.\n\nLast day: recovered $ {_run.LastDayRecovered:N0} • lost $ {_run.LastDayLost:N0}\nCarrying: {_run.Weight(local):0.#} kg.")
                + "\n" + _run.CheckpointStatus;
            bool finished=_run.Phase==SalvagePhase.Ended||_run.Phase==SalvagePhase.Failed;
            if(finished)
            {
                var account=EchoProtocol.Networking.Authority.MatchAuthorityRuntime.Instance;
                var reward=account!=null?account.LastLocalReward:null;
                _resultText.text=(_run.Phase == SalvagePhase.Failed ? Choose("CHƯA ĐẠT MỤC TIÊU", "SALES TARGET MISSED") : Choose("ĐÃ DỪNG CHUYẾN", "RUN ENDED")) +
                    Choose($"\nCấp {_run.Cycle} • Đã bán $ {_run.TotalSold:N0}\n\n",
                        $"\nLevel {_run.Cycle} • Total sales $ {_run.TotalSold:N0}\n\n") +
                    (reward != null ? Choose($"Đã nhận: +{reward.currencyAmount:N0} tín dụng • +{reward.experiencePointsAwarded:N0} kinh nghiệm",
                        $"Rewards: +{reward.currencyAmount:N0} Credits • +{reward.experiencePointsAwarded:N0} XP") :
                    _run.AccountRewardsDisabled ? Choose("Chuyến này không đủ điều kiện nhận thưởng.", "This run is not eligible for rewards.") :
                    account != null && account.IsResultPermanentlyRejected(account.MatchId.ToString()) ?
                        Choose("Chưa thể lưu kết quả. Vui lòng kiểm tra hồ sơ trận.", "Result could not be saved. Check the match record.") :
                        Choose("Đang lưu kết quả và xác nhận thưởng…", "Saving the result and confirming rewards…"));
            }
            int seconds = Mathf.CeilToInt(_run.SecondsRemaining);
            _countdownRoot.gameObject.SetActive(_run.Phase == SalvagePhase.InDay);
            _countdown.text = $"{seconds/60:00}:{seconds%60:00}";
            if (_lastPhase != _run.PhaseCode)
            {
                _lastPhase = _run.PhaseCode;
                CloseTerminal();
                if(finished) { _result.gameObject.SetActive(true); var player=_run.Player(local); if(player!=null)_inputLock.Acquire(player.gameObject,CloseTerminal); }
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
            if(_run.Phase==SalvagePhase.Failed||_run.Phase==SalvagePhase.Ended)_result.gameObject.SetActive(true);
            else _journal.gameObject.SetActive(true);
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
        public void CloseTerminal() { if (_terminal != null) _terminal.gameObject.SetActive(false); if (_journal != null) _journal.gameObject.SetActive(false); if(_result!=null)_result.gameObject.SetActive(false); _inputLock?.Release(); }
        private void Feedback(string message)
        {
            _feedbackMessage = message;
            _feedback.text = SalvageRunState.FeedbackText(message);
            _feedbackUntil = Time.unscaledTime + 5;
        }

        private void RefreshLanguage()
        {
            foreach (var (label, vietnamese, english) in _localizedLabels)
                label.text = Choose(vietnamese, english);
            _refreshAt = 0;
            if (Time.unscaledTime <= _feedbackUntil)
                _feedback.text = SalvageRunState.FeedbackText(_feedbackMessage);
            if (_terminal.gameObject.activeSelf) RefreshTerminal();
        }
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
            if (count == 0 || total <= 0) { Feedback(SalvageRunState.EmptyStorageFeedback); return; }
            int revision = _run.Revision;
            EchoProtocol.UI.GameUIFeedback.Instance.Confirm(Choose("BÁN TẤT CẢ HÀNG?", "SELL ALL CARGO?"),
                () => Choose($"Bán {count} món tại căn cứ với tổng giá trị $ {total:N0}.\nTiền bán được tính vào mục tiêu trước. Phần dư dùng để mua đồ.",
                    $"Sell {count} items at base for $ {total:N0}.\nSales count toward the target first. Any surplus is available to spend."),
                () => _run?.Request(SalvageCommand.SellAll, revision, total), Choose($"BÁN · $ {total:N0}", $"SELL · $ {total:N0}"));
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
            _title.text = Choose("THU HỒI • ", "SALVAGE • ") + (_run.Phase switch
            {
                SalvagePhase.InDay => Choose("ĐANG THU HỒI", "RECOVERY IN PROGRESS"),
                SalvagePhase.DaySummary => Choose("TỔNG KẾT NGÀY", "DAY SUMMARY"),
                SalvagePhase.CycleSummary => Choose("HOÀN THÀNH CẤP", "LEVEL COMPLETE"),
                SalvagePhase.Failed => Choose("CHƯA ĐẠT MỤC TIÊU", "SALES TARGET MISSED"),
                SalvagePhase.Ended => Choose("KẾT THÚC CHUYẾN", "RUN ENDED"),
                _ => Choose("CĂN CỨ", "BASE")
            });
            int upgradesLeft = Mathf.Max(0, SalvageRunState.PowerDailyLimit - _run.PowerPurchasesToday(_run.Seat(_run.Runner.LocalPlayer)));
            _summary.text = $"$ {_run.Funds:N0}  •  " + (_tab == 2
                ? Choose($"Lượt nâng cấp còn lại hôm nay: {upgradesLeft}", $"Upgrades left today: {upgradesLeft}")
                : _tab == 1 ? Choose("Trang bị & vận chuyển", "Equipment & transport") : Choose("Vật phẩm tại căn cứ", "Cargo at base"));
            var text = _departure.GetComponentInChildren<Text>();
            bool ended = _run.Phase == SalvagePhase.Ended || _run.Phase == SalvagePhase.Failed;
            int localSeat = _run.Seat(_run.Runner.LocalPlayer);
            bool voted = localSeat >= 0 && _run.Crew[localSeat].EndVote != 0;
            _lastVotes = 0; for (int i = 0; i < 4; i++) if (_run.Crew[i].EndVote != 0) _lastVotes |= 1 << i;
            _endRun.interactable = !ended && _run.Phase != SalvagePhase.InDay && !voted;
            _endRun.GetComponentInChildren<Text>().text = voted ? Choose("ĐÃ BỎ PHIẾU • CHỜ ĐỘI", "VOTED • WAITING FOR CREW") : Choose("BỎ PHIẾU DỪNG CHUYẾN", "VOTE TO END RUN");
            bool stored = false; for (int i = 0; i < _run.Items.Length; i++) stored |= _run.IsSaleEligible(i);
            _sellAll.interactable = _tab == 0 && stored && !ended;
            text.text = ended ? Choose("RỜI CHUYẾN", "LEAVE RUN") : _run.Phase == SalvagePhase.InDay ? Choose("KẾT THÚC NGÀY", "END DAY") : Choose("KHỞI HÀNH", "DEPART");
            _departure.interactable = ended || _run.Phase == SalvagePhase.InDay || _run.Runner.IsServer;
            if (_tab == 2)
            {
                int bought=_run.PowerPurchasesToday(localSeat);
                _pageLabel.text="";
                for(int i=0;i<SalvageRunState.PowerNames.Length;i++)
                {
                    int power=i,rank=_run.PowerRank(localSeat,i),price=SalvageRunState.PowerPrice(i,rank);
                    bool unlimited=SalvageRunState.UnlimitedPower(i), available=SalvageRunState.CanUpgradePower(i,rank);
                    var buyer=_run.Player(localSeat);
                    float current=i==1&&buyer!=null?buyer.GetComponent<NetworkPlayerLifeState>().MaximumHealth:i==2&&buyer!=null?buyer.GetComponent<NetworkPlayerMovement>().MaxStamina:0;
                    string progress=unlimited?$"{current:0} → {current+(i==1?20:10):0}":$"+{rank*SalvageRunState.PowerSteps[i]*100:0}% → +{Mathf.Min(3,rank+1)*SalvageRunState.PowerSteps[i]*100:0}%   •   {Choose("Bậc", "Rank")} {rank}/3";
                    string benefit = i == 1 ? Choose("+20 máu tối đa / lần", "+20 max health per purchase")
                        : i == 2 ? Choose("+10 thể lực tối đa / lần", "+10 max stamina per purchase")
                        : Choose($"+{SalvageRunState.PowerSteps[i]*100:0}% mỗi bậc", $"+{SalvageRunState.PowerSteps[i]*100:0}% per rank");
                    string reason = !available ? Choose("ĐÃ TỐI ĐA", "MAX RANK") : ended ? Choose("ĐÃ KẾT THÚC", "RUN ENDED")
                        : _run.Phase == SalvagePhase.InDay ? Choose("VỀ CĂN CỨ", "RETURN TO BASE")
                        : bought >= SalvageRunState.PowerDailyLimit ? Choose("HẾT LƯỢT", "DAILY LIMIT")
                        : PurchaseLabel(price);
                    AddRow(i,$"{SalvageRunState.PowerName(i)}\n{progress}   •   {benefit}",reason,()=>
                    {
                        EchoProtocol.UI.GameUIFeedback.Instance.Confirm(Choose("NÂNG CẤP CÁ NHÂN?", "BUY UPGRADE?"),
                            () => $"{SalvageRunState.PowerName(power)}: {progress} • {benefit}\n$ {price:N0}",
                            () => _run.Request(SalvageCommand.BuyPower,power,rank), Choose("MUA", "BUY"));
                    },!ended&&_run.Phase!=SalvagePhase.InDay&&available&&bought<SalvageRunState.PowerDailyLimit&&_run.Funds>=price);
                }
                return;
            }
            if (_tab == 1)
            {
                int toolUses = LobbyPlayerState.ConfiguredMultiUseTeamToolUses;
                string usesLabel = Choose($"{toolUses} lần sử dụng", $"Uses: {toolUses}");
                string[] details =
                {
                    Choose("Quét tìm vật phẩm", "Scan for cargo"),
                    usesLabel,
                    Choose("Hồi 50 máu", "Restores 50 health"),
                    usesLabel,
                    Choose("Hồi 100 máu", "Restores 100 health"),
                    Choose("Vận chuyển • nhận cạnh máy", "Transport • delivered next to the terminal")
                };
                for (int i = 0; i < details.Length; i++)
                {
                    int tool = i + 1;
                    int stock = _run.EquipmentRemaining(tool);
                    int price = SalvageLedger.EquipmentPrice(tool);
                    bool occupied = localSeat >= 0 && _run.Crew[localSeat].Tool != 0;
                    string reason = ended ? Choose("ĐÃ KẾT THÚC", "RUN ENDED")
                        : _run.Phase == SalvagePhase.InDay ? Choose("VỀ CĂN CỨ", "RETURN TO BASE")
                        : stock <= 0 ? tool == 6 ? Choose("ĐÃ MUA XE", "CART PURCHASED") : Choose("HẾT HÀNG", "SOLD OUT")
                        : localSeat < 0 ? Choose("CHỜ NGƯỜI CHƠI", "WAITING FOR PLAYER")
                        : tool != 6 && occupied ? Choose("Ô ĐÃ ĐẦY", "SLOT FULL") : PurchaseLabel(price);
                    string remaining = tool == 6 ? Choose($"Còn {stock} xe ở cấp này", $"Carts left this level: {stock}")
                        : Choose($"Còn {stock} món hôm nay", $"Stock left today: {stock}");
                    AddRow(i, $"{EquipmentName(tool)}\n{details[i]}   •   {remaining}", reason, () => _run.Request(SalvageCommand.Buy,tool),
                        _run.Phase != SalvagePhase.InDay && !ended && _run.Funds >= price && localSeat >= 0 && stock > 0 && (tool == 6 || !occupied));
                }
                int carried = localSeat < 0 ? 0 : _run.Crew[localSeat].Tool;
                int uses = localSeat < 0 ? 0 : _run.Crew[localSeat].Uses;
                _pageLabel.text = Choose("Đang mang: ", "Equipped: ") + EquipmentName(carried == 3 && uses > 1 ? 5 : carried);
                return;
            }
            var indices = new List<int>();
            for (int i = 0; i < _run.Items.Length; i++) if (_run.IsSaleEligible(i)) indices.Add(i);
            if(indices.Count==0) { var empty=Label(_rows,"Empty",new Vector2(12,-40),new Vector2(780,90),17); empty.text=Choose("Chưa có hàng để bán.\nĐưa vật phẩm về phòng căn cứ, sau đó mở mục này.",
                "No cargo to sell.\nBring items into the base, then reopen this tab."); _rowObjects.Add(empty.gameObject); }
            int pages = Mathf.Max(1,(indices.Count+6)/7); _page = Mathf.Clamp(_page,0,pages-1); _pageLabel.text = Choose($"Căn cứ: {indices.Count} món • {_page+1}/{pages}", $"Items at base: {indices.Count} • {_page+1}/{pages}");
            for (int row = 0; row < 7 && _page*7+row < indices.Count; row++)
            {
                int index = indices[_page*7+row]; var cargo = _run.Cargo(index); var type = SalvageLedger.Types[cargo.Definition];
                string sellLabel = Choose($"BÁN · $ {cargo.Value:N0}", $"SELL · $ {cargo.Value:N0}");
                AddRow(row, $"{type.DisplayName}\n{Choose("Tình trạng", "Condition")} {cargo.Integrity}%   •   {type.Weight:0.#} kg", sellLabel,
                    () =>
                    {
                        EchoProtocol.UI.GameUIFeedback.Instance.Confirm(Choose("BÁN HÀNG?", "SELL ITEM?"),
                            () => $"{type.DisplayName}\n" + Choose($"Giá trị $ {cargo.Value:N0} • Tiền bán được tính vào mục tiêu trước. Phần dư dùng để mua đồ.",
                                $"Value $ {cargo.Value:N0} • Sales count toward the target first. Any surplus is available to spend."),
                            () => _run?.Request(SalvageCommand.Sell,index,cargo.Serial), sellLabel);
                    }, !ended);
            }
        }
        private string PurchaseLabel(int price) => _run.Funds < price
            ? Choose($"THIẾU $ {price-_run.Funds:N0}", $"NEED $ {price-_run.Funds:N0}")
            : Choose($"MUA · $ {price:N0}", $"BUY · $ {price:N0}");

        internal static string EquipmentName(int product) => product switch
        {
            1 => Choose("Máy quét hiện trường", "Field Scanner"),
            2 => Choose("Máy tạo tiếng động", "Noise Maker"),
            3 => Choose("Túi cứu thương", "First Aid Kit"),
            4 => Choose("Thiết bị chặn cửa", "Door Jammer"),
            5 => Choose("Túi cứu thương lớn", "Large First Aid Kit"),
            6 => Choose("Xe hàng", "Transport Cart"),
            _ => Choose("Trống", "Empty")
        };

        private void LateUpdate()
        {
            var canvas=GetComponentInParent<Canvas>(); if(canvas==null)return;
            var size=((RectTransform)canvas.transform).rect.size;
            foreach(var panel in new[]{_terminal,_journal,_result,_recovery})if(panel!=null)
                panel.localScale=Vector3.one*Mathf.Min(1,(size.x-32)/panel.sizeDelta.x,(size.y-32)/panel.sizeDelta.y);
        }
        private void AddRow(int row, string description, string action, UnityEngine.Events.UnityAction click, bool enabled)
        {
            var root = Panel("CargoRow",_rows,new Vector2(0,1),new Vector2(0,-row*48),new Vector2(812,44)); _rowObjects.Add(root.gameObject);
            if(_tab!=0)
            {
                var iconRoot=Rect("Icon",root,new Vector2(0,1),new Vector2(10,-7),new Vector2(28,28));
                var icon=iconRoot.gameObject.AddComponent<HUDPurgeIcon>();icon.raycastTarget=false;icon.color=HUDPresentationStyle.Ink;
                icon.Kind=_tab==1?new[]{PurgeIcon.Scanner,PurgeIcon.NoiseMaker,PurgeIcon.Medkit,PurgeIcon.DoorJammer,PurgeIcon.Medkit,PurgeIcon.Cart}[row]
                    :new[]{PurgeIcon.Stamina,PurgeIcon.Health,PurgeIcon.Stamina,PurgeIcon.Stamina,PurgeIcon.Interact}[row];
            }
            int split=description.IndexOf('\n');
            if(split>=0)description="<b>"+description.Substring(0,split)+"</b>\n<size=12>"+description.Substring(split+1)+"</size>";
            var itemLabel=Label(root,"Item",new Vector2(_tab==0?10:48,0),new Vector2(_tab==0?580:540,44),15);
            itemLabel.text=description;itemLabel.alignment=TextAnchor.MiddleLeft;
            Button(root,action,602,2,204,click).interactable=enabled;
        }
        private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        { var rect = Rect(name,parent,anchor,position,size); var image=rect.gameObject.AddComponent<Image>();
          image.color=new Color(.035f,.045f,.045f,.96f); var outline=rect.gameObject.AddComponent<Outline>(); outline.effectColor=HUDPresentationStyle.Track; return rect; }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        { var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); go.layer=parent.gameObject.layer;
          var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=rect.pivot=anchor; rect.anchoredPosition=position; rect.sizeDelta=size; return rect; }
        private Text Label(Transform parent,string name,Vector2 position,Vector2 size,int fontSize)
        { var rect=Rect(name,parent,new Vector2(0,1),position,size); var text=rect.gameObject.AddComponent<Text>();
          text.font=_font; text.fontSize=fontSize; text.color=HUDPresentationStyle.Ink; text.raycastTarget=false; text.supportRichText=true; text.verticalOverflow=VerticalWrapMode.Truncate; return text; }
        private void BindLocalizedLabel(Text label, string vietnamese, string english)
        {
            _localizedLabels.Add((label, vietnamese, english));
            label.text = Choose(vietnamese, english);
        }

        private Button LocalizedButton(Transform parent, string vietnamese, string english, float x, float y,
            float width, UnityEngine.Events.UnityAction action)
        {
            var button = Button(parent, english, x, y, width, action);
            BindLocalizedLabel(button.GetComponentInChildren<Text>(), vietnamese, english);
            return button;
        }

        private Button Button(Transform parent,string label,float x,float y,float width,UnityEngine.Events.UnityAction action)
        {
            var rect=Panel(label,parent,new Vector2(0,1),new Vector2(x,-y),new Vector2(width,36));
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>(); button.onClick.AddListener(action);
            var text=Label(rect,"Label",new Vector2(4,-4),new Vector2(width-8,28),13); text.text=label; text.alignment=TextAnchor.MiddleCenter;
            return button;
        }
    }
}


