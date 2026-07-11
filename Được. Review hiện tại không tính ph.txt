Dưới đây là **bản SRS hoàn chỉnh mới nhất**, đã gộp từ SRS trước và file bổ sung mới của bạn. Bản này chốt các điểm mới: **4 tháng triển khai, demo 3–4 laptop thật, Unity 6.3 LTS, Photon Fusion Free 100 CCU, PostgreSQL cloud Neon/Supabase, backend deploy riêng, seed admin, shop 15 cosmetic items có ảnh, và reward anti-cheat do backend xử lý**.  

# SOFTWARE REQUIREMENT SPECIFICATION

# ECHO PROTOCOL

**Project name:** ECHO PROTOCOL
**Project type:** Online Cooperative First-Person Horror Game
**Document type:** Software Requirement Specification
**Version:** 1.1
**Language:** Vietnamese
**Team size:** 4 members
**Project duration:** 4 months
**Target platform:** Windows 10/11 64-bit
**Main technologies:** Unity 6.3 LTS, C#, Photon Fusion Host Mode, ASP.NET Core Web API, PostgreSQL
**Document usage:** Internal team document

---

# 1. Giới thiệu

## 1.1 Mục đích tài liệu

Tài liệu SRS này mô tả đầy đủ yêu cầu phần mềm cho dự án **ECHO PROTOCOL**, một trò chơi kinh dị hợp tác trực tuyến góc nhìn thứ nhất dành cho **2–4 người chơi**.

Tài liệu này dùng để:

| Mục đích          | Mô tả                                                              |
| ----------------- | ------------------------------------------------------------------ |
| Phân tích yêu cầu | Xác định chính xác hệ thống cần làm gì                             |
| Thiết kế hệ thống | Làm cơ sở cho kiến trúc game, backend, database và admin dashboard |
| Chia task         | Giúp nhóm 4 người phân công rõ ràng                                |
| Triển khai        | Làm tài liệu tham chiếu khi code                                   |
| Kiểm thử          | Làm cơ sở viết test case và acceptance criteria                    |
| Demo capstone     | Xác định scope MVP để tránh làm quá rộng                           |

Tài liệu này chỉ dùng nội bộ team, không yêu cầu xuất Word/PDF ở giai đoạn hiện tại.

---

## 1.2 Phạm vi dự án

**ECHO PROTOCOL** là game kinh dị hợp tác online. Người chơi vào vai nhóm điều tra viên bị mắc kẹt trong một cơ sở nghiên cứu bỏ hoang. Để chiến thắng, nhóm phải phối hợp tìm fuse, khôi phục nguồn điện, lấy access card, mở escape door và chạy đến escape zone trong khi bị truy đuổi bởi monster AI.

Hệ thống gồm 5 phần chính:

| Thành phần           | Mô tả                                                                         |
| -------------------- | ----------------------------------------------------------------------------- |
| Unity Game Client    | Game chính, UI, gameplay, multiplayer, monster AI, Adaptive AI, shop frontend |
| Photon Fusion        | Đồng bộ real-time multiplayer theo Host Mode                                  |
| ASP.NET Core Web API | Xử lý account, JWT, profile, wallet, shop, inventory, transaction, logs       |
| PostgreSQL Database  | Lưu users, wallet, inventory, shop items, transactions, match logs, AI logs   |
| Admin Dashboard      | Quản lý shop item, xem match logs, xem AI behavior logs                       |

---

## 1.3 Mục tiêu MVP

Phiên bản MVP cần đạt được các mục tiêu sau:

| Hạng mục       | Mục tiêu                                                                         |
| -------------- | -------------------------------------------------------------------------------- |
| Map            | 1 map, ưu tiên 1 tầng                                                            |
| Monster        | 1 monster type                                                                   |
| Player         | Hỗ trợ 2–4 players                                                               |
| Demo tối thiểu | 2 players ổn định                                                                |
| Demo cuối      | 3–4 laptop thật                                                                  |
| Multiplayer    | Create room, join room, lobby ready, host start game                             |
| Gameplay loop  | Login/Register → Room/Lobby → Find 3 Fuse → Restore Power → Access Card → Escape |
| AI             | Monster AI + Adaptive AI thấy rõ trong gameplay                                  |
| Backend        | ASP.NET Core Web API deploy online                                               |
| Database       | PostgreSQL cloud bằng Neon hoặc Supabase                                         |
| Shop           | Virtual shop, wallet, inventory, purchase transaction                            |
| Shop items     | Ít nhất 15 cosmetic items có ảnh/image URL                                       |
| Admin          | Seed admin account, CRUD shop item, xem match logs, xem AI logs                  |
| Build          | Unity Windows build chạy được trên Windows 10/11 64-bit                          |

---

## 1.4 Ngoài phạm vi

Các chức năng sau **không triển khai trong MVP**:

| Chức năng                 | Trạng thái |
| ------------------------- | ---------- |
| Real-money payment        | Won’t Have |
| Dedicated game server     | Won’t Have |
| Voice chat                | Won’t Have |
| Public matchmaking        | Won’t Have |
| Ranking system            | Won’t Have |
| Multiple maps             | Won’t Have |
| Multiple monster types    | Won’t Have |
| Procedural map generation | Won’t Have |
| VR mode                   | Won’t Have |
| Complex combat system     | Won’t Have |
| Host migration            | Won’t Have |

---

# 2. Mô tả tổng quan hệ thống

## 2.1 Bối cảnh sản phẩm

ECHO PROTOCOL kết hợp nhiều mảng của software engineering:

| Mảng                    | Cách áp dụng trong dự án                                           |
| ----------------------- | ------------------------------------------------------------------ |
| Game Development        | Unity, gameplay, map, UI, animation, horror atmosphere             |
| Real-time Networking    | Photon Fusion Host Mode, room, lobby, player sync, object sync     |
| Artificial Intelligence | Monster FSM, vision/noise detection, Adaptive AI                   |
| Backend Development     | ASP.NET Core Web API, JWT, RESTful API                             |
| Database                | PostgreSQL, relational schema, transaction, data integrity         |
| Admin System            | Dashboard quản lý shop và xem logs                                 |
| Security                | Password hashing, role-based authorization, server-side validation |
| Testing                 | Multiplayer test, AI test, API test, deployment test               |

Gameplay real-time được xử lý bằng **Photon Fusion Host Mode**. Backend không xử lý trực tiếp movement hoặc monster real-time. Backend chịu trách nhiệm lưu trữ dữ liệu persistent như account, wallet, inventory, purchase history, match logs và AI behavior logs.

---

## 2.2 Kiến trúc tổng quan

```text
+--------------------------------------------------+
|                  Unity Game Client               |
|--------------------------------------------------|
| Login/Register                                   |
| Main Menu                                        |
| Lobby                                            |
| Multiplayer Gameplay                             |
| Player System                                    |
| Monster AI                                       |
| Adaptive AI                                      |
| Shop Frontend                                    |
| Inventory UI                                     |
| Result Screen                                    |
+---------------------+----------------------------+
                      |
                      | HTTPS REST API
                      v
+--------------------------------------------------+
|              ASP.NET Core Web API                |
|--------------------------------------------------|
| Authentication / JWT                             |
| Player Profile                                   |
| Wallet                                           |
| Shop Catalog                                     |
| Inventory                                        |
| Purchase Transaction                             |
| Match Logs                                       |
| AI Behavior Logs                                 |
+---------------------+----------------------------+
                      |
                      v
+--------------------------------------------------+
|              PostgreSQL Cloud Database           |
|              Neon or Supabase PostgreSQL         |
+--------------------------------------------------+


+--------------------------------------------------+
|              Photon Fusion Host Mode             |
|--------------------------------------------------|
| Room / Session                                   |
| Lobby Sync                                       |
| Player Sync                                      |
| Object Sync                                      |
| Monster Sync                                     |
| Match State Sync                                 |
+--------------------------------------------------+
```

---

## 2.3 Phân tách trách nhiệm

| Thành phần      | Trách nhiệm                                                             |
| --------------- | ----------------------------------------------------------------------- |
| Unity Client    | UI, input, gameplay, animation, audio, local interaction, shop frontend |
| Photon Fusion   | Đồng bộ real-time giữa host và clients                                  |
| Host Player     | Authority cho monster AI, item pickup, puzzle state, revive, win/lose   |
| Backend API     | Account, JWT, profile, wallet, shop, inventory, purchase, reward, logs  |
| PostgreSQL      | Lưu dữ liệu persistent                                                  |
| Admin Dashboard | Quản lý shop item, xem match logs, xem AI logs                          |

---

## 2.4 Actor hệ thống

| Actor         | Mô tả                                                         |
| ------------- | ------------------------------------------------------------- |
| Guest         | Người chưa đăng nhập                                          |
| Player        | Người chơi đã đăng nhập                                       |
| Host Player   | Player tạo room, giữ quyền host trong Photon Fusion Host Mode |
| Administrator | Người quản trị shop item, xem match logs và AI logs           |

---

# 3. Quyết định kỹ thuật cuối cùng

## 3.1 Technology Stack

| Hạng mục           | Quyết định                                        |
| ------------------ | ------------------------------------------------- |
| Game Engine        | Unity 6.3 LTS                                     |
| Unity fallback     | Unity 6.0 LTS nếu Photon/asset lỗi compatibility  |
| Client Language    | C#                                                |
| Multiplayer SDK    | Photon Fusion                                     |
| Multiplayer Mode   | Host Mode                                         |
| Photon Plan        | Free 100 CCU plan                                 |
| Backend            | ASP.NET Core Web API                              |
| ORM                | Entity Framework Core                             |
| Database Provider  | Npgsql                                            |
| Database           | PostgreSQL                                        |
| PostgreSQL Cloud   | Neon hoặc Supabase                                |
| Admin Dashboard    | ASP.NET Core Razor Pages hoặc MVC                 |
| Authentication     | JWT                                               |
| Password Hashing   | BCrypt hoặc PBKDF2                                |
| API Format         | RESTful API + JSON                                |
| Backend Deployment | Azure App Service, Railway hoặc cloud tương đương |
| Target OS          | Windows 10/11 64-bit                              |

---

## 3.2 Unity Version Rule

Dự án sử dụng **Unity 6.3 LTS** làm version chính.

