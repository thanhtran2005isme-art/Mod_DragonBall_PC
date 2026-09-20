# Network Diagnostics

## Kết nối chính

- Session chính: `GameAssembly/GClass14.cs`
- Session phụ: `GameAssembly/GClass85.cs`
- Server list: `GameAssembly/GClass134.cs`
- SV15: `dragon15.teamobi.com:14445`
- Packet ping: `-120` và `-121`
- Handler ping response: `GameAssembly/Assets.src.f/Controller2.cs`

## Giai đoạn 1 — 2026-09-20

Đã thực hiện:

- bỏ `Thread.Sleep(5)` **chỉ ở receive loop** của GClass14/GClass85;
- giữ sleep của send worker để tránh busy-spin;
- đặt `TcpClient.NoDelay = true` cho kết nối trực tiếp và proxy;
- sau khi nhận ping response, đợi khoảng 1000 ms mới gửi ping tiếp;
- HUD hiển thị:
  - server hiện tại;
  - ping `-120/-121`;
  - proxy ON/OFF;
  - send queue chính;
  - receive queue chính.

Mục tiêu là phân biệt:

```text
ping cao + receive queue cao -> client/main-thread đang backlog
ping cao + queue thấp          -> nghi server/route nhiều hơn
send queue cao                 -> socket/server đang không tiêu thụ kịp
```

Giai đoạn 1 **không tự throttle Auto Train**. Adaptive Auto Train là bước sau khi đã có số liệu.


### Fix khởi động ping

Bản đầu của Giai đoạn 1 chỉ lên lịch ping tiếp theo sau khi đã nhận response, nên nếu chưa có request đầu tiên HUD có thể đứng ở `Ping 0/0ms`.

Đã sửa để sau khi session chính kết nối:

```text
connect
-> gửi -120 và -121 lần đầu
-> nhận response
-> đo RTT
-> chờ ~1000 ms
-> gửi vòng tiếp theo
```

Có cờ in-flight riêng để không spam ping mỗi frame trong lúc đang chờ response.


## Combat RTT diagnostic

Đo riêng độ trễ gameplay thay vì suy luận chỉ từ packet ping.

Mốc bắt đầu:

```text
GClass7.method_73()
-> ATTACK mob được đưa vào send queue
-> lưu mobId + timestamp
```

Mốc phản hồi server:

- `case -9`: server cập nhật HP/damage mob;
- `case -12`: mob chết;
- packet `45`: attack miss.

Các attack đang chờ được lưu FIFO theo `mobId`. Khi server trả state của mob, diagnostic ghép với attack cũ nhất đang chờ của đúng mob.

HUD:

```text
Combat 4820ms | Pending 7
```

- `Combat`: RTT của attack mob gần nhất đã nhận phản hồi.
- `Pending`: số attack mob đã gửi nhưng chưa ghép được với phản hồi server.
- probe quá 30 giây được tự xóa; danh sách được chặn tối đa 128 phần tử.

Đây là diagnostic, chưa dùng Combat RTT để throttle Auto Train.


## Giai đoạn 2 — Adaptive combat scheduler

Mục tiêu: không dùng kiểu `lag -> sleep vài giây -> farm lại`. Đồ sát quái được điều tiết theo phản hồi thật của server.

### Metrics

- `Combat RTT`: ATTACK -> HP/death/miss.
- `ACK/s`: số phản hồi combat khớp attack mỗi giây, tính trên cửa sổ 5 giây.
- `Min RTT`: Combat RTT thấp nhất của session hiện tại.
- `Queue Delay`: `Combat RTT - Min RTT`.
- `Pending`: attack đã gửi nhưng chưa nhận response.
- `Adaptive Window`: số attack tối đa được phép đang bay.
- `Pace`: khoảng cách gửi tối thiểu khi hệ thống nhận thấy congestion.

### Điều khiển

