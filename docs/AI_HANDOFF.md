# AI_HANDOFF — Trạng thái hiện tại

> Repo: `thanhtran2005isme-art/Mod_DragonBall_PC`  
> Branch chính: `main`  
> Cập nhật handoff: 2026-09-27  
> Đây là bản tóm tắt hiện tại. Chi tiết cũ chuyển sang `docs/history/`.

## 1. Mục tiêu của file này

Dùng để một phiên AI mới vào việc trong vài phút, không phải đọc lại toàn bộ dự án.

Sau file này, đọc:

1. `docs/ARCHITECTURE.md`
2. `docs/DECISIONS.md`
3. `docs/TROUBLESHOOTING.md`
4. `docs/history/` gần nhất
5. Git history + source liên quan task

## 2. Snapshot repo hiện tại

Solution:

```text
ModThanhLC.sln
```

Các project/folder chính:

```text
GameAssembly/               gameplay + network chính
AccountManager/             DragonBoyManager / quản lý tài khoản
LicenseCheckBypass/         project bypass/hook license
LicenseCheckBypassInjector/ injector
ProductLicense/             logic license
Lib/                        dependency
Output/                     runtime/output
docs/                       tài liệu kỹ thuật
```

Output gameplay quan trọng:

```text
Output\Dragon ball_237b.exe
Output\Dragon ball_237b_Data\Managed\Assembly-CSharp.dll
```

## 3. Cách build nhanh phần gameplay

Repo local đang được tài liệu hóa tại:

```text
C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1
```

Nếu máy hiện tại dùng path khác, xác minh lại trước khi chạy lệnh.

```bat
cd /d C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1
taskkill /F /IM "Dragon ball_237b.exe" 2>nul

"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" GameAssembly\GameAssembly.csproj /t:rebuild /p:Configuration=Release
```

Khi chỉ sửa gameplay, ưu tiên build riêng `GameAssembly.csproj` thay vì full solution.

## 4. Các khu vực kỹ thuật đang quan trọng

### Săn Boss đa tài khoản — feature branch

Branch triển khai hiện tại:

```text
feat-boss-hunt-manager
```

Commit chức năng đầu tiên:

```text
f02197f Thêm săn boss đa tài khoản trên Manager
```

Đã có:

