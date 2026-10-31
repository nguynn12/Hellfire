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
| `RangedZombieAI.cs` | **Phần tôi tự phát triển** | Component AI tầm xa hoàn chỉnh: Kiting, LoS đa tầng, Telegraph, Server Raycast |
| `RangedZombieVisuals.cs` | **Phần tôi tự phát triển** | Hiệu ứng ngắm cảnh báo (Laser Telegraph), Tracer đạn, Muzzle Flash và Impact |
| `Enemy_Archer_Test.prefab`| **Phần tôi tự phát triển** | Prefab quái tầm xa hoàn chỉnh với đầy đủ Hitbox Head/Torso/Limb |
| `TestArcherAI.unity` | **Phần tôi tự phát triển** | Scene thử nghiệm biệt lập có NavMesh, sàn Arena và tường cản LoS |
| `ArcherAIVerificationTests.cs`| **Phần tôi tự phát triển** | Bộ test tự động 8 tiêu chuẩn QA kiểm thử tính năng |

---

## 2. NỀN TẢNG LÝ THUYẾT AI GAMEPLAY TẦM XA

### 2.1. Máy trạng thái hữu hạn (Finite State Machine - FSM)
FSM là mô hình điều khiển AI kinh điển trong game phát triển dựa trên tập hợp các trạng thái rời rạc:
- **`Idle` (Nghỉ/Tuần tra):** Quái đứng yên hoặc quan sát xung quanh, định kỳ quét tìm người chơi trong bán kính `_detectionRange`.
- **`Chase` (Truy đuổi/Di chuyển chiến thuật):** 
  - Nếu mục tiêu ở quá xa (> 20m) hoặc bị khuất sau vật cản: AI kích hoạt `NavMeshAgent.SetDestination` tiến lại gần.
  - Nếu mục tiêu áp sát quá gần (< 10m): AI kích hoạt hành vi **Kiting** (lùi giật lùi theo hướng ngược lại với Player) để tái lập khoảng cách bắn tối ưu.
- **`Attack` (Tấn công tầm xa):** Khi mục tiêu nằm đúng cự ly 10–20m và có tầm nhìn thông suốt:
  - Dừng `NavMeshAgent`.
  - Quay mặt nhìn về phía mục tiêu mượt mà bằng `Quaternion.Slerp`.
  - Kích hoạt chu kỳ Telegraph (tụ năng lượng) -> Bắn đạn -> Cooldown hồi chiêu.
- **`Dead` (Tử trận):** Ngắt toàn bộ coroutine, vô hiệu hóa NavMeshAgent, phát animation ngã gục và tự hủy sau 3 giây.

### 2.2. Kiểm tra tầm nhìn (Line of Sight - LoS) và Bài toán Tự cản Collider
- Trong game bắn súng, AI tầm xa tuyệt đối **không được nhìn và bắn xuyên tường**.
- **Giải pháp kỹ thuật:** Raycast từ vị trí mắt của AI (`GetShootOrigin()` = `transform.position + Vector3.up * 1.4f`) tới tâm ngực người chơi (`target.position + Vector3.up * 1.0f`).
- **Khắc phục lỗi tự cản (Self-Collider Occlusion):** Khi quái có nhiều Hitbox (Head, Torso, Limbs), một lệnh Raycast thông thường bắt đầu từ bên trong cơ thể quái sẽ ngay lập tức chạm vào collider của chính nó và báo "bị cản". Trong `RangedZombieAI.cs`, hàm `CheckLineOfSight` sử dụng `Physics.RaycastAll`, sắp xếp theo khoảng cách và **bỏ qua mọi collider thuộc về bản thân quái**, chỉ chặn lại nếu gặp vật cản môi trường (Tường, Cột, Thùng).

### 2.3. Cơ chế cảnh báo sớm (Telegraph / Wind-up)
- Nếu quái bắn hitscan lập tức không báo trước, người chơi sẽ cảm thấy ức chế và không có cơ hội phản xạ (thiếu "Fair Gameplay").
- Do đó, trước khi bắn, quái kích hoạt `_chargeDuration = 0.45s`:
  - Bật tia laser đỏ nhạt rọi thẳng vào người chơi.
  - Người chơi nhìn thấy tia laser có 0.45 giây để nhảy né hoặc chạy núp sau góc tường.
  - Khi hết 0.45s, đạn mới bắn ra. Nếu người chơi đã nấp kịp, đạn va vào mép tường và người chơi an toàn.

---

## 3. KIẾN TRÚC MÃ NGUỒN & CÁC CLASS QUAN TRỌNG

