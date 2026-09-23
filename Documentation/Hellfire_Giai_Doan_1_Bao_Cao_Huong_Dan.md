# Hellfire — Tài liệu Giai đoạn 1: Nền tảng Mạng LAN & Điều khiển nhân vật First-Person

**Dự án**: Hellfire (Co-op 2–4 players FPS Roguelite qua mạng LAN)  
**Nhánh Git**: `feature/phase1-foundation-movement-lan`  
**Engine**: Unity 6 (`6000.5.6f1`)  
**Networking**: Netcode for GameObjects (NGO `2.13.2`) + Unity Transport (UTP `2.6.0`) Direct IP  
**Input System**: Unity Input System (`1.20.0`) + uGUI (`2.5.0`)  
**Tài liệu tham chiếu**: `Hellfire_Dac_Ta_Ky_Thuat.md` (Single Source of Truth)  

---

## 1. Tổng quan Giai đoạn 1

Giai đoạn 1 tập trung hoàn toàn vào việc thiết lập nền tảng cốt lõi của trò chơi trên Unity 6, bao gồm:
1. Khởi tạo dự án, thiết lập hệ thống package và chuẩn hóa cây thư mục theo Mục 8 đặc tả.
2. Xây dựng hệ thống mạng LAN Server-Authoritative: Tạo Host, Client kết nối Direct IP, giới hạn cứng 4 người chơi, tự động quét phòng qua UDP Broadcast (`LanDiscovery.cs`), và xử lý an toàn khi mất kết nối Host.
3. Lập trình bộ điều khiển nhân vật góc nhìn thứ nhất (`PlayerMovement.cs`) dùng `CharacterController` + Unity New Input System với các thông số chuẩn xác theo Mục 1.3 đặc tả.
4. Xây dựng luồng 3 Scene (`MainMenu`, `Lobby`, `Gameplay`) và giao diện uGUI Canvas chuẩn (Mục 7 & Mục 10.1) tương thích hoàn toàn với `InputSystemUIInputModule`.

> **Giới hạn phạm vi nghiêm ngặt**: Giai đoạn 1 **hoàn toàn không** chứa logic của Giai đoạn 2–4 (không có tính sát thương đạn, không có AI quái vật, không có sinh bản đồ ngẫu nhiên, không có hệ thống rương/vật phẩm).

---

## 2. Chi tiết các thành phần kỹ thuật đã triển khai

### 2.1 Cấu hình Package & Tag / Layer

- **Package trong `Packages/manifest.json` (chuẩn Unity 6)**:
  - `com.unity.netcode.gameobjects`: `2.13.2` (Thư viện mạng NGO hỗ trợ Unity 6)
  - `com.unity.transport`: `2.6.0` (Unity Transport Protocol)
  - `com.unity.inputsystem`: `1.20.0` (New Input System)
  - `com.unity.ugui`: `2.5.0` (uGUI EventSystem)
- **Tag & Layer trong `ProjectSettings/TagManager.asset`**:
  - **Tags**: `Player`, `Enemy`, `Hitbox`.
  - **Layers**:
    - Layer 6: `PlayerBody` — Dành cho mô hình toàn thân của nhân vật (bị ẩn khỏi Camera của chính chủ sở hữu qua Culling Mask, nhưng hiển thị bình thường với các người chơi khác).
    - Layer 7: `Viewmodel` — Dành cho mô hình tay súng của chính chủ sở hữu (chỉ Camera của chính chủ mới render layer này).

---

### 2.2 Danh mục Script & Môi trường thực thi

Toàn bộ script đều tuân thủ quy ước đặt tên (Mục 11.1), có header comment ghi rõ môi trường thực thi (*Server-only / Client-only / Cả hai*), và toàn bộ thông số cân bằng đều là `[SerializeField] private` (không hardcode).

