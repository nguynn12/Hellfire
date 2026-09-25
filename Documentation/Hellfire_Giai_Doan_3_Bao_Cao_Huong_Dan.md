# Báo Cáo Kỹ Thuật & Hướng Dẫn Thử Nghiệm Giai Đoạn 3 (Dungeon Generation & Enemy AI)

Dự án: **Hellfire** (Unity 6 `6000.5.6f1` + Netcode for GameObjects `2.13.2` + UTP `2.6.0` + AI Navigation `2.0.14`)  
Nhánh Git: `feature/dungeon-generation`  
Tài liệu tham chiếu gốc: `Documentation/Hellfire_Dac_Ta_Ky_Thuat.md`

---

## 1. Tóm Tắt Các Thành Phần Đã Triển Khai Trong Giai Đoạn 3

Tất cả các tính năng được phát triển bám sát 100% tài liệu đặc tả kỹ thuật:

### 1.1. Sinh Bản Đồ Ngẫu Nhiên Tất Định (Dungeon Generation) — Mục 4.1
- **Thuật toán Room-and-Corridor dựa trên lưới**:
  - Quản lý dữ liệu logic qua [DungeonGrid.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Dungeon/DungeonGrid.cs) và [DungeonGenerator.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Dungeon/DungeonGenerator.cs).
  - Sử dụng duy nhất `System.Random(seed)` — **tuyệt đối không** dùng `UnityEngine.Random` cho phần logic bản đồ, đảm bảo tính tất định (deterministic) 100% trên mọi platform.
  - Số lượng phòng ngẫu nhiên trong khoảng `[8, 14]` phòng (`_minRoomCount` = 8, `_maxRoomCount` = 14).
  - Tự động dựng hình học các phòng và mở cửa (doorways) nối với các hành lang (corridors) 4 hướng liền kề.
- **Xác định phòng Boss xa nhất**:
  - Dùng thuật toán duyệt đồ thị **BFS (Breadth-First Search)** từ phòng xuất phát (`Spawn Room` tại tọa độ `(0, 0)`), tính toán khoảng cách đường đi ngắn nhất đến từng phòng.
  - Phòng có khoảng cách lớn nhất được chọn làm **Phòng Trùm Cuối (Boss Room)**.

### 1.2. Đồng Bộ Bản Đồ Qua Seed Mạng — Mục 2.3
- Quản lý qua [DungeonManager.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Dungeon/DungeonManager.cs).
- Host sinh `dungeonSeed` (int), đồng bộ qua `NetworkVariable<int> DungeonSeed` (Server ghi, toàn bộ Client đọc).
- Mỗi máy tự chạy cùng thuật toán sinh bản đồ cục bộ từ `seed` đó và tự dựng hình học. **Tuyệt đối không truyền hình học qua mạng**, tiết kiệm băng thông và loại trừ giật lag.

### 1.3. Bake NavMesh Lúc Chạy (Runtime NavMesh Baking) — Mục 4.2
- Quản lý qua [RuntimeNavMeshBaker.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/Dungeon/RuntimeNavMeshBaker.cs) sử dụng gói `com.unity.ai.navigation` (`NavMeshSurface`).
- Thứ tự chuẩn xác: Dựng xong hình học bản đồ $\rightarrow$ Mỗi máy tự gọi bake NavMesh cục bộ $\rightarrow$ NavMesh hoàn tất mới cho phép Server spawn quái vật.

### 1.4. Trí Tuệ Nhân Tạo Quái Vật (Enemy FSM) — Mục 5.1 & 6.2
- Kiến trúc FSM quản lý qua [EnemyStateMachine.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/AI/EnemyStateMachine.cs), **chạy CHỈ TRÊN SERVER** (Server-Authoritative). Client chỉ nhận vị trí qua `NetworkTransform`.
- Đủ 4 trạng thái: `Idle`, `Chase`, `Attack`, `Dead`.
- Quét tìm người chơi gần nhất còn sống định kỳ (mỗi 0.5s, không quét mỗi frame để tối ưu hiệu năng).
- Kiểm tra tầm nhìn **Line-of-Sight** bằng `Physics.Raycast` để quái vật không bao giờ nhìn xuyên tường.
- Khi bị người chơi bắn trúng: Dù chưa thấy cũng lập tức chuyển sang `Chase` và nhắm vào người bắn.
- Đầy đủ 3 loại quái chuẩn thông số Mục 6.2:
  1. **Quỷ nhỏ (Imp)**: 30 HP, 10 DMG/đòn, tốc độ 5 m/s, cận chiến liên tục.
  2. **Cung thủ địa ngục (Hellspawn Archer)** ([HellspawnArcherAI.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/AI/HellspawnArcherAI.cs)): 25 HP, 12 DMG/phát (hitscan từ Server), tốc độ 3.5 m/s, giữ cự ly 10–20m, tự động lùi lại nếu người chơi áp sát < 10m.
  3. **Quỷ khổng lồ (Brute)** ([BruteAI.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/AI/BruteAI.cs)): 120 HP, 25 DMG/đòn, tốc độ 2.5 m/s, tank melee gây rung chấn/choáng ngắn.

