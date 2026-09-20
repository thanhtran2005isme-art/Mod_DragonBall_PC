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
