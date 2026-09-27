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
| `DragonBoyManager/BossHuntCoordinator.cs` | central boss authority, session/generation, zone ledger, heartbeat/watchdog, FOUND/DEAD/RALLY/READY/FAILED |
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
  -> tới map: request zone-list mới và chờ response mới, không dùng `int_63` còn sót từ map trước
  -> nếu workerIndex >= số khu khả dụng: Standby, không scan trùng
  -> nếu server báo zone: chỉ worker sở hữu zone đó theo partition ưu tiên zone đó
  -> nếu chưa thấy target: worker i quét fallback start+i, start+i+N, start+i+2N, ...
  -> mỗi khu dùng entity grace động: min 2s, stable window 0.8s, max 5s; announced zone max 7s
  -> target vẫn được resolve mỗi tick từ GClass158.list_3, FOUND ngay khi xuất hiện
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

- toàn pha rally: 90 giây;
- đổi khu: tối đa 3 lần;
- đã vào đúng map+khu nhưng target chưa load: 8 giây;
- worker fail được cô lập, không chặn worker khác tiếp tục đánh.

Protocol Manager/Game dành riêng cho Boss Hunt:

```text
Manager -> Game:
100 START_SCAN
101 STOP
102 RALLY
103 BOSS_SYNC
104 BOSS_INVALIDATE

Game -> Manager:
110 ZONE
111 FOUND
112 DEAD
113 READY
114 FAILED
115 BOSS_SPAWN
116 BOSS_DEATH
117 TELEMETRY
118 HEARTBEAT
```

Payload boss được JSON-serialize thành UTF-8 trong `vMessage.data`; transport ngoài dùng frame `[4-byte network-order length][UTF-8 JSON]`. Mọi event session-scoped dùng cả `sessionId + assignmentGeneration`; generation tăng khi reassign để event cũ tới trễ không thể sửa state hiện tại.

Thông báo boss chết không còn được poll bằng index từ queue UI `gclass88_14`. `GClass144.method_121()` đưa từng thông báo mới vào queue riêng của `BossZoneScanner`; queue này được drain trong `Update()` trên game loop.

Thông báo boss xuất hiện được `GClass156.TryParseBossAnnouncement()` parse thành `bossName + mapName + mapId + zone`. `GClass156.list_0` vẫn chỉ là HUD 5 dòng; automation dùng cache riêng theo boss, chọn record mới nhất còn fresh tối đa 60 phút. Death announcement invalidate cache của boss/family tương ứng. Boss Hunt dùng cache automation này để route tới đúng map trước khi scan.

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


## 9. Manager/Game socket framing cho Boss Hunt và command cũ

Kênh localhost `SocketServer <-> GClass150` không còn giả định một lần TCP receive tương ứng đúng một JSON.

Wire format hiện tại:

```text
[4-byte big-endian payload length][UTF-8 JSON vMessage]
```

Mỗi connection có receive accumulator riêng:

- frame bị chia qua nhiều TCP receive sẽ được giữ cho tới khi đủ payload;
- nhiều frame dính trong một receive được tách và dispatch lần lượt;
- frame length âm hoặc lớn hơn 1 MiB bị coi là invalid;
- send loop xử lý trường hợp `Socket.Send()` chỉ gửi một phần buffer.

Game handshake lại `cmd=0 + accountId` ở **mọi connection mới**. Manager ưu tiên resolve account từ `TabData`, thay socket cũ bằng socket mới và callback connection cũ chỉ được đánh dấu disconnect nếu nó vẫn là socket hiện hành của account.


## 10. Zone-list freshness và worker capacity

Boss Hunt không được dùng trực tiếp một `GClass144.int_63` bất kỳ sau khi vừa Xmap.

Khi bắt đầu scan trên map mới:

1. ghi `mapId` hiện tại và reference của zone-list cũ làm baseline;
2. gửi `method_58()` để request zone-list;
3. chỉ chấp nhận khi `int_63` là mảng mới và client vẫn ở đúng map đã request;
4. quá 10 giây chưa có response mới thì worker báo `ZONE_LIST_TIMEOUT`.

Sau khi biết số khu thật, partition worker dùng số khu khả dụng. Worker có `workerIndex >= availableZoneCount` chuyển sang `Standby`; không modulo về khu đầu. Standby vẫn active để nhận `RALLY` khi worker khác tìm thấy boss.


## 11. Boss Hunt runtime diagnostics và entity grace

Pha scan không còn dùng dwell cố định 900 ms.

Khi worker vào đúng zone:

- snapshot số entity trong `GClass144.gclass88_5` và số boss trong `GClass158.list_3`;
- target vẫn được tìm ở đầu mỗi tick, nên nếu boss xuất hiện thì `FOUND` ngay;
- không rời khu trước 2 giây;
- sau mốc 2 giây, nếu entity count > 0 và snapshot không đổi trong ít nhất 800 ms thì khu được xem là đủ ổn định để chuyển tiếp;
- nếu entity vẫn rỗng/dao động, chờ tối đa 5 giây;
- zone được announcement chỉ đích danh được chờ tối đa 7 giây.

Structured diagnostic:

```text
Data/Errors/BossHuntProtocol.log
```

Mỗi dòng dùng trường `source/event/session/account/boss/state/map/zone/detail`. Game ghi các event route, zone-list, zone request/arrival, entity change, target, rally và terminal event. Socket Game ghi connect/handshake/reconnect. Manager ghi session, assignment, client event, rally và disconnect. Chỉ event/action mới được ghi; không log mỗi frame.