### 1.5. Chúa Tể Địa Ngục (Boss Hellfire Lord) — Mục 5.2
- Quản lý qua [BossStateMachine.cs](file:///c:/Users/storm/Downloads/HellFire/Assets/_Project/Scripts/AI/BossStateMachine.cs) đặt tại phòng Boss xa nhất.
- Máu: **800 HP**.
- Đủ 3 trạng thái FSM riêng:
  1. **Phase 1** (Máu > 50%): Tốc độ 3.5 m/s, cận chiến quét rộng AOE 4m (30 DMG, trúng nhiều người chơi cùng lúc).
  2. **PhaseTransition** (Máu $\le$ 50% lần đầu): Boss **bất tử tạm thời trong 2 giây** (`Health.IsInvulnerable = true`), phát hiệu ứng gầm thét, di chuyển về giữa phòng boss, sau 2s tắt bất tử và chuyển sang Phase 2.
  3. **Phase 2**: Tốc độ tăng lên 4.5 m/s, xen kẽ 60% cận chiến và 40% lùi bắn cầu lửa tầm xa AOE 3m (20 DMG).
  4. **Cơ chế triệu hồi Imp**: Cứ mỗi 20% máu tối đa mất đi ở Phase 2 (mỗi 160 HP), boss tự động triệu hồi thêm 2 quái Imp hỗ trợ.

---

## 2. Hướng Dẫn Thiết Lập Tự Động 1-Click Trong Unity Editor

1. Mở dự án trong **Unity Editor (6000.5.6f1)**.
2. Trên thanh menu trên cùng, chọn:
   ```text
   Hellfire -> Phase 3 -> Setup All (Dungeon Generator, NavMesh Baker, Enemy & Boss Prefabs)
   ```
3. Công cụ tự động:
   - Tạo 4 Prefab quái vật & Boss với đầy đủ NavMeshAgent, Health, FSM, NetworkTransform, 3 Collider Hitbox (`EnemyImp.prefab`, `EnemyArcher.prefab`, `EnemyBrute.prefab`, `EnemyBossHellfireLord.prefab`).
   - Đăng ký cả 4 prefab vào `DefaultNetworkPrefabs.asset`.
   - Cấu hình GameObject `DungeonManager` hoàn chỉnh trong scene `Gameplay.unity` (gồm `DungeonGenerator`, `RuntimeNavMeshBaker`, `DungeonEnemySpawner`).

---

## 3. Hướng Dẫn Kiểm Thử 2–4 Máy Qua LAN

### Các Bước Thực Hiện:
1. Chạy menu `Hellfire -> Phase 3 -> Setup All` trong Unity Editor.
2. Chọn `File -> Build Profiles` $\rightarrow$ Nhấn **Build** ra thư mục `Build/Hellfire.exe`.
3. Phân phối file build sang 2–4 máy LAN (hoặc chạy 1 máy Host trong Unity Editor + 1 máy Client bằng file `.exe`).
4. **Máy 1 (Host)**: Tạo phòng LAN trong `MainMenu` $\rightarrow$ Nhấn "Bắt Đầu Trận Đấu".
5. **Máy 2 / 3 / 4 (Client)**: Nhấn "Tìm Phòng Tự Động" hoặc nhập IP máy Host và bấm "Tham Gia".

### Các Kịch Bản Xác Minh:

#### Kịch bản 1: Kiểm tra tính Đồng nhất của Bản đồ giữa các máy
- Khi vào màn `Gameplay`, Host tự động sinh Seed (ví dụ `582914`) và gửi tới mọi Client.
- **Xác minh**:
  - Đi bộ qua các phòng: Số lượng phòng, hình dạng hành lang, vị trí các góc cua và cửa mở trên tất cả các máy Host và Client là **giống hệt nhau 100%**.
  - Không có tình trạng người chơi đi xuyên qua tường hoặc bị kẹt lơ lửng trên máy khác.

#### Kịch bản 2: Kiểm tra Quái vật Thường & Tìm đường NavMesh
- Tiến vào các phòng Normal:
  - **Quỷ nhỏ (Imp)**: Thấy người chơi sẽ lao thẳng cận chiến với tốc độ nhanh (5 m/s).
  - **Cung thủ (Archer)**: Đứng ở cự ly 10–20m bắn tia hitscan màu tím về phía người chơi. Nếu người chơi chạy áp sát lại gần (< 10m), Archer sẽ lùi lại giữ cự ly.
  - **Quỷ khổng lồ (Brute)**: Đi chậm (2.5 m/s), chịu được nhiều phát bắn (120 HP), đánh cận chiến gây sát thương nặng (25 DMG).
  - Quái vật chỉ di chuyển khi có đường đi hợp lệ trên NavMesh, không đi xuyên tường hay nhìn thấy người chơi qua vách ngăn kín (Line-of-Sight).

#### Kịch bản 3: Kiểm tra Boss Chúa Tể Địa Ngục (800 HP)
- Đi theo hành lang đến phòng xa nhất (Phòng Boss):
  - **Phase 1**: Boss lao vào cận chiến quét rộng. Bắn boss tụt từ 800 HP xuống 400 HP (50%).
  - **PhaseTransition**: Ngay khi máu chạm 400 HP, boss phát sáng vàng, chạy về tâm phòng và **bất tử trong 2 giây** (bắn vào không mất máu).
  - **Phase 2**: Sau 2s, boss chuyển sang Phase 2 (chạy nhanh hơn 4.5 m/s, bắn cầu lửa nổ AOE). Khi boss mất thêm mỗi 160 HP, quan sát thấy 2 quái Imp được triệu hồi xuất hiện ngay trong phòng boss!