Rule:

* Tất cả thành viên làm Unity phải dùng cùng version.
* Không tự ý đổi version trong quá trình implementation.
* Nếu Photon Fusion hoặc asset phát sinh lỗi tương thích, nhóm có thể fallback sang **Unity 6.0 LTS**.
* Việc fallback phải được thống nhất trước khi implementation chính.
* Version Unity phải được ghi rõ trong README.

---

## 3.3 Photon Requirement

Dự án sử dụng **Photon Fusion Host Mode** cho real-time multiplayer.

Với demo nội bộ bằng 3–4 laptop, **Photon Fusion Free 100 CCU plan** là đủ cho scope MVP.

Trước ngày demo, nhóm phải kiểm tra:

| Mục cần kiểm tra | Mục tiêu                                     |
| ---------------- | -------------------------------------------- |
| Photon Dashboard | App hoạt động                                |
| App ID           | Đúng App ID trong Unity config               |
| Region           | Các máy dùng cùng region                     |
| CCU plan         | Không vượt giới hạn Free plan                |
| Create room      | Host tạo room thành công                     |
| Join room        | Client join được bằng room code/session name |
| Ready/start      | Lobby sync đúng                              |
| Movement sync    | Các máy thấy movement của nhau               |

---

## 3.4 Deployment and Demo Environment

Backend **ASP.NET Core Web API** phải được deploy riêng trên **Azure App Service, Railway hoặc nền tảng cloud tương đương**.

PostgreSQL database được deploy trên **Neon hoặc Supabase**.

Không được ghi nhầm Neon/Supabase là nơi deploy backend API. Neon/Supabase chỉ là PostgreSQL cloud database.

Unity client **không kết nối trực tiếp đến PostgreSQL database**. Unity chỉ giao tiếp với backend thông qua HTTPS REST API.

| Thành phần      | Môi trường deploy/demo                             |
| --------------- | -------------------------------------------------- |
| Unity Client    | Windows build chạy trên 3–4 laptop                 |
| Multiplayer     | Photon Fusion Host Mode                            |
| Backend API     | Azure App Service, Railway hoặc cloud tương đương  |
| Database        | Neon PostgreSQL hoặc Supabase PostgreSQL           |
| Admin Dashboard | Deploy cùng backend hoặc cùng ASP.NET Core project |
| API Protocol    | HTTPS REST API                                     |
| Authentication  | JWT                                                |

---

## 3.5 Demo Environment tối thiểu

| Hạng mục      | Yêu cầu                                      |
| ------------- | -------------------------------------------- |
| Laptop demo   | 3–4 laptop Windows 10/11 64-bit              |
| Internet      | Ổn định                                      |
| Unity Build   | Windows build                                |
| Photon        | App ID đã cấu hình                           |
| Backend       | API online                                   |
| Database      | PostgreSQL cloud                             |
| Admin account | Có seed data role ADMIN                      |
| Shop data     | Tối thiểu 15 cosmetic items có ảnh/image URL |

---

# 4. Game Loop chính

## 4.1 Luồng chơi tổng quát

```text
Player register/login
        ↓
Main Menu
        ↓
Host tạo room hoặc Player join room
        ↓
Lobby hiển thị player list, ready status, host indicator
        ↓
Tất cả non-host players ready
        ↓
Host start game
        ↓
Players spawn trong abandoned research facility
        ↓
Players tìm 3 fuse trong map
        ↓
Đem 3 fuse về Power Room
        ↓
Restore power
        ↓
Security Room được mở khóa
        ↓
Players lấy Access Card
        ↓
Dùng Access Card mở Escape Door
        ↓
Players chạy đến Escape Zone
        ↓
Win/Lose + Result Screen
        ↓
Submit raw match stats + AI logs về backend
        ↓
Backend validate/tính reward + cập nhật wallet
```

---

## 4.2 Điều kiện thắng

Match được tính là **WIN** nếu:

* Đã thu thập đủ 3 fuse.
* Đã restore power.
* Đã lấy access card.
* Đã mở escape door.
* Ít nhất 1 player còn sống vào được escape zone.

---

## 4.3 Điều kiện thua

Match được tính là **LOSE** nếu một trong các điều kiện sau xảy ra:

* Tất cả players bị eliminated.
* Hết thời gian 15 phút.
* Host disconnect trong match.

Nếu host disconnect, result phải được ghi nhận là:

```text
HOST_DISCONNECTED
```

---

## 4.4 Match Timer

| Hạng mục                               | Giá trị                                        |
| -------------------------------------- | ---------------------------------------------- |
| Thời lượng match                       | 15 phút                                        |
| Nếu hết giờ                            | Match lose                                     |
| Nếu objective hoàn thành trước 15 phút | Match win khi ít nhất 1 player vào Escape Zone |
| Nếu host disconnect                    | Match kết thúc với `HOST_DISCONNECTED`         |

---

# 5. Map Scope

## 5.1 Quy mô map

| Thành phần               |                 Số lượng |
| ------------------------ | -----------------------: |
| Số map                   |                        1 |
| Số tầng                  |             1 tầng chính |
| Khu vực chính            |                        4 |
| Số phòng                 |                    10–14 |
| Fuse cần thu thập        |                        3 |
| Fuse spawn locations     |                        6 |
| Door thường              |                      4–6 |
| Locked door chính        |                        2 |
| Escape door              |                        1 |
| Hiding spots             |                      6–8 |
| Monster patrol waypoints |                    10–14 |
| Objective flow chính     |                        1 |
| Puzzle phụ               | Dual-switch, Should Have |

---

## 5.2 Khu vực chính trong map

| Khu vực                  | Vai trò                                                      |
| ------------------------ | ------------------------------------------------------------ |
| Entrance Zone            | Khu vực spawn của players, tương đối an toàn                 |
| Lab Zone                 | Có fuse spawn, nhiều phòng nhỏ, dễ bị truy đuổi              |
| Storage/Maintenance Zone | Có hiding spots, đường vòng, tạo áp lực khi bị monster đuổi  |
| Power/Security Zone      | Có power box, security room, access card và đường tới escape |

---

## 5.3 Objective placement

| Object           | Vị trí đề xuất                             |
| ---------------- | ------------------------------------------ |
| Fuse             | Random 3 trong 6 vị trí spawn              |
| Power Box        | Power Room                                 |
| Security Room    | Power/Security Zone                        |
| Access Card      | Trong Security Room                        |
| Escape Door      | Gần cuối map                               |
| Escape Zone      | Sau Escape Door                            |
| Hiding Spots     | Rải ở Lab Zone và Storage/Maintenance Zone |
| Patrol Waypoints | Đi qua các khu vực chính                   |

---

# 6. Functional Requirements

# 6.1 Authentication Module

## FR-AUTH-01: Register Account

**Mô tả:**
Guest có thể đăng ký tài khoản bằng username và password.

**Input:**

* Username.
* Password.
* Confirm password.

**Validation:**

* Username không được rỗng.
* Username không được trùng.
* Password tối thiểu 6 ký tự.
* Confirm password phải giống password.

**Processing:**

* Backend kiểm tra username.
* Backend hash password bằng BCrypt hoặc PBKDF2.
* Backend tạo user mới với role mặc định là `PLAYER`.
* Backend tạo player profile.
* Backend tạo wallet mặc định.

**Priority:** Must Have.

---

## FR-AUTH-02: Login

**Mô tả:**
Player/Admin có thể đăng nhập bằng username và password.

**Input:**

* Username.
* Password.

**Validation:**

* Username tồn tại.
* Password đúng.
* Account không bị khóa.

**Processing:**

* Backend verify password hash.
* Backend tạo JWT có expiry time.
* Backend trả về user info, role, wallet balance và profile.

**Priority:** Must Have.

---

## FR-AUTH-03: Logout

**Mô tả:**
Player có thể logout khỏi game.

**Processing:**

* Unity xóa token local.
* Người dùng quay về Login Screen hoặc Main Menu.

**Priority:** Should Have.

---

## FR-AUTH-04: Role-based Authorization

**Mô tả:**
Hệ thống phải phân quyền giữa `PLAYER` và `ADMIN`.

| Role   | Quyền                                                    |
| ------ | -------------------------------------------------------- |
| PLAYER | Chơi game, vào room, mua item, equip item, xem inventory |
| ADMIN  | Quản lý shop item, xem match logs, xem AI logs           |

**Priority:** Must Have.

---

## FR-AUTH-05: Seed Admin Account

**Mô tả:**
Admin account được tạo bằng seed data khi khởi tạo database hoặc chạy migration.

**Thông tin đề xuất:**

| Field         | Value                        |
| ------------- | ---------------------------- |
| username      | admin                        |
| role          | ADMIN                        |
| status        | ACTIVE                       |
| password      | Không lưu plain text         |
| password_hash | Hash bằng BCrypt hoặc PBKDF2 |

**Rule:**

* Admin account không tạo bằng luồng register thường.
* Password admin phải được hash.
* Không commit plain password thật vào repository public.
* Admin API bắt buộc kiểm tra role `ADMIN`.

**Priority:** Must Have.

---

# 6.2 Player Profile Module

## FR-PROFILE-01: Load Player Profile

**Mô tả:**
Sau khi login, Unity client phải load profile của player.

**Data trả về:**

* User ID.
* Username.
* Display name.
* Wallet balance.
* Owned items.
* Equipped items.
* Basic match statistics.

**Priority:** Must Have.

---

## FR-PROFILE-02: Update Equipped Cosmetics

**Mô tả:**
Player có thể equip cosmetic item đã sở hữu.

**Validation:**

* Player đã đăng nhập.
* Item tồn tại.
* Item thuộc inventory của player.
* Item chưa bị archived.
* Mỗi category chỉ có 1 item được equip tại một thời điểm.

**Priority:** Must Have.

---

# 6.3 Multiplayer Session and Lobby Module

## FR-LOBBY-01: Create Room

**Mô tả:**
Player có thể tạo room trong Unity.

**Input:**

* Room name hoặc auto-generated room code.
* Max players mặc định: 4.

**Processing:**

* Photon Fusion tạo session.
* Player tạo room trở thành host.
* Host được đưa vào lobby.

**Priority:** Must Have.

---