| File Script | Vị trí | Môi trường | Chức năng chính |
|---|---|---|---|
| `NetworkConnectManager.cs` | `Assets/_Project/Scripts/Networking/` | Cả hai | Quản lý vòng đời Host / Client Direct IP, duyệt kết nối tối đa 4 người qua `ConnectionApprovalCallback`, xử lý đưa Client về MainMenu khi Host ngắt kết nối (Mục 2.2 & 2.2.1). |
| `LanDiscovery.cs` | `Assets/_Project/Scripts/Networking/` | Cả hai | Tự động phát hiện phòng LAN qua UDP Broadcast (Port 47777). Host tự phát gói tin định kỳ, Client quét và nhận danh sách phòng theo thời gian thực (Mục 2.2). |
| `GameManager.cs` | `Assets/_Project/Scripts/Networking/` | Cả hai | Quản lý `NetworkVariable<GameState> CurrentState` (Server-authoritative) theo đúng enum 6 trạng thái: `Lobby`, `Generating`, `Playing`, `BossFight`, `Victory`, `GameOver` (Mục 10.2). |
| `PlayerMovement.cs` | `Assets/_Project/Scripts/Player/` | Cả hai | Điều khiển góc nhìn FPS, di chuyển WASD, chạy Shift, nhảy, trọng lực, ẩn/hiện layer body, đồng bộ vị trí qua `NetworkTransform` (Mục 1 & 1.3). |
| `MainMenuUI.cs` | `Assets/_Project/Scripts/UI/` | Client-only | Giao diện uGUI Main Menu: Tạo phòng (Host), Nhập IP vào phòng, Danh sách quét LAN, Cài đặt độ nhạy chuột, Modal thông báo ngắt kết nối (Mục 7 & 10.1). |
| `LobbyUI.cs` | `Assets/_Project/Scripts/UI/` | Cả hai | Giao diện phòng chờ: Hiển thị 4 slot người chơi, nút "Bắt đầu game" dành riêng cho Host, chuyển Scene đồng bộ qua `NetworkManager.SceneManager` (Mục 10.1). |
| `GameplayHUD.cs` | `Assets/_Project/Scripts/UI/` | Client-only | Crosshair tâm màn hình, menu tạm dừng (ESC) thoát về menu chính (Mục 7). |
| `HellfirePhase1Setup.cs` | `Assets/_Project/Editor/` | Editor-only | Menu Editor tự động tạo toàn bộ Prefab, Scene và đăng ký Build Settings bằng 1 click. |

---

### 2.3 Chi tiết Bộ điều khiển nhân vật First-Person (`PlayerMovement.cs`)

Tuân thủ nghiêm ngặt Mục 1 của đặc tả kỹ thuật:
- **Góc nhìn**: 100% First-Person (không có góc nhìn thứ 3 hoặc isometric).
- **Cấu trúc Prefab**:
  ```
  Player (Root: NetworkObject, CharacterController, PlayerMovement, NetworkTransform)
  ├── CameraPivot (y = 1.6m — trục xoay pitch)
  │   └── PlayerCamera (Camera FOV 90, AudioListener, Culling Mask ẩn PlayerBody)
  │       └── WeaponViewmodel (Layer Viewmodel, hiển thị tay + súng)
  ├── PlayerBodyMesh (Layer PlayerBody, hiển thị capsule/body cho người khác thấy)
  └── HitboxRoot (Colliders phục vụ combat Giai đoạn 2)
  ```
- **Thông số di chuyển**:
  - `walkSpeed`: 4.5 m/s
  - `sprintSpeed`: 7.0 m/s (giữ phím Left Shift)
  - `backwardMultiplier`: 0.7 (đi lùi bằng 70% tốc độ đi thường)
  - `jumpHeight`: 1.2 m
  - `gravity`: -20.0 m/s² (rơi dứt khoát theo phong cách FPS nhịp nhanh)
  - `pitchClamp`: -85° đến +85° (tránh lật ngược camera)
  - `slopeLimit`: 45°, `stepOffset`: 0.3 m, `radius`: 0.35 m, `height`: 1.8 m
  - Không có double-jump / dash (đã chốt loại khỏi bản MVP).