### 3.1. Class `RangedZombieAI.cs`
- **Vị trí:** `Assets/_Project/Scripts/AI/RangedZombieAI.cs`
- **Kế thừa:** `EnemyStateMachine` (NetworkBehaviour).
- **Trách nhiệm:**
  - Điều khiển logic trên Server (Server-Authoritative).
  - Quản lý khoảng cách chiến đấu: `_minComfortDistance = 10f`, `_maxCombatDistance = 20f`.
  - Quản lý nhịp độ: `_attackCooldown = 1.6f`, `_chargeDuration = 0.45f`.
  - Hàm `CheckLineOfSight(Transform target, out RaycastHit blockingHit)`: Xác thực tầm nhìn thực tế.
  - Hàm `ExecuteRangedShot(Vector3 shootOrigin, Vector3 targetAimPoint)`: Server raycast tính sát thương 12 DMG lên `IDamageable`, chặn đạn ở tường, gọi ClientRpc phát hiệu ứng.

### 3.2. Class `RangedZombieVisuals.cs`
- **Vị trí:** `Assets/_Project/Scripts/AI/RangedZombieVisuals.cs`
- **Trách nhiệm:**
  - Client-side cosmetic VFX.
  - Vẽ tia ngắm báo hiệu `_laserSight` (LineRenderer URP Unlit).
  - Vẽ vệt đạn bay `_tracerRenderer` (tím - đỏ).
  - Tạo tia lửa va chạm `SpawnImpactSpark` (màu đỏ nếu trúng người, màu cam vàng nếu trúng tường).
  - Ánh sáng đầu nòng `MuzzleFlashRoutine` (Point Light cường độ cao trong 0.06s).

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

Toàn bộ 8/8 test case đã được thực thi và xác nhận **PASS 100%** trên Unity Editor:

| STT | Tên Kiểm Thử | Mục Đích Kiểm Tra | Kết Quả Thực Tế |
|:---:|---|---|:---:|
| 1 | `Test01_ArcherPrefabConfiguration` | Đảm bảo Prefab có đủ AI, Visuals, Health, NavMesh, Hitbox Head/Torso/Limb | **PASS** |
| 2 | `Test02_TargetOutOfRangeNotAttacked` | Player ở cự ly 35m (ngoài tầm 25m) -> Quái không thấy, không bắn | **PASS** |
| 3 | `Test03_TargetInRangeWithLoSAttacked`| Player ở 15m không vật cản -> Xác nhận LoS = true, đủ điều kiện bắn | **PASS** |
| 4 | `Test04_WallBlocksLineOfSightNoWallPenetration` | Có tường chắn ở giữa -> LoS = false, bắn đạn chạm tường, Player giữ nguyên 100 HP | **PASS** |
| 5 | `Test05_AttackCooldownEnforced` | Kiểm tra thời gian hồi chiêu 1.6s và nạp đạn 0.45s được thiết lập đúng | **PASS** |
| 6 | `Test06_AccurateDamageDealtToPlayer`| Bắn trúng Player trực tiếp -> Máu giảm đúng 12 DMG (từ 100 xuống 88) | **PASS** |
| 7 | `Test07_IgnoreDeadOrDownedPlayer` | Khi Player chết hoặc gục (`IsDead`/`IsDowned`) -> Loại bỏ khỏi mục tiêu | **PASS** |
| 8 | `Test08_MeleeAICompatibilityAndIntegrity` | Quái cận chiến của nhóm (`EnemyImp`, `EnemyBrute`) và scene/prefab nguyên vẹn | **PASS** |

---

## 6. ĐỀ XUẤT 3–5 GÓI ASSET 3D MODEL CHO QUÁI TẦM XA (ĐỂ BẠN CHỌN)

Hiện tại dự án đang dùng **Capsule Model Placeholder** với màu tím đặc trưng để kiểm thử thuật toán AI. Dưới đây là 4 gói Asset 3D chất lượng cao, miễn phí/chuẩn bản quyền Unity Asset Store có thể tích hợp sau khi bạn phê duyệt:

1. **Polygon Zombie / Undead Pack (Synty Studios)**
   - *Phong cách:* Low-poly cách điệu, tối ưu hiệu năng mobile/PC cực tốt.
   - *Nội dung:* Có sẵn Zombie Archer cầm cung hoặc Zombie Spitter bắn độc.
   - *Giấy phép:* Standard Unity Asset Store EULA.
2. **Skeleton Archer Animated (Meshtint Free Pack)**
   - *Phong cách:* Quái xương cung thủ trung cổ hắc ám, trang bị cung tên.
   - *Animation:* Đầy đủ Idle, Walk, Run, Aim, Shoot, Death, Hit.
   - *Giấy phép:* Miễn phí thương mại trên Unity Asset Store.
