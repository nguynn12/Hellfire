# Hellfire — Codex Working Rules

## Phạm vi và nguồn yêu cầu
- Trả lời người dùng bằng tiếng Việt.
- Trước công việc Blender, đọc `Documentation/CLAUDE.md`,
  `Blender/MASTER_PROMPT.md`, Asset Brief hiện hành và Design liên quan.
- Chỉ thị hiện hành của người dùng được ưu tiên. Báo xung đột quan trọng.
- Không xem trạng thái duyệt, số đo hay export của bản cũ là bằng chứng
  cho asset mới.

## Bảo vệ dự án
- Không mở, khôi phục, sửa hoặc tái sử dụng nội dung trong
  `_Quarantine/HellsWhisper_Cleanup_20261104/`.
- Bản Hell's Whisper mới là prop game phi chức năng, dựng độc lập;
  không tự kế thừa cơ cấu hay thông số kỹ thuật của bản đã nghỉ dùng.
- Không chỉnh Unity nếu chưa được yêu cầu rõ ràng.
- Không reset scene chưa lưu, xóa dữ liệu không thuộc lượt làm việc,
  hoặc ghi đè tài liệu/source đã có khi chưa được duyệt.
- Không cài/cập nhật phần mềm hoặc thay addon khi chưa được cho phép.

## Phân công skill bắt buộc
- arjun988: modeling, hard-surface, sculpting, procedural geometry,
  retopology và UV liên quan modeling.
- EL4CTEO / blender-agent-skills: PBR, texturing, baking, lighting,
  lookdev, LOD, optimization và asset QA.
- CoplayDev `blender-to-unity`: chỉ phụ trách export/Unity handoff.
- Không dùng nhánh geometry/export của bộ khác để vượt phân công này.
- Đọc SKILL.md thực tế và tài nguyên liên quan trước khi dùng.
  Bộ đã khảo sát nằm tại `~/.gemini/config/plugins/`.
  File tồn tại không đồng nghĩa skill đã được Codex tự động nạp.

## Blender MCP
- Kiểm tra tool và schema thực sự được cấp trong phiên hiện tại.
- Ưu tiên tool MCP trực tiếp. Nếu chỉ dùng được Python MCP client,
  nói rõ giới hạn; không tuyên bố có truy cập tool trực tiếp.
- Trước khi sửa scene, đọc file hiện hành, scene và dữ liệu liên quan.
- Chỉ chạy bpy trong phạm vi đã được cho phép; kiểm tra kết quả trả về.
- Khi cần quyền ngoài sandbox, trình bày lệnh và xin quyền trước.
- Nếu tool trực tiếp không xuất hiện sau restart, kiểm tra lỗi khởi động
  hoặc giới hạn thực tế; không suy đoán nguyên nhân thành kết luận.

## Quyền phê duyệt nghệ thuật
- AI không được tự phê duyệt chất lượng nghệ thuật của sản phẩm mình tạo.
- AI được phân tích hình ảnh, chỉ ra khuyết điểm và tinh chỉnh geometry
  trong phạm vi đã được duyệt.
- Người dùng quyết định phê duyệt cuối cùng đối với silhouette chính,
  art direction và việc chuyển giữa các giai đoạn sản xuất chính.
- Script chạy thành công, file Blender hợp lệ hoặc topology đạt kiểm tra
  không chứng minh chất lượng nghệ thuật.

## Chiến lược geometry
- Không áp đặt một kỹ thuật modeling cho mọi asset. Chọn phương pháp
  theo hình học, yêu cầu thị giác và khả năng chỉnh sửa.
- Primitive phù hợp được phép sử dụng; không coi việc xếp chồng primitive
  là modeling hoàn chỉnh.
- Ưu tiên hình khối thống nhất, silhouette mạnh, tính nhất quán nghệ thuật
  và các chuyển tiếp hình khối có chủ đích.

## Quy trình dựng và kiểm tra thị giác
- Brief → blockout → duyệt silhouette → refinement → UV/bake/materials
  → optimization/QA → source cuối → handoff nếu được yêu cầu.
- Chỉ chuyển giai đoạn sản xuất chính sau khi người dùng phê duyệt.
- Dùng `look` chụp viewport và các góc lặp lại được.
  Mở ảnh bằng công cụ thị giác trước khi nhận xét hình thức.
- Sau thay đổi đáng kể: xem ảnh → xác định lỗi → sửa trong phạm vi
  đã duyệt → chụp lại cùng góc. Không coi script thành công là đủ.
- Kiểm tra Solid và wireframe; thêm material/ánh sáng trung tính
  khi đánh giá vật liệu. Tách quan sát ảnh khỏi dữ liệu scene.

## Technical QA và Artistic QA
- Báo cáo riêng hai phần; không coi technical QA đạt là đủ để tuyên bố
  asset production-ready.
- Technical QA: kiểm tra tính toàn vẹn geometry, normals, transforms,
  topology, evaluated triangles và tổ chức asset; báo số đo, lỗi và
  cảnh báo còn lại. Không áp ngân sách polygon tùy tiện.
- Artistic QA: đánh giá silhouette, tỷ lệ, phân cấp thị giác, ngôn ngữ
  hình khối, chất lượng bề mặt và sự thống nhất nghệ thuật tổng thể.
  Đây là nhận định để người dùng xem xét, không thay quyền phê duyệt.

## An toàn transforms
- Không tự động Apply Rotation/Scale cho mọi object.
- Trước khi apply, đánh giá phụ thuộc modifier, hierarchy, animation
  và nhu cầu export; chọn object và thời điểm phù hợp.
- Nếu yêu cầu apply trong tài liệu khác xung đột với các phụ thuộc này,
  báo xung đột và đề xuất cách xử lý trước khi thay đổi rủi ro.

## Đường dẫn và bàn giao
- Source mới: `Blender/Blender_Assets/<category>/`.
- Design: `Blender/Blender_Design/<category>/`.
- Giữ một file .blend cuối có thể chỉnh sửa; không ghi đè bản hiện có.
- Dùng mét, 1 unit = 1 m; pivot vũ khí tại grip.
- Bảo toàn emission; xác minh chuyển trục, scale và vật liệu khi handoff.
- Chỉ export bằng CoplayDev khi được duyệt và đủ công cụ.
- Chưa kiểm tra trong Unity thì ghi `Not verified in Unity`.

## Báo cáo dựa trên bằng chứng
- Phân biệt rõ: quan sát đã xác minh, kết quả kỹ thuật đã đo,
  nhận định nghệ thuật, giả định chưa xác minh và giới hạn còn lại.
- Không bịa lời gọi tool thành công, screenshot, thao tác file
  hoặc deliverable đã hoàn thành.
- Chỉ báo hoàn thành thao tác hay bàn giao khi đã kiểm tra kết quả thực tế.
