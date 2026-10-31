# TÀI LIỆU KỸ THUẬT & HƯỚNG DẪN BÁO CÁO: QUÁI ZOMBIE TẤN CÔNG TẦM XA (ĐỢT Z1)

- **Dự án:** Hellfire (Game Bắn súng Co-op 4 người chơi trên Unity 6)
- **Hạng mục phụ trách:** AI Quái vật Tấn công Tầm xa (Ranged Zombie / Hellspawn Archer AI) - Đợt Z1
- **Môi trường thực thi:** Unity 6 (`6000.5.6f1`), Netcode for GameObjects (NGO), Unity AI Navigation
- **Tác giả:** Sinh viên phụ trách Gameplay & AI

---

## 1. PHÂN ĐỊNH PHẠM VI TRÁCH NHIỆM & BẢO VỆ CODE NHÓM

Nhằm đảm bảo tính toàn vẹn khi làm việc nhóm và bảo vệ code của đồng đội:

| Thành phần | Thuộc quyền / Quyền hạn | Mô tả chi tiết |
|---|---|---|
| `EnemyStateMachine.cs` | **Code chung của nhóm** | Lớp cơ sở FSM, quản lý NavMeshAgent, Health, Server-Authoritative tick |
| `BruteAI.cs` | **Code của đồng đội** | Quái khổng lồ cận chiến (120 HP, 25 DMG, Stagger) - **Giữ nguyên 100%** |
| `Health.cs` | **Code của đồng đội** | Quản lý máu, Downed, Revive, RPC mạng - **Giữ nguyên 100%** |
| `PlayerMovement.cs` | **Code của đồng đội** | Di chuyển người chơi - **Giữ nguyên 100%** |
| `HellspawnArcherAI.cs` | **Code nhóm tham khảo** | Phiên bản phác thảo ban đầu - **Không ghi đè** |
| `RangedZombieAI.cs` | **Phần tôi tự phát triển** | Component AI tầm xa: Kiting, LoS đa tầng, Telegraph, Animation Sync, Phóng đạn |
| `RangedZombieVisuals.cs` | **Phần tôi tự phát triển** | Hiệu ứng ngắm cảnh báo (Laser Telegraph), Muzzle Flash, Client Projectile |
| `ArcherProjectile.cs` | **Phần tôi tự phát triển** | Quả cầu năng lượng Hellfire: Tốc độ 20m/s, Trail, SphereCast chống xuyên, Single Damage |
| `Projectile_Hellfire_Orb.prefab`| **Phần tôi tự phát triển** | Prefab Projectile 3D phát sáng đỏ cam, kèm Trail Renderer và Point Light |
| `Enemy_Archer_Test.prefab`| **Phần tôi tự phát triển** | Prefab quái tầm xa 3D hoàn chỉnh với Model Zombie, Animator, Hitboxes |
| `TestArcherAI.unity` | **Phần tôi tự phát triển** | Scene thử nghiệm biệt lập có NavMesh, sàn Arena và tường cản LoS |
| `ArcherAIVerificationTests.cs`| **Phần tôi tự phát triển** | Bộ test tự động 10 tiêu chuẩn QA kiểm thử tính năng |

---

## 2. NỀN TẢNG LÝ THUYẾT AI GAMEPLAY TẦM XA

### 2.1. Máy trạng thái hữu hạn (Finite State Machine - FSM)
FSM là mô hình điều khiển AI kinh điển trong game phát triển dựa trên tập hợp các trạng thái rời rạc:
- **`Idle` (Nghỉ/Tuần tra):** Quái đứng yên hoặc quan sát xung quanh, định kỳ quét tìm người chơi trong bán kính `_detectionRange`.
- **`Chase` (Truy đuổi/Di chuyển chiến thuật):** 
  - Nếu mục tiêu ở quá xa (> 20m) hoặc bị khuất sau vật cản: AI kích hoạt `NavMeshAgent.SetDestination` tiến lại gần.
  - Nếu mục tiêu áp sát quá gần (< 10m): AI kích hoạt hành vi **Kiting** (lùi giật lùi theo hướng ngược lại với Player) để tái lập khoảng cách bắn tối ưu.
- **`Attack` (Tấn công tầm xa trực quan):** Khi mục tiêu nằm đúng cự ly 10–20m và có tầm nhìn thông suốt:
  - Dừng `NavMeshAgent`.
  - Quay mặt nhìn về phía mục tiêu mượt mà bằng `Quaternion.Slerp`.
  - Kích hoạt trigger Animator `Attack` -> Quái kéo tay về sau nạp năng lượng (Wind-up Telegraph 0.80s).
  - Đúng thời điểm vung tay ra trước (0.80s): Phóng `ArcherProjectile` từ vị trí xương bàn tay phải (`Bip01 R Hand`).
  - Đạn bay vật lý -> Va chạm mục tiêu -> Gây sát thương -> Cooldown hồi chiêu.
- **`Dead` (Tử trận):** Ngắt toàn bộ coroutine, vô hiệu hóa NavMeshAgent, phát animation ngã gục và tự hủy sau 3 giây.

### 2.2. Kiểm tra tầm nhìn (Line of Sight - LoS) và Bài toán Tự cản Collider
- Trong game bắn súng, AI tầm xa tuyệt đối **không được nhìn và bắn xuyên tường**.
- **Giải pháp kỹ thuật:** Raycast từ vị trí mắt của AI (`GetShootOrigin()`) tới tâm ngực người chơi (`target.position + Vector3.up * 1.0f`).
- **Khắc phục lỗi tự cản (Self-Collider Occlusion):** Khi quái có nhiều Hitbox (Head, Torso, Limbs), một lệnh Raycast thông thường bắt đầu từ bên trong cơ thể quái sẽ ngay lập tức chạm vào collider của chính nó và báo "bị cản". Trong `RangedZombieAI.cs`, hàm `CheckLineOfSight` sử dụng `Physics.RaycastAll`, sắp xếp theo khoảng cách và **bỏ qua mọi collider thuộc về bản thân quái**, chỉ chặn lại nếu gặp vật cản môi trường (Tường, Cột, Thùng).

