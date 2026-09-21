# Architecture — Mod Dragon Ball PC

Tài liệu này mô tả cấu trúc ổn định của repo. Trạng thái task hiện tại nằm ở `docs/AI_HANDOFF.md`.

## 1. Solution-level architecture

```text
ModThanhLC.sln
├─ GameAssembly/
├─ AccountManager/
├─ LicenseCheckBypass/
├─ LicenseCheckBypassInjector/
└─ ProductLicense/
```

### GameAssembly

Vai trò: gameplay chính, UI game, packet/session, automation.

Target quan trọng: **.NET Framework 3.5**.

Build output:

```text
Output\Dragon ball_237b_Data\Managed\Assembly-CSharp.dll
```

### AccountManager

Vai trò: DragonBoyManager / quản lý tài khoản / launcher hỗ trợ.

Target được tài liệu hiện tại ghi nhận: .NET Framework 4.8.

### LicenseCheckBypass / LicenseCheckBypassInjector / ProductLicense

Các project phụ liên quan license/bypass/injector. Không sửa khi task chỉ liên quan gameplay nếu chưa đọc đúng source/history.

### Output

Runtime tree chính:

```text
Output/
├─ Dragon ball_237b.exe
├─ Dragon ball_237b_Data/
│  └─ Managed/
│     └─ Assembly-CSharp.dll
├─ Data/
│  ├─ kaitokid.txt
│  └─ Errors/
└─ Files/
```

## 2. Module map quan trọng trong GameAssembly

| File/module | Vai trò |
|---|---|
| `AssemblyCSharp.Functions/GClass164.cs` | Auto Attack, auto skill, cooldown, gửi hit |
| `AssemblyCSharp.Functions/GClass166.cs` | Auto Train / Đồ sát quái |
| `AssemblyCSharp.Functions/GClass167.cs` | custom image/Base64/logo |
| `AssemblyCSharp.Functions/GClass171.cs` | update dispatcher/render module |
| `AssemblyCSharp.Functions/KOLTracker.cs` | học/sync/hiển thị tiến độ KOL |
| `GClass7.cs` | packet gửi, select skill, attack, combat diagnostics |
| `GClass12.cs` | xử lý nhiều packet/response từ server |
| `GClass14.cs` | session TCP chính |
| `GClass85.cs` | session TCP phụ |
| `GClass134.cs` | danh sách/chọn server |
| `GClass73.cs` | startup/render/screen |
| `GClass144.cs` | game screen/HUD/skill |
| `mResources.cs` | resource/language |

## 3. Luồng combat khái quát

```text
Auto module
  -> chọn skill
  -> GClass7.method_56(skillTemplateId)   [SELECT SKILL]
  -> chờ micro-delay cần thiết
  -> GClass7.method_73(...)               [ATTACK]
  -> server response
  -> GClass12 / handlers cập nhật HP/death/miss
  -> combat diagnostics/adaptive scheduler cập nhật
```

Điểm quan trọng: packet ATTACK không mang trực tiếp skill ID theo context hiện tại; server phụ thuộc state skill đã select trước đó. Vì vậy timing giữa SELECT SKILL và ATTACK là một phần của protocol thực tế.

## 4. Luồng network khái quát

```text
GClass14 / GClass85
  -> TCP session
  -> send/receive queues
  -> GClass7 gửi packet
  -> controller/handlers xử lý response
```

Diagnostic/adaptive logic đã được bổ sung quanh:

- ping;
- Combat RTT;
- pending attack probes;
- ACK rate;
- adaptive window;
- pacing.

Chi tiết: `docs/NETWORKING.md`.

## 5. Luồng KOL hiện tại

```text
người dùng tương tác NPC/menu
  -> KOLTracker học packet/menu path
  -> background sync replay path đã học
  -> parse progress server
  -> local mirror chỉ cộng khi có bằng chứng last-hit đủ mạnh
  -> server sync tiếp tục là mốc đối chiếu
```

KOL tracker liên quan ít nhất:

- `KOLTracker.cs`;
- `GClass7.cs`;
- `GClass12.cs`.

Khi sửa KOL, bắt buộc đọc commit gần đây vì logic phụ thuộc packet order và các cửa sổ thời gian.

## 6. Build architecture

Gameplay-only loop:

```text
edit GameAssembly
-> rebuild GameAssembly.csproj Release
-> PostBuild copy Assembly-CSharp.dll vào Output
-> chạy game
-> kiểm tra runtime logs/server behavior
```

Full solution loop dùng khi thay đổi nhiều project hoặc cần xác nhận toàn bộ integration.

CI workflow:

```text
.github/workflows/build-and-release.yml
```

## 7. Documentation architecture

```text
AGENTS.md
  -> docs/AI_HANDOFF.md
     -> docs/ARCHITECTURE.md
     -> docs/DECISIONS.md
     -> docs/TROUBLESHOOTING.md
     -> docs/history/YYYY-MM.md

docs/PROJECT_CONTEXT.md   deep/legacy technical context
docs/NETWORKING.md        networking deep dive
CHANGELOG.md              project timeline
README.md                 stable quick start
```

Không dùng AI_HANDOFF làm nơi chứa toàn bộ lịch sử.
