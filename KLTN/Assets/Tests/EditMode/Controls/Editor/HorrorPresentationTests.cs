using EchoProtocol.Audio;
using NUnit.Framework;
using UnityEngine;

public sealed class HorrorPresentationTests
{
    [Test]
    public void SharedCueDuplicateOrOlderOrdinalCannotReplayIncludingAfterMute()
    {
        uint consumed = 0;
        Assert.That(HorrorPresentationRules.ConsumeOrdinal(0, ref consumed), Is.False);
        Assert.That(HorrorPresentationRules.ConsumeOrdinal(5, ref consumed), Is.True);
        Assert.That(HorrorPresentationRules.ConsumeOrdinal(5, ref consumed), Is.False);
        Assert.That(HorrorPresentationRules.ConsumeOrdinal(4, ref consumed), Is.False);
        Assert.That(HorrorPresentationRules.ConsumeOrdinal(6, ref consumed), Is.True);
    }
    [Test]
    public void DangerDefersEventsWithoutBacklogAndNewMatchStartsWithGrace()
    {
        var pacing = new HorrorPresentationRules(); pacing.Reset(0);
        Assert.That(pacing.Ready(59, false), Is.False);
        Assert.That(pacing.Ready(60, true), Is.False);
        Assert.That(pacing.Ready(79, false), Is.False);
        Assert.That(pacing.Ready(80, false), Is.True);
        pacing.Presented(1, 80, 1, false);
        Assert.That(pacing.Ready(139, false), Is.False);
        Assert.That(pacing.Ready(140, false), Is.True);
        pacing.Reset(200);
        Assert.That(pacing.Ready(259, false), Is.False);
        Assert.That(pacing.LastCue, Is.EqualTo(-1));
    }

    [Test]
    public void LastTwoCuesAreExcludedAndEchoHasItsOwnLongCooldown()
    {
        var pacing = new HorrorPresentationRules(); pacing.Reset(0);
        pacing.Presented(10, 180, 65, true);
        pacing.Presented(1, 245, 65, false);
        Assert.That(pacing.CanChoose(10), Is.False);
        Assert.That(pacing.CanChoose(1), Is.False);
        Assert.That(pacing.CanChoose(2), Is.True);
        Assert.That(pacing.NextEchoAt, Is.EqualTo(480));
    }

    [TestCase(true, false, false, false, "player/metal_footstep_")]
    [TestCase(false, true, false, true, "player/crouch_footstep_")]
    [TestCase(false, false, false, true, "player/sprint_footstep_")]
    [TestCase(false, false, false, false, "player/concrete_footstep_")]
    public void SurfacePalettePreservesMovementCadence(bool metal, bool soft, bool crouch, bool sprint, string expected)
        => Assert.That(HorrorPresentationRules.StepPrefix(metal, soft, crouch, sprint), Is.EqualTo(expected));

    [Test]
    public void EchoSettingClampsInvalidSavedValuesAndPersistsOff()
    {
        string key = "Echo.Audio.EchoIntensity";
        bool existed = PlayerPrefs.HasKey(key); int old = PlayerPrefs.GetInt(key);
        try
        {
            HorrorPresentationSettings.SetEchoIntensity(-10);
            Assert.That(HorrorPresentationSettings.EchoIntensity, Is.Zero);
            HorrorPresentationSettings.SetEchoIntensity(99);
            Assert.That(HorrorPresentationSettings.EchoIntensity, Is.EqualTo(2));
        }
        finally { if (existed) PlayerPrefs.SetInt(key, old); else PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
    }

    [Test]
    public void RequiredPresentationClipsResolveWithoutNewAudioDependencies()
    {
        var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
        Assert.That(catalog, Is.Not.Null);
        foreach (var key in new[] { "atmosphere/flooded_water", "map_ambience/pipe_creak",
            "scifi/pressure_release_01", "scifi/pressure_release_02", "scifi/engine_start",
            "map_ambience/distant_metal_bang", "map_ambience/steam_hiss", "player/concrete_footstep_01",
            "objectives/charge_engage", "objectives/charge_interrupt", "energy_core/ambient_hum_loop" })
            Assert.That(catalog.Find(key), Is.Not.Null, key);
    }
}
