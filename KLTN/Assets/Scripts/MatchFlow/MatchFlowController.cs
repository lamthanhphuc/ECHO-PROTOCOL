using System;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.Events;

public class MatchFlowController : MonoBehaviour
{
    [Header("Objective References")]
    [SerializeField] private EnergyCoreObjectiveProgress coreProgress;
    [SerializeField] private PowerPuzzleController powerPuzzle;
    [SerializeField] private SecurityTerminalDownload securityTerminal;
    [SerializeField] private EscapeDoorCountdown escapeDoor;
    [SerializeField] private GameObject zone1DoorToZone2Blocker;

    [Header("Player State")]
    [SerializeField] private bool loseWhenAllPlayersEliminated = true;

    [Header("Events")]
    [SerializeField] private UnityEvent phaseChanged;
    [SerializeField] private UnityEvent finalHuntStarted;
    [SerializeField] private UnityEvent matchWon;
    [SerializeField] private UnityEvent matchLost;

    private PlayerDownState[] _players;
    private MatchPhase _phase = MatchPhase.ExploreCore;
    private bool _networkAuthorityPresentationOnly;
    private string _offlineAuthCode;
    private string _powerAuthorizationCode = string.Empty;
    private bool _securityHoldCompleted;
    private bool _powerPuzzleCompleted;
    private bool _restoreMainPowerCompleted;

    public event Action<MatchPhase> PhaseChanged;
    public event Action MatchWon;
    public event Action MatchLost;

    public MatchPhase Phase => _phase;
    public bool IsMatchEnded => _phase == MatchPhase.Win || _phase == MatchPhase.Lose;
    public string PowerAuthorizationCode
    {
        get
        {
            if (string.IsNullOrEmpty(_powerAuthorizationCode) && _securityHoldCompleted)
            {
                var zone2Director = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance;
                if (zone2Director != null && !string.IsNullOrEmpty(zone2Director.AuthorizationCode))
                {
                    _powerAuthorizationCode = zone2Director.AuthorizationCode;
                }
                else
                {
                    _powerAuthorizationCode = GetOrGenerateAuthCode();
                }
            }
            return _powerAuthorizationCode;
        }
    }
    public bool IsSecurityHoldComplete => _securityHoldCompleted;
    public bool IsPowerPuzzleComplete => _powerPuzzleCompleted;
    public bool IsRestoreMainPowerComplete => _restoreMainPowerCompleted;

    private string GetOrGenerateAuthCode()
    {
        if (string.IsNullOrEmpty(_offlineAuthCode))
        {
            _offlineAuthCode = UnityEngine.Random.Range(0, 10000).ToString("D4");
        }
        return _offlineAuthCode;
    }

    private void Awake()
    {
        ResolveReferences();
        RefreshPlayers();
    }

    private void OnEnable()
    {
        if (_networkAuthorityPresentationOnly) return;
        SubscribeObjectives();
        SubscribePlayers();
    }

    private void OnDisable()
    {
        UnsubscribeObjectives();
        UnsubscribePlayers();
    }

    public void RefreshPlayers()
    {
        if (_networkAuthorityPresentationOnly) return;

        UnsubscribePlayers();
        _players = FindObjectsByType<PlayerDownState>(FindObjectsInactive.Include);
        SubscribePlayers();
    }

