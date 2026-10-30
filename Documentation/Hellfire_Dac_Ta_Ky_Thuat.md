# Hellfire — Đặc tả kỹ thuật (Technical Specification)

> **Phạm vi áp dụng (quan trọng)**: Tài liệu này là đặc tả RIÊNG của chủ dự án, dùng để điều khiển AI lập trình (Antigravity). Các thành viên khác trong nhóm (Phước, Benz2311...) KHÔNG bị ràng buộc bởi nó và có thể tự thêm tính năng mới. Code do họ viết được coi là **phần được tích hợp**, không phải lỗi vi phạm đặc tả. AI KHÔNG ĐƯỢC hoàn nguyên hay viết lại chúng; việc của AI là làm cho chúng tương thích với hệ thống hiện có (xem mục 19).

**Mục đích tài liệu**: Đây là tài liệu tham chiếu kỹ thuật duy nhất (single source of truth) cho dự án. Mọi lập trình viên hoặc AI hỗ trợ code khi đọc tài liệu này phải hiểu chính xác cơ chế cần cài đặt mà không cần suy đoán thêm. Nếu một chi tiết không có trong tài liệu này, đó là chi tiết chưa được quyết định — không tự bịa ra cách làm, hãy hỏi lại hoặc đánh dấu `TODO`.

**Phiên bản tài liệu**: 1.0
**Engine**: Unity 3D — khuyến nghị Unity 6 LTS hoặc Unity 2022.3 LTS trở lên.
**Networking**: Netcode for GameObjects (NGO) + Unity Transport (UTP), chế độ LAN trực tiếp (Direct IP), không dùng Relay/dedicated server.

---

## 0. Quy ước chung của tài liệu

- Từ khóa **BẮT BUỘC**, **KHÔNG ĐƯỢC**, **PHẢI** dùng để chỉ ràng buộc cứng, không được thay đổi khi code.
- Từ khóa **NÊN** dùng để chỉ khuyến nghị, có thể điều chỉnh nếu có lý do kỹ thuật chính đáng, nhưng phải ghi chú lại lý do trong code (comment).
- Tên class/script trong tài liệu là tên NÊN dùng để giữ nhất quán giữa các module do 3 người viết riêng.
- Đơn vị đo lường: mét (Unity unit = 1 mét), tốc độ tính bằng mét/giây, thời gian tính bằng giây, góc tính bằng độ.

---

## 1. Góc nhìn và điều khiển nhân vật (Camera & Character Controller)

### 1.1 Góc nhìn — QUYẾT ĐỊNH CUỐI CÙNG

**BẮT BUỘC: Góc nhìn thứ nhất (First-Person) — KHÔNG phải góc nhìn thứ ba, KHÔNG phải nhìn nghiêng từ trên xuống (isometric/top-down).**

> ⚠️ Lưu ý cho người/AI đọc tài liệu: bản phân tích ban đầu của dự án có đề cập "góc nhìn thứ ba qua vai hoặc nhìn nghiêng từ trên xuống" — chi tiết đó **đã bị loại bỏ và thay thế hoàn toàn** bởi quyết định này. Không cài đặt lại cơ chế camera thứ ba hoặc top-down dưới bất kỳ hình thức nào, kể cả làm "tùy chọn" hay "dự phòng".

- Camera gắn cố định tại vị trí mắt nhân vật (eye position), là con (child object) của nhân vật, xoay theo trục dọc (yaw) cùng thân nhân vật và xoay theo trục ngang (pitch) độc lập.
- **KHÔNG BẬT hiển thị mô hình toàn thân nhân vật của chính người chơi trong camera** — chỉ hiển thị mô hình tay súng (viewmodel/arms) gắn riêng, render ở layer camera riêng để tránh clip vào tường.
- Người chơi khác trong multiplayer PHẢI thấy mô hình toàn thân (full-body) của người chơi này bình thường — full-body model chỉ ẩn với chính chủ nhân vật đó (dùng layer culling mask trên camera, không dùng cách destroy/disable object).

### 1.2 Cấu trúc GameObject nhân vật

```
Player (root, có NetworkObject + CharacterController)
├── CameraPivot (điểm xoay pitch, đặt tại ~1.6m tính từ chân)
│   └── PlayerCamera (Camera, Field of View mặc định 90 độ, người chơi chỉnh được 70–110 trong Settings theo mục 16.9, chỉ active trên client sở hữu — IsOwner)
│       └── WeaponViewmodel (model tay + súng, layer "Viewmodel", chỉ camera của chủ sở hữu render layer này)
├── PlayerBodyMesh (mô hình toàn thân, layer "PlayerBody", camera chủ sở hữu KHÔNG render layer này, camera người khác PHẢI render)
├── HitboxRoot (các collider để tính trúng đạn: Head, Torso, Limbs — xem mục 3)
└── AudioSources (chân bước, bắn súng, ...)
```

### 1.3 Cơ chế di chuyển (Movement)

**BẮT BUỘC dùng `CharacterController` của Unity (không dùng Rigidbody + physics-based movement)** để đảm bảo di chuyển ổn định, dễ dự đoán qua mạng, tránh vật lý gây trượt/nảy không mong muốn.

Script: `PlayerMovement.cs` (chạy trên mỗi client cho nhân vật của chính họ — client-side prediction; xem mục 2 về mô hình quyền trên mạng).

Thông số di chuyển mặc định (đặt làm `[SerializeField]` để dễ chỉnh cân bằng sau, KHÔNG hardcode):

| Thông số | Giá trị mặc định | Ghi chú |
|---|---|---|
| Tốc độ đi thường (walkSpeed) | 4.5 m/s | |
| Tốc độ chạy (sprintSpeed) | 7.0 m/s | Giữ phím Shift để chạy |
| Tốc độ đi lùi | bằng 70% walkSpeed | Đi lùi luôn chậm hơn |
| Chiều cao nhảy (jumpHeight) | 1.2 m | Tính bằng công thức `v = sqrt(2 * g * h)` |
| Trọng lực (gravity) | -20 m/s² | Nặng hơn trọng lực Trái Đất thực (-9.8) để cảm giác bấm nút nhanh, dứt khoát — đặc trưng game bắn súng nhịp nhanh |
| Bán kính CharacterController | 0.35 m | |
| Chiều cao CharacterController | 1.8 m | |
| Góc dốc tối đa đi được (slopeLimit) | 45 độ | |
| Độ cao bậc thang bước qua được (stepOffset) | 0.3 m | |
| Độ nhạy chuột (mouseSensitivity) | 2.0 (đơn vị tùy chỉnh) | PHẢI có trong menu Settings để người chơi tự chỉnh |
| Giới hạn góc nhìn lên/xuống (pitch clamp) | -85 độ đến +85 độ | Tránh lật ngược camera |

**Input System**: BẮT BUỘC dùng Unity **Input System** package (không dùng Input Manager cũ), để dễ hỗ trợ rebind phím sau này.

Luồng xử lý di chuyển mỗi frame (trong `Update()` hoặc `FixedUpdate()` tùy quyết định kỹ thuật cụ thể lúc code, nhưng PHẢI nhất quán):

1. Đọc input trục di chuyển (WASD → Vector2), input nhìn chuột (Vector2 delta).
2. Xoay `Player` root theo trục Y (yaw) bằng input chuột ngang.
3. Xoay `CameraPivot` theo trục X (pitch) bằng input chuột dọc, clamp trong khoảng đã định.
4. Tính vector di chuyển theo hướng local (forward/right của Player root) nhân với tốc độ tương ứng (đi/chạy/lùi).
5. Áp dụng trọng lực cộng dồn vào trục Y khi không chạm đất (`CharacterController.isGrounded == false`).
6. Gọi `CharacterController.Move(finalVector * Time.deltaTime)`.
7. Xử lý nhảy: chỉ cho nhảy khi `isGrounded == true`, set vận tốc Y tức thời bằng công thức chiều cao nhảy ở trên.

**KHÔNG ĐƯỢC** cho phép nhảy liên tục trên không (double jump) trừ khi có bùa lợi (power-up) cụ thể được thiết kế cho phép — mặc định là không.

---

## 2. Kiến trúc mạng (Networking Architecture)

### 2.1 Mô hình quyền hạn — QUYẾT ĐỊNH BẮT BUỘC

**BẮT BUỘC dùng mô hình Server-Authoritative (máy chủ có toàn quyền quyết định trạng thái game).** Máy chủ (host, tức 1 trong 4 người chơi đóng vai trò Host trong NGO) là nguồn chân lý duy nhất cho:

- Vị trí và máu của quái vật.
- Sát thương gây ra (server tính toán lại, không tin số liệu client gửi lên).
- Trạng thái vật phẩm rơi ra, rương mở hay chưa.
- Trạng thái mở khóa cửa, chuyển màn.

Client (bao gồm cả client chạy trên máy Host) chỉ có quyền:

- Gửi **input** của người chơi (không gửi kết quả, không tự tính sát thương final).
- Dự đoán (predict) chuyển động của chính nhân vật mình để cảm giác mượt (client-side prediction), nhưng vị trí cuối PHẢI được reconcile theo dữ liệu do server xác nhận nếu lệch quá ngưỡng (khuyến nghị dùng `NetworkTransform` với Interpolation bật sẵn của NGO cho các object không phải nhân vật do client điều khiển).
- Hiển thị hiệu ứng hình ảnh/âm thanh ngay lập tức phía client để giảm cảm giác trễ (ví dụ: tracer đường đạn, âm thanh súng nổ hiện ngay khi bấm chuột, không đợi server xác nhận) — nhưng sát thương thực tế vẫn phải chờ server xác nhận.

### 2.1.1 Ngoại lệ DUY NHẤT: di chuyển người chơi là Owner-Authoritative — ĐÃ CHỐT

**Lý do**: `PlayerMovement` cho chủ sở hữu di chuyển cục bộ bằng `CharacterController` và không gửi input lên Server. Nếu `NetworkTransform` của Player ở chế độ Server-authoritative (mặc định), Server và các máy khác sẽ không bao giờ thấy client di chuyển (đứng yên ở điểm spawn). Vì game chỉ chơi LAN giữa bạn bè (không có yếu tố gian lận cạnh tranh), dự án chọn để **chủ sở hữu quyết định vị trí nhân vật của chính mình**.

Quy định BẮT BUỘC:

1. `NetworkTransform` trên **root của `Player.prefab`** PHẢI đặt `AuthorityMode = Owner`. Mọi `NetworkTransform` khác (quái vật, boss, vật phẩm rơi) GIỮ NGUYÊN `AuthorityMode = Server`.
2. Ngoại lệ này **CHỈ áp dụng cho vị trí/góc xoay của nhân vật người chơi**. Mọi thứ còn lại vẫn theo mục 2.1: máu, sát thương, trạng thái Downed/Dead, revive, AI, vật phẩm, rương, `GameState`.
3. **Dịch chuyển người chơi bởi Server** (ví dụ đưa về phòng spawn): Server KHÔNG được gán trực tiếp `transform.position` của người chơi (vì Server không còn là authority của transform này). Thay vào đó, Server gọi một `ClientRpc` có `ClientRpcParams` nhắm đúng `TargetClientIds = { clientId của chủ nhân }`; trên máy chủ nhân, handler PHẢI theo đúng thứ tự: (a) tắt `CharacterController`, (b) đặt `transform.position`/`rotation`, (c) gọi `NetworkTransform.Teleport(...)` để các máy khác nhảy ngay tới vị trí mới thay vì nội suy, (d) bật lại `CharacterController`. Bỏ bước (a) sẽ làm `CharacterController` ghi đè vị trí vừa đặt.
4. Với `ServerRpc` bắn súng (mục 3.1), Server **NÊN** kiểm tra origin do client gửi lên cách vị trí người chơi đó trên Server không quá 3 mét (giá trị `[SerializeField]`), nếu quá thì bỏ qua phát bắn. Đây là kiểm tra hợp lý rẻ tiền, không phải chống gian lận toàn diện.
5. Các kiểm tra khoảng cách do Server thực hiện (revive trong 2m, mở rương, nhặt vật phẩm, AI chọn người chơi gần nhất) dùng vị trí người chơi mà Server nhận được qua `NetworkTransform` — sau thay đổi này các giá trị đó mới chính xác với mọi client.

**Tiêu chí nghiệm thu (test bằng 2 máy hoặc 2 instance)**: Client đi một vòng → màn hình Host thấy nhân vật Client di chuyển tương ứng; quái chọn đúng Client làm mục tiêu khi Client gần nhất; Client đứng cạnh đồng đội đang gục giữ phím revive thì hồi sinh được.

### 2.2 Thiết lập kết nối LAN

- Dùng `UnityTransport` với chế độ `Direct IP` — người tạo phòng chọn "Host", các người chơi khác nhập địa chỉ IP LAN của Host để "Join" (client-server model tiêu chuẩn của NGO, Host = server + client cùng lúc).
- **NÊN** viết thêm một script `LanDiscovery.cs` dùng `UdpClient` để Host tự động broadcast thông tin phòng (tên phòng, IP, số người hiện tại/tối đa) trong LAN, và client có thể quét (scan) danh sách phòng thay vì phải gõ tay địa chỉ IP. Đây không phải bắt buộc cho bản MVP nhưng NÊN làm sớm vì cải thiện trải nghiệm rất nhiều.
- Cổng mạng (port) mặc định: `7777` (chuẩn của UTP), NÊN cho phép đổi trong Settings để tránh xung đột cổng khi test nhiều phòng cùng lúc trên cùng mạng LAN.
- Số người chơi tối đa: 4. **BẮT BUỘC** giới hạn cứng số slot kết nối ở phía server (`NetworkManager.ConnectionApprovalCallback`), từ chối kết nối thứ 5 trở lên.

