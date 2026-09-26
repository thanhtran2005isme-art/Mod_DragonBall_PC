# AI_HANDOFF — Trạng thái hiện tại

> Repo: `thanhtran2005isme-art/Mod_DragonBall_PC`  
> Branch chính: `main`  
> Cập nhật handoff: 2026-09-26  
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
- client tự dò chuỗi khu, báo `ZONE / FOUND / DEAD / READY / FAILED` về Manager;
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
- thông báo VIP mới được hook trực tiếp từ `GClass144.method_121()` vào queue riêng của `BossZoneScanner`, rồi xử lý trên game loop; không còn phụ thuộc index của queue UI bị xóa đầu.

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

V1 hiện quét các khu trên **map mà từng account đang đứng lúc bắt đầu**. Khi FOUND, map thật của finder mới là source of truth để rally. Không suy đoán map spawn chỉ từ tên boss.

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

- Boss Hunt đã harden timeout/FAILED/death-event nhưng chưa test runtime nhiều account; vẫn cần xác nhận zone dwell, zone full, route map, target reload và chuỗi thông báo boss chết thực tế.
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
