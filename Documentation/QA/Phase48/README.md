# Phase 4.8 — báo cáo và test LAN

## Git và phạm vi

Base `main`: `8ce72cc`. Các nhánh xếp nối tiếp nhau theo thứ tự dưới, tất cả là hậu duệ của main; không merge/push main.

| Nhóm | Nhánh | Commit source | Console / checklist |
|---|---|---|---|
| 1 | fix/scene-leftovers | 12e3bc3 | [Console](01-console.txt), [mục 14](01-checklist.md) |
| 2 | feature/rect-rooms | bd506e8 | [Console](02-console.txt), [mục 14](02-checklist.md) |
| 3 | feature/room-themes | 2ccd63d | [Console](03-console.txt), [mục 14](03-checklist.md) |
| 4 | feature/party-teleport-lock | e8b8a05 | [Console](04-console.txt), [mục 14](04-checklist.md) |
| 5 | feature/floor-scaling | b486970 | [Boss / biên dịch cuối](05-console.txt), [Combat / công thức / tất định](05-combat-and-formula-console.txt), [mục 14](05-checklist.md) |

Đã triển khai năm nhóm. Chưa nghiệm thu đầy đủ LAN hoặc nghệ thuật. Không triển khai mục 21, không đổi tầng, loại/chỉ số quái, shop/Linh hồn, luật mở cổng/thưởng/Victory sau Boss. Không chạy HellfirePhase*Setup, không thêm InitializeOnLoad.

Smoke test chạy trên Unity 6000.5.6f1, workspace đang có sửa đổi chưa commit và art/source chưa tracked của chủ dự án. Các sửa đổi đó không được đưa vào commit của lượt này. Kết quả này không chứng minh một clean checkout đã được build/test.

## Bằng chứng đã có

- Từng nhóm mở Unity và chạy Host qua MainMenu → Lobby → Gameplay. Console runtime cuối nhóm 1–5 đều không có lỗi đỏ. Lần biên dịch cuối: 20 warnings, 0 errors; Play Mode đã dừng.
- Trước khóa phòng: EnemyStateMachine=0; quét YAML ba scene không thấy quái/vật cản/rương/tế đàn/shop đặt sẵn.
- Scene được đọc lại: grid=36; base=5..7; floorStep=2; cap=14; chờ teleport=0.5s và chờ spawn=0.8s.
- HUD hiện “Tầng 1”; Combat roll=6, một người ×0.7 → 4 quái. Công thức tầng 2/3 được kiểm tra bằng hàm tính, không ghi CurrentFloor để giả lập chuyển tầng.
- Sáu seed × hai lần dựng trong preview scene: room type, size, theme và obstacle bounds khớp nhau. Đây là kiểm tra cục bộ, chưa thay thế so sánh nhiều máy.
- Boss khóa phòng, vào BossFight, spawn với 480 HP cho một người; không đổi cơ chế sau khi hạ Boss. Chưa kiểm thử lại đủ toàn bộ ba pha/triệu hồi và hạ Boss qua LAN.

## Những điểm chưa đạt nghiệm thu

- Chưa có 2–4 máy: kéo đồng đội từ xa, Downed/Dead, đồng bộ owner transform, revive và trạng thái cổng vẫn cần test LAN.
- Bố trí giữ các giới hạn không chắn cửa, khoảng cách và diện tích; sau 30 lần thử có thể bỏ cụm đúng 20.3. Mẫu nhiều seed có phòng chỉ đặt được 1 cụm, nên chưa đạt tiêu chí 2–5 cụm/phòng của 20.7 trong mọi trường hợp. Không giảm khoảng cách hoặc tăng diện tích để ép đủ cụm.
- Khi mọi vị trí spawn đều bị người chơi/vật cản chiếm, spawn chờ thử lại; cờ spawn xong chưa được đặt, tránh Cleared sớm. Khi test, di chuyển đội hình để kiểm tra trường hợp này không gây khóa kéo dài bất thường.
- Quan sát thấy material hồng trên Boss/rương; cảnh dungeon còn sáng/skybox. Còn cảnh báo font TMP thiếu ký tự và API cũ. Chưa tự phê duyệt art direction.
- Checklist ghi riêng Đạt và Không đạt nghiệm thu; không coi biên dịch sạch là toàn bộ mục 14 đạt.