## FR-LOBBY-02: Join Room

**Mô tả:**
Player khác có thể join room bằng room code hoặc session name.

**Validation:**

* Room tồn tại.
* Room chưa full.
* Match chưa bắt đầu.
* Player đã login.

**Failure cases:**

* Room không tồn tại.
* Room đã full.
* Match đã bắt đầu.
* Network error.

**Priority:** Must Have.

---

## FR-LOBBY-03: Lobby Player List

**Mô tả:**
Lobby phải hiển thị danh sách players.

**Data hiển thị:**

* Player name.
* Ready status.
* Host indicator.
* Equipped cosmetic preview nếu có.

**Priority:** Must Have.

---

## FR-LOBBY-04: Ready Status

**Mô tả:**
Player có thể ready/unready trong lobby.

**Rule:**

* Host có thể không cần ready.
* Non-host players phải ready.
* Host chỉ được start game khi đủ điều kiện.

**Priority:** Must Have.

---

## FR-LOBBY-05: Host Start Game

**Mô tả:**
Chỉ host được quyền start game.

**Validation:**

* Có tối thiểu 2 players.
* Tối đa 4 players.
* Tất cả non-host players đã ready.
* Match chưa bắt đầu.

**Priority:** Must Have.

---

## FR-LOBBY-06: Host Disconnect in Lobby

**Mô tả:**
Nếu host disconnect trong lobby:

* Room bị đóng.
* Clients quay về Main Menu.
* Hiển thị thông báo: `Host disconnected. Room closed.`

**Priority:** Must Have.

---

## FR-LOBBY-07: Client Disconnect in Lobby

**Mô tả:**
Nếu client disconnect trong lobby:

* Player bị xóa khỏi lobby list.
* Ready state được cập nhật lại.

**Priority:** Must Have.

---

## FR-LOBBY-08: Prevent Late Join

**Mô tả:**
Không cho player join room khi match đã bắt đầu.

**Priority:** Must Have.

---

# 6.4 Player Gameplay Module

## FR-PLAYER-01: Player Movement

**Mô tả:**
Player có thể điều khiển nhân vật ở góc nhìn thứ nhất.

**Actions:**

* Walk.
* Sprint.
* Crouch.
* Look around.
* Interact.
* Hide.
* Revive.

**Priority:** Must Have.

---

## FR-PLAYER-02: Player Movement Sync

**Mô tả:**
Vị trí, hướng nhìn và trạng thái cơ bản của player phải được đồng bộ giữa các máy.

**Networked states:**

* Position.
* Rotation.
* Movement state.
* Downed state.
* Hidden state.
* Disconnected state.

**Priority:** Must Have.

---

## FR-PLAYER-03: Interaction System

**Mô tả:**
Player có thể tương tác với object trong map.

**Interactable objects:**

* Fuse.
* Power box.
* Door.
* Locked door.
* Access card.
* Escape door.
* Hiding spot.
* Downed teammate.
* Escape zone.

**Validation:**

* Player đứng đủ gần object.
* Object còn active.
* Interaction không bị cooldown.
* Host validate object state.

**Priority:** Must Have.

---

## FR-PLAYER-04: Sprint Noise Event

**Mô tả:**
Khi player sprint nhiều hoặc thực hiện hành động gây tiếng động, hệ thống tạo noise event.

**Noise event gồm:**

* Player ID.
* Position.
* Noise level.
* Timestamp.
* Noise type.

**Noise sources:**

* Sprint.
* Door interaction.
* Failed interaction nếu có.
* Objective interaction nếu cần.

**Priority:** Must Have.

---

## FR-PLAYER-05: Hiding System

**Mô tả:**
Player có thể trốn tại hiding spot.

**Rule:**

* Khi hidden, player giảm khả năng bị phát hiện bởi vision.
* Monster không được biết chính xác vị trí player nếu không có evidence hợp lệ.
* Nếu player thường xuyên dùng cùng hiding spot, Adaptive AI có thể khiến monster inspect hiding spot đó.

**Priority:** Must Have.

---

## FR-PLAYER-06: Downed State

**Mô tả:**
Khi bị monster bắt, player chuyển sang trạng thái `DOWNED`.

**Effect:**

* Player không thể di chuyển.
* Player có thể chờ teammate revive.
* Nếu hết revive timer, player bị eliminated.

**Priority:** Must Have.

---

## FR-PLAYER-07: Revive System

**Mô tả:**
Teammate có thể revive player đang downed.

**Validation:**

* Người revive đứng đủ gần.
* Giữ interaction đủ thời gian.
* Người revive chưa bị downed.
* Host validate revive progress.

**Priority:** Must Have.

---

## FR-PLAYER-08: Ping System

**Mô tả:**
Player có thể ping vị trí hoặc objective cho teammate.

**Priority:** Should Have.

---

# 6.5 Objective Gameplay Module

## FR-OBJ-01: Fuse Pickup

**Mô tả:**
Players phải tìm và nhặt 3 fuse trong map.

**Rule:**

* Có 6 fuse spawn locations.
* Mỗi match chọn 3 vị trí spawn fuse.
* Fuse được sync cho tất cả players.
* Khi một player pickup fuse, fuse biến mất với tất cả players.
* Nếu hai players cùng nhặt fuse, host quyết định pickup hợp lệ.

**Priority:** Must Have.

---

## FR-OBJ-02: Restore Power

**Mô tả:**
Players đem đủ 3 fuse về Power Room để restore power.

**Validation:**

* Đủ 3 fuse đã được collect.
* Player tương tác với power box.
* Host validate objective state.

**Result:**

* Power restored.
* Security Room được unlock.

**Priority:** Must Have.

---

## FR-OBJ-03: Unlock Security Room

**Mô tả:**
Security Room bị khóa cho đến khi power restored.

**Rule:**

* Trước khi restore power: không vào được.
* Sau khi restore power: door mở hoặc unlock.

**Priority:** Must Have.

---

## FR-OBJ-04: Retrieve Access Card

**Mô tả:**
Sau khi Security Room mở, player có thể lấy Access Card.

**Rule:**

* Access Card nằm trong Security Room.
* Chỉ cần 1 access card cho team.
* Access Card state được sync.

**Priority:** Must Have.

---

## FR-OBJ-05: Open Escape Door

**Mô tả:**
Player dùng Access Card để mở Escape Door.

**Validation:**

* Team đã có Access Card.
* Player tương tác với Escape Door.
* Host validate state.

**Priority:** Must Have.

---

## FR-OBJ-06: Reach Escape Zone

**Mô tả:**
Sau khi Escape Door mở, player cần chạy đến Escape Zone.

**Win condition:**

* Ít nhất 1 player còn sống vào Escape Zone.
* Objective flow đã hoàn thành.

**Priority:** Must Have.

---

## FR-OBJ-07: Dual-switch Puzzle

**Mô tả:**
Puzzle phụ yêu cầu 2 players kích hoạt 2 switch trong một khoảng thời gian ngắn.

**Priority:** Should Have.

---

# 6.6 Monster AI Module

## FR-AI-01: Monster FSM

**Mô tả:**
Monster AI phải hoạt động theo Finite State Machine.

| State       | Mô tả                                                 |
| ----------- | ----------------------------------------------------- |
| Patrol      | Monster đi tuần theo waypoint                         |
| Investigate | Monster đi đến vị trí có noise hoặc dấu hiệu nghi ngờ |
| Chase       | Monster đuổi theo player đã phát hiện                 |
| Search      | Monster tìm quanh last known position                 |
| Down Player | Monster tấn công và làm player bị downed              |
| Return      | Monster quay lại patrol route                         |
| Ambush      | Monster đón đầu route, Should Have                    |

**Priority:**
Must Have cho Patrol, Investigate, Chase, Search, Down Player, Return.
Should Have cho Ambush.

---

## FR-AI-02: NavMesh Navigation

**Mô tả:**
Monster di chuyển bằng NavMesh.

**Requirement:**

* Monster không đi xuyên tường.
* Monster đi được giữa các khu vực chính.
* Monster có thể đến patrol waypoints.
* Monster có tốc độ khác nhau theo state.

**Priority:** Must Have.

---

## FR-AI-03: Vision Detection

**Mô tả:**
Monster có thể phát hiện player bằng field-of-view.

**Điều kiện phát hiện:**

* Player nằm trong vision range.
* Player nằm trong vision angle.
* Không có wall/obstacle chặn raycast.
* Player không hidden hoặc hidden nhưng bị inspect hợp lệ.

**Priority:** Must Have.

---

## FR-AI-04: Noise Detection

**Mô tả:**
Monster phản ứng với noise events.

**Behavior:**

* Nếu đang Patrol và nhận noise event, chuyển sang Investigate.
* Nếu noise mạnh hoặc lặp lại nhiều lần, có thể ưu tiên investigate/chase player đó.

**Priority:** Must Have.

---

## FR-AI-05: Last Known Position

**Mô tả:**
Khi monster mất dấu player, nó lưu last known position.

**Behavior:**

* Chase → Search.
* Search quanh last known position.
* Nếu không tìm thấy player, Return/Patrol.

**Priority:** Must Have.

---

# 6.7 Adaptive AI Module

## FR-ADAI-01: Telemetry Collection

**Mô tả:**
Hệ thống phải thu thập telemetry của player trong match.

| Telemetry               | Mô tả                            |
| ----------------------- | -------------------------------- |
| Sprint count            | Số lần player sprint             |
| Sprint duration         | Tổng thời gian sprint            |
| Noise events            | Số lần tạo tiếng ồn              |
| Isolation time          | Thời gian player đi xa teammate  |
| Objective item carrying | Player đang cầm fuse/access card |
| Recent detection        | Player vừa bị monster phát hiện  |
| Hiding spot usage       | Lịch sử sử dụng hiding spot      |
| Downed count            | Số lần bị downed                 |
| Revive count            | Số lần revive teammate           |

**Priority:** Must Have.

---

## FR-ADAI-02: Target Score Calculation

**Mô tả:**
Monster chọn target dựa trên TargetScore.

**Công thức:**

```text
TargetScore =
  NoiseScore * 0.35
+ IsolationScore * 0.25
+ ObjectiveItemScore * 0.20
+ RecentDetectionScore * 0.10
+ HidingPatternScore * 0.10
```

