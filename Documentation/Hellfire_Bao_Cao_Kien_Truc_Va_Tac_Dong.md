# BÁO CÁO PHÂN TÍCH KIẾN TRÚC & TÁC ĐỘNG HỆ THỐNG (HELLFIRE)
*(Cập nhật sau đợt Tái cấu trúc Hệ thống Chiến đấu, Chỉ số Nhân vật, Vũ khí & Tế đàn)*

---

## 1. Sơ Đồ Khối Kiến Trúc & Luồng Dữ Liệu Tổng Quan

### 1.1. Tóm tắt Architecture Patterns Đang Sử Dụng
Dự án **Hellfire** là một game bắn súng góc nhìn thứ nhất (FPS) Co-op qua mạng LAN (tối đa 4 người) được xây dựng trên nền tảng **Unity Netcode for GameObjects (NGO)** kết hợp **Unity Transport (UTP)**. Codebase triển khai các design pattern chính sau:
- **Server-Authoritative Network Pattern**: Mọi quyết định trạng thái quan trọng (trừ máu, tính toán giáp hiệu dụng theo hàm mũ $e^{-0.01053605 \times \text{Armor}}$, nâng cấp chỉ số, kiểm tra điều kiện tế đàn, spawn quái vật, FSM của AI, sinh map) đều do Server/Host làm chủ. Client chỉ gửi RPC (`ServerRpc`) và nhận kết quả hiển thị.
- **Phân tách Trách nhiệm Chỉ số (Decoupled Stat Architecture)**:
  - **Nhân vật (`PlayerStats`)**: Chỉ quản lý các chỉ số sinh tồn và cơ động: Máu cộng thêm (`BonusMaxHealth`), Giáp (`Armor`), Hệ số tốc chạy (`MoveSpeedMultiplier`) với các trần cứng (Max Caps). Không sở hữu chỉ số tấn công.
  - **Vũ khí (`WeaponData` & `RuntimeWeapon`)**: Quản lý toàn bộ chỉ số tấn công: Sát thương (`BaseDamage`), Tốc đánh (`AttackRateAPS`), Xuyên giáp (`ArmorPenetration`), Chí mạng (`CritChance`, `CritDamageMultiplier`), Băng đạn, Nạp đạn và Tầm quét cận chiến (`MeleeRange`, `MeleeSwingAngle`).
- **Open/Closed Principle qua Modifier Slot (`IWeaponModifier`)**: Cung cấp interface mở rộng cho phép gắn ngọc, phụ kiện, hiệu ứng đòn đánh vào vũ khí mà không cần sửa code lõi.
- **Zero-GC Damage Pipeline**: Sử dụng `readonly struct DamagePayload`, bộ đệm tĩnh `Physics.OverlapSphereNonAlloc` và cache Material cho hiệu ứng đạn/va chạm để loại bỏ hoàn toàn cấp phát bộ nhớ rác trong vòng lặp chiến đấu.
- **Shrine History Tracking (Lưu lịch sử trên người chơi)**: Mỗi tế đàn có 1 `ShrineId` duy nhất. `PlayerStats` trên Server lưu trữ `HashSet<int> _usedShrineIds`. Hiệu ứng dập lửa được gửi qua `ClientRpc` riêng biệt chỉ tới máy người chơi đã kích hoạt (Client-side visual), đồng đội chưa dùng vẫn thấy cháy sáng bình thường.
- **Classic Singleton Pattern**: Triển khai qua `public static Instance { get; private set; }` trên các lớp dịch vụ (`GameManager`, `NetworkConnectManager`, `LanDiscovery`, `DungeonManager`).
- **C# Event-Driven / Observer Pattern**: Sử dụng `System.Action` để phân tách tầng mô phỏng và giao diện HUD (`Health.OnHealthChanged`, `PlayerStats.OnStatsChanged`, `WeaponController.OnAmmoChanged`).

