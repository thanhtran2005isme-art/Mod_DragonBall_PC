# Project Context — Mod Dragon Ball PC

> Đây là tài liệu kỹ thuật chi tiết.  
> README ở root là bản ngắn dùng để bắt đầu nhanh.  
> File này giữ thông tin dài để README không phình ra theo thời gian.

> **Tài liệu bàn giao / Project handoff**
>
> README này là **nguồn thông tin đầu tiên phải đọc** trước khi sửa project.  
> Mục tiêu: phiên làm việc sau chỉ cần đọc README là biết **repo ở đâu trên máy, build thế nào, output ở đâu, class nào phụ trách chức năng nào, commit/push ra sao và các lỗi/logic quan trọng đã xử lý**.
>
> **Cập nhật gần nhất:** 2026-09-20  
> **Branch chính:** `main`  
> **Repo:** `thanhtran2005isme-art/Mod_DragonBall_PC`

---

## 1. QUICK START — ĐỌC PHẦN NÀY TRƯỚC

### Repo trên máy Windows

**Đúng:**

```text
C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1
```

**Không dùng folder cũ này:**

```text
C:\Users\Admin\Pictures\ModThanhLC
```

### Pull code mới nhất

```bat
cd /d C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1
git pull origin main
git log -5 --oneline
```

### Build nhanh khi chỉ sửa gameplay / GameAssembly

Đây là cách **ưu tiên**.

```bat
cd /d C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1

"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" GameAssembly\GameAssembly.csproj /t:rebuild /p:Configuration=Release
```

DLL sau build:

```text
Output\Dragon ball_237b_Data\Managed\Assembly-CSharp.dll
```

Game:

```text
Output\Dragon ball_237b.exe
```

Mở thư mục Output:

```bat
start "" "C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1\Output"
```

Chạy game:

```bat
start "" "C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1\Output\Dragon ball_237b.exe"
```

Nếu DLL bị lock:

```bat
taskkill /F /IM "Dragon ball_237b.exe"
taskkill /F /IM DragonBoyManager.exe
```

---

## 2. Cấu trúc project

Solution:

```text
ModThanhLC.sln
```

Các project chính:

| Project / folder | Vai trò |
|---|---|
| `GameAssembly/` | Source gameplay chính, build thành `Assembly-CSharp.dll` |
| `AccountManager/` | DragonBoyManager, quản lý tài khoản / launcher |
| `LicenseCheckBypass/` | DLL bypass/hook liên quan license |
| `LicenseCheckBypassInjector/` | Injector cho bypass |
| `ProductLicense/` | Logic/license project |
| `Lib/` | DLL dependency |
| `Output/` | Game/runtime/output cuối cùng |
| `.github/workflows/build-and-release.yml` | GitHub Actions build + artifact + prerelease |

Target framework quan trọng:

- `GameAssembly`: **.NET Framework 3.5 / net35**
- `AccountManager`: **.NET Framework 4.8**
- `LicenseCheckBypass`: **.NET Framework 4.8**

> Khi sửa `GameAssembly`, tránh dùng API mới không có trong .NET 3.5.  
> Ví dụ trước đây đã phải tránh overload `Path.Combine` 3 tham số.

---

## 3. Các file/runtime quan trọng trong Output

```text
Output\
├─ Dragon ball_237b.exe
├─ Dragon ball_237b_Data\
│  └─ Managed\
│     └─ Assembly-CSharp.dll
├─ Data\
│  ├─ kaitokid.txt
│  ├─ logo.png
│  ├─ wallpaper.png
│  ├─ Errors\
│  └─ QLTK\
└─ Files\
```

### Logo KaitoKid

File quan trọng:

```text
Output\Data\kaitokid.txt
```

Đây là **Base64 ảnh KaitoKid** và hiện được dùng cho:

1. Logo KaitoKid trên HUD.
2. Logo lớn màn hình startup/login.

Source xử lý:

```text
GameAssembly/AssemblyCSharp.Functions/GClass167.cs
```

- `method_4("logoGameScr", ...)` đọc `Data\kaitokid.txt` cho HUD.
- `method_5("imgTitle")` đọc cùng file cho splash/login.
- Không fallback về logo Thanh VLC cũ nếu custom logo lỗi.