## Test 2–4 máy theo 20.7

1. Tạo cùng một bản build từ `feature/floor-scaling` cho mọi máy, cùng dữ liệu/asset và phiên bản. Nếu muốn kèm sửa đổi chưa commit của chủ dự án, thống nhất chúng trước khi build. Một máy Host, 1–3 máy Join IP LAN. Vào từ MainMenu, Ready và bắt đầu ở Lobby; không mở Gameplay rồi bấm Play độc lập.
2. Sau loading, đứng tại Spawn, chưa vào phòng Combat. Host Console phải có `[SceneAudit] ... EnemyStateMachine=0`. Quan sát các hành lang: không có quái, vật cản, rương/tế đàn/shop. Log `[DungeonCorridor]` phải có mọi chiều dài >=10m.
3. So sánh cùng tọa độ phòng trên các máy: `[DungeonRoom]` phải có Size giống nhau; `[RoomTheme]` cùng theme/cụm/vật thể. Đi qua nhiều phòng và nhiều seed để thấy phòng chữ nhật/kích thước/chủ đề đa dạng. Kiểm tra vật cản đúng bộ chủ đề, lối cửa 5×6m thoáng; ghi lại seed + grid nếu chỉ có 0–1 cụm.
4. Để một người đứng ở lõi Combat, những người khác ở Spawn/hành lang/phòng khác. Cả đội không Dead phải vào quanh người kích hoạt, vị trí mục tiêu cách entrant 2–4m, cách nhau >=1.5m. Sau 0.5s cổng hạ, sau 0.8s tiếp theo quái spawn. Không ai bị bỏ ngoài hoặc kẹt cổng. Thử lại với Client làm entrant và thử hai người bước vào cùng lúc; clientId nhỏ nhất trong phòng là entrant.
5. Với ít nhất 3–4 máy: tạo một người Downed ở ngoài, một người Dead ở ngoài và một người còn sống khác để tránh GameOver. Người sống vào Combat/Boss: Downed phải được kéo vào, Dead giữ vị trí. Cứu Downed bên trong vẫn hoạt động.
6. Host kiểm tra log `[DungeonEnemySpawner] floor=1 ... baseRange=5..7 rolled=N ... final=M`. Với 2 người, M=N (5–7). Với 3 người, roll 5/6/7 → 7/8/10. Với 4 người → 9/11/13. Một người → 4/4/5. Không hiểu “5–7 gốc” là bắt buộc 5–7 con sau nhân hệ số. HUD mọi máy hiện “Tầng 1”. Không sửa CurrentFloor để làm chuyển tầng trong lượt test này.
7. Khi spawn: kiểm tra quái nằm trong lõi, cách mặt tường >=2m, vật cản >=1.5m, mọi người >=5m. Quái còn sống bị cổng giữ trong phòng; thử sát cửa. Nếu có quái lọt ra, ghi seed/grid/type và thời điểm; bảo vệ Server kiểm tra mỗi giây.
8. Diệt hết: mọi cửa mở, collider cổng tắt; quay lại không khóa/respawn. Phòng Spawn/Upgrade/Treasure/Shop không kéo người, không có vật cản chủ đề hay quái. Tế đàn/rương/shop và Linh hồn vẫn theo luật cũ, mỗi người mua riêng.
9. Vào Boss: gom đội và khóa như Combat; Boss mới spawn, BossFight hiển thị đúng. Test chuyển pha bất tử 2s, triệu hồi Imp; hạ Boss vẫn mở cổng, dọn triệu hồi, thưởng/Victory như trước. Không có phòng xuống tầng hay tự tăng CurrentFloor.
10. Bắt đầu lượt mới: seed mới, CurrentFloor=1, Linh hồn reset. Ghi riêng kết quả từng dòng mục 14, lỗi Console, ảnh và seed để đối chiếu.

Công cụ `Hellfire > Phase 4.8` là menu kiểm tra thủ công dành cho Editor, không tự chạy. Bài kiểm tra Host tự động dùng cổng 17777; chơi LAN bình thường dùng cấu hình UI của game. Không dùng menu dịch chuyển QA để thay thế đi bộ/đồng bộ thật khi nghiệm thu LAN.