### 1.2. Đánh giá Decoupling & Khả năng mở rộng (Extensibility)
- **Điểm đánh giá tổng thể:** **Tốt / Cao (High - 8.5/10)** *(Tăng từ 6.5/10 sau đợt tái cấu trúc)*.
- **Lý do kỹ thuật ngắn gọn:**
  - *Điểm mạnh (Pros):*
    - Chỉ số tấn công và sinh tồn đã được phân tách triệt để giữa `RuntimeWeapon` và `PlayerStats`.
    - Interface `IDamageable` nhận `DamagePayload` kết hợp công thức giáp chuẩn hóa loại bỏ hoàn toàn việc phụ thuộc cứng vào kiểu đối tượng.
    - Cơ chế mở rộng `IWeaponModifier` giải quyết triệt để vi phạm Open/Closed Principle.
    - Hiệu năng chiến đấu đạt Zero-GC (không sinh rác Heap).
  - *Điểm cần lưu ý (Minor Blockers còn lại):*
    - `EnemyStateMachine` vẫn sử dụng `switch-case` trên enum trạng thái `EnemyState` (sẽ tối ưu hóa hơn nếu chuyển sang State Pattern dạng class ở giai đoạn AI nâng cao).
    - `PlayerRevive` vẫn quét tìm đồng đội gần nhất bằng `FindObjectsByType<Health>`.

### 1.3. Sơ đồ khối kiến trúc & luồng dữ liệu

```mermaid
flowchart TD
    subgraph UI_Layer ["Tầng Giao Diện (Presentation Layer)"]
        GameplayHUD["GameplayHUD.cs<br/>(Hiển thị HP, Đạn, Sát thương tròn)"]
        LobbyUI["LobbyUI.cs"]
        MainMenuUI["MainMenuUI.cs"]
    end

    subgraph Player_Subsystem ["Hệ Thống Người Chơi (Player)"]
        PStats["PlayerStats.cs<br/>(BonusHealth, Armor, Speed)<br/>[Lưu lịch sử ShrineId]"]
        PMove["PlayerMovement.cs<br/>(Nhân hệ số MoveSpeedMultiplier)"]
        PRev["PlayerRevive.cs"]
    end

    subgraph Combat_Subsystem ["Hệ Thống Chiến Đấu (Combat System)"]
        WCtrl["WeaponController.cs<br/>(Quản lý List<RuntimeWeapon>)<br/>[Hitscan + Melee NonAlloc]"]
        RWeapon["RuntimeWeapon.cs<br/>(Level Upgrade, Evaluate Stats)"]
        WData["WeaponData.cs<br/>(ScriptableObject)"]
        WMod["<<interface>> IWeaponModifier<br/>(Gắn Ngọc / Phụ kiện)"]
        DPayload["<<readonly struct>> DamagePayload<br/>(RawDamage, ArmorPen, Hitbox)"]
        Health["Health.cs<br/>(Công thức Giáp e^-0.01053605*Armor)"]
        IDam["<<interface>> IDamageable"]
        Hitbox["HitboxIdentifier.cs"]
    end

    subgraph World_Items ["Tầng Thế Giới & Vật Phẩm (Items)"]
        Shrine["InteractiveShrine.cs<br/>(ShrineId, BodyBuff / WeaponUpgrade)<br/>[Dập lửa Client-Side]"]
    end

    subgraph AI_Enemies ["Tầng Trí Tuệ Nhân Tạo (AI - Server Only)"]
        ESM["EnemyStateMachine.cs"]
        Brute["BruteAI.cs"]
        Archer["HellspawnArcherAI.cs"]
        Boss["BossStateMachine.cs"]
    end

    %% Mối liên hệ
    WCtrl --> RWeapon
    RWeapon --> WData
    RWeapon --> WMod
    WCtrl -->|Gửi đòn đánh| DPayload
    DPayload --> Hitbox
    Hitbox --> IDam
    IDam <|.. Health
    Health -->|Đọc Armor| PStats
    PMove -->|Đọc Speed Multiplier| PStats

    Shrine -->|Tăng Máu/Giáp/Tốc độ| PStats
    Shrine -->|Tăng Cấp Súng| WCtrl
    Shrine -.->|Kiểm tra ID đã dùng| PStats

    ESM -->|Tấn công qua DamagePayload| Health
    Brute --|> ESM
    Archer --|> ESM
    Boss --|> ESM

    GameplayHUD -.->|Event Action| Health
    GameplayHUD -.->|Event Action| WCtrl
    GameplayHUD -.->|Event Action| PStats
```

---