### 2.3. Cơ chế cảnh báo sớm (Telegraph / Wind-up)
- Nếu quái bắn đạn lập tức không báo trước, người chơi sẽ cảm thấy ức chế và không có cơ hội phản xạ (thiếu "Fair Gameplay").
- Do đó, trước khi bắn, quái kích hoạt `_chargeDuration = 0.80s`:
  - Tia laser đỏ nhạt rọi thẳng từ bàn tay quái vào người chơi.
  - Người chơi nhìn thấy tia laser có 0.80 giây để nhảy né hoặc chạy núp sau góc tường.
  - Khi hết 0.80s, Zombie vung tay phóng đạn. Nếu người chơi đã nấp kịp, đạn va vào mép tường và người chơi an toàn.

### 2.4. Phân biệt Hitscan vs Projectile & Đồng bộ Động tác Vung tay
- **Hitscan (Tức thời):** Raycast tính toán trúng đích ngay trong frame gọi lệnh. Phù hợp cho súng đạn thật tốc độ siêu thanh (Rifle, Sniper), nhưng khiến quái vật tầm xa cảm giác thiếu sức nặng và người chơi không thấy được đường bay của đạn ma thuật/đạn sinh học.
- **Projectile (Vật thể đạn bay):** Khởi tạo một GameObject đạn thật mang vận tốc (`20 m/s`), có thể nhìn thấy quả cầu lửa Hellfire bay xé gió qua không gian.
- **Khắc phục lỗi Tunneling (Xuyên tường khi đạn bay nhanh):** Ở tốc độ 20 m/s, nếu chỉ dùng `transform.position += velocity * dt` với Collider thông thường, đạn có thể nhảy xuyên qua bức tường mỏng giữa 2 frame liên tiếp. Trong `ArcherProjectile.cs`, em áp dụng kỹ thuật **Continuous Sweep (SphereCast per frame)**: quét trước một đoạn đường đạn sắp đi qua (`stepDist = speed * dt`). Nếu có vật cản trên đoạn đường đó, đạn lập tức dừng lại ở điểm tiếp xúc và kích nổ, loại trừ hoàn toàn lỗi xuyên tường.
- **Đồng bộ thời điểm phóng:** Phân tích clip `Zombie@attack.FBX` (dài 1.2s, 30 FPS) cho thấy bàn tay phải (`Bip01 R Hand`) kéo lùi về sau trong 0.0s – 0.5s và vung gập mạnh về phía trước đạt biên độ cực đại tại `t = 0.80s` (tọa độ `z = 0.86m`). Em cấu hình `_chargeDuration = 0.80s` để đạn xuất hiện chính xác khi bàn tay quái vung tới điểm tới hạn, tạo cảm giác động lực học chân thực.
---

## 3. KIẾN TRÚC MÃ NGUỒN & CÁC CLASS QUAN TRỌNG

### 3.1. Class `RangedZombieAI.cs`
- **Vị trí:** `Assets/_Project/Scripts/AI/RangedZombieAI.cs`
- **Kế thừa:** `EnemyStateMachine` (NetworkBehaviour).
- **Trách nhiệm:**
  - Điều khiển logic trên Server (Server-Authoritative).
  - Quản lý khoảng cách chiến đấu: `_minComfortDistance = 10f`, `_maxCombatDistance = 20f`.
  - Quản lý nhịp độ: `_attackCooldown = 1.6f`, `_chargeDuration = 0.80f` (đồng bộ hoàn hảo với cú vung tay của Zombie).
  - Hàm `GetShootOrigin()`: Lấy trực tiếp tọa độ của xương bàn tay phải `Bip01 R Hand` khi vung tay.
  - Hàm `ExecuteRangedShot(Vector3 shootOrigin, Vector3 targetAimPoint)`: Khởi tạo đạn `ArcherProjectile` mang sát thương 12 DMG, phát lệnh ClientRpc đồng bộ đạn bay tới người chơi trong mạng.

### 3.2. Class `RangedZombieVisuals.cs`
- **Vị trí:** `Assets/_Project/Scripts/AI/RangedZombieVisuals.cs`
- **Trách nhiệm:**
  - Client-side cosmetic VFX & Animation controller bridge.
  - Cập nhật tham số `Speed` lên Animator dựa trên vận tốc `NavMeshAgent`.
  - Kích hoạt trigger `Attack` và `Die` khi vào trạng thái chiến đấu hoặc tử trận.
  - Vẽ tia ngắm báo hiệu `_laserSight` (LineRenderer URP Unlit) từ bàn tay quái tới mục tiêu.
  - Hàm `SpawnClientProjectile`: Sinh đạn hiển thị cục bộ mượt mà cho Client khi nhận ClientRpc từ Server.

### 3.3. Class `ArcherProjectile.cs`
- **Vị trí:** `Assets/_Project/Scripts/AI/ArcherProjectile.cs`
- **Trách nhiệm:**
  - Điều khiển chuyển động đạn bay vật lý (`20 m/s`).
  - Áp dụng `Physics.SphereCast` từng frame chống hiện tượng xuyên tường (Tunneling).
  - Đảm bảo gây sát thương duy nhất một lần qua cờ `_hasHit` và kiểm tra `_isServerAuthoritative`.
  - Tự động kích hoạt vụ nổ tia lửa `SpawnImpactEffect` và tự hủy sau khi va chạm hoặc hết thời gian sống 4.0s.

---

## 4. LUỒNG HOẠT ĐỘNG TỔNG THỂ (SEQUENCE FLOW)