- tab top-level `SĂN BOSS` trong DragonBoyManager;
- Manager lấy các account đang kết nối và chia khu theo `workerIndex/workerCount`;
- client lấy vị trí boss từ thông báo `BOSS ... vừa xuất hiện tại ... khu vực ...`; nếu có map hợp lệ thì tự Xmap tới đúng map trước, sau đó mới dò khu và báo `ZONE / FOUND / DEAD / READY / FAILED` về Manager;
- account đầu tiên thấy đúng boss làm Manager chuyển cả session sang rally;
- các account còn lại tự tới đúng `mapId + zone`, resolve lại boss theo tên rồi focus/đánh bằng `GClass158` hiện có;
- thông báo game xác nhận đúng boss mục tiêu chết là terminal event: dừng scan/rally/fight toàn bộ session;
- HP boss `<= 0` là tín hiệu chết bổ sung;
- `sessionId` chặn event cũ tới trễ;
- account disconnect trong lúc scan được loại khỏi worker set và phần còn lại được chia lại;
- lệnh START/STOP/RALLY nhận từ socket chỉ được enqueue; mọi thao tác game thật được apply trong `BossZoneScanner.Update()` trên game loop;
- Auto Boss cũ bị tắt trong pha scan và được khôi phục khi session dừng;
- UI Start/boss/start-zone bị khóa khi session đang chạy;
- layout tab đã thu gọn để không bị cắt khi Manager ép cửa sổ về `765x480`;
- nếu toàn bộ worker mất kết nối ở scan/rally/fighting, Manager tự chuyển session sang `Stopped`;
- rally không còn retry vô hạn: toàn pha rally timeout 45 giây; đổi khu thử tối đa 3 lần; vào đúng map/khu nhưng không resolve được target trong 8 giây thì worker báo `FAILED`;
- khi đang Fighting mà target biến mất, client chờ grace 3 giây để resolve lại; vẫn mất target thì báo `FAILED`, không tự suy boss đã chết;
- worker `FAILED` không chặn các worker đã `READY`; Manager chuyển sang Fighting khi mọi worker còn kết nối đã ở trạng thái READY hoặc FAILED và còn ít nhất một READY;
- nếu không còn worker nào có thể tới boss, Manager dừng toàn phiên;
- thông báo VIP mới được hook trực tiếp từ `GClass144.method_121()` vào queue riêng của `BossZoneScanner`, rồi xử lý trên game loop; không còn phụ thuộc index của queue UI bị xóa đầu;
- `GClass156` luôn cache thông báo xuất hiện boss dù HUD danh sách boss đang bật hay tắt; scanner dùng cache này làm source of truth cho `bossName -> mapId + zone`;
- nếu chưa có vị trí boss, scanner ở `WaitingLocation` và **không quét nhầm map hiện tại**;
- vị trí automation không còn phụ thuộc `GClass156.list_0` 5 dòng HUD: có cache riêng theo boss, giữ bản ghi mới nhất và chỉ dùng location còn fresh trong 60 phút;
- death announcement invalidate location cache của đúng boss/family ngay trước khi scanner xử lý terminal death;
- khi có vị trí, scanner dùng `Class21.method_8(mapId)` để Xmap tới map boss; nếu thông báo có zone thì ưu tiên zone đó trước, không thấy target mới quay về round-robin;
- Xmap scan/rally không còn bị hủy mỗi 5 giây: khi Xmap đang chạy thì giữ nguyên; chỉ restart sau 30 giây không đổi map hoặc khi Xmap đã dừng, với timeout tổng 90 giây;
- socket Manager↔Game dùng frame `[4-byte network-order length][UTF-8 JSON]`, có receive accumulator nên chịu được TCP split/coalesced message;
- mỗi TCP connection mới từ Game luôn handshake lại `accountId`; client tự retry kết nối mỗi giây khi Manager chưa sẵn sàng; callback socket cũ không được phép đánh dấu socket mới là disconnected;
- sau khi tới map scan, scanner **không dùng lại `int_63` cũ**: ghi baseline reference, request zone-list mới và chỉ lập kế hoạch khu khi server thay bằng mảng mới trên đúng map hiện tại; quá 10 giây không có response mới -> `ZONE_LIST_TIMEOUT`;
- khi số worker lớn hơn số khu khả dụng, worker dư chuyển sang `Standby` thay vì modulo quay lại khu đã có worker khác; worker Standby vẫn giữ session và vẫn nhận `RALLY` khi có finder;
- announced zone chỉ được ưu tiên bởi worker sở hữu zone đó theo partition, tránh mọi account cùng scan một khu;
- dwell mỗi khu không còn cố định 900 ms: target vẫn được check mỗi tick; nếu chưa thấy thì chờ tối thiểu 2 giây, chỉ rời sớm sau min khi entity snapshot đã ổn định 800 ms; khu thường có trần 5 giây, khu được announcement chỉ đích danh có trần 7 giây;
- thêm structured log `Data/Errors/BossHuntProtocol.log` ở Game + Manager cho các event socket/session/route/zone/entity/FOUND/RALLY/READY/FAILED/DEAD; không log mỗi frame;
- Manager hiện là **source of truth chung** cho boss location/lifecycle: Game gửi `115 BOSS_SPAWN` và `116 BOSS_DEATH`, Manager deduplicate/cache rồi sync lại cho mọi client bằng `103 BOSS_SYNC` / `104 BOSS_INVALIDATE`;
- death parser giữ thêm `killer` khi raw announcement có dạng `... bởi X` / `... killed by X`; Manager lưu spawn/death time, raw announcement và source account;
- mỗi assignment có `assignmentGeneration`; START/RALLY/ZONE/FOUND/READY/FAILED/telemetry chỉ hợp lệ khi `sessionId + generation` trùng hiện tại; reconnect/reassign làm event generation cũ vô hiệu;
- Game gửi `117 TELEMETRY` cho `ZONE_ENTER / ZONE_CLEAR / ZONE_FAILED`; Manager giữ zone ledger + lịch sử khu theo account và tự cảnh báo duplicate zone trong cùng generation;
- Game gửi `118 HEARTBEAT` mỗi 2 giây khi session active; Manager watchdog timeout 8 giây, đánh dấu worker `Unresponsive` và reassign phần còn lại khi đang Scanning;
- boss-name match đã siết theo boundary giống nhau ở Game/Manager/cache, không còn substring hai chiều kiểu `Số 1` match `Số 10`;
- panel Manager hiển thị generation, worker, các khu đã dò, tuổi event/heartbeat và warning dò trùng; box Boss hiển thị spawn/death time, map/khu, source account và killer khi parse được;
- observability mức cao đã bổ sung: trạng thái boss `Unknown/Alive/Dead/Stale`, cache age, lifetime, raw announcement, finder username+ID, assigned zones, scanned zones, unique coverage, scan cycle, zone dwell, entity/boss count, zone-fail count, rally progress, target HP và live session timeline;
- cùng một lifecycle event từ nhiều account giữ **timestamp quan sát sớm nhất**; các observer sau chỉ bổ sung source account, không ghi đè spawn/death time;
- Boss Hunt log không còn dùng chung một file giữa process: Game ghi `BossHuntProtocol.Game.pid<PID>.log`, Manager ghi `BossHuntProtocol.Manager.pid<PID>.log`; cả hai dùng absolute path dưới `AppDomain.CurrentDomain.BaseDirectory\\Data\\Errors`, fallback cũng là absolute temp path.

