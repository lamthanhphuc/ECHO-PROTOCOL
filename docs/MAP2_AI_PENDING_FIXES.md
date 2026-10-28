# Map 2 AI — các lỗi cần sửa tiếp

Ghi ngày 2026-10-21, nhánh `fix/stalker`, workspace `D:\Bin\KLTN-main`.
Ban đầu người dùng yêu cầu lưu chẩn đoán để sửa sau. Sau đó đã yêu cầu triển khai; trạng thái mới ở phần dưới. Các bằng chứng runtime ban đầu được giữ lại để đối chiếu.

## Trạng thái triển khai — 2026-10-21

- Đã sửa Catfish ở đất khô trở về đi bộ qua `TryActivateAgent`, giữ lượt tuần tra đang băng nước; xóa cache đích cũ khi bật lại agent để yêu cầu đường mới.
- Độ cao bơi nước nông theo nền đi được, lọc nhân vật/pickup/trigger/mặt nước và không nhận mái cao hơn mặt nước. Tại tọa độ cá bị lún, chân mới `y=15.34546`, cả 8 hướng bước nhỏ thông trong kiểm tra geometry Scene_A.
- Bán kính agent và thân Catfish lấy từ profile NavMesh đang dùng (Map 2 hiện là 0.4 m); Disruptor không phải Catfish giữ baseline 0.65 m. Kiểm tra đi theo NavMesh chấp nhận bậc trong giới hạn climb của profile, nhưng vẫn chặn cửa đóng/trần thấp.
- Probe vừa tới được kiểm tra tầm nhìn ngay; probe đang quét được ưu tiên mỗi tick trước khi xoay, không phải chờ cursor đi hết hàng nghìn node. Vẫn giữ line of sight, scan dự phòng và chống lặp probe.
- Đã tái hiện nguyên nhân fallback: điểm xuất phát `(490.142, 16.844, 544.636)` được node gần nhất gán `Isolated`; 48 candidate gần đều có native `PathComplete` nhưng bị loại ngay tại mẫu xuất phát. Fallback nay cho phép tiền tố `Isolated` trở vào patrol zone; sau khi vào phải ở trong zone và không được đi qua zone gameplay khác. Không tăng native path budget.
- Không đổi scene/NavMesh/RegionGraph trong lượt sửa này: tuyến Native NavMesh vào nhà đã hoàn chỉnh, sai lệch chính nằm ở footprint và guard bậc thềm.

Kiểm tra đã chạy: 38/38 minion, 96/96 RoomSweep, biên dịch sạch; 2/2 smoke test controller trên NavMesh Scene_A tại tọa độ kẹt cũ và điểm fallback `Isolated`. Smoke test dùng cấu hình Lizard trong fixture cô lập, không chứng minh trận Host/Client hoàn chỉnh.

Giới hạn còn cần thử trong trận: tuyến vào nhà được lấy mẫu có một chiếc ghế `lo_Prop_Chair_A_LOD0/LOD1` cản collider ở khoảng `(558.5, 17.996, 273.65)` dù Native NavMesh đi qua. Không tắt collider ghế hay tuyên bố tuyến đó hoàn toàn hết vật cản; cần quan sát vòng tránh/phục hồi trong trận và cân nhắc sửa bake geometry nếu vẫn mắc tại đây. Chưa xác nhận lại toàn bộ Host/Client hoặc Catfish Purge Scene_B. Chưa commit/push.

## Bối cảnh

- Gameplay 1, Map 2, `Scene_A`, Host đang Play, không pause khi lấy dữ liệu.
- Người dùng báo Stalker liên tục quay 360°, có lúc đứng yên; Catfish không qua được cửa mở và vài con đứng yên, gồm một con dưới nước.
- Các bản sửa và kiểm tra trước đó vẫn là WIP. Kết quả cũ (32 minion, 91 RoomSweep, 18 navigation, smoke test tại vị trí cũ) không chứng minh các lỗi mới này đã hết.
- Giữ toàn bộ WIP; không sửa các thay đổi ngoài phạm vi như `MatchDifficulty.cs` và các font asset.