| Thành phần           | Trọng số | Ý nghĩa                          |
| -------------------- | -------: | -------------------------------- |
| NoiseScore           |     0.35 | Player tạo nhiều tiếng ồn        |
| IsolationScore       |     0.25 | Player đi xa đồng đội            |
| ObjectiveItemScore   |     0.20 | Player đang giữ fuse/access card |
| RecentDetectionScore |     0.10 | Player vừa bị phát hiện          |
| HidingPatternScore   |     0.10 | Player lặp lại thói quen trốn    |

**Priority:** Must Have.

---

## FR-ADAI-03: Noise Priority

**Mô tả:**
Player tạo nhiều noise sẽ bị monster ưu tiên investigate hoặc chase.

**Rule đề xuất:**

* Nếu player sprint nhiều lần trong 60 giây, NoiseScore tăng.
* Monster ưu tiên investigate vị trí của player đó.
* AI log ghi reason: `HIGH_NOISE_PRIORITY`.

**Priority:** Must Have.

---

## FR-ADAI-04: Isolation Targeting

**Mô tả:**
Player đi lẻ xa teammate dễ bị monster chọn làm target.

**Rule đề xuất:**

* Nếu player cách teammate gần nhất hơn 12m trong ít nhất 10 giây, IsolationScore tăng.
* Monster có thể ưu tiên player này nếu có thêm evidence như noise hoặc recent detection.

**Priority:** Must Have.

---

## FR-ADAI-05: Hide Spot Learning

**Mô tả:**
Nếu player dùng cùng hiding spot nhiều lần, monster có khả năng inspect hiding spot đó nhiều hơn.

**Rule đề xuất:**

* Nếu player dùng cùng hiding spot từ 2 lần trở lên trong một match, HidingPatternScore tăng.
* Monster có thể chuyển sang Search/Inspect tại hiding spot đó.
* AI log ghi reason: `REPEATED_HIDE_SPOT`.

**Priority:** Must Have.

---

## FR-ADAI-06: Route Ambush

**Mô tả:**
Monster có thể đón đầu ở route player thường đi.

**Priority:** Should Have.

---

## FR-ADAI-07: AI Behavior Logging

**Mô tả:**
Các decision quan trọng của monster AI phải được lưu log.

**Log fields:**

* Match ID.
* Target user ID nếu có.
* AI state.
* Trigger reason.
* Target score.
* Noise score.
* Isolation score.
* Objective item score.
* Recent detection score.
* Hiding pattern score.
* Position.
* Timestamp.

**Priority:** Must Have.

---

# 6.8 Match Result, Reward and Anti-cheat Module

## FR-RESULT-01: Result Screen

**Mô tả:**
Sau khi match kết thúc, Unity hiển thị Result Screen.

**Data hiển thị:**

* Result: WIN/LOSE/HOST_DISCONNECTED.
* Match duration.
* Objective completion.
* Detection count.
* Downed count.
* Revive count.
* Monster chase count.
* ECHO Credits earned.
* Adaptive AI behaviors triggered.

**Priority:** Must Have.

---

## FR-RESULT-02: Raw Match Stats Submission

**Mô tả:**
Unity/host chỉ gửi raw match stats về backend, không tự quyết định reward cuối cùng.

**Raw data gồm:**

* matchCode hoặc matchId.
* startedAt.
* endedAt.
* durationSeconds.
* result.
* objectiveCompletion.
* player stats.
* AI logs.
* raw contribution data nếu có.

**Priority:** Must Have.

---

## FR-RESULT-03: Backend Reward Calculation

**Mô tả:**
Backend phải validate hoặc tính lại reward dựa trên raw match stats.

**Reward đề xuất:**

| Điều kiện                      |  Reward |
| ------------------------------ | ------: |
| Match win                      |    +100 |
| Player survived                |     +50 |
| Revive teammate                | +20/lần |
| Objective contribution         |     +30 |
| Match lose participation       |     +20 |
| Reward tối đa mỗi player/match |     250 |

**Security rule:**

* Client không được tự cộng ECHO Credits.
* Host không được tự quyết định reward cuối cùng.
* Backend là nơi duy nhất cộng ECHO Credits vào wallet.
* Wallet update phải chạy trong database transaction.

**Priority:** Must Have.

---

## FR-RESULT-04: Reward Anti-cheat Validation

**Mô tả:**
Backend phải chống gian lận reward ở mức server-side.

| Validation       | Rule                                                          |
| ---------------- | ------------------------------------------------------------- |
| Match duration   | Không nhỏ hơn 1 phút hoặc lớn hơn 15 phút trong MVP           |
| Player count     | Phải từ 2 đến 4                                               |
| Match result     | Chỉ nhận WIN / LOSE / HOST_DISCONNECTED                       |
| Reward max       | Không vượt quá reward tối đa                                  |
| Duplicate submit | Cùng matchCode/matchId không được cộng reward nhiều lần       |
| Player validity  | userId phải tồn tại và thuộc match log                        |
| Wallet update    | Chỉ backend update                                            |
| Transaction      | Match log + player log + wallet update phải atomic nếu có thể |

**Error code đề xuất:**

| Error Code                 | Trường hợp                |
| -------------------------- | ------------------------- |
| INVALID_MATCH_RESULT       | Match result không hợp lệ |
| INVALID_MATCH_DURATION     | Duration không hợp lệ     |
| INVALID_PLAYER_COUNT       | Player count không hợp lệ |
| INVALID_REWARD_AMOUNT      | Reward vượt giới hạn      |
| DUPLICATE_MATCH_SUBMISSION | Match đã submit trước đó  |
| PLAYER_NOT_IN_MATCH        | Player không thuộc match  |
| WALLET_UPDATE_FAILED       | Cộng tiền thất bại        |

**Priority:** Must Have.

---

## FR-RESULT-05: Submit Match Log Idempotency

**Mô tả:**
API submit match log phải chống submit trùng bằng `matchCode` hoặc `matchId`.

**Rule:**

* Nếu matchCode/matchId đã submit, backend không cộng reward lần hai.
* Backend trả lỗi `DUPLICATE_MATCH_SUBMISSION`.
* Nếu match đã lưu nhưng reward chưa xử lý, backend xử lý theo trạng thái `reward_processed`.

**Priority:** Must Have.

---

# 6.9 Virtual Shop Module

## FR-SHOP-01: View Shop Catalog

**Mô tả:**
Player có thể xem danh sách item trong shop.

**Item fields:**

* Item ID.
* Name.
* Description.
* Category.
* Price.
* Image URL/reference.
* Ownership status.
* Enabled status.

**Priority:** Must Have.

---

## FR-SHOP-02: Shop MVP 15 Cosmetic Items

**Mô tả:**
Shop MVP phải có tối thiểu **15 cosmetic items** và mỗi item phải có ảnh thật hoặc image reference hợp lệ.

**Phân bổ item:**

| Category        | Số lượng |
| --------------- | -------: |
| FLASHLIGHT_SKIN |        4 |
| OUTFIT          |        4 |
| BADGE           |        4 |
| PROFILE_ICON    |        3 |
| Tổng            |       15 |

**Mỗi item cần có:**

| Field       | Mô tả                                           |
| ----------- | ----------------------------------------------- |
| name        | Tên item                                        |
| description | Mô tả item                                      |
| category    | FLASHLIGHT_SKIN / OUTFIT / BADGE / PROFILE_ICON |
| price       | Giá bằng ECHO Credits                           |
| image_url   | Link hoặc path ảnh item                         |
| is_enabled  | Có cho mua hay không                            |
| is_archived | Có bị ẩn khỏi shop hay không                    |

**Priority:** Must Have.

---

## FR-SHOP-03: Shop Image Requirement

**Mô tả:**
Hệ thống phải hỗ trợ lưu image reference cho shop item.

**Rule:**

* Admin nhập `image_url` khi tạo hoặc cập nhật item.
* Ảnh có thể lưu trong static folder của backend, cloud storage hoặc URL hợp lệ.
* Unity shop UI load ảnh từ `image_url`.
* Nếu ảnh lỗi, Unity hiển thị placeholder nhưng vẫn hiển thị tên, mô tả và giá item.

**Priority:** Must Have cho image URL.
**Priority:** Should Have cho upload file ảnh trực tiếp.

---

## FR-SHOP-04: Purchase Item

**Mô tả:**
Player có thể mua cosmetic item bằng ECHO Credits.

**Validation:**

* Player đã login.
* Item tồn tại.
* Item enabled.
* Item chưa bị archived.
* Player chưa sở hữu item.
* Wallet đủ balance.

**Transaction rule:**

* Trừ wallet.
* Thêm item vào inventory.
* Tạo purchase transaction.
* Tạo wallet transaction.
* Nếu bất kỳ bước nào fail, rollback toàn bộ.

**Priority:** Must Have.

---

## FR-SHOP-05: View Inventory

**Mô tả:**
Player có thể xem item đã sở hữu.

**Priority:** Must Have.

---

## FR-SHOP-06: Equip Cosmetic

**Mô tả:**
Player có thể equip cosmetic đã sở hữu.

**Rule:**

* Chỉ equip item trong inventory.
* Mỗi category chỉ có 1 item đang equip.

**Priority:** Must Have.

---

## FR-SHOP-07: Lobby Cosmetic Sync

**Mô tả:**
Cosmetic được equip có thể hiển thị trong lobby hoặc match.

**Priority:** Should Have.

---

# 6.10 Admin Dashboard Module

## FR-ADMIN-01: Admin Login

**Mô tả:**
Administrator đăng nhập bằng account role `ADMIN`.

**Priority:** Must Have.

---

## FR-ADMIN-02: Create Shop Item

**Mô tả:**
Admin có thể tạo shop item mới.

**Fields:**

* Name.
* Description.
* Category.
* Price.
* Image URL/reference.
* Enabled status.

**Priority:** Must Have.

---

## FR-ADMIN-03: Update Shop Item

**Mô tả:**
Admin có thể cập nhật thông tin shop item, bao gồm image URL.

**Priority:** Must Have.

---

## FR-ADMIN-04: Disable/Archive Shop Item

**Mô tả:**
Admin có thể disable hoặc archive item.

**Rule:**