## 12. Manager authority, generation, zone ledger và watchdog

Boss Hunt không còn xem cache riêng trong từng Game process là source of truth cuối.

Luồng announcement:

```text
Server announcement
  -> mỗi Game parse spawn/death
  -> Game -> Manager: 115/116 + observedAt + rawMessage + killer nếu có
  -> Manager deduplicate và giữ record canonical
  -> Manager -> mọi Game: 103 BOSS_SYNC hoặc 104 BOSS_INVALIDATE
```

Khi Manager reconnect/restart trong cùng runtime client, Game gửi lại cache location hiện có sau handshake. Manager chọn observation mới nhất; nếu đã có death record mới hơn thì spawn cache cũ bị từ chối và client reconnect được gửi invalidate.

Assignment safety:

- mỗi lần phân worker có `assignmentGeneration`;
- reassign do disconnect/FAILED/watchdog tăng generation;
- Manager bỏ `ZONE/FOUND/READY/FAILED/117/118` có generation cũ;
- Scanner cũng bỏ START/RALLY cũ.

Zone ledger:

- `ZONE_ENTER` mở ownership của `generation + mapId + zone`;
- `ZONE_CLEAR` ghi account đã scan xong;
- nếu account khác enter cùng zone đang active hoặc zone đã được worker khác scan trong cùng generation, Manager sinh `DUPLICATE_ZONE` và đưa warning vào snapshot/UI;
- worker giữ `ScannedZones` và `ZoneHistory` để audit.

Liveness:

- Game gửi heartbeat 2 giây/lần khi Boss Hunt active;
- Manager watchdog chạy 2 giây/lần;
- quá 8 giây không heartbeat -> worker `Unresponsive/Failed`;
- ở Scanning, worker còn khỏe được reassign bằng generation mới;
- ở Rallying/Fighting, worker timeout không còn chặn các worker còn lại.


## 13. Boss Hunt observability model

Manager snapshot hiện mang đủ dữ liệu để nghiệm thu runtime, không chỉ hiển thị state tổng:

- boss presence: `Unknown / Alive / Dead / Stale`;
- spawn/death observation time đến millisecond, raw announcement, killer nếu parser lấy được, source accounts;
- finder username + accountId;
- worker assignment: generation, worker index/count, assigned zones;
- scan audit: scanned zones, unique coverage, scan cycle, zone-enter timestamp;
- runtime load: entity count, boss count, target HP;
- failure audit: zone-fail count + lỗi gần nhất;
- route/rally action và heartbeat/event age;
- session timeline: session start, assignment, route, zone enter/clear/fail, duplicate, FOUND, RALLY, READY, FAILED, WATCHDOG, boss spawn/death và stop.

Coverage được tính theo zone ledger của **generation hiện tại**, nên cùng một zone quét nhiều vòng vẫn chỉ tính một lần trong unique coverage.

### Timestamp lifecycle

Nhiều client có thể nhận cùng announcement gần như đồng thời. Với cùng một lifecycle event, Manager giữ timestamp quan sát **sớm nhất** và chỉ gom thêm `SourceAccounts`; observation tới sau không được phép dời spawn/death time về sau.

### Boss Hunt log files

Không process nào còn append chung một Boss Hunt log:

```text
<runtime-base>\Data\Errors\BossHuntProtocol.Game.pid<PID>.log
<runtime-base>\Data\Errors\BossHuntProtocol.Manager.pid<PID>.log
```

Path được tạo từ `AppDomain.CurrentDomain.BaseDirectory`, không phụ thuộc current working directory. Nhánh fallback dùng `Path.GetTempPath()`, vẫn là absolute path. Cách tách theo PID tránh lock nội-process bị hiểu nhầm là lock xuyên process.

## 14. Central zone partition và medium observability

Client không còn tự quyết định round-robin partition dựa trên zone-list cục bộ.

```text
Game -> Manager: 117 ZONE_CAPACITY { maxZone, totalZones }
Manager chờ mọi worker khỏe báo capacity
Manager chọn canonicalMaxZone = min(maxZone của các worker)
Manager chia round-robin một lần
Manager -> Game: 105 ZONE_ASSIGNMENT { assignedZones, totalZones, generation }
Game chỉ scan đúng danh sách Manager cấp
```

Nếu số worker lớn hơn số zone, worker dư nhận danh sách rỗng và chuyển Standby. Cách chọn giao phần an toàn chung tránh việc hai client nhìn zone-list khác nhau rồi tự sinh partition chồng lấn.

Dashboard medium metrics theo worker:

- số khu đã clear;
- khu/phút tính từ thời điểm worker vào session;
- tổng failure;
- tổng timeout;
- scan cycle;
- assigned/scanned zones vẫn giữ làm audit.

Boss catalog không còn nằm cứng trong `TabBossHunt.cs`. Runtime đọc:

```text
<runtime-base>\Data\BossHuntBosses.txt
```

Tên boss mới có thể được thêm bằng file, target người dùng nhập hoặc announcement đã quan sát; không cần sửa source UI.

Boss Hunt log mỗi process rotate khi file đạt 5 MB, giữ 3 archive và dọn file cùng loại cũ hơn 14 ngày. Manager có viewer log trực tiếp, merge các `BossHuntProtocol.*.log*` gần nhất để debug nhiều process.
