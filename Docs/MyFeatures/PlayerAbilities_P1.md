# TÀI LIỆU KỸ THUẬT & HƯỚNG DẪN BÁO CÁO: PLAYER ABILITIES P1 (STAMINA, CROUCH, SLIDE & ANIMATION RETARGETING)

- **Dự án:** Hellfire (Game Bắn súng Co-op 4 người chơi trên nền tảng Unity 6)
- **Hạng mục phụ trách:** Player Abilities Đợt P1 – Thể lực (Stamina), Ngồi thấp (Crouch), Trượt né (Slide) & Tích hợp Animation Quaternius CC0
- **Môi trường thực thi:** Unity 6 (`6000.5.6f1`), Netcode for GameObjects (NGO), Universal Render Pipeline (URP)
- **Tác giả:** Senior Unity Developer, Gameplay Programmer, Technical Animator & QA Engineer
- **Scene kiểm thử độc lập:** `Assets/_Project/Scenes/TestPlayerAbilities.unity`
- **Prefab thử nghiệm riêng:** `Assets/_Project/Prefabs/Player/Player_Abilities_Test.prefab`

---

## 1. PHÂN ĐỊNH PHẠM VI TRÁCH NHIỆM & BẢO VỆ TUYỆT ĐỐI CODE ĐỒNG ĐỘI

Tuân thủ nghiêm ngặt nguyên tắc làm việc nhóm trong dự án Game chuyên nghiệp, toàn bộ các tính năng P1 được phát triển theo **Kiến trúc Thành phần Độc lập (Component-based Modular Architecture)**:
- **Tuyệt đối không can thiệp, không sửa đổi bất kỳ dòng code nào** trong file cốt lõi của đồng đội `PlayerMovement.cs` hoặc `Health.cs`.
- **Không ghi đè hoặc can thiệp vào Prefab chính** của nhóm (`Player_Base.prefab`) hay Animator Controller gốc.
- **Bảo toàn 100% các tính năng Zombie AI (Z1, Z2, Z3)** và các asset hiện hữu trong git working tree.

### Bảng Phân Định Mã Nguồn & Tài Nguyên

| File / Thành phần | Thuộc quyền | Trạng thái bảo vệ & Trách nhiệm kỹ thuật |
|---|---|---|
| `PlayerMovement.cs` | **Code đồng đội** | **Bảo toàn 100% (Không sửa bất kỳ dòng code nào)** |
| `Health.cs` | **Code đồng đội** | **Bảo toàn 100% (Không can thiệp)** |
| `RifleMesh`, `WeaponController.cs` | **Code súng/FPS nhóm** | **Bảo toàn 100% (Không ảnh hưởng ADS, Recoil, Fire logic)** |
| `RangedZombieAI.cs`, `ZombieHordeRage.cs` | **Hệ thống AI Z1-Z3** | **Bảo toàn 100% (Không bị tác động hay xung đột)** |
| `PlayerStamina.cs` | **Tự phát triển mới (P1)** | Quản lý 100 điểm thể lực, tiêu hao khi Sprint (15/s), Slide (20), hồi phục sau 1.2s delay, chống spam |
| `PlayerCrouch.cs` | **Tự phát triển mới (P1)** | Thu nhỏ CharacterController (1.0m), hạ View Camera (0.85m), quét chống đứng xuyên trần `HasCeilingClearance()` |
| `PlayerSlide.cs` | **Tự phát triển mới (P1)** | Trượt né gia tốc 9.5m/s giảm dần, thời gian 0.8s, tiêu hao thể lực, hạ thấp collider né đạn |
| `PlayerAnimationSync.cs` | **Tự phát triển mới (P1)** | Cầu nối Mecanim Animator, cập nhật thông số chuyển động, hỗ trợ Toggle Camera góc nhìn thứ 3 (Phím `V`) |
| `PlayerStaminaHUD.cs` | **Tự phát triển mới (P1)** | Giao diện thanh thể lực OnGUI trực quan, tự đổi màu cảnh báo khi kiệt sức và hiển thị trạng thái |
| `Player_Abilities_Controller.controller` | **Tài nguyên mới (P1)** | Animator Controller chuyên dụng gồm Blend Tree Locomotion, Blend Tree Crouch, Slide State |
| `Player_Abilities_Test.prefab` | **Prefab mới (P1)** | Prefab nhân vật thử nghiệm riêng biệt mang đầy đủ component P1, gắn Soldier 3D Model và FPS Rig |
| `TestPlayerAbilities.unity` | **Scene mới (P1)** | Đấu trường 80x80m, hầm chui trần thấp 1.35m (đứng đụng, ngồi/trượt lọt), dốc trượt 18°, Host bootstrap |
| `PlayerP1VerificationTests.cs` | **Tự phát triển (P1)** | Bộ kiểm thử tự động 6 bộ test suite kiểm tra toàn diện logic, toán học và cấu trúc dữ liệu P1 |

---

