# Changelog

Ghi ngắn gọn các thay đổi quan trọng, mới nhất ở trên.  
Không dùng file này để giải thích kiến trúc dài; chi tiết nằm trong `docs/`.

## 2026-09-27

- Boss Hunt observability: thêm Unknown/Alive/Dead/Stale, cache age, exact spawn/death time, lifetime, raw announcement, source account và finder username+ID.
- Worker dashboard thêm assigned/scanned zones, unique coverage, scan cycle, zone dwell, entity/boss count, target HP, zone-fail count và rally telemetry.
- Thêm live session timeline từ spawn/route/zone/FOUND/RALLY/READY tới death/stop.
- Giữ timestamp observation sớm nhất khi cùng announcement được nhiều account báo.
- Tách Boss Hunt log theo PID, dùng absolute runtime path; sửa tương thích `.NET 3.5` cho `Path.Combine`.
- `fe5379d`, `5f03c50`, `836f4fa` full workflow SUCCESS.


- Critical Boss Hunt hardening: Manager trở thành canonical source cho spawn/death/location; protocol thêm `103/104/115/116/117/118`.
- Thêm assignment generation chống stale event sau disconnect/reassign.
- Thêm central zone ledger + per-worker scan history + duplicate-zone warning.
- Thêm heartbeat 2s và Manager watchdog 8s để phát hiện worker treo dù socket còn Connected.
- Death parser giữ killer khi raw announcement có thông tin; Manager lưu spawn/death timestamp, raw message và source account.
- Panel SĂN BOSS hiển thị generation, worker, scanned zones, last signal, duplicate warning và lifecycle boss.
- Chặn cache spawn cũ hồi sinh boss sau death/reconnect.
- Commits `f21f72d`, `7198770`, `786c3ff` đều full workflow SUCCESS.


- Hardening P2 Boss Hunt: thay dwell 900 ms bằng entity grace động (min 2s, stable 0.8s, max 5s; announced zone max 7s).
- Thêm structured `Data/Errors/BossHuntProtocol.log` xuyên Game/Manager cho socket, route, zone, entity và state transition.
- Commit `3a94b2e` đã qua bước MSBuild full solution.


- Hardening P1 Boss Hunt: chờ zone-list mới theo đúng map, timeout nếu không có fresh response, không dùng dữ liệu `int_63` stale.
- Worker dư so với số khu chuyển `Standby` thay vì modulo scan trùng; Standby vẫn nhận RALLY.
- Manager hiển thị trạng thái tải zone-list và worker dự phòng.
- Commit `5546cde` đã qua bước MSBuild full solution.


## 2026-09-26

- Hoàn tất hardening P0 Boss Hunt: Xmap progress-aware, cache vị trí boss riêng + freshness/invalidate death, TCP length-prefix framing, accumulator và reconnect handshake lại account.
- `8d6e0f3` và `8d108a9` đều CI SUCCESS full solution.


- Fix Boss Hunt không còn quét map hiện tại khi boss ở map khác: dùng announcement server để resolve `bossName -> mapId + zone`, Xmap tới đúng map rồi mới scan.
- Nếu chưa có vị trí boss, worker ở trạng thái chờ; nếu có zone từ announcement thì ưu tiên zone đó trước khi fallback round-robin.
- Manager hiển thị trạng thái `Chờ vị trí boss` / `Đang tới <map>` trong pha chuẩn bị scan.
- Commit `c6c9c79` đã build full solution SUCCESS; `d54131d` dọn reset state scan-map.


- Thêm event Boss Hunt `114 FAILED`; worker route/khu/target lỗi được cô lập thay vì làm toàn session treo ở Rallying.
- Giới hạn rally: 45 giây toàn pha, 3 lần đổi khu, 8 giây chờ target; Fighting cho target mất grace 3 giây trước khi báo lỗi.
- Manager cho phép session tiếp tục Fighting khi một số worker FAILED nhưng vẫn còn worker READY; dừng nếu không còn worker khả dụng.
- Hook thông báo VIP mới trực tiếp từ `GClass144.method_121()` vào queue của `BossZoneScanner`, tránh bỏ lỡ/đọc lại do queue UI xóa phần tử đầu.
- Commit code `1786046` và `0ee000c`; run `36254447048` đã qua bước MSBuild full solution.