## 1. Catfish trên đất khô vẫn ở nhánh bơi — đã xác nhận

Các vị trí runtime:

| Vị trí | Hướng bước nhỏ không bị collider chặn |
| --- | --- |
| `(596.198, 16.656, 400.548)` | 5/8 |
| `(567.102, 17.400, 278.249)` | 6/8 |
| `(583.979, 16.685, 400.526)` | 5/8 |
| `(584.225, 16.821, 463.619)` | 7/8 |

Cả bốn đều có `water=false`, NavMesh ngay dưới chân, native path tới player là `PathComplete`, nhưng `_catfishOnLand=false`, `_catfishWasInWater=false`, `_catfishPatrolCrossing=false`, agent bị disable. Chúng đang `Harass` và không bị UV stun.

Hướng sửa: trong `CreepMinionRuntime.TickCatfishSwim`, phục hồi về nhánh đi bộ khi đang khô và không còn thực hiện lượt băng qua nước. Dùng `TryActivateAgent` cùng kiểm tra collider hiện có; không Warp xuyên tường. Kiểm tra việc watchdog xóa `_catfishPatrolCrossing` khiến cá bị giữ ở trạng thái bơi trên đất khô. Không làm mất cơ chế tuần tra băng qua nước.

## 2. Catfish dưới nước bị lún vào nền dốc — đã xác nhận

- Vị trí đứng yên: `(585.976, 15.070, 265.939)`.
- Mặt nước `y=16.0`; đang ở nhánh bơi, `_catfishWasInWater=true`, agent disabled, không có NavMesh trong 2 m.
- Cả 8 hướng bước ngang nhỏ đều bị chặn.
- Raycast xuống nền tại chính vị trí đó: Terrain `y=15.2952614`, pháp tuyến `(0.414, 0.893, 0.174)`.
- Chân ở `15.070`, thấp hơn nền khoảng 0.225 m; độ cao bơi cố định `surfaceY - 0.9` không theo nền nước nông. Capsule ban đầu xuyên nhẹ Terrain; ComputePenetration cho độ xuyên khoảng 0.247 m.

Hướng sửa: nâng độ cao bơi theo nền có thể đi được ở nước nông, rồi chuyển lên bờ qua đường hợp lệ. Lọc nhân vật, pickup, trigger và mặt nước khi tìm nền. Không bỏ kiểm tra collider, không teleport lên NavMesh ở xa và không đẩy cá lên mái/tầng khác. Thử lại đúng tọa độ này với nền dốc và quá trình nước → bờ → NavMesh.

## 3. Kích thước Catfish khác profile NavMesh ở cửa mở — đã xác nhận sai lệch, cần đo cửa

- Catfish runtime: NavMeshAgent radius **0.65**, CapsuleCollider radius **0.65**, height **1.8**.
- Agent type `-1372625422`, profile NavMesh đang bake với radius **0.4**.
- `CreepMinionRuntime.Spawned()` đang ép mọi Disruptor/Catfish về radius 0.65.
- Người dùng thấy dải NavMesh cửa mở quá nhỏ, Catfish không lọt. Chưa đo trực tiếp cửa cụ thể nên chưa kết luận cần bake rộng hơn hay sửa footprint.

Hướng sửa: đo khoảng trống cửa mở và kiểm tra path ở cả hai phía. Đồng bộ footprint Catfish với profile dùng để bake nếu phù hợp; giữ baseline Disruptor ở map khác. Chỉ chỉnh bake khi bằng chứng yêu cầu và cập nhật RegionGraph tương ứng. Cửa đóng vẫn phải chặn cả NavMesh/path và collider; không tắt collider khung/tường để lách lỗi.

## 4. Stalker lặp quay 360° — cần sửa nhánh quan sát, chưa xác nhận hết nguyên nhân

