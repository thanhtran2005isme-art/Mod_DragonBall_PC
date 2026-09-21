# AGENTS.md — Quy trình bàn giao cho AI/agent

File này là **điểm vào bắt buộc** cho mọi phiên AI/agent làm việc với repo `thanhtran2005isme-art/Mod_DragonBall_PC`.

Mục tiêu: phiên mới hiểu đúng trạng thái hiện tại mà không phải đọc lại toàn bộ lịch sử dự án, đồng thời không sửa code dựa trên trí nhớ hoặc suy đoán.

## 1. Thứ tự phải đọc trước khi sửa code

1. `AGENTS.md` — file này.
2. `docs/AI_HANDOFF.md` — trạng thái hiện tại, phần đang nóng, việc còn mở.
3. `docs/ARCHITECTURE.md` — cấu trúc repo và luồng chính.
4. `docs/DECISIONS.md` — các quyết định kỹ thuật đã chốt và lý do.
5. `docs/TROUBLESHOOTING.md` — lỗi đã gặp và cách chẩn đoán.
6. File mới nhất trong `docs/history/`.
7. Git history / PR / commit gần đây **liên quan trực tiếp** đến task sắp làm.
8. Các source file hiện tại liên quan đến task.

Không cần đọc toàn bộ Git history mỗi lần. Chỉ đào sâu lịch sử ở phần liên quan.

## 2. Source of truth

Ưu tiên theo thứ tự:

1. Code hiện tại trên `main`.
2. Git history / commit / PR giải thích code đó thay đổi thế nào.
3. Tài liệu trong `docs/`.
4. `docs/AI_HANDOFF.md` chỉ là bản tóm tắt để vào việc nhanh.

Nếu tài liệu mâu thuẫn với code hiện tại, **code thắng**. Sau khi xác minh, cập nhật lại tài liệu trong cùng task.

Không đoán hành vi chỉ từ trí nhớ của phiên chat trước.

## 3. Trước khi thay đổi bất kỳ code nào

Hãy đọc context theo thứ tự ở mục 1, sau đó tóm tắt ngắn cho người dùng:

- cấu trúc repo hiện tại;
- cách build/chạy phần liên quan;
- quyết định kỹ thuật quan trọng ảnh hưởng task;
- lỗi/rủi ro/việc còn tồn tại;
- commit/PR gần nhất có liên quan;
- các file dự kiến phải sửa.

**Chưa sửa code trước khi hoàn thành bước tóm tắt context**, trừ khi người dùng nói rõ muốn bỏ qua bước này.

## 4. Quy tắc build/test quan trọng

- Gameplay chính nằm trong `GameAssembly/`.
- Nếu chỉ sửa gameplay, ưu tiên build riêng:
  `GameAssembly/GameAssembly.csproj`.
- `GameAssembly` target .NET Framework 3.5: tránh API mới không có trên net35.
- Trước khi build nếu DLL bị lock, tắt game/manager theo hướng dẫn trong `README.md` và `docs/TROUBLESHOOTING.md`.
- Với combat, không kết luận chỉ từ animation; phải kiểm tra damage/HP/phản hồi server.
- Sau push, kiểm tra GitHub Actions khi thay đổi có thể ảnh hưởng build.

## 5. Quy tắc sửa tài liệu sau mỗi task

Chỉ cập nhật file phù hợp, không nhồi tất cả vào một file:

- `docs/AI_HANDOFF.md`: trạng thái hiện tại, phần đang làm, việc chưa xong, commit gần nhất đáng chú ý.
- `docs/ARCHITECTURE.md`: khi cấu trúc project, module hoặc luồng lớn thay đổi.
- `docs/DECISIONS.md`: khi có quyết định kỹ thuật mới hoặc thay đổi quyết định cũ.
- `docs/TROUBLESHOOTING.md`: khi gặp lỗi lặp lại và đã biết cách chẩn đoán/fix.
- `docs/history/YYYY-MM.md`: chuyển chi tiết cũ khỏi AI_HANDOFF để giữ handoff ngắn.
- `CHANGELOG.md`: ghi thay đổi đáng chú ý theo thời gian.
- `README.md`: chỉ cập nhật thông tin ổn định/quick start.

## 6. Giới hạn kích thước AI_HANDOFF

Giữ `docs/AI_HANDOFF.md` ở mức vài trăm dòng trở xuống.

Khi chi tiết không còn cần cho công việc hiện tại:

1. chuyển sang `docs/history/YYYY-MM.md`;
2. giữ lại trong AI_HANDOFF một dòng tóm tắt + đường dẫn nếu còn hữu ích.

## 7. Các tài liệu kỹ thuật sâu hiện có

- `docs/PROJECT_CONTEXT.md`: context kỹ thuật chi tiết/legacy đã tích lũy.
- `docs/NETWORKING.md`: network diagnostics, Combat RTT, adaptive combat, SV15.
- `CHANGELOG.md`: timeline thay đổi đáng chú ý.

Các file mới không thay thế Git history; chúng giúp AI biết **đọc gì trước**.