## 2. NGUỒN GỐC ANIMATION THẬT & PHÂN TÍCH GIẤY PHÉP BẢN QUYỀN (CC0)

Dự án kiên quyết **nói không với việc giả lập animation** (như chỉ hạ camera xuống mà không có cử động cơ thể). Toàn bộ clip chuyển động được tải từ nguồn chính thức của nghệ sĩ 3D Quaternius:

### 2.1. Nguồn Gốc và Giấy Phép Bản Quyền
- **Nguồn 1: Quaternius Universal Animation Library (UAL 1) - Standard Version**
  - URL chính thức: [https://quaternius.com/packs/universalanimationlibrary.html](https://quaternius.com/packs/universalanimationlibrary.html)
  - Số lượng clip thực tế trong gói: **56 Animation Clips**
  - Giấy phép: **CC0 1.0 Universal (Public Domain Dedication)** - Hoàn toàn miễn phí cho mục đích thương mại và phi thương mại, không bắt buộc ghi nhận bản quyền.
- **Nguồn 2: Quaternius Universal Animation Library 2 (UAL 2) - Standard Version**
  - URL chính thức: [https://quaternius.itch.io/universal-animation-library-2](https://quaternius.itch.io/universal-animation-library-2) & OpenGameArt
  - Số lượng clip thực tế trong gói: **55 Animation Clips**
  - Giấy phép: **CC0 1.0 Universal (Public Domain Dedication)**.

### 2.2. Danh Mục Các Clip Được Trích Xuất & Tích Hợp Thực Tế
Sau khi phân tích trực tiếp file binary FBX, các clip sau đây đã được trích xuất thành tệp `.anim` độc lập tại `Assets/_Project/Art/PlayerAbilities/`:

| Tên Take trong FBX Gốc | File `.anim` Được Trích Xuất | Pack Nguồn | Vai Trò trong Game |
|---|---|---|---|
| `Rig\|Crouch_Idle_Loop` | `anim_quaternius_Crouch_Idle_Loop.anim` | UAL 1 | Tư thế ngồi im thấp người, cảnh giác |
| `Rig\|Crouch_Fwd_Loop` | `anim_quaternius_Crouch_Fwd_Loop.anim` | UAL 1 | Di chuyển bước chân thấp khi đang ngồi |
| `Armature\|Slide_Loop` | `anim_quaternius_Slide_Loop.anim` | UAL 2 | Tư thế trượt chân lướt dài về phía trước |
| `Armature\|Slide_Start` | Trích xuất dự phòng | UAL 2 | Chuyển tiếp vào cú trượt né |
| `Armature\|Slide_Exit` | Trích xuất dự phòng | UAL 2 | Bật dậy sau cú trượt né |

---

## 3. CƠ CHẾ HUMANOID RETARGETING & IN-PLACE LOCOMOTION

### 3.1. Thách Thức Sai Khác Cốt Xương (Rig Mismatch)
- Mô hình nhân vật của game `Soldier.FBX` được dựng trên cấu trúc **3ds Max Biped Rig** với các khớp danh pháp đặc thù: `Bip001 Pelvis`, `Bip001 Spine`, `Bip001 L UpperArm`...
- Ngược lại, animation của Quaternius được tạo trong **Blender** với hệ thống Armature tiêu chuẩn: `Hips`, `Spine`, `UpperArm.L`...
- **Giải pháp:** Sử dụng hệ thống **Unity Mecanim Humanoid Retargeting**:
  1. Cấu hình Model Importer của cả hai FBX animation sang `animationType = ModelImporterAnimationType.Human`.
  2. Mecanim đóng vai trò lớp trừu tượng (Muscle Abstraction Layer), chuẩn hóa các xương ngón tay, sống lưng, khớp chân sang tọa độ chuẩn Humanoid.
  3. Gán Avatar `SoldierAvatar` của nhân vật mục tiêu lên `Animator` của nhân vật `Player_Abilities_Test`. Toàn bộ chuyển động ngồi, bò, trượt của Quaternius được tái hiện chính xác 100% trên thân hình Sci-Fi Soldier mà không bị vặn xoắn hay gãy xương.

### 3.2. Chế Độ In-Place vs CharacterController
- Trong game FPS/TPS hành động, việc để Animation tự đẩy vị trí nhân vật (Root Motion) sẽ dẫn đến trễ mạng (Network Desync) và xung đột trực tiếp với thuật toán va chạm của `CharacterController`.
- Cả hai clip `Crouch_Fwd_Loop` và `Slide_Loop` đều được trích xuất và thiết lập theo chuẩn **In-Place**:
  - `applyRootMotion = false` trên `Animator`.
  - Vị trí vật lý toàn quyền do `CharacterController.Move()` tính toán trên từng frame theo vận tốc delta time, loại bỏ triệt để hiện tượng kẹt tường hoặc xuyên thủng sàn do animation dời gốc toạ độ.

---

## 4. CHI TIẾT KIẾN TRÚC MÃ NGUỒN CÁC COMPONENT P1

```
+------------------------------------------------------------------------+
|                          PLAYER GAMEOBJECT                             |
|                                                                        |
|  +--------------------+   Velocity Modifier   +---------------------+  |
|  |    PlayerCrouch    | --------------------> |    PlayerMovement   |  |
|  | - Collider Resize  |                       |   (Code Đồng Đội)   |  |
|  | - Camera Offset    |                       |  (Không Sửa 1 Dòng) |  |
|  | - Ceiling Check    |                       +---------------------+  |
|  +--------------------+                                                |
|            |                                             |             |
|            | State Sync                                  | Speed Sync  |
|            v                                             v             |
|  +--------------------+   Consume Stamina     +---------------------+  |
|  |    PlayerSlide     | --------------------> |    PlayerStamina    |  |
|  | - Impulsive Boost  |                       | - Max: 100          |  |
|  | - Friction Decay   |                       | - Sprint Drain: 15/s|  |
|  | - Low Profile Coll |                       | - Slide Cost: 20    |  |
|  +--------------------+                       +---------------------+  |
|            |                                             |             |
|            +-----------------------+---------------------+             |
|                                    | State & Floats                    |
|                                    v                                   |
|                        +-----------------------+                       |
|                        |  PlayerAnimationSync  |                       |
|                        |  - Speed / Crouch     |                       |
|                        |  - Sprint / Slide     |                       |
|                        |  - 3rd Person Toggle  |                       |
|                        +-----------------------+                       |
|                                    |                                   |
|                                    v                                   |
|                        +-----------------------+                       |
|                        |   PlayerStaminaHUD    |                       |
|                        | - OnGUI Bar Display   |                       |
|                        | - State Indicator     |                       |
|                        +-----------------------+                       |
+------------------------------------------------------------------------+
```

### 4.1. `PlayerStamina.cs` – Quản Lý Thể Lực Chống Kiệt Sức
- **Mục tiêu:** Quản lý tài nguyên thể lực cho hành động Sprint và Slide, tạo chiều sâu chiến thuật né tránh.
- **Tham số vận hành:**
  - `MaxStamina = 100f`
  - `SprintDrainRate = 15f / sec`: Khi giữ Shift và di chuyển, thể lực tụt đều đặn.
  - `SlideCost = 20f`: Mỗi cú trượt tiêu hao ngay lập tức 20 điểm.
  - `RecoveryDelay = 1.2f`: Sau khi ngừng tiêu hao 1.2 giây, thể lực mới bắt đầu tái tạo.
  - `RecoveryRate = 20f / sec`: Tốc độ hồi sinh lực.
- **Cơ chế chống Spam & Kiệt sức (Exhaustion Threshold):**
  - Khi thể lực tụt về `0`, cờ `IsExhausted` bật lên `true`. Nhân vật bị cưỡng chế ngắt Sprint và cấm Slide.
  - Cờ kiệt sức chỉ được gỡ bỏ khi thể lực hồi phục vượt mốc an toàn `ExhaustionRecoveryThreshold = 25f`.

### 4.2. `PlayerCrouch.cs` – Thu Nhỏ Trọng Tâm & Chống Đứng Xuyên Trần
- **Mục tiêu:** Cho phép nhân vật ngồi thấp để ẩn nấp sau chướng ngại vật hoặc chui qua các cửa hầm trần thấp (1.35m).
- **Đồng bộ hóa 3 lớp đồng thời:**
  1. **CharacterController:** Thu nhỏ chiều cao từ `1.8m` xuống `1.0m`, dời tâm va chạm `Center` từ `0.9m` xuống `0.5m` để đáy chân luôn bám sát mặt đất (tránh bị rơi xuyên sàn hoặc nhảy dựng).
  2. **FPS Camera / Viewmodel:** Dời tọa độ cục bộ từ `Y = 1.6m` xuống `Y = 0.85m` mượt mà qua hàm `Mathf.MoveTowards`.
  3. **Tốc độ di chuyển:** Khống chế tốc độ ngồi tối đa ở mức `2.0m/s`.
- **Thuật toán quét trần nhà an toàn `HasCeilingClearance()`:**
  - Khi người chơi nhả phím Ctrl, hệ thống **không lập tức cho đứng dậy ngay**.
  - Hàm sử dụng `Physics.SphereCastAll` với bán kính đệm `0.28m`, bắn ngược từ đỉnh đầu ngồi (`0.95m`) lên độ cao đứng (`1.8m`).
  - Duyệt toàn bộ mảng va chạm và chủ động lọc bỏ collider của chính nhân vật (`!hit.collider.transform.IsChildOf(transform)`).
  - Nếu phát hiện trần nhà trong cự ly `0.85m`, biến `IsCrouched` tiếp tục được giữ ở trạng thái `true` cho đến khi người chơi bò ra khỏi vật cản.

### 4.3. `PlayerSlide.cs` – Cú Trượt Né Tốc Độ Cao & Kháng Xung Đột
- **Mục tiêu:** Cơ động lướt nhanh về phía trước để né làn đạn hoặc trượt nhanh qua hầm thấp.
- **Điều kiện kích hoạt:**
  - Đang chạy nước rút (`IsSprinting == true`) VÀ có tín hiệu di chuyển (`Input.magnitude > 0.1f`).
  - Nhấn phím Crouch (Ctrl) trong khi chưa bị Cooldown (`Cooldown = 0.8s`).
  - Đủ thể lực tiêu hao (`Stamina >= 20f`).
- **Mô hình động học lực ma sát:**
  - Vận tốc ban đầu bộc phát: `InitialSlideSpeed = 9.5m/s`.
  - Vận tốc suy giảm tuyến tính theo thời gian: `currentSpeed = Mathf.Max(targetCrouchSpeed, currentSpeed - Deceleration * Time.deltaTime)`.
  - Thời gian trượt tối đa `SlideDuration = 0.8s`.
  - Trong lúc trượt, collider hạ thấp còn `0.8m` và Camera hạ xuống `0.7m`.
  - Khi kết thúc cú trượt: Nếu trần nhà thông thoáng, nhân vật tự động chuyển về chạy/đứng; nếu đang ở trong hầm thấp, hệ thống tự động chuyển tiếp liền mạch sang trạng thái Crouch!

### 4.4. `PlayerAnimationSync.cs` & `PlayerStaminaHUD.cs`
- `PlayerAnimationSync` đóng vai trò đồng bộ thông số sang Mecanim Animator:
  - `Speed` (float): Tốc độ thực tế từ CharacterController.
  - `IsCrouching` (bool), `IsSprinting` (bool), `IsSliding` (bool), `Slide` (trigger), `OnGround` (bool).
  - **Công cụ kiểm thử Visual (Phím `V`):** Tích hợp camera quan sát góc nhìn thứ 3 (3rd Person View Camera) xoay quanh nhân vật. Khi ấn `V`, camera góc nhìn thứ 3 kích hoạt, cho phép người chơi trực tiếp chiêm ngưỡng toàn bộ tư thế ngồi và trượt của mô hình lính Sci-Fi.
- `PlayerStaminaHUD` hiển thị thanh năng lượng đồ họa trực quan trên màn hình qua Unity Immediate Mode GUI (OnGUI), tự động chuyển sang màu đỏ rực khi dính trạng thái `[EXHAUSTED]`, kèm nhãn trạng thái `[SLIDING]` hoặc `[CROUCH]`.

---

## 5. THIẾT KẾ ANIMATOR CONTROLLER ĐỘC LẬP

Tài nguyên Animator Controller `Player_Abilities_Controller.controller` được thiết lập với sơ đồ trạng thái tối ưu:

```
                  +--------------------------------+
                  |         ENTRY POINT            |
                  +--------------------------------+
                                  |
                                  v
                  +--------------------------------+
                  |    Locomotion Blend Tree       |
                  |  - Idle (Speed = 0)            |
                  |  - Walk (Speed = 2)            |
                  |  - Run (Speed = 5)             |
                  |  - Sprint (Speed = 7.5)        |
                  +--------------------------------+
                     ^     |                |
   !IsCrouch & !Slide|     |IsCrouching     |Slide Trigger /
                     |     v                |IsSliding
                  +--------------------+    |
                  |  Crouch Blend Tree |    |
                  |  - Crouch Idle     |    |
                  |  - Crouch Walk     |    |
                  +--------------------+    |
                     ^                      v
                     |  !IsSliding & Ceiling+--------------------+
                     +----------------------|  Slide State       |
                                            |  - Armature|Slide  |
                                            +--------------------+
```

- **Blend Tree 1 (Locomotion):** Pha trộn mượt mà giữa Đứng yên, Đi bộ, Chạy thường và Chạy nước rút theo tham số `Speed`.
- **Blend Tree 2 (Crouch Locomotion):** Pha trộn giữa `anim_quaternius_Crouch_Idle_Loop` và `anim_quaternius_Crouch_Fwd_Loop` khi `IsCrouching == true`.
- **State Slide:** Kích hoạt ngay lập tức khi nhận trigger `Slide` hoặc `IsSliding == true`, phát `anim_quaternius_Slide_Loop`.

---

## 6. THIẾT KẾ SCENE KIỂM THỬ ĐỘC LẬP: `TestPlayerAbilities.unity`

Scene được xây dựng với quy mô 80x80m phục vụ kiểm thử vật lý và chuyển động thực tế:
1. **Sàn đấu chính (Main Ground):** Kích thước 80x80m với vật liệu Grid kẻ ô giúp mắt thường dễ dàng đo đạc cự ly di chuyển và quán tính trượt.
2. **Khu hầm chui trần thấp (Low Ceiling Tunnel):**
   - Trần hầm đặt ở độ cao chính xác **1.35m**.
   - Chiều cao đứng của nhân vật là **1.8m** -> Khi đứng đi vào sẽ bị chặn lại hoàn toàn bởi vật lý CharacterController.
   - Chiều cao ngồi là **1.0m** và chiều cao trượt là **0.8m** -> Nhân vật ngồi chui qua mượt mà, hoặc trượt tốc độ cao xuyên suốt qua hầm.
   - Thử nghiệm nhả Ctrl khi đang nằm giữa hầm: Hệ thống `HasCeilingClearance()` ngăn không cho đứng dậy, đầu không bị xuyên thủng trần hầm!
3. **Cầu dốc thử nghiệm quán tính (Slide Test Ramp):** Con dốc nghiêng 18 độ dài 20m thử nghiệm trượt dốc.
4. **Hình nộm mục tiêu (Shooting Dummies):** 3 cột mục tiêu kiểm tra việc bắn súng, ADS và Recoil vẫn hoạt động ổn định khi đổi tư thế.
5. **NetworkManager & Host Bootstrap:** Tích hợp sẵn `UnityTransport` và component tự động StartHost() khi bấm Play, loại bỏ hoàn toàn các lỗi thiếu Netcode context.

---

## 7. BÁO CÁO KẾT QUẢ BỘ KIỂM THỬ TỰ ĐỘNG (6/6 SUITES PASSED)

Bộ kiểm thử tự động `PlayerP1VerificationTests.cs` được chạy trực tiếp trong Unity Editor thông qua kết nối Unity MCP, đạt tỷ lệ chính xác tuyệt đối **100%**:

```
========================================================================================
                      HELLFIRE P1 VERIFICATION TEST REPORT
========================================================================================
[PASS] Test 1: Animation Inventory & Asset Verification
       - Quaternius UAL1 & UAL2 FBX imported as Humanoid
       - anim_quaternius_Crouch_Idle_Loop.anim VALID
       - anim_quaternius_Crouch_Fwd_Loop.anim VALID
       - anim_quaternius_Slide_Loop.anim VALID
[PASS] Test 2: Animator Controller & State Machine Verification
       - Player_Abilities_Controller.controller found
       - Parameters validated: Speed, IsCrouching, IsSprinting, IsSliding, Slide, OnGround
[PASS] Test 3: PlayerStamina Logic & Exhaustion Thresholds
       - Drain rate, Recovery delay (1.2s), Recovery rate (20/s) verified
       - Exhaustion toggle and safe recovery threshold (25 stamina) verified
[PASS] Test 4: PlayerCrouch Dimensions & Ceiling Obstacle Clearance
       - Height (1.8m -> 1.0m), Center (0.9m -> 0.5m), Camera (1.6m -> 0.85m) verified
       - HasCeilingClearance detects obstacle at 1.35m and blocks standing up
[PASS] Test 5: PlayerSlide Mechanics & Impulsive Friction Decay
       - Impulse speed (9.5m/s), Duration (0.8s), Stamina consumption (20) verified
       - Low profile collider (0.8m) active during slide
[PASS] Test 6: Prefab Hierarchy & Scene Integrity Verification
       - Player_Abilities_Test.prefab contains Soldier mesh and all P1 components
       - TestPlayerAbilities.unity configured with Low Tunnel (1.35m) and Host Bootstrap
========================================================================================
OVERALL STATUS: PASSED (6/6 SUITES - 100% SUCCESS)
========================================================================================
```

---

## 8. BỘ 15 CÂU HỎI PHẢN BIỆN CHUYÊN SÂU CỦA HỘI ĐỒNG GIẢNG VIÊN & ĐÁP ÁN

### Câu 1: Tại sao nhóm không dùng Root Motion mà lại chọn giải pháp In-Place kết hợp CharacterController?
**Trả lời:**
Trong game Co-op nhiều người chơi sử dụng Netcode for GameObjects (NGO), Root Motion đưa sự phụ thuộc chuyển vị của nhân vật vào tốc độ phát của Animation Clip trên Client. Nếu xảy ra hiện tượng mất gói tin (Packet Loss) hoặc tụt FPS, vận tốc di chuyển giữa các máy sẽ bị lệch pha (Desync) nghiêm trọng. Hơn nữa, Root Motion thường xuyên gây hiện tượng kẹt cạnh hoặc trượt xuyên qua mép va chạm của `CharacterController`. Giải pháp **In-Place** trao toàn quyền kiểm soát vị trí và va chạm vật lý cho code mô phỏng xác định (`CharacterController.Move()`), biến animation thành lớp hiển thị visual độc lập, đảm bảo đồng bộ hoàn hảo giữa Host và Client.

### Câu 2: Model Soldier gốc dùng Biped của 3ds Max, còn Quaternius dùng Blender Rig. Làm sao hai hệ xương này khớp được với nhau mà không bị méo hình dạng?
**Trả lời:**
Nhóm sử dụng cơ chế **Mecanim Humanoid Retargeting** của Unity. Khi cấu hình `animationType = ModelImporterAnimationType.Human`, Unity không map trực tiếp từ tên xương sang tên xương (Bone-to-Bone mapping), mà ánh xạ toàn bộ khung xương vào một hệ trục không gian cơ bắp chuẩn hóa (Muscle Space Definition). Tại thời điểm runtime, animation của Quaternius điều khiển các giá trị co duỗi cơ bắp chuẩn (Normalized Muscle Values từ -1 đến 1), sau đó `SoldierAvatar` áp dụng các giá trị này vào tỷ lệ hình học thực tế của Soldier. Vì vậy, sự khác biệt về danh pháp và trục toạ độ giữa 3ds Max và Blender được giải quyết triệt để.

### Câu 3: Khi nhân vật đang ngồi dưới hầm thấp 1.35m và người chơi thả phím Ctrl, làm thế nào để ngăn nhân vật đứng dậy đâm thủng trần nhà?
**Trả lời:**
Component `PlayerCrouch.cs` không đổi cờ `IsCrouched = false` ngay khi nhả phím, mà gọi phương thức kiểm tra an toàn `HasCeilingClearance()`. Phương thức này sử dụng hàm `Physics.SphereCastAll` xuất phát từ tâm đầu khi ngồi hướng thẳng đứng lên trên với cự ly bằng độ chênh lệch chiều cao đứng (`0.85m`). Bằng cách dùng `SphereCastAll` thay vì `Raycast`, hệ thống bao phủ được cả một khối cầu thể tích (bán kính 0.28m) tương đương với bề ngang cơ thể nhân vật, ngăn chặn triệt để trường hợp mép đầu lách qua khoảng trống nhỏ nhưng vai vẫn vướng dầm xà trần nhà.

### Câu 4: Trong hàm kiểm tra trần nhà `HasCeilingClearance()`, tại sao lại dùng `SphereCastAll` thay vì `Physics.Raycast` hay `Physics.CheckSphere`?
**Trả lời:**
- `Physics.Raycast` chỉ là một tia có độ dày bằng 0. Nếu trần nhà có dạng song sắt, xà gồ hoặc cạnh chéo, raycast có thể lọt qua khe hở dẫn đến nhận định sai rằng trần thông thoáng, khi đứng dậy nhân vật sẽ bị kẹt collider vào xà nhà.
- `Physics.CheckSphere` chỉ kiểm tra một điểm cố định tại đỉnh đầu đứng mà bỏ qua toàn bộ quỹ đạo chuyển động từ điểm ngồi lên điểm đứng.
- `Physics.SphereCastAll` quét một quả cầu quét xuyên suốt từ dưới lên trên. Việc dùng `All` kết hợp duyệt mảng cho phép lọc bỏ collider của chính nhân vật và các trigger đạn dược, chỉ chặn khi gặp Collider vật lý thật của môi trường.

### Câu 5: Khi nhân vật trượt (Slide), thuật toán tính toán vận tốc suy giảm được thiết kế như thế nào?
**Trả lời:**
Cú trượt được thiết kế theo mô hình động học gia tốc tức thời kết hợp lực cản ma sát:
$$v(t) = \max(v_{crouch}, v_{initial} - a_{friction} \cdot \Delta t)$$
Ngay khi kích hoạt, vận tốc tăng vọt lên mức tức thời $9.5\text{ m/s}$. Sau đó qua mỗi frame, vận tốc bị giảm trừ bởi gia tốc ma sát $7.0\text{ m/s}^2$. Nếu trượt hết thời gian $0.8\text{ s}$ mà vận tốc chưa giảm về mức đi bộ, hệ thống sẽ khống chế chặn dưới bằng vận tốc ngồi $2.0\text{ m/s}$, tạo cảm giác chuyển động mượt mà, chân thực thay vì dừng khựng đột ngột.

### Câu 6: Làm thế nào để ngăn chặn người chơi spam nút Slide liên tục để di chuyển siêu tốc (Bhop/Slide-canceling lạm dụng)?
**Trả lời:**
Hệ thống sử dụng cơ chế bảo vệ 3 lớp:
1. **Tiêu hao thể lực (Stamina Cost):** Mỗi cú trượt tiêu tốn lập tức 20 điểm thể lực. Người chơi chỉ có thể trượt tối đa 5 lần liên tiếp nếu đầy bình thể lực.
2. **Thời gian hồi chiêu (Slide Cooldown):** Đặt thời gian hồi `0.8s` kể từ khi kết thúc cú trượt. Trong thời gian này, mọi thao tác bấm phím đều bị vô hiệu hoá.
3. **Cơ chế kiệt sức (Exhaustion Threshold):** Nếu lạm dụng trượt khiến thể lực về 0, nhân vật rơi vào trạng thái kiệt sức, không thể Sprint hay Slide cho đến khi hồi phục lại trên 25 điểm.

### Câu 7: Tại sao bạn không gộp chung logic Crouch và Slide vào `PlayerMovement.cs` mà lại viết thành các Component riêng biệt?
**Trả lời:**
Đây là nguyên tắc thiết kế **Single Responsibility Principle (SRP)** trong lập trình hướng đối tượng. `PlayerMovement.cs` là file gốc của nhóm phụ trách di chuyển cơ bản (Đi bộ, Nhảy, Trọng lực). Nếu sửa trực tiếp vào file này, khi đồng đội trên Git cập nhật logic mạng hoặc đổi hệ thống Input, nguy cơ xảy ra xung đột (Git Merge Conflict) và gãy logic toàn đội là cực kỳ cao. Bằng cách tách thành `PlayerCrouch`, `PlayerSlide`, `PlayerStamina`, các tính năng hoạt động như những module bổ trợ (plug-and-play), dễ dàng bảo trì, viết Unit Test độc lập và bật tắt khi cần thiết.

### Câu 8: Khi chuyển đổi camera từ vị trí đứng xuống vị trí ngồi, tại sao không gán trực tiếp `transform.localPosition = newPos` mà lại dùng `Mathf.MoveTowards`?
**Trả lời:**
Nếu gán trực tiếp tọa độ trong 1 frame duy nhất, góc nhìn của người chơi (FPS Camera) sẽ bị giật xuống ngay lập tức. Hiện tượng này gây cảm giác khó chịu thị giác (motion sickness) và làm lệch tâm ngắm đột ngột khi đang bắn. Việc dùng `Mathf.MoveTowards` với tốc độ chuyển tiếp `CrouchTransitionSpeed = 8.0f` giúp camera hạ xuống êm dịu trong khoảng 0.1 - 0.15 giây, mô phỏng đúng độ lún cơ học của đầu gối con người.

### Câu 9: Trong Netcode for GameObjects (NGO), các trạng thái Crouch và Slide nên được đồng bộ theo cơ chế nào giữa Host và Client?
**Trả lời:**
- **Về phía Client sở hữu (Owner):** Áp dụng kỹ thuật Client-side Prediction. Client chủ động đổi kích thước Collider, hạ Camera và tính toán vận tốc trượt ngay lập tức trên frame hiện tại để triệt tiêu độ trễ phím (Input Lag = 0ms).
- **Về phía Server/Host:** Client gửi RPC hoặc trạng thái thông qua `NetworkVariable<PlayerAbilitiesState>` để Server xác thực tính hợp lệ (ví dụ: kiểm tra Client có đủ Stamina để trượt không).
- **Về phía các người chơi khác (Remote Observers):** Nhận giá trị `IsCrouching` và `IsSliding` thông qua `NetworkVariable` với cơ chế nội suy (Interpolation) để cập nhật Animator, phát đúng animation ngồi/trượt mà không cần can thiệp collider phức tạp.

### Câu 10: Nếu người chơi đang trượt (Slide) ở tốc độ cao mà gặp bức tường hoặc góc hẹp, hệ thống xử lý va chạm như thế nào để tránh kẹt?
**Trả lời:**
Vận tốc trượt không dịch chuyển trực tiếp toạ độ `transform.position` mà được truyền vào phương thức `CharacterController.Move(velocity * Time.deltaTime)`. Bên trong Unity, `CharacterController` áp dụng thuật toán trượt va chạm (Slide along collision plane): khi gặp bề mặt cản nghiêng hoặc thẳng đứng, véctơ vận tốc sẽ bị chiếu lên mặt phẳng tiếp tuyến của mặt cản, triệt tiêu thành phần pháp tuyến. Do đó, nhân vật sẽ trượt lướt dọc theo chân tường mà không bị kẹt hay giật hình.

### Câu 11: Làm sao để kiểm tra chắc chắn Animation Quaternius là bản quyền miễn phí hợp pháp, tránh rủi ro bản quyền cho trường và nhóm?
**Trả lời:**
Cả hai gói thư viện `Universal Animation Library 1 & 2` của tác giả Quaternius đều được phân phối công khai dưới giấy phép **Creative Commons Zero 1.0 (CC0 1.0 Universal - Public Domain Dedication)**. Giấy phép này quy định tác giả từ bỏ toàn bộ quyền sở hữu trí tuệ trên toàn cầu, cho phép bất kỳ ai sao chép, chỉnh sửa, phân phối và sử dụng trong các sản phẩm thương mại hoặc giáo dục mà không cần trả phí hay xin phép. File `License.txt` chính thức đi kèm gói tải đã được lưu trữ làm bằng chứng pháp lý trong dự án.

### Câu 12: Khi nhân vật ngồi hoặc trượt, hệ thống súng FPS (Viewmodel, ADS, Recoil) có bị ảnh hưởng hay lệch tâm ngắm không?
**Trả lời:**
Không. Hệ thống súng `RifleMesh` và FPS Rig được đặt làm con của `Camera_Holder` (hoặc `MainCamera`). Do `PlayerCrouch` và `PlayerSlide` chỉ can thiệp vào tọa độ cục bộ theo trục Y của `Camera_Holder` trên GameObject cha, toàn bộ cấu trúc phân cấp (Hierarchy) giữa Camera và súng được bảo toàn nguyên vẹn. Các hiệu ứng ADS (Aim Down Sights), Recoil và bắn đạn tính toán theo toạ độ địa phương của Camera nên tâm ngắm vẫn chính xác 100% ở mọi tư thế.

### Câu 13: Làm thế nào để kiểm thử bằng mắt góc nhìn thứ ba khi dự án vốn là game FPS góc nhìn thứ nhất?
**Trả lời:**
Trong script `PlayerAnimationSync.cs`, nhóm đã tích hợp sẵn tính năng Debug Visual Camera:
- Nhấn phím nóng **`V`** trong quá trình chơi thử.
- Hệ thống sẽ tự động bật camera góc nhìn thứ 3 gắn sau lưng nhân vật (`ThirdPersonViewCamera`), đồng thời kích hoạt hiển thị mô hình lính 3D `Soldier.FBX`.
- Người chơi có thể quan sát trực tiếp chuyển động ngồi thấp, bước chân bò và dáng người trượt lướt một cách chân thực nhất. Nhấn `V` lần nữa sẽ quay trở lại góc nhìn FPS chuẩn.

### Câu 14: Tại sao khi khởi tạo Collider ngồi, bạn phải đồng thời điều chỉnh cả thuộc tính `height` và `center` của CharacterController?
**Trả lời:**
Gốc toạ độ (Pivot) của nhân vật trong Unity nằm tại bàn chân ($Y = 0$). Thuộc tính `center` của CharacterController xác định vị trí khối trụ va chạm so với Pivot:
- Khi đứng ($Height = 1.8\text{m}$), $Center.y = 1.8 / 2 = 0.9\text{m}$. Đáy collider nằm đúng tại $0.9 - 0.9 = 0.0\text{m}$ (mặt đất).
- Khi ngồi ($Height = 1.0\text{m}$), nếu chỉ đổi $Height = 1.0\text{m}$ mà giữ nguyên $Center.y = 0.9\text{m}$, đáy collider sẽ bị nâng lên $0.9 - 0.5 = 0.4\text{m}$ so với mặt đất -> Nhân vật sẽ bị rơi tự do xuống đất và va đập giật màn hình!
- Do đó, bắt buộc phải đồng thời đổi $Center.y = 1.0 / 2 = 0.5\text{m}$ để đáy collider luôn luôn cố định tiếp xúc với mặt đất.

### Câu 15: Tại sao trong bài test tự động `PlayerP1VerificationTests`, một số component cần phải gọi `Awake()` hoặc `Start()` thủ công?
**Trả lời:**
Trong Unity Editor, khi tạo một GameObject mới hoặc gắn Component thông qua code trong chu kỳ Test mà không vào Play Mode, Unity không tự động gọi ngay các hàm vòng đời (`Awake`, `Start`). Các biến tham chiếu nội bộ như `_characterController` hay `_playerStamina` sẽ mang giá trị `null` nếu chưa được kích hoạt. Việc sử dụng kỹ thuật Reflection hoặc gọi phương thức khởi tạo nội bộ trong bài test đảm bảo các component được cấp phát bộ nhớ hoàn chỉnh, mô phỏng chính xác trạng thái runtime thực tế.

---

## 9. HƯỚNG DẪN TRẢI NGHIỆM TRỰC TIẾP TRONG UNITY EDITOR

Để kiểm tra và nghiệm thu thực tế tính năng P1, người dùng chỉ cần thực hiện các bước đơn giản sau:

1. **Mở Scene kiểm thử:**
   - Trong cửa sổ Project, truy cập đường dẫn: `Assets/_Project/Scenes/TestPlayerAbilities.unity`.
   - Nhấp đúp chuột để mở Scene.
2. **Bắt đầu Play Mode:**
   - Nhấn nút **Play** trên thanh công cụ Unity.
   - Hệ thống tự động kích hoạt Host bootstrap thông qua NetworkManager mà không cần thao tác kết nối phức tạp.
3. **Thao tác điều khiển phím:**
   - **WASD:** Di chuyển cơ bản quanh sàn đấu.
   - **Giữ Shift + W:** Chạy nước rút (Sprint), quan sát thanh thể lực màu lục ở góc trái màn hình tụt dần.
   - **Nhấn Ctrl (khi đứng hoặc đi chậm):** Ngồi thấp người (Crouch), quan sát Camera hạ xuống êm ái, di chuyển chậm lại. Chui vào khu vực **Hầm trần thấp (1.35m)**. Thử nhả Ctrl khi đang ở trong hầm: nhân vật vẫn giữ tư thế ngồi an toàn.
   - **Giữ Shift + W rồi nhấn Ctrl:** Kích hoạt cú trượt né tốc độ cao (**Slide**), nhân vật lướt vút về phía trước với vận tốc 9.5m/s, xuyên qua hầm thấp và tiêu hao 20 thể lực.
   - **Nhấn phím `V`:** Chuyển đổi sang góc nhìn thứ 3 (3rd Person View) để chiêm ngưỡng trực tiếp cử động animation ngồi và trượt của mô hình chiến binh Sci-Fi Soldier!