File mới chính:

```text
AccountManager/DragonBoyManager/BossHuntCoordinator.cs
AccountManager/DragonBoyManager/TabBossHunt.cs
GameAssembly/AssemblyCSharp.Functions/BossZoneScanner.cs
```

Hook integration:

```text
MainController.cs
SocketServer.cs
GClass150.cs
GClass171.cs
```

GitHub Actions run `36029651400` đã build full solution thành công cho V1. Hardening ngày 2026-09-26 có commit `1786046` và `0ee000c`; run `36254447048` đã qua bước MSBuild full solution. **Chưa có runtime gameplay test nhiều account**, vì vậy chưa nên merge vào `main` chỉ dựa trên compile.

Boss Hunt hiện **không còn quét map mà account đang đứng một cách mù quáng**. Source of truth vị trí ban đầu là thông báo boss thực tế do `GClass156` parse; scanner Xmap tới đúng `mapId`, ưu tiên `zone` được server báo, rồi mới fallback sang round-robin nếu chưa resolve được target.

### Auto Attack / Auto Train

- `GClass164.cs` = Tự động đánh / Auto Attack.
- `GClass166.cs` = Đồ sát quái / Auto Train.
- Sau SELECT SKILL phải giữ micro-delay khoảng **100 ms** trước ATTACK.
- Không reset `GClass164.long_10` ngay sau SELECT SKILL.
- Mục tiêu là dùng cooldown thật của skill, không quay lại delay cố định 550 ms nếu chưa có lý do/test rõ ràng.

### Networking / adaptive combat

Chi tiết ở `docs/NETWORKING.md`.

Trạng thái đã có:

- TCP NoDelay cho session trực tiếp/proxy;
- diagnostic ping;
- Combat RTT;
- ACK/s, Pending, Queue Delay;
- adaptive window + packet pacing cho `/dsq`;
- SV15 slot contender retry theo phản hồi server.

### KOL tracker

File chính:

```text
GameAssembly/AssemblyCSharp.Functions/KOLTracker.cs
```

Các commit gần nhất đã chuyển logic KOL sang xác nhận chặt hơn:

- học/replay menu KOL bằng packet thực tế;
- hỗ trợ chuỗi menu packet 32 hai bước;
- theo dõi KOL local khi farm ngoài Đảo Kame;
- siết last-hit local;
- logic đang chạy trên `main` vẫn stage candidate khi có own-drop hoặc attack được gửi lúc client thấy mob HP = 1;
- logic hiện tại vẫn chờ packet tăng SM/TN khoảng 750 ms trước khi local +1.

