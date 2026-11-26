# QUY TRÌNH TẠO ASSET 3D (PIPELINE RULES)
Bắt buộc tuân thủ nghiêm ngặt dây chuyền sau khi tạo asset cho game Hellfire:

1. **Modeling & Sculpting (Dựng hình):** CHỈ SỬ DỤNG kỹ năng từ bộ `arjun988`.
2. **Materials, Lighting & LOD (Tối ưu & Vật liệu):** CHỈ SỬ DỤNG kỹ năng từ bộ `EL4CTEO`. 
3. **Export & Unity Import (Xuất file):** TUYỆT ĐỐI KHÔNG dùng công cụ export của arjun988 hay EL4CTEO. CHỈ SỬ DỤNG kỹ năng `blender-to-unity` của CoplayDev.

# NGỮ CẢNH GAME HELLFIRE & YÊU CẦU KỸ THUẬT
- **Engine:** Unity 6.
- **Phong cách nghệ thuật:** Dark Fantasy / Doom-style. Kim loại gỉ sét, thô ráp, xương xẩu, kết hợp ma thuật hắc ám và ánh sáng dung nham (đỏ/cam).
- **Quy tắc Vũ khí (VUI LÒNG ĐỌC KỸ):** 
  + **Pivot Point (Điểm xoay):** BẮT BUỘC đặt điểm Origin của mô hình ngay tại vị trí Tay cầm (Grip) để nhân vật FPS cầm súng chuẩn xác.
  + **Vật liệu:** Phải giữ nguyên vẹn kênh phát sáng (Emission map) khi xuất qua Unity cho các chi tiết dung nham/rune.
- **Tỷ lệ (Scale):** 1m = 1 Unit. BẮT BUỘC thực hiện Apply Scale và Apply Rotation (Ctrl+A) trước khi xuất.
- **Định dạng:** Ưu tiên .FBX cho vũ khí hitscan/mesh tĩnh. Dùng .GLB nếu có animation/xương.