## 2. Ma Trận Chức Năng & Vị Trí Code (Feature-to-File Map)

| Feature / Subsystem | Class/Script chính | Đường dẫn file tương đối | Trách nhiệm chính (Single Responsibility) | Khả năng mở rộng (Dễ/Khó & Lý do) |
|---|---|---|---|---|
| **Zero-GC Damage Struct** | `DamagePayload` | `../Assets/_Project/Scripts/Combat/DamagePayload.cs` | Struct bất biến chứa thông tin đòn đánh: RawDamage, ArmorPen, Hitbox, AttackerId | **Rất Dễ:** Dạng readonly struct, bổ sung cờ trạng thái đòn đánh (chí mạng, nguyên tố) không gây GC. |
| **Weapon Modifier Slot** | `IWeaponModifier` | `../Assets/_Project/Scripts/Combat/IWeaponModifier.cs` | Interface mở rộng cho phép gắn ngọc, bùa chú, phụ kiện vào súng/vũ khí cận chiến | **Rất Dễ (Chuẩn OCP):** Tạo class mới cài đặt interface mà không cần sửa bất kỳ dòng code vũ khí nào. |
| **Runtime Weapon Instance** | `RuntimeWeapon` | `../Assets/_Project/Scripts/Combat/RuntimeWeapon.cs` | Quản lý cấp độ nâng cấp, danh sách modifier và đánh giá chỉ số cuối của vũ khí | **Dễ:** Tách biệt hoàn toàn trạng thái động khỏi ScriptableObject tĩnh. |
| **Player Body Stats** | `PlayerStats` | `../Assets/_Project/Scripts/Player/PlayerStats.cs` | Quản lý máu cộng thêm, giáp, tốc độ chạy có trần cứng và lưu lịch sử Tế đàn đã dùng | **Dễ:** Mọi buff cơ thể đều đi qua `TryApplyBodyBuff`, đảm bảo tính toàn vẹn chỉ số Server. |
| **Interactive Shrine** | `InteractiveShrine` | `../Assets/_Project/Scripts/Items/InteractiveShrine.cs` | Tế đàn ban phước tăng chỉ số hoặc nâng cấp súng; xử lý dập lửa cục bộ client | **Dễ:** Cấu hình loại buff qua Inspector, đặt nhiều tế đàn với ShrineId độc lập. |
| **Damage Contract** | `IDamageable` | `../Assets/_Project/Scripts/Combat/IDamageable.cs` | Contract interface nhận `DamagePayload` cho Server & Client quan sát | **Dễ:** Mọi thực thể nhận sát thương chỉ cần implement `TakeDamage(DamagePayload)`. |
| **Hitbox System** | `HitboxIdentifier` | `../Assets/_Project/Scripts/Combat/HitboxIdentifier.cs` | Nhận và chuyển tiếp `DamagePayload`, gán đúng `HitboxType` trước khi forward về IDamageable | **Dễ:** Gắn collider con trên xương của prefab độc lập. |
| **Hitbox Data** | `HitboxType` | `../Assets/_Project/Scripts/Combat/HitboxType.cs` | Enum các vùng nhận sát thương (Head, Torso, Limb) | **Dễ:** Thêm vùng mới (Weakpoint, Core). |
| **Health & Defense State** | `Health` | `../Assets/_Project/Scripts/Combat/Health.cs` | Quản lý máu, tính toán giảm trừ giáp mũ $e^{-0.01053605 \times \text{Armor}}$, đếm ngược Downed | **Dễ:** Đọc giáp từ PlayerStats nếu là người chơi, tính toán tự động khuếch đại khi giáp âm. |
| **Weapon Static Config** | `WeaponData` | `../Assets/_Project/Scripts/Combat/WeaponData.cs` | ScriptableObject định nghĩa thông số vũ khí (Súng & Cận chiến: APS, Xuyên giáp, Crit, Tầm quét) | **Dễ:** Tạo file `.asset` trong Editor độc lập. |
| **Weapon Controller** | `WeaponController` | `../Assets/_Project/Scripts/Combat/WeaponController.cs` | Quản lý danh sách `RuntimeWeapon`, bắn Hitscan và chém cận chiến `OverlapSphereNonAlloc` | **Dễ:** Tách chỉ số ra `RuntimeWeapon`, logic chiến đấu đạt Zero-GC. |
| **Player FPS Movement** | `PlayerMovement` | `../Assets/_Project/Scripts/Player/PlayerMovement.cs` | Điều khiển di chuyển CharacterController, nhân hệ số `MoveSpeedMultiplier` từ `PlayerStats` | **Dễ:** Tốc độ chạy tự động điều chỉnh theo buff từ tế đàn. |
| **Co-op Player Revive** | `PlayerRevive` | `../Assets/_Project/Scripts/Player/PlayerRevive.cs` | Quản lý việc cứu đồng đội bị gục (Downed) khi giữ [E] trong phạm vi 2m | **Trung bình:** ServerRpc kiểm tra khoảng cách chặt chẽ. |
| **AI State Definition** | `EnemyType` | `../Assets/_Project/Scripts/AI/EnemyType.cs` | Khai báo enum chủng loại kẻ địch (`EnemyType`) và trạng thái (`EnemyState`) | **Trung bình:** Enum định danh phân loại. |
| **AI Base FSM** | `EnemyStateMachine` | `../Assets/_Project/Scripts/AI/EnemyStateMachine.cs` | FSM cơ sở (Idle, Chase, Attack, Dead), đóng gói `DamagePayload` khi tấn công | **Dễ:** Tấn công thông qua `DamagePayload` chuẩn hóa. |
| **AI Heavy Melee (Brute)** | `BruteAI` | `../Assets/_Project/Scripts/AI/BruteAI.cs` | Quái quỷ khổng lồ 120 HP, đòn đánh 25 DMG có xuyên giáp 15 | **Dễ:** Kế thừa sạch sẽ, override `ExecuteAttack`. |
| **AI Ranged (Archer)** | `HellspawnArcherAI` | `../Assets/_Project/Scripts/AI/HellspawnArcherAI.cs` | Cung thủ tầm xa 25 HP, hitscan 10-20m, bắn đóng gói `DamagePayload` | **Dễ:** Hitscan raycast độc lập trên Server. |
| **AI Boss (Hellfire Lord)** | `BossStateMachine` | `../Assets/_Project/Scripts/AI/BossStateMachine.cs` | Boss 800 HP, 2 Phase, AOE swipe/fireball đóng gói `DamagePayload` có xuyên giáp | **Trung bình:** Quản lý phase 2 và triệu hồi quái đệ tử. |
| **Test Dummy Enemy** | `TestEnemyImp` | `../Assets/_Project/Scripts/Enemy/TestEnemyImp.cs` | Quái Imp kiểm thử hiển thị thanh máu overhead | **Dễ:** Script testbed độc lập. |
| **Dungeon Topology & BFS** | `DungeonGrid` | `../Assets/_Project/Scripts/Dungeon/DungeonGrid.cs` | Cấu trúc dữ liệu đồ thị phòng 2D và thuật toán BFS tìm Boss Room | **Dễ:** Pure C# data structure. |
| **Dungeon Room Enums** | `DungeonRoomType` | `../Assets/_Project/Scripts/Dungeon/DungeonRoomType.cs` | Enum định nghĩa loại phòng (Spawn, Normal, Boss) | **Dễ:** Thêm loại phòng mới. |
| **Deterministic Dungeon Gen** | `DungeonGenerator` | `../Assets/_Project/Scripts/Dungeon/DungeonGenerator.cs` | Sinh sơ đồ ngục ngẫu nhiên theo Seed qua `System.Random` | **Trung bình:** Dựng hình học phòng bằng code. |
| **Runtime NavMesh** | `RuntimeNavMeshBaker` | `../Assets/_Project/Scripts/Dungeon/RuntimeNavMeshBaker.cs` | Bake NavMeshSurface tại runtime sau khi hoàn tất dựng hình học | **Dễ:** Đóng gói độc lập. |
| **Dungeon Spawner** | `DungeonEnemySpawner` | `../Assets/_Project/Scripts/Dungeon/DungeonEnemySpawner.cs` | Điều phối sinh quái vật ngẫu nhiên vào các phòng Normal và Boss Room | **Trung bình:** Sinh quái vật Server-authoritative. |
| **Dungeon Coordinator** | `DungeonManager` | `../Assets/_Project/Scripts/Dungeon/DungeonManager.cs` | Quản lý Seed qua `NetworkVariable`, điều phối luồng dựng map | **Trung bình:** Singleton trung tâm điều phối. |
| **Game Flow & Rules** | `GameManager` | `../Assets/_Project/Scripts/Networking/GameManager.cs` | Quản lý vòng lặp trạng thái game (Lobby, Playing, BossFight, v.v.) | **Dễ:** Quản lý state tập trung. |
| **LAN UDP Discovery** | `LanDiscovery` | `../Assets/_Project/Scripts/Networking/LanDiscovery.cs` | Gửi và nhận gói tin UDP Broadcast cổng 47777 tìm phòng trong mạng LAN | **Dễ:** Dùng thread nền và `ConcurrentQueue`. |
| **Network Lifecycle** | `NetworkConnectManager` | `../Assets/_Project/Scripts/Networking/NetworkConnectManager.cs` | Quản lý vòng đời Host/Client, Connection Approval chặn người thứ 5 | **Dễ:** Quản lý tập trung UTP transport. |
| **HUD & Combat UI** | `GameplayHUD` | `../Assets/_Project/Scripts/UI/GameplayHUD.cs` | Hiển thị máu (làm tròn số nguyên), đạn, tâm ngắm, hitmarker, Pause Menu | **Dễ:** Lắng nghe event từ Health và WeaponController. |
| **Lobby UI** | `LobbyUI` | `../Assets/_Project/Scripts/UI/LobbyUI.cs` | Hiển thị 4 slot người chơi trong phòng chờ, nút Host Start Game | **Dễ:** Đồng bộ qua NetworkManager. |
| **Main Menu UI** | `MainMenuUI` | `../Assets/_Project/Scripts/UI/MainMenuUI.cs` | Giao diện điều hướng Menu chính, Host, Join IP, Quét LAN, Cài đặt | **Trung bình:** Quản lý 6 panel UI. |

