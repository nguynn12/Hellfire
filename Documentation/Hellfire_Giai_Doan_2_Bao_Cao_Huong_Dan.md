# Báo Cáo Kỹ Thuật & Hướng Dẫn Thử Nghiệm Giai Đoạn 2 (Combat & Hitscan System)

Dự án: **Hellfire** (Unity 6 `6000.5.6f1` + Netcode for GameObjects `2.13.2` + UTP `2.6.0`)  
Nhánh Git: `feature/hitscan-combat`  
Tài liệu tham chiếu gốc: `Documentation/Hellfire_Dac_Ta_Ky_Thuat.md`

---

## 1. Tóm Tắt Các Tính Năng Đã Triển Khai Trong Giai Đoạn 2

Tất cả các thành phần được xây dựng bám sát 100% tài liệu đặc tả kỹ thuật:

### 1.1. Hệ thống Bắn Tia Dò Tìm (Hitscan) — Mục 3.1 & 6.2
- **Cơ chế Client-Side Prediction**: Khi bấm Chuột Trái (`Fire`), Client lập tức phát âm thanh giả lập (`AudioSource.PlayClipAtPoint` với procedural gunshot clip) và vẽ hiệu ứng tia đạn Tracer (`LineRenderer` mờ dần sau 0.05s) từ Muzzle súng đến điểm nhắm.
- **Server-Authoritative Raycast**: Client gửi `FireServerRpc(origin, direction, timestamp)`. Server thực hiện raycast độc lập từ vị trí và hướng bắn của camera, kiểm tra LayerMask (`Hitbox`, `Default`, `Environment`), xác định vật cản và mục tiêu hợp lệ. Server **không bao giờ** nhận dữ liệu sát thương do Client tự tính.
- **Quản lý Đạn Dược & Thay Đạn**: Quản lý `currentAmmo`, `reserveAmmo`, thời gian nạp đạn `reloadTime` (bấm phím `R`). Đổi 4 loại vũ khí bằng phím số `1`, `2`, `3`, `4`.

### 1.2. Hệ thống Hitbox Đa Vùng & Hệ Số Sát Thương — Mục 3.2
- **HitboxType**: Phân loại gồm `Head` (x2.0), `Torso` (x1.0), `Limb` (x0.75).
- **HitboxIdentifier**: Gắn trên các sub-collider riêng biệt của nhân vật/quái vật, chuyển tiếp sự kiện sát thương lên `IDamageable` ở Root với hệ số tương ứng.
- **IDamageable**: Interface chuẩn `TakeDamage(float damage, HitboxType hitboxType, ulong attackerId)`.

### 1.3. Đồng Bộ Máu, Trạng Thái Downed và Hồi Sinh (Revive) — Mục 2.4 & 3.3
- **Đồng bộ biến mạng (Health.cs)**:
  - `NetworkVariable<float> CurrentHealth`: Quyền ghi duy nhất thuộc về Server (`NetworkVariableWritePermission.Server`), Client đọc.
  - `NetworkVariable<bool> IsDowned`: Bật khi máu $\le 0$, kích hoạt đếm ngược xuất huyết 60 giây (`downedDuration = 60s`).
  - `NetworkVariable<bool> IsDead`: Bật khi hết 60s Downed mà không được cứu.
  - `NetworkVariable<float> DownedTimer`: Đồng bộ thời gian còn lại trước khi tử vong.
- **Cơ chế Hồi Sinh (PlayerRevive.cs)**:
  - Đồng đội đứng trong phạm vi $\le 2.0\text{m}$, nhìn vào người bị gục và giữ phím `E` liên tục trong 3.0 giây (`reviveHoldDuration = 3s`).
  - Server kiểm tra khoảng cách và tính hợp lệ trước khi gọi `ServerRevive()`, hồi lại 30% lượng máu tối đa (`reviveHealthPercent = 30%`).

### 1.4. Cơ Chế Chống Bắn Đồng Đội (Friendly Fire = OFF) — Mục 3.4
- Tia raycast của Server vẫn va chạm và bị chặn bởi Collider của đồng đội (đồng đội che chắn đường đạn, không thể bắn xuyên qua người đồng đội).
- Server kiểm tra `targetPlayer.OwnerClientId == attackerId` hoặc cùng phe người chơi để **bỏ qua việc trừ máu** (Damage = 0), đồng thời phát hiệu ứng va chạm tia đạn dừng tại vị trí đồng đội.

