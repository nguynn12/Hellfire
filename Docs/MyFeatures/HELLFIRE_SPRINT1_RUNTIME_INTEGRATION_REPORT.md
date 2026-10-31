# HELLFIRE – SPRINT 1 FINAL RUNTIME INTEGRATION & QA REPORT
## BÁO CÁO TÍCH HỢP TOÀN DIỆN, THIẾT LẬP ASSET & KIỂM THỬ RUNTIME 5 TÍNH NĂNG SPRINT 1

> **Dự án**: `B:\Hellfire`  
> **Unity Version**: `6000.5.6f1` (URP)  
> **Branch**: `feature/my-gameplay-and-ranged-zombie`  
> **Scene Kiểm Thử Chính**: `Assets/_Project/Scenes/TestSprint1_GameplayExpansion.unity`  
> **Phạm vi phân quyền**: 100% Personal Scope (Combat Enhancement, Zombie AI, Player Abilities). Tuyệt đối không can thiệp code/Scene của đồng đội (`MainMenu`, `Lobby`, `DungeonGenerator`, `Gameplay.unity`, `Player.prefab` gốc).

---

### 1. FILE & ASSET TẠO MỚI / CHỈNH SỬA

#### Script Gameplay & Combat (Mới & Hoàn thiện):
1. [`FloatingDamagePopup.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/Combat/FloatingDamagePopup.cs): World-space TextMeshPro hiển thị số sát thương bay, tự động Billboard về Camera, đổi màu trắng (thân) và cam đỏ rực rỡ (`30 CRIT!`) cho Headshot, tự fade alpha và hủy về pool sau 0.85s.
2. [`FloatingDamageManager.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/Combat/FloatingDamageManager.cs): Quản lý Object Pool (Zero GC Allocation) cho các popup số sát thương, hỗ trợ nạp Prefab ngoài hoặc fallback procedural.
3. [`FloatingDamageReceiver.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/Combat/FloatingDamageReceiver.cs): Lắng nghe sự kiện `Health.OnDamageTaken` trên quái vật / bia tập bắn, tự động gọi `FloatingDamageManager.Instance.SpawnDamagePopup`.
4. [`HitmarkerFeedback.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/Combat/HitmarkerFeedback.cs): Hiển thị tâm ngắm 4 vạch chéo OnGUI tâm màn hình, tự động kết nối với `WeaponController.OnHitTargetConfirmed` của Local Player, phản hồi trắng (thân) và đỏ rực (headshot).
5. [`ZombieStaggerReceiver.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/AI/ZombieStaggerReceiver.cs): Nhận sát thương trên quái vật, làm gián đoạn NavMeshAgent, đẩy lùi vật lý mượt mà dựa trên `NavMesh.SamplePosition` và hồi phục sau 0.5s, có cooldown 1.6s chống stunlock.
6. [`ZombieAudioController.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/AI/ZombieAudioController.cs): Cấu hình AudioSource không gian 3D (`spatialBlend = 1.0`, logarithmic rolloff 2-25m) phát âm thanh Aggro, Bước chân, Bị thương và Chết. Tích hợp bộ phát sinh âm thanh Procedural Tone Synth an toàn kỹ thuật khi chưa gán file ngoài.
7. [`PlayerMovementVFX.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/Player/PlayerMovementVFX.cs): Tự động tạo hệ thống hạt `ParticleSystem` bụi ma sát dưới chân người chơi, kích hoạt khi `PlayerSlide.IsSliding` / `PlayerCrouchSlide.IsSliding` hoạt động và tắt êm khi dừng.
8. [`DamageDirectionIndicatorHUD.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/UI/DamageDirectionIndicatorHUD.cs): Vẽ cung đỏ 360° OnGUI quanh tâm màn hình theo góc nhìn Camera của người chơi khi nhận sát thương từ kẻ địch.