Domain startup:

```text
GameAssembly/GClass73.cs
```

Hiện hiển thị:

```text
[kaitokid.com]
```

Logo/panel HUD cũ có Thanh VLC đã được bỏ bằng cách dùng asset panel gốc:

```csharp
gclass70_28 = GClass73.smethod_43("/mainImage/myTexture2dpanel.png");
```

trong:

```text
GameAssembly/GClass144.cs
```

---

## 4. Tự động đánh vs Auto Train / Đồ sát quái

**Hai chức năng này khác nhau. Không nhầm class.**

### 4.1 Tự động đánh / Auto Attack

Source:

```text
GameAssembly/AssemblyCSharp.Functions/GClass164.cs
```

Bật/tắt bằng phím:

```text
A
```

Biến chính:

```csharp
bool_0
```

Luồng update:

```text
GClass171.method_4()
  -> GClass164.method_2()
       -> method_12()   // Auto Skill
       -> method_15()   // Auto Attack nếu bool_0 = true
```

Ý nghĩa:

- Auto Attack **không phải bot tự tìm quái hoàn chỉnh**.
- Nó chủ yếu tự đánh mục tiêu/focus hiện tại.
- `method_15()` là nơi gửi đòn thực tế.

Packet đánh:

```text
GameAssembly/GClass7.cs
GClass7.method_73(...)
```

### 4.2 Auto Train / Đồ sát quái

Source:

```text
GameAssembly/AssemblyCSharp.Functions/GClass166.cs
```

Bật/tắt bằng lệnh/menu Đồ sát quái, ví dụ:

```text
/dsq
```

Luồng chính:

```text
GClass166.method_12()
  -> kiểm tra trạng thái
  -> method_16() chọn quái
  -> method_21() chọn skill
  -> di chuyển nếu cần
  -> dùng GClass164.method_15() / hệ thống attack để đánh
```

Auto Train / Đồ sát quái có thêm:

- tự chọn quái;
- tự đổi mục tiêu;
- tự di chuyển;
- danh sách quái;
- danh sách skill;
- nhặt item;
- các chế độ đánh quái.

---

## 5. Logic đổi skill + attack hiện tại — CỰC KỲ QUAN TRỌNG

### Vì sao không được để delay = 0 hoàn toàn?

Protocol hiện tại:

**Đổi skill:**

```text
GClass7.method_56(skillTemplateId)
packet 34 = SELECT SKILL
```

**Đánh:**

```text
GClass7.method_73(...)
ATTACK MOB / ATTACK CHAR
```

Packet attack **không mang ID skill trực tiếp**. Server dựa vào trạng thái skill đã được chọn trước đó.

Nếu gửi:

```text
SELECT SKILL A
ATTACK A
SELECT SKILL B
ATTACK B
```

quá sát nhau, client có thể hiện **2 animation** nhưng server chỉ tính **1 damage**.

Đã từng gặp đúng case:

- 2 skill đấm;
- cooldown khoảng `0.4s`;
- nhìn thấy 2 cú;
- HP quái chỉ trừ damage 1 cú.

### Quy tắc hiện tại

**Micro-delay 100 ms sau SELECT SKILL trước ATTACK.**

Không quay lại delay cũ 550 ms.

Không dùng lại:

```text
cooldown × 1.2
minimum 415 ms
```

Cooldown cơ bản vẫn lấy theo:

```csharp
skill.int_1
```

Logic mục tiêu:

```text
A đánh
  ↓
B đã hồi
  ↓
SELECT SKILL B
  ↓
khóa không cho đổi skill tiếp
  ↓
đợi tối thiểu ~100 ms
  ↓
ATTACK B
  ↓
mở khóa
  ↓
xét skill tiếp theo
```

### Biến khóa quan trọng

Trong `GClass164`:

```csharp
long_10
```

`method_62()` trong `GClass144` đặt:

```csharp
GClass164.smethod_0().long_10 = GClass203.smethod_18();
```

`GClass164.method_15()` hiện kiểm tra khoảng 100 ms trước khi cho gửi hit sau đổi skill.