**Quan trọng:** SM/TN không phải bằng chứng last-hit độc lập; hit gây damage bình thường cũng có thể tăng SM/TN. Vì vậy công thức hiện tại đang được giữ nguyên tạm thời để đo sai lệch, chưa được xem là kết luận protocol cuối.

Đã thêm diagnostic timeline tại:

```text
Output\Data\Errors\KOLProtocol.log
```

Log ghi sequence ATTACK/probe, HP response, MISS, MOB DIE, drop owner, SM/TN, stage/skip/timeout và chênh lệch khi server sync. Mục tiêu trước mắt là xác định packet nào thực sự đủ mạnh để chứng minh last-hit trong khu có nhiều người farm, **không đổi công thức +1 KOL cho tới khi có log thực tế**.

## 5. Commit gần đây đáng chú ý

```text
836f4fa Loại bỏ fallback log tương đối của săn boss   [feature branch]
dd7a97d Sửa viewer log tương thích CSharp 7.3   [feature branch]
208a1a6 Hoàn thiện log catalog và thống kê săn boss   [feature branch]
26db73a Đưa phân khu săn boss về Manager trung tâm   [feature branch]
53b9ba7 Sửa Path.Combine tương thích net35 cho log boss   [feature branch]
3c807ec Giữ timestamp sớm nhất cho lifecycle boss   [feature branch]
0d350ae Tách log săn boss theo process và cố định đường dẫn   [feature branch]
5f03c50 Nâng dashboard săn boss với coverage HP và timeline   [feature branch]
fe5379d Bổ sung telemetry chi tiết và timeline săn boss   [feature branch]
786c3ff Ngăn cache spawn cũ hồi sinh boss đã chết   [feature branch]
7198770 Hiển thị giám sát generation khu dò và lịch sử boss   [feature branch]
f21f72d Đồng bộ boss toàn cục và thêm telemetry worker   [feature branch]
8f93b2e Nâng Manager thành nguồn điều phối săn boss trung tâm   [feature branch]
3a94b2e Thêm entity grace động và log protocol săn boss   [feature branch]
5546cde Chờ zone-list mới và đưa worker dư về dự phòng   [feature branch]
2250625 Sửa cắt tiền tố thông báo VIP cho cache boss   [feature branch]
8d108a9 Đóng khung TCP và handshake lại khi reconnect   [feature branch]
8d6e0f3 Ổn định route và cache vị trí boss   [feature branch]
d54131d Dọn reset trạng thái scan map boss   [feature branch]
c6c9c79 Tự tới đúng map boss trước khi dò khu   [feature branch]
0ee000c Bắt thông báo boss chết trực tiếp vào game loop   [feature branch]
1786046 Chống treo rally và cô lập worker săn boss lỗi   [feature branch]
f02197f Thêm săn boss đa tài khoản trên Manager   [feature branch]
0f07d48 Ghi chi tiết handoff điều tra KOL last-hit
ed2639d Thêm diagnostic protocol cho KOL last-hit
712dc3f Xac nhan KOL bang kill-shot 1 HP va goi tang SM TN
40785a2 Siết KOL local theo last hit của người chơi
8544663 Theo doi KOL local khi farm ngoai Dao Kame
8a015c5 Replay đủ chuỗi menu KOL 2 bước
006a730 Học và đồng bộ KOL bằng packet 32 thực tế
bfdc638 Bắt KOL trực tiếp từ packet 22 thực tế
```

## 6. Rủi ro/lỗi đã biết