* Disabled item không thể mua.
* Archived item không hiển thị trong shop.
* Player đã sở hữu item disabled vẫn có thể thấy trong inventory, tùy rule triển khai.

**Priority:** Must Have.

---

## FR-ADMIN-05: Preview Shop Item Image

**Mô tả:**
Admin dashboard hiển thị preview ảnh item dựa trên image URL.

**Priority:** Should Have.

---

## FR-ADMIN-06: Upload Shop Item Image

**Mô tả:**
Admin có thể upload file ảnh trực tiếp.

**Priority:** Should Have.

Trong MVP, nhóm có thể dùng image URL hoặc static file path thay vì xây dựng hệ thống upload file phức tạp.

---

## FR-ADMIN-07: View Match Logs

**Mô tả:**
Admin có thể xem danh sách match logs.

**Priority:** Must Have.

---

## FR-ADMIN-08: View AI Behavior Logs

**Mô tả:**
Admin có thể xem AI logs để chứng minh Adaptive AI hoạt động.

**Priority:** Must Have.

---

## FR-ADMIN-09: Filter/Search Logs

**Mô tả:**
Admin có thể filter/search logs theo player, match result, AI reason hoặc thời gian.

**Priority:** Should Have.

---

# 7. Non-functional Requirements

## 7.1 Performance

| ID          | Requirement                                         |
| ----------- | --------------------------------------------------- |
| NFR-PERF-01 | Game target tối thiểu 45 FPS trên máy test          |
| NFR-PERF-02 | Room hỗ trợ 2–4 players                             |
| NFR-PERF-03 | Demo tối thiểu 2 players ổn định                    |
| NFR-PERF-04 | Demo cuối dùng 3–4 laptop thật                      |
| NFR-PERF-05 | Match timer mặc định 15 phút                        |
| NFR-PERF-06 | Shop API response dưới 2 giây trong môi trường demo |
| NFR-PERF-07 | Monster AI không gây tụt FPS nghiêm trọng           |
| NFR-PERF-08 | Scene loading không quá 15 giây trên máy test       |

---

## 7.2 Environment

| ID         | Requirement                                              |
| ---------- | -------------------------------------------------------- |
| NFR-ENV-01 | Tất cả Unity developers phải dùng cùng Unity version     |
| NFR-ENV-02 | Unity version chính là Unity 6.3 LTS                     |
| NFR-ENV-03 | Unity fallback là Unity 6.0 LTS nếu có lỗi compatibility |
| NFR-ENV-04 | Backend phải chạy được local và deployed online          |
| NFR-ENV-05 | PostgreSQL database phải chạy được local và cloud        |
| NFR-ENV-06 | Unity build phải chạy được trên Windows 10/11 64-bit     |

---

## 7.3 Deployment

| ID         | Requirement                                                     |
| ---------- | --------------------------------------------------------------- |
| NFR-DEP-01 | Backend API phải deploy online trước demo                       |
| NFR-DEP-02 | PostgreSQL cloud dùng Neon hoặc Supabase                        |
| NFR-DEP-03 | Unity build trên 3–4 laptop phải kết nối được backend deployed  |
| NFR-DEP-04 | Photon App ID, region và CCU plan phải được kiểm tra trước demo |
| NFR-DEP-05 | Admin dashboard phải truy cập được từ môi trường demo           |

---

## 7.4 Reliability

| ID         | Requirement                                                                          |
| ---------- | ------------------------------------------------------------------------------------ |
| NFR-REL-01 | Purchase transaction phải atomic                                                     |
| NFR-REL-02 | Wallet balance không được âm                                                         |
| NFR-REL-03 | Inventory không được duplicate item                                                  |
| NFR-REL-04 | Host disconnect trong match phải kết thúc match an toàn                              |
| NFR-REL-05 | Client disconnect trong match phải được mark disconnected/eliminated                 |
| NFR-REL-06 | Backend mất kết nối thì game vẫn vào menu, nhưng shop/inventory/log disable tạm thời |
| NFR-REL-07 | Submit match log trùng không được cộng reward lần hai                                |

---

## 7.5 Security

| ID         | Requirement                                                   |
| ---------- | ------------------------------------------------------------- |
| NFR-SEC-01 | Password phải hash bằng BCrypt hoặc PBKDF2                    |
| NFR-SEC-02 | API dùng JWT authentication                                   |
| NFR-SEC-03 | JWT phải có expiry time                                       |
| NFR-SEC-04 | Admin API bắt buộc role `ADMIN`                               |
| NFR-SEC-05 | Purchase, wallet update, equip item phải validate server-side |
| NFR-SEC-06 | Client không được tự cộng ECHO Credits                        |
| NFR-SEC-07 | Backend phải validate hoặc tính lại reward                    |
| NFR-SEC-08 | Backend phải chống duplicate match submission                 |
| NFR-SEC-09 | Mọi thay đổi wallet phải được ghi log                         |
| NFR-SEC-10 | Backend phải reject hoặc clamp reward bất thường              |

---

## 7.6 Shop

| ID          | Requirement                                           |
| ----------- | ----------------------------------------------------- |
| NFR-SHOP-01 | Shop MVP có ít nhất 15 cosmetic items                 |
| NFR-SHOP-02 | Mỗi shop item có ảnh thật hoặc image reference hợp lệ |
| NFR-SHOP-03 | Unity shop UI phải xử lý được trường hợp ảnh lỗi      |
| NFR-SHOP-04 | Player không thể mua item disabled/archived           |
| NFR-SHOP-05 | Player không thể mua item đã sở hữu                   |

---

## 7.7 Maintainability

| ID          | Requirement                                     |
| ----------- | ----------------------------------------------- |
| NFR-MAIN-01 | Unity code chia module rõ ràng                  |
| NFR-MAIN-02 | Backend dùng layered architecture               |
| NFR-MAIN-03 | API response format thống nhất                  |
| NFR-MAIN-04 | Có README hướng dẫn chạy backend và Unity build |
| NFR-MAIN-05 | Có Swagger hoặc Postman collection              |
| NFR-MAIN-06 | Code có naming convention thống nhất            |

---

## 7.8 Usability

| ID         | Requirement                                             |
| ---------- | ------------------------------------------------------- |
| NFR-USE-01 | UI dễ hiểu cho người chơi mới                           |
| NFR-USE-02 | Có interaction prompt khi đứng gần object               |
| NFR-USE-03 | HUD hiển thị objective hiện tại                         |
| NFR-USE-04 | Error message rõ ràng khi login/shop fail               |
| NFR-USE-05 | Result screen dễ hiểu, có reward và AI behavior summary |

---

# 8. Database Requirements

Database chính thức: **PostgreSQL**.

## 8.1 Users

| Field         | Type         | Note            |
| ------------- | ------------ | --------------- |
| id            | uuid         | Primary key     |
| username      | varchar(100) | Unique          |
| password_hash | text         | Hashed password |
| role          | varchar(20)  | PLAYER/ADMIN    |
| status        | varchar(20)  | ACTIVE/LOCKED   |
| created_at    | timestamp    |                 |
| updated_at    | timestamp    |                 |

---

## 8.2 PlayerProfiles

| Field         | Type         | Note        |
| ------------- | ------------ | ----------- |
| id            | uuid         | Primary key |
| user_id       | uuid         | FK Users    |
| display_name  | varchar(100) |             |
| total_matches | int          | default 0   |
| total_wins    | int          | default 0   |
| created_at    | timestamp    |             |
| updated_at    | timestamp    |             |

---

## 8.3 Wallets

| Field      | Type      | Note        |
| ---------- | --------- | ----------- |
| id         | uuid      | Primary key |
| user_id    | uuid      | FK Users    |
| balance    | int       | >= 0        |
| updated_at | timestamp |             |

---

## 8.4 ShopItems

| Field       | Type         | Note                                      |
| ----------- | ------------ | ----------------------------------------- |
| id          | uuid         | Primary key                               |
| name        | varchar(150) |                                           |
| description | text         |                                           |
| category    | varchar(50)  | FLASHLIGHT_SKIN/OUTFIT/BADGE/PROFILE_ICON |
| price       | int          | >= 0                                      |
| image_url   | text         | URL hoặc static path của ảnh item         |
| is_enabled  | boolean      |                                           |
| is_archived | boolean      |                                           |
| created_at  | timestamp    |                                           |
| updated_at  | timestamp    |                                           |

---

## 8.5 Inventories

| Field        | Type      | Note         |
| ------------ | --------- | ------------ |
| id           | uuid      | Primary key  |
| user_id      | uuid      | FK Users     |
| shop_item_id | uuid      | FK ShopItems |
| acquired_at  | timestamp |              |

**Constraint:**

```text
unique(user_id, shop_item_id)
```

---

## 8.6 EquippedItems

| Field        | Type        | Note          |
| ------------ | ----------- | ------------- |
| id           | uuid        | Primary key   |
| user_id      | uuid        | FK Users      |
| category     | varchar(50) | Item category |
| shop_item_id | uuid        | FK ShopItems  |
| equipped_at  | timestamp   |               |

**Constraint:**

```text
unique(user_id, category)
```

---

## 8.7 PurchaseTransactions

| Field          | Type        | Note                   |
| -------------- | ----------- | ---------------------- |
| id             | uuid        | Primary key            |
| user_id        | uuid        | FK Users               |
| shop_item_id   | uuid        | FK ShopItems           |
| price          | int         | Price at purchase time |
| balance_before | int         |                        |
| balance_after  | int         |                        |
| status         | varchar(20) | SUCCESS/FAILED         |
| failure_reason | text        | nullable               |
| created_at     | timestamp   |                        |

---

## 8.8 MatchLogs

| Field                | Type         | Note                       |
| -------------------- | ------------ | -------------------------- |
| id                   | uuid         | Primary key                |
| match_code           | varchar(100) | Unique                     |
| submitted_by_user_id | uuid         | Host/client submit log     |
| started_at           | timestamp    |                            |
| ended_at             | timestamp    |                            |
| duration_seconds     | int          |                            |
| result               | varchar(30)  | WIN/LOSE/HOST_DISCONNECTED |
| player_count         | int          |                            |
| objective_completion | numeric(5,2) | 0–1 hoặc %                 |
| reward_processed     | boolean      | Đã xử lý reward hay chưa   |
| created_at           | timestamp    |                            |