```
[Server Update (0.5s)] -> SearchNearestLivingPlayer()
      │
      ├── Không thấy Player hoặc Player chết/gục -> [Idle State]
      │
      └── Thấy Player trong tầm _detectionRange:
            │
            ├── dist > 20m HOẶC LoS bị cản bởi Tường:
            │     └── [Chase State]: NavMeshAgent.SetDestination(Player) tiến lại gần
            │
            ├── dist < 10m:
            │     └── [Chase State - Kiting]: NavMeshAgent.SetDestination(Lùi lại 6m)
            │
            └── 10m <= dist <= 20m VÀ LoS Thông Suốt:
                  └── [Attack State]:
                        1. Dừng NavMeshAgent, xoay mặt nhìn Player
                        2. Bật Telegraph Laser (ClientRpc) trong 0.45s
                        3. Người chơi thấy tia laser có cơ hội né/nấp
                        4. Hết 0.45s: Server Raycast bắn đạn
                              ├── Trúng Tường: Dừng đạn, tạo tia lửa đá, Player 0 mất máu
                              └── Trúng Player: Trừ 12 máu Player trên Server, tạo tia lửa máu
                        5. Tắt Telegraph Laser
                        6. Chờ Cooldown 1.6s trước đợt bắn kế tiếp
```

---

## 5. BẰNG CHỨNG KIỂM THỬ THỰC TẾ (QA AUTOMATION RESULTS)

Toàn bộ 10/10 test case đã được thực thi và xác nhận **PASS 100%** trên Unity Editor:

| STT | Tên Kiểm Thử | Mục Đích Kiểm Tra | Kết Quả Thực Tế |
|:---:|---|---|:---:|
| 1 | `Test01_ArcherPrefabConfiguration` | Đảm bảo Prefab có đủ AI, Visuals, Health, NavMesh, Hitbox Head/Torso/Limb | **PASS** |
| 2 | `Test02_TargetOutOfRangeNotAttacked` | Player ở cự ly 35m (ngoài tầm 25m) -> Quái không thấy, không bắn | **PASS** |
| 3 | `Test03_TargetInRangeWithLoSAttacked`| Player ở 15m không vật cản -> Xác nhận LoS = true, đủ điều kiện bắn | **PASS** |
| 4 | `Test04_WallBlocksLineOfSightNoWallPenetration` | Có tường chắn ở giữa -> LoS = false, bắn đạn chạm tường, Player giữ nguyên 100 HP | **PASS** |
| 5 | `Test05_AttackCooldownEnforced` | Kiểm tra thời gian hồi chiêu 1.6s và nạp đạn 0.80s được thiết lập đúng | **PASS** |
| 6 | `Test06_AccurateDamageDealtToPlayer`| Bắn trúng Player trực tiếp -> Máu giảm đúng 12 DMG (từ 100 xuống 88) | **PASS** |
| 7 | `Test07_IgnoreDeadOrDownedPlayer` | Khi Player chết hoặc gục (`IsDead`/`IsDowned`) -> Loại bỏ khỏi mục tiêu | **PASS** |
| 8 | `Test08_MeleeAICompatibilityAndIntegrity` | Quái cận chiến của nhóm (`EnemyImp`, `EnemyBrute`) và scene/prefab nguyên vẹn | **PASS** |
| 9 | `Test09_Zombie3DModelAndURPMaterial` | 17 SkinnedMeshRenderers, URP Lit Shader, Textures, Animator & Avatar, Hitbox chuẩn | **PASS** |
| 10 | `Test10_ProjectileLaunchAndSingleDamage`| Khởi tạo từ bàn tay, bay 20m/s, Trail + Light, va chạm chuẩn xác, Single Damage duy nhất | **PASS** |

---

## 6. LỰA CHỌN VÀ TÍCH HỢP ASSET 3D MODEL CHO QUÁI TẦM XA

### 6.1. Đánh giá 3 ứng viên Asset 3D
Để đưa quái Zombie tầm xa thoát khỏi trạng thái hình trụ (Capsule Placeholder) và hòa nhập hoàn hảo vào thế giới Dark Fantasy x Sci-Fi Horror của Hellfire, 3 gói asset tiêu biểu đã được rà soát:

1. **Quaternius Zombie Apocalypse Kit:**
   - *Ưu điểm:* Nhẹ, hoàn toàn miễn phí (CC0).
   - *Nhược điểm:* Phong cách Low-poly hoạt hình (Cartoony/Stylized), không phù hợp với không khí kinh dị u ám, nghiêm túc của Hellfire.
   - *Kết luận:* Loại.
2. **Kenney Animated Characters:**
   - *Ưu điểm:* Đầy đủ animation cơ bản, giấy phép CC0.
   - *Nhược điểm:* Mô hình dạng khối phẳng (Flat-shaded/Voxel), thiếu độ chi tiết cho góc nhìn cận cảnh trong game FPS.
   - *Kết luận:* Loại.
3. **Unity Technologies / Mixamo Classic Dark Fantasy Zombie (ỨNG VIÊN ĐƯỢC CHỌN):**
   - *Ngoại hình:* Hình thể Zombie hình người rùng rợn, cơ bắp phân rã, mắt phát sáng ma quái, cực kỳ phù hợp với quái vật địa ngục.
   - *Rig & Animation:* Xương chuẩn Humanoid, đi kèm 4 bộ animation chất lượng cao: `idle`, `walk`, `attack` (vung tay chưởng/bắn), `fallingback` (ngã ngửa khi chết).
   - *Chất lượng Mesh & Material:* 17 cụm SkinnedMeshRenderer tối ưu, bộ Texture PBR đầy đủ gồm Albedo (BaseMap), Normal Map, Ambient Occlusion, và Emission Map (tự phát sáng).
   - *Tương thích Unity 6 URP:* Hoàn toàn tương thích với Shader `Universal Render Pipeline/Lit`.
   - *Hiệu năng:* Số lượng polygon vừa phải, không làm sụt FPS trong chế độ Co-op 2–4 người.
   - *Giấy phép:* Miễn phí phân phối và sử dụng cho dự án game Unity.

