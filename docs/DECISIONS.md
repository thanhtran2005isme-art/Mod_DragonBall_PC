# Technical Decisions

Các quyết định ở đây là những điểm đã được chốt để phiên sau không vô tình "sửa ngược" lại.

## D-001 — Code + Git history là source of truth

**Trạng thái:** active

Khi docs và code mâu thuẫn:

1. xác minh code trên `main`;
2. đọc commit/PR liên quan;
3. cập nhật docs.

Lý do: handoff là snapshot và có thể chậm hơn code.

## D-002 — Tách tài liệu AI theo vai trò

**Trạng thái:** active — 2026-09-22

- `AI_HANDOFF.md` = trạng thái hiện tại.
- `ARCHITECTURE.md` = cấu trúc ổn định.
- `DECISIONS.md` = tại sao đã chọn cách này.
- `TROUBLESHOOTING.md` = lỗi đã gặp.
- `history/` = thông tin cũ đã archive.

Lý do: không để một file context tăng vô hạn và buộc AI đọc lại lịch sử dài ở mỗi phiên.

## D-003 — Gameplay ưu tiên build riêng GameAssembly

**Trạng thái:** active

Khi chỉ sửa gameplay, build:

```text
GameAssembly/GameAssembly.csproj
```

thay vì bắt buộc full solution.

Lý do: vòng lặp test nhanh hơn và tránh các lỗi PostBuild local không liên quan ở project khác.

## D-004 — Giữ tương thích .NET Framework 3.5 cho GameAssembly

**Trạng thái:** active

Không dùng API mới chỉ có ở .NET mới hơn nếu chưa có giải pháp tương thích.

Lý do: `GameAssembly` target net35.

## D-005 — SELECT SKILL phải có micro-delay trước ATTACK

**Trạng thái:** active

Behavior hiện tại giữ khoảng **100 ms** sau đổi skill trước hit.

Không reset `GClass164.long_10` ngay sau SELECT SKILL.

Lý do: đã quan sát case client hiện 2 animation nhưng server chỉ tính 1 damage khi packet quá sát nhau.

## D-006 — Adaptive combat dựa trên phản hồi server, không fixed sleep dài

**Trạng thái:** active

Adaptive `/dsq` dùng các tín hiệu như:

- Combat RTT;
- ACK/s;
- Pending;
- Queue Delay;
- adaptive window;
- pacing.

Lý do: server/network congestion cần feedback loop; fixed sleep dài làm throughput kém và không phản ứng đúng với tình trạng thật.

Chi tiết: `docs/NETWORKING.md`.

## D-007 — Đánh tay không bị throttle bởi adaptive /dsq

**Trạng thái:** active

Adaptive gate chỉ áp dụng cho Đồ sát quái theo context hiện tại.

Lý do: diagnostic/throttle tự động không nên làm thay đổi cảm giác điều khiển tay.

## D-008 — KOL local +1 phải có xác nhận last-hit mạnh

**Trạng thái:** active — thay đổi gần nhất 2026-09-21

Không còn cộng local +1 chỉ vì một combat probe "mới".

Flow hiện tại:

1. mob chết phải có bằng chứng đủ mạnh:
   - drop của mình; hoặc
   - đúng attack được gửi khi HP server của mob = 1;
2. chưa cộng ngay tại death;
3. chờ gói tăng SM/TN của chính client;
4. nếu gói hợp lệ đến trong cửa sổ khoảng 750 ms thì local +1;
5. server sync vẫn dùng để đối chiếu/điều chỉnh.

Lý do: giảm false-positive khi người khác last-hit.

## D-009 — README chỉ chứa thông tin ổn định

**Trạng thái:** active

Không cập nhật README cho mọi commit.

README chỉ nên chứa quick start, build path/output, module mapping ổn định và pointer sang docs.

## D-010 — Không dùng SM/TN đơn lẻ làm bằng chứng last-hit KOL

**Ngày:** 2026-09-22

- Packet tăng Sức mạnh/Tiềm năng có thể đến từ hit gây damage bình thường, không chỉ hit kết liễu.
- Vì vậy `-3 type=2` chỉ được coi là tín hiệu diagnostic/reward, không phải bằng chứng độc lập rằng client mình last-hit.
- Trước khi thay công thức local KOL, phải trace sequence own ATTACK -> HP/MISS/DIE -> drop -> SM/TN trong các tình huống có và không có người khác cùng farm.
- Commit diagnostic phải giữ nguyên cách +1 hiện tại để số liệu so sánh không bị trộn với thay đổi thuật toán.