**Constraint đề xuất:**

```text
unique(match_code)
```

---

## 8.9 PlayerMatchLogs

| Field           | Type    | Note                         |
| --------------- | ------- | ---------------------------- |
| id              | uuid    | Primary key                  |
| match_log_id    | uuid    | FK MatchLogs                 |
| user_id         | uuid    | FK Users                     |
| survived        | boolean |                              |
| disconnected    | boolean |                              |
| detection_count | int     |                              |
| downed_count    | int     |                              |
| revive_count    | int     |                              |
| reward_earned   | int     | Reward backend tính/validate |
| reward_reason   | text    | Optional                     |

---

## 8.10 AIBehaviorLogs

| Field                  | Type          | Note                                   |
| ---------------------- | ------------- | -------------------------------------- |
| id                     | uuid          | Primary key                            |
| match_log_id           | uuid          | FK MatchLogs                           |
| target_user_id         | uuid          | nullable                               |
| ai_state               | varchar(50)   | Patrol/Investigate/Chase/Search        |
| trigger_reason         | varchar(100)  | HIGH_NOISE_PRIORITY/REPEATED_HIDE_SPOT |
| target_score           | numeric(5,2)  | nullable                               |
| noise_score            | numeric(5,2)  | nullable                               |
| isolation_score        | numeric(5,2)  | nullable                               |
| objective_item_score   | numeric(5,2)  | nullable                               |
| recent_detection_score | numeric(5,2)  | nullable                               |
| hiding_pattern_score   | numeric(5,2)  | nullable                               |
| position_x             | numeric(10,2) |                                        |
| position_y             | numeric(10,2) |                                        |
| position_z             | numeric(10,2) |                                        |
| created_at             | timestamp     |                                        |

---

## 8.11 WalletTransactions

Bảng này dùng để audit mọi thay đổi wallet.

| Field            | Type        | Note                                   |
| ---------------- | ----------- | -------------------------------------- |
| id               | uuid        | Primary key                            |
| user_id          | uuid        | FK Users                               |
| transaction_type | varchar(30) | PURCHASE / MATCH_REWARD / ADMIN_ADJUST |
| amount           | int         | Có thể âm hoặc dương                   |
| balance_before   | int         | Balance trước giao dịch                |
| balance_after    | int         | Balance sau giao dịch                  |
| reference_id     | uuid        | MatchLogId hoặc PurchaseTransactionId  |
| description      | text        | Mô tả                                  |
| created_at       | timestamp   | Thời điểm tạo                          |

**Rule:**

* Purchase tạo WalletTransaction amount âm.
* Match reward tạo WalletTransaction amount dương.
* Wallet không được âm.
* Mọi thay đổi wallet phải có log.

---

# 9. API Requirements

## 9.1 API Response Format chuẩn

### Success response

```json
{
  "success": true,
  "message": "Purchase successfully",
  "data": {
    "walletBalance": 400
  }
}
```

### Error response

```json
{
  "success": false,
  "message": "Not enough balance",
  "errorCode": "INSUFFICIENT_BALANCE"
}
```

---

## 9.2 HTTP Status Code

| Status code | Trường hợp                                                               |
| ----------: | ------------------------------------------------------------------------ |
|         200 | Request thành công                                                       |
|         201 | Tạo mới thành công                                                       |
|         400 | Request sai validation                                                   |
|         401 | Chưa đăng nhập hoặc token sai                                            |
|         403 | Không đủ quyền                                                           |
|         404 | Không tìm thấy resource                                                  |
|         409 | Xung đột dữ liệu, ví dụ username trùng/item đã sở hữu/match submit trùng |
|         500 | Lỗi server                                                               |

---

## 9.3 Authentication APIs

### POST `/api/auth/register`

Request:

```json
{
  "username": "player01",
  "password": "123456",
  "confirmPassword": "123456"
}
```

Response:

```json
{
  "success": true,
  "message": "Register successfully",
  "data": {
    "username": "player01"
  }
}
```

---

### POST `/api/auth/login`

Request:

```json
{
  "username": "player01",
  "password": "123456"
}
```

Response:

```json
{
  "success": true,
  "message": "Login successfully",
  "data": {
    "accessToken": "jwt_token",
    "user": {
      "id": "uuid",
      "username": "player01",
      "role": "PLAYER"
    },
    "wallet": {
      "balance": 500
    }
  }
}
```

---

## 9.4 Player APIs

### GET `/api/player/me`

Header:

```text
Authorization: Bearer <token>
```

Response:

```json
{
  "success": true,
  "message": "Profile loaded",
  "data": {
    "id": "uuid",
    "username": "player01",
    "displayName": "Player 01",
    "walletBalance": 500,
    "equippedItems": []
  }
}
```

---

## 9.5 Shop APIs

### GET `/api/shop/items`

Response:

```json
{
  "success": true,
  "message": "Shop items loaded",
  "data": [
    {
      "id": "uuid",
      "name": "Red Flashlight",
      "description": "Red flashlight skin",
      "category": "FLASHLIGHT_SKIN",
      "price": 100,
      "imageUrl": "/images/shop/red_flashlight.png",
      "isOwned": false,
      "isEnabled": true
    }
  ]
}
```

---

### POST `/api/shop/purchase`

Request:

```json
{
  "shopItemId": "uuid"
}
```

Success response:

```json
{
  "success": true,
  "message": "Purchase successfully",
  "data": {
    "walletBalance": 400
  }
}
```

Error response:

```json
{
  "success": false,
  "message": "Not enough balance",
  "errorCode": "INSUFFICIENT_BALANCE"
}
```

---

## 9.6 Inventory APIs

### GET `/api/inventory/me`

Response:

```json
{
  "success": true,
  "message": "Inventory loaded",
  "data": [
    {
      "itemId": "uuid",
      "name": "Red Flashlight",
      "category": "FLASHLIGHT_SKIN",
      "imageUrl": "/images/shop/red_flashlight.png",
      "equipped": true
    }
  ]
}
```

---

### POST `/api/inventory/equip`

Request:

```json
{
  "shopItemId": "uuid"
}
```

Response:

```json
{
  "success": true,
  "message": "Item equipped",
  "data": {
    "shopItemId": "uuid"
  }
}
```

---

## 9.7 Match Log APIs

### POST `/api/matches/logs`

**Rule:**

* Unity/host gửi raw stats.
* Backend validate dữ liệu.
* Backend tính lại reward.
* Backend chống duplicate submit.
* Backend cộng wallet server-side.

Request:

```json
{
  "matchCode": "ROOM123",
  "startedAt": "2026-07-10T10:00:00Z",
  "endedAt": "2026-07-10T10:15:00Z",
  "durationSeconds": 900,
  "result": "WIN",
  "playerCount": 2,
  "objectiveCompletion": 1.0,
  "players": [
    {
      "userId": "uuid",
      "survived": true,
      "disconnected": false,
      "detectionCount": 3,
      "downedCount": 1,
      "reviveCount": 2,
      "objectiveContribution": 1
    }
  ],
  "aiLogs": [
    {
      "targetUserId": "uuid",
      "aiState": "CHASE",
      "triggerReason": "HIGH_NOISE_PRIORITY",
      "targetScore": 0.82,
      "noiseScore": 0.9,
      "isolationScore": 0.4,
      "objectiveItemScore": 0.3,
      "recentDetectionScore": 0.7,
      "hidingPatternScore": 0.1,
      "positionX": 12.1,
      "positionY": 0,
      "positionZ": 9.5
    }
  ]
}
```

Success response:

```json
{
  "success": true,
  "message": "Match log submitted",
  "data": {
    "matchLogId": "uuid",
    "playerRewards": [
      {
        "userId": "uuid",
        "rewardEarned": 170,
        "walletBalance": 670
      }
    ]
  }
}
```

Duplicate response:

```json
{
  "success": false,
  "message": "Match has already been submitted",
  "errorCode": "DUPLICATE_MATCH_SUBMISSION"
}
```

Invalid reward response:

```json
{
  "success": false,
  "message": "Invalid reward amount",
  "errorCode": "INVALID_REWARD_AMOUNT"
}
```

---

## 9.8 Admin APIs

### POST `/api/admin/shop-items`

Role required: `ADMIN`

Request:

```json
{
  "name": "Red Flashlight",
  "description": "Red flashlight skin",
  "category": "FLASHLIGHT_SKIN",
  "price": 100,
  "imageUrl": "/images/shop/red_flashlight.png",
  "isEnabled": true
}
```

---

### PUT `/api/admin/shop-items/{id}`

Request:

```json
{
  "name": "Red Flashlight V2",
  "description": "Updated red flashlight skin",
  "category": "FLASHLIGHT_SKIN",
  "price": 120,
  "imageUrl": "/images/shop/red_flashlight_v2.png",
  "isEnabled": true
}
```

---

### PATCH `/api/admin/shop-items/{id}/disable`

Disable item.

---

### PATCH `/api/admin/shop-items/{id}/archive`

Archive item.

---

### GET `/api/admin/match-logs`

Xem match logs.

---

### GET `/api/admin/ai-logs`

Xem AI behavior logs.

---

# 10. Multiplayer Networking Requirements

## 10.1 Authority Model

| Object/System           | Authority                                   |
| ----------------------- | ------------------------------------------- |
| Player movement         | Input owner + network sync                  |
| Monster AI              | Host                                        |
| Item pickup             | Host                                        |
| Fuse state              | Host                                        |
| Door state              | Host                                        |
| Puzzle state            | Host                                        |
| Revive validation       | Host                                        |
| Win/Lose condition      | Host                                        |
| Match result submission | Host/client gửi raw stats, backend validate |

---

## 10.2 Networked Objects

Các object cần sync qua Photon Fusion:

* Player character.
* Monster.
* Fuse.
* Power box.
* Door.
* Locked door.
* Access card.
* Escape door.
* Hiding spot state.
* Objective manager.
* Match state manager.
* Revive state.
* Lobby ready state.

---

## 10.3 Multiplayer Edge Cases

