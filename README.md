# Mod Dragon Ball PC — KaitoKid

> **README ngắn / điểm vào của dự án.**  
> Chỉ giữ thông tin ổn định để lần sau đọc nhanh là làm được ngay.  
> Lịch sử thay đổi xem `CHANGELOG.md`. Chi tiết kỹ thuật xem `docs/PROJECT_CONTEXT.md`.

## Quick start

Repo local đúng:

```text
C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1
```

Không dùng folder cũ:

```text
C:\Users\Admin\Pictures\ModThanhLC
```

Pull:

```bat
cd /d C:\Users\Admin\Downloads\ModThanhLC-2.0-patch1
git pull origin main
git log -5 --oneline
```

Build gameplay / GameAssembly:

```bat
taskkill /F /IM "Dragon ball_237b.exe" 2>nul

"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" GameAssembly\GameAssembly.csproj /t:rebuild /p:Configuration=Release
```

Output chính:

```text
Game: Output\Dragon ball_237b.exe
DLL : Output\Dragon ball_237b_Data\Managed\Assembly-CSharp.dll
```

Nếu DLL bị lock:

```bat
taskkill /F /IM "Dragon ball_237b.exe"
taskkill /F /IM DragonBoyManager.exe
```

## File/class quan trọng

| File | Vai trò |
|---|---|
| `GameAssembly/AssemblyCSharp.Functions/GClass164.cs` | Tự động đánh / Auto Attack |
| `GameAssembly/AssemblyCSharp.Functions/GClass166.cs` | Auto Train / Đồ sát quái |
| `GameAssembly/GClass7.cs` | Packet/network; select skill, attack |
| `GameAssembly/GClass14.cs` | Session TCP chính |
| `GameAssembly/GClass85.cs` | Session TCP phụ |
| `GameAssembly/GClass134.cs` | Danh sách server / chọn server |
| `GameAssembly/AssemblyCSharp.Functions/GClass167.cs` | Logo/Base64 |
| `GameAssembly/GClass73.cs` | Render/screen/startup |
| `GameAssembly/GClass144.cs` | Game screen/HUD/skill |
| `Output/Data/kaitokid.txt` | Base64 logo KaitoKid |

## Quy tắc combat hiện tại

Auto Attack và Auto Train là **hai chức năng khác nhau**:

```text
GClass164 = Tự động đánh
GClass166 = Auto Train / Đồ sát quái
```

Sau khi đổi skill phải giữ micro-delay khoảng **100 ms** trước packet ATTACK để tránh lỗi:

```text
thấy 2 animation nhưng server chỉ tính 1 damage
```

Không tự ý reset `GClass164.long_10` ngay sau SELECT SKILL.

Cooldown đánh hiện ưu tiên cooldown thật của skill, không quay lại delay cũ 550 ms nếu chưa có lý do/test rõ ràng.

## Git / commit

Trước khi sửa:

```bat
git status
git pull origin main
```

Sau khi sửa:

```bat
git diff
git add <đúng file đã sửa>
git diff --cached
git commit -m "Mô tả rõ thay đổi"
git push origin main
```

Quy ước:

- 1 behavior hoàn chỉnh = 1 commit.
- Không dùng commit message kiểu `update`, `test`, `abc`.
- Kiểm tra GitHub Actions sau push.
- Nếu chỉ sửa `GameAssembly`, ưu tiên build riêng `GameAssembly.csproj`.

## Log

Runtime log thường ở:

```text
Output\Data\Errors\
```

Hay dùng:

```text
SendAttack.txt
startMurderingMob.txt
logo_error.log
frame_render.log
```

## Tài liệu chi tiết

- `docs/PROJECT_CONTEXT.md` — kiến trúc, build, combat, logo, lỗi đã biết, ghi chú kỹ thuật.
- `CHANGELOG.md` — lịch sử thay đổi ngắn gọn theo commit.

## Quy tắc cập nhật tài liệu

**Không cập nhật README cho mọi commit.**

Chỉ sửa README khi có thay đổi ổn định như:

- đổi thư mục repo local;
- đổi lệnh/path build;
- đổi output chính;
- đổi class phụ trách chức năng lớn;
- đổi quy tắc nền tảng mà phiên sau bắt buộc phải biết.

Thay đổi tính năng thông thường ghi vào `CHANGELOG.md` hoặc tài liệu liên quan trong `docs/`.
