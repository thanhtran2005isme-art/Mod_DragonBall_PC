# Changelog

Ghi ngắn gọn các thay đổi quan trọng, mới nhất ở trên.  
Không dùng file này để giải thích kiến trúc dài; chi tiết nằm trong `docs/`.

## 2026-09-20

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
