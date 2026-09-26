# Architecture — Mod Dragon Ball PC

Tài liệu này mô tả cấu trúc ổn định của repo. Trạng thái task hiện tại nằm ở `docs/AI_HANDOFF.md`.

## 1. Solution-level architecture

```text
ModThanhLC.sln
├─ GameAssembly/
├─ AccountManager/
├─ LicenseCheckBypass/
├─ LicenseCheckBypassInjector/
└─ ProductLicense/
```

### GameAssembly

Vai trò: gameplay chính, UI game, packet/session, automation.

Target quan trọng: **.NET Framework 3.5**.

Build output:

```text
Output\Dragon ball_237b_Data\Managed\Assembly-CSharp.dll
```

### AccountManager

Vai trò: DragonBoyManager / quản lý tài khoản / launcher hỗ trợ.

Target được tài liệu hiện tại ghi nhận: .NET Framework 4.8.

### LicenseCheckBypass / LicenseCheckBypassInjector / ProductLicense

Các project phụ liên quan license/bypass/injector. Không sửa khi task chỉ liên quan gameplay nếu chưa đọc đúng source/history.

### Output

Runtime tree chính:

```text
Output/
├─ Dragon ball_237b.exe
├─ Dragon ball_237b_Data/
│  └─ Managed/
│     └─ Assembly-CSharp.dll
├─ Data/
│  ├─ kaitokid.txt
│  └─ Errors/
└─ Files/
```

## 2. Module map quan trọng trong GameAssembly

| File/module | Vai trò |
|---|---|
| `AssemblyCSharp.Functions/GClass164.cs` | Auto Attack, auto skill, cooldown, gửi hit |
| `AssemblyCSharp.Functions/GClass166.cs` | Auto Train / Đồ sát quái |
| `AssemblyCSharp.Functions/GClass167.cs` | custom image/Base64/logo |
| `AssemblyCSharp.Functions/GClass171.cs` | update dispatcher/render module |
| `AssemblyCSharp.Functions/KOLTracker.cs` | học/sync/hiển thị tiến độ KOL |
| `AssemblyCSharp.Functions/BossZoneScanner.cs` | lấy vị trí boss từ announcement, route đúng map, dò khu, detect target/death, rally và pin target |
| `GClass7.cs` | packet gửi, select skill, attack, combat diagnostics |
| `GClass12.cs` | xử lý nhiều packet/response từ server |
| `GClass14.cs` | session TCP chính |
| `GClass85.cs` | session TCP phụ |
| `GClass134.cs` | danh sách/chọn server |
| `GClass73.cs` | startup/render/screen |
| `GClass144.cs` | game screen/HUD/skill + hook thông báo VIP mới sang BossZoneScanner |
| `mResources.cs` | resource/language |

Manager có thêm:

| File/module | Vai trò |
|---|---|
| `DragonBoyManager/TabBossHunt.cs` | tab top-level SĂN BOSS |
| `DragonBoyManager/BossHuntCoordinator.cs` | session state, chia worker, FOUND/DEAD/RALLY/READY/FAILED |
| `DragonBoyManager/SocketServer.cs` | nhận event boss từ từng account và gửi lệnh targeted |

## 3. Luồng săn boss đa tài khoản

```text
DragonBoyManager / Tab SĂN BOSS
  -> BossHuntCoordinator tạo sessionId
  -> lấy N account đang Connected
  -> START_SCAN(workerIndex, workerCount, bossName, startZone)
       |
       v
Game client / BossZoneScanner
  -> lấy bossName -> mapId + zone từ cache announcement GClass156
  -> chưa có vị trí: WaitingLocation, không quét map hiện tại
  -> có vị trí: Class21.method_8(mapId) Xmap tới đúng map
  -> nếu server báo zone: ưu tiên zone đó trước
  -> nếu chưa thấy target: worker i quét fallback start+i, start+i+N, start+i+2N, ...
  -> mỗi khu chờ entity load
  -> resolve đúng boss từ GClass158.list_3
       |
       +-- chưa thấy -> khu được phân tiếp theo
       |
       +-- thấy -> FOUND(mapId, zone, accountId)
                      |
                      v
Manager chuyển session sang RALLYING
  -> broadcast RALLY(mapId, zone, bossName)
       |
       v
Mỗi Game client
  -> route tới map bằng Class21
  -> đổi đúng zone bằng GClass7.method_42
  -> resolve lại boss object theo tên
  -> pin gclass78_0 + GClass159.method_26
  -> bật GClass158 auto boss hiện có
  -> READY
       |
       +-- route/zone/target timeout -> FAILED
       |
       v
Manager: FIGHTING khi mọi worker còn kết nối đã READY hoặc FAILED
         và vẫn còn ít nhất một READY
```

Boss object **không được giữ xuyên map/zone**. Chỉ giữ identity `bossName + mapId + zone`, rồi resolve lại entity từ `GClass158.list_3`.