## D-011 — Manager điều phối phiên săn boss đa tài khoản

**Trạng thái:** active — 2026-09-24

- Manager tạo `sessionId`, chọn các account đang kết nối và chia worker.
- Client trước hết lấy `mapId + zone` từ thông báo boss thực tế (`GClass156`), Xmap tới đúng map; chỉ sau đó mới scan zone. Nếu server có zone cụ thể thì thử zone đó trước, rồi mới fallback sang dãy `start + workerIndex + round * workerCount`.
- Account đầu tiên thấy target gửi `FOUND`; Manager dùng map/khu thật đó để rally tất cả worker.
- Sau đổi map/khu phải resolve lại boss từ `GClass158.list_3`; không giữ object boss cũ.
- Focus/di chuyển/đánh tái sử dụng `GClass158` và `GClass159`.
- Thông báo game xác nhận đúng target chết hoặc HP target <= 0 sẽ dừng toàn bộ session.
- Boss chỉ biến mất khỏi entity list không đủ để kết luận chết.
- Event/lệnh cũ khác `sessionId` hiện tại phải bị bỏ qua.
- Callback socket không được trực tiếp thao tác gameplay; START/STOP/RALLY phải được enqueue và drain trong `BossZoneScanner.Update()` trên game loop.

Không được quét map hiện tại khi chưa biết vị trí boss. Không hard-code/đoán map từ tên boss; dùng announcement server đã parse làm source of truth. Nếu chưa có announcement phù hợp, worker ở `WaitingLocation`.


## D-012 — Worker săn boss lỗi không được làm treo cả session

**Trạng thái:** active — 2026-09-26

- Game -> Manager có event `114 FAILED` dành riêng cho lỗi route/khu/target; không dùng `READY` hay `DEAD` để biểu diễn lỗi.
- Rally có giới hạn: 45 giây toàn pha, 3 lần đổi khu, 8 giây chờ target sau khi đã tới đúng map+khu.
- Trong Fighting, target mất được chờ 3 giây để resolve lại; hết grace mới báo `FAILED`.
- Worker `FAILED` được xem là terminal cho worker đó và không chặn các worker `READY`.
- Manager chỉ vào Fighting khi mọi worker còn kết nối đã settle (READY hoặc FAILED) và còn ít nhất một READY.
- Nếu không còn worker READY khả dụng, toàn session dừng.
- Boss biến mất không được chuyển thành DEAD; DEAD vẫn chỉ đến từ HP <= 0 hoặc thông báo server xác nhận đúng target.
- Thông báo server mới được hook tại `GClass144.method_121()`, enqueue vào scanner và drain trên game loop; không dùng cursor index của queue UI vì queue đó xóa phần tử đầu theo thời gian.


## D-013 — Boss location automation tách khỏi HUD và invalidated theo lifecycle

**Trạng thái:** active — 2026-09-26

- `GClass156.list_0` chỉ phục vụ danh sách HUD gần nhất và tiếp tục giới hạn 5 dòng.
- Boss Hunt không dùng `list_0` làm cache automation.
- Automation giữ latest location riêng theo tên boss, gồm map/zone/timestamp, và chỉ dùng record còn fresh tối đa 60 phút.
- Death announcement invalidate record của boss/family tương ứng.
- Không hard-code map boss theo tên.

## D-014 — TCP Manager/Game bắt buộc có frame và reconnect handshake

**Trạng thái:** active — 2026-09-26

- Wire format localhost là `4-byte network-order length + UTF-8 JSON`.
- Cả Manager và Game phải có receive accumulator; không được deserialize trực tiếp mỗi `Receive()`.
- Mọi send phải xử lý partial send.
- Mỗi socket mới của Game phải gửi lại `cmd=0 + accountId`.
- Manager ưu tiên account hiện tại từ `TabData`, thay socket cũ bằng socket mới; disconnect callback của socket cũ không được hạ trạng thái socket mới.
- Game tự retry connect khi Manager tạm thời chưa sẵn sàng.

## D-015 — Xmap Boss Hunt chỉ restart khi dừng hoặc stall

**Trạng thái:** active — 2026-09-26