---

## 3. Bản Đồ Phụ Thuộc & Phân Tích Lan Truyền (Dependency & Impact Matrix)

### 3.1. `Health.cs`
- **Đường dẫn:** `Assets/_Project/Scripts/Combat/Health.cs`
- **Các thành phần phụ thuộc vào nó (Inbound Dependencies):**
  - `PlayerStats.cs` (Gọi `IncreaseMaxHealthServer()` khi người chơi nhận buff máu từ Tế đàn).
  - `WeaponController.cs` (Kiểm tra trạng thái Downed/Dead của người tấn công).
  - `PlayerMovement.cs` (Vô hiệu hóa di chuyển khi Downed/Dead).
  - `PlayerRevive.cs` (Quét tìm teammate Downed, gọi `ServerRevive()`).
  - `EnemyStateMachine.cs` & AI derived classes (Gây sát thương qua `DamagePayload`, lắng nghe `OnDied`).
  - `GameplayHUD.cs` (Lắng nghe sự kiện `OnHealthChanged`, `OnDamageTaken`).
- **Các thành phần nó phụ thuộc vào (Outbound Dependencies):**
  - `IDamageable.cs` (Implement interface này qua hàm `TakeDamage(DamagePayload)`).
  - `DamagePayload.cs` (Nhận struct sát thương Zero-GC).
  - `PlayerStats.cs` (Đọc chỉ số `playerStats.Armor.Value` khi mục tiêu là người chơi).
  - Unity NGO (`NetworkBehaviour`, `NetworkVariable<float>`, `[ClientRpc]`).
