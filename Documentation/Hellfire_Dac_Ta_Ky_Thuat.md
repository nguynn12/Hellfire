# Hellfire — Đặc tả kỹ thuật (Technical Specification)

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
│   └── PlayerCamera (Camera, Field of View mặc định 90 độ, chỉ active trên client sở hữu — IsOwner)
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
7. Random hóa loại phòng (phòng thường/phòng có rương vật phẩm) cho các phòng còn lại bằng `rng`, theo tỷ lệ cấu hình được (ví dụ 30% phòng có rương).
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

| Loại rương | Tỷ lệ xuất hiện trong màn (mục 4.1 bước 7) | Kết quả mở ra |
|---|---|---|
| Rương thường | ~70% số rương trong màn (theo tỷ lệ cấu hình ở mục 4.1) | 40% → 1 bùa lợi ngẫu nhiên (trong 4 loại ở mục 6.2)<br>60% → 1 vũ khí ngẫu nhiên (trong 4 khẩu ở mục 6.2) |
| Rương boss | Đúng 1 rương, spawn khi boss chết (mục 5.2) | 100% → ít nhất 1 bùa lợi, cộng tỷ lệ rơi thêm 1 vũ khí (con số % cụ thể cho phần "rơi thêm vũ khí" ở rương boss vẫn để nhóm quyết định sau, xem mục 13) |

**NÊN** khi Server chọn vũ khí ngẫu nhiên rơi ra từ rương thường, loại trừ khẩu vũ khí người chơi đang cầm ra khỏi danh sách chọn (tránh rơi trùng vũ khí đã có) — nếu người chơi đã sở hữu hết cả 4 khẩu, cho phép rơi trùng bình thường. *(Đây là đề xuất mặc định hợp lý, chưa phải quyết định cứng — nhóm có thể đổi nếu muốn cho rơi hoàn toàn ngẫu nhiên kể cả trùng vũ khí đang cầm.)*

---

## 7. Giao diện người dùng (UI/HUD)

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
     → BossFight (đã vào phòng boss — kích hoạt nhạc/UI riêng theo mục 9.3)
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