- Không hủy/restart Xmap theo timer 5 giây nữa.
- Khi Xmap vẫn chạy và còn trong cửa sổ progress, scanner/rally để router tiếp tục.
- Nếu Xmap đã dừng trước khi tới target, cho phép start lại sau cooldown ngắn.
- Nếu Xmap vẫn active nhưng không đổi map trong 30 giây, coi là stall và restart.
- Timeout route tổng hiện là 90 giây.


## D-016 — Zone-list phải fresh theo map; worker dư vào Standby

**Trạng thái:** active — 2026-09-27

- Sau Xmap, không được dùng ngay `GClass144.int_63` vì có thể là dữ liệu map trước.
- Scanner ghi baseline reference + `mapId`, request zone-list mới và chỉ chấp nhận mảng mới khi vẫn ở đúng map đó.
- Không có zone-list mới trong 10 giây -> `FAILED: ZONE_LIST_TIMEOUT`; không fallback về giả định 15 khu.
- Partition không dùng modulo khi `workerCount > availableZoneCount`.
- Worker dư chuyển `Standby`, không đổi khu và không scan trùng.
- Standby không phải FAILED: vẫn giữ session, vẫn nhận RALLY và tham gia đánh khi có finder.
- Announced zone chỉ được ưu tiên bởi worker mà zone đó thuộc partition của nó.


## D-017 — Scan zone dùng entity grace động, không dùng dwell 900 ms cố định

**Trạng thái:** active — 2026-09-27

- `FindTargetBoss()` vẫn chạy trước logic dwell ở mỗi tick.
- Min dwell mỗi zone: 2000 ms.
- Entity stable window: 800 ms.
- Zone thường max dwell: 5000 ms.
- Announced zone max dwell: 7000 ms.
- Entity snapshot hiện dựa vào count của `gclass88_5` và `GClass158.list_3`; thay đổi count reset stable window.
- Max dwell đảm bảo scanner không đứng vô hạn ở zone rỗng/lag.

## D-018 — Boss Hunt có structured protocol log xuyên Game/Manager

**Trạng thái:** active — 2026-09-27

- File: `Data/Errors/BossHuntProtocol.log`.
- Format trường: `source/event/session/account/boss/state/mapId/map/zone/detail` ở Game; Manager dùng cùng prefix và detail tương ứng.
- Chỉ log transition/action/event; không log mỗi frame.
- Các nhóm bắt buộc: socket connect/handshake/disconnect, session/assignment, Xmap, zone-list, zone request/arrival, entity change, FOUND, RALLY, READY, FAILED, DEAD, STOP.
- Runtime bug Boss Hunt nên kèm file log này trước khi thay thuật toán.


## D-019 — Manager là authority chung cho lifecycle/location boss

**Trạng thái:** active — 2026-09-27

- Game chỉ là observer của announcement; mọi spawn/death được gửi lên Manager bằng `115/116`.
- Manager giữ canonical record: boss, map, zone, spawn/death time, raw message, killer nếu parse được và source accounts.
- Manager sync location/invalidate xuống mọi client bằng `103/104`.
- START_SCAN ưu tiên canonical location từ Manager; scanner không tự route bằng cache cục bộ khi Manager chưa xác nhận.
- Client reconnect gửi lại cache hiện có; spawn observation cũ hơn death đã biết không được phép hồi sinh boss.

## D-020 — Assignment phải có generation

**Trạng thái:** active — 2026-09-27

- `sessionId` một mình không đủ sau reassign.
- Mỗi partition có `assignmentGeneration`.
- Reassign tăng generation.
- Event session-scoped chỉ hợp lệ khi cả sessionId và generation bằng hiện tại.
- STOP generation mới được phép dừng scanner generation cũ trong watchdog/reassign.

## D-021 — Manager giữ zone ledger và tự phát hiện dò trùng

**Trạng thái:** active — 2026-09-27

- Game gửi telemetry `ZONE_ENTER / ZONE_CLEAR / ZONE_FAILED`.
- Ledger key là `generation + mapId + zone`.
- Enter trùng zone đang active hoặc zone đã được worker khác scan trong cùng generation sinh cảnh báo duplicate.
- Lịch sử khu được giữ theo worker để audit, không chỉ dựa vào UI hiện thời.

## D-022 — Heartbeat là nguồn liveness của Boss Hunt, không dùng Socket.Connected một mình

**Trạng thái:** active — 2026-09-27

