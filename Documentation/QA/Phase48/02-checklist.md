# Nhóm 2 — kiểm chứng

Nhánh: feature/rect-rooms. Unity 6000.5.6f1; chạy Host qua MainMenu → Lobby → Gameplay.
Console: 52 logs, 6 warnings, 0 errors. EnemyStateMachine = 0 trước khóa phòng.
Đạt nghĩa là đã kiểm tra ở phạm vi ghi bên dưới; Không đạt nghiệm thu có thể là thiếu kiểm thử, không khẳng định code lỗi.
Không có máy LAN thứ hai trong lượt kiểm tra này.

| # | Ràng buộc mục 14 | Kết quả |
|---|---|---|
| 1 | Góc nhìn là **first-person**, không có camera thứ ba/top-down. | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 2 | Di chuyển dùng `CharacterController`, không dùng Rigidbody physics-based. | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 3 | Toàn bộ trạng thái quan trọng (máu, vị trí quái vật, vật phẩm rơi, trạng thái rương) do **Server quyết định**, client không tự quyết định rồi báo lên. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 4 | Sát thương được **Server tự raycast xác nhận lại**, không tin số liệu client tự gửi. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 5 | Bản đồ ngẫu nhiên đồng bộ qua **seed**, không truyền hình học qua mạng. | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 6 | Thuật toán sinh bản đồ dùng `System.Random(seed)`, deterministic tuyệt đối. | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 7 | NavMesh bake **sau khi** dựng xong hình học, **trước khi** spawn quái vật. | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 8 | AI quái vật chỉ chạy logic trên **Server**. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 9 | Điểm gốc raycast tính sát thương là **tâm camera**, không phải nòng súng vật lý. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 10 | Friendly fire **TẮT**: raycast vẫn dừng lại ở đồng đội (chặn đường bắn) nhưng **không trừ máu** đồng đội. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 11 | Nhân vật chết vào trạng thái **`Downed`** trước (60 giây, có thể chỉnh trong Inspector), không loại khỏi lượt chơi ngay; chỉ loại khi hết `downedDuration` mà chưa được đồng đội revive (giữ phím 3 giây, hồi 30% máu tối đa). | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 12 | Rương thường: **40% ra bùa lợi, 60% ra vũ khí**. Rương boss: 100% có ít nhất 1 bùa lợi. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 13 | Không code dash/double-jump ở bản MVP hiện tại — đây là giới hạn phạm vi có chủ đích, không phải thiếu sót. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 14 | Toàn bộ điều kiện và kết quả revive (thời gian giữ phím, % máu hồi lại) do **Server xác thực**, client không tự báo "đã cứu xong". | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 15 | **KHÔNG** cài đặt host migration/server failover dưới bất kỳ hình thức nào — Host rớt mạng thì cả phòng về Main Menu, không khôi phục trạng thái. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 16 | Toàn bộ UI dùng **uGUI (Canvas)** — không có màn hình nào dùng UI Toolkit. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 17 | Mọi hệ thống gameplay kiểm tra `GameManager.CurrentState` trước khi cho phép hành động (không bắn được khi ở `Lobby`, AI không chạy khi ở `Generating`...). | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 18 | Chuyển Scene `Lobby → Gameplay` dùng `NetworkManager.SceneManager`, không dùng `SceneManager.LoadScene()` gọi độc lập từng client. | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 19 | Phong cách hình ảnh là **địa ngục tối tăm cổ điển**, không phải liminal space/Backrooms. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 20 | Boss có đúng 3 trạng thái FSM: `Phase1 → PhaseTransition (bất tử 2s) → Phase2`, không bỏ qua bước bất tử tạm thời khi chuyển pha. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 21 | `Player.prefab`: `NetworkTransform` ở root có `AuthorityMode = Owner` (ngoại lệ mục 2.1.1); mọi NetworkTransform khác giữ `Server`. | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 22 | Server không gán trực tiếp `transform.position` của người chơi; dịch chuyển người chơi chỉ qua `ClientRpc` nhắm chủ nhân (tắt CharacterController → đặt vị trí → `Teleport` → bật lại). | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 23 | Quái/rương/boss chỉ được spawn SAU KHI mọi client báo `DungeonReady` (mục 15.1); mọi vị trí spawn đều qua `NavMesh.SamplePosition` (mục 15.2). | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 24 | Số quái và máu boss nhân theo số người chơi lúc vào `Generating` và giữ cố định cả lượt (mục 15.3). | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 25 | Màu người chơi chỉ lấy từ bảng 4 màu ở mục 16.1, không dùng màu đỏ cho người chơi. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 26 | Scene Gameplay không còn `ArenaFloor`/`Pillar_*`; `HellfirePhase1Setup` không tạo lại chúng (mục 17.0). | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 27 | `DungeonRoomType` có đúng 6 loại (Spawn, Combat, Upgrade, Treasure, Shop, Boss); phòng Upgrade/Treasure/Shop ưu tiên nhánh cụt ngoài đường chính; phòng an toàn không có quái, cổng, cột (mục 17.1). | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 28 | Hành lang không có quái, không khóa; chỉ phòng Combat có 0–4 cột đặt ở 4 ô góc (mục 17.2, 17.3). | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 29 | Cổng sắt chỉ có ở phòng Combat/Boss; collider bật khi đóng và tắt khi mở; Server quyết định khóa/mở; mỗi phòng chỉ khóa một lần (mục 17.4). | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 30 | Quái chỉ spawn khi phòng chuyển `Locked`; boss chỉ spawn khi phòng boss `Locked` (mục 17.5). | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 31 | Linh hồn là `NetworkVariable<int>` do Server ghi, mỗi người một ví, reset mỗi lượt (mục 17.7). | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 32 | Shop: 5 món giống nhau cho mọi người; tập "đã mua" tách riêng từng người; Server xác thực mọi giao dịch (mục 17.8). | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 33 | Đạn dự trữ VẪN VÔ HẠN (mục 18, 19.3); không có đạn dự trữ hữu hạn trong `WeaponController`/`RuntimeWeapon`. | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 34 | Cảm giác súng của Benz (WeaponSwayBobbing, WeaponRecoil, WeaponAimDownSights, WeaponMotionCoordinator) được gắn vào `Player.prefab` THẬT và chỉ chạy phía chủ sở hữu (thuần hình ảnh); hitscan vẫn do Server xác nhận (mục 19.2). | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 35 | `RangedZombieAI` có prefab chính thức, đã đăng ký NetworkPrefabs và nằm trong bảng trọng số spawn của phòng Combat (mục 19.4). | Không đạt nghiệm thu — chưa kiểm thử đầy đủ, cần test hồi quy/LAN |
| 36 | Scene `Gameplay.unity` KHÔNG chứa quái, vật cản, rương, tế đàn nào đặt sẵn: mọi thứ đó do `DungeonManager` sinh lúc chạy. Trước khi phòng nào khóa, số quái đang tồn tại phải bằng 0 (mục 20.0). | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 37 | Sau khi đổi bất kỳ `[SerializeField]` mặc định nào, PHẢI kiểm tra lại giá trị đã lưu trong scene (scene ghi đè mặc định trong code) (mục 20.1). | Đạt trong smoke test/kiểm tra source liên quan; chưa thay thế test LAN |
| 38 | Phòng Combat là hình chữ nhật với kích thước riêng từng phòng; hành lang dài tối thiểu 10 m, không quái, không vật cản (mục 20.2, 20.4). | Đạt — kích thước phòng, hành lang kiểm tra trong smoke test; đọc lại scene |
| 39 | Phòng Combat có chủ đề (hầm rượu, hầm mộ, lò rèn, nhà ngục, mỏ đổ nát), ít vật cản theo đúng bộ của chủ đề, luôn chừa lối vào thông thoáng (mục 20.3). | Không đạt — thuộc nhóm 2–5, chưa triển khai |
| 40 | Số quái gốc mỗi phòng tăng theo `CurrentFloor` (tầng 1: 5–7), nhân hệ số số người chơi; `CurrentFloor` do Server ghi; chưa có chuyển tầng (mục 20.6, 20.8). | Không đạt — thuộc nhóm 2–5, chưa triển khai |
| 41 | Một người vào phòng Combat/Boss thì cả đội còn trong cuộc (kể cả Downed) được dịch chuyển vào theo, cổng khóa, quái spawn (mục 20.5). | Không đạt — thuộc nhóm 2–5, chưa triển khai |