- Boss Hunt đã harden P0/P1/P2 + critical + nhóm lỗi mức cao: lifecycle/timestamps/raw/source/status stale, assigned/scanned/coverage/cycle/dwell/entity/zone-fail/rally/HP/finder/timeline và per-process absolute log. `fe5379d`, `5f03c50`, `836f4fa` đều full workflow SUCCESS. Vẫn phải runtime test 1→2→3 account trước khi bỏ Draft/merge.
- Nhóm lỗi mức trung bình đã xử lý: Manager central zone partition (`105 ZONE_ASSIGNMENT`) lấy min `maxZone` chung của các worker đã báo capacity; log rotation 5 MB x 3 archive + cleanup 14 ngày; viewer log trực tiếp trong Manager; boss catalog động ở `Output/Data/BossHuntBosses.txt` và tự học boss từ announcement/target nhập tay; dashboard có throughput `khu/phút`, tổng fail và timeout. Heartbeat và duplicate warning đã được xử lý từ vòng critical/high trước đó. `26db73a` và `dd7a97d` full workflow SUCCESS.
- Crash hardening Manager: `OpenAccount()` không để exception thoát ra async UI handler; `SocketServer.AcceptCallback()` có outer guard; listener lỗi thoáng qua không còn gọi `Application.Exit()`; account được đưa vào `waitingAccounts` trước `process.Start()`; thêm `Data/Errors/ManagerRuntime.log` với nguồn `OPEN_ACCOUNT_*`, `SOCKET_*`, `UI_THREAD_EXCEPTION`, `APPDOMAIN_UNHANDLED`. Commit `2694b4c` full workflow SUCCESS.
- Fix Boss Hunt connected-count=0: Manager launch truyền `--manager 1 --managerPort <port>`, gọi `SocketServer.EnsureStarted(port)` trước `process.Start()`, Game force `GClass150.bool_0=true` cho manager-launched process và override port bằng argument. Không còn phụ thuộc `FunctionSetting.ini` index 7. Thêm `HANDSHAKE_RX ... mapped=<bool>` vào `ManagerRuntime.log`; log viewer đọc cả ManagerRuntime. Commit `5cfc15f` full workflow SUCCESS.
- Handshake transport đã tách rõ semantics: Game gửi `cmd=0 + accountId` (HELLO), Manager chỉ trả `cmd=99 + accountId` (ACK) sau khi map account; Manager không gửi `cmd=0` ngay khi Accept nữa. Game retry HELLO tối đa 10 lần trên cùng socket, chỉ coi kết nối ready và sync boss cache sau ACK. Thêm log `HANDSHAKE_TX/HANDSHAKE_ACK/HANDSHAKE_ACK_TIMEOUT/SOCKET_PREHANDSHAKE_CLOSE`. Commit `7771a15` full workflow SUCCESS.
- Runtime compatibility fix: Unity/.NET runtime của Game không load được `System.IO.InvalidDataException` dù CI net35 compile được. Điều này làm `GClass150` chết ngay sau CONNECTED trước HANDSHAKE_TX. Hai chỗ framing trong `GClass150.cs` đã đổi sang `IOException`; commit `41e5484` full workflow SUCCESS. Không dùng `InvalidDataException` trong GameAssembly mới.
- Lifecycle death hardening: death parser nhận thêm mẫu `bị <killer> tiêu diệt/hạ gục/đánh bại`, direct `... bởi <killer>` và English equivalents; mọi announcement được log raw + parsed/unparsed. `ZONE_PLAN` telemetry giờ giữ `assignedZones/totalZones`. Nếu boss đã spawn >=45s và mọi worker khỏe đã tới scan cycle >=3 mà vẫn không FOUND, Manager đánh dấu record `STALE`, invalidate location và dừng phiên với lý do `death/killer chưa xác nhận` thay vì quét vô hạn. Commit `0fa2a2e` full workflow SUCCESS.
- Coverage correction: scan exhaustion không còn dựa riêng vào `scanCycle`; bắt buộc `uniqueCoverage >= totalZones`. Coverage 5/17 phải tiếp tục scan, không được STALE/STOP. Game forward raw announcement liên quan boss lên Manager timeline bằng telemetry `ANNOUNCEMENT_RAW`, giúp nhìn trực tiếp death text thật. Commits `b210ba5`, `ce4af3b`; `ce4af3b` full workflow SUCCESS.
- Zone partition rule chốt: Game báo `totalZones = int_63.Length` cho toàn map; `Khu bắt đầu` chỉ xoay thứ tự danh sách, không cắt bỏ khu trước đó. 1 account nhận toàn bộ zone list; N account được chia contiguous cân bằng bằng quotient/remainder, chênh tối đa 1 khu; account dư Standby. Không overlap trong cùng generation. Commits `30d12fc`, `e9468fc`; `e9468fc` full workflow SUCCESS.
- Session target lock: khi Start đã chọn/nhận một spawn cụ thể, Manager giữ `_sessionTargetBoss`; spawn mới cùng family chỉ ghi `BOSS_SPAWN_QUEUED`, không thay target session. Game khóa concrete target name + map + zone + observedAt và bỏ qua `BOSS_SYNC` khác instance (`SESSION_TARGET_SYNC_IGNORED`). Entity lookup và local death matching dùng concrete locked target. Commits `12ee5c8`, `9102779`, `d36acaa`; `d36acaa` full workflow SUCCESS.
- Boss catalog runtime sync: repo không có master list boss đầy đủ; seed 23 tên chỉ là fallback. Game gửi `CmdBossCatalog=119` ngay sau handshake và mỗi ~15s, lấy tên từ `GClass158.list_3`, `GClass156` cache và target hiện tại. Manager batch-merge vào `Data/BossHuntBosses.txt`, canonicalize instance về family đã biết và tăng revision để dropdown tự refresh. Commits `1d31b26`, `cceae42`, `23b054e`, `e2c19f2`, `9071a6e`; `9071a6e` full workflow SUCCESS.
- Combat-death source: `GClass12` packet `-60` mang attacker charId + target charId + server `isDie`. Khi target là boss `GClass78` (`int_13 < 0`) và `isDie=true`, Game enqueue `ObserveCombatCharacterDeath(...)`; `BossZoneScanner.Update()` validate concrete locked target + map + zone rồi gửi `CmdBossDeath` kèm `killerId/killerName/raw= combat:-60...`. Manager lưu killer ID/name và dừng đúng session target. Commits `58e2ea9`, `ee557c8`, `dba8863`, `dcbba04`, `b763a80`, `09ee59f`; HEAD `09ee59f` full workflow SUCCESS.