- Game heartbeat mỗi 2 giây khi Boss Hunt active.
- Manager watchdog timeout sau 8 giây.
- Worker timeout bị đánh dấu Unresponsive/Failed.
- Nếu đang Scanning, Manager reassign worker khỏe bằng generation mới.
- Socket.Connected chỉ còn là điều kiện transport phụ, không phải bằng chứng worker gameplay còn sống.


## D-023 — Boss lifecycle hiển thị Unknown/Alive/Dead/Stale và giữ timestamp sớm nhất

**Trạng thái:** active — 2026-09-27

- `Alive` chỉ áp dụng khi spawn canonical còn trong freshness window.
- Spawn canonical quá 60 phút nhưng chưa có death -> `Stale`; không dùng làm location khởi động phiên.
- Không có record -> `Unknown`; death đã xác nhận -> `Dead`.
- Cùng một spawn/death được nhiều client báo: giữ timestamp observation sớm nhất.
- Raw announcement, source accounts và killer (nếu có) phải được giữ để audit.

## D-024 — Worker observability phải đủ để nghiệm thu partition thực tế

**Trạng thái:** active — 2026-09-27

Manager phải giữ và expose tối thiểu:

- assigned zones;
- scanned zones;
- unique coverage;
- scan cycle;
- zone-enter time/dwell;
- entity count / boss count;
- zone change failure count;
- rally route/zone state;
- target HP;
- finder username + ID;
- session timeline.

Dữ liệu này đến từ `117 TELEMETRY` + `118 HEARTBEAT`, không suy ngược từ label UI.

## D-025 — Boss Hunt diagnostics không dùng file chung xuyên process

**Trạng thái:** active — 2026-09-27

- Game và Manager ghi file riêng theo PID.
- File nằm dưới absolute runtime base path `Data/Errors`.
- Không dựa vào current working directory.
- Không dùng `lock` nội-process để giả định an toàn cho nhiều process.

## D-026 — Manager quyết định partition zone cuối cùng

**Trạng thái:** active — 2026-09-27

- Game chỉ báo `ZONE_CAPACITY`, không tự chia round-robin cuối cùng.
- Manager đợi toàn bộ worker khỏe báo `maxZone`.
- Canonical max là giá trị nhỏ nhất giữa các worker, ưu tiên safety hơn tận dụng zone mà chỉ một client nhìn thấy.
- Manager gửi explicit `assignedZones` bằng command `105 ZONE_ASSIGNMENT`.
- Scanner chỉ quét các zone trong danh sách này.
- Worker dư được Standby.

## D-027 — Boss catalog là dữ liệu runtime, không hard-code trong UI

**Trạng thái:** active — 2026-09-27

- File runtime: `Data/BossHuntBosses.txt`.
- Dropdown đọc file khi mở.
- Boss từ target nhập tay hoặc announcement mới được tự ghi nhớ.
- Thêm boss mới không yêu cầu sửa `TabBossHunt.cs`.

## D-028 — Boss Hunt log có rotation và viewer trực tiếp

**Trạng thái:** active — 2026-09-27

- Mỗi PID giữ file riêng như D-025.
- Rotate ở 5 MB.
- Giữ 3 archive cho mỗi active log path.
- Cleanup file cùng loại cũ hơn 14 ngày.
- Manager có `BossHuntLogViewer` để xem trực tiếp các log Game/Manager gần nhất.

## D-029 — Performance worker được đo bằng event thật

**Trạng thái:** active — 2026-09-27

- `ZoneClearCount` tăng ở `ZONE_CLEAR`.
- throughput hiển thị `zones/minute` từ thời điểm worker tham gia session.
- `FailureCount` tăng ở zone failure, FAILED hoặc watchdog.
- `TimeoutCount` tăng cho FAILED chứa TIMEOUT và watchdog timeout.
- Không suy performance từ label UI.

## D-030 — Tách HELLO và ACK của Manager/Game handshake

**Trạng thái:** active — 2026-09-27

- `cmd=0` chỉ dùng Game -> Manager để gửi `accountId`.
- `cmd=99` chỉ dùng Manager -> Game để ACK đúng `accountId`.
- Manager không chủ động gửi `cmd=0` khi vừa accept socket.
- Game không sync boss cache trước khi nhận ACK.
- HELLO được retry trên cùng socket trước khi reconnect để tránh vòng connect/close mù.

## D-031 — Không dùng InvalidDataException trong GameAssembly

**Trạng thái:** active — 2026-09-27

