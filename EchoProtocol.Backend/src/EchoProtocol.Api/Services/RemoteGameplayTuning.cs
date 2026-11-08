namespace EchoProtocol.Api.Services;

public static class RemoteGameplayTuning
{
    public sealed record Field(string Key, string Group, string Label, decimal Min, decimal Max, bool Integral = false, string? Map = null, string? Help = null);

    public static readonly Field[] Fields =
    [
        new("staminaDrain", "Người chơi", "Tiêu hao thể lực / giây", 1, 100),
        new("staminaRecovery", "Người chơi", "Hồi thể lực / giây", 1, 100),
        new("revivesPerZone", "Người chơi", "Số lượt cứu mỗi Zone", 0, 5, true),
        new("reviveSeconds", "Người chơi", "Thời gian cứu (giây)", .5m, 20),
        new("stalkerDamage", "Stalker", "Sát thương mỗi đòn của Stalker", 1, 150, Help: "Tăng giá trị để mỗi đòn đánh của Stalker trừ nhiều máu hơn."),
        new("stalkerDetectSeconds", "Stalker", "Thời gian Stalker cần để phát hiện (giây)", .25m, 8, Help: "Giá trị lớn hơn cho người chơi thêm thời gian thoát khỏi tầm quan sát."),
        new("stalkerForgetSeconds", "Stalker", "Thời gian Stalker còn nhớ mục tiêu (giây)", .5m, 60, Help: "Tăng giá trị để Stalker bám theo mục tiêu lâu hơn sau khi mất dấu."),
        new("stalkerSearchSeconds", "Stalker", "Thời gian Stalker tìm quanh vị trí mất dấu (giây)", .25m, 30, Help: "Tăng giá trị để Stalker tìm lâu hơn trước khi kết thúc tìm kiếm."),
        new("stalkerHearing", "Stalker", "Hệ số nghe", .25m, 2),
        new("listenerHearing", "AI khác", "Hệ số tầm nghe của Listener", .5m, 2),
        new("stalkerSpecialCooldown", "Stalker", "Hồi chiêu đặc biệt của Stalker (giây)", 30, 900, Help: "Tăng giá trị để Stalker phải đợi lâu hơn giữa các lần dùng chiêu đặc biệt."),
        new("stalkerDoorBreakSeconds", "Stalker", "Thời gian Stalker phá cửa (giây)", .25m, 20, Help: "Tăng giá trị để cửa cản Stalker lâu hơn."),
        new("hideInspectSeconds", "Stalker", "Thời gian Stalker kiểm tra chỗ trốn (giây)", .1m, 5, Help: "Thời gian dành cho một lần kiểm tra chỗ trốn."),
        new("hideReinspectCooldown", "Stalker", "Thời gian chờ kiểm tra lại chỗ trốn (giây)", 1, 60, Help: "Tăng giá trị để Stalker kiểm tra lại ít thường xuyên hơn."),
        new("minionCapZone1", "Minion", "Giới hạn Zone 1", 0, 3, true, "SciFi"),
        new("minionCapZone2", "Minion", "Giới hạn Zone 2", 0, 4, true, "SciFi"),
        new("minionCapZone3", "Minion", "Giới hạn Zone 3", 0, 4, true, "SciFi"),
        new("toolsPerZone", "Công cụ", "Ngân sách công cụ mỗi Zone", 5, 6, true, "SciFi"),
        new("zone1ToolBudget", "Công cụ", "Phân bổ công cụ Zone 1", 5, 6, true, "SciFi"),
        new("zone2ToolBudget", "Công cụ", "Phân bổ công cụ Zone 2", 5, 6, true, "SciFi"),
        new("zone3ToolBudget", "Công cụ", "Phân bổ công cụ Zone 3", 5, 6, true, "SciFi"),
        new("inventorySlots", "Công cụ", "Số ô kho đồ thường", 1, 2, true, Help: "Số vật phẩm thường người chơi có thể mang theo cùng lúc."),
        new("multiUseToolCharges", "Công cụ", "Lượt dùng mỗi máy tạo ồn hoặc khóa chặn cửa", 1, 3, true, Help: "Cùng một giới hạn áp dụng cho từng công cụ thuộc hai loại này."),
        new("toolPickupDistance", "Công cụ", "Tầm nhặt công cụ (đơn vị Unity)", 1, 5, Help: "Khoảng cách tối đa từ người chơi đến công cụ để nhặt."),
        new("scannerDuration", "Công cụ", "Thời gian máy quét hoạt động (giây)", 1, 45, Help: "Tăng giá trị để hiệu ứng quét kéo dài hơn."),
        new("scannerCooldown", "Công cụ", "Thời gian chờ dùng lại máy quét (giây)", 10, 120, Help: "Tăng giá trị để người chơi phải đợi lâu hơn giữa các lần quét."),
        new("scannerRange", "Công cụ", "Tầm quét (đơn vị Unity)", 20, 150, Help: "Tăng giá trị để máy quét phát hiện ở xa hơn."),
        new("toolSpawnSpacing", "Công cụ", "Khoảng cách tối thiểu giữa các điểm đặt công cụ (đơn vị Unity)", .25m, 6, false, "SciFi", Help: "Tăng giá trị để các điểm đặt công cụ cách xa nhau hơn."),
        new("noiseMakerSeconds", "Công cụ", "Thời gian máy tạo tiếng ồn hoạt động (giây)", 1, 30, Help: "Tăng giá trị để công cụ phát tiếng ồn lâu hơn."),
        new("noiseMakerLoudness", "Công cụ", "Hệ số cường độ máy tạo tiếng ồn", .1m, 2, Help: "Mốc 1 giữ cường độ gốc. Giá trị 0,5 giảm còn một nửa."),
        new("doorJammerBreakSeconds", "Công cụ", "Thời gian phá khóa chặn cửa (giây)", .25m, 20, Help: "Tăng giá trị để khóa chặn cửa cầm cự lâu hơn khi bị phá."),
        new("doorTravelSeconds", "Môi trường", "Thời gian đóng mở cửa (giây)", .1m, 5),
        new("terminalHoldSeconds", "Nhiệm vụ", "Thời gian tải dữ liệu ở terminal an ninh (giây)", 5, 180, false, "SciFi", Help: "Tăng giá trị để đội phải giữ terminal lâu hơn cho đến khi tải xong."),
        new("missionDurationSeconds", "Thời lượng trận", "Giới hạn thời gian toàn trận (giây)", 900, 7200, true, Help: "Ví dụ 1.800 giây tương đương 30 phút."),
        new("coresPerSectorBox", "Nhiệm vụ", "Số lõi cần nộp cho mỗi Sector Box", 1, 2, true, "SciFi", Help: "Tăng giá trị để mỗi hộp yêu cầu thêm lõi."),
        new("relayRepairWindowSeconds", "Nhiệm vụ · Relay", "Thời hạn hoàn thành terminal sau khi sửa Relay (giây)", 75, 600, false, "SciFi", Help: "Bắt đầu khi cả 4 Relay đã online. Đặt dài hơn thời gian tải terminal ít nhất 15 giây."),
        new("relaySyncSeconds", "Nhiệm vụ · Relay", "Thời gian giữ thao tác đồng bộ Relay B (giây)", 2, 20, false, "SciFi", Help: "Tăng giá trị để người chơi phải giữ thao tác đồng bộ lâu hơn."),
        new("relayCodeAttempts", "Nhiệm vụ · Relay", "Số lần được thử mã tại Relay B", 3, 5, true, "SciFi", Help: "Tăng giá trị để người chơi có thêm lượt nhập mã."),
        new("escapeSeconds", "Nhiệm vụ", "Thời hạn thoát ở giai đoạn thường (giây)", 15, 180, false, "SciFi", Help: "Tăng giá trị để đội có thêm thời gian tới điểm thoát."),
        new("convoySpeed", "Convoy · Di chuyển", "Tốc độ convoy khi có từ hai người hộ tống (đơn vị Unity/giây)", .5m, 15, false, "SciFi", Help: "Tốc độ mục tiêu khi có ít nhất hai người chơi còn sống trong bán kính hộ tống."),
        new("convoyAcceleration", "Convoy · Di chuyển", "Mức tăng tốc convoy (đơn vị Unity/giây²)", .25m, 25, false, "SciFi", Help: "Tăng giá trị để xe đạt tốc độ mục tiêu nhanh hơn."),
        new("convoyDeceleration", "Convoy · Di chuyển", "Mức giảm tốc convoy (đơn vị Unity/giây²)", .25m, 35, false, "SciFi", Help: "Tăng giá trị để xe chậm lại nhanh hơn khi tốc độ mục tiêu giảm."),
        new("convoyEscortRadius", "Convoy · Hộ tống", "Bán kính tính người hộ tống (đơn vị Unity)", 2, 15, false, "SciFi", Help: "Chỉ người chơi còn sống trong bán kính này mới được tính hộ tống."),
        new("convoySoloMultiplier", "Convoy · Hộ tống", "Tỷ lệ tốc độ khi có một người hộ tống", .25m, 1, false, "SciFi", Help: "Ví dụ 0,5 là nửa tốc độ thường. Khi không có ai hộ tống, xe giảm tốc về 0."),
        new("convoyFuelCapacity", "Convoy · Nhiên liệu", "Số chặng chạy được sau mỗi lần nạp", 2, 3, true, "SciFi", Help: "Tăng giá trị để xe đi thêm chặng trước khi cần nạp lại."),
        new("convoyRefuelSeconds", "Convoy · Nhiên liệu", "Thời gian nạp nhiên liệu (giây)", .5m, 8, false, "SciFi", Help: "Tăng giá trị để thao tác nạp mất nhiều thời gian hơn."),
        new("purgeSpawnCooldown", "Purge", "Nhịp spawn Purge (giây)", 5, 180, false, "Scene_B"),
        new("purgeMinionCap", "Purge", "Giới hạn Fishcat", 0, 10, true, "Scene_B"),
        new("purgeHuntSeconds", "Purge", "Thời gian săn cơ bản (giây)", 20, 150, false, "Scene_B")
    ];

    public static readonly IReadOnlyDictionary<string, Field> ByKey =
        Fields.ToDictionary(field => field.Key, StringComparer.Ordinal);

    // Keep validating stored snapshots using their original keys and limits.
    public static bool IsRetired(string key) => key is
        "stalkerHearing" or "listenerHearing" or "doorTravelSeconds" or
        "toolsPerZone" or "zone1ToolBudget" or "zone2ToolBudget" or "zone3ToolBudget";

    public static readonly Field[] EditableFields = Fields.Where(field => !IsRetired(field.Key)).ToArray();
}