- **Rủi ro dây chuyền (Ripple Effect):**
  - **Code files bắt buộc phải đồng bộ:** `IDamageable.cs`, `HitboxIdentifier.cs`, `PlayerStats.cs`, `WeaponController.cs`, toàn bộ AI scripts.
  - **Quy tắc công thức giáp:** Luôn đảm bảo `EffectiveArmor = TargetArmor - ArmorPenetration` và hệ số giảm thương $e^{-0.01053605 \times \text{EffectiveArmor}}$ tính đúng trên Server để tránh Network Desync.

---

### 3.2. `PlayerStats.cs`
- **Đường dẫn:** `Assets/_Project/Scripts/Player/PlayerStats.cs`
- **Các thành phần phụ thuộc vào nó (Inbound Dependencies):**
  - `Health.cs` (Đọc `Armor.Value` để tính giảm trừ sát thương nhận vào).
  - `PlayerMovement.cs` (Đọc `MoveSpeedMultiplier.Value` để nhân vào vận tốc di chuyển).
  - `InteractiveShrine.cs` (Kiểm tra `HasUsedShrine(id)`, gọi `TryApplyBodyBuff()` và `RecordShrineUsed(id)`).
  - `GameplayHUD.cs` (Lắng nghe `OnStatsChanged` để cập nhật HUD hiển thị giáp/tốc độ nếu cần).