| Case                             | Rule                                                    |
| -------------------------------- | ------------------------------------------------------- |
| Host disconnect trong lobby      | Room đóng, client quay về main menu                     |
| Host disconnect trong match      | Match kết thúc, result là `HOST_DISCONNECTED`           |
| Client disconnect trong lobby    | Xóa player khỏi lobby list                              |
| Client disconnect trong match    | Player bị marked disconnected/eliminated                |
| Player join khi match đã bắt đầu | Không cho join                                          |
| Hai player cùng nhặt fuse        | Host quyết định pickup hợp lệ                           |
| Player spam interact             | Host validate cooldown và object state                  |
| Backend mất kết nối              | Game vẫn vào menu, nhưng shop/inventory/log tạm disable |
| Submit match log fail            | Hiển thị warning hoặc retry nếu kịp triển khai          |

---

# 11. UI Requirements

## 11.1 Required Screens

| Screen             | Mô tả                                      |
| ------------------ | ------------------------------------------ |
| Login Screen       | Đăng nhập                                  |
| Register Screen    | Đăng ký                                    |
| Main Menu          | Play, Shop, Inventory, Settings, Quit      |
| Create Room Screen | Tạo room                                   |
| Join Room Screen   | Join room bằng code/session name           |
| Lobby Screen       | Player list, ready, host start             |
| Loading Screen     | Chuyển scene                               |
| In-game HUD        | Objective, teammate status, prompt         |
| Pause Menu         | Resume, Settings, Leave                    |
| Shop Screen        | Catalog, item image, item detail, purchase |
| Inventory Screen   | Owned items, equip                         |
| Result Screen      | Result, stats, reward, AI summary          |
| Admin Dashboard    | CRUD shop item, view logs                  |

---

## 11.2 HUD Requirements

HUD cần hiển thị:

* Current objective.
* Teammate status.
* Downed player indicator.
* Interaction prompt.
* Revive progress.
* Escape progress.
* Match timer 15 phút.
* Optional: noise indicator.

---

## 11.3 Shop UI Requirements

Shop UI cần hiển thị:

* Item image.
* Item name.
* Item description.
* Item category.
* Item price.
* Ownership status.
* Purchase button.
* Wallet balance.

Nếu ảnh không load được:

* Hiển thị placeholder.
* Vẫn hiển thị tên, mô tả, giá và trạng thái item.

---

# 12. Testing Requirements

## 12.1 Backend Unit Test

| Test                       | Expected Result                                  |
| -------------------------- | ------------------------------------------------ |
| Register username mới      | Thành công                                       |
| Register username trùng    | Lỗi 409                                          |
| Login đúng password        | Trả JWT                                          |
| Login sai password         | Lỗi 401                                          |
| Purchase đủ tiền           | Trừ wallet + thêm inventory + wallet transaction |
| Purchase thiếu tiền        | Lỗi, wallet không đổi                            |
| Purchase item đã sở hữu    | Lỗi 409                                          |
| Equip item chưa sở hữu     | Lỗi                                              |
| Admin API với Player token | Lỗi 403                                          |
| Submit matchCode trùng     | Lỗi `DUPLICATE_MATCH_SUBMISSION`                 |

---

## 12.2 Multiplayer Test

| Test Case             | Expected Result                       |
| --------------------- | ------------------------------------- |
| Host tạo room         | Room được tạo                         |
| Client join room      | Client xuất hiện trong lobby          |
| Ready status          | Sync đúng                             |
| Host start game       | Tất cả load game scene                |
| 2 máy cùng vào match  | Movement sync                         |
| 3–4 laptop join lobby | Tất cả thấy nhau                      |
| Player pickup fuse    | Fuse biến mất với tất cả              |
| Restore power         | Objective state sync                  |
| Open escape door      | Door state sync                       |
| Host disconnect       | Match end                             |
| Client disconnect     | Player marked disconnected/eliminated |

---

## 12.3 AI Test

| Test Case                                     | Expected Result                             |
| --------------------------------------------- | ------------------------------------------- |
| Player sprint tạo noise                       | Monster chuyển Patrol → Investigate         |
| Monster thấy player trong FOV                 | Monster chuyển Investigate/Patrol → Chase   |
| Monster mất dấu player                        | Monster chuyển Chase → Search               |
| Player sprint nhiều trong 60 giây             | Monster ưu tiên investigate/chase player đó |
| Player cách teammate >12m trong 10 giây       | IsolationScore tăng                         |
| Player dùng cùng hiding spot từ 2 lần trở lên | Monster có thể inspect hiding spot đó       |
| Match kết thúc                                | AI logs được lưu vào backend                |

---

## 12.4 Deployment Test

| Test Case                     | Expected Result                     |
| ----------------------------- | ----------------------------------- |
| Backend deployed health check | API trả response thành công         |
| Unity build gọi deployed API  | Login/load profile thành công       |
| PostgreSQL cloud connection   | Backend đọc/ghi database thành công |
| Admin dashboard deployed      | Admin login và xem dashboard được   |
| 3 laptop join lobby           | Tất cả player xuất hiện trong lobby |
| 3 laptop start match          | Tất cả load vào game scene          |

---

## 12.5 Photon Test

| Test Case        | Expected Result                             |
| ---------------- | ------------------------------------------- |
| Kiểm tra App ID  | App ID đúng trong Unity config              |
| Kiểm tra region  | Các máy dùng cùng region                    |
| Kiểm tra CCU     | Không vượt giới hạn plan                    |
| Host tạo room    | Room được tạo thành công                    |
| Client join room | Join thành công bằng room code/session name |
| Host disconnect  | Match/lobby xử lý đúng rule                 |

---

## 12.6 Shop Image Test

| Test Case                  | Expected Result                                 |
| -------------------------- | ----------------------------------------------- |
| Admin tạo item có imageUrl | Item lưu thành công                             |
| Unity load item image      | Ảnh hiển thị trong shop                         |
| Image URL lỗi              | UI dùng placeholder hoặc vẫn hiển thị item text |
| Admin cập nhật imageUrl    | Unity load ảnh mới                              |
| Shop có 15 items           | Catalog hiển thị đủ item enabled                |

---

## 12.7 Reward Anti-cheat Test

| Test Case                    | Expected Result                          |
| ---------------------------- | ---------------------------------------- |
| Submit match log hợp lệ      | Backend lưu log và cộng reward           |
| Submit matchCode trùng       | Backend trả `DUPLICATE_MATCH_SUBMISSION` |
| Submit reward vượt giới hạn  | Backend reject hoặc clamp                |
| Submit player không tồn tại  | Backend reject                           |
| Submit playerCount ngoài 2–4 | Backend reject                           |
| Submit duration >15 phút MVP | Backend reject hoặc normalize theo rule  |
| Wallet update sau reward     | Có WalletTransaction                     |
| Client tự gọi update wallet  | Không có API cho phép hoặc bị reject     |

---

# 13. Acceptance Criteria

| ID           | Acceptance Criteria                                                                     |
| ------------ | --------------------------------------------------------------------------------------- |
| AC-01        | Player có thể register/login thành công bằng backend deployed                           |
| AC-02        | Unity client nhận JWT và load profile sau login                                         |
| AC-03        | Host có thể tạo room                                                                    |
| AC-04        | Player khác có thể join room bằng room code/session name                                |
| AC-05        | Lobby hiển thị player list, ready status và host indicator                              |
| AC-06        | Host chỉ start game khi đủ điều kiện                                                    |
| AC-07        | Tối thiểu 2 Unity build trên 2 máy có thể vào cùng match                                |
| AC-08        | Player movement được sync giữa các máy                                                  |
| AC-09        | Player có thể nhặt fuse và fuse biến mất với tất cả players                             |
| AC-10        | Đặt đủ 3 fuse thì power restored                                                        |
| AC-11        | Power restored thì Security Room mở khóa                                                |
| AC-12        | Player lấy Access Card và mở Escape Door                                                |
| AC-13        | Match win nếu objective hoàn thành và ít nhất 1 player sống sót vào Escape Zone         |
| AC-14        | Match lose nếu tất cả players bị eliminated                                             |
| AC-15        | Match lose nếu hết timer 15 phút                                                        |
| AC-16        | Host disconnect trong match làm match kết thúc với result `HOST_DISCONNECTED`           |
| AC-17        | Monster chuyển Patrol → Investigate khi nhận noise event                                |
| AC-18        | Monster chuyển Patrol/Investigate → Chase khi thấy player trong FOV                     |
| AC-19        | Monster chuyển Chase → Search khi mất dấu player                                        |
| AC-20        | Player sprint nhiều lần trong 60 giây sẽ bị monster ưu tiên investigate/chase hơn       |
| AC-21        | Player cách teammate gần nhất hơn 12m trong 10 giây sẽ tăng IsolationScore              |
| AC-22        | Player dùng cùng hiding spot từ 2 lần trở lên thì monster có thể inspect hiding spot đó |
| AC-23        | AI behavior decision được lưu vào backend log                                           |
| AC-24        | Result screen hiển thị duration, result, stats và reward                                |
| AC-25        | Backend lưu match log sau match                                                         |
| AC-26        | Backend lưu AI behavior log sau match hoặc trong match                                  |
| AC-27        | Player có thể xem shop catalog                                                          |
| AC-28        | Player có thể mua item nếu đủ ECHO Credits                                              |
| AC-29        | Purchase trừ wallet và thêm item vào inventory bằng transaction                         |
| AC-30        | Player không thể mua item đã sở hữu                                                     |
| AC-31        | Player không thể mua item nếu không đủ tiền                                             |
| AC-32        | Player có thể equip cosmetic đã sở hữu                                                  |
| AC-33        | Admin có thể tạo, sửa, disable/archive shop item                                        |
| AC-34        | Admin có thể xem match logs                                                             |
| AC-35        | Admin có thể xem AI behavior logs                                                       |
| AC-36        | Backend được deploy và Unity build nhiều máy có thể kết nối                             |
| AC-DEP-01    | Dự án có Unity Windows build chạy được trên ít nhất 3 laptop                            |
| AC-DEP-02    | Ít nhất 3 laptop có thể join cùng lobby trong buổi test demo                            |
| AC-DEP-03    | Backend API được deploy online                                                          |
| AC-DEP-04    | PostgreSQL database được deploy trên Neon hoặc Supabase                                 |
| AC-DEP-05    | Unity build login được với backend deployed                                             |
| AC-DEP-06    | Unity build load shop được từ backend deployed                                          |
| AC-DEP-07    | Admin dashboard truy cập được bằng account seed role ADMIN                              |
| AC-DEP-08    | Admin account được tạo bằng seed data, password được hash                               |
| AC-SHOP-01   | Shop có tối thiểu 15 cosmetic items                                                     |
| AC-SHOP-02   | Mỗi cosmetic item có image_url hoặc ảnh hiển thị được                                   |
| AC-SHOP-03   | Admin có thể tạo/cập nhật item kèm image URL                                            |
| AC-SHOP-04   | Unity shop UI hiển thị ảnh, tên, mô tả, giá và trạng thái sở hữu                        |
| AC-SEC-01    | Client/host không thể tự cộng ECHO Credits trực tiếp                                    |
| AC-SEC-02    | Backend tính lại hoặc validate reward khi submit match log                              |
| AC-SEC-03    | Backend không cộng reward lần hai nếu matchCode/matchId đã submit                       |
| AC-SEC-04    | Backend reject reward vượt giới hạn tối đa                                              |
| AC-SEC-05    | Wallet update sau match được ghi vào WalletTransactions                                 |
| AC-PHOTON-01 | Photon Fusion App ID và region được cấu hình đúng                                       |
| AC-PHOTON-02 | Photon Free 100 CCU plan đủ cho demo 3–4 laptop                                         |
| AC-TIME-01   | Project plan chia thành 4 tháng với milestone rõ ràng                                   |