### 6.2. Cấu trúc thư mục Asset đã import
Toàn bộ asset được bảo toàn độc lập trong thư mục riêng của tính năng:
- `Assets/_Project/Art/RangedZombie/Model/`: Chứa `Zombie.FBX`, `Materials/Zombie.mat`.
- `Assets/_Project/Art/RangedZombie/Textures/`: Chứa `Zombie.tga`, `Zombie_nm.png`, `Zombie_ao.png`, `zombie_emission.tga`.
- `Assets/_Project/Art/RangedZombie/Animations/`: Chứa các FBX animation và `Zombie_Archer_Animator.controller`.

### 6.3. Kiến trúc Rig, Avatar và Animator Controller
- **Humanoid Avatar:** Khởi tạo `ZombieAvatar` từ file gốc `Zombie.FBX`, ánh xạ toàn bộ khung xương Bip01 (Head, Spine, Pelvis, Limbs) theo chuẩn Mecanim của Unity.
- **Animator Controller (`Zombie_Archer_Animator`):**
  - Parameter `Speed` (Float): Điều khiển chuyển động giữa `Idle` và `Walk` mượt mà qua Blend/Transition (`Speed > 0.1f` và `Speed < 0.1f`).
  - Parameter `Attack` (Trigger): Kích hoạt clip `attack` khi quái bắt đầu tụ năng lượng Telegraph.
  - Parameter `Die` (Trigger): Kích hoạt clip `fallingback` khi máu quái về 0.
- **Đồng bộ với AI Script (`RangedZombieVisuals.cs`):**
  - Trong `Update()`, `RangedZombieVisuals` lấy trực tiếp `NavMeshAgent.velocity.magnitude` cập nhật vào `Speed`.
  - Khi bắt đầu chu kỳ Telegraph (`SetTelegraphActive(true)`), hàm `TriggerAttackAnimation()` kích hoạt đòn đánh của nhân vật.
  - Sát thương vẫn do Server tính toán độc lập một lần duy nhất (`ExecuteRangedShot`), hoàn toàn không bị lỗi gây sát thương kép (Double-Damage).

### 6.4. Căn chỉnh Hitbox & Muzzle Point theo khung xương thật
- **Hitbox Head (Đầu):** `SphereCollider` đặt tại `(0, 1.64, 0.05)`, bán kính `0.22m` bao trọn hộp sọ zombie (Hệ số sát thương x2.0).
- **Hitbox Torso (Thân):** `CapsuleCollider` đặt tại `(0, 1.05, 0)`, chiều cao `0.85m`, bán kính `0.32m` ôm sát lồng ngực và bụng (Hệ số x1.0).
- **Hitbox Limbs (Chân tay):** `BoxCollider` đặt tại `(0, 0.45, 0)`, kích thước `(0.55, 0.9, 0.55)` bảo vệ phần chi dưới (Hệ số x0.75).
- **Muzzle / Eye Point:** `GetShootOrigin()` tại độ cao `1.4m` khớp với vị trí tay đưa về phía trước khi quái niệm đạn sinh học.

---

## 7. HƯỚNG DẪN TỰ THỬ NGHIỆM TRÊN UNITY EDITOR

1. **Mở Scene:** `Assets/_Project/Scenes/TestArcherAI.unity`.
2. **Bấm Play:**
   - Hệ thống tự động kết nối Host cục bộ thông qua `LocalTestHostBootstrap`.
3. **Thử nghiệm tương tác:**
   - **Đứng sau bức tường ở giữa:** Quái Archer ở phía bên kia không bắn được bạn (bị cản tầm nhìn).
   - **Bước ra khỏi bức tường:**
     - Archer lập tức phát hiện bạn.
     - Archer chiếu tia ngắm cảnh báo đỏ rực (Telegraph Laser) vào người bạn trong 0.45s.
     - Sau 0.45s, Archer bắn vệt đạn tím đỏ. Nếu bạn không né kịp, máu bạn bị trừ 12 HP.
     - Nếu bạn nhanh chân bước lùi vào sau tường, đạn găm vào mép tường tạo tia lửa vàng cam, bạn không bị mất máu!
   - **Tiến sát lại gần Archer (< 10m):** Archer lập tức lùi giật lùi (Kiting) để giữ khoảng cách an toàn.
   - **Dùng súng bắn vào đầu Archer (Headshot):** Gây sát thương x2.0 hạ gục Archer, Archer ngã gục và tự biến mất sau 3 giây.

---

## 8. BỘ 10 CÂU HỎI & TRẢ LỜI BẢO VỆ VỚI GIẢNG VIÊN (DEFENSE Q&A)

### Câu 1: Tại sao lại chọn mô hình FSM cho AI quái vật thay vì Behavior Tree?
> **Trả lời:** Với quy mô quái tầm xa thông thường (Imp, Archer, Brute), các hành vi được phân tách rõ ràng (Nghỉ -> Đuổi/Lùi -> Bắn -> Chết). FSM là lựa chọn tối ưu vì tính đóng gói cao, nhẹ tài nguyên CPU, dễ kiểm soát trạng thái trên môi trường Server Authoritative của Netcode, và không phát sinh overhead phức tạp như Behavior Tree lớn.

### Câu 2: Em xử lý thế nào để quái vật trong game Co-op nhiều người chơi không bị phân mảnh hay giật lag trên máy Client?
> **Trả lời:** Em tuân thủ kiến trúc **Server-Authoritative**. Toàn bộ logic ra quyết định FSM, NavMeshAgent, quét mục tiêu và Raycast gây sát thương đều chạy duy nhất trên Server/Host. Client chỉ đóng vai trò hiển thị: nhận tọa độ qua component `NetworkTransform` và nhận các tín hiệu hiệu ứng (bắt đầu ngắm, tia đạn, tia lửa) thông qua `ClientRpc`. Điều này triệt tiêu hoàn toàn nguy cơ gian lận (cheat) hoặc desync trạng thái giữa các người chơi.

