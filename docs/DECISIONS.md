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
- Client tự quét dãy khu `start + workerIndex + round * workerCount`.
- Account đầu tiên thấy target gửi `FOUND`; Manager dùng map/khu thật đó để rally tất cả worker.
- Sau đổi map/khu phải resolve lại boss từ `GClass158.list_3`; không giữ object boss cũ.
- Focus/di chuyển/đánh tái sử dụng `GClass158` và `GClass159`.
- Thông báo game xác nhận đúng target chết hoặc HP target <= 0 sẽ dừng toàn bộ session.
- Boss chỉ biến mất khỏi entity list không đủ để kết luận chết.
- Event/lệnh cũ khác `sessionId` hiện tại phải bị bỏ qua.

V1 quét trên map hiện tại của từng worker; không tự đoán map spawn chỉ dựa vào tên boss.