- CI target net35 có thể compile `System.IO.InvalidDataException`, nhưng runtime Unity client hiện tại ném `TypeLoadException` khi JIT method chứa type này.
- Game-side framing/parser phải dùng `IOException` hoặc exception runtime-compatible khác.
- Manager net48 không bị giới hạn này.
- Runtime log 2026-09-27 cho thấy chuỗi `CONNECTED -> TypeLoadException -> CONNECT_RETRY`, trước khi có `HANDSHAKE_TX`.

## D-032 — Không suy death/killer từ scan exhaustion

**Trạng thái:** active — 2026-09-27

- Death/killer chỉ được xác nhận từ announcement parse được hoặc HP<=0 khi đang giữ entity mục tiêu.
- Scan không thấy boss không được phép tự ghi `Dead` hay tự tạo killer.
- Nếu boss đã spawn >=45 giây và toàn bộ worker khỏe đạt scan cycle >=3 mà chưa FOUND, record chuyển `Stale`, location bị invalidate và session dừng.
- Mục đích là chặn vòng scan vô hạn mà vẫn giữ đúng tính xác thực dữ liệu.

## D-033 — Log raw announcement để audit parser boss

**Trạng thái:** active — 2026-09-27

- Mọi chatVip đi qua Boss Hunt được ghi event `GAME_ANNOUNCEMENT/RAW`.
- Parse thành công ghi `DEATH_PARSED` hoặc `SPAWN_PARSED`.
- Message có dấu hiệu death nhưng parser không nhận được ghi `DEATH_UNPARSED`.
- Log rotation hiện có giới hạn dung lượng, nên raw diagnostic được phép bật thường trực.

## D-034 — Scan exhaustion yêu cầu coverage hoàn chỉnh

**Trạng thái:** active — 2026-09-27

- `scanCycle` chỉ cho biết worker đã quay vòng danh sách assignment; không chứng minh toàn bộ zone đã được clear.
- Chỉ cho phép `SCAN_EXHAUSTED` khi `uniqueCoverage >= totalZones` và `totalZones > 0`.
- Zone fail không được tính là clear coverage.
- Nếu coverage chưa đủ, session tiếp tục scan và status phải cho thấy coverage hiện tại.
- Raw announcement liên quan boss được forward lên Manager timeline để audit death/killer parser trực tiếp.

## D-035 — Chia đều toàn bộ zone list theo số account

**Trạng thái:** active — 2026-09-27

- `totalZones` lấy từ toàn bộ zone list của map (`int_63.Length`), không lấy từ `startZone..maxZone`.
- `Khu bắt đầu` chỉ xoay thứ tự quét: ví dụ 17 khu, start K5 -> `K5..K16,K0..K4`.
- 1 account nhận 100% danh sách khu.
- N account chia contiguous cân bằng; số khu giữa hai account chênh tối đa 1.
- Với 17 khu / 3 account -> 6 + 6 + 5.
- Nếu account nhiều hơn khu, account dư nhận assignment rỗng và Standby.
- Không có overlap trong cùng assignment generation.

## D-036 — Session săn boss khóa vào một spawn instance

**Trạng thái:** active — 2026-09-27

- Selector người dùng như `Super Broly` chỉ dùng để chọn family.
- Khi Start có spawn hợp lệ, session khóa concrete boss name + map + zone + spawn observation.
- Nếu chưa có spawn lúc Start, spawn matching đầu tiên bind target.
- Spawn mới cùng family trong khi session đang chạy chỉ được cache/history; không đổi route, không đổi entity target.
- Game bỏ qua manager sync khác locked instance cho mục đích routing.
- Local entity/death matching dùng concrete locked target, tránh sibling spawn chết làm dừng nhầm session.
- Sau khi target hiện tại DEAD/STOP, phiên tiếp theo mới được chọn spawn mới nhất còn sống.

## D-037 — Catalog boss học từ runtime nhiều client

**Trạng thái:** active — 2026-09-27

- Repo không có master list boss đầy đủ đáng tin cậy; danh sách 23 tên ban đầu chỉ là seed/fallback.
- Game gửi `CmdBossCatalog = 119` về Manager ngay sau handshake và định kỳ khoảng 15 giây.
- Nguồn catalog Game: boss hiện diện trong `GClass158.list_3`, cache announcement `GClass156`, selector/target hiện tại.
- Manager batch-merge catalog vào `Data/BossHuntBosses.txt`.
- Nếu tên concrete bắt đầu bằng family đã biết, ví dụ `Super Broly 28`, catalog giữ family `Super Broly` để tránh dropdown phình theo từng instance.
- Boss hoàn toàn mới vẫn được thêm nguyên tên.
- `TabBossHunt` theo dõi catalog revision và tự refresh dropdown.