---

# 14. MVP Priority

## 14.1 Must Have

| Nhóm         | Chức năng                                          |
| ------------ | -------------------------------------------------- |
| Timeline     | Project timeline 4 tháng                           |
| Environment  | Unity 6.3 LTS hoặc fallback thống nhất             |
| Deployment   | Demo 3–4 laptop thật                               |
| Deployment   | Backend API deploy online                          |
| Deployment   | PostgreSQL cloud trên Neon hoặc Supabase           |
| Photon       | Photon App ID/region/plan được kiểm tra trước demo |
| Auth         | Register/Login                                     |
| Auth         | JWT authentication                                 |
| Auth         | Admin account seed role ADMIN                      |
| Database     | PostgreSQL database                                |
| Multiplayer  | Create/join room                                   |
| Multiplayer  | Lobby ready/start                                  |
| Build        | Build Unity Windows chạy nhiều máy                 |
| Multiplayer  | 2–4 players, demo tối thiểu 2 players              |
| Gameplay     | Player movement sync                               |
| Gameplay     | Interaction system                                 |
| Objective    | Fuse pickup                                        |
| Objective    | Power restoration                                  |
| Objective    | Access card                                        |
| Objective    | Escape door                                        |
| Objective    | Win/lose condition                                 |
| AI           | Monster Patrol, Investigate, Chase, Search         |
| AI           | Vision detection                                   |
| AI           | Noise detection                                    |
| Player State | Down/revive                                        |
| Adaptive AI  | Noise Priority                                     |
| Adaptive AI  | Isolation Targeting                                |
| Adaptive AI  | Hide Spot Learning                                 |
| Result       | Result screen                                      |
| Economy      | Wallet                                             |
| Economy      | Shop                                               |
| Economy      | Inventory                                          |
| Economy      | Purchase transaction                               |
| Economy      | Shop có 15 cosmetic items                          |
| Economy      | Cosmetic items có ảnh thật/image_url               |
| Economy      | Backend validate hoặc tính lại reward              |
| Economy      | Chống duplicate match submission                   |
| Economy      | Wallet transaction audit                           |
| Admin        | Admin CRUD shop item                               |
| Logs         | Match logs                                         |
| Logs         | AI behavior logs                                   |

---

## 14.2 Should Have

| Chức năng                 |
| ------------------------- |
| 4-player demo ổn định     |
| Dual-switch puzzle        |
| Ping system               |
| Lobby cosmetic sync       |
| Admin filter/search logs  |
| Route ambush              |
| Admin preview ảnh item    |
| Upload file ảnh trực tiếp |

---

## 14.3 Could Have

| Chức năng                             |
| ------------------------------------- |
| Better horror lighting                |
| More cosmetics                        |
| More animations                       |
| Advanced sound effects                |
| Difficulty settings                   |
| Local retry khi submit match log fail |

---

## 14.4 Won’t Have

| Chức năng              |
| ---------------------- |
| Real-money payment     |
| Dedicated server       |
| Voice chat             |
| Public matchmaking     |
| Ranking system         |
| Multiple maps          |
| Multiple monster types |

---

# 15. Phân công nhóm 4 người

## Member 1 — Unity Gameplay

**Trách nhiệm chính:**

* Player controller.
* Interaction system.
* Map layout.
* Objective flow.
* Fuse pickup.
* Power restoration.
* Access card.
* Escape door.
* HUD.
* Result screen.
* Shop UI basic.

**Deliverables:**

* Playable map.
* Objective flow hoàn chỉnh.
* Gameplay demo.
* HUD/result UI.

---

## Member 2 — Backend Lead

**Trách nhiệm chính:**

* ASP.NET Core Web API.
* PostgreSQL schema.
* Entity Framework Core.
* Authentication.
* JWT.
* Password hashing.
* Player profile.
* Wallet.
* Inventory.
* Purchase transaction.
* Reward calculation.

**Deliverables:**

* Backend API.
* Database schema.
* API documentation.
* Swagger/Postman collection.

---

## Member 3 — Backend/Admin/Deploy

**Trách nhiệm chính:**

* Admin dashboard.
* Shop item CRUD.
* Image URL management.
* Match logs.
* AI logs.
* WalletTransactions.
* Backend deployment.
* PostgreSQL cloud deployment.
* Backend testing.

**Deliverables:**

* Admin dashboard.
* Deployed backend.
* Logs viewer.
* Test report.
* Seed data: admin + 15 cosmetic items.

---

## Member 4 — Photon/AI Integration

**Trách nhiệm chính:**

* Photon room/lobby.
* Network sync.
* Host authority.
* Monster sync.
* Monster FSM.
* Adaptive AI telemetry.
* Noise Priority.
* Isolation Targeting.
* Hide Spot Learning.
* AI behavior logging integration.

**Deliverables:**

* Multiplayer demo.
* AI sync demo.
* Adaptive AI gameplay demo.
* AI behavior logs.

---

# 16. Four-month Implementation Timeline

## 16.1 Tổng quan 4 tháng

| Thời gian | Mục tiêu                           | Deliverables                                                                                                                    |
| --------- | ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Tháng 1   | Foundation + Auth + Lobby          | Unity project, backend project, PostgreSQL, register/login, JWT, Photon create/join room, lobby ready                           |
| Tháng 2   | Multiplayer Gameplay + Objective   | Player movement sync, map blockout, fuse pickup, power room, access card, escape door, win/lose, match timer                    |
| Tháng 3   | Monster AI + Adaptive AI + Economy | Monster FSM, NavMesh, vision/noise detection, down/revive, Adaptive AI telemetry, shop, inventory, purchase, 15 cosmetic items  |
| Tháng 4   | Admin + Deploy + Testing + Polish  | Admin dashboard, match logs, AI logs, backend deploy, PostgreSQL cloud, Unity build, 3–4 laptop test, bug fix, demo preparation |

---

## 16.2 Tháng 1 chi tiết

| Tuần   | Công việc                                                |
| ------ | -------------------------------------------------------- |
| Tuần 1 | Setup Git, Unity, Photon, ASP.NET Core, PostgreSQL local |
| Tuần 2 | Register/Login API, JWT, password hashing, seed admin    |
| Tuần 3 | Unity login/register UI, load profile                    |
| Tuần 4 | Photon create room, join room, lobby list, ready state   |

---

## 16.3 Tháng 2 chi tiết

| Tuần   | Công việc                                                 |
| ------ | --------------------------------------------------------- |
| Tuần 5 | Player prefab, first-person controller, network transform |
| Tuần 6 | Map blockout 1 tầng, 4 khu vực chính                      |
| Tuần 7 | Fuse pickup, power box, security room, access card        |
| Tuần 8 | Escape door, escape zone, win/lose, match timer           |

---

## 16.4 Tháng 3 chi tiết

| Tuần    | Công việc                                                   |
| ------- | ----------------------------------------------------------- |
| Tuần 9  | Monster NavMesh, patrol, investigate, chase, search         |
| Tuần 10 | Vision detection, noise detection, down/revive              |
| Tuần 11 | Adaptive AI: NoiseScore, IsolationScore, HidingPatternScore |
| Tuần 12 | Shop, inventory, purchase transaction, 15 cosmetic items    |

---

## 16.5 Tháng 4 chi tiết

| Tuần    | Công việc                                                 |
| ------- | --------------------------------------------------------- |
| Tuần 13 | Admin dashboard, CRUD shop item, image URL                |
| Tuần 14 | Match logs, AI behavior logs, reward validation           |
| Tuần 15 | Deploy backend, deploy PostgreSQL cloud, test Unity build |
| Tuần 16 | Test 3–4 laptop, fix bug, polish UI, prepare demo         |

---

# 17. Kết luận

Bản SRS này chốt phạm vi triển khai thực tế cho nhóm 4 người trong 4 tháng:

```text
1 map
1 monster
2–4 players
demo tối thiểu 2 players ổn định
demo cuối 3–4 laptop thật
Unity 6.3 LTS
Photon Fusion Host Mode Free 100 CCU
ASP.NET Core Web API deployed online
PostgreSQL cloud Neon/Supabase
Admin dashboard
Seed admin account
15 cosmetic shop items có ảnh/image URL
Virtual shop + wallet + inventory
Reward anti-cheat server-side
Match logs
AI behavior logs
Adaptive AI thấy rõ trong gameplay
Windows build
```

Ưu tiên lớn nhất là hoàn thành MVP ổn định trước: **login, room/lobby, multiplayer sync, objective flow, monster AI, Adaptive AI MVP, backend deployed, PostgreSQL cloud, shop 15 items, admin dashboard và reward anti-cheat**. Các phần như ping system, dual-switch puzzle, route ambush, upload ảnh trực tiếp và 4-player demo ổn định chỉ nên làm sau khi các phần Must Have đã hoàn thành.
