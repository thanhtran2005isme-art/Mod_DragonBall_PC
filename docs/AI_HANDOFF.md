# AI_HANDOFF — Trạng thái hiện tại

> Repo: `thanhtran2005isme-art/Mod_DragonBall_PC`  
> Branch chính: `main`  
> Cập nhật handoff: 2026-09-22  
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
- chỉ stage kill candidate khi có bằng chứng drop của mình hoặc đòn gửi lúc HP server của mob = 1;
- chưa +1 ngay ở packet mob chết; chờ gói tăng SM/TN của chính client trong cửa sổ khoảng 750 ms rồi mới xác nhận local +1.

Điểm này nhạy với packet ordering, vì vậy khi sửa phải đọc commit gần nhất và test bằng phản hồi server thật.

## 5. Commit gần đây đáng chú ý

Trước khi thêm hệ thống tài liệu AI này, các commit gần nhất gồm:

```text
17cb74f Update README with build instructions
712dc3f Xac nhan KOL bang kill-shot 1 HP va goi tang SM TN
40785a2 Siết KOL local theo last hit của người chơi
8544663 Theo doi KOL local khi farm ngoai Dao Kame
8a015c5 Replay đủ chuỗi menu KOL 2 bước
006a730 Học và đồng bộ KOL bằng packet 32 thực tế
bfdc638 Bắt KOL trực tiếp từ packet 22 thực tế
```

Không có PR gần đây được tìm thấy tại thời điểm tạo handoff; thay đổi đang đi trực tiếp qua `main`.

## 6. Rủi ro/lỗi đã biết

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