- **Các thành phần nó phụ thuộc vào (Outbound Dependencies):**
  - `Health.cs` (Gọi `IncreaseMaxHealthServer()` khi buff máu tối đa).
  - Unity NGO (`NetworkBehaviour`, `NetworkVariable<float>`).
- **Rủi ro dây chuyền (Ripple Effect):**
  - **Code files liên đới:** `PlayerMovement.cs`, `Health.cs`, `InteractiveShrine.cs`.
  - **Prefab references:** Bắt buộc phải gắn component `PlayerStats` lên `Player.prefab` cùng cấp với `Health` và `PlayerMovement`.

---

### 3.3. `WeaponController.cs` & `RuntimeWeapon.cs`
- **Đường dẫn:** `Assets/_Project/Scripts/Combat/WeaponController.cs` & `RuntimeWeapon.cs`
- **Các thành phần phụ thuộc vào nó (Inbound Dependencies):**
  - `InteractiveShrine.cs` (Gọi `UpgradeCurrentWeaponServer()` khi tế đàn là loại WeaponUpgrade).
  - `GameplayHUD.cs` (Lắng nghe `OnAmmoChanged`, `OnWeaponChanged`, `OnWeaponUpgraded`).
- **Các thành phần nó phụ thuộc vào (Outbound Dependencies):**
  - `RuntimeWeapon.cs` (Đóng gói dữ liệu runtime, tính toán final stats và crit roll).
  - `WeaponData.cs` (ScriptableObject tĩnh).
  - `IWeaponModifier.cs` (Interface can thiệp chỉ số và hiệu ứng OnHit).
  - `DamagePayload.cs` (Đóng gói sát thương gửi sang mục tiêu).
  - `HitboxIdentifier.cs` & `IDamageable.cs` (Gọi `ForwardDamage(DamagePayload)` hoặc `TakeDamage(DamagePayload)`).
- **Rủi ro dây chuyền (Ripple Effect):**
  - **Code files liên đới:** `GameplayHUD.cs`, `InteractiveShrine.cs`.
  - **Prefab references:** Kiểm tra danh sách `_availableWeapons` trong `Player.prefab`.

---

### 3.4. `InteractiveShrine.cs`
- **Đường dẫn:** `Assets/_Project/Scripts/Items/InteractiveShrine.cs`
- **Các thành phần phụ thuộc vào nó (Inbound Dependencies):**
  - Không có script nào phụ thuộc trực tiếp vào nó (Là thực thể tương tác độc lập trong thế giới).
- **Các thành phần nó phụ thuộc vào (Outbound Dependencies):**
  - `PlayerStats.cs` (Kiểm tra lịch sử và cấp buff thân thể).
  - `WeaponController.cs` (Cấp buff nâng cấp súng).
  - Unity NGO (`NetworkBehaviour`, `[ServerRpc]`, `[ClientRpc]`).
- **Rủi ro dây chuyền (Ripple Effect):**
  - **Scene / Level Design:** Khi đặt nhiều Tế đàn trong Scene/Prefab phòng hầm ngục, **mỗi Tế đàn bắt buộc phải được gán một `_shrineId` khác nhau** trong Inspector (ví dụ: Shrine 1, Shrine 2, Shrine 3). Nếu trùng ID, người chơi dùng 1 tế đàn sẽ bị tính là đã dùng toàn bộ các tế đàn trùng ID đó.

---

## 4. Báo Cáo "Code Smell" & Đánh Giá Tái Cấu Trúc (Extensibility Progress)