---

## 3. Hướng dẫn thiết lập trong Unity Editor (1-Click Setup)

Nhóm phát triển đã tích hợp sẵn công cụ tự động tạo tài nguyên:

1. Mở dự án trong **Unity Editor**.
2. Trên thanh Menu trên cùng, chọn:
   $$\text{Hellfire} \rightarrow \text{Phase 1} \rightarrow \text{Setup All (Prefabs, Scenes, Build Settings)}$$
3. Công cụ sẽ tự động:
   - Tạo Prefab `Player` tại `Assets/_Project/Prefabs/Player.prefab`.
   - Tạo Prefab `NetworkManager` tại `Assets/_Project/Prefabs/NetworkManager.prefab` (gắn kèm UTP, LanDiscovery, GameManager).
   - Tạo 3 Scene chuẩn tại `Assets/_Project/Scenes/`:
     - `MainMenu.unity`
     - `Lobby.unity`
     - `Gameplay.unity` (kèm sàn test arena 50m x 50m và các cột chướng ngại vật).
   - Đăng ký cả 3 Scene vào **Build Settings** để `NetworkSceneManager` hoạt động trơn tru.

---

## 4. Hướng dẫn kiểm thử thực tế bằng 2 máy qua mạng LAN

### 4.1 Chuẩn bị
1. **Kết nối mạng**: Kết nối Máy 1 (Host) và Máy 2 (Client) vào chung một Router Wi-Fi hoặc Switch mạng LAN.
2. **Lấy địa chỉ IP Máy 1**:
   - Trên Máy 1: Mở PowerShell/CMD, gõ `ipconfig`.
   - Tìm dòng `IPv4 Address` (ví dụ: `192.168.1.15`).
3. **Build file game**:
   - Trong Unity Editor: Vào `File -> Build Settings` $\rightarrow$ Nhấn **Build** (xuất ra thư mục `Builds/Hellfire.exe`).
   - Copy thư mục `Builds/` sang Máy 2.

---

### 4.2 Các kịch bản kiểm thử (Test Cases)

#### Kịch bản 1: Tạo phòng & Quét phòng tự động (UDP Discovery)
1. **Máy 1**: Chạy `Hellfire.exe` $\rightarrow$ Bấm **TẠO PHÒNG (HOST)** $\rightarrow$ Bấm **Host**.
   - Màn hình chuyển vào `Lobby` với hiển thị: `Slot 1: Player #0 [Chủ phòng] (Bạn)` và `Người chơi: 1/4`.
2. **Máy 2**: Chạy `Hellfire.exe` $\rightarrow$ Bấm **QUÉT PHÒNG LAN**.
   - Danh sách phòng của Máy 1 xuất hiện tự động (`Hellfire LAN Match - 192.168.1.15:7777 [1/4]`).
   - Bấm vào phòng để kết nối.
3. **Kết quả**: Cả 2 máy trong Lobby đều cập nhật `Người chơi: 2/4`.

#### Kịch bản 2: Bắt đầu game & Kiểm tra Điều khiển First-Person
1. Trên Máy 1 (Host), bấm nút **BẮT ĐẦU GAME (HOST)** (nút này chỉ hiển thị trên máy Host).
2. Cả 2 máy chuyển đồng bộ vào scene `Gameplay`.
3. **Kiểm tra**:
   - **Góc nhìn FPS**: Mỗi máy điều khiển độc lập góc nhìn thứ nhất, không thấy body của chính mình.
   - **Nhìn đối phương**: Khi nhìn sang người chơi kia, thấy rõ mô hình capsule di chuyển, xoay theo hướng họ đang nhìn.
   - **Di chuyển**: Thử nghiệm đi bộ (4.5 m/s), giữ Shift chạy nhanh (7.0 m/s), đi lùi (chậm hơn 30%), bấm Space nhảy ($1.2\text{m}$, rơi dứt khoát).
   - **Đồng bộ**: Vị trí nhân vật cập nhật mượt mà qua `NetworkTransform`.

