namespace EchoProtocol.Api.Services;

public static class RemoteGameplayTuning
{
    public sealed record Field(string Key, string Group, string Label, decimal Min, decimal Max, bool Integral = false, string? Map = null);

    public static readonly Field[] Fields =
    [
        new("staminaDrain", "Người chơi", "Tiêu hao thể lực / giây", 1, 100),
        new("staminaRecovery", "Người chơi", "Hồi thể lực / giây", 1, 100),
        new("revivesPerZone", "Người chơi", "Số lượt cứu mỗi Zone", 0, 5, true),
        new("reviveSeconds", "Người chơi", "Thời gian cứu (giây)", .5m, 20),
        new("stalkerDamage", "Stalker", "Sát thương tấn công", 1, 150),
        new("stalkerDetectSeconds", "Stalker", "Thời gian phát hiện (giây)", .25m, 8),
        new("stalkerForgetSeconds", "Stalker", "Thời gian mất dấu (giây)", .5m, 60),
        new("stalkerSearchSeconds", "Stalker", "Thời gian tìm kiếm (giây)", .25m, 30),
        new("stalkerHearing", "Stalker", "Hệ số nghe", .25m, 2),
        new("listenerHearing", "AI khác", "Hệ số tầm nghe của Listener", .5m, 2),
        new("stalkerSpecialCooldown", "Stalker", "Hồi chiêu đặc biệt (giây)", 30, 900),
        new("stalkerDoorBreakSeconds", "Stalker", "Thời gian phá cửa (giây)", .25m, 20),
        new("hideInspectSeconds", "Stalker", "Thời gian kiểm tra chỗ trốn (giây)", .1m, 5),
        new("hideReinspectCooldown", "Stalker", "Hồi kiểm tra chỗ trốn (giây)", 1, 60),
        new("minionCapZone1", "Minion", "Giới hạn Zone 1", 0, 3, true, "SciFi"),
        new("minionCapZone2", "Minion", "Giới hạn Zone 2", 0, 4, true, "SciFi"),
        new("minionCapZone3", "Minion", "Giới hạn Zone 3", 0, 4, true, "SciFi"),
        new("toolsPerZone", "Công cụ", "Ngân sách công cụ mỗi Zone", 5, 6, true, "SciFi"),
        new("zone1ToolBudget", "Công cụ", "Phân bổ công cụ Zone 1", 5, 6, true, "SciFi"),
        new("zone2ToolBudget", "Công cụ", "Phân bổ công cụ Zone 2", 5, 6, true, "SciFi"),
        new("zone3ToolBudget", "Công cụ", "Phân bổ công cụ Zone 3", 5, 6, true, "SciFi"),
        new("inventorySlots", "Công cụ", "Số ô kho đồ thường", 1, 2, true),
        new("multiUseToolCharges", "Công cụ", "Lượt dùng máy tạo ồn và khóa cửa", 1, 3, true),
        new("toolPickupDistance", "Công cụ", "Tầm nhặt công cụ", 1, 5),
        new("scannerDuration", "Công cụ", "Thời lượng máy quét (giây)", 1, 45),
        new("scannerCooldown", "Công cụ", "Hồi máy quét (giây)", 10, 120),
        new("scannerRange", "Công cụ", "Tầm quét", 20, 150),
        new("toolSpawnSpacing", "Công cụ", "Khoảng cách tối thiểu khi spawn", .25m, 6, false, "SciFi"),
        new("noiseMakerSeconds", "Công cụ", "Thời lượng máy tạo tiếng ồn (giây)", 1, 30),
        new("noiseMakerLoudness", "Công cụ", "Cường độ máy tạo tiếng ồn", .1m, 2),
        new("doorJammerBreakSeconds", "Công cụ", "Thời gian phá khóa chặn cửa (giây)", .25m, 20),
        new("doorTravelSeconds", "Môi trường", "Thời gian đóng mở cửa (giây)", .1m, 5),
        new("terminalHoldSeconds", "Nhiệm vụ", "Thời gian giữ terminal (giây)", 5, 180, false, "SciFi"),
        new("missionDurationSeconds", "Nhiệm vụ", "Thời lượng trận (giây)", 900, 7200, true),
        new("coresPerSectorBox", "Nhiệm vụ", "Số lõi mỗi Sector Box", 1, 2, true, "SciFi"),
        new("relayRepairWindowSeconds", "Nhiệm vụ", "Cửa sổ hoàn thành Relay (giây)", 75, 600, false, "SciFi"),
        new("relaySyncSeconds", "Nhiệm vụ", "Thời gian giữ đồng bộ Relay B (giây)", 2, 20, false, "SciFi"),
        new("relayCodeAttempts", "Nhiệm vụ", "Số lượt thử mã Relay B", 3, 5, true, "SciFi"),
        new("escapeSeconds", "Nhiệm vụ", "Thời gian thoát thường (giây)", 15, 180, false, "SciFi"),
        new("convoySpeed", "Convoy", "Tốc độ convoy thường (đơn vị Unity/giây)", .5m, 15, false, "SciFi"),
        new("convoyAcceleration", "Convoy", "Gia tốc convoy", .25m, 25, false, "SciFi"),
        new("convoyDeceleration", "Convoy", "Giảm tốc convoy", .25m, 35, false, "SciFi"),
        new("convoyEscortRadius", "Convoy", "Khoảng cách hộ tống", 2, 15, false, "SciFi"),
        new("convoySoloMultiplier", "Convoy", "Hệ số hộ tống solo", .25m, 1, false, "SciFi"),
        new("convoyFuelCapacity", "Convoy", "Số chặng mỗi lần nạp", 2, 3, true, "SciFi"),
        new("convoyRefuelSeconds", "Convoy", "Thời gian nạp nhiên liệu (giây)", .5m, 8, false, "SciFi"),
        new("purgeSpawnCooldown", "Purge", "Nhịp spawn Purge (giây)", 5, 180, false, "Scene_B"),
        new("purgeMinionCap", "Purge", "Giới hạn Fishcat", 0, 10, true, "Scene_B"),
        new("purgeHuntSeconds", "Purge", "Thời gian săn cơ bản (giây)", 20, 150, false, "Scene_B")
    ];

    public static readonly IReadOnlyDictionary<string, Field> ByKey =
        Fields.ToDictionary(field => field.Key, StringComparer.Ordinal);
}
