# Troubleshooting

Tài liệu này ghi các lỗi đã gặp và cách chẩn đoán. Nếu một lỗi mới được giải quyết và có khả năng lặp lại, thêm vào đây.

## 1. Build báo DLL đang bị lock / copy không được

### Triệu chứng

Không ghi đè được:

```text
Output\Dragon ball_237b_Data\Managed\Assembly-CSharp.dll
```

### Xử lý

```bat
taskkill /F /IM "Dragon ball_237b.exe" 2>nul
taskkill /F /IM DragonBoyManager.exe 2>nul
```

Sau đó rebuild.

## 2. Full solution fail ở PostBuild nhưng GameAssembly compile được

### Đã từng gặp

Một số PostBuild của `AccountManager` / `LicenseCheckBypass` copy file vào `Output` / `Output\Lib` và có thể fail do môi trường local.

### Cách xử lý

Nếu task chỉ sửa gameplay:

1. build riêng `GameAssembly/GameAssembly.csproj`;
2. kiểm tra DLL trong Output;
3. dùng GitHub Actions để xác nhận full solution sau push khi cần.

Không vội kết luận source gameplay sai chỉ vì PostBuild của project khác fail.

## 3. Compile GameAssembly lỗi vì API không tồn tại

### Nguyên nhân thường gặp

`GameAssembly` target .NET Framework 3.5.

### Cách xử lý

- kiểm tra API/overload có tồn tại trên net35 hay không;
- ưu tiên implementation tương thích cũ;
- tránh vô tình dùng syntax/API từ framework mới.

## 4. Thấy 2 animation đánh nhưng server chỉ trừ 1 damage

### Kiểm tra

- có vừa SELECT SKILL rồi ATTACK quá sát nhau không;
- `GClass164.long_10` có bị reset quá sớm không;
- micro-delay khoảng 100 ms có còn được giữ không.

Không xác nhận fix chỉ dựa trên animation; phải kiểm tra HP/damage phía server.

## 5. HUD ping đứng 0/0 ms sau connect

Network diagnostic từng có phiên bản chỉ schedule ping tiếp sau khi đã nhận response, nên chưa có request đầu tiên thì HUD đứng 0.

Logic hiện tại đã gửi probe đầu tiên sau connect và dùng cờ in-flight.

Nếu lỗi quay lại, đọc `docs/NETWORKING.md` và kiểm tra flow connect -> ping -120/-121 -> response.

## 6. Adaptive /dsq chậm hoặc pending tăng

Đọc HUD:

- Combat RTT;
- Pending;
- ACK/s;
- Adaptive Window;
- Pace;
- Queue Delay.

Gợi ý chẩn đoán:

```text
RTT cao + pending tăng + ACK thấp
-> congestion/server xử lý chậm

ping cao nhưng combat bình thường
-> không nên chỉ dựa ping để throttle

send/receive queue cao
-> xem lại socket/main-thread backlog
```

Chi tiết thuật toán: `docs/NETWORKING.md`.

## 7. KOL local không tăng dù vừa giết quái

Logic hiện tại cố tình chặt.

Kiểm tra:

1. KOL task đã được nhận diện/sync chưa;
2. mob death có bằng chứng drop của mình hoặc one-HP kill-shot candidate không;
3. packet tăng SM/TN của chính client có đến trong khoảng xác nhận không;
4. HUD KOL có state `P:` pending mob và `T:` timeout tăng không;
5. server sync sau đó có trả progress mới không.

Nếu timeout tăng nhiều, đọc commit gần nhất liên quan `KOLTracker.cs`, `GClass7.cs`, `GClass12.cs` trước khi nới lỏng điều kiện.

## 8. KOL sync không replay đúng menu

KOL đã hỗ trợ:

- packet 22;
- packet 32;
- packet 32 với chuỗi menu hai bước.

Nếu menu server thay đổi:

1. quan sát packet interaction thực tế mới;
2. kiểm tra candidate/query path trong KOLTracker;
3. không hardcode từ trí nhớ nếu packet/menu ID hiện tại khác.

## 9. Đang sửa nhầm repo/folder local

Path đang được tài liệu hóa:

```text
C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1
```

Folder cũ từng được cảnh báo không dùng:

```text
C:\Users\Admin\Pictures\ModThanhLC
```

Trước khi sửa local:

```bat
git rev-parse --show-toplevel
git remote -v
git status
git log -5 --oneline
```

## 10. Runtime logs

Thường xem:

```text
Output\Data\Errors\
```

Các log đã biết:

```text
SendAttack.txt
startMurderingMob.txt
logo_error.log
frame_render.log
panel_exit.log
```

Chọn log theo module thay vì đọc tất cả.