Sau khi packet attack được gửi thành công, `smethod_5()` / `smethod_6()` reset:

```csharp
long_10 = -1L;
```

**Không reset `long_10` ngay sau SELECT SKILL**, nếu không lỗi “2 animation nhưng 1 damage” có thể quay lại.

---

## 6. Logic skill của Đồ sát quái hiện tại

Trong:

```text
GameAssembly/AssemblyCSharp.Functions/GClass166.cs
```

### `method_21()`

Dùng để chọn skill.

Logic hiện tại:

- skill đang cầm = current skill;
- nếu vừa SELECT SKILL nhưng chưa attack xong thì giữ nguyên skill đó;
- nếu có skill khác đã cooldown xong thì có thể chuyển sang skill đó;
- nếu skill khác chưa hồi thì tiếp tục skill hiện tại.

Mục tiêu:

```text
A -> B nếu B sẵn sàng -> A
```

Nếu B chưa hồi:

```text
A -> A
```

### `method_23()`

Xác định skill có sẵn sàng hay không:

- skill phải khác null;
- cooldown thật đã hết;
- không còn `bool_0` cooldown;
- đủ KI;
- không thuộc nhóm skill bị loại.

---

## 7. Build project

### 7.1 Build riêng GameAssembly — khuyên dùng khi sửa gameplay

```bat
cd /d C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1

"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" GameAssembly\GameAssembly.csproj /t:rebuild /p:Configuration=Release
```

PostBuild của `GameAssembly.csproj` tự copy DLL tới:

```text
Output\Dragon ball_237b_Data\Managed\Assembly-CSharp.dll
```

Kiểm tra:

```bat
dir "Output\Dragon ball_237b_Data\Managed\Assembly-CSharp.dll"
```

### 7.2 Build toàn solution

Có thể dùng:

```bat
cd /d C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1

nuget.exe restore ModThanhLC.sln

"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" ModThanhLC.sln /t:rebuild /p:Configuration=Release
```

Hoặc restore MSBuild trước:

```bat
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" ModThanhLC.sln /t:restore /p:Configuration=Release
nuget.exe restore ModThanhLC.sln
```

### Lỗi local từng gặp khi build full solution

Full solution trên máy local từng build phần code xong nhưng fail ở **PostBuild copy** của:

- `AccountManager`
- `LicenseCheckBypass`

Các PostBuild này copy DLL/file vào `Output` / `Output\Lib`.

GitHub Actions vẫn có thể build thành công.

Vì vậy:

> Nếu chỉ sửa `GameAssembly`, **không cần build cả solution**. Build riêng `GameAssembly.csproj` để nhanh và tránh lỗi môi trường/PostBuild không liên quan.

---

## 8. GitHub Actions / CI

Workflow:

```text
.github/workflows/build-and-release.yml
```

Workflow chạy khi:

- push;
- `workflow_dispatch`.

Các bước chính:

```text
checkout
-> setup MSBuild
-> setup NuGet
-> msbuild restore
-> nuget restore
-> msbuild rebuild Release
-> upload Output artifact
-> zip Output
-> tạo prerelease
```

Artifact:

```text
Output
```

Tên zip:

```text
DragonBall.Pro.2.3.7.<branch>.zip
```

Nếu local full solution lỗi nhưng GitHub Actions success thì cần phân biệt **lỗi môi trường local/PostBuild** với **lỗi source code**.

---

## 9. Quy trình Git — pull / sửa / commit / push

### Trước khi sửa

```bat
cd /d C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1

git status
git pull origin main
git log -5 --oneline
```

### Xem file đã đổi

```bat
git status
git diff
```

### Stage đúng file cần commit

**Ưu tiên add từng file**, không `git add .` mù.

Ví dụ:

```bat
git add GameAssembly\AssemblyCSharp.Functions\GClass164.cs
git add GameAssembly\AssemblyCSharp.Functions\GClass166.cs
```

Kiểm tra:

```bat
git diff --cached
```

### Commit

Một commit nên chứa **một thay đổi logic hoàn chỉnh**.

Ví dụ tốt:

```bat
git commit -m "Sửa chuyển skill khi auto train quái"
```

```bat
git commit -m "Bỏ delay nhân tạo của Tự động đánh"
```

```bat
git commit -m "Đồng bộ 100ms đổi skill cho Auto Attack và Auto Train"
```

Nếu 2 file cùng phục vụ **một behavior** thì nên commit chung.

Ví dụ `GClass164` + `GClass166` cùng sửa logic 100 ms:

```bat
git add GameAssembly\AssemblyCSharp.Functions\GClass164.cs GameAssembly\AssemblyCSharp.Functions\GClass166.cs
git commit -m "Đồng bộ 100ms đổi skill cho Auto Attack và Auto Train"
```

### Push

```bat
git push origin main
```

Sau đó:

```bat
git log -3 --oneline
```

và kiểm tra GitHub Actions.

### Quy ước commit

Nên dùng tiếng Việt, ngắn và nói đúng behavior:

```text
Sửa ...
Bỏ ...
Thêm ...
Đổi ...
Fix ...
Đồng bộ ...
```

Không dùng message mơ hồ như:

```text
update
fix
test
abc
```

---

## 10. Commit quan trọng gần đây

### Auto Attack / Auto Train

```text
5005d68 Đồng bộ 100ms đổi skill cho Auto Attack và Auto Train
3c726f9 Bỏ delay nhân tạo của Tự động đánh
9ec1690 Luân phiên skill auto train và bỏ delay khi đổi skill
6bc123e Sửa chuyển skill khi auto train quái
```

### Logo / branding

```text
74a6c01 Đổi domain splash thành kaitokid.com
51ef5e8 Dùng kaitokid.txt cho logo màn hình login
a71cf1e Bỏ logo Thanh VLC khỏi panel HUD
f75f3da Fix runtime logo KaitoKid và bỏ fallback Thanh VLC
5f423e0 Fix đường dẫn logo KaitoKid cho .NET 3.5
ea5748a Buộc HUD dùng logo KaitoKid từ logoGameScr
eb418de Đọc logo KaitoKid theo thư mục game
a203980 Thêm Base64 logo KaitoKid mới
4845f36 Dùng Base64 KaitoKid từ Data/kaitokid.txt cho logo client
089eb05 Đổi domain hiển thị panel thành kaitokid.com
965b99c Đổi tên hiển thị panel thành KaitoKid
```

---

## 11. Các class/file cần nhớ

| File | Chức năng |
|---|---|
| `GameAssembly/AssemblyCSharp.Functions/GClass164.cs` | Tự động đánh, auto skill, cooldown, gửi attack |
| `GameAssembly/AssemblyCSharp.Functions/GClass166.cs` | Đồ sát quái / auto train quái, chọn mob, chọn skill, di chuyển |
| `GameAssembly/AssemblyCSharp.Functions/GClass167.cs` | Custom image/Base64, logo HUD/login |
| `GameAssembly/AssemblyCSharp.Functions/GClass171.cs` | Update dispatcher của nhiều module, render logo HUD |
| `GameAssembly/GClass7.cs` | Packet/network; `method_56` select skill, `method_73` attack |
| `GameAssembly/GClass73.cs` | Screen/render; splash/login text `[kaitokid.com]`, error overlay |
| `GameAssembly/GClass144.cs` | Game screen/HUD, select/use skill, panel assets |
| `GameAssembly/mResources.cs` | Language resources và gán `imgTitle` |
| `GameAssembly/AssemblyCSharp.Functions/GClass151.cs` | Client version/name/date |
| `AccountManager/DragonBoyManager/CheckInfo.cs` | License/info logic của manager |

---

## 12. Logs cần kiểm tra khi lỗi

Khi game chạy từ `Output`, log thường nằm trong:

```text
Output\Data\Errors\
```

Các log đã gặp / source có ghi:

```text
logo_error.log
frame_render.log
SendAttack.txt
startMurderingMob.txt
panel_exit.log
```

Nếu lỗi Auto Attack:

```text
Output\Data\Errors\SendAttack.txt
```

Nếu lỗi Đồ sát / Auto Train:

```text
Output\Data\Errors\startMurderingMob.txt
```