### 4.1. Các Điểm Nghẽn Đã Được Giải Quyết Triệt Để (Resolved)
1. **[ĐÃ GIẢI QUYẾT] Vi Phạm Open/Closed Principle Trên Hệ Thống Vũ Khí:**
   - *Trước đây:* Mọi chỉ số bị đóng cứng trong `WeaponData` và `WeaponController`, không thể thêm ngọc hoặc phụ kiện.
   - *Hiện tại:* Đã tách ra `RuntimeWeapon` và cung cấp interface mở rộng `IWeaponModifier`. Bất kỳ lập trình viên nào muốn thêm hiệu ứng (đốt cháy, hút máu, xuyên giáp bổ sung) chỉ cần tạo class implement `IWeaponModifier` và gọi `runtimeWeapon.AddModifier(mod)`.
2. **[ĐÃ GIẢI QUYẾT] Cấp Phát Rác Bộ Nhớ (Zero-GC Optimization):**
   - *Trước đây:* Tạo mới `new Material(...)` mỗi lần bắn raycast/tracer và gọi `GameObject.CreatePrimitive` runtime.
   - *Hiện tại:* 
     - Gói sát thương chuyển thành `readonly struct DamagePayload` nhẹ, không cấp phát heap.
     - Cận chiến dùng mảng tĩnh `Physics.OverlapSphereNonAlloc` với bộ đệm `_meleeHitBuffer[32]`.
     - Toàn bộ material FX dùng chung qua phương thức cache `GetOrCreateFxMaterial()`.
3. **[ĐÃ GIẢI QUYẾT] Phân Tách Trách Nhiệm Chỉ Số Thân Thể & Tấn Công:**
   - *Trước đây:* Nhân vật không có quản lý giáp hay tốc chạy rõ ràng, thiếu trần cứng.
   - *Hiện tại:* `PlayerStats` quản lý độc lập Máu (MaxCap 150), Giáp (MaxCap 60), Tốc độ (MaxCap 1.8x). Nhân vật không ôm đồm sát thương; vũ khí không ôm đồm máu.
4. **[ĐÃ GIẢI QUYẾT] Logic Tế Đàn Tránh Gian Lận & Đảm Bảo Trải Nghiệm Co-op:**
   - *Hiện tại:* Lưu lịch sử ID tế đàn trực tiếp trên `PlayerStats` ở phía Server. Mỗi người chỉ dùng 1 lần. Hiệu ứng tắt lửa được cô lập cục bộ cho riêng máy người đó thông qua `ClientRpcParams`, đồng đội chưa dùng vẫn thấy lửa cháy bình thường.

### 4.2. Các Điểm Nghẽn Còn Lại Cần Lưu Ý Trong Tương Lai
1. **Switch-case trên FSM Quái vật:**
   - `EnemyStateMachine.cs` vẫn dùng switch trên enum `EnemyState`. Khi mở rộng AI quái vật phức tạp ở Giai đoạn sau, khuyến nghị chuyển sang Hierarchical State Machine (HSM) hoặc Behavior Tree.
2. **Quét tìm đồng đội bằng `FindObjectsByType`:**
   - `PlayerRevive.cs` vẫn gọi `FindObjectsByType<Health>` để tìm đồng đội Downed. Khuyến nghị đăng ký danh sách người chơi tĩnh qua `PlayerRegistry` để tối ưu hóa CPU.

---

## 5. Bảng Hướng Dẫn Thao Tác Nhanh (Actionable Playbook)