- Bản sửa trước đã thêm `BeginRoomSweepSelfProbeScan` khi tới probe nhưng coverage chưa đánh dấu quan sát, để ngăn lặp giữa hai probe sát nhau.
- Người dùng hiện thấy Stalker quay 360° liên tục. Không tiếp tục ép quét trọn vòng tại từng vertex NavMesh dày đặc.
- `UpdateRoomSweepVisualCoverage` chỉ xử lý 32 node mỗi tick theo cursor; `TickRoomSweepSelfProbeScan` chưa ưu tiên kiểm tra trực tiếp probe đang quét.
- Một mẫu runtime có **4.005 probe**, đã quan sát **3.874**, cursor **2.450**. Đây là bằng chứng mật độ probe lớn, chưa phải bằng chứng duy nhất của lỗi quay.

Hướng kiểm tra/sửa nhỏ: kiểm tra trực tiếp khả năng nhìn probe vừa tới trước khi mở scan, và ưu tiên probe đang quét mỗi tick để kết thúc ngay khi đã nhìn thấy. Giữ cơ chế chống lặp probe và kiểm tra line of sight; không đánh dấu cả phòng đã quan sát chỉ vì tới một điểm. Kiểm tra số lần quét liên tiếp và thời gian đứng yên, không chỉ kiểm tra tổng quãng đường di chuyển.

## 5. Stalker chờ lập đường dự phòng quá lâu — đã thấy trạng thái chờ

- Tại `(490.142, 16.844, 544.636)`: PATROL, không có active destination, navigation Idle, `NoReachableRegionObjective`, `_roomSweepPlanningDeferred=true`, fallback index **8.572**, budget còn **0**; chỉ 3 native path checks mỗi tick.
- Sau đó Stalker đã di chuyển lên lầu và qua các vị trí khác. Không kết luận nó bị đứng vĩnh viễn hoặc mọi candidate đều sai zone.
- Tại `(489.415, 20.546, 506.636)`, nhiều candidate gần có `PathComplete` và qua kiểm tra zone; có candidate `Isolated`/`PathPartial` cần tiếp tục loại đúng.

Hướng sửa: tìm vì sao danh sách dự phòng phải duyệt hàng nghìn candidate, ưu tiên candidate gần có thể tiếp cận và giữ kiểm tra zone/đảo chơi. Watchdog 5 giây phải tạo tiến triển thực tế, không chỉ reset bộ đếm/đổi đích trong lúc vẫn duyệt vô hạn. Không tăng mạnh native path budget gây giật frame.

## File và công cụ để tiếp tục

- `KLTN/Assets/Scripts/AI/Minions/CreepMinionRuntime.cs`: `Spawned`, `TickCatfishSwim`, `TickStationaryRecovery`, `TryActivateAgent`, `IsMovementStepBlocked`.
- `KLTN/Assets/Scripts/AI/Stalker/StalkerController.cs`: `MarkRoomSweepDestinationReached`, `TickRoomSweepSelfProbeScan`, `UpdateRoomSweepVisualCoverage`, `TrySetRoomSweepLocalFallbackDestination`, `TickStationaryRecovery`.
- `KLTN/Assets/Scripts/AI/Stalker/StalkerNavigationController.cs`: giữ kiểm tra va chạm/thoát chồng lấn đã sửa.
- Kiểm tra: `CreepMinionMovementTests`, `CreepMinionContractTests`, `RoomSweepControllerIntegrationTests`, `Map2StalkerPatrolSmokeTests`.
- Script chẩn đoán tạm: `KLTN/Temp/InspectLiveStuck.cs`, `InspectLiveNavigationDetails.cs`, `InspectRoomSweepScan.cs`, `InspectPatrolFallback.cs`.
- Snapshot tạm: `KLTN/Temp/live-stuck-current.json`. `Temp` có thể bị xóa; bằng chứng chính đã chép vào ghi chú này.
- Unity CLI kết nối project `D:/Bin/KLTN-main/KLTN`, port 7800 tại lúc chẩn đoán. Xác minh lại khi tiếp tục.

Gate lúc triển khai đã được kiểm tra bằng Unity CLI: Editor ở ngoài Play, scene Bootstrap. Các phần bên trên mô tả dữ liệu trước sửa; xem trạng thái triển khai để biết kết quả mới.