Nếu lỗi logo:

```text
Output\Data\Errors\logo_error.log
```

Nếu màn hình render lỗi:

```text
Output\Data\Errors\frame_render.log
```

---

## 13. Kiểm tra game/process khi test

Xem process:

```bat
powershell -NoProfile -Command "Get-Process | Where-Object {$_.ProcessName -like '*Dragon*'} | Select-Object ProcessName,Path"
```

Tắt game trước khi build nếu DLL bị khóa:

```bat
taskkill /F /IM "Dragon ball_237b.exe"
```

Tắt manager nếu cần:

```bat
taskkill /F /IM DragonBoyManager.exe
```

Sau build chạy lại:

```bat
start "" "C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1\Output\Dragon ball_237b.exe"
```

---

## 14. Checklist trước khi kết luận một bản sửa đã ổn

1. Pull đúng branch `main`.
2. Kiểm tra commit mới nhất bằng `git log -1 --oneline`.
3. Build `GameAssembly.csproj`.
4. Xác nhận `Assembly-CSharp.dll` trong `Output\Dragon ball_237b_Data\Managed` đã đổi thời gian.
5. Tắt hẳn game cũ rồi mở lại.
6. Test đúng chức năng vừa sửa.
7. Nếu có lỗi, đọc `Output\Data\Errors`.
8. Kiểm tra GitHub Actions của commit.
9. Không kết luận chỉ từ animation; với combat phải kiểm tra **damage/HP thực tế phía server**.

---

## 15. Ghi chú đặc biệt cho AI / phiên làm việc sau

Nếu đang hỗ trợ sửa project này:

1. **Đọc README này trước khi hỏi user lại thông tin cũ.**
2. Repo local mặc định:
   `C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1`.
3. Gameplay chủ yếu sửa trong `GameAssembly`.
4. Build gameplay bằng `GameAssembly\GameAssembly.csproj`, không tự động build full solution nếu không cần.
5. Phân biệt:
   - `GClass164` = **Tự động đánh / Auto Attack**
   - `GClass166` = **Đồ sát quái / Auto Train quái**
6. Không bỏ micro-delay đổi skill xuống 0 mà không test damage server.
7. Hiện behavior mong muốn là:
   - cooldown thật của skill;
   - sau SELECT SKILL chờ khoảng **100 ms**;
   - sau đó ATTACK;
   - không đổi skill tiếp trong lúc chờ hit;
   - hit xong mới mở khóa.
8. Logo KaitoKid lấy từ `Output\Data\kaitokid.txt`.
9. Nếu thay đổi đường dẫn local, build command, output, class mapping hoặc combat behavior quan trọng thì **cập nhật README trong cùng phiên làm việc**.
10. Khi user nói “commit”, ưu tiên:
    - một behavior = một commit;
    - message mô tả rõ thay đổi;
    - kiểm tra GitHub Actions sau push.

---

## 16. GitHub Actions hiện tại

Workflow build/release:

```text
.github/workflows/build-and-release.yml
```

GitHub Actions là kiểm tra build toàn solution đáng tin cậy sau khi push.  
Local build riêng `GameAssembly` là vòng test nhanh cho gameplay.

---

## 17. Nguyên tắc cập nhật README

README này phải được cập nhật khi có một trong các thay đổi sau:

- đổi thư mục repo trên máy;
- đổi phiên bản Visual Studio/MSBuild;
- đổi lệnh build;
- đổi tên exe/DLL/output;
- thêm/bỏ project;
- đổi class phụ trách chức năng chính;
- đổi protocol/timing Auto Attack/Auto Train;
- đổi vị trí logo/config/runtime data;
- phát hiện lỗi build local mới có tính lặp lại.

Mục tiêu là **không phải tìm lại từ đầu ở phiên làm việc sau**.


---

## Quy tắc tài liệu mới

Từ 2026-09-20:

- `README.md` chỉ chứa thông tin ổn định và quick start.
- `CHANGELOG.md` chứa lịch sử thay đổi.
- Chi tiết kỹ thuật dài được giữ trong `docs/`.
- Không thêm mọi commit mới vào README.