- Full solution local từng fail ở PostBuild copy của một số project dù source compile được; gameplay thường nên build riêng.
- DLL `Assembly-CSharp.dll` có thể bị game/manager lock.
- `GameAssembly` là .NET Framework 3.5, dễ lỗi nếu dùng API mới.
- Combat có thể hiển thị animation đúng nhưng server chỉ tính một hit nếu timing SELECT SKILL/ATTACK sai.
- KOL local mirror là logic suy luận từ packet client/server; khi thay đổi phải đối chiếu server sync, không chỉ HUD local.

Xem thêm: `docs/TROUBLESHOOTING.md`.

## 7. Tài liệu sâu cần biết

- `docs/PROJECT_CONTEXT.md`: build, combat, logo, class mapping, quy trình Git.
- `docs/NETWORKING.md`: network diagnostics/adaptive combat.
- `CHANGELOG.md`: timeline thay đổi.
- `docs/history/2026-09.md`: lịch sử tháng hiện tại.

## 8. Khi kết thúc task tiếp theo

Cập nhật file này chỉ với trạng thái **còn cần cho phiên sau**.

Nếu nội dung cũ dài ra, chuyển nó sang `docs/history/YYYY-MM.md` thay vì để AI_HANDOFF phình vô hạn.

### Boss Hunt death evidence merge — 2026-09-27

- Announcement không còn vừa parse trên callback vừa có một parser death riêng ở game loop. `ObserveAnnouncement()` chỉ enqueue; `DrainAnnouncements()` là đường parse duy nhất.
- Announcement death gửi `CmdBossDeath=116` trước khi dừng local target.
- Manager merge death evidence vào cả record Alive lẫn Dead để trường hợp announcement trước / combat `-60` sau vẫn giữ map/spawn/raw và bổ sung `killerId`.
- `CmdDead=112` (ví dụ HP <= 0) được chuyển thành death fallback với killer unknown thay vì chỉ Stop.
- Raw announcement và killer name thật được ưu tiên hiển thị; combat `-60` vẫn là nguồn xác nhận có `killerId`.
- Code commits: `80cd904`, `1a13b43`, `7689c1c`. Chưa có GitHub Actions run/status trên HEAD tại thời điểm handoff; vẫn cần runtime test thật.