| Nhu cầu phát triển | File cần sửa / thêm mới | Những file/asset khác bắt buộc phải đồng bộ theo |
|---|---|---|
| **Thêm 1 loại Modifier / Ngọc vũ khí mới** | 1. Tạo script mới (ví dụ `FireGemModifier.cs`) cài đặt interface `IWeaponModifier`.<br>2. Cài đặt hàm `ModifyStats(...)` (ví dụ: `damage += 10f`, `critChance += 0.05f`) và `OnHit(...)` (ví dụ: gây hiệu ứng đốt cháy). | 1. Khi nhặt ngọc hoặc mua ngọc từ Shop, gọi `weaponController.CurrentRuntimeWeapon.AddModifier(new FireGemModifier())`.<br>2. Không cần sửa bất kỳ dòng code nào trong `WeaponController` hay `WeaponData` (Chuẩn OCP). |
| **Cấu hình 1 Tế đàn mới trong Map/Dungeon** | 1. Kéo prefab Tế đàn vào Scene/Phòng hầm ngục.<br>2. Trên component `InteractiveShrine`, gán `_shrineId` một số nguyên duy nhất (không trùng với tế đàn khác trong màn chơi).<br>3. Chọn `_shrineType`: `BodyBuff` (tăng máu/giáp/tốc độ), `WeaponUpgrade` (tăng cấp súng), hoặc `DualChoice`. | 1. Kéo thả các tham chiếu hiển thị: `_flameVisual` (GameObject ngọn lửa), `_shrineLight` (nguồn sáng), `_shrineRenderer` (mesh tế đàn).<br>2. Đảm bảo Collider của Tế đàn được bật cờ `isTrigger = true` để người chơi bước vào cự ly 3m bấm phím tương tác. |
| **Thêm 1 loại vũ khí Cận chiến mới (Melee)** | 1. Tạo asset `Machete_Data.asset` từ menu `Hellfire/Combat/Weapon Data`.<br>2. Đặt `WeaponType = MeleeWeapon`.<br>3. Cấu hình `BaseDamage`, `AttackRateAPS` (ví dụ 2.5 APS), `ArmorPenetration`, `MeleeRange` (ví dụ 2.8m), `MeleeSwingAngle` (ví dụ 80 độ). | 1. Mở `Player.prefab`, kéo `Machete_Data.asset` vào danh sách `_availableWeapons` của `WeaponController`.<br>2. Đảm bảo model vũ khí viewmodel được gán đầy đủ. |
| **Điều chỉnh công thức giáp hoặc trần chỉ số** | 1. Để chỉnh công thức giảm thương: Sửa tại [`Health.cs:207`](file:///F:/LTG/Hellfire/Assets/_Project/Scripts/Combat/Health.cs#L207) (`damageFactor = Mathf.Exp(-0.01053605f * effectiveArmor)`).<br>2. Để chỉnh trần chỉ số người chơi: Sửa các hằng số tại [`PlayerStats.cs:20-23`](file:///F:/LTG/Hellfire/Assets/_Project/Scripts/Player/PlayerStats.cs#L20-L23) (`MaxBonusHealthCap`, `MaxArmorCap`, `MaxSpeedMultiplierCap`). | 1. Không cần sửa UI HUD vì HUD đọc tự động qua NetworkVariable và `MaxHealthNet`. |
| **Thêm 1 loại đạn bay vật lý (Projectile Rocket)** | 1. Thêm cờ hoặc loại hình vào `WeaponData`.<br>2. Trong `WeaponController.cs`, tại nhánh bắn của súng, kiểm tra nếu là Projectile thì Instantiate `RocketPrefab` có gắn `NetworkObject`. | 1. Đăng ký prefab đạn vào `NetworkPrefabsList` trong `NetworkManager.prefab`.<br>2. Đạn khi va chạm sẽ đóng gói `DamagePayload` gửi vào `hitbox.ForwardDamage(payload)`. |

---

## 6. Tổng Kết Kiến Trúc Cho Lập Trình Viên

1. **Khả năng mở rộng (Extensibility):** Hệ thống đạt mức độ phân tách cao (Decoupled). Việc tách bạch `PlayerStats` (chỉ số sinh tồn/cơ động) và `RuntimeWeapon` (chỉ số tấn công/khe cắm modifier) giúp việc cân bằng game độc lập và dễ dàng.
2. **Quy tắc vàng khi truyền sát thương:** **Luôn luôn sử dụng `DamagePayload`**. Tuyệt đối không truyền sát thương thô dạng số thực rời rạc để đảm bảo tính toàn vẹn của chỉ số xuyên giáp, tính toán giảm trừ giáp mũ và tính toán Hitbox trên Server.
3. **Quy tắc vàng khi làm việc với Tế đàn:** Luôn gán `ShrineId` duy nhất cho từng Tế đàn trên bản đồ để hệ thống theo dõi lịch sử người chơi hoạt động chính xác và không bị xung đột trải nghiệm giữa các thành viên trong đội Co-op.