Death path:

```text
target HP <= 0
        hoặc
thông báo game mới xác nhận đúng target chết
        |
        v
client -> DEAD(sessionId)
        |
        v
Manager -> STOP toàn bộ account
```

Boss biến mất khỏi entity list một mình **không** được coi là chết vì có thể do map/zone đang load. Khi đang Fighting, scanner cho phép 3 giây để resolve lại target; nếu vẫn không thấy thì worker báo `FAILED`, không báo `DEAD`.

Rally hiện có guard để không treo vô hạn:

- toàn pha rally: 45 giây;
- đổi khu: tối đa 3 lần;
- đã vào đúng map+khu nhưng target chưa load: 8 giây;
- worker fail được cô lập, không chặn worker khác tiếp tục đánh.

Protocol Manager/Game dành riêng cho Boss Hunt:

```text
Manager -> Game: 100 START_SCAN, 101 STOP, 102 RALLY
Game -> Manager: 110 ZONE, 111 FOUND, 112 DEAD, 113 READY, 114 FAILED
```

Payload boss được JSON-serialize thành UTF-8 trong `vMessage.data`; outer socket protocol cũ vẫn giữ nguyên. `sessionId` bắt buộc dùng để bỏ event/lệnh cũ tới trễ.

Thông báo boss chết không còn được poll bằng index từ queue UI `gclass88_14`. `GClass144.method_121()` đưa từng thông báo mới vào queue riêng của `BossZoneScanner`; queue này được drain trong `Update()` trên game loop.

Thông báo boss xuất hiện được `GClass156.TryParseBossAnnouncement()` parse thành `bossName + mapName + mapId + zone`. Cache `GClass156.list_0` được duy trì độc lập với việc bật/tắt HUD danh sách boss. Boss Hunt dùng cache này để route tới đúng map trước khi scan.

Scan không suy luận map từ tên boss và cũng không quét map hiện tại một cách mặc định. Source of truth ban đầu là **announcement thực tế của server**; sau khi một worker resolve được entity thật, `FOUND(mapId, zone)` tiếp tục là source of truth cho pha rally.

## 4. Luồng combat khái quát

```text
Auto module
  -> chọn skill
  -> GClass7.method_56(skillTemplateId)   [SELECT SKILL]
  -> chờ micro-delay cần thiết
  -> GClass7.method_73(...)               [ATTACK]
  -> server response
  -> GClass12 / handlers cập nhật HP/death/miss
  -> combat diagnostics/adaptive scheduler cập nhật
```

Điểm quan trọng: packet ATTACK không mang trực tiếp skill ID theo context hiện tại; server phụ thuộc state skill đã select trước đó. Vì vậy timing giữa SELECT SKILL và ATTACK là một phần của protocol thực tế.

## 5. Luồng network khái quát

```text
GClass14 / GClass85
  -> TCP session
  -> send/receive queues
  -> GClass7 gửi packet
  -> controller/handlers xử lý response
```

Diagnostic/adaptive logic đã được bổ sung quanh:

- ping;
- Combat RTT;
- pending attack probes;
- ACK rate;
- adaptive window;
- pacing.

Chi tiết: `docs/NETWORKING.md`.

## 6. Luồng KOL hiện tại

```text
người dùng tương tác NPC/menu
  -> KOLTracker học packet/menu path
  -> background sync replay path đã học
  -> parse progress server
  -> local mirror chỉ cộng khi có bằng chứng last-hit đủ mạnh
  -> server sync tiếp tục là mốc đối chiếu
```

KOL tracker liên quan ít nhất:

- `KOLTracker.cs`;
- `GClass7.cs`;
- `GClass12.cs`.

Khi sửa KOL, bắt buộc đọc commit gần đây vì logic phụ thuộc packet order và các cửa sổ thời gian.

## 7. Build architecture

Gameplay-only loop:

```text
edit GameAssembly
-> rebuild GameAssembly.csproj Release
-> PostBuild copy Assembly-CSharp.dll vào Output
-> chạy game
-> kiểm tra runtime logs/server behavior
```

Full solution loop dùng khi thay đổi nhiều project hoặc cần xác nhận toàn bộ integration.

CI workflow:

```text
.github/workflows/build-and-release.yml
```

## 8. Documentation architecture

```text
AGENTS.md
  -> docs/AI_HANDOFF.md
     -> docs/ARCHITECTURE.md
     -> docs/DECISIONS.md
     -> docs/TROUBLESHOOTING.md
     -> docs/history/YYYY-MM.md

docs/PROJECT_CONTEXT.md   deep/legacy technical context
docs/NETWORKING.md        networking deep dive
CHANGELOG.md              project timeline
README.md                 stable quick start
```

Không dùng AI_HANDOFF làm nơi chứa toàn bộ lịch sử.