## 2026-09-25

- Sửa layout tab `SĂN BOSS` để toàn bộ control nằm trong khung Manager thực tế `765x480`.
- Chuyển START/STOP/RALLY sang queue thread-safe; callback socket không còn trực tiếp thao tác trạng thái gameplay.
- Khóa Start ở cả UI và coordinator khi session đang chạy; tự `Stopped` nếu toàn bộ worker mất kết nối.
- Tắt Auto Boss cũ trong lúc scan để tránh đánh nhầm boss khác, sau đó khôi phục setting khi phiên dừng.

## 2026-09-24

- Thêm tab top-level `SĂN BOSS` cho DragonBoyManager trên branch `feat-boss-hunt-manager`.
- Thêm `BossHuntCoordinator` để chia zone round-robin cho các account đang kết nối, quản lý `sessionId`, FOUND/RALLY/FIGHTING/STOP và reassign khi worker mất kết nối.
- Thêm `BossZoneScanner` phía GameAssembly: tự dò khu, resolve boss theo tên, report FOUND/DEAD/READY, rally tới map+khu thật và tái sử dụng focus/auto boss hiện có.
- Boss mục tiêu chết từ thông báo game hoặc HP <= 0 sẽ dừng toàn bộ phiên; boss chỉ biến mất khỏi entity list không được coi là chết.
- GitHub Actions run `36029651400` đã build full solution, upload artifact, nén và phát hành thành công; runtime nhiều account vẫn cần test trước khi merge vào `main`.

## 2026-09-22

- Thêm `KOLProtocol.log` để trace ATTACK/probe, HP/MISS/DIE, drop owner, SM/TN, skip/timeout/overlap và correction khi server sync; chưa thay đổi công thức +1 KOL.
- Ghi nhận SM/TN là reward theo damage, không phải bằng chứng last-hit độc lập; dùng diagnostic thực tế trước khi sửa thuật toán.
- Thêm hệ thống bàn giao cho AI/agent: `AGENTS.md`, `docs/AI_HANDOFF.md`, `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, `docs/TROUBLESHOOTING.md`, `docs/history/2026-09.md`; README/PROJECT_CONTEXT được nối vào luồng đọc mới.

## 2026-09-20

- SV15 slot contender: tự retry khi server báo `quá tải`/`vui lòng đợi` hoặc packet 122, mỗi lần có jitter 650–1100 ms và chỉ gửi request mới sau phản hồi trước.
- Giai đoạn 2 adaptive combat: ACK rate + adaptive window + packet pacing cho Đồ sát quái; giảm request khi session nghẽn và tự tăng lại khi RTT phục hồi.
- Thêm Combat RTT: đo từ lúc client gửi ATTACK mob tới lúc server trả HP/death/miss của đúng mob; HUD hiển thị Combat RTT và số attack Pending.
- Fix diagnostic network: gửi ping `-120/-121` lần đầu sau khi kết nối để HUD không đứng ở `0/0ms`.
- Giai đoạn 1 network: bỏ sleep 5 ms ở receive loop, bật TCP NoDelay, giới hạn ping 1 giây/lần và thêm HUD Ping/Queue/Proxy/Server.
- `c2da2c6` — Viết tài liệu bàn giao build và cấu trúc dự án.
- `5005d68` — Đồng bộ micro-delay 100 ms đổi skill cho Auto Attack và Auto Train.
- `3c726f9` — Bỏ delay nhân tạo lớn của Tự động đánh, dùng cooldown thật của skill.
- `9ec1690` — Luân phiên skill Auto Train và xử lý delay khi đổi skill.
- `74a6c01` — Đổi domain splash thành `kaitokid.com`.
- `51ef5e8` — Dùng `kaitokid.txt` cho logo màn hình login.
- `a71cf1e` — Bỏ logo Thanh VLC khỏi panel HUD.
- `f75f3da` — Fix runtime logo KaitoKid và bỏ fallback Thanh VLC.
- `5f423e0` — Fix đường dẫn logo KaitoKid cho .NET 3.5.