- Window khởi đầu 3, tối thiểu 2, tối đa 10.
- RTT/queue delay thấp: tăng window từ từ.
- RTT/queue delay cao: giảm window theo kiểu multiplicative decrease.
- Nếu có pending nhưng lâu không có ACK, window tiếp tục tự giảm.
- Ping `-120/-121` chỉ là tín hiệu phụ để cap window nhanh khi spike; quyết định chính vẫn dựa Combat RTT/ACK/Pending.
- Khi congestion, pace được tính từ ACK rate để không tạo request nhanh hơn nhiều so với tốc độ server đang trả.
- Đánh tay không bị throttle. Gate chỉ áp dụng khi `/dsq` đang bật.
- Khi mob chết, xóa toàn bộ pending còn lại của đúng mob đó để tránh giữ request stale trong diagnostic/scheduler.

HUD mới:

```text
Combat 6959ms | Pending 3 | ACK 2.4/s
Adaptive W:3 | Pace 379ms | QD 6880ms
```

Khi server/session hồi, RTT giảm và ACK tiếp tục về thì window tự tăng lại; không cần timer resume cố định.


## SV15 — Slot contender

SV15 có hai nhánh phản hồi login đã quan sát:

- packet `-26` với text như `quá tải` / `vui lòng đợi`: login bị từ chối ngay;
- packet `122` (`second login`) kèm số giây.

Riêng SV15, mod chạy slot contender:

```text
LOGIN
-> server từ chối / yêu cầu second login
-> chờ ngẫu nhiên 650–1100 ms
-> LOGIN lại
-> chỉ lên lịch lần kế tiếp sau khi server đã phản hồi
```

Không có vòng spam song song và không tạo nhiều login request đang bay cùng lúc. Khi nhận map-info `-24` (đã vào game), retry pending được hủy ngay.

Các server khác không dùng slot contender; packet `122` giữ đúng thời gian chờ server gửi.


### Credential source khi retry SV15

Game mở từ `DragonBoyManager.exe` nhận `--username`, `--password`, `--server` và lưu trong `GClass172`.
Slot retry phải dùng lại đúng nguồn này. Không dùng `GClass133.method_9()` làm đường chính vì hàm đó đọc `GClass1["acc"] / ["pass"]`, là cache của màn login và có thể khác tài khoản mà Manager vừa mở.

Luồng hiện tại:

```text
DragonBoyManager
-> command line username/password/server
-> GClass172
-> login lần đầu

SV15 quá tải
-> slot retry
-> GClass172 credential
-> gửi login lại
```

Chỉ fallback sang login-cache cũ nếu không có credential từ Manager.


### Direct retry — không quay lại màn login

Khi game được mở bằng `DragonBoyManager.exe`, username/password/server chỉ cần đọc một lần từ command line và được giữ trong `GClass172`.

Retry SV15 hiện gọi trực tiếp:

```text
GClass7.method_38(managerUsername, managerPassword, version, 0)
```

Không còn `GClass133.switchToMe()` trước mỗi retry, nên không quay lại LoginScr, không nạp lại acc/pass và không chọn lại server. Fallback qua `GClass133.method_9()` chỉ dùng khi không có credential Manager.


### Khóa auto-login cũ khi slot contender hoạt động

Sau phản hồi `quá tải`, các cờ login cũ được hạ xuống. Trước đây `GClass172.method_3()` nhìn thấy trạng thái này và tự chạy lại flow Manager (~2 giây), dẫn tới `method_4() -> LoginScr.switchToMe()` dù slot contender đã có direct retry riêng.

Hiện slot contender có hai trạng thái:

```text
PENDING   = đang chờ jitter trước lần thử tiếp theo
IN_FLIGHT = đã gửi LOGIN và đang chờ server phản hồi
```

Trong cả hai trạng thái, `GClass172.method_3()` return ngay. Khi server trả `quá tải`/packet 122 thì IN_FLIGHT -> PENDING; khi nhận map-info `-24` hoặc lỗi login khác thì contender được hủy.
