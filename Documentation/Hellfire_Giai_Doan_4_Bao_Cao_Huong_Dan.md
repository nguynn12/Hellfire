# Báo Cáo Kỹ Thuật & Hướng Dẫn Thử Nghiệm Giai Đoạn 4 (Power-ups, Loot Chests & Hoàn Thiện MVP)

Dự án: **Hellfire** (Unity 6 `6000.5.6f1` + Netcode for GameObjects `2.13.2` + UTP `2.6.0` + AI Navigation `2.0.14`)  
Nhánh Git: `feature/loot-and-gameflow`  
Tài liệu tham chiếu gốc: `Documentation/Hellfire_Dac_Ta_Ky_Thuat.md` và `Documentation/Hellfire_Phan_Tich_Lo_Trinh.md`

---

## 1. Tóm Tắt Các Thành Phần Đã Triển Khai Trong Giai Đoạn 4 (MVP Hoàn Chỉnh)

Tất cả các tính năng được phát triển bám sát 100% tài liệu đặc tả kỹ thuật, không vượt quá phạm vi đã thống nhất:

### 1.1. Hệ Thống Bùa Lợi (Power-up System) — Mục 6.1 & 6.2
- **Cấu trúc Dữ liệu & ScriptableObjects**:
  - Định nghĩa enum [PowerUpType.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Items/PowerUpType.cs) và ScriptableObject [PowerUpData.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Items/PowerUpData.cs).
  - Đầy đủ 4 loại bùa lợi chuẩn thông số thiết kế cân bằng:
    1. **Trái Tim Máu Lớn (`MaxHealthUp`)**: `+25 HP` máu tối đa, **hồi đầy máu ngay khi nhặt** (vĩnh viễn trong lượt chơi).
    2. **Giày Tốc Độ (`SwiftBoots`)**: `+20%` tốc độ di chuyển áp dụng cho toàn bộ đi bộ, chạy nhanh và đi lùi (vĩnh viễn trong lượt chơi).
    3. **Bùa Cuồng Nộ (`BerserkerCharm`)**: `+30%` sát thương gây ra cho tất cả các loại súng (vĩnh viễn trong lượt chơi).
    4. **Khiên Bảo Vệ (`GuardianShield`)**: Miễn nhiễm sát thương hoàn toàn trong **8 giây**, kích hoạt ngay khi nhặt.
- **Quản lý Bùa Lợi Máy Chủ (Server-Authoritative Buff Manager)**:
  - Triển khai [PlayerBuffManager.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Player/PlayerBuffManager.cs) trên Player.
  - Các chỉ số `SpeedMultiplierNet`, `DamageMultiplierNet`, `ShieldTimerNet` được đồng bộ bằng `NetworkVariable` chỉ Server được phép ghi.
  - Tích hợp trực tiếp vào [Health.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Combat/Health.cs), [PlayerMovement.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Player/PlayerMovement.cs), và [WeaponController.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Combat/WeaponController.cs).

### 1.2. Rương Vật Phẩm (Loot Chest) & Rơi Đồ (Item Pickup) — Mục 6.3
- **Rương Vật Phẩm Server-Authoritative**:
  - Triển khai [LootChest.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Items/LootChest.cs).
  - Trạng thái mở rương là `NetworkVariable<bool> IsOpened` chỉ Server ghi.
  - Người chơi tiến lại gần rương (< 3m) và nhấn phím `E` để gửi `RequestOpenChestServerRpc()`.
  - **Tỷ lệ rơi đồ đã chốt trong đặc tả**:
    - **Rương thường**: `40% Bùa lợi` / `60% Vũ khí` (tự động loại trừ vũ khí người chơi đang cầm trên tay nếu có thể).
    - **Rương Boss**: `100% rơi ít nhất 1 bùa lợi` + `50% rơi thêm 1 vũ khí` (`// TODO(cần xác nhận): tỉ lệ rơi thêm vũ khí ở rương boss tạm đặt 50% theo mục 13`).
  - Hoạt ảnh xoay nắp rương mượt mà, phát âm thanh và hiệu ứng ánh sáng.
- **Vật Phẩm Rơi Trên Mặt Đất (Item Pickup)**:
  - Triển khai [ItemPickup.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Items/ItemPickup.cs) với `NetworkObject`.
  - Hiệu ứng 3D xoay tròn, nhấp nhô sóng sine, phát sáng theo màu nhận diện của bùa/vũ khí, TextMeshPro hiển thị tên và mô tả chi tiết.
  - Khi người chơi chạm vào collider, Server xác thực và kích hoạt bùa lợi hoặc trao súng mới cho `WeaponController`.