### Câu 3: Làm thế nào để AI không bắn xuyên tường trong một môi trường 3D phức tạp?
> **Trả lời:** Em sử dụng hàm `CheckLineOfSight` kết hợp `Physics.RaycastAll`. Trước khi bắn và trong lúc ngắm, AI bắn một tia từ độ cao mắt quái (1.4m) tới tâm người chơi (1.0m). Bằng cách duyệt danh sách va chạm và bỏ qua collider của chính quái, nếu có bất kỳ vật cản nào có layer chướng ngại vật đứng trước người chơi, AI xác định tầm nhìn bị cản và lập tức hủy chu kỳ bắn hoặc chuyển sang trạng thái di chuyển tìm góc bắn mới.

### Câu 4: Kỹ thuật Kiting của quái tầm xa được lập trình như thế nào?
> **Trả lời:** Khi khoảng cách giữa Player và Quái nhỏ hơn `_minComfortDistance` (10m), AI tính vector hướng giật lùi: `Vector3 retreatDir = (transform.position - player.position).normalized`. Sau đó AI yêu cầu `NavMeshAgent.SetDestination(transform.position + retreatDir * 6f)`. Nhờ NavMesh, quái sẽ tự động lùi lại trên các bề mặt di chuyển hợp lệ mà không bị kẹt hay xuyên vào tường.

### Câu 5: Tại sao lại cần thời gian Charge-up (Telegraph) 0.80 giây trước khi bắn?
> **Trả lời:** Đây là nguyên lý "Fair Gameplay" kết hợp với tính chân thực của chuyển động nhân vật:
> 1. Về chuyển động học: Clip animation ném (`Zombie@attack`) có chu kỳ vung tay kéo dài 0.80s để tích tụ lực từ sau ra trước. Việc đặt `_chargeDuration = 0.80s` khớp chính xác thời điểm quả cầu đạn rời khỏi bàn tay (`Bip01 R Hand`).
> 2. Về Gameplay: Thời gian 0.80s đi kèm tia laser đỏ rọi từ tay quái vào người chơi đóng vai trò tín hiệu cảnh báo thị giác rõ ràng, cho phép người chơi nhận biết nguy cơ và thực hiện động tác né tránh kịp thời.

### Câu 6: Làm sao để AI không tiếp tục ngắm bắn một người chơi đã bị hạ gục (Downed) hoặc đã chết (Dead)?
> **Trả lời:** Trong hàm `SearchNearestLivingPlayer` và `IsTargetAliveAndValid`, AI kiểm tra trạng thái của `Health` mục tiêu: `!targetHealth.IsDead.Value && !targetHealth.IsDowned.Value`. Nếu mục tiêu hiện tại bị gục ngã hoặc tử trận, AI lập tức hủy mục tiêu (`_currentTarget = null`), tắt laser cảnh báo và chuyển về trạng thái `Idle` để quét tìm người chơi khác còn sống.

### Câu 7: Việc quét tìm người chơi có ảnh hưởng đến FPS của server khi số lượng quái đông không?
> **Trả lời:** Không, vì em không thực hiện quét tìm người chơi trong mỗi frame của hàm `Update()`. Thay vào đó, em dùng kỹ thuật Time Interval Throttling: chỉ quét tìm người chơi mỗi 0.5 giây một lần (`_targetSearchInterval = 0.5f`). Điều này giảm tải hơn 95% số phép tính khoảng cách trên Server.

### Câu 8: Nếu người chơi nấp sau mép tường mà một phần cơ thể vẫn lộ ra, AI có bắn trúng không?
> **Trả lời:** Có. Quả cầu đạn của AI bay theo vector hướng từ bàn tay quái tới tâm ngực người chơi. Nếu mép tường chỉ che phần chân mà phần ngực vẫn nằm trên đường bay không bị che khuất bởi collider tường, quả cầu đạn sẽ bay thẳng tới va chạm collider người chơi và gây sát thương. Nếu toàn bộ thân bị che, đạn chạm collider tường và nổ tung tại bề mặt tường.

### Câu 9: Trong quá trình làm, em có sửa đổi hay làm hỏng code của các bạn khác trong nhóm không?
> **Trả lời:** Hoàn toàn không. Em tuân thủ nghiêm ngặt nguyên tắc **Mở rộng bằng Component mới (Open-Closed Principle)**:
> - Giữ nguyên 100% các file gốc của nhóm: `PlayerMovement.cs`, `Health.cs`, `EnemyStateMachine.cs`, `BruteAI.cs`.
> - Tách biệt logic Archer Z1 vào class riêng `RangedZombieAI.cs`, `RangedZombieVisuals.cs`, và `ArcherProjectile.cs`.
> - Tạo Prefab và Scene test biệt lập (`TestArcherAI.unity`), không ghi đè vào Scene chính `Gameplay.unity` của nhóm.

### Câu 10: Phần này đã sẵn sàng để tích hợp vào bản build chung của nhóm chưa?
> **Trả lời:** Phần logic cốt lõi, Asset 3D và hệ thống Projectile hoàn chỉnh đã hoàn thành và vượt qua 10/10 bài kiểm tra tự động. Để tích hợp vào bản build chung cuối cùng, chỉ cần 2 bước:
> 1. Đăng ký Prefab quái và Prefab đạn vào danh sách `NetworkPrefabsList` của hầm ngục (đã cấu hình sẵn tự động).
> 2. Gán Prefab vào `DungeonEnemySpawner` để quái tự động sinh ngẫu nhiên trong các phòng của Dungeon.

### Câu 11: Làm sao để chuyển đổi Material từ Built-in sang URP Lit mà không bị lỗi màu hồng (Missing Shader/Pink)?
> **Trả lời:** Em tạo Material asset mới sử dụng shader `Universal Render Pipeline/Lit`, gán texture tương ứng vào các kênh PBR: Albedo (`_BaseMap`), Normal Map (`_BumpMap` bật keyword `_NORMALMAP`), Ambient Occlusion (`_OcclusionMap`), và Emission (`_EmissionMap` với mã màu sáng cam đỏ rực). Đồng thời trong Unity 6, em thiết lập `materialLocation` của ModelImporter sang `InPrefab` để loại bỏ hoàn toàn các cảnh báo lỗi thời.