3. **Mutant / Zombie Sci-Fi Horror (Mixamo Standard)**
   - *Phong cách:* Zombie quái vật biến dị phòng thí nghiệm viễn tưởng, rất hợp với bối cảnh Hellfire & Space Soldier.
   - *Animation:* Hàng trăm animation nạp đạn, ngắm bắn, gục chết có sẵn miễn phí trên Mixamo.
   - *Giấy phép:* Adobe Mixamo Free License (sử dụng tự do cho game).
4. **RPG Monster Duo PBR (Polyart Studio)**
   - *Phong cách:* Quỷ địa ngục có sừng, phát sáng dung nham, mắt rực lửa.
   - *Chức năng:* Phù hợp hoàn hảo với cốt truyện "Địa ngục Hellfire".
   - *Giấy phép:* Free on Unity Asset Store.

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

### Câu 5: Tại sao lại cần thời gian Charge-up (Telegraph) 0.45 giây trước khi bắn?
> **Trả lời:** Đây là nguyên lý "Fair Gameplay" trong Game Design. Đòn đánh hitscan có tốc độ tức thời; nếu quái bắn ngay lập tức khi phát hiện, người chơi không thể phản ứng và sẽ có trải nghiệm bất công. Thời gian 0.45s kèm hiệu ứng tia ngắm laser cảnh báo cho người chơi biết quái chuẩn bị bắn, tạo cơ hội cho người chơi trượt né (dash) hoặc nấp sau chướng ngại vật, làm tăng tính chiến thuật cho game.

### Câu 6: Làm sao để AI không tiếp tục ngắm bắn một người chơi đã bị hạ gục (Downed) hoặc đã chết (Dead)?
> **Trả lời:** Trong hàm `SearchNearestLivingPlayer` và `IsTargetAliveAndValid`, AI kiểm tra trạng thái của `Health` mục tiêu: `!targetHealth.IsDead.Value && !targetHealth.IsDowned.Value`. Nếu mục tiêu hiện tại bị gục ngã hoặc tử trận, AI lập tức hủy mục tiêu (`_currentTarget = null`), tắt laser cảnh báo và chuyển về trạng thái `Idle` để quét tìm người chơi khác còn sống.

### Câu 7: Việc quét tìm người chơi có ảnh hưởng đến FPS của server khi số lượng quái đông không?
> **Trả lời:** Không, vì em không thực hiện quét tìm người chơi trong mỗi frame của hàm `Update()`. Thay vào đó, em dùng kỹ thuật Time Interval Throttling: chỉ quét tìm người chơi mỗi 0.5 giây một lần (`_targetSearchInterval = 0.5f`). Điều này giảm tải hơn 95% số phép tính khoảng cách trên Server.

### Câu 8: Nếu người chơi nấp sau mép tường mà một phần cơ thể vẫn lộ ra, AI có bắn trúng không?
> **Trả lời:** Có. Tia đạn của AI được bắn bằng raycast vector hướng từ đầu nòng tới tâm ngực người chơi. Nếu mép tường chỉ che phần chân mà phần ngực vẫn nằm trên đường bay không bị che khuất bởi collider tường, raycast sẽ trúng collider người chơi và gây sát thương chính xác. Nếu toàn bộ thân bị che, đạn sẽ chạm collider tường và dừng lại.

### Câu 9: Trong quá trình làm, em có sửa đổi hay làm hỏng code của các bạn khác trong nhóm không?
> **Trả lời:** Hoàn toàn không. Em tuân thủ nghiêm ngặt nguyên tắc **Mở rộng bằng Component mới (Open-Closed Principle)**:
> - Giữ nguyên 100% các file gốc của nhóm: `PlayerMovement.cs`, `Health.cs`, `EnemyStateMachine.cs`, `BruteAI.cs`.
> - Tách biệt logic Archer Z1 vào class riêng `RangedZombieAI.cs` và `RangedZombieVisuals.cs`.
> - Tạo Prefab và Scene test biệt lập (`TestArcherAI.unity`), không ghi đè vào Scene chính `Gameplay.unity` của nhóm.

### Câu 10: Phần này đã sẵn sàng để tích hợp vào bản build chung của nhóm chưa?
> **Trả lời:** Phần logic cốt lõi (Core AI & Combat Interaction) đã hoàn thành và vượt qua 8/8 bài kiểm tra tự động. Để tích hợp vào bản build chung cuối cùng, chỉ cần 2 bước:
> 1. Đăng ký Prefab quái vào danh sách `NetworkPrefabsList` của hầm ngục (đã cấu hình sẵn tự động).
> 2. Gán Prefab vào `DungeonEnemySpawner` để quái tự động sinh ngẫu nhiên trong các phòng của Dungeon.