### 1.5. 4 Vũ Khí MVP Chuẩn Đặc Tả — Mục 6.2
Tạo 4 ScriptableObject `WeaponData` tại `Assets/_Project/ScriptableObjects/Weapons/`:
1. **Pistol (Súng lục cơ bản)**: Damage 15, Fire Rate 0.25s, Mag 12, Range 100m, Pellets 1, Semi-Auto.
2. **SMG (Tiểu liên)**: Damage 8, Fire Rate 0.08s, Mag 30, Range 60m, Pellets 1, Full-Auto.
3. **Shotgun (Súng săn)**: Damage 6/viên $\times$ 8 viên tỏa, Fire Rate 0.9s, Mag 6, Range 15m, Pellets 8, Spread 4.5 độ.
4. **Rifle (Súng trường bán tự động)**: Damage 35, Fire Rate 0.6s, Mag 10, Range 150m, Pellets 1, Semi-Auto.

### 1.6. Quái Vật Thử Nghiệm (Test Enemy Imp) — Mục 6.2
- Prefab `EnemyImp.prefab` với 30 HP, thanh máu World-Space Billboard trên đầu, 3 Collider riêng biệt cho 3 vùng Hitbox:
  - **Head Collider** (Sphere trên đầu): Nhận sát thương x2.0 $\rightarrow$ 1 phát Pistol (15 $\times$ 2 = 30 DMG) là tiêu diệt ngay lập tức.
  - **Torso Collider** (Capsule phần thân): Nhận sát thương x1.0 $\rightarrow$ 2 phát Pistol (15 $\times$ 2 = 30 DMG) để tiêu diệt.
  - **Limbs Collider** (Box phần chân/tay): Nhận sát thương x0.75 (11.25 DMG) $\rightarrow$ 3 phát Pistol để tiêu diệt.
- Khi bị bắn: Quái nhấp nháy đỏ (`Material flash`), cập nhật thanh máu trên đầu cho toàn bộ người chơi thấy; khi chết thì ngã gục và tự hủy biến mất sau 3 giây.

### 1.7. Giao Diện Người Chơi (GameplayHUD.cs)
- Hiển thị thanh máu (HP Bar) và chỉ số số `100 / 100`.
- Hiển thị tên vũ khí hiện tại, số đạn còn lại trong băng và tổng đạn dự trữ (`12 / 60`).
- Hiển thị tâm ngắm Crosshair và hiệu ứng Hitmarker nhấp nháy khi bắn trúng mục tiêu.
- Khi bị Downed: Hiển thị màn hình đỏ cảnh báo và đồng hồ đếm ngược `60s`.
- Khi tiến hành hồi sinh đồng đội: Hiển thị thanh tiến trình giữ phím `E` (0% - 100%).

---

## 2. Hướng Dẫn Thiết Lập Tự Động 1-Click Trong Unity Editor

1. Mở dự án trên **Unity Editor (6000.5.6f1)**.
2. Trên thanh menu trên cùng của Unity, chọn:
   ```text
   Hellfire -> Phase 2 -> Setup All (Weapons, Player Combat, Imp Enemy, Gameplay Scene)
   ```
3. Công cụ tự động thực hiện:
   - Tạo thư mục và sinh 4 asset vũ khí ScriptableObject (`Pistol.asset`, `SMG.asset`, `Shotgun.asset`, `Rifle.asset`).
   - Cập nhật `Player.prefab`: Thêm các component `Health`, `WeaponController`, `PlayerRevive`, các Hitbox Colliders (Head, Torso, Limbs), AudioSource, và liên kết các vũ khí mặc định.
   - Tạo `EnemyImp.prefab` với đầy đủ Collider, `TestEnemyImp`, thanh máu trên đầu và đăng ký vào `NetworkPrefabsList`.
   - Nạp sẵn 4 quái vật Imp vào các vị trí khác nhau trong scene `Gameplay.unity` để làm bia bắn thử nghiệm.
   - Thiết lập `GameplayHUD` hoàn chỉnh trong Canvas của scene `Gameplay.unity`.

---

## 3. Hướng Dẫn Chi Tiết Kiểm Thử LAN (2 - 4 Máy / Build Thử Nghiệm)