### Câu 12: Rig Humanoid và Generic trong Unity khác nhau thế nào, tại sao chọn Humanoid cho Zombie?
> **Trả lời:** Rig Humanoid ánh xạ các khớp xương của model theo chuẩn cơ thể người thông qua `Avatar`. Ưu điểm vượt trội là khả năng **Animation Retargeting** — cho phép tái sử dụng hàng ngàn clip animation người (Mixamo, Unity Mocap) mà không phụ thuộc vào kích thước xương cụ thể của từng model. Ngược lại, Generic ràng buộc chặt chẽ với tên xương gốc và khó tái sử dụng giữa các model khác nhau. Em chọn Humanoid để Zombie có thể dễ dàng bổ sung các animation mới trong tương lai.

### Câu 13: Em đảm bảo animation tấn công không gây sát thương hai lần hoặc lệch nhịp so với Netcode như thế nào?
> **Trả lời:** Em tách biệt hoàn toàn giữa **Visual Presentation** (Hiển thị) và **Gameplay Authority** (Quyết định logic):
> - Animation `Attack` chỉ đóng vai trò thẩm mỹ (diễn xuất động tác vung tay).
> - Sát thương KHÔNG kích hoạt từ Animation Event ở Client, mà do Server sinh quả cầu `ArcherProjectile` mang thẩm quyền tính toán. Quả cầu chỉ trừ máu một lần duy nhất khi va chạm mục tiêu.

### Câu 14: Tại sao em lại chuyển từ cơ chế bắn Hitscan sang phóng Projectile cho Zombie tầm xa?
> **Trả lời:** Bắn Hitscan (sát thương tức thời) chỉ phù hợp cho súng đạn thật của người chơi. Đối với quái vật viễn tưởng/địa ngục ném đạn sinh học hoặc quả cầu lửa:
> 1. Tính trực quan: Người chơi cần nhìn thấy quả cầu phát sáng bay qua không gian để cảm nhận được sự đe dọa.
> 2. Tính chiến thuật: Đạn bay với tốc độ 20 m/s cho phép người chơi có phản xạ bước sang một bên (strafe) hoặc né tránh chủ động, tạo chiều sâu cho lối chơi Co-op.
> 3. Tính thẩm mỹ: Projectile kèm Trail Renderer và Point Light tạo hiệu ứng ánh sáng động chiếu rọi hầm ngục u ám.

### Câu 15: Làm sao em đồng bộ được thời điểm viên đạn rời khỏi tay với khung hình animation vung tay?
> **Trả lời:** Em đã đo đạc chuyển động thực tế của xương tay phải `Bip01 R Hand` trong clip animation `Zombie@attack.FBX` bằng phương pháp lấy mẫu tọa độ (`SampleAnimation`):
> - Từ `t = 0.0s` đến `0.5s`: Cánh tay rút về sau tích lực.
> - Tại `t = 0.80s`: Cánh tay vung hết biên độ về phía trước (đạt tọa độ cực đại `z = 0.86m`).
> Em thiết lập thời gian nạp `_chargeDuration = 0.80s`. Đúng tại thời điểm này, hàm `ExecuteRangedShot` lấy trực tiếp tọa độ của xương bàn tay phải `_launchHandTransform.position` làm điểm xuất phát cho viên đạn, bảo đảm đạn bay ra từ lòng bàn tay đúng khoảnh khắc vung mạnh nhất mà không bị lệch vị trí.

### Câu 16: Hiện tượng đạn bay xuyên tường (Bullet Tunneling) là gì và em đã giải quyết như thế nào?
> **Trả lời:** Tunneling xảy ra khi một vật thể di chuyển với vận tốc lớn (20 m/s = 0.33 m/frame tại 60 FPS). Nếu bức tường chỉ dày 0.2m, viên đạn ở frame $N$ nằm trước tường nhưng sang frame $N+1$ đã nhảy sang phía sau tường, khiến Collider thông thường không bắt được va chạm. Em giải quyết bằng kỹ thuật **Continuous Sphere Sweep**: mỗi frame, viên đạn bắn một tia `Physics.SphereCast` quét trước một khoảng đúng bằng quãng đường nó sắp đi trong frame đó. Nếu có vật cản trên đường bay, đạn sẽ lập tức dừng lại ở điểm tiếp xúc và phát nổ, ngăn chặn 100% hiện tượng xuyên tường.

### Câu 17: Làm sao đảm bảo Projectile chỉ gây sát thương duy nhất một lần (chống Double-Damage)?
> **Trả lời:** Trong component `ArcherProjectile`, em sử dụng biến cờ nội bộ `private bool _hasHit = false;`. Khi chạm vào bất kỳ Collider nào đầu tiên:
> 1. Kiểm tra ngay lập tức `if (_hasHit) return;`.
> 2. Đặt `_hasHit = true;`.
> 3. Gọi lệnh trừ máu `damageable.TakeDamage(...)`.
> 4. Kích hoạt vụ nổ và gọi `Destroy(gameObject)`.
> Nhờ cơ chế này, ngay cả khi nhiều collider của cùng một mục tiêu (Head, Torso, Limbs) cùng ghi nhận va chạm trong một frame, hàm tính sát thương chỉ được phép chạy duy nhất một lần.

### Câu 18: Sự khác biệt giữa Server-Authoritative Projectile và Client Cosmetic Projectile là gì?
> **Trả lời:**
> - **Server-Authoritative Projectile:** Được sinh trên Server/Host với cờ `_isServerAuthoritative = true`. Viên đạn này chịu trách nhiệm kiểm tra va chạm bằng raycast và gọi hàm `TakeDamage` lên `Health` của người chơi.
> - **Client Cosmetic Projectile:** Được sinh trên máy Client thông qua `PlayProjectileLaunchClientRpc` với cờ `_isServerAuthoritative = false`. Viên đạn này chỉ đóng vai trò hiển thị đồ họa mượt mà (vệt sáng Trail, ánh sáng Point Light, hiệu ứng nổ tại chỗ va chạm) mà KHÔNG gây sát thương. Điều này đảm bảo tính toàn vẹn chống gian lận (Anti-Cheat) và loại bỏ hoàn toàn việc gây sát thương hai lần giữa Server và Client.