## D-038 — Ưu tiên combat packet -60 để xác nhận death + killer

**Trạng thái:** active — 2026-09-27

- VIP packet `93` vẫn dùng cho spawn/death text nếu server broadcast.
- Nguồn death+killer mạnh hơn là combat packet `GClass12 case -60`: có attacker charId, target charId và server boolean `isDie`.
- Chỉ nhận combat death khi target là boss-like `GClass78` (`targetId < 0`) và `isDie=true`.
- Network handler chỉ enqueue evidence; `BossZoneScanner.Update()` xử lý trên game loop.
- Scanner chỉ accept khi target name khớp concrete session target và map/zone khớp lock hiện tại.
- Killer name resolve từ attacker charId; nếu không resolve được vẫn giữ `killerId` và fallback `#<id>`.
- HP packet `56` / entity remove `-6` không đủ để suy killer; chỉ dùng làm fallback death/lost-target nếu cần.

## D-039 — Announcement cập nhật death ngay; combat chỉ bổ sung/xác nhận

**Trạng thái:** active — 2026-09-27

- VIP announcement đi qua `GClass144.method_121()` và được enqueue vào `BossZoneScanner`; parse/send Boss Hunt thực hiện trên game loop.
- Khi announcement parse được boss chết, Game gửi ngay `CmdBossDeath=116` với boss name, killer text, raw announcement và timestamp. Nếu đó là target của session, local scanner dừng sau khi gửi evidence.
- `CmdDead=112` không còn là đường chỉ dừng session: Manager chuyển nó thành death fallback có killer rỗng, vì vậy UI vẫn có death time nhưng tuyệt đối không tự đoán người hạ.
- Manager merge nhiều evidence vào cùng canonical record, kể cả khi record đã Dead: announcement và combat `-60` đến trước/sau đều bổ sung cho nhau thay vì tạo/ghi đè record mới.
- Ưu tiên dữ liệu hiển thị: tên killer thực > fallback `#id`; raw announcement thực > raw `combat:` / `fallback:`. `killerId` từ combat vẫn được bổ sung khi có.
- Parser hỗ trợ cả dạng victim-first (`BOSS X ... bởi Y`) và killer-first có marker BOSS (`Y ... BOSS X`).
- Không suy killer từ HP/entity disappearance.

## D-040 — Boss Hunt dùng requested worker scope và death evidence hiển thị rõ

**Trạng thái:** active — 2026-09-27

- Death của phiên hiện tại được phép kết thúc session ngay cả khi chưa kịp `TARGET_LOCK`, nhưng chỉ khi `sessionId + assignmentGeneration` đúng phiên đang chạy và tên boss khớp target family. Death global/session 0 không được dùng để dừng nhầm phiên.
- Canonical boss record giữ `DeathEvidence`: `ANNOUNCEMENT`, `COMBAT_-60`, `FALLBACK`; nhiều nguồn được merge, không ghi đè mất nguồn mạnh hơn.
- Nếu Game nhận thông báo nghi là death nhưng parser không hiểu, gửi telemetry `DEATH_UNPARSED` lên Manager để panel hiển thị raw ngay.
- Start session có requested-worker scope. Chế độ mặc định lấy toàn bộ account đang connected; UI có thể truyền danh sách account đang được chọn ở tab ACCOUNT.
- Requested account được giữ qua disconnect. Reconnect khi Scanning làm tăng generation và chia lại khu; reconnect khi Rallying/Fighting được bootstrap bằng START_SCAN rồi RALLY để quay lại target hiện tại.
- Worker được chọn nhưng đang offline vẫn có placeholder `Chờ kết nối` trên panel.
- Panel 765x480 không thêm dày cột; dùng summary + tabs `BOSS / SỰ KIỆN / TIMELINE`, hiển thị StopReason, healthy/requested worker, heartbeat max, target lock, death evidence và unparsed warning.
- Double-click worker focus/restore cửa sổ game tương ứng. Boss dropdown bật autocomplete; âm báo có toggle; log viewer lọc theo session/account/event/boss và hỗ trợ copy.
- Runtime multi-account/reconnect vẫn là gate trước khi bỏ Draft.

