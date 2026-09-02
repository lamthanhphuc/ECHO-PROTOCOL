using System;
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
        UnsubscribePlayers();
        _players = FindObjectsByType<PlayerDownState>(FindObjectsInactive.Include);
        SubscribePlayers();
    }

    public void NotifyCoreObjectiveComplete()
    {
        SetPhase(MatchPhase.PowerPuzzle);
    }

    public void NotifyPowerPuzzleComplete()
    {
        SetPhase(MatchPhase.SecurityHold);
    }

    public void NotifySecurityHoldComplete()
    {
        SetPhase(MatchPhase.FinalHunt);
        finalHuntStarted?.Invoke();
    }

    public void StartExitCountdown()
    {
        SetPhase(MatchPhase.ExitCountdown);
    }

    public void WinMatch()
    {
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
}