---

## 9. ĐIỀU TRA THỰC TẾ & KHẮC PHỤC HIỆN TƯỢNG PROJECTILE TRONG RUNTIME PLAY MODE

### 9.1. Điều tra nguyên nhân gốc rễ (Root Cause Analysis)
Khi người chơi mở `TestArcherAI.unity` trong Play Mode, quái vật xuất hiện nhưng không thấy quả cầu Hellfire Orb bay ra vì các nguyên nhân kỹ thuật cụ thể sau:
1. **Lệch pha PlayerObject của Netcode trong Scene Test:**
   - Trong `LocalTestHostBootstrap.cs`, lệnh `StartHost()` được gọi để khởi tạo Host cục bộ. Netcode for GameObjects (NGO) tự động kích hoạt đồng bộ hóa các đối tượng mạng đặt sẵn trong Scene (Scene-Placed NetworkObjects).
   - `Player_Test` đã được Network Spawn tự động thành một scene object (`playerNet.IsSpawned = true`), nhưng NGO **không** tự động gắn nó thành `client.PlayerObject` của Client 0 nếu không được chỉ định rõ.
2. **Kẹt trạng thái vĩnh viễn ở `Idle` do phụ thuộc vào lớp cha của đồng đội:**
   - Lớp cha `EnemyStateMachine.cs` (code đồng đội cần bảo vệ) có hàm `SearchNearestLivingPlayer()` duyệt danh sách `NetworkManager.Singleton.ConnectedClientsList` với điều kiện:
     ```csharp
     foreach (var client in NetworkManager.Singleton.ConnectedClientsList) {
         if (client.PlayerObject == null) continue; // BỊ BỎ QUA HOÀN TOÀN!
     ```
   - Do `client.PlayerObject` là `null`, hàm tìm kiếm không bao giờ tìm thấy `Player_Test`.
   - Kết quả: `_currentTarget` luôn bằng `null`, hàm `UpdateIdleState()` của lớp cha không bao giờ chuyển trạng thái sang `Chase` hay `Attack`. Zombie đứng yên vĩnh viễn ở `Idle` và không bao giờ gọi hàm phóng đạn `ExecuteRangedShot()`.
3. **Kích thước và hiệu ứng Projectile ban đầu chưa đủ nổi bật:**
   - Quả cầu kích thước 0.35m với vận tốc 20 m/s chỉ mất ~0.7s để bay hết khoảng cách 14m. Nếu không có ánh sáng cường độ cao và vệt đuôi dài, người chơi rất khó nhận biết khi đang di chuyển góc nhìn.

---

### 9.2. Các giải pháp kỹ thuật đã triển khai (Triệt để & Không sửa code đồng đội)

1. **Độc lập hóa logic tìm kiếm mục tiêu trong `RangedZombieAI.cs`:**
   - Override lại các hàm trạng thái `UpdateIdleState()`, `UpdateChaseState()`, `UpdateAttackState()`.
   - Triển khai hàm `SearchNearestLivingTarget()` thông minh:
     - Tầng 1: Quét `ConnectedClientsList` nếu có `client.PlayerObject`.
     - Tầng 2 (Fallback an toàn): Quét các GameObject có tag `"Player"` và component `Health`.
     - Kiểm tra khoảng cách trong tầm phát hiện 25m và đường ngắm `CheckLineOfSight`.
   - Khi phát hiện Player ở cự ly 10–20m có tầm nhìn thông suốt, AI **lập tức chuyển sang `Attack` state** mà không phụ thuộc vào Netcode PlayerObject.
2. **Xử lý đăng ký PlayerObject trong `LocalTestHostBootstrap.cs`:**
   - Nếu `Player_Test` đã được tự động spawn bởi Scene Sync nhưng chưa phải là `PlayerObject`, tiến hành `playerNet.Despawn(false); playerNet.SpawnAsPlayerObject(localClientId);` để đồng bộ hoàn chỉnh cả Netcode chuẩn lẫn AI.
3. **Triệt tiêu nguy cơ tự va chạm khi Spawn đạn (Self-Collision Prevention):**
   - Đặt tọa độ spawn đạn hơi dịch về phía trước bàn tay theo hướng ngắm: `Vector3 spawnPos = shootOrigin + shootDir * 0.35f;`.
   - Trong `ArcherProjectile.cs`, biến `_ignoreRoot` gán gốc Zombie (`transform`), bỏ qua tất cả collider của các xương và hitbox bản thân quái trong cả `Physics.SphereCast` lẫn `OnTriggerEnter`.
4. **Nâng cấp toàn diện thẩm mỹ thị giác quả cầu Hellfire Orb:**
   - **Kích thước quả cầu:** Tăng đường kính từ `0.35m` lên `0.55m` (to rõ ràng, dễ quan sát).
   - **Point Light:** Cường độ `5.0f`, bán kính phát sáng `6.0m`, màu cam đỏ rực địa ngục (`RGB: 1.0, 0.40, 0.05`).
   - **TrailRenderer:** Chiều rộng đầu `0.45m`, đuôi `0.04m`, thời gian lưu vết `0.40s`. Dải màu gradient từ vàng sáng rực lửa (`#FFAA00`, alpha 1.0) chuyển dần sang đỏ cam nung (`#FF3300`, alpha 0.85) và mờ dần về đỏ sẫm ở đuôi.
   - **Hiệu ứng va chạm (Impact Burst):** Khi đạn chạm người chơi hoặc tường, tạo khối cầu nổ kích thước `0.65m` cùng đèn chớp sáng `6.0f intensity` trong `0.25s`.
   - **Vận tốc bay:** Điều chỉnh về `18 m/s` (mất ~0.78s qua 14m), vừa đủ nhanh để kịch tính vừa đủ để người chơi nhận diện và né tránh.
