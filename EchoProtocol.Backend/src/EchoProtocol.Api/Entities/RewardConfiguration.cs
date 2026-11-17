using System.ComponentModel.DataAnnotations;

namespace EchoProtocol.Api.Entities;

public sealed class RewardConfiguration
{
    public static readonly Guid SharedId = new("c7e0137e-2484-4c88-9d7b-2161c6e15cef");
    public Guid Id { get; set; }
    public DateTime EffectiveAtUtc { get; set; }
    public Guid UpdatedByUserId { get; set; }
    [Range(0, 10000)] public int Participation { get; set; } = 20;
    [Range(0, 10000)] public int Objective { get; set; } = 30;
    [Range(0, 10000)] public int Win { get; set; } = 50;
    [Range(0, 10000)] public int Survival { get; set; } = 20;
    [Range(0, 10000)] public int Contribution { get; set; } = 10;
    [Range(0, 10000)] public int Disconnected { get; set; } = 10;
    [Range(0, 10000)] public int Maximum { get; set; } = 130;
}