    public void NotifyCoreObjectiveComplete()
    {
        if (_networkAuthorityPresentationOnly) return;
        SetZone1DoorToZone2Unlocked(true);
        if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null)
        {
            EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.SetOfflineStage(EchoProtocol.MatchFlow.Zone2MissionStage.FindSecurityTerminal);
        }
        SetPhase(MatchPhase.SecurityHold);
    }

    public void NotifySecurityHoldComplete()
    {
        if (_networkAuthorityPresentationOnly) return;

        _securityHoldCompleted = true;

        var zone2Director = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance;
        if (zone2Director != null &&
            !string.IsNullOrEmpty(zone2Director.AuthorizationCode))
        {
            _powerAuthorizationCode = zone2Director.AuthorizationCode;
        }
        else
        {
            _powerAuthorizationCode = GetOrGenerateAuthCode();
        }

        if (zone2Director != null)
        {
            zone2Director.SetOfflineStage(
                EchoProtocol.MatchFlow.Zone2MissionStage.AuthorizationCodeGranted);
        }

        SetPhase(MatchPhase.PowerPuzzle);
    }

    public void NotifyPowerPuzzleComplete()
    {
        if (_networkAuthorityPresentationOnly) return;

        bool enteringFinalHunt =
            _phase == MatchPhase.PowerPuzzle
            && !IsMatchEnded;

        _powerPuzzleCompleted = true;
        _restoreMainPowerCompleted = true;

        if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null)
        {
            EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.ApplyDoorState(true);
            EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.SetOfflineStage(
                EchoProtocol.MatchFlow.Zone2MissionStage.Zone2Completed);
        }

        if (enteringFinalHunt)
        {
            SetPhase(MatchPhase.FinalHunt);

            if (_phase == MatchPhase.FinalHunt)
            {
                finalHuntStarted?.Invoke();
            }
        }
    }

    public bool VerifyPowerCode(string code)
    {
        if (!_securityHoldCompleted || _phase != MatchPhase.PowerPuzzle)
        {
            return false;
        }
        string expectedCode = PowerAuthorizationCode;
        return !string.IsNullOrEmpty(code) && code == expectedCode;
    }

    public void StartExitCountdown()
    {
        if (_networkAuthorityPresentationOnly) return;
        SetPhase(MatchPhase.ExitCountdown);
    }

    public void WinMatch()
    {
        if (_networkAuthorityPresentationOnly) return;

        if (IsMatchEnded)
        {
            return;
        }

        SetPhase(MatchPhase.Win);
        matchWon?.Invoke();
        MatchWon?.Invoke();
    }

    public void LoseMatch()
    {
        if (_networkAuthorityPresentationOnly) return;

        if (IsMatchEnded)
        {
            return;
        }

        SetPhase(MatchPhase.Lose);
        matchLost?.Invoke();
        MatchLost?.Invoke();
    }

    private void ResolveReferences()
    {
        if (coreProgress == null)
        {
            coreProgress = FindAnyObjectByType<EnergyCoreObjectiveProgress>();
        }

        if (powerPuzzle == null)
        {
            powerPuzzle = FindAnyObjectByType<PowerPuzzleController>();
        }

        if (securityTerminal == null)
        {
            securityTerminal = FindAnyObjectByType<SecurityTerminalDownload>();
        }

        if (escapeDoor == null)
        {
            escapeDoor = FindAnyObjectByType<EscapeDoorCountdown>();
        }

        ResolveZone1DoorToZone2Blocker();
    }

    private void ResolveZone1DoorToZone2Blocker()
    {
        if (zone1DoorToZone2Blocker != null)
        {
            return;
        }

        GameObject doorToZone2 = GameObject.Find("DoorToZone2");
        if (doorToZone2 == null)
        {
            return;
        }

        Transform[] children = doorToZone2.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == "LP_Bay_Door_snaps")
            {
                zone1DoorToZone2Blocker = children[i].gameObject;
                return;
            }
        }
    }

    private void SetZone1DoorToZone2Unlocked(bool unlocked)
    {
        ResolveZone1DoorToZone2Blocker();
        if (zone1DoorToZone2Blocker == null)
        {
            return;
        }

        if (zone1DoorToZone2Blocker.activeSelf == unlocked)
        {
            zone1DoorToZone2Blocker.SetActive(!unlocked);
        }

        var colliders = zone1DoorToZone2Blocker.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = !unlocked;
        }

        // The bay-door prefab puts its passage-wide collider on the wall sibling.
        Transform wall = zone1DoorToZone2Blocker.transform.parent?.Find("LP_Bay_Door_Wall_snaps");
        if (wall != null)
        {
            BoxCollider passageCollider = null;
            var wallColliders = wall.GetComponents<BoxCollider>();
            for (int i = 0; i < wallColliders.Length; i++)
            {
                if (passageCollider == null || wallColliders[i].size.z > passageCollider.size.z)
                {
                    passageCollider = wallColliders[i];
                }
            }

            if (passageCollider != null)
            {
                passageCollider.enabled = !unlocked;
            }
        }

        var obstacles = zone1DoorToZone2Blocker.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
        for (int i = 0; i < obstacles.Length; i++)
        {
            obstacles[i].enabled = !unlocked;
        }
    }

    private void SubscribeObjectives()
    {
        if (coreProgress != null)
        {
            coreProgress.ObjectiveCompleted += NotifyCoreObjectiveComplete;
        }

        if (powerPuzzle != null)
        {
            powerPuzzle.PuzzleCompleted += OnPowerPuzzleCompleted;
        }

        if (securityTerminal != null)
        {
            securityTerminal.DownloadCompleted += OnSecurityDownloadCompleted;
        }

        if (escapeDoor != null)
        {
            escapeDoor.CountdownStarted += OnEscapeCountdownStarted;
            escapeDoor.CountdownCompleted += OnEscapeCountdownCompleted;
        }
    }

    private void UnsubscribeObjectives()
    {
        if (coreProgress != null)
        {
            coreProgress.ObjectiveCompleted -= NotifyCoreObjectiveComplete;
        }

        if (powerPuzzle != null)
        {
            powerPuzzle.PuzzleCompleted -= OnPowerPuzzleCompleted;
        }

        if (securityTerminal != null)
        {
            securityTerminal.DownloadCompleted -= OnSecurityDownloadCompleted;
        }

        if (escapeDoor != null)
        {
            escapeDoor.CountdownStarted -= OnEscapeCountdownStarted;
            escapeDoor.CountdownCompleted -= OnEscapeCountdownCompleted;
        }
    }

    private void SubscribePlayers()
    {
        if (_players == null)
        {
            return;
        }

        foreach (PlayerDownState player in _players)
        {
            if (player != null)
            {
                player.StateChanged += OnPlayerLifeStateChanged;
            }
        }
    }

    private void UnsubscribePlayers()
    {
        if (_players == null)
        {
            return;
        }

        foreach (PlayerDownState player in _players)
        {
            if (player != null)
            {
                player.StateChanged -= OnPlayerLifeStateChanged;
            }
        }
    }

    private void SetPhase(MatchPhase nextPhase)
    {
        if (_phase == nextPhase || IsMatchEnded)
        {
            return;
        }

        _phase = nextPhase;
        phaseChanged?.Invoke();
        PhaseChanged?.Invoke(_phase);
    }

    private void OnPowerPuzzleCompleted(PowerPuzzleController puzzle)
    {
        NotifyPowerPuzzleComplete();
    }

    private void OnSecurityDownloadCompleted(SecurityTerminalDownload terminal)
    {
        NotifySecurityHoldComplete();
    }

    private void OnEscapeCountdownStarted(EscapeDoorCountdown door)
    {
        StartExitCountdown();
    }

    private void OnEscapeCountdownCompleted(EscapeDoorCountdown door)
    {
        WinMatch();
    }

    private void OnPlayerLifeStateChanged(PlayerDownState player, PlayerLifeState state)
    {
        if (loseWhenAllPlayersEliminated && AreAllPlayersEliminated())
        {
            LoseMatch();
        }
    }

    private bool AreAllPlayersEliminated()
    {
        if (_players == null || _players.Length == 0)
        {
            return false;
        }

        foreach (PlayerDownState player in _players)
        {
            if (player != null && !player.IsEliminated)
            {
                return false;
            }
        }

        return true;
    }

    public void SetNetworkAuthorityPresentationOnly(bool enabled)
    {
        if (_networkAuthorityPresentationOnly == enabled) return;
        _networkAuthorityPresentationOnly = enabled;
        if (enabled)
        {
            UnsubscribeObjectives();
            UnsubscribePlayers();
        }
    }

    public void ApplyAuthoritativeSnapshot(
        NetworkMatchPhase networkPhase,
        NetworkMatchStatus networkStatus,
        NetworkMatchResult networkResult)
    {
        ApplyAuthoritativeSnapshot(networkPhase, networkStatus, networkResult, string.Empty, false, false, false);
    }

    public void ApplyAuthoritativeSnapshot(
        NetworkMatchPhase networkPhase,
        NetworkMatchStatus networkStatus,
        NetworkMatchResult networkResult,
        string powerAuthorizationCode,
        bool securityHoldCompleted,
        bool powerPuzzleCompleted,
        bool restoreMainPowerCompleted)
    {
        if (!string.IsNullOrEmpty(powerAuthorizationCode))
        {
            _powerAuthorizationCode = powerAuthorizationCode;
        }
        _securityHoldCompleted = securityHoldCompleted;
        _powerPuzzleCompleted = powerPuzzleCompleted;
        _restoreMainPowerCompleted = restoreMainPowerCompleted;

        var nextPhase = networkStatus == NetworkMatchStatus.Ended
            ? networkResult == NetworkMatchResult.Win ? MatchPhase.Win : MatchPhase.Lose
            : networkPhase switch
            {
                NetworkMatchPhase.CoreObjective => MatchPhase.ExploreCore,
                NetworkMatchPhase.Zone2Objective => MatchPhase.SecurityHold,
                NetworkMatchPhase.SecurityHold => MatchPhase.SecurityHold,
                NetworkMatchPhase.Puzzle => MatchPhase.PowerPuzzle,
                NetworkMatchPhase.FinalHunt => MatchPhase.FinalHunt,
                NetworkMatchPhase.Escape => MatchPhase.ExitCountdown,
                _ => _phase,
            };

        SetZone1DoorToZone2Unlocked(networkStatus == NetworkMatchStatus.Ended
            || networkPhase != NetworkMatchPhase.CoreObjective);

        bool zone2DoorsUnlocked = networkStatus == NetworkMatchStatus.Ended
            || powerPuzzleCompleted
            || restoreMainPowerCompleted
            || (networkPhase != NetworkMatchPhase.CoreObjective
                && networkPhase != NetworkMatchPhase.Zone2Objective
                && networkPhase != NetworkMatchPhase.SecurityHold
                && networkPhase != NetworkMatchPhase.Puzzle);
        if (zone2DoorsUnlocked && EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null)
        {
            EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.ApplyDoorState(true);
        }

        if (_phase == nextPhase) return;
        _phase = nextPhase;
        phaseChanged?.Invoke();
        PhaseChanged?.Invoke(_phase);
        if (_phase == MatchPhase.FinalHunt) finalHuntStarted?.Invoke();
        if (_phase == MatchPhase.Win)
        {
            matchWon?.Invoke();
            MatchWon?.Invoke();
        }
        else if (_phase == MatchPhase.Lose)
        {
            matchLost?.Invoke();
            MatchLost?.Invoke();
        }
    }
}