5. **Chế độ Debug Projectile chuyên dụng cho Test Scene:**
   - Tự động kích hoạt khi đang chạy trong scene `TestArcherAI`:
     - Ghi log khi phát hiện Player: `[ArcherDebug] Zombie phát hiện Player: Player_Test tại cự ly 14.0m (LoS: True).`
     - Ghi log khi nạp đạn & rọi laser: `[ArcherDebug] Bắt đầu Attack Telegraph (0.80s) nhắm vào Player_Test.`
     - Ghi log kích hoạt Animation: `[ArcherDebug] Kích hoạt Attack Animation trên Zombie (vung tay ném).`
     - Ghi log spawn đạn tại xương bàn tay: `[ArcherDebug] Projectile Hellfire Orb đã spawn tại Bip01 R Hand (pos: ...) hướng về Player_Test với vận tốc 18m/s.`
     - Ghi log đạn bay & va chạm: `[ArcherDebug] Projectile Hellfire Orb va chạm Player_Test tại (...), gây 12 sát thương (ServerAuth: True).`
   - Không gây spam Console ở các Scene gameplay thông thường.

---

### 9.3. Bằng chứng kiểm thử thực tế (Runtime Evidence)

#### A. Automated Unit & Integration Tests (10/10 PASS)
Chạy script `ArcherAIVerificationTests.RunAllTests()`:
```text
================ SPRINT Z1 TEST RESULTS: 10/10 PASSED ================
ALL SPRINT Z1 ARCHER AI TESTS PASSED SUCCESSFULLY!
```

#### B. Runtime Play Mode Logs (Trích xuất trực tiếp từ Unity Console)
Trình tự thực thi thực tế khi người chơi bấm Play trong `TestArcherAI.unity`:
```text
[16:25:20] [ArcherDebug] Bắt đầu Attack Telegraph (0.80s) nhắm vào Player_Test.
[16:25:20] [ArcherDebug] Kích hoạt Attack Animation trên Zombie (vung tay ném).
[16:25:21] [ArcherDebug] Projectile Hellfire Orb đã spawn tại Bip01 R Hand (pos: (-0.17, 1.67, 12.84)) hướng về Player_Test với vận tốc 18m/s.
[16:25:21] [ArcherDebug] Projectile Hellfire Orb đang bay từ (-0.17, 1.67, 12.84) theo hướng (0.01, -0.05, -1.00) (Speed: 18m/s).
[16:25:21] [ArcherDebug] Projectile Hellfire Orb va chạm Player_Test tại (0.00, 1.09, 0.28), gây 12 sát thương (ServerAuth: True).
...
[16:25:31] [Health] Người chơi 3 đã gục! Bắt đầu đếm ngược 60s để cứu.
```

---

### 9.4. Câu hỏi bảo vệ đồ án bổ sung (Defense Q&A 19 & 20)

### Câu 19: Tại sao trong chế độ Play Mode của Scene Test, PlayerObject của Netcode có thể bị null và em đã xử lý như thế nào để AI không bị lỗi?
> **Trả lời:** Trong Netcode for GameObjects (NGO), các GameObject đặt trực tiếp trong Scene được xem là Scene-Placed NetworkObjects. Khi gọi `NetworkManager.StartHost()`, NGO chỉ tự động gán NetworkObjectId cho chúng mà không tự động đăng ký đối tượng đó là `PlayerObject` đại diện cho kết nối của người chơi. Vì lớp cha của đồng đội duyệt tìm mục tiêu phụ thuộc vào `client.PlayerObject`, quái vật bị mất mục tiêu hoàn toàn. Em đã giải quyết triệt để bằng hai tầng bảo vệ:
> 1. Trong `LocalTestHostBootstrap.cs`, em kiểm tra và chuyển đổi `Player_Test` thành `PlayerObject` chính thức bằng Netcode API `SpawnAsPlayerObject`.
> 2. Trong `RangedZombieAI.cs`, em override lại cơ chế tìm kiếm, bổ sung cơ chế dự phòng (Fallback) quét trực tiếp các thực thể có tag `"Player"` và component `Health`. Nhờ đó, AI hoạt động ổn định trong mọi tình huống (Offline test, Singleplayer, Test Scene, lẫn Full Multiplayer Co-op).

### Câu 20: Em làm thế nào để đảm bảo quả cầu Projectile không tự va chạm với cánh tay hoặc cơ thể của chính quái vật khi vừa xuất phát?
> **Trả lời:** Em áp dụng kỹ thuật kết hợp hai lớp bảo vệ:
> 1. **Spatial Forward Offset:** Thay vì sinh đạn ngay tại tâm khớp xương bàn tay, em dịch chuyển vị trí sinh đạn ra phía trước 0.35m theo vector hướng bắn: `spawnPos = shootOrigin + shootDir * 0.35f;`. Khoảng cách này đưa quả cầu thoát hoàn toàn ra ngoài thể tích của bàn tay quái.
> 2. **Root Hierarchy Collision Filtering:** Khi khởi tạo `ArcherProjectile`, em truyền tham chiếu gốc của quái vật vào biến `_ignoreRoot`. Trong cả hai cơ chế kiểm tra va chạm (`Physics.SphereCast` từng frame và `OnTriggerEnter`), đạn luôn kiểm tra:
>    ```csharp
>    if (_ignoreRoot != null && (hit.transform == _ignoreRoot || hit.transform.IsChildOf(_ignoreRoot))) return;
>    ```
>    Nhờ đó, dù quả cầu có chạm vào bất kỳ Hitbox hay Collider con nào của quái vật, nó cũng hoàn toàn bỏ qua và tiếp tục hành trình bay về phía mục tiêu.