### Boss Hunt operational panel + reconnect — 2026-09-27

- Death trước `TARGET_LOCK` đã được xử lý theo current `sessionId + generation`; không dùng global death để dừng phiên.
- Boss record có `DeathEvidence` và merge `ANNOUNCEMENT + COMBAT_-60 + FALLBACK`.
- Game forward `DEATH_UNPARSED` lên Manager; panel hiển thị raw cảnh báo.
- Start hỗ trợ toàn bộ account connected hoặc chỉ account đang selected ở tab ACCOUNT. Requested account offline hiện placeholder và được giữ để rejoin.
- Reconnect lúc Scanning -> generation mới + repartition; Rallying/Fighting -> bootstrap lại worker và đưa về target hiện tại.
- Panel có healthy/requested worker, worst heartbeat, StopReason, target lock, evidence, tab SỰ KIỆN từ `RecentBossEvents`, double-click focus game, toggle âm báo và autocomplete boss.
- Log Viewer có filter session/account/event/boss + copy.
- Các commit triển khai sau baseline `b22a4d0`; chưa có GitHub Actions run cho HEAD tại thời điểm cập nhật, nên vẫn cần build + runtime test thật trước merge.

### TabControl name collision fix — 2026-09-27

- Sau khi nâng panel Boss Hunt, full solution local báo CS1061 tại `TabBossHunt.cs`: `TabControl` không có `TabPages`.
- Root cause: namespace `DragonBoyManager` đã có class riêng `TabControl : UserControl`, che khuất `System.Windows.Forms.TabControl`.
- Fix ở commit `b71a29d`: field `detailTabs` dùng explicit `System.Windows.Forms.TabControl`.
- Cần rebuild lại full solution để xác nhận không còn compile error mới.

### Coverage 50/51 infinite-scan fix — 2026-09-27

- Runtime screenshot: 1 worker heartbeat khỏe, scan cycle 6, Boss Super Broly 29 vẫn ALIVE >12 phút, coverage kẹt 50/51.
- Root cause: stale fallback cũ bắt buộc coverage đủ tuyệt đối.
- Manager giờ track failure từng zone và có near-complete stalled fallback:
  - full coverage: cycle >=3 như cũ;
  - thiếu <=1 zone: cycle >=5 + missing-zone failures >=3, hoặc hard stop ở cycle >=6;
  - chỉ chuyển STALE + STOP, không tự ghi DEAD/killer.
- Snapshot/panel thêm `MissingZones` và `MinimumHealthyScanCycle`.
- Game chỉ forward `DEATH_UNPARSED` nếu death-like announcement có nhắc đúng active target; boss khác chỉ log ignored.
- Code commits: `a18ac89`, `fe834a7`, `091be43`, `f2a63d5`, `ca5018d`.
- Bổ sung targeted parser cho format `X diệt được <Boss> mọi người đều ngưỡng mộ`; chỉ nhận nếu victim khớp active target.
- Cần rebuild full solution + runtime retest case 50/51 để xác nhận.

### Global admiration death parser — 2026-09-27

- Runtime mới xác nhận near-complete fallback đã hoạt động: session dừng ở coverage 50/51 và target chuyển STALE.
- Phần còn thiếu là death evidence: format `X diệt được <Boss> mọi người đều ngưỡng mộ` trước đó chỉ có targeted fallback khi session active.
- `GClass156.TryParseBossDeathAnnouncement` giờ nhận format này ở tầng global, nên announcement tới sau khi session đã STOP vẫn tạo `CmdBossDeath` qua BossZoneScanner và Manager có thể nâng record `STALE -> DEAD`.
- Panel không còn prefix `HB LOST` khi session đã dừng; STALE dùng nhãn `Từ lúc spawn` thay vì `Sống` để tránh hiểu nhầm.
- Commits: `a135b90`, `9dfeffa`.
- Cần rebuild và runtime test bằng một death announcement thực tế.