### 1.3. Tích Hợp Rương Vào Hầm Ngục & Trận Đánh Boss — Mục 4.1, 5.2, 6.3
- [DungeonEnemySpawner.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Dungeon/DungeonEnemySpawner.cs): Tự động sinh 1 rương báu trong mỗi phòng thường (`Normal Room`) của hầm ngục.
- [BossStateMachine.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/AI/BossStateMachine.cs): Khi Chúa quỷ Hellfire (800 HP) bị tiêu diệt, tự động sinh **Rương Boss Hoàng Kim** ngay tại vị trí boss tử trận, đồng thời gửi tín hiệu kích hoạt Chiến Thắng tới `GameManager`.

### 1.4. Quản Lý Vòng Lặp Trò Chơi (Game Loop & Game State) — Mục 10.1 & 10.2
- Triển khai mở rộng [GameManager.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Networking/GameManager.cs):
  - Đồng bộ `NetworkVariable<GameState> CurrentState`: `Lobby` $\rightarrow$ `Generating` $\rightarrow$ `Playing` $\rightarrow$ `BossFight` $\rightarrow$ `Victory` / `GameOver` $\rightarrow$ `Lobby`.
  - **Điều kiện Thất bại (Game Over)**: Server định kỳ kiểm tra toàn bộ người chơi trong phòng. Khi **tất cả người chơi đều bị Downed hoặc Dead** $\rightarrow$ Kích hoạt `GameOver`.
  - **Điều kiện Chiến thắng (Victory)**: Khi Boss bị hạ gục $\rightarrow$ Kích hoạt `Victory`.
  - **Vòng lặp Chơi lại (Replay Loop)**: Host nhấn nút "Về Sảnh (Lobby)" trên màn hình kết quả $\rightarrow$ Server gọi `NetworkManager.SceneManager.LoadScene("Lobby", LoadSceneMode.Single)` chuyển đồng bộ tất cả người chơi về sảnh để bắt đầu một lượt chơi mới với Seed hầm ngục hoàn toàn mới.

### 1.5. Nâng Cấp Giao Diện HUD Đầy Đủ (uGUI Canvas) — Mục 7 & 10.1
- Triển khai mở rộng [GameplayHUD.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/UI/GameplayHUD.cs):
  - **Pause Menu cục bộ**: Nhấn `ESC` để bật/tắt (mở khóa chuột, nút Tiếp tục, nút Rời phòng; **không làm dừng Time.timeScale** để tránh lỗi mạng LAN).
  - **Khiên Bảo Vệ (Guardian Shield)**: Panel đếm ngược thời gian bất tử màu vàng cam ở góc trên bên trái (`🛡️ KHIÊN BẢO VỆ: 7.8s`).
  - **Buff Toast**: Dòng thông báo nổi màu sắc sinh động ở giữa màn hình khi nhặt được bùa lợi (`+25 MÁU TỐI ĐA!`, `+20% TỐC ĐỘ!`, v.v.).
  - **Màn hình Kết Quả Chiến Thắng (Victory Overlay)**: Tông màu xanh ngọc/hoàng kim rực rỡ, hiển thị nút "Về Sảnh" cho Host và nhãn "Đang chờ chủ phòng..." cho Client.
  - **Màn hình Kết Quả Thất Bại (Game Over Overlay)**: Tông màu đỏ thẫm hắc ám khi cả đội bị quái hạ gục.

---

## 2. Hướng Dẫn Thiết Lập Tự Động 1-Click Trong Unity Editor

1. Mở dự án trong **Unity Editor (6000.5.6f1)**.
2. Trên thanh menu trên cùng, chọn:
   ```text
   Hellfire -> Phase 4 -> Setup All (Power-ups, Loot Chests, Game Loop & UI Overlays)
   ```
3. Công cụ [HellfirePhase4Setup.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Editor/HellfirePhase4Setup.cs) sẽ tự động thực thi:
   - Tạo 4 `PowerUpData` ScriptableObjects trong `Assets/_Project/Data/PowerUps/`.
   - Tạo các Prefab `ItemPickup.prefab`, `LootChest.prefab`, `BossLootChest.prefab`.
   - Cập nhật `PlayerBuffManager` vào `Player.prefab`.
   - Cập nhật liên kết `BossChestPrefab` vào `EnemyBossHellfireLord.prefab`.
   - Cập nhật liên kết `LootChestPrefab` vào `DungeonEnemySpawner` trong scene `Gameplay.unity`.
   - Đăng ký toàn bộ Prefabs vào `NetworkManager.prefab`.
   - Thiết lập đầy đủ Canvas UI: Shield Timer, Buff Toast, Victory & GameOver Overlays.