#### Kịch bản 3: Kiểm tra giới hạn 4 người chơi
- Nếu kết nối thêm máy thứ 5 vào cùng phòng, Server sẽ từ chối kết nối qua `ConnectionApprovalCallback` và báo lý do `Phòng đã đầy (tối đa 4 người)`.

#### Kịch bản 4: Kiểm tra xử lý mất kết nối Host (Mục 2.2.1)
1. Trên Máy 1 (Host), bấm `ESC` $\rightarrow$ Chọn **Rời game** (hoặc đóng cửa sổ game).
2. **Quan sát Máy 2 (Client)**:
   - Client tự động ngắt kết nối an toàn.
   - Tự động quay về Scene `MainMenu`.
   - Hiển thị hộp thoại popup: *"Mất kết nối với chủ phòng."*

---

## 5. Bảng nghiệm thu tiêu chí Giai đoạn 1 (Checklist)

| Tiêu chí | Đặc tả kỹ thuật | Trạng thái |
|---|---|---|
| Góc nhìn nhân vật | 100% First-Person, camera tại mắt (~1.6m), pitch clamp [-85°, +85°] | ✅ Đạt |
| Hiển thị mô hình | Ẩn body với chính chủ, hiển thị với người khác (Culling Mask) | ✅ Đạt |
| Bộ điều khiển di chuyển | CharacterController + New Input System (không dùng Rigidbody) | ✅ Đạt |
| Thông số di chuyển | Đi 4.5m/s, Chạy 7.0m/s, Lùi 70%, Nhảy 1.2m, Trọng lực -20m/s² | ✅ Đạt |
| Ràng buộc di chuyển | Không có dash, không có double-jump (Mục 13.1) | ✅ Đạt |
| Kiến trúc mạng LAN | NGO + UnityTransport Direct IP (port 7777) | ✅ Đạt |
| Giới hạn người chơi | Server Connection Approval giới hạn tối đa 4 người | ✅ Đạt |
| Tự động tìm phòng | LanDiscovery qua UDP Broadcast (Port 47777) | ✅ Đạt |
| Xử lý Host rớt mạng | Không host migration; Client về MainMenu kèm thông báo | ✅ Đạt |
| Quản lý trạng thái game | GameManager đồng bộ GameState qua NetworkVariable | ✅ Đạt |
| Chuyển Scene đồng bộ | Sử dụng `NetworkManager.SceneManager` | ✅ Đạt |
| Công nghệ giao diện | 100% uGUI Canvas (không dùng UI Toolkit) | ✅ Đạt |
| Phân chia nhánh Git | Thực hiện trên `feature/phase1-foundation-movement-lan` | ✅ Đạt |

---

## 6. Kế hoạch tiếp theo (Giai đoạn 2)

Sau khi Giai đoạn 1 được xác nhận nghiệm thu, dự án sẽ chuyển sang **Giai đoạn 2 — Hoàn thiện chiến đấu**:
1. Nhánh Git mới: `feature/phase2-combat-hitscan`.
2. Hệ thống vũ khí `WeaponData` (ScriptableObject) với 4 khẩu súng MVP: Pistol, SMG, Shotgun, Rifle (Mục 6.2).
3. Cơ chế bắn tia dò tìm Hitscan Server-Authoritative (`WeaponController.cs` & `WeaponServerLogic.cs`): Client gửi RPC vị trí/hướng ngắm từ tâm camera, Server tự raycast lại tính damage, kiểm tra hitbox Head (x2.0), Torso (x1.0), Limb (x0.75) (Mục 3.1 & 3.2).
4. Đồng bộ máu `NetworkVariable<float> Health` và trạng thái `Downed` / Revive đồng đội trong 60 giây (Mục 3.3).
5. Quái vật thử nghiệm (Imp) với máy trạng thái cơ bản để test nhận sát thương và mất máu trên mạng LAN.