### 2.2.1 Host migration — ĐÃ CHỐT: KHÔNG LÀM (Phương án A)

**Quyết định cuối cùng: dự án KHÔNG cài đặt host migration.** Nếu máy Host bị ngắt kết nối (rớt mạng, thoát ứng dụng, crash) giữa lượt chơi, hành vi bắt buộc là:

1. `NetworkManager` phát sự kiện `OnClientDisconnectCallback`/`OnServerStopped` (tùy phiên bản NGO) trên các client còn lại.
2. Toàn bộ client còn lại **BẮT BUỘC** hiển thị màn hình thông báo "Mất kết nối với chủ phòng" và tự động điều hướng về Main Menu (xem mục 10 — Game Flow).
3. **KHÔNG ĐƯỢC** cố gắng giữ lại trạng thái game (máu, vị trí, vật phẩm đã nhặt) sau sự cố này — lượt chơi coi như hủy, người chơi phải tạo phòng mới và bắt đầu lại từ đầu (seed mới, mục 2.3).
4. **KHÔNG code bất kỳ cơ chế promote client thành host mới, không lưu snapshot trạng thái để khôi phục.** Đây là giới hạn phạm vi có chủ đích (out of scope), không phải thiếu sót — nếu một AI/lập trình viên đọc thấy code liên quan đến "host migration" hoặc "server failover" trong yêu cầu tương lai, cần xác nhận lại với nhóm trước khi làm vì nó đi ngược quyết định này.

### 2.3 Đồng bộ bản đồ sinh ngẫu nhiên — CƠ CHẾ BẮT BUỘC

**KHÔNG ĐƯỢC** gửi toàn bộ dữ liệu hình học bản đồ (mesh, vị trí từng phòng...) qua mạng dưới dạng object đã dựng sẵn theo kiểu spawn hàng loạt không kiểm soát.

**Cơ chế BẮT BUỘC dùng — Seed đồng bộ**:

1. Khi Host bắt đầu một lượt chơi mới, Host tự sinh một số nguyên ngẫu nhiên `int dungeonSeed` (ví dụ dùng `System.Random` hoặc `UnityEngine.Random` với seed gốc lấy từ thời gian hệ thống).
2. Host gửi `dungeonSeed` này cho tất cả client qua một `NetworkVariable<int>` hoặc một `ClientRpc` gọi một lần khi bắt đầu màn.
3. **Mỗi máy (kể cả Host) tự chạy lại đúng cùng một thuật toán sinh bản đồ (xem mục 4) với cùng seed đó** để tự dựng bản đồ cục bộ — bản đồ sẽ giống hệt nhau trên mọi máy vì thuật toán là deterministic (tất định) khi cùng seed.
4. Thuật toán sinh bản đồ **BẮT BUỘC** không được gọi bất kỳ hàm ngẫu nhiên nào không dùng seed đã cho (ví dụ không dùng `Random.Range` không seed ở giữa quá trình sinh phòng) — nếu không, bản đồ sẽ lệch nhau giữa các máy.
5. Vị trí spawn của quái vật, rương vật phẩm ban đầu cũng PHẢI được sinh từ cùng seed/luồng ngẫu nhiên đó để đồng bộ, HOẶC do Host quyết định và gửi qua `ClientRpc`/`NetworkObject` spawn chuẩn của NGO (khuyến nghị dùng cách này cho object có network behaviour như quái vật, để dễ đồng bộ máu/AI về sau — chỉ riêng **hình học bản đồ tĩnh** như phòng/hành lang mới dùng cơ chế seed ở trên).

### 2.4 Đồng bộ chiến đấu (máu, sát thương)

- Máu (`Health`) của mỗi nhân vật và quái vật **BẮT BUỘC** là `NetworkVariable<float>`, chỉ Server được quyền ghi (`NetworkVariableWritePermission.Server`).
- Khi client bắn trúng mục tiêu (xem mục 3), client gửi `ServerRpc` báo "tôi đã bắn, từ vị trí X, hướng Y, vào lúc thời điểm Z" — **KHÔNG gửi thẳng "tôi đã gây bao nhiêu sát thương"**. Server tự raycast lại để xác nhận và tự tính sát thương, tránh gian lận (cheat).
- Sau khi Server tính xong, Server cập nhật `NetworkVariable<float> Health` — giá trị này tự động đồng bộ xuống mọi client qua cơ chế có sẵn của NGO, không cần tự viết thêm RPC đồng bộ máu.
- Hiệu ứng hình ảnh khi trúng đạn (máu bắn ra, số sát thương hiện lên) **NÊN** được server gọi `ClientRpc` để phát cho tất cả client cùng thấy đồng thời.

---

## 3. Cơ chế chiến đấu (Combat System)

### 3.1 Bắn tia dò tìm (Hitscan)

Script: `WeaponController.cs` (client-side, gắn trên viewmodel), phối hợp với `WeaponServerLogic.cs` hoặc tương đương chạy server-side.

Luồng xử lý khi người chơi bấm chuột trái (bắn):

1. **Client**: Kiểm tra điều kiện bắn được (còn đạn trong băng > 0, không đang nạp đạn, hết thời gian hồi giữa 2 phát `fireRate`).
2. **Client**: Phát ngay hiệu ứng cục bộ (muzzle flash particle, âm thanh súng nổ, animation giật súng, vẽ tracer tạm thời) để không có độ trễ cảm nhận — đây là hiệu ứng "cosmetic", không quyết định kết quả game.
3. **Client**: Raycast từ vị trí nòng súng (hoặc từ tâm camera, xem lưu ý bên dưới) theo hướng camera đang nhìn, khoảng cách tối đa = `weaponRange` (mặc định 100m).
4. **Client**: Gửi `ServerRpc` chứa: origin (vị trí bắn), direction (hướng bắn), timestamp, weaponId.
5. **Server**: Nhận RPC, tự raycast lại độc lập bằng chính dữ liệu origin/direction nhận được (KHÔNG tin trực tiếp "đã trúng ai" mà client tự báo, để chống cheat aimbot/wallhack gửi sai dữ liệu).
6. **Server**: Nếu raycast trúng object có tag "Enemy" hoặc "Player" (friendly fire — xem 3.4), lấy `HitboxType` tại điểm trúng (Head/Torso/Limb — xem mục 3.2), tính sát thương = `baseDamage * hitboxMultiplier`.
7. **Server**: Trừ máu qua `NetworkVariable`, kiểm tra máu ≤ 0 → xử lý chết (mục 3.3).
8. **Server**: Gọi `ClientRpc` để mọi máy phát hiệu ứng máu bắn ra tại điểm trúng thật (theo raycast của server, không theo raycast client để tránh hiển thị sai vị trí trúng nếu có lag).

> **Lưu ý kỹ thuật quan trọng**: Điểm gốc raycast dùng để TÍNH SÁT THƯƠNG PHẢI là từ **tâm camera** (không phải từ nòng súng vật lý trên model), để đảm bảo "ngắm giữa màn hình là trúng đúng chỗ ngắm" — đây là chuẩn phổ biến của game bắn súng góc nhìn thứ nhất. Vệt sáng tracer thị giác thì NÊN vẽ từ nòng súng model đến điểm trúng, để nhìn tự nhiên — tức là điểm bắt đầu của logic và điểm bắt đầu của hiệu ứng hình ảnh có thể khác nhau, đây là chủ đích, không phải lỗi.

### 3.2 Hitbox và hệ số sát thương

`HitboxRoot` của mỗi nhân vật/quái vật có các collider con, mỗi collider gắn script `HitboxIdentifier.cs` với enum `HitboxType { Head, Torso, Limb }`.

Hệ số sát thương nhân thêm mặc định (điều chỉnh trong file cấu hình vũ khí, không hardcode):

| Vị trí trúng | Hệ số nhân |
|---|---|
| Đầu (Head) | x2.0 |
| Thân (Torso) | x1.0 |
| Tay/chân (Limb) | x0.75 |

### 3.3 Xử lý khi nhân vật/quái vật chết

- Khi `Health <= 0` (kiểm tra trên Server): Server gọi `ClientRpc` để phát animation/hiệu ứng chết trên mọi máy, disable collider và movement, KHÔNG destroy NetworkObject ngay lập tức (để animation chết kịp phát), sau đó despawn theo thời gian trễ (NÊN 3–5 giây với quái vật, xem mục 3.4 riêng cho người chơi).
- **Người chơi chết** (theo mục 1, "vòng lặp trò chơi"): Nếu TOÀN BỘ người chơi trong phòng đều chết → toàn bộ lượt chơi thất bại, quay lại màn hình chờ, lượt chơi tiếp theo sinh bản đồ mới hoàn toàn (seed mới). Nếu chỉ một số người chết còn người khác sống → áp dụng cơ chế **hồi sinh đồng đội (revive) — ĐÃ CHỐT: BẬT.**

**Cơ chế revive — QUYẾT ĐỊNH CUỐI CÙNG:**

1. Khi `Health <= 0` trên Server, nhân vật **KHÔNG** vào thẳng trạng thái loại khỏi lượt chơi, mà chuyển sang trạng thái **`Downed` (gục)** — đây là một `NetworkVariable<bool> IsDowned` hoặc thêm state vào enum trạng thái nhân vật, do Server ghi.
2. Khi vào trạng thái `Downed`: vô hiệu hóa di chuyển và bắn súng của người chơi đó (client vẫn nhận input nhưng Server bỏ qua/không xử lý), phát animation gục xuống, nhân vật vẫn hiển thị trên map cho đồng đội thấy.
3. Nhân vật `Downed` có thời gian giới hạn để được cứu — **ĐÃ CHỐT: mặc định 60 giây** (`downedDuration`, để `[SerializeField]` chỉnh cân bằng sau khi playtest — xem Giai đoạn 5, tài liệu Phân tích & Lộ trình). Hết thời gian mà chưa được cứu → chuyển hẳn sang trạng thái `Dead` (loại khỏi lượt chơi cho đến khi qua màn hoặc toàn đội thất bại).
4. Đồng đội còn sống lại gần (trong bán kính tương tác, ví dụ 2m) nhân vật đang `Downed`, giữ phím tương tác trong khoảng thời gian cố định — **ĐÃ CHỐT: 3 giây** (`reviveHoldDuration`) để hồi sinh.
5. Luồng mạng: Client (người đi cứu) gửi `ServerRpc` báo đang giữ phím cứu + ID của nhân vật đang gục → Server xác nhận điều kiện hợp lệ (đúng khoảng cách, đúng trạng thái `Downed`, không bị gián đoạn giữa chừng — nếu người cứu di chuyển ra xa hoặc bị trúng đòn thì hủy tiến trình cứu) → sau khi đủ `reviveHoldDuration`, Server chuyển nhân vật gục về trạng thái bình thường, hồi máu về **% máu giới hạn — ĐÃ CHỐT: 30% máu tối đa** (`reviveHealthPercent`) → cập nhật `NetworkVariable<float> Health` và `IsDowned = false`.
6. **KHÔNG ĐƯỢC** cho phép tự hồi sinh chính mình (một người không thể vừa gục vừa giữ phím cứu chính mình).
7. **KHÔNG ĐƯỢC** để client tự quyết định đã cứu xong rồi báo Server hồi máu — toàn bộ điều kiện thời gian giữ phím và xác nhận hồi sinh PHẢI được Server tự tính và xác thực, tương tự nguyên tắc server-authoritative ở mục 2.1.

### 3.4 Friendly fire (bắn đồng đội) — ĐÃ CHỐT: TẮT

**Quyết định cuối cùng: Friendly fire TẮT.** Đạn hitscan (mục 3.1) khi Server raycast trúng object có tag "Player" thuộc cùng đội (co-op, cả 4 người chơi luôn cùng 1 đội — game không có PvP) **PHẢI bỏ qua, không trừ máu, không xử lý sát thương**, nhưng tia raycast vẫn tính là "trúng" về mặt vật lý (tức đạn không xuyên qua tiếp để trúng quái vật đứng sau lưng đồng đội — nói cách khác, đồng đội vẫn chặn đường bắn về mặt hình học/collision, chỉ là không nhận sát thương). Đây là điểm cần lưu ý khi code: kiểm tra "là đồng đội thì bỏ qua sát thương" phải nằm ở bước tính damage, không phải ở bước raycast (raycast vẫn dừng lại khi trúng đồng đội như bình thường).

---

## 4. Sinh bản đồ ngẫu nhiên (Procedural Dungeon Generation)

### 4.1 Thuật toán — QUYẾT ĐỊNH KỸ THUẬT

**BẮT BUỘC** thuật toán phải là **deterministic** (tất định): với cùng một `seed` đầu vào, PHẢI luôn sinh ra chính xác cùng một kết quả, chạy được độc lập trên nhiều máy khác nhau mà không cần trao đổi thêm dữ liệu (xem mục 2.3).

Đề xuất thuật toán cụ thể — **Room-and-Corridor dựa trên lưới (Grid-based room placement)**:

1. Dùng `System.Random rng = new System.Random(dungeonSeed)` — **KHÔNG dùng `UnityEngine.Random.InitState()` cho phần logic sinh bản đồ** vì `System.Random` cho kết quả nhất quán, dễ kiểm soát hơn giữa các phiên bản Unity/platform khác nhau (Unity Random có thể có khác biệt nhỏ giữa các build target trong một số trường hợp).
2. Định nghĩa lưới 2D kích thước cố định (ví dụ 20x20 ô, mỗi ô đại diện 1 phòng tiềm năng, kích thước thật mỗi ô ví dụ 15m x 15m).
3. Sinh số lượng phòng ngẫu nhiên trong khoảng `[minRoomCount, maxRoomCount]` (ví dụ 8–14 phòng) bằng `rng`.
4. Đặt phòng đầu tiên (phòng xuất phát/spawn) tại vị trí cố định hoặc gần trung tâm lưới.
5. Lặp: chọn một phòng đã đặt, chọn hướng ngẫu nhiên (Bắc/Nam/Đông/Tây) bằng `rng`, đặt phòng mới liền kề nếu ô đó còn trống, nối 2 phòng bằng một hành lang (corridor prefab).
6. Sau khi đủ số phòng, chọn 1 phòng xa nhất tính theo đường đi (BFS/Dijkstra trên lưới) so với phòng spawn làm **phòng trùm cuối (boss room)**.
7. **(ĐÃ THAY THẾ bởi mục 17.1)** Gán loại phòng Spawn / Combat / Upgrade / Treasure / Shop / Boss theo quy tắc ở mục 17.1. Không còn khái niệm "phòng có rương 30%".
8. Sau khi có sơ đồ logic (dữ liệu 2D, chưa phải GameObject), mới tiến hành **Instantiate** prefab phòng/hành lang tương ứng vào đúng vị trí thế giới thực (world position) — bước dựng hình học này chạy độc lập trên từng máy, dùng cùng dữ liệu logic đã sinh từ cùng seed nên kết quả hình học giống hệt nhau.

### 4.2 Bake NavMesh lúc chạy (Runtime NavMesh Baking)

**BẮT BUỘC** dùng gói `AI Navigation` (NavMeshComponents chính thức của Unity, hỗ trợ bake runtime qua `NavMeshSurface.BuildNavMesh()`).

- Sau khi toàn bộ hình học bản đồ (phòng + hành lang) đã Instantiate xong trên một máy, máy đó gọi `NavMeshSurface.BuildNavMesh()` để bake lại NavMesh cho đúng bản đồ vừa dựng.
- **NÊN** dùng `NavMeshSurface.UpdateNavMesh()` hoặc chạy bake trên luồng phụ/bất đồng bộ nếu Unity version hỗ trợ, để tránh đứng hình (freeze frame) khi bản đồ lớn — xem rủi ro đã nêu ở tài liệu phân tích.
- **BẮT BUỘC** mỗi máy tự bake NavMesh cục bộ của riêng mình sau khi tự dựng xong bản đồ từ seed — **KHÔNG** truyền dữ liệu NavMesh đã bake qua mạng, vì mỗi máy dựng hình học giống hệt nhau nên bake ra kết quả NavMesh cũng giống hệt nhau, không cần đồng bộ thêm.
- Thứ tự bắt buộc: Sinh dữ liệu bản đồ logic → Dựng hình học (Instantiate phòng/hành lang) → Bake NavMesh → mới được phép spawn quái vật (quái vật cần NavMesh sẵn sàng để dùng `NavMeshAgent` ngay khi xuất hiện, nếu spawn trước khi bake xong, quái vật sẽ đứng yên không tìm được đường).

---

## 5. Trí tuệ nhân tạo quái vật (Enemy AI)

### 5.1 Kiến trúc: Máy trạng thái hữu hạn (Finite State Machine)

Script gốc: `EnemyStateMachine.cs`, chạy **CHỈ trên Server** (theo mô hình server-authoritative ở mục 2.1). Client không tự chạy logic AI, chỉ nhận và nội suy (interpolate) vị trí/animation qua `NetworkTransform` và `NetworkAnimator` (hoặc tương đương) để hiển thị.

Các trạng thái BẮT BUỘC có trong bản đầu tiên:

| Trạng thái | Điều kiện vào | Hành vi |
|---|---|---|
| `Idle` | Trạng thái khởi đầu, chưa phát hiện người chơi | Đứng yên hoặc đi tuần tra ngẫu nhiên trong bán kính nhỏ quanh vị trí spawn |
| `Chase` | Phát hiện người chơi trong tầm nhìn (`detectionRange`, mặc định 15m) hoặc bị bắn trúng dù chưa thấy | Dùng `NavMeshAgent.SetDestination()` trỏ tới người chơi **gần nhất còn sống** (tính lại mục tiêu định kỳ, ví dụ mỗi 0.5 giây, không phải mỗi frame để tiết kiệm hiệu năng) |
| `Attack` | Khoảng cách tới mục tiêu ≤ `attackRange` (tùy loại quái, ví dụ 2m cho cận chiến) | Dừng di chuyển, phát animation tấn công, gây sát thương theo cơ chế xem bên dưới |
| `Dead` | `Health <= 0` | Xem mục 3.3 |