### Cách 1: Test Nhanh Trên 1 Máy (Unity Editor + Standalone Build)
1. Trong Unity Editor, chọn `File -> Build Profiles` (hoặc `Build Settings`), nhấn **Build** ra thư mục `Build/Hellfire.exe`.
2. Chạy file `Build/Hellfire.exe` (đóng vai trò Client 1).
3. Trong Unity Editor, mở scene `MainMenu.unity`, bấm nút **Play** (▶) (đóng vai trò Host).
4. **Host**: Nhập Tên phòng $\rightarrow$ Nhấn **Tạo Phòng (Host)** $\rightarrow$ Trong sảnh Lobby nhấn **Bắt Đầu Trận Đấu**.
5. **Client 1**: Nhập IP `127.0.0.1` $\rightarrow$ Nhấn **Tham Gia (Client)** (hoặc dùng nút Tìm Phòng Tự Động LAN).

---

### Cách 2: Test Mạng Cục Bộ LAN (2 - 4 Máy Tính Khác Nhau)
1. Đảm bảo các máy tính cùng kết nối chung một mạng Wi-Fi hoặc mạng LAN Switch (cùng dải IP ví dụ `192.168.1.x`).
2. Tắt hoặc cho phép Windows Defender Firewall cho ứng dụng `Hellfire.exe` qua cổng UDP `7777`.
3. Máy 1 tạo phòng (Host). Các máy 2, 3, 4 nhấn nút **Tìm Phòng Tự Động (LAN Discovery)** hoặc nhập trực tiếp địa chỉ IPv4 của Máy 1 rồi bấm **Tham Gia**.
4. Khi đủ người, Máy 1 bấm **Bắt Đầu Trận Đấu**. Cả 2-4 máy cùng chuyển vào màn chơi `Gameplay`.

---

## 4. Kịch Bản Kiểm Thử Cần Xác Minh

### Kịch bản 1: Kiểm thử Bắn Hitscan & 4 Loại Vũ Khí
- Bấm phím `1` (Pistol), `2` (SMG), `3` (Shotgun), `4` (Rifle) để chuyển đổi vũ khí.
- Bắn vào tường/môi trường: Tia đạn hiển thị chính xác từ nòng súng tới điểm ngắm tâm camera, đạn giảm đúng số lượng, bấm `R` để nạp đạn khi hết băng.

### Kịch bản 2: Kiểm thử Hitbox & Hệ Số Sát Thương Trên Quái Imp (30 HP)
- **Headshot**: Dùng Pistol bắn vào đầu Imp $\rightarrow$ Imp nhận 30 DMG ($15 \times 2.0$) và chết ngay chỉ sau **1 phát bắn**.
- **Torsoshot**: Dùng Pistol bắn vào thân Imp $\rightarrow$ Imp nhận 15 DMG ($15 \times 1.0$), thanh máu trên đầu tụt còn 50%, bắn phát thứ 2 là Imp chết (**2 phát bắn**).
- **Limbshot**: Dùng Pistol bắn vào chân Imp $\rightarrow$ Imp nhận 11.25 DMG ($15 \times 0.75$), cần bắn **3 phát** mới tiêu diệt được.
- Tất cả người chơi khác trong phòng đều nhìn thấy thanh máu của Imp tụt tức thì theo thời gian thực và thấy Imp ngã gục khi chết.

### Kịch bản 3: Kiểm thử Chống Bắn Đồng Đội (Friendly Fire = OFF)
- Cho Player A bắn thẳng vào người Player B:
  - Tia đạn dừng lại tại người của Player B (không bay xuyên qua).
  - Máu của Player B trên máy Player B và trên máy Host **không bị trừ** (vẫn giữ nguyên 100 HP).

### Kịch bản 4: Kiểm thử Trạng Thái Gục (Downed) & Hồi Sinh (Revive)
- Cho Player A nhận sát thương đến khi hết máu:
  - Player A chuyển sang trạng thái Downed (nằm gục, không thể di chuyển WASD, màn hình đỏ xuất hiện đếm ngược 60 giây).
  - Player B tiến lại gần Player A ($\le 2\text{m}$), nhìn vào Player A và giữ phím `E`:
  - Thanh tiến trình Revive trên màn hình Player B chạy từ 0% đến 100% trong vòng 3 giây.
  - Sau 3 giây, Player A đứng dậy trở lại trạng thái bình thường với 30 HP (30% máu tối đa).