---

## 3. Hướng Dẫn Kiểm Thử Vòng Lặp Trò Chơi Hoàn Chỉnh (LAN Co-op 2–4 Người Chơi)

### Các Bước Chuẩn Bị:
1. Chạy menu `Hellfire -> Phase 4 -> Setup All` trong Unity Editor.
2. Chọn `File -> Build Profiles` $\rightarrow$ Nhấn **Build** ra thư mục `Build/Hellfire.exe`.
3. Khởi chạy 1 máy Host và 1-3 máy Client trên cùng mạng LAN nội bộ.
4. Host tạo phòng tại `MainMenu` $\rightarrow$ Client tìm phòng hoặc nhập IP và tham gia vào `Lobby` $\rightarrow$ Host bấm **"Bắt Đầu Trận Đấu"**.

---

### Kịch Bản 1: Kiểm Tra Rương Báu & Nhặt Bùa Lợi
- Di chuyển qua các phòng Normal trong hầm ngục:
  - Nhìn thấy các rương báu bằng gỗ phát sáng màu vàng.
  - Đi lại gần rương (< 3m), màn hình hiện gợi ý `[E] Mở Rương`.
  - Nhấn `E`: Nắp rương tự động mở lên, phát âm thanh mở khóa, và rơi ra một viên ngọc Bùa lợi hoặc Súng mới.
  - Đi qua viên ngọc:
    - Nếu là **Trái tim máu lớn**: Máu tối đa tăng lên `125 HP` (hoặc `150 HP`), thanh máu đầy 100%, xuất hiện thông báo toast màu xanh lá.
    - If là **Giày tốc độ**: Tốc độ di chuyển đi/chạy/lùi của nhân vật tăng rõ rệt (+20%).
    - If là **Bùa cuồng nộ**: Bắn vào quái vật thấy lượng máu quái tụt nhanh hơn rõ rệt (+30% DMG).
    - If là **Khiên bảo vệ**: Góc trên màn hình xuất hiện đồng hồ đếm ngược `🛡️ KHIÊN BẢO VỆ: 8.0s`. Trong 8 giây này, quái cắn hoặc đánh trúng người chơi **hoàn toàn không mất máu**!

---

### Kịch Bản 2: Kiểm Tra Nhặt Vũ Khí Mới Từ Rương
- Mở rương rơi ra súng (ví dụ: Shotgun hoặc Súng trường Rifle):
  - Chạm vào súng rơi: Hệ thống tự động thêm súng vào trang bị của người chơi.
  - Sử dụng phím `1`, `2`, `3`, `4` để chuyển đổi qua lại giữa Súng lục, SMG, Shotgun, Rifle.

---

### Kịch Bản 3: Kiểm Tra Màn Hình Thất Bại (Game Over)
- Cho tất cả người chơi trong phòng lao vào quái vật để bị hạ gục (Downed) và hết thời gian 15s đếm ngược:
  - Khi người chơi cuối cùng gục ngã/chết:
  - Trận đấu ngay lập tức hiển thị màn hình **THẤT BẠI (GAME OVER)**.
  - Trên máy Host: Có nút **"VỀ SẢNH (LOBBY)"**.
  - Trên máy Client: Hiển thị dòng chữ *"Đang chờ Chủ phòng quay về sảnh..."*.
  - Host nhấn nút: Cả Host và Client đồng loạt được chuyển về phòng chờ `Lobby` an toàn mà **không bị mất kết nối mạng**.

---

### Kịch Bản 4: Tiêu Diệt Boss, Rương Boss & Màn Hình Chiến Thắng (Victory)
- Cả đội cùng tiến vào Phòng Boss xa nhất:
  - Chiến đấu vượt qua Phase 1 $\rightarrow$ Phase Transition $\rightarrow$ Phase 2 của Boss Hellfire Lord (800 HP).
  - Khi bắn phát đạn cuối cùng hạ gục Chúa quỷ:
    1. Boss gục xuống, ngay lập tức xuất hiện **Rương Boss Hoàng Kim** với ánh sáng tím rực rỡ (rơi bùa lợi và vũ khí cao cấp).
    2. Màn hình xuất hiện giao diện **CHIẾN THẮNG (VICTORY)** rực rỡ chúc mừng toàn đội.
    3. Host nhấn nút **"VỀ SẢNH (LOBBY)"** để dẫn cả đội quay về phòng chờ và bắt đầu lượt chơi mới (với Seed hầm ngục mới được sinh ngẫu nhiên).
