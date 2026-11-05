using System;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using UnityEditor;
using UnityEngine.UIElements;

public sealed class AEDv2DiagnosticsWindow : EditorWindow
{
    private Label _content;

    [MenuItem("Tools/Echo Protocol/AED/Diagnostics")]
    public static void Open() => GetWindow<AEDv2DiagnosticsWindow>("AED Diagnostics");

    public void CreateGUI()
    {
        _content = new Label();
        var scroll = new ScrollView();
        scroll.Add(_content);
        rootVisualElement.Add(scroll);
        rootVisualElement.schedule.Execute(Refresh).Every(500);
        Refresh();
    }

    private void Refresh()
    {
        if (_content == null) return;
        var authority = MatchAuthorityRuntime.Instance;
        var runtime = ScenarioConfigAuthorityRuntime.Instance;
        var settings = runtime?.RuntimeSettings;
        var provider = BackendAdaptiveInputSnapshotProvider.Current;
        var proposal = AEDv2Authority.LastProposal;
        var matchState = NetworkMatchState.Instance;
        var matchId = authority?.MatchId ?? Guid.Empty;
        var applied = AEDv2Authority.TryGetApplied(matchId, out var plan, out var revision);
        var previous = applied ? plan : AEDv2Plan.Normal();
        var key = proposal?.Key;
        var commitStatus = applied ? "Applied" : proposal?.Changed == true
            ? settings?.ExtendedPolicyGameplayEnabled == true
                ? AEDv2Authority.HasBackendPreMatchApproval ? "Backend approved" : "HOLD"
                : "ShadowOnly"
            : "HOLD";
        var planFingerprint = applied ? plan.Fingerprint()
            : matchState != null && matchState.AEDv2PlanRevision > 0
                ? matchState.AEDv2PlanFingerprint.ToString()
                : previous.Fingerprint();
        _content.text =
            $"Backend match ID: {matchId:D}\n" +
            $"Resolution mode: {runtime?.CurrentScenarioResolutionMode.ToString() ?? "Fixed"}\n" +
            $"AED shadow: {settings?.ExtendedPolicyShadowEnabled == true}\n" +
            $"AED gameplay: {settings?.ExtendedPolicyGameplayEnabled == true}\n" +
            $"Provider: {(provider == null ? "Unbound" : "Bound")}\n" +
            $"Snapshot ID: {provider?.SnapshotId.ToString("D") ?? ""}\n" +
            $"Snapshot fingerprint: {provider?.SnapshotFingerprint ?? ""}\n" +
            $"Snapshot validity: {provider?.SnapshotValidity ?? "Unavailable"}\n" +
            $"Team size: {provider?.TeamSize ?? 0}\n" +
            $"Observed Survival mean: {provider?.SurvivalMean?.ToString() ?? "N/A"}\n" +
            $"Observed Noise mean: {provider?.NoiseMean?.ToString() ?? "N/A"}\n" +
            $"Gate: {(proposal == null ? "Not evaluated" : proposal.Eligible ? "Eligible" : "Ineligible")}\n" +
            $"Ineligibility reason: {proposal?.Reason ?? provider?.LastReason ?? ""}\n" +
            $"Last proposed parameter: {key?.ToString() ?? "None"}\n" +
            $"Previous value: {(key.HasValue ? AEDv2Catalog.Find(key.Value).Baseline.ToString() : "N/A")}\n" +
            $"New value: {(key.HasValue ? proposal.Plan.Get(key.Value).ToString() : "N/A")}\n" +
            $"Decision point: {runtime?.LastDecisionPoint.ToString() ?? "N/A"}\n" +
            $"Commit status: {commitStatus}\n" +
            $"Applied revision: {(applied ? revision : matchState?.AEDv2PlanRevision ?? 0)}\n" +
            $"Current plan fingerprint: {planFingerprint}";
    }
}