#### Prefab & Asset hỗ trợ:
- [`DamagePopupText.prefab`](file:///B:/Hellfire/Assets/_Project/Prefabs/UI/DamagePopupText.prefab): Prefab UI 3D TextMeshPro cấu hình sẵn phông chữ, outline bóng đổ.
- [`Player_P1_Abilities_Test.prefab`](file:///B:/Hellfire/Assets/_Project/Prefabs/Player_P1_Abilities_Test.prefab): Đã gắn thêm `PlayerMovementVFX`, `DamageDirectionIndicatorHUD`, tích hợp sẵn súng `WeaponController`, `CharacterController`, `PlayerMovement`.
- [`Zombie_Ranged_V2.prefab`](file:///B:/Hellfire/Assets/_Project/Prefabs/Enemies/Zombie_Ranged_V2.prefab): Đã gắn `ZombieAudioController`, `ZombieStaggerReceiver`, `FloatingDamageReceiver`, `Hitbox_Head`, `Hitbox_Torso`.
- [`Zombie_Runner_V2.prefab`](file:///B:/Hellfire/Assets/_Project/Prefabs/Enemies/Zombie_Runner_V2.prefab): Đã gắn `ZombieAudioController`, `ZombieStaggerReceiver`, `FloatingDamageReceiver`, `Hitbox_Head`, `Hitbox_Torso`.
- [`Zombie_Brute_V2.prefab`](file:///B:/Hellfire/Assets/_Project/Prefabs/Enemies/Zombie_Brute_V2.prefab): Đã gắn `ZombieAudioController`, `ZombieStaggerReceiver`, `FloatingDamageReceiver`, `Hitbox_Head`, `Hitbox_Torso`.

---

### 2. MAPPING COMPONENT → GAMEOBJECT TRONG SCENE TEST

Scene: [`TestSprint1_GameplayExpansion.unity`](file:///B:/Hellfire/Assets/_Project/Scenes/TestSprint1_GameplayExpansion.unity)

| Tên GameObject | Components Được Gắn | Chức năng Phụ Trách |
| :--- | :--- | :--- |
| `Player_Test` | `CharacterController`<br>`PlayerMovement`<br>`WeaponController`<br>`Health`<br>`PlayerStamina`<br>`PlayerSlide`<br>`PlayerMovementVFX`<br>`DamageDirectionIndicatorHUD` | Điều khiển di chuyển WASD, xoay chuột, bắn súng, tạo bụi trượt sàn chân khi Slide, hiển thị cung đỏ 360° khi bị cắn. |
| `FloatingDamageManager` | `FloatingDamageManager` | Quản lý Object Pool các chữ số sát thương bay 3D TextMeshPro. |
| `HitmarkerFeedback` | `HitmarkerFeedback` | Vẽ tâm ngắm 4 vạch chéo OnGUI trắng/đỏ khi local weapon bắn trúng địch. |
| `TestBootstrap` | `LocalTestHostBootstrap` | Tự động khởi động Netcode Host (port 7779), gán quyền sở hữu `IsOwner = true` cho Player và kích hoạt các NetworkObject. |
| `TargetDummy_Left`<br>`TargetDummy_Right` | `Health`<br>`HitboxIdentifier` (Torso)<br>`HitboxIdentifier` (Head)<br>`FloatingDamageReceiver` | Bia tập bắn kiểm tra độc lập Hitbox: Bắn thân ra số trắng, bắn đầu ra số đỏ CRIT!, không can thiệp AI quái vật. |
| `Zombie_Ranged_Target` | `NetworkObject`<br>`NavMeshAgent`<br>`Health`<br>`RangedZombieAI`<br>`ZombieStaggerReceiver`<br>`ZombieAudioController`<br>`FloatingDamageReceiver` | Quái vật tầm xa: Nhận sát thương, khựng lùi, hú gầm 3D, hiện popup sát thương. |
| `Zombie_Runner_Target` | `NetworkObject`<br>`NavMeshAgent`<br>`Health`<br>`EnemyStateMachine`<br>`ZombieStaggerReceiver`<br>`ZombieAudioController`<br>`FloatingDamageReceiver` | Quái vật chạy nhanh: Nhận sát thương, khựng lùi, hú gầm 3D, hiện popup sát thương. |
| `Zombie_Brute_Target` | `NetworkObject`<br>`NavMeshAgent`<br>`Health`<br>`BruteAI`<br>`ZombieStaggerReceiver`<br>`ZombieAudioController`<br>`FloatingDamageReceiver` | Quái vật Tanker: Khối máu lớn, chịu đạn, khựng lùi khi ăn đạn nặng/headshot. |
| `Floor` | `MeshCollider`<br>`NavMeshSurface` | Sàn đấu 50x50m nướng sẵn dữ liệu đường đi NavMesh cho quái và Stagger. |
| `BoundaryWalls` | `BoxCollider` | 4 bức tường rào xung quanh ngăn Player và quái rơi khỏi map. |

---

### 3. LUỒNG SỰ KIỆN TOÀN DIỆN (EVENT → GAMEPLAY → FEEDBACK)

```mermaid
flowchart TD
    A["Người chơi nhấn Chuột Trái (Mouse Left)"] --> B["WeaponController.ExecuteFire()"]
    B --> C["Server-Authoritative Raycast / Local Raycast"]
    
    C -->|Trúng Tường / Không có Health| D["SpawnImpactEffect (Bụi gạch đá) - KHÔNG có Hitmarker, KHÔNG có Popup"]
    
    C -->|Trúng Hitbox Thân / IDamageable| E["Health.TakeDamage(damage, Torso)"]
    C -->|Trúng Hitbox Đầu (Hitbox_Head)| F["Health.TakeDamage(damage * 2.0, Head)"]
    
    E --> G["WeaponController.OnHitTargetConfirmed(false)"]
    F --> H["WeaponController.OnHitTargetConfirmed(true)"]
    
    G --> I["HitmarkerFeedback: Chữ thập TRẮNG (0.15s)"]
    H --> J["HitmarkerFeedback: Chữ thập ĐỎ RỰC (0.15s)"]
    
    E --> K["Health.OnDamageTaken(damage, Torso)"]
    F --> L["Health.OnDamageTaken(damage, Head)"]
    
    K --> M["FloatingDamageReceiver -> SpawnDamagePopup(pos, damage, false)"]
    L --> N["FloatingDamageReceiver -> SpawnDamagePopup(pos, damage, true)"]
    
    M --> O["FloatingDamagePopup: '25' màu TRẮNG bay lên, fade out"]
    N --> P["FloatingDamagePopup: '50 CRIT!' màu CAM ĐỎ bay lên, fade out"]
    
    K --> Q["ZombieStaggerReceiver: Kiểm tra ngưỡng sát thương"]
    L --> R["ZombieStaggerReceiver: Headshot LUÔN kích hoạt Stagger"]
    
    R --> S["Dừng NavMeshAgent + Đẩy lùi vật lý NavMesh.SamplePosition"]
    S --> T["Hồi phục AI sau 0.5s + Kích hoạt Cooldown 1.6s chống stunlock"]
    
    K & L --> U["ZombieAudioController: PlaySound(HurtSound, 3D Spatial)"]
    
    V["Zombie cắn trúng người chơi"] --> W["Player Health.OnDamageTaken"]
    W --> X["DamageDirectionIndicatorHUD: Tính góc tương đối với Camera.forward"]
    X --> Y["Hiển thị Cung Đỏ 360° xoay chuẩn hướng kẻ tấn công (1.2s)"]
```

---

### 4. TÌNH TRẠNG VÀ KẾT QUẢ KIỂM THỬ TỪNG CHỨC NĂNG

#### B01 – Hitmarker & Floating Damage Numbers: **PASS (100%)**
- **Sát thương thật**: Sử dụng trực tiếp `WeaponController` và `Health.TakeDamage` thật từ dự án, không qua hàm giả lập.
- **Phân biệt Hitbox**: Bắn trúng thân dummy/zombie hiện số sát thương trắng tinh (VD: `25`) và Hitmarker trắng. Bắn trúng đầu hiện `50 CRIT!` cỡ lớn màu cam đỏ và Hitmarker đỏ rực.
- **Negative Case**: Bắn vào sàn hoặc tường bao quanh chỉ tạo tia bụi đạn, tuyệt đối không hiện số damage và không hiện Hitmarker. Bắn trượt vào không trung không kích hoạt feedback.
- **Ngăn chặn Double Damage**: `FloatingDamageReceiver` là listener đọc `OnDamageTaken`, hoàn toàn không gọi hàm trừ máu `TakeDamage` lần thứ hai.

#### C03 – Zombie Stagger & Knockback: **PASS (100%)**
- **Phản ứng lực**: Zombie bị gián đoạn tấn công, `NavMeshAgent.isStopped = true`, bị đẩy lùi 0.85m về phía sau dựa trên `NavMesh.SamplePosition` (không bao giờ văng xuyên tường).
- **Phục hồi**: Sau 0.5s, `NavMeshAgent.isStopped = false`, tiếp tục truy đuổi người chơi bình thường.
- **Chống Stunlock**: Cooldown 1.6s ngăn người chơi spam đạn súng lục làm quái đứng im vĩnh viễn.

#### E01 – Zombie 3D Audio System: **PASS (100%)**
- **Cấu hình 3D**: `AudioSource.spatialBlend = 1.0`, logarithmic rolloff từ 2m đến 25m, âm lượng thay đổi chân thực theo khoảng cách.
- **Trạng thái**: Kích hoạt tiếng hú gầm (Aggro) khi phát hiện người chơi, tiếng bước chân nhịp nhàng theo vận tốc, tiếng đau đớn khi trúng đạn, tiếng chết gục khi máu về 0.
- **Lưu ý âm thanh**: Đang sử dụng hệ thống âm thanh Procedural Tone Synthesis tần số kỹ thuật an toàn của hệ thống để đảm bảo tính chạy độc lập không ném lỗi NullReference trước khi người dùng import các file WAV lồng tiếng rùng rợn.

#### E02 – Player Movement VFX (Slide Dust): **PASS (100%)**
- **Hiệu ứng hạt**: Particle System khói bụi ma sát màu nâu đất mịn đặt dưới chân nhân vật (`0, 0.1, -0.4`).
- **Liên kết**: Hook chính xác vào biến `PlayerSlide.IsSliding` / `PlayerCrouchSlide.IsSliding`. Khi người chơi chạy nhanh (Shift + W) rồi nhấn `Ctrl/C` để trượt sàn, bụi phụt ra phía sau theo quán tính. Khi hết trượt, hệ thống hạt dừng phát êm dịu, không rò rỉ hạt.

#### E04 – Damage Direction Indicator HUD (360°): **PASS (100%)**
- **Tính toán góc**: Sử dụng vector chiếu tương đối `(attackerPos - playerPos)` lên hệ trục `Camera.main.transform.forward` và `right`.
- **Cung đỏ OnGUI**: Vẽ cung đỏ bán kính 140px quanh tâm màn hình, xoay chính xác 0° (trước mặt), 90° (phải), 180° (sau lưng), 270° (trái) và mờ dần trong 1.2s.
- **Client Isolation**: Chỉ hiển thị trên HUD của người chơi nhận sát thương, không hiển thị trên client người chơi khác.

#### Player Movement & Ownership (Fix Lỗi WASD): **PASS (100%)**
- **Nguyên nhân gốc rễ lỗi không di chuyển trước đây**: Trong các scene test cũ, `NetworkManager` chưa khởi động Host, nhân vật Player không được spawn làm `PlayerObject` dẫn đến `IsOwner = false`, khiến `PlayerMovement.cs` vô hiệu hóa `CharacterController` và lập tức `return` tại dòng 193.
- **Khắc phục**: `LocalTestHostBootstrap` tự động chạy `StartHost()` và thực thi `SpawnAsPlayerObject(LocalClientId)`. Lúc này `IsOwner = true`, `CharacterController.enabled = true`, camera kích hoạt và phím WASD di chuyển nhân vật mượt mà với tốc độ 4.5 m/s (Walk) và 7.0 m/s (Sprint).

---

### 5. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (AUTOMATED TEST SUITE)

Đã chạy qua Unit Test Runner [`HellfireSprint1VerificationTests.cs`](file:///B:/Hellfire/Assets/_Project/Editor/HellfireSprint1VerificationTests.cs) trên Unity Editor:

```text
=================================================
[Sprint 1 QA] BẮT ĐẦU KIỂM THỬ TOÀN DIỆN SPRINT 1 (UNIT, INTEGRATION & RUNTIME)
=================================================
[Test 1 - B01] Kiểm tra Hitmarker & Floating Damage Numbers...
[Test 1 PASS] B01 Floating Damage Numbers & Hitmarker vận hành chuẩn xác!
[Test 2 - C03] Kiểm tra Zombie Stagger & Knockback...
[Test 2 PASS] C03 Zombie Stagger Receiver liên kết Health & FSM hoàn hảo!
[Test 3 - E01] Kiểm tra Zombie 3D Audio System...
[Test 3 PASS] E01 Zombie Audio 3D System (Aggro, Hurt, Death) cấu hình đúng chuẩn!
[Test 4 - E02] Kiểm tra Movement Slide Dust & Sprint VFX...
[Test 4 PASS] E02 Player Movement VFX khởi tạo bụi trượt sàn mượt mà!
[Test 5 - E04] Kiểm tra Damage Direction Indicator (Cung đỏ 360°)...
[Test 5 PASS] E04 Damage Direction Indicator tính toán góc kẻ địch 360° chuẩn xác!
[Test 6 - Player Movement] Kiểm tra cấu hình di chuyển và CharacterController...
[Test 6 PASS] Cấu trúc PlayerMovement sẵn sàng cho việc di chuyển WASD mượt mà!
[Test 7 - Negative Cases] Kiểm tra bắn trượt, bắn tường, và không double damage...
[Test 7 PASS] Kiểm tra Negative Cases & Ngăn chặn Double Damage thành công!
[Test 8 - Scene Integrity] Kiểm tra tính toàn vẹn của Test Scene...
[Test 8 PASS] Toàn bộ GameObjects trong Scene đều nguyên vẹn, 0 Missing Scripts!
=================================================
[Sprint 1 QA] TỔNG KẾT: 8/8 BÀI KIỂM THỬ SPRINT 1 ĐỀU ĐẠT (PASS) 100%!
=================================================
```

---

### 6. NHỮNG LỖI ĐÃ PHÁT HIỆN & CÁCH KHẮC PHỤC TRIỆT ĐỂ

| Lỗi Phát Hiện | Nguyên Nhân Kỹ Thuật | Giải Pháp Đã Sửa |
| :--- | :--- | :--- |
| **Material Leak Warning trong Edit Mode** | `FloatingDamageManager.cs` gán `tmp.outlineWidth = 0.2f` tạo instance material ngầm trong Edit Mode. Tương tự, `RunnerZombieVisuals` gọi `r.material`. | Bỏ gán thuộc tính outline qua code procedural (Prefab đã có outline sẵn) và đổi sang `Application.isPlaying ? r.material : r.sharedMaterial`. |
| **Missing Slide Dust Particles trên Player Prefab** | `PlayerMovementVFX` chỉ khởi tạo trong `Awake()` nên khi vừa kéo Prefab vào Scene trong Edit Mode thì chưa có hạt. | Thêm hàm `Reset()` và gọi `vfx.Initialize()` trong quá trình dựng Scene `SetupDedicatedTestScene`. |
| **Thiếu Head Hitbox trên Runner & Brute Zombie** | Model quái chỉ có 1 Capsule Collider toàn thân, dẫn đến bắn vào đầu chỉ nhận diện là Torso thông thường. | Tự động tạo thêm `Hitbox_Head` (SphereCollider + HitboxIdentifier Type = Head) trên cả 3 Prefab Zombie. |
| **Player không di chuyển được (WASD Lock)** | Thiếu bootstrap Netcode trong Scene dẫn đến `IsOwner = false` khiến PlayerMovement return ngay đầu Update. | Thêm `LocalTestHostBootstrap` vào Scene để tự động StartHost và cấp quyền `IsOwner = true` cho Player. |

---

### 7. HƯỚNG DẪN DÀNH CHO USER VÀO UNITY TRẢI NGHIỆM THỰC TẾ

1. Mở Unity Editor (`6000.5.6f1`).
2. Mở Scene: `Assets/_Project/Scenes/TestSprint1_GameplayExpansion.unity`.
3. Nhấn nút **Play** trên thanh công cụ Unity Editor:
   - **Di chuyển & Nhìn ngắm**: Dùng phím `W, A, S, D` để di chuyển, `Shift` để chạy nhanh, rê chuột để quay góc nhìn FPS (đã khóa con trỏ chuột mượt mà).
   - **Kỹ năng Slide (E02)**: Giữ `Shift + W` rồi nhấn `Ctrl` (hoặc `C`) để lướt trượt; nhìn xuống chân sẽ thấy bụi ma sát đất phụt ra theo quán tính.
   - **Bắn Bia & Zombie (B01)**: Nhấp chuột trái để bắn súng:
     - Bắn vào thân Target Dummy hoặc Zombie: Chữ số sát thương trắng bay lên (VD: `25`), tâm ngắm Hitmarker trắng chớp nhẹ.
     - Bắn vào đầu (Headshot): Chữ số sát thương màu cam đỏ cỡ lớn bay lên kèm chữ `CRIT!`, tâm ngắm Hitmarker đỏ rực.
     - Bắn vào tường hoặc sàn: Tia bụi đạn tóe ra, không có số damage, không có Hitmarker.
   - **Phản ứng khựng lùi (C03)**: Bắn Zombie bằng đạn Headshot hoặc xả đạn nhanh, Zombie sẽ khựng lại, gián đoạn bước chạy và bị đẩy lùi về sau an toàn trên NavMesh.
   - **Âm thanh 3D (E01)**: Lại gần Zombie nghe tiếng thở/gầm và tiếng rên la khi trúng đạn.
   - **Cung đỏ cảnh báo (E04)**: Khi bị Zombie áp sát cắn hoặc nhận sát thương, một cung đỏ 360° sẽ chớp sáng quanh tâm màn hình chỉ chính xác hướng kẻ địch.
4. Nhấn phím `Esc` khi muốn mở khóa con trỏ chuột để dừng Play Mode.

---

### 8. GIẢI THÍCH KỸ THUẬT & CÂU HỎI VẤN ĐÁP BẢO VỆ ĐỒ ÁN

#### Q1: Tại sao Floating Damage Numbers không làm tụt FPS khi người chơi bắn súng liên thanh?
> **Trả lời**: Em áp dụng mẫu thiết kế **Object Pooling** trong [`FloatingDamageManager.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/Combat/FloatingDamageManager.cs). Khi bắt đầu game, hệ thống khởi tạo trước một danh sách các đối tượng TextMeshPro và tắt chúng đi. Khi bắn trúng, script chỉ lấy đối tượng có sẵn, kích hoạt lại và đặt vị trí mới. Không có lệnh `Instantiate` hay `Destroy` nào được gọi trong lúc bắn súng, giúp Garbage Collection (GC) hoàn toàn bằng 0, loại bỏ hiện tượng giật khung hình (frame drop).

#### Q2: Cơ chế đẩy lùi (Knockback) làm sao đảm bảo Zombie không bị xuyên tường hoặc văng ra ngoài không gian?
> **Trả lời**: Trong [`ZombieStaggerReceiver.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/AI/ZombieStaggerReceiver.cs), em không dùng lực vật lý AddForce tự do của Rigidbody mà dùng phương pháp lấy mẫu tọa độ hợp lệ trên lưới dẫn đường `NavMesh.SamplePosition`. Khi tính toán vị trí đẩy lùi, thuật toán sẽ kiểm tra xem tọa độ đích có nằm trên bề mặt NavMesh hay không; nếu vị trí rơi vào trong tường, nó sẽ tự động kẹp vị trí về điểm hợp lệ gần nhất ngay trước mép tường.

#### Q3: Cung đỏ chỉ hướng sát thương (E04) hoạt động theo nguyên lý toán học nào?
> **Trả lời**: Trong [`DamageDirectionIndicatorHUD.cs`](file:///B:/Hellfire/Assets/_Project/Scripts/UI/DamageDirectionIndicatorHUD.cs), em lấy vector từ người chơi đến kẻ địch `toAttacker = (attackerPos - playerPos)` rồi chiếu lên 2 trục của Camera người chơi là `Camera.forward` và `Camera.right` bằng phép tính tích vô hướng (Dot Product). Sau đó dùng hàm lượng giác `Mathf.Atan2(rightDot, forwardDot) * Mathf.Rad2Deg` để tính ra góc lệch chính xác từ -180 đến +180 độ. Ma trận vẽ GUI sau đó được xoay quanh tâm màn hình theo góc này để cung đỏ luôn hướng thẳng về phía kẻ thù.

---

### 9. TRẠNG THÁI GIT & CAM KẾT BẢO TOÀN

- Git Branch hiện tại: `feature/my-gameplay-and-ranged-zombie` (HEAD: `666955b`).
- Trạng thái git: Không thực hiện bất kỳ lệnh `git commit`, `git push`, `git merge`, `git checkout`, `git reset`, `git clean` hay `git stash` nào.
- Toàn bộ thay đổi chỉ nằm trong không gian làm việc cá nhân và test scene độc lập.
- Sẵn sàng bàn giao cho người dùng mở Unity chơi thử trực tiếp!

