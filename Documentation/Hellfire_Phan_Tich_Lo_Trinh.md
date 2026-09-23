# Dự án Game Hellfire — Phân tích và Lộ trình

> Tài liệu này giữ nguyên nội dung gốc của nhóm và bổ sung các đề xuất kỹ thuật/quản lý (đánh dấu 💡) nhằm giảm rủi ro trong quá trình phát triển.

## 1. Cơ chế cốt lõi

- **Góc nhìn và Đồ họa**: Đồ họa 3D, sử dụng góc nhìn thứ ba qua vai nhân vật hoặc nhìn nghiêng từ trên xuống.
- **Cơ chế chiến đấu**: Nhịp độ cực nhanh. Sát thương được tính ngay lập tức khi bóp cò thay vì dùng vật thể đạn bay chậm. Người chơi sẽ cảm nhận được đường đạn thông qua các vệt sáng đồ họa lướt qua màn hình.
- **Chế độ nhiều người chơi**: Cho phép 2 đến 4 người cùng chơi phối hợp thông qua kết nối mạng LAN cục bộ.
- **Cấu trúc màn chơi**: Các phòng được kết nối với nhau bằng hệ thống hành lang, toàn bộ sơ đồ ngục tối được máy tính tự động sinh ra ngẫu nhiên mỗi khi bắt đầu một lượt chơi mới.
- **Vòng lặp trò chơi**: Người chơi tiến vào hầm ngục, tiêu diệt quái vật, thu thập vũ khí và bùa lợi, đánh trùm cuối. Nếu chết sẽ chơi lại từ đầu, nếu thắng sẽ qua màn với các chỉ số mạnh hơn.

💡 **Đề xuất bổ sung:**
- Nên giới hạn rõ **phạm vi MVP** (bản chơi được đầu tiên): ví dụ 1 tầng ngục, 3–4 loại quái, 3–5 loại vũ khí, 1 boss. Việc này giúp nhóm 3 người tránh phình phạm vi (scope creep) khi mới bắt đầu.
- Cân nhắc định nghĩa rõ "chỉ số mạnh hơn" khi qua màn là gì (máu, sát thương, hay vật phẩm vĩnh viễn) để tránh mất cân bằng khó xử lý về sau.

## 2. Giải pháp kỹ thuật

- **Phần mềm phát triển**: Unity 3D.
- **Hệ thống mạng**: Sử dụng thư viện Netcode for GameObjects (NGO) của Unity. Cấu hình giao thức vận chuyển ưu tiên kết nối trực tiếp qua địa chỉ IP mạng LAN để giảm độ trễ, không qua máy chủ trung gian.
- **Xử lý đường đạn**: Bắn tia dò tìm (raycast) từ nòng súng đến mục tiêu để tính sát thương ngay lập tức. Dùng công cụ vẽ đường thẳng hiển thị một vệt sáng chớp nhoáng trong một phần mười giây để tạo cảm giác có tia đạn bay qua, kết hợp với hiệu ứng hạt tại nòng súng và vị trí trúng đạn.
- **Trí tuệ nhân tạo quái vật**: Kết hợp hệ thống máy trạng thái (state machine) để chuyển đổi hành vi và công cụ lưới điều hướng (NavMesh) để tìm đường đi. Lưới điều hướng này phải được nướng (bake) trực tiếp ngay trong lúc chơi vì bản đồ là sinh ngẫu nhiên.

💡 **Đề xuất bổ sung để giảm rủi ro kỹ thuật:**

| Vấn đề | Rủi ro nếu bỏ qua | Đề xuất giải pháp |
|---|---|---|
| Đồng bộ bản đồ ngẫu nhiên | Mỗi máy sinh bản đồ khác nhau → game vỡ trận | Host sinh một **seed** duy nhất, gửi seed cho mọi client; mỗi máy tự sinh lại bản đồ giống hệt từ seed đó thay vì gửi toàn bộ dữ liệu bản đồ qua mạng |
| Tìm phòng trong LAN | NGO không có sẵn tính năng LAN discovery | Tự viết cơ chế broadcast UDP để các máy tự tìm thấy nhau, hoặc chấp nhận nhập IP thủ công cho bản đầu |
| Host rời phòng giữa chừng | Cả phòng bị văng nếu người làm host thoát/rớt mạng | Quyết định sớm: có làm "host migration" (chuyển giao host) hay không. Nếu không đủ thời gian, ít nhất nên có xử lý thông báo rõ ràng và đưa người chơi về màn hình chờ an toàn thay vì crash |
| AI chạy trên nhiều máy | Quái vật "giật/lệch vị trí" giữa các máy nếu mỗi máy tự tính AI | Áp dụng mô hình **server-authoritative**: chỉ máy chủ tính toán AI và va chạm, client chỉ nhận vị trí/animation để hiển thị |
| Đồng bộ máu và sát thương | Dữ liệu máu lệch giữa server/client gây tranh cãi "ăn gian" | Máu và sát thương chỉ được tính và xác nhận ở server (server-authoritative), client chỉ gửi input, không tự trừ máu |
| Bake NavMesh lúc chạy | Bake đồng bộ (synchronous) trên bản đồ lớn có thể gây giật/đứng hình lúc vào màn | Ưu tiên bake bất đồng bộ (async), hoặc bake từng phòng một rồi ghép lại thay vì bake toàn bộ bản đồ cùng lúc |
| Nguồn tài nguyên (asset) | Vi phạm bản quyền hoặc chậm tiến độ vì tự vẽ mọi thứ | Xác định rõ asset 3D/âm thanh nào tự làm, asset nào mua từ Unity Asset Store, tránh vấn đề bản quyền |
| Quản lý mã nguồn | Ghi đè code, mất lịch sử thay đổi khi 3 người cùng sửa | Dùng Git (kèm Git LFS cho file Unity lớn) và quy ước nhánh (branch) rõ ràng ngay từ Giai đoạn 1 |

