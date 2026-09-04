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

    public event Action<MatchPhase> PhaseChanged;
    public event Action MatchWon;
    public event Action MatchLost;

    public MatchPhase Phase => _phase;
    public bool IsMatchEnded => _phase == MatchPhase.Win || _phase == MatchPhase.Lose;

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
        SetPhase(MatchPhase.PowerPuzzle);
    }

    public void NotifyPowerPuzzleComplete()
    {
        if (_networkAuthorityPresentationOnly) return;
        SetPhase(MatchPhase.SecurityHold);
    }

    public void NotifySecurityHoldComplete()
    {
        if (_networkAuthorityPresentationOnly) return;
        SetPhase(MatchPhase.FinalHunt);
        finalHuntStarted?.Invoke();
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
        var nextPhase = networkStatus == NetworkMatchStatus.Ended
            ? networkResult == NetworkMatchResult.Win ? MatchPhase.Win : MatchPhase.Lose
            : networkPhase switch
            {
                NetworkMatchPhase.CoreObjective => MatchPhase.ExploreCore,
                NetworkMatchPhase.Puzzle => MatchPhase.PowerPuzzle,
                NetworkMatchPhase.SecurityHold => MatchPhase.SecurityHold,
                NetworkMatchPhase.FinalHunt => MatchPhase.FinalHunt,
                NetworkMatchPhase.Escape => MatchPhase.ExitCountdown,
                _ => _phase,
            };

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