- Chuyển từ `Idle` sang `Chase`: dùng kiểm tra tầm nhìn bằng `Vector3.Distance` kết hợp raycast kiểm tra không bị tường chắn (line of sight), **KHÔNG** chỉ dùng khoảng cách đơn thuần vì sẽ khiến quái vật "nhìn xuyên tường".
- Quái vật cận chiến (melee) gây sát thương qua việc Server kiểm tra khoảng cách tại đúng thời điểm animation tấn công "trúng đòn" (dùng Animation Event gọi hàm C# tại đúng frame, KHÔNG dùng collider va chạm liên tục kiểu OnTriggerStay để tránh gây sát thương nhiều lần không kiểm soát).
- Quái vật tầm xa (ranged), nếu có trong scope, dùng chính cơ chế hitscan ở mục 3.1 nhưng do Server tự khởi tạo (không qua ServerRpc vì quái vật không phải client).

### 5.2 Boss

Boss dùng cùng kiến trúc FSM nhưng **NÊN** tách file riêng kế thừa hoặc compose thêm các trạng thái mở rộng (ví dụ `Phase2`, `SpecialAttack`) thay vì nhồi hết vào chung 1 file với quái thường, để dễ bảo trì khi thiết kế cơ chế boss phức tạp hơn về sau.

**Boss MVP — Chúa tể Địa ngục (Hellfire Lord)** — ĐÃ CHỐT, đặt tại phòng boss xác định ở mục 4.1 bước 6:

| Thuộc tính | Giá trị |
|---|---|
| Máu | 800 |
| Tốc độ di chuyển Phase 1 | 3.5 m/s |
| Tốc độ di chuyển Phase 2 | 4.5 m/s |
| Sát thương đòn cận chiến | 30/đòn (phạm vi quét rộng, có thể trúng nhiều người chơi cùng lúc) |
| Sát thương đòn tầm xa (chỉ Phase 2) | 20/phát (quả cầu lửa AOE, bán kính nổ 3m) |

Trạng thái FSM riêng của boss (bổ sung vào `EnemyStateMachine.cs` hoặc file kế thừa riêng):

1. **`Phase1`** (điều kiện: `Health > 50%`): chỉ tấn công cận chiến, hành vi giống `Chase`/`Attack` ở mục 5.1 nhưng dùng thông số Phase 1 ở trên.
2. **`PhaseTransition`** (điều kiện: `Health` giảm xuống ≤ 50% lần đầu tiên, chỉ kích hoạt đúng 1 lần trong suốt trận đấu): boss **bất tử tạm thời trong 2 giây** (BẮT BUỘC set cờ miễn sát thương trong state này để tránh bị giết ngay lúc chuyển pha), phát animation/hiệu ứng "gầm thét", di chuyển về giữa phòng, sau 2 giây tự chuyển sang `Phase2`.
3. **`Phase2`** (điều kiện: đã qua `PhaseTransition`): xen kẽ giữa lao vào cận chiến và lùi ra dùng đòn tầm xa (**NÊN** ví dụ 60% thời gian cận chiến, 40% thời gian lùi bắn tầm xa, có thể chỉnh qua `[SerializeField]`). Cứ mỗi 20% máu tối đa mất đi tính từ lúc vào Phase 2, boss triệu hồi thêm 2 quái Imp (dùng lại cơ chế spawn quái vật thường, không cần logic mới).

**Phần thưởng khi hạ boss**: Khi `Health <= 0`, Server spawn 1 rương đặc biệt (dùng lại `NetworkObject` Loot Chest ở mục 6.3, chỉ khác cấu hình tỷ lệ rơi đồ) — **BẮT BUỘC** rương này đảm bảo rơi ít nhất 1 bùa lợi (power-up), có tỷ lệ rơi thêm 1 vũ khí.

---

## 6. Hệ thống vật phẩm (Loot & Item System)

### 6.1 Phân loại

- **Vũ khí (Weapon)**: mỗi loại vũ khí là 1 `ScriptableObject` (`WeaponData.cs`) chứa: tên, sát thương gốc (`baseDamage`), tốc độ bắn (`fireRate`), số đạn/băng, tầm bắn, prefab viewmodel, prefab world-model (khi rơi trên sàn), âm thanh, icon UI. **BẮT BUỘC** dùng ScriptableObject để dữ liệu vũ khí tách biệt khỏi code, dễ chỉnh cân bằng mà không cần sửa script.
- **Bùa lợi (Power-up/Buff)**: tương tự, dùng `ScriptableObject` (`PowerUpData.cs`) chứa loại hiệu ứng (tăng máu tối đa, tăng tốc độ di chuyển, tăng sát thương...), giá trị, thời lượng (nếu là buff tạm thời) hoặc vĩnh viễn trong lượt chơi.

### 6.2 Nội dung dữ liệu MVP — ĐÃ CHỐT

Đây là danh sách nội dung thật cần tạo `ScriptableObject` tương ứng cho bản MVP, **đã được nhóm chốt dùng làm số liệu chính thức để code**. Vì toàn bộ số liệu này nằm trong `ScriptableObject` (dữ liệu), không nằm trong code logic, nhóm vẫn có thể tinh chỉnh lại con số trực tiếp trên asset sau khi playtest (Giai đoạn 5, tài liệu Phân tích & Lộ trình) mà không cần sửa code.

**Vũ khí (4 khẩu, mỗi khẩu 1 asset `WeaponData`):**

| Tên | Vai trò | Sát thương/phát | Tốc độ bắn | Băng đạn | Tầm bắn | Ghi chú |
|---|---|---|---|---|---|---|
| Súng lục (Pistol) | Khởi đầu, cân bằng | 15 | 0.25s/phát | 12 viên | 100m | Vũ khí mặc định khi bắt đầu lượt chơi |
| Súng máy (SMG) | Sát thương liên tục, cận-trung | 8 | 0.08s/phát | 30 viên | 60m | **NÊN** có độ lệch bắn (spread) tăng dần khi bắn liên tục |
| Shotgun | Sát thương nổ cận chiến | 6 x 8 viên đạn ghém | 0.9s/phát | 6 viên | 15m (giảm mạnh theo khoảng cách) | Bắn ra 8 tia raycast riêng biệt trong 1 lần bắn, mỗi tia có góc lệch ngẫu nhiên nhỏ quanh hướng ngắm |
| Súng trường (Rifle) | Tầm xa, chính xác | 35 | 0.6s/phát | 10 viên | 150m | Hệ số headshot áp dụng bình thường theo mục 3.2 (x2 = 70 sát thương đầu) |

**Quái vật thường (3 loại, mỗi loại 1 cấu hình `EnemyStateMachine` + prefab riêng):**

| Tên | Vai trò AI | Máu | Sát thương | Tốc độ di chuyển | Hành vi đặc trưng |
|---|---|---|---|---|---|
| Quỷ nhỏ (Imp) | Melee rusher | 30 | 10/đòn | 5 m/s | Lao thẳng vào người chơi gần nhất, tấn công liên tục |
| Cung thủ địa ngục (Hellspawn Archer) | Ranged | 25 | 12/phát (hitscan) | Tiêu chuẩn (**NÊN** 3.5 m/s) | Giữ khoảng cách 10–20m, lùi lại nếu người chơi lại gần |
| Quỷ khổng lồ (Brute) | Tank melee | 120 | 25/đòn | 2.5 m/s | Di chuyển chậm, đòn đánh gây choáng (stagger) ngắn cho người chơi trúng đòn |

**Bùa lợi (4 loại, mỗi loại 1 asset `PowerUpData`):**

| Tên | Hiệu ứng | Thời lượng |
|---|---|---|
| Trái tim máu lớn (Max Health Up) | +25 máu tối đa, hồi đầy ngay khi nhặt | Vĩnh viễn (trong lượt chơi) |
| Giày tốc độ (Swift Boots) | +20% tốc độ di chuyển (áp dụng lên toàn bộ tốc độ ở mục 1.3: đi/chạy/lùi) | Vĩnh viễn (trong lượt chơi) |
| Bùa sát thương (Berserker Charm) | +30% sát thương gây ra (nhân vào `baseDamage` mọi vũ khí đang cầm) | Vĩnh viễn (trong lượt chơi) |
| Khiên tạm thời (Guardian Shield) | Miễn sát thương hoàn toàn (Server bỏ qua toàn bộ tính damage lên nhân vật này trong thời gian hiệu lực) | 8 giây, kích hoạt ngay khi nhặt (không phải buff nền, dùng ngay) |

**Boss**: xem mục 5.2.

### 6.3 Rương vật phẩm (Loot Chest)

- Rương là `NetworkObject`, trạng thái mở/chưa mở là `NetworkVariable<bool>`, chỉ Server ghi.
- Khi người chơi tương tác (giữ phím tương tác gần rương): Client gửi `ServerRpc` yêu cầu mở → Server kiểm tra hợp lệ (chưa mở, người chơi đủ gần) → Server chọn ngẫu nhiên vật phẩm rơi ra (dùng `System.Random` seed riêng cho phiên chơi, không nhất thiết cùng seed bản đồ) → Server spawn `NetworkObject` vật phẩm rơi ra tại vị trí rương → cập nhật `NetworkVariable` đã mở → mọi client tự đồng bộ nhìn thấy rương ở trạng thái mở và vật phẩm xuất hiện.
- **KHÔNG ĐƯỢC** để client tự quyết định vật phẩm gì rơi ra rồi báo lên server — luôn phải là quyết định của Server để tránh gian lận chọn đồ hiếm.

**Tỷ lệ rơi đồ — ĐÃ CHỐT:**

| Loại rương | Vị trí xuất hiện | Kết quả mở ra |
|---|---|---|
| Rương thường | Chỉ trong phòng Treasure (mục 17.6) | 40% → 1 bùa lợi ngẫu nhiên (trong 4 loại ở mục 6.2)<br>60% → 1 vũ khí ngẫu nhiên (trong 4 khẩu ở mục 6.2) |
| Rương boss | Đúng 1 rương, spawn khi boss chết (mục 5.2) | 100% → ít nhất 1 bùa lợi, cộng tỷ lệ rơi thêm 1 vũ khí (con số % cụ thể cho phần "rơi thêm vũ khí" ở rương boss vẫn để nhóm quyết định sau, xem mục 13) |

**NÊN** khi Server chọn vũ khí ngẫu nhiên rơi ra từ rương thường, loại trừ khẩu vũ khí người chơi đang cầm ra khỏi danh sách chọn (tránh rơi trùng vũ khí đã có) — nếu người chơi đã sở hữu hết cả 4 khẩu, cho phép rơi trùng bình thường. *(Đây là đề xuất mặc định hợp lý, chưa phải quyết định cứng — nhóm có thể đổi nếu muốn cho rơi hoàn toàn ngẫu nhiên kể cả trùng vũ khí đang cầm.)*

---

## 7. Giao diện người dùng (UI/HUD)

> Mục này là HUD cơ bản. Các phần mở rộng (panel/marker đồng đội, loading, boss bar, bản đồ, cài đặt, hiệu ứng nút...) nằm ở **mục 16**; nếu hai mục mâu thuẫn thì **mục 16 thắng**.

**Công nghệ UI — ĐÃ CHỐT: uGUI (Canvas truyền thống).** **KHÔNG dùng UI Toolkit** cho bất kỳ màn hình nào trong dự án — toàn bộ HUD, menu, pause screen **BẮT BUỘC** dựng bằng Canvas + `Image`/`Text`/`Button` chuẩn của uGUI để giữ nhất quán, tránh trộn lẫn 2 hệ thống UI khác nhau trong cùng dự án.

Các thành phần UI bắt buộc có trong bản đầu tiên:

- Thanh máu (Health bar) — góc dưới trái, cập nhật realtime theo `NetworkVariable<float> Health` của chính người chơi.
- Số đạn hiện tại/tổng số đạn dự trữ — góc dưới phải.
- Điểm ngắm (crosshair) — chính giữa màn hình, PHẢI trùng với điểm gốc raycast tính sát thương (xem lưu ý mục 3.1) để không gây cảm giác "ngắm sai".
- Màn hình tạm dừng (Pause menu) — hiện khi bấm ESC, **NÊN** chỉ tạm dừng hiển thị cục bộ (không dừng toàn bộ simulation qua mạng, vì các người chơi khác vẫn đang chơi) — tức Pause ở đây là menu, không phải `Time.timeScale = 0` toàn cục.
- Màn hình chờ/kết quả khi qua màn hoặc thất bại toàn đội.

---

## 8. Cấu trúc thư mục dự án (Project Folder Structure)

Đề xuất cấu trúc thư mục trong `Assets/` để 3 người làm việc song song không đè code lên nhau:

```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Player/          (di chuyển, camera, input — phụ trách: Tạ Nhật Nguyên)
│   │   ├── Networking/       (kết nối LAN, seed sync — phụ trách: Tạ Nhật Nguyên)
│   │   ├── DungeonGen/       (sinh bản đồ, NavMesh bake — phụ trách: Tạ Nhật Nguyên)
│   │   ├── Combat/           (hitscan, weapon, hitbox — phụ trách: Võ Hùng Mạnh)
│   │   ├── AI/               (FSM quái vật, boss — phụ trách: Trần Ngọc Bảo Phước)
│   │   ├── Items/            (loot, chest, ScriptableObject data — phụ trách: Trần Ngọc Bảo Phước)
│   │   └── UI/                (HUD, menu — phụ trách: Trần Ngọc Bảo Phước)
│   ├── Prefabs/
│   ├── ScriptableObjects/
│   ├── Scenes/
│   └── Art/ (Models, Materials, VFX, Audio)
├── Plugins/ (thư viện bên thứ 3 nếu có)
└── ThirdParty/ (asset mua từ Asset Store — ghi rõ nguồn gốc/license trong README riêng)
```

**NÊN** dùng Git với `.gitignore` chuẩn cho Unity (loại trừ `Library/`, `Temp/`, `Obj/`, `Build/`) và bật Git LFS cho các file lớn (`.fbx`, `.png`, `.wav` dung lượng lớn).

---

## 9. Định hướng nghệ thuật và không khí (Art & Aesthetic Direction)

> Mục này định nghĩa "trông và cảm giác thế nào" (look & feel), tách biệt hoàn toàn khỏi "vận hành ra sao" (đã đặc tả ở các mục cơ chế phía trên). Nếu mục này không ghi rõ điều gì, AI/lập trình viên **KHÔNG được tự chọn phong cách** — phải hỏi lại nhóm, tương tự nguyên tắc ở mục 0.

### 9.1 Phong cách hình ảnh tổng thể — ĐÃ CHỐT: Địa ngục tối tăm cổ điển (Dark Fantasy/Doom-style)

**KHÔNG phải phong cách "liminal space"** (không gian trống rải kiểu Backrooms) — đây là 2 hướng thẩm mỹ khác nhau và dự án đi theo hướng dưới đây:

- Bảng màu chủ đạo: tông tối, ám đỏ/cam (lửa, dung nham) và xám/đen (đá, kim loại rỉ sét), tương phản cao giữa vùng sáng (nguồn lửa/đuốc) và vùng tối (góc phòng, hành lang).
- Vật liệu bề mặt: đá cổ, kim loại gỉ, xương/máu làm điểm nhấn trang trí — tránh bề mặt sạch sẽ, hiện đại, phẳng lì kiểu văn phòng/liminal.
- Ánh sáng: **BẮT BUỘC** dùng nguồn sáng động (dynamic light) tại các điểm đuốc/lửa trong phòng, có dao động cường độ nhẹ (flicker) để tạo cảm giác sống động — không dùng lighting tĩnh đều một tông trên toàn bản đồ.
- Quái vật/Boss: tạo hình theo hướng quỷ dữ/sinh vật địa ngục (demon), khớp với tên gọi đã đặt ở mục 5–6 (Imp, Hellspawn Archer, Brute, Hellfire Lord).

### 9.2 Nguồn tài nguyên (Asset Sourcing) — ĐÃ CHỐT

- **NÊN** ưu tiên dùng asset có sẵn từ Unity Asset Store (kể cả asset miễn phí) cho mô hình 3D, âm thanh, VFX ở giai đoạn đầu, để tiết kiệm thời gian cho nhóm 3 người — không cần tự dựng mọi thứ từ đầu.
- **BẮT BUỘC** ghi rõ nguồn gốc và giấy phép (license) của mọi asset bên thứ 3 vào file `ThirdParty/README.md` (đã có trong cấu trúc thư mục ở mục 8) — gồm tên asset, link nguồn, loại license, có được phép dùng thương mại hay không.
- **KHÔNG ĐƯỢC** dùng asset có bản quyền không rõ ràng hoặc lấy trực tiếp từ game khác (rip asset).

### 9.3 Âm thanh (Audio Direction)

- Nhạc nền: tối, u ám, nhịp chậm ở khu vực thường; chuyển tông nhanh/kịch tính hơn khi vào phòng boss — **NÊN** dùng hệ thống chuyển nhạc theo trạng thái game (xem `GameManager` ở mục 10).
- Âm thanh chiến đấu (súng nổ, quái vật gầm, hiệu ứng trúng đòn) **BẮT BUỘC** rõ ràng, dứt khoát, ưu tiên cảm giác "nặng tay" (impact) phù hợp tinh thần "nhịp độ cực nhanh" đã ghi ở tài liệu phân tích ban đầu.

---

## 10. Kiến trúc luồng game và Scene (Game Flow & Scene Architecture)

### 10.1 Danh sách Scene — BẮT BUỘC

| Tên Scene | Vai trò |
|---|---|
| `MainMenu` | Màn hình chính: Tạo phòng (Host) / Vào phòng (Join qua IP hoặc LAN Discovery, xem mục 2.2), Cài đặt (Settings) |
| `Lobby` | Phòng chờ sau khi tạo/vào phòng — hiển thị danh sách người chơi đã kết nối, nút "Bắt đầu" (chỉ Host thấy nút này) |
| `Gameplay` | Scene chính chứa toàn bộ hầm ngục sinh ra, nơi diễn ra vòng lặp trò chơi |
| `ResultScreen` | Màn hình kết quả sau khi qua màn (Victory) hoặc toàn đội thất bại (Game Over) — **NÊN** làm dạng overlay UI trong `Gameplay` thay vì load Scene riêng, để tránh phức tạp hóa việc giữ kết nối mạng qua NGO khi đổi Scene (NGO cần cấu hình `NetworkSceneManager` cẩn thận nếu đổi Scene có network object đang tồn tại) |

**BẮT BUỘC** dùng `NetworkManager.SceneManager` (cơ chế chuyển Scene có sẵn của NGO) khi chuyển từ `Lobby` sang `Gameplay`, để đảm bảo mọi client chuyển Scene đồng bộ theo lệnh của Server — **KHÔNG ĐƯỢC** để mỗi client tự gọi `SceneManager.LoadScene()` của Unity độc lập.

### 10.2 Quản lý trạng thái game — `GameManager.cs`

**BẮT BUỘC** có 1 script `GameManager.cs` duy nhất, gắn trên 1 `NetworkObject` tồn tại xuyên suốt (dùng `DontDestroyOnLoad` hoặc spawn lại đầu mỗi Scene `Gameplay`, cần thống nhất khi code), giữ 1 `NetworkVariable<GameState> CurrentState` — **chỉ Server ghi**, mọi client đọc để hiển thị UI tương ứng.

Enum `GameState` **BẮT BUỘC** có các giá trị sau, đúng thứ tự chuyển trạng thái một chiều (không nhảy ngược, trừ khi bắt đầu lượt chơi mới):

```
Lobby → Generating (đang sinh bản đồ + bake NavMesh, xem mục 4)
     → Playing (người chơi đang khám phá/chiến đấu)
     → BossFight (đã vào phòng boss — kích hoạt nhạc/UI riêng theo mục 9.3); đúng lúc phòng boss chuyển sang `Locked` theo mục 17.4
     → Victory (đã hạ boss)      → quay lại Lobby cho lượt chơi mới (seed mới)
     → GameOver (toàn đội chết)  → quay lại Lobby cho lượt chơi mới (seed mới)
```

- **BẮT BUỘC**: mọi hệ thống khác (combat ở mục 3, AI ở mục 5, UI ở mục 7) PHẢI kiểm tra `GameManager.CurrentState` trước khi cho phép hành động tương ứng — ví dụ: không cho bắn súng khi `CurrentState == Lobby`, không cho AI hoạt động khi `CurrentState == Generating` (vì bản đồ/NavMesh chưa xong).
- Chuyển sang `Victory`/`GameOver` **BẮT BUỘC** do Server quyết định (kiểm tra máu boss hoặc máu toàn bộ người chơi), không phải client tự báo lên.

---

## 11. Quy ước viết code (Coding Conventions)

> Mục này tồn tại để **giảm tối đa hallucination khi nhiều AI/lập trình viên viết code ở các thời điểm khác nhau** — mọi đoạn code sinh ra, bất kể ai/AI nào viết, PHẢI tuân theo quy ước dưới đây để giữ codebase nhất quán.

### 11.1 Quy tắc đặt tên

- Class/Script: `PascalCase` (ví dụ `PlayerMovement`, `EnemyStateMachine`).
- Biến/hàm private: `camelCase` với tiền tố `_` cho field private (ví dụ `_currentHealth`), **KHÔNG** dùng tiền tố `m_` kiểu C++ cũ.
- Hàm public và property: `PascalCase` (ví dụ `TakeDamage()`, `IsGrounded`).
- Hằng số (`const`/`static readonly`): `UPPER_SNAKE_CASE` hoặc `PascalCase` tùy ngữ cảnh — **NÊN** thống nhất `PascalCase` để khớp chuẩn C#/.NET (ví dụ `MaxPlayerCount`).
- `NetworkVariable` đặt tên rõ nghĩa, PascalCase, không viết tắt (ví dụ `NetworkVariable<float> CurrentHealth`, không viết `NetworkVariable<float> hp`).

### 11.2 Nguyên tắc bắt buộc khi viết code

- **BẮT BUỘC** mọi thông số cân bằng gameplay (sát thương, tốc độ, thời gian hồi...) phải là `[SerializeField] private` có thể chỉnh trong Inspector, **KHÔNG hardcode** số liệu thẳng trong logic code (đã nêu rải rác ở các mục trên, đây là quy tắc tổng quát áp dụng toàn dự án).
- **KHÔNG ĐƯỢC** dùng `public` field tùy tiện để "cho dễ" truy cập — dùng `[SerializeField] private` + property `public` chỉ khi thực sự cần truy cập từ script khác.
- **BẮT BUỘC** mỗi file script mới có comment header ngắn gọn ở đầu file: tên script, mục đích, và **script này chạy ở đâu** (Server-only / Client-only / cả hai) — đặc biệt quan trọng với dự án có networking để tránh nhầm lẫn quyền hạn (xem mục 2.1).
- **KHÔNG ĐƯỢC** tự thêm thư viện/package mới (qua Package Manager hoặc NuGet) nếu không có trong mục "Giải pháp kỹ thuật" đã liệt kê (NGO, AI Navigation, Input System) — nếu cần thêm, phải hỏi lại nhóm trước.
- Với các phần chưa được quyết định trong tài liệu này (xem mục 13 — Danh sách quyết định còn để ngỏ): **BẮT BUỘC** để lại comment `// TODO(cần xác nhận): <mô tả>` tại đúng chỗ code, **KHÔNG được tự bịa ra hành vi** rồi code như thể đã được duyệt.

### 11.3 Về nguyên tắc "code tối giản" (khuyến nghị dùng kèm Ponytail)

Nhóm **NÊN** cân nhắc dùng thêm ruleset [Ponytail](https://github.com/DietrichGebert/ponytail) (cài được cho nhiều AI coding agent, bao gồm Antigravity CLI) song song với tài liệu này. Ponytail không thay thế đặc tả — nó chỉ ép AI tuân theo nguyên tắc YAGNI khi *hiện thực hóa* các mục đã đặc tả ở trên: ưu tiên dùng lại code/thư viện đã có trong dự án thay vì tự viết mới, không tạo thêm class/hệ thống phức tạp khi một giải pháp ngắn gọn hơn vẫn đáp ứng đúng yêu cầu trong tài liệu này. Nói cách khác: **tài liệu này quyết định "làm cái gì", Ponytail chỉ ảnh hưởng đến "viết bao nhiêu dòng code để làm ra nó"** — không mâu thuẫn với các ràng buộc BẮT BUỘC/KHÔNG ĐƯỢC đã nêu ở trên.

---

## 12. Quy trình Git (Git Workflow)

- **BẮT BUỘC** dùng Git, hosting trên GitHub (repo riêng cho dự án), với `.gitignore` chuẩn cho Unity (loại trừ `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`) và bật **Git LFS** cho file nhị phân lớn (`.fbx`, `.png`, `.wav`, `.mp4`...).
- **Nhánh `main`**: **BẮT BUỘC** luôn ở trạng thái chạy được (buildable), **KHÔNG ĐƯỢC** commit thẳng lên `main`.
- Mỗi tính năng/hệ thống (ví dụ: di chuyển nhân vật, hitscan, AI quái vật...) làm trên 1 nhánh riêng, đặt tên theo mẫu `feature/<ten-tinh-nang>` (ví dụ `feature/player-movement`, `feature/hitscan-combat`).
- Sau khi hoàn thành 1 nhánh tính năng và **đã tự test bằng ít nhất 2 máy qua LAN** (bắt buộc với mọi nhánh liên quan đến networking — xem đề xuất kiểm tra theo giai đoạn ở tài liệu Phân tích & Lộ trình), tạo Pull Request để merge vào `main`, có ít nhất 1 thành viên khác xem qua code trước khi merge (code review tối thiểu, không cần quy trình phức tạp với nhóm 3 người).
- Thông điệp commit **NÊN** ngắn gọn, mô tả đúng thay đổi, tiếng Việt hoặc tiếng Anh đều được nhưng **PHẢI nhất quán trong toàn bộ repo** (nhóm tự chọn 1 ngôn ngữ commit message và giữ nguyên).
- **KHÔNG ĐƯỢC** commit trực tiếp file lớn build ra (thư mục `Build/`, file `.apk`/`.exe` đã build) vào Git — dùng GitHub Releases hoặc lưu trữ ngoài nếu cần chia sẻ file build.

---

## 13. Danh sách quyết định còn để ngỏ (Open Decisions — cần nhóm xác nhận trước khi code phần liên quan)

Đây là các điểm mà tài liệu **cố ý không quyết định thay nhóm**, vì cần lựa chọn thiết kế/gameplay, không phải thuần kỹ thuật. AI hoặc lập trình viên đọc tài liệu này **KHÔNG được tự ý chọn phương án** cho các mục dưới đây — phải hỏi lại nhóm:

1. **Dash/double-jump hoặc các kỹ năng di chuyển đặc biệt khác** — **ĐÃ CHỐT: KHÔNG có ở bản đầu (MVP)**, để ngỏ làm bản sau. **BẮT BUỘC** không code bất kỳ cơ chế nào thuộc loại này ở giai đoạn hiện tại; nếu tương lai muốn thêm, cần cập nhật lại tài liệu này trước (đặc biệt mục 1.3 — di chuyển, và cân nhắc ảnh hưởng lên mục 2.1 — client-side prediction, vì dash cần xử lý qua mạng cẩn thận hơn đi bộ thường).
2. **Tỷ lệ % cụ thể cho phần "rơi thêm vũ khí" ở rương boss** (mục 6.3) — hiện chỉ chốt "rương boss đảm bảo 100% có ít nhất 1 bùa lợi", chưa có con số % cho khả năng rơi thêm vũ khí đi kèm. Đây là điểm duy nhất còn thiếu số liệu cụ thể trong toàn bộ hệ thống vật phẩm.
3. Cách chọn vũ khí ngẫu nhiên có loại trừ khẩu đang cầm hay không (đề xuất mặc định ở mục 6.3 là có loại trừ, nhưng đánh dấu "chưa phải quyết định cứng") — nhóm nên xác nhận lại nếu muốn đổi.

4. Các số mặc định MỚI do đề xuất, chưa playtest (chỉnh bằng `[SerializeField]`/`ScriptableObject`, không cần sửa code): hệ số cân bằng theo số người (mục 15.3), `readyTimeoutSeconds` 30 giây (15.1), `maxDistance` SamplePosition 3 m (15.2), phòng boss luôn hiện trên bản đồ (16.8), các thông số hiệu ứng ở mục 16.

5. Các số mặc định MỚI ở mục 17 chưa playtest (chỉnh bằng `[SerializeField]`/`ScriptableObject`): ô lưới 26 m, 0–4 cột, thưởng Linh hồn (5/8/15, +20 dọn phòng), giá shop (80/50/50/50/30), số rương Treasure (2), độ trễ spawn quái 0.8 s, thời gian hạ/nâng cổng (0.6 s/0.8 s).

*(Các mục về thông số revive và bảng số liệu vũ khí/quái vật/bùa lợi/boss đã được chốt là số liệu chính thức để code — xem mục 3.3, 5.2, 6.2. Vẫn có thể chỉnh lại con số này sau khi playtest vì toàn bộ nằm trong `[SerializeField]`/`ScriptableObject`, không cần sửa code logic.)*

---

## 14. Tóm tắt các ràng buộc cứng (Hard Constraints Checklist)

Danh sách nhanh để kiểm tra khi code hoặc review code — nếu vi phạm bất kỳ dòng nào dưới đây, code đó SAI so với đặc tả:

- [ ] Góc nhìn là **first-person**, không có camera thứ ba/top-down.
- [ ] Di chuyển dùng `CharacterController`, không dùng Rigidbody physics-based.
- [ ] Toàn bộ trạng thái quan trọng (máu, vị trí quái vật, vật phẩm rơi, trạng thái rương) do **Server quyết định**, client không tự quyết định rồi báo lên.
- [ ] Sát thương được **Server tự raycast xác nhận lại**, không tin số liệu client tự gửi.
- [ ] Bản đồ ngẫu nhiên đồng bộ qua **seed**, không truyền hình học qua mạng.
- [ ] Thuật toán sinh bản đồ dùng `System.Random(seed)`, deterministic tuyệt đối.
- [ ] NavMesh bake **sau khi** dựng xong hình học, **trước khi** spawn quái vật.
- [ ] AI quái vật chỉ chạy logic trên **Server**.
- [ ] Điểm gốc raycast tính sát thương là **tâm camera**, không phải nòng súng vật lý.
- [ ] Friendly fire **TẮT**: raycast vẫn dừng lại ở đồng đội (chặn đường bắn) nhưng **không trừ máu** đồng đội.
- [ ] Nhân vật chết vào trạng thái **`Downed`** trước (60 giây, có thể chỉnh trong Inspector), không loại khỏi lượt chơi ngay; chỉ loại khi hết `downedDuration` mà chưa được đồng đội revive (giữ phím 3 giây, hồi 30% máu tối đa).
- [ ] Rương thường: **40% ra bùa lợi, 60% ra vũ khí**. Rương boss: 100% có ít nhất 1 bùa lợi.
- [ ] Không code dash/double-jump ở bản MVP hiện tại — đây là giới hạn phạm vi có chủ đích, không phải thiếu sót.
- [ ] Toàn bộ điều kiện và kết quả revive (thời gian giữ phím, % máu hồi lại) do **Server xác thực**, client không tự báo "đã cứu xong".
- [ ] **KHÔNG** cài đặt host migration/server failover dưới bất kỳ hình thức nào — Host rớt mạng thì cả phòng về Main Menu, không khôi phục trạng thái.
- [ ] Toàn bộ UI dùng **uGUI (Canvas)** — không có màn hình nào dùng UI Toolkit.
- [ ] Mọi hệ thống gameplay kiểm tra `GameManager.CurrentState` trước khi cho phép hành động (không bắn được khi ở `Lobby`, AI không chạy khi ở `Generating`...).
- [ ] Chuyển Scene `Lobby → Gameplay` dùng `NetworkManager.SceneManager`, không dùng `SceneManager.LoadScene()` gọi độc lập từng client.
- [ ] Phong cách hình ảnh là **địa ngục tối tăm cổ điển**, không phải liminal space/Backrooms.
- [ ] Boss có đúng 3 trạng thái FSM: `Phase1 → PhaseTransition (bất tử 2s) → Phase2`, không bỏ qua bước bất tử tạm thời khi chuyển pha.
- [ ] `Player.prefab`: `NetworkTransform` ở root có `AuthorityMode = Owner` (ngoại lệ mục 2.1.1); mọi NetworkTransform khác giữ `Server`.
- [ ] Server không gán trực tiếp `transform.position` của người chơi; dịch chuyển người chơi chỉ qua `ClientRpc` nhắm chủ nhân (tắt CharacterController → đặt vị trí → `Teleport` → bật lại).
- [ ] Quái/rương/boss chỉ được spawn SAU KHI mọi client báo `DungeonReady` (mục 15.1); mọi vị trí spawn đều qua `NavMesh.SamplePosition` (mục 15.2).
- [ ] Số quái và máu boss nhân theo số người chơi lúc vào `Generating` và giữ cố định cả lượt (mục 15.3).
- [ ] Màu người chơi chỉ lấy từ bảng 4 màu ở mục 16.1, không dùng màu đỏ cho người chơi.
- [ ] Scene Gameplay không còn `ArenaFloor`/`Pillar_*`; `HellfirePhase1Setup` không tạo lại chúng (mục 17.0).
- [ ] `DungeonRoomType` có đúng 6 loại (Spawn, Combat, Upgrade, Treasure, Shop, Boss); phòng Upgrade/Treasure/Shop ưu tiên nhánh cụt ngoài đường chính; phòng an toàn không có quái, cổng, cột (mục 17.1).
- [ ] Hành lang không có quái, không khóa; chỉ phòng Combat có 0–4 cột đặt ở 4 ô góc (mục 17.2, 17.3).
- [ ] Cổng sắt chỉ có ở phòng Combat/Boss; collider bật khi đóng và tắt khi mở; Server quyết định khóa/mở; mỗi phòng chỉ khóa một lần (mục 17.4).
- [ ] Quái chỉ spawn khi phòng chuyển `Locked`; boss chỉ spawn khi phòng boss `Locked` (mục 17.5).
- [ ] Linh hồn là `NetworkVariable<int>` do Server ghi, mỗi người một ví, reset mỗi lượt (mục 17.7).
- [ ] Shop: 5 món giống nhau cho mọi người; tập "đã mua" tách riêng từng người; Server xác thực mọi giao dịch (mục 17.8).
- [ ] Đạn dự trữ VẪN VÔ HẠN (mục 18, 19.3); không có đạn dự trữ hữu hạn trong `WeaponController`/`RuntimeWeapon`.
- [ ] Cảm giác súng của Benz (WeaponSwayBobbing, WeaponRecoil, WeaponAimDownSights, WeaponMotionCoordinator) được gắn vào `Player.prefab` THẬT và chỉ chạy phía chủ sở hữu (thuần hình ảnh); hitscan vẫn do Server xác nhận (mục 19.2).
- [ ] `RangedZombieAI` có prefab chính thức, đã đăng ký NetworkPrefabs và nằm trong bảng trọng số spawn của phòng Combat (mục 19.4).

---

## 15. Bổ sung nền tảng — Giai đoạn 4.4 (làm TRƯỚC UI nâng cao)

> Các mục 15.1–15.3 sửa/bổ sung cho các hệ thống đã làm ở Giai đoạn 2–4. Cùng với ngoại lệ ở mục 2.1.1, đây là **gói nền tảng phải xong và test được bằng 2 máy** trước khi làm mục 16.

### 15.1 Cổng chờ đủ máy (Loading Gate) — BẮT BUỘC

**Vấn đề**: mỗi máy tự dựng bản đồ từ seed rồi tự bake NavMesh (mục 2.3, 4.2) với tốc độ khác nhau. `NetworkSceneManager.OnLoadEventCompleted` chỉ báo mọi máy đã tải xong **scene**, không báo bản đồ đã dựng xong. Nếu Server spawn quái hoặc dịch chuyển người chơi sớm, máy chậm hơn sẽ thấy quái/người rơi xuống khoảng trống.

Luồng BẮT BUỘC:

1. Khi Host bấm bắt đầu: `GameManager` chuyển `CurrentState = Generating` (trạng thái này đã có trong enum nhưng hiện chưa được dùng).
2. Mỗi máy (kể cả Host) sau khi **dựng xong hình học VÀ bake xong NavMesh** gọi `DungeonReadyServerRpc()`.
3. Server giữ `HashSet<ulong> readyClients` và `NetworkVariable<int> ReadyCount` (Server ghi, mọi client đọc để hiển thị loading).
4. Khi `readyClients` chứa đủ mọi client đang kết nối → Server mới được: (a) dịch chuyển người chơi về phòng spawn (theo mục 2.1.1), (b) spawn quái/rương/boss, (c) chuyển `CurrentState = Playing`. **Đúng thứ tự (a) → (b) → (c).**
5. Timeout `readyTimeoutSeconds` (`[SerializeField]`, mặc định **30 giây**): hết hạn mà còn client chưa sẵn sàng → Server ngắt kết nối các client đó với lý do "Tải bản đồ quá lâu" rồi tiếp tục với số client còn lại.
6. Trong `Generating`, người chơi KHÔNG được di chuyển/bắn (`CanPlayerMove()` trả về false), AI không chạy.
7. Nếu một client ngắt kết nối giữa lúc `Generating`, Server loại client đó khỏi tập chờ để không bị kẹt.

### 15.2 Kiểm tra vị trí spawn hợp lệ — BẮT BUỘC

Hiện quái spawn lệch ngẫu nhiên quanh tâm phòng và rương đặt ở offset cố định, không kiểm tra có đứng trên NavMesh hay không (nguy cơ kẹt tường/ngoài sàn).

- Mọi vị trí spawn do Server quyết định (quái thường, boss, quái do boss triệu hồi, rương, vị trí xếp người chơi ở phòng spawn) PHẢI qua `NavMesh.SamplePosition(pos, out hit, maxDistance, NavMesh.AllAreas)`; `maxDistance` là `[SerializeField]` mặc định **3.0 m**.
- Nếu thất bại: dùng tâm phòng đã sample; nếu tâm phòng cũng thất bại thì `Debug.LogWarning` kèm tên phòng và vẫn spawn tại tâm phòng (không được bỏ qua việc spawn boss/rương khiến vòng chơi bị kẹt).
- Logic này chạy **chỉ trên Server** (mọi spawn là của Server).

### 15.3 Cân bằng theo số người chơi — BẮT BUỘC

- Tạo `ScriptableObject` `DifficultyScalingData` chứa 2 mảng 4 phần tử, chỉ số theo số người chơi 1/2/3/4:

| Số người | Hệ số số lượng quái thường | Hệ số máu tối đa của boss |
|---|---|---|
| 1 | 0.7 | 0.6 |
| 2 | 1.0 | 1.0 |
| 3 | 1.4 | 1.5 |
| 4 | 1.8 | 2.0 |

  Đây là **số khởi điểm** để code, chỉnh lại sau playtest (Giai đoạn 5) trực tiếp trên asset, không sửa code.
- Số người chơi được **chốt một lần** khi vào `Generating` (số client đang kết nối) và giữ nguyên cả lượt, kể cả có người thoát.
- Số quái mỗi phòng = `max(1, round(số quái đã roll theo mục 6.2 × hệ số))`. Máu tối đa boss = `800 × hệ số`, `CurrentHealth` khởi tạo bằng máu tối đa. Quái Imp do boss triệu hồi (mục 5.2) KHÔNG nhân hệ số. Sát thương quái KHÔNG đổi theo số người.

---

## 16. UI nâng cao và cảm giác chơi — Giai đoạn 4.5

> Toàn bộ UI vẫn là **uGUI** (mục 7), không thêm package/thư viện mới (không DOTween; dùng coroutine + `AnimationCurve`). Mọi thông số bên dưới là `[SerializeField]`. Chia làm 2 đợt giao việc: **4.5A** = 16.1, 16.2, 16.3, 16.10; **4.5B** = 16.4 → 16.9 và 16.11.

### 16.1 Danh tính người chơi: tên, màu, số — ĐÃ CHỐT

- Component `PlayerIdentity.cs` trên `Player.prefab` với: `NetworkVariable<FixedString32Bytes> PlayerName`, `NetworkVariable<int> ColorIndex` (0–3), `NetworkVariable<bool> IsReady` — **chỉ Server ghi**.
- **Tên**: người chơi nhập ở Main Menu (tối đa 16 ký tự, cắt khoảng trắng đầu/cuối), lưu `PlayerPrefs` khóa `PlayerName`; rỗng thì dùng "Player N" (N = ColorIndex + 1). Sau khi spawn, chủ nhân gửi tên cho Server bằng `ServerRpc`; Server kiểm tra/cắt độ dài rồi ghi vào `PlayerName`.
- **Màu**: Server gán `ColorIndex` = chỉ số nhỏ nhất chưa dùng trong 0–3 khi người chơi spawn, cố định suốt lượt chơi. Bảng màu **BẮT BUỘC** dùng đúng 4 màu (lấy từ bảng Okabe-Ito thân thiện mù màu):

| ColorIndex | Số hiển thị | Màu | Hex |
|---|---|---|---|
| 0 | 1 | Xanh da trời | `#56B4E9` |
| 1 | 2 | Cam | `#E69F00` |
| 2 | 3 | Xanh lục | `#009E73` |
| 3 | 4 | Tím hồng | `#CC79A7` |

- **KHÔNG ĐƯỢC** dùng màu đỏ hoặc vàng cho màu người chơi (đỏ dành cho sát thương/kẻ địch, vàng đang dùng cho crosshair/hitmarker). Mọi nơi hiển thị màu người chơi PHẢI kèm **số 1–4** để người mù màu vẫn phân biệt được.
- Tạo lớp tĩnh `PlayerPalette` (một nơi duy nhất giữ bảng màu); mọi UI lấy màu từ đây, không hardcode.
- Lobby: danh sách slot hiển thị chip màu + số + tên + dấu ✓ khi `IsReady` (thay cho cách tô xanh/trắng hiện tại).

### 16.2 Panel đồng đội trên HUD — ĐÃ CHỐT

- Đặt ở cạnh trái màn hình, xếp dọc, tối đa 3 mục (mọi người chơi trừ bản thân); ẩn khi chơi một mình.
- Mỗi mục: chip màu + số, tên, thanh máu (`CurrentHealth / MaxHealth`).
- Trạng thái: **Bình thường** (thanh máu); **Gục** (`IsDowned`): thanh máu thay bằng đếm ngược `DownedTimer` dạng `mm:ss`, biểu tượng nhấp nháy; **Chết** (`IsDead`): làm xám, hiện dấu X.
- Cập nhật qua callback `OnValueChanged` của `NetworkVariable` (không dò mỗi frame). Khi có người vào/ra, panel tự thêm/xóa mục.

### 16.3 Marker vị trí đồng đội — ĐÃ CHỐT

- Dùng Canvas Screen Space Overlay, các phần tử UI tạo sẵn theo dạng pool (không Instantiate liên tục).
- Mỗi đồng đội chưa chết có 1 marker: icon màu + số, đặt ở điểm đầu nhân vật cộng 0.5 m, dùng `Camera.WorldToScreenPoint`. **Hiện xuyên tường** (không raycast che khuất). Hiển thị khoảng cách làm tròn mét.
- Ngoài màn hình hoặc nằm sau lưng camera (`z < 0` → lật hướng): kẹp marker vào mép màn hình (chừa lề 5%) kèm mũi tên chỉ hướng.
- Kích thước thu nhỏ dần theo khoảng cách (từ 1.0 ở gần xuống 0.6 từ 40 m trở lên).
- Đồng đội **Gục**: marker nhấp nháy (khoảng 2 lần/giây) kèm số giây còn lại. **Chết**: bỏ marker. Không hiện marker cho chính mình.

### 16.4 Màn hình loading và chuyển cảnh — ĐÃ CHỐT

- Panel toàn màn hình hiện khi `GameState == Generating` (và trong lúc tải scene Lobby → Gameplay): spinner xoay 360°/giây, dòng chữ "Đang tạo hầm ngục... {ReadyCount}/{số người} sẵn sàng" (dùng `ReadyCount` ở mục 15.1), và mẹo chơi đổi mỗi 4 giây (tối thiểu 6 mẹo tiếng Việt, ví dụ về revive, về bắn đầu, về bùa lợi).
- Khi vào `Playing`: mờ dần panel trong 0.5 giây rồi tắt. Chuyển giữa các scene: fade đen 0.3 giây.

### 16.5 Hiệu ứng nút — ĐÃ CHỐT

- Component `UIButtonFeedback.cs` gắn vào mọi `Button`: hover phóng 1.05× trong 0.1 giây, nhấn thu 0.95× trong 0.05 giây rồi trở lại; phát tiếng click 2D. Dùng `Time.unscaledDeltaTime`, coroutine + `AnimationCurve`.

### 16.6 Thanh máu boss — ĐÃ CHỐT

- Giữa trên màn hình, hiện khi `GameState == BossFight`: tên "CHÚA TỂ ĐỊA NGỤC", thanh máu, vạch đánh dấu ở 50%.
- Khi boss vào `PhaseTransition`: Server gọi `ClientRpc` để mọi máy rung màn hình ~0.5 giây và hiện chữ "PHA 2" ngắn. Rung màn hình PHẢI tuân theo công tắc "Giảm rung màn hình" (mục 16.9).

### 16.7 Phản hồi chiến đấu — ĐÃ CHỐT

- **Hitmarker** (đã có): giữ thời lượng trong khoảng 50–100 ms. **Headshot**: hình/màu khác + âm thanh riêng. **Hạ gục**: marker lớn hơn + âm thanh riêng; Server báo xác nhận hạ gục tới đúng người bắn bằng `ClientRpc` nhắm chủ nhân.
- **Bị đánh**: viền đỏ (vignette bằng UI Image) hiện rồi mờ dần ~0.4 giây, độ đậm theo lượng sát thương; mũi tên chỉ hướng kẻ tấn công (Server gửi vị trí kẻ tấn công cho nạn nhân bằng `ClientRpc` nhắm chủ nhân).
- **Máu thấp** (< 30%): viền đỏ nhịp ~1.2 lần/giây kèm âm thanh tim đập; tắt khi hồi máu hoặc khi vào trạng thái Gục.

### 16.8 Bản đồ hầm ngục (Tab) — ĐÃ CHỐT

- Nhấn **Tab** bật/tắt bản đồ toàn màn hình (không phải minimap góc). Vẽ từ dữ liệu `DungeonGrid` đã có: mỗi phòng là 1 ô vuông, hành lang là đoạn nối.
- Phòng đã có ít nhất một người chơi đi vào: sáng. Phòng kề phòng đã đi qua: mờ (đã lộ). Phòng khác: ẩn. Danh sách phòng đã đi qua là hợp của mọi người chơi, **mỗi client tự tính** từ vị trí các người chơi mà nó nhận được (không cần đồng bộ thêm qua mạng).
- **Phòng boss luôn hiện** với biểu tượng đầu lâu (công tắc `revealBossRoom` mặc định bật, để người chơi không bị lạc).
- Đồng đội hiện bằng chấm đúng màu/số theo mục 16.1; bản thân hiện bằng mũi tên chỉ hướng nhìn.

### 16.9 Cài đặt (Settings) — ĐÃ CHỐT

- **FOV**: thanh trượt 70–110, mặc định **90**, lưu `PlayerPrefs` khóa `FieldOfView`, áp dụng ngay cho camera người chơi (thay cho việc cố định 90 ở mục 1.2/`PlayerMovement`).
- **Âm lượng tổng**: thanh trượt 0–1, áp dụng `AudioListener.volume`, lưu `PlayerPrefs`. (Tách riêng nhạc/hiệu ứng là việc của bản sau.)
- **Giảm rung màn hình**: công tắc, khi bật thì mọi hiệu ứng rung màn hình (mục 16.6, và các rung khác nếu có) bị tắt hoặc giảm tối thiểu.
- Độ nhạy chuột giữ nguyên như hiện tại.

### 16.10 Lobby: sẵn sàng (Ready-up) và hiển thị IP — ĐÃ CHỐT

- Mỗi người chơi **không phải Host** có nút bật/tắt "Sẵn sàng" (ghi `PlayerIdentity.IsReady` thông qua `ServerRpc`). Host mặc định coi như sẵn sàng.
- Nút "Bắt đầu" của Host chỉ bấm được khi mọi người chơi khác đã sẵn sàng; cho phép bắt đầu khi chỉ có một mình Host (số người = 1 theo mục 15.3).
- Lobby hiển thị **IP LAN của Host** dạng `x.x.x.x:cổng` kèm nút sao chép, lấy IPv4 thuộc dải riêng (192.168.x.x, 10.x.x.x, 172.16–31.x.x) từ `Dns.GetHostEntry`; nếu có nhiều địa chỉ thì hiển thị tối đa 3 để người dùng chọn đúng card mạng.
- **NÊN**: ở Main Menu, khi Client bấm vào phòng mà quá 10 giây chưa kết nối được thì hiện thông báo lỗi rõ ràng và cho thử lại (thay vì chỉ hiện "Đang kết nối..." mãi).

### 16.11 Tổng kết lượt chơi — ĐÃ CHỐT nhưng ƯU TIÊN THẤP

- **Chỉ làm khi mọi mục khác của 16 đã test xong.** Server ghi nhận theo từng người chơi: số quái hạ, tổng sát thương, số lần cứu đồng đội, số lần bị gục, và thời gian cả lượt. Khi chuyển sang `Victory`/`GameOver`, Server gửi bảng này cho mọi client bằng `ClientRpc` (dùng struct `INetworkSerializable`), hiển thị trên màn hình kết quả với màu/số/tên theo mục 16.1.

### 16.12 Chưa làm ở bản này (giới hạn phạm vi có chủ đích)

Hệ thống ping, kill feed, damage numbers, bảng điểm trực tiếp, minimap có đánh dấu quái, tách âm lượng nhạc/hiệu ứng. (Cảm giác súng gồm sway/bobbing, giật, ADS đã được tích hợp, xem mục 19.2.) AI/lập trình viên **KHÔNG ĐƯỢC** tự thêm các mục này; muốn thêm phải cập nhật đặc tả trước.

---

## 17. Phòng, cổng sắt và cửa hàng — Giai đoạn 4.6

> **Điều kiện tiên quyết**: Giai đoạn 4.4 đã xong (mục 2.1.1 và 15), vì mọi kiểm tra "người chơi đang ở trong phòng / gần cửa hàng" của Server dùng vị trí người chơi do `NetworkTransform` gửi lên; và nhánh `feature/shrine-and-stats` đã được merge vào `main` (mục 17.6 dùng lại `InteractiveShrine`). Nếu mục 17 mâu thuẫn với mục 4.1 (bước 7), 6.3, 10.2 hoặc 16.12 thì **mục 17 thắng**.

### 17.0 Dọn đấu trường test — BẮT BUỘC làm đầu tiên

- Scene `Gameplay` đang còn sót đấu trường test do `HellfirePhase1Setup` tạo: `ArenaFloor` (Plane 50×50 tại gốc tọa độ) và **16** khối `Pillar_{x}_{z}` (2×4×2, x và z thuộc {-15, -5, 5, 15}; điều kiện "bỏ ô (0,0)" trong vòng lặp không bao giờ đúng nên đủ 16 khối). Chúng nằm chồng lên phòng/hành lang của hầm ngục sinh ngẫu nhiên và có mặt sàn trùng độ cao y=0 với sàn phòng.
- **Xóa** `ArenaFloor` và mọi `Pillar_*` khỏi scene; **xóa đoạn code tạo chúng** trong `HellfirePhase1Setup` (và bất kỳ script Editor nào khác tạo lại) để chạy lại setup không sinh lại.
- Sau khi dọn, không còn mesh/collider nào nằm ngoài `DungeonGeometryRoot` tham gia bake NavMesh, ngoại trừ người chơi, quái, vật phẩm, ánh sáng và UI.

### 17.1 Loại phòng và cách phân bổ — BẮT BUỘC

`enum DungeonRoomType` PHẢI có đúng 6 giá trị và thay hẳn các giá trị cũ:

| Loại | Số lượng mỗi lượt | An toàn? | Nội dung |
|---|---|---|---|
| `Spawn` | 1 | Có | Phòng xuất phát, không quái, không cổng |
| `Combat` | Phần còn lại (3–9 phòng khi có 8–14 phòng) | Không | Khóa cổng, quái spawn khi vào (mục 17.4, 17.5), 0–4 cột (mục 17.3) |
| `Upgrade` | 1 | Có | 1 tế đàn chọn 1 trong 3 (mục 17.6) |
| `Treasure` | 1 | Có | Rương kho báu (mục 17.6) |
| `Shop` | 1 | Có | Cửa hàng 5 món (mục 17.8) |
| `Boss` | 1 | Không | Khóa cổng, boss spawn khi vào (mục 17.4) |

Phòng `Spawn`, `Upgrade`, `Treasure`, `Shop` **không có quái, không có cổng, không có cột**.

Thuật toán phân bổ (chạy sau khi đã có sơ đồ logic và đã xác định phòng boss theo mục 4.1 bước 6). BẮT BUỘC dùng một `System.Random` **riêng** dựng từ seed (ví dụ `new System.Random(dungeonSeed ^ 0x2F6B)`), để không làm thay đổi sơ đồ phòng hiện có và để kết quả giống hệt nhau trên mọi máy:

1. Lấy "đường chính" = đường đi ngắn nhất Spawn → Boss (BFS đã có trong `DungeonGrid`).
2. Ứng viên ưu tiên 1: phòng cụt (đúng 1 kết nối), không phải Spawn/Boss, không nằm trên đường chính.
3. Nếu chưa đủ 3 ứng viên: thêm ứng viên ưu tiên 2 = phòng không nằm trên đường chính (không phải Spawn/Boss), kể cả phòng không cụt.
4. Nếu vẫn chưa đủ: lấy phòng bất kỳ còn lại không phải Spawn/Boss, ưu tiên phòng xa Spawn nhất; ghi `Debug.LogWarning`.
5. Xáo thứ tự ứng viên bằng rng riêng, lần lượt gán `Upgrade`, `Treasure`, `Shop`. Mọi phòng còn lại là `Combat`.

### 17.2 Hành lang và khả năng nhìn thấy

- Hành lang **không bao giờ có quái và không bao giờ khóa**. Quái chỉ spawn trong phạm vi sàn phòng `Combat`/`Boss` (mục 17.5).
- `_gridCellSize` đổi mặc định từ 20 lên **26** (phòng vẫn 14 m; theo công thức hiện tại `khoảng cách − roomSize + 1`, hành lang dài khoảng 13 m).
- Hành lang PHẢI nhìn thấy rõ từ trong phòng: (a) sàn hành lang dùng chất liệu khác sàn phòng (tối hơn) qua `_corridorFloorMaterial` mới, (b) mỗi hành lang có 2 đèn điểm ở 1/3 và 2/3 chiều dài, nháy nhẹ theo mục 9.1, (c) mỗi cửa phòng có khung cửa (2 trụ + 1 dầm) để nhận ra lối đi. Mọi thông số là `[SerializeField]`.

### 17.3 Cột (chướng ngại vật) trong phòng Combat

- Chỉ phòng `Combat` có cột. Số cột = `rng.Next(0, 5)` (0–4) dùng rng riêng ở mục 17.1 (hoặc một rng riêng khác dựng từ seed, giống nhau trên mọi máy).
- Vị trí cột chỉ lấy từ **4 ô góc cục bộ** `(±4, 0, ±4)` của phòng (không bao giờ nằm trên trục cửa hay tâm phòng), mỗi ô tối đa 1 cột.
- Kích thước 2×4×2, có collider, là con của `DungeonGeometryRoot`, dựng **trước** khi bake NavMesh.
- KHÔNG dùng chướng ngại nào khác (thùng, tường chắn thêm...).

### 17.4 Cổng sắt và khóa phòng — BẮT BUỘC

Áp dụng cho phòng `Combat` và `Boss`. **Server là chủ trạng thái.** Mỗi phòng có trạng thái `Idle` → `Locked` → `Cleared` (một chiều; `Cleared` là vĩnh viễn, không bao giờ khóa lại, quái không respawn). Phòng được nhận diện bằng tọa độ lưới; trạng thái cổng được đồng bộ bằng `ClientRpc` `SetRoomGatesClientRpc(Vector2Int roomGrid, bool closed)` để mỗi máy hạ/nâng cổng trên bản đồ mà nó tự dựng từ seed (không cần NetworkObject cho phòng).

Luồng:

1. Server mỗi 0,2 giây kiểm tra các phòng đang `Idle`: nếu có ít nhất 1 người chơi **còn sống và không `Downed`** có vị trí (theo Server) nằm trong vùng "lõi" của phòng — hình vuông cạnh `roomSize − 3` (thụt vào 1,5 m mỗi phía để không kích hoạt khi mới đứng ở ngưỡng cửa) — thì kích hoạt khóa.
2. Khi khóa, Server theo thứ tự: (a) đặt `Locked`; (b) dịch chuyển vào trong phòng 2 m mọi người chơi đang đứng trong vùng ngưỡng cửa (cách mặt tường dưới 1,5 m, theo bề rộng cửa) bằng `ClientRpc` nhắm chủ nhân theo mục 2.1.1 điểm 3, để không ai bị kẹt trong collider cổng; (c) gọi `SetRoomGatesClientRpc(roomGrid, true)`; (d) sau **0,8 giây** (cổng đã hạ xong) spawn quái theo mục 17.5. Với phòng `Boss`: đặt `GameState = BossFight` và spawn boss ở bước (d).
3. Điều kiện mở: mọi quái thuộc phòng đó đã chết, kể cả quái do boss triệu hồi (phòng `Boss`: khi boss chết thì Server tự hạ mọi quái còn sống của phòng). Khi đó Server đặt `Cleared`, gọi `SetRoomGatesClientRpc(roomGrid, false)` và trao thưởng (mục 17.7). Với phòng `Boss` tiếp tục luồng `Victory` như mục 10.2.
4. Người chơi đứng ngoài lúc cổng hạ bị chặn bên ngoài cho đến khi phòng `Cleared` (chấp nhận được). Người chơi `Downed` bên trong vẫn được đồng đội bên trong revive. Nếu mọi người chơi còn sống đều `Downed`/`Dead` thì luật `GameOver` ở mục 3.3 và 10.2 áp dụng như cũ (cổng không thay đổi luật này).

Cổng (đặt ở **mọi** cửa của phòng `Combat`/`Boss`, kể cả cửa người chơi vừa đi vào):

- **Hình**: hàng song sắt đứng (khối mảnh khoảng 0,15 × chiều cao tường × 0,15, cách nhau khoảng 0,4 m) cộng thanh ngang trên và dưới, bề ngang bằng `_corridorWidth`, đặt đúng mặt phẳng tường chứa cửa; chất liệu sắt tối qua `_gateMaterial`.
- **Chuyển động**: đóng = hạ từ trên xuống trong 0,6 giây; mở = nâng lên trong 0,8 giây (lên trên đỉnh tường), bằng coroutine + `AnimationCurve`. Phát âm thanh đóng/mở (chưa có clip thì để `// TODO(cần xác nhận)`).
- **Va chạm**: `BoxCollider` phủ khoảng cửa, **bật khi đóng và TẮT khi mở** (bắt buộc, nếu không người chơi không đi qua được). Thêm `NavMeshObstacle` (carve bật) hoạt động khi đóng.
- Bản MVP: cổng đóng chặn cả đạn lẫn tầm nhìn của AI (đạn xuyên song sắt là việc của bản sau).

### 17.5 Quái spawn theo phòng (thay cho spawn toàn bản đồ lúc bắt đầu)

- Quái **KHÔNG** còn được spawn hàng loạt khi vào `Playing`. `DungeonEnemySpawner` chỉ spawn khi một phòng chuyển sang `Locked`; boss chỉ spawn khi phòng `Boss` `Locked`. Không bao giờ có quái trong hành lang hoặc phòng an toàn.
- Số quái phòng `Combat` = `max(1, round(N × hệ số mục 15.3))`, với N là số quái mỗi phòng mà `DungeonEnemySpawner` đang roll hiện nay (giữ nguyên khoảng giá trị và cách chọn loại Imp/Archer/Brute hiện có; chỗ nào không rõ thì để `// TODO(cần xác nhận)`, không tự bịa số).
- Vị trí: trong vùng sàn phòng, cách tường tối thiểu 2 m, qua `NavMesh.SamplePosition` (mục 15.2), không trùng vị trí cột; **NÊN** tránh bán kính 4 m quanh người chơi nếu còn chỗ.
- Server đếm `aliveEnemies` theo từng phòng; quái chết hoặc despawn đều giảm bộ đếm. Quái rơi ra ngoài bản đồ (y < -10) coi như đã chết để phòng không bị kẹt khóa mãi.
- Chỉ **một đợt** quái mỗi phòng.

### 17.6 Phòng Upgrade và Treasure

- **Upgrade**: đúng 1 `InteractiveShrine` ở tâm phòng, dùng lại hệ thống tế đàn 3 lựa chọn của nhóm (mỗi người chơi dùng một lần mỗi tế đàn, đúng cơ chế `_usedShrineIds` hiện có). `ShrineId` xác định theo tọa độ lưới của phòng để giống nhau trên mọi máy.
- **Treasure**: `treasureChestCount` (mặc định **2**) `LootChest` thường đặt đối xứng quanh tâm phòng, mở theo luật mục 6.3 (40% bùa lợi / 60% vũ khí). Rương dùng chung (ai mở trước thì nhận), giữ như hiện tại.
- Rương **chỉ** xuất hiện ở phòng `Treasure` (và rương boss); không còn rương rải trong phòng khác.

### 17.7 Tiền "Linh hồn" (Souls)

- `PlayerWallet.cs` trên `Player.prefab` với `NetworkVariable<int> Souls`: **chỉ Server ghi**, chỉ chủ nhân đọc (`NetworkVariableReadPermission.Owner`). Bắt đầu mỗi lượt chơi = 0 (không giữ giữa các lượt).
- **Nhận tiền (chia sẻ)**: khi một quái chết, **mọi người chơi còn sống (không `Dead`) cùng nhận đủ mức thưởng**, để không tranh đòn kết liễu. Mức mặc định: Imp 5, Archer 8, Brute 15, Imp do boss triệu hồi 0, boss 0. Khi phòng `Combat` chuyển `Cleared`: mỗi người chơi còn sống nhận thêm **+20** (`roomClearBonus`). Các số là `[SerializeField]`.
- HUD: một bộ đếm "Linh hồn: N" ở góc trên phải.

### 17.8 Phòng Shop — mua riêng từng người, bày chung — ĐÃ CHỐT

- `ShopStation.cs` (NetworkObject) ở tâm phòng `Shop`, hiện gợi ý `[E]` khi người chơi trong 3 m.
- **5 món bày bán, giống hệt nhau với mọi người chơi**: Server chọn một lần khi phòng được dựng (rng riêng theo seed) 5 món **khác nhau** từ nhóm 8 món: 4 vũ khí (Pistol, SMG, Shotgun, Rifle) và 4 bùa lợi (Max Health Up, Swift Boots, Berserker Charm, Guardian Shield). Lưu thành `NetworkList<int> OfferIds` để mọi máy hiển thị cùng 5 món. KHÔNG dùng vật phẩm ngoài nhóm 8 món này.
- **Mua là riêng của từng người, hiển thị là chung**: Server giữ `Dictionary<ulong, HashSet<int>> purchased` (clientId → các món đã mua). Mua không làm món biến mất với người khác. Ví dụ shop bán A, B, C, D, E: người 1 mua A và B thì người 1 thấy A, B ở trạng thái "ĐÃ MUA", còn người 2 vẫn thấy và mua được cả A, B, C, D, E. Một người mua được **nhiều món** miễn đủ Linh hồn, mỗi món tối đa **1 lần cho mỗi người**.
- **Giá mặc định** (thêm trường `ShopPrice` kiểu int, `[SerializeField]`, vào `WeaponData` và `PowerUpData`): vũ khí 80; Max Health Up 50; Swift Boots 50; Berserker Charm 50; Guardian Shield 30.
- **Luồng**: bấm `[E]` mở `ShopModalUI` (uGUI, cục bộ, giống modal tế đàn; Esc để đóng; không pause game) với 5 thẻ gồm icon, tên, mô tả, giá và trạng thái ("ĐÃ MUA" / "KHÔNG ĐỦ LINH HỒN"). Bấm mua gọi `BuyOfferServerRpc(int offerIndex)` (`RequireOwnership = false`, dùng clientId của người gửi). Server kiểm tra theo thứ tự: người gửi còn sống và không `Downed`; đứng trong 3 m (theo vị trí Server); `GameState` là `Playing` hoặc `BossFight`; `offerIndex` hợp lệ; người này chưa mua món đó; đủ Linh hồn; nếu là vũ khí thì người này chưa sở hữu khẩu đó (nếu đã có: từ chối, **không trừ tiền**, báo "Bạn đã có vũ khí này"). Hợp lệ thì trừ Linh hồn, trao món qua **đúng đường trao vật phẩm đang có** (vũ khí như `ItemPickup`; bùa lợi qua `PlayerBuffManager`, kể cả Guardian Shield kích hoạt ngay), đánh dấu đã mua và gửi kết quả cho đúng người mua bằng `ClientRpc` nhắm chủ nhân. Không hợp lệ thì chỉ báo lỗi cho người đó, không đổi trạng thái nào.
- KHÔNG có: bán lại đồ, làm mới kho, giá đổi theo số lần mua.

### 17.9 Bản đồ (mục 16.8) — cập nhật nhỏ

- Phòng đã lộ hiển thị biểu tượng theo loại: `Boss` đầu lâu (đã có), `Upgrade` tế đàn, `Treasure` rương, `Shop` đồng xu, `Combat` chưa dọn có ổ khóa nhỏ (biến mất khi `Cleared`). `Spawn` không biểu tượng.

### 17.10 Chưa làm ở bản này

Nhiều đợt quái trong một phòng, phòng mini-boss, phòng bí mật, chìa khóa, tiền vĩnh viễn giữa các lượt (kiểu Gears của Hellhole), bán lại đồ, làm mới kho shop, đạn/tầm nhìn xuyên song sắt, cắt cảnh khi vào phòng boss. AI/lập trình viên KHÔNG ĐƯỢC tự thêm các mục này.

### 17.11 Tiêu chí nghiệm thu (test 2–4 máy)

1. Gameplay không còn cột/sàn test; sàn không nhấp nháy.
2. Hành lang nhìn thấy được từ cửa phòng (đèn, khung cửa, sàn khác màu) và không có quái trong hành lang.
3. Vào phòng `Combat`: cổng hạ ở mọi cửa, quái xuất hiện và không ra khỏi phòng; diệt hết thì cổng mở; vào lại không bị khóa.
4. Người đứng ngoài lúc cổng hạ bị chặn đến khi phòng mở; revive đồng đội gục bên trong vẫn được.
5. Phòng `Boss`: khóa khi bước vào, boss mới spawn, `GameState = BossFight`; boss chết thì mở cổng và `Victory`.
6. Loại phòng và cột **giống hệt** trên mọi máy cùng seed; các phòng Upgrade/Treasure/Shop nằm ở nhánh cụt khi có thể.
7. Upgrade: mỗi người dùng tế đàn đúng 1 lần. Treasure: có 2 rương.
8. Shop: 5 món giống nhau trên mọi máy; người 1 mua A, B thì người 2 vẫn mua được A, B; mua thiếu tiền / mua lại / đứng xa / sai trạng thái đều bị Server từ chối.
9. Linh hồn: hạ quái cả đội cùng được cộng; reset về 0 khi lượt mới.

---

## 18. Ghi nhận thay đổi của nhóm so với đặc tả gốc (đã có trong nhánh `feature/shrine-and-stats`)

- Bỏ vũ khí cận chiến; mọi vũ khí là súng bắn xa.
- Đạn dự trữ vô hạn; băng đạn (`MagSize`) vẫn có giới hạn, HUD hiển thị `đạn hiện tại / băng đạn`.
- Nâng cấp súng toàn cục giữ nguyên khi đổi súng; modifier qua `WeaponModifierData`; `PlayerStats` (máu cộng thêm, giáp, tốc chạy). Chi tiết công thức giáp xem `Documentation/Hellfire_Bao_Cao_Kien_Truc_Va_Tac_Dong.md`.
- Tế đàn chọn 1 trong 3 (`InteractiveShrine`), mỗi người chơi dùng một lần mỗi tế đàn.

Các điểm này **được công nhận là đúng** và thắng các mô tả cũ ở mục 3, 6.1, 6.2 nếu mâu thuẫn (ví dụ về "đạn dự trữ"). AI/lập trình viên KHÔNG ĐƯỢC hoàn nguyên chúng.

---

## 19. Tích hợp từ các thành viên khác — Giai đoạn 4.7

> Mục này ghi nhận những gì chủ dự án đã quyết định **giữ lại** từ công việc của Benz2311 và Phước. Nếu mâu thuẫn với mục 3, 5, 6, 16.12 thì **mục 19 thắng**.

### 19.1 Nguyên tắc

- Nền tích hợp là nhánh `merge/phuoc-fixes` (đã gộp bản sửa của Phước, đã đạt toàn bộ bất biến mục 14). Mọi tích hợp mới làm trên một nhánh con của nó, không động vào `main`.
- AI KHÔNG hoàn nguyên, không viết lại từ đầu, không đổi tên các script của người khác. Chỉ sửa tối thiểu để chúng biên dịch, chạy đúng trên mạng, và không phá các bất biến ở mục 14.
- Các scene/prefab test do họ tạo (`TestCombatJuice`, `TestArcherAI`, `TestShrine`...) giữ làm công cụ dev; không đưa vào luồng chơi thật.

### 19.2 Cảm giác súng của Benz2311 — ĐÃ CHỐT: GIỮ LẠI

- Gồm `WeaponSwayBobbing`, `WeaponRecoil`, `WeaponAimDownSights`, `WeaponMotionCoordinator` (commit `0037194`, nhánh `feature/sprint-1-combat-juice`), kèm các thay đổi nhỏ trong `WeaponController`/`WeaponData`.
- **Các component này hiện chỉ được gắn vào `Player_CombatJuice_Test.prefab`.** PHẢI gắn chúng vào **`Player.prefab` thật**, trên cùng GameObject viewmodel mà công cụ `HellfireCombatJuiceSetup` đã dùng cho prefab test, để ván chơi thật có cảm giác súng.
- Chỉ chạy trên máy **chủ sở hữu** (`IsOwner`/`CanLocalControl`), thuần hình ảnh. KHÔNG được làm đổi luật mục 3.1: Server vẫn tự raycast xác nhận và tính sát thương; hướng bắn gửi lên Server là hướng thực của camera tại thời điểm bắn.
- Công tắc "Giảm rung màn hình" (mục 16.9) PHẢI áp dụng cho hiệu ứng giật/rung mới nếu có rung màn hình.

### 19.3 Đạn — ĐÃ CHỐT: VẪN VÔ HẠN

- Đạn dự trữ vô hạn như mục 18; băng đạn vẫn hữu hạn. Nhánh `test-weapons` của Phước có đạn dự trữ hữu hạn (mặc định 120) nhưng **không có nguồn tiếp đạn**: phần này KHÔNG được tích hợp. Khi tích hợp công việc của Phước về sau, đạn vô hạn thắng.

### 19.4 Quái tầm xa `RangedZombieAI` của Benz2311 — ĐÃ CHỐT: THÊM VÀO

- Quái thứ 4 của phòng Combat (tên hiển thị "Zombie tầm xa"). Kế thừa `EnemyStateMachine`, chạy CHỈ trên Server (mục 5.1), có phát hiện, kiểm tra tầm nhìn, giữ khoảng cách và đòn tấn công có báo trước; hiệu ứng gửi bằng `ClientRpc`.
- Cần một **prefab chính thức** (không dùng `Enemy_Archer_Test.prefab`): cấu trúc giống các prefab quái hiện có (`NetworkObject`, `NetworkTransform` với `AuthorityMode = Server`, `Health` + hitbox, `NavMeshAgent`, `RangedZombieAI`, `RangedZombieVisuals`), đăng ký vào `DefaultNetworkPrefabs` qua Inspector của Unity.
- Thưởng Linh hồn `_soulsReward = 8` (như Archer). Hệ số số lượng theo số người (mục 15.3) áp dụng như quái thường. KHÔNG dùng làm quái do boss triệu hồi.
- `DungeonEnemySpawner` đổi sang chọn loại quái theo **trọng số `[SerializeField] int`**, mặc định: Imp 40, Archer 25, Zombie tầm xa 15, Brute 20 (tổng 100). Mọi vị trí spawn vẫn qua `NavMesh.SamplePosition` (mục 15.2).

### 19.5 Chưa tích hợp ở bản này

- Nhánh `test-weapons` của Phước (model súng 3D, crosshair HUD, ammo HUD, FX pool, giật/ADS riêng): chờ Phước rebase lên nền mới; phần giật/ADS của Phước KHÔNG dùng vì đã chọn bản của Benz.
- Công cụ test offline thứ ba của Benz (`LocalTestHostBootstrap`, `OfflineDummyHealth`, `TargetDummyFeedback`): không mang vào.
- `WeaponVisualEffects` của Benz: chỉ mang vào nếu gắn được mà không phải sửa `WeaponController` nhiều; nếu không thì bỏ và báo lại.

### 19.6 Tiêu chí nghiệm thu

1. Unity biên dịch sạch, Console không lỗi đỏ; checklist mục 14 vẫn Đạt (đặc biệt: `Player.prefab AuthorityMode = 1`, một `OnDestroy` trong `PlayerMovement`, bản vá cờ spawn xong).
2. Vào ván thật: có sway/bobbing khi đi, giật khi bắn, ADS khi giữ chuột phải; Client và Host đều thấy đúng và sát thương vẫn do Server xác nhận.
3. Đạn dự trữ vẫn vô hạn: hết băng thì nạp lại được mãi.
4. Phòng Combat thỉnh thoảng có Zombie tầm xa: nó giữ khoảng cách, bắn có báo trước, chết thì cả đội nhận 8 Linh hồn, tổng kết lượt chơi ghi nhận hạ gục.
5. Cổng sắt vẫn giữ quái trong phòng, phòng vẫn chỉ mở khi diệt hết (kể cả Zombie tầm xa).