## 3. Lộ trình phát triển

- **Giai đoạn 1 — Xây dựng nền tảng**: Khởi tạo dự án, thiết lập hệ thống mạng và tạo bộ điều khiển nhân vật 3D cơ bản. Đảm bảo các máy có thể tạo phòng và vào phòng qua mạng LAN.
- **Giai đoạn 2 — Hoàn thiện chiến đấu**: Lập trình tính năng bắn tia dò tìm, vẽ hiệu ứng đường đạn và tạo quái vật thử nghiệm. Cần đồng bộ chính xác lượng máu và hiệu ứng bắn giữa máy chủ và máy khách.
- **Giai đoạn 3 — Sinh bản đồ và tạo AI**: Lập trình thuật toán ghép các phòng lại với nhau, nướng lưới điều hướng ngay khi bản đồ vừa tạo xong. Cài đặt cho quái vật biết cách tìm và đuổi theo người chơi gần nhất.
- **Giai đoạn 4 — Vật phẩm và hoàn thiện**: Thiết kế hệ thống rương rơi vũ khí, bùa lợi, làm giao diện máu, đạn và màn hình tạm dừng. Xử lý logic chuyển cảnh khi qua màn.

💡 **Đề xuất bổ sung — Giai đoạn 5 (mới): Playtest & Polish**
- Dành riêng thời gian để 3 thành viên (và người ngoài nếu có thể) chơi thử nhiều lần, ghi nhận lỗi và cảm giác chơi (game feel).
- Cân bằng lại độ khó quái vật, sát thương vũ khí, tốc độ hồi máu.
- Sửa lỗi đồng bộ mạng phát sinh khi test với 4 người chơi thật (khác với test 1 người).
- Kinh nghiệm thực tế: giai đoạn polish thường tốn thời gian tương đương một giai đoạn phát triển tính năng, nên đừng bỏ qua khi lập kế hoạch thời gian.

💡 **Đề xuất bổ sung — Nên gắn mốc thời gian cụ thể** cho từng giai đoạn (ví dụ theo tuần) để dễ theo dõi tiến độ, thay vì chỉ liệt kê đầu việc. Có thể dùng bảng Kanban đơn giản (Trello/Notion) để 3 người theo dõi lẫn nhau.

## 4. Phân bổ nhân sự

- **Tạ Nhật Nguyên**: Chịu trách nhiệm thiết lập nền tảng mạng LAN, lập trình bộ điều khiển nhân vật và thuật toán sinh bản đồ ngục tối ngẫu nhiên.
- **Võ Hùng Mạnh**: Xử lý cơ chế bắn tia dò tìm, lập trình hệ thống vũ khí, làm các hiệu ứng hình ảnh đường đạn và âm thanh chiến đấu.
- **Trần Ngọc Bảo Phước**: Lập trình trí tuệ nhân tạo cho quái vật và Boss, xử lý hệ thống tìm đường, thiết kế giao diện người dùng và hệ thống rương vật phẩm.

💡 **Đề xuất bổ sung về phân bổ nhân sự:**
- Tạ Nhật Nguyên đang đảm nhận cả **network nền tảng** lẫn **thuật toán sinh bản đồ** — đây là hai phần khó và tốn thời gian nhất của dự án, dễ trở thành điểm nghẽn (bottleneck) khiến các phần khác phải chờ. Nếu có thể, nên chia nhỏ: ví dụ Nguyên tập trung network trước, thuật toán sinh bản đồ có thể làm cùng hoặc nhờ hỗ trợ từ Phước sau khi Phước xong phần AI.
- Trần Ngọc Bảo Phước phụ trách **AI (kỹ thuật)** và **thiết kế giao diện người dùng (thiết kế/UX)** — hai kỹ năng khá khác nhau. Nếu Phước mạnh về lập trình hơn là thiết kế hình ảnh, có thể để Võ Hùng Mạnh hỗ trợ phần UI khi rảnh giữa Giai đoạn 2 và 3, tránh UI bị làm vội ở cuối dự án.
- Nên có **buổi đồng bộ tiến độ ngắn hàng tuần** giữa 3 người, đặc biệt quan trọng ở Giai đoạn 1–2 vì network và chiến đấu là hai hệ thống phụ thuộc lẫn nhau chặt chẽ.

## Tóm tắt rủi ro cần theo dõi sát nhất

1. Đồng bộ mạng (bản đồ, máu, AI) — rủi ro kỹ thuật cao nhất, nên làm đúng ngay từ Giai đoạn 1–2.
2. Hiệu năng bake NavMesh thời gian thực trên bản đồ ngẫu nhiên.
3. Điểm nghẽn nhân sự ở Tạ Nhật Nguyên (network + sinh bản đồ).
4. Thiếu thời gian polish/cân bằng ở cuối dự án nếu không lên lịch từ đầu.
