# Gameplay Showcase – demo bàn giao

Unity 6000.5.6f1. Mở **Assets/_Project/Scenes/TestZombieShowcase.unity**, Play rồi click Game View. Đây là demo chính duy nhất; không cần khởi động Lobby để thử local Host.

Điều khiển: WASD; Left Shift chạy; Space nhảy; Left Ctrl/C ngồi; giữ W+Left Shift rồi nhấn Ctrl/C để trượt; LMB bắn; RMB ADS; R reload; Esc mở con trỏ. Zombie tự chiến đấu sau khi Player spawn.

Đã cấu hình trên Player_Saiga_Test: Saiga và bảy clip, ADS/camera recoil/sway/bob, Stamina/Crouch/Slide/HUD có sẵn, Hitmarker; ba Zombie có Damage Numbers dùng pool, audio và VFX. Không viết lại AI hoặc sửa code Player/Health/WeaponController của nhóm.

## Kết quả Play Mode

- Đi bộ 4,5 m/s, chạy 7 m/s; Stamina 100→91,6 khi chạy rồi hồi 100.
- Crouch: collider 1,8→1,01 m; Slide=True, collider khoảng 0,83 m, trừ thể lực.
- Space nhảy khoảng 1,09 m; chuột, LMB gây damage và ADS FOV=65 hoạt động.
- Hitmarker có component nhận event; mỗi phát trúng quan sát được một DamagePopup đang hoạt động. Fire/Reload Fast/Full đã kiểm tra ở lượt Saiga trước.
- Runner/Archer/Brute chết, collider tắt, NavMeshAgent tắt và despawn; sau cả ba, Player vẫn 4,5 m/s, khoảng 33,33 ms/frame (~30 FPS), không va collider xác.
- Lượt native AI riêng: Archer 100→88 HP, Runner 88→78→68→58, Brute 58→33; ba attacker có damage thật. Headshot Brute 140→110, stagger=True; bắn tiếp chết, dừng NavMesh.
- Console kiểm tra không có Error/Exception. Input di chuyển/kỹ năng được kiểm bằng thiết bị ảo Input System đi qua script thật; chưa thay thế nghiệm thu thao tác tay, build standalone hay phiên co-op nhiều máy.

Log: `FPSArmsEvidence/gameplay-showcase-tests.txt`, `CodexShowcaseEvidence/attack-fix-tests.txt`. Ảnh: `FPSArmsEvidence/Gameplay_Showcase_GameView.png`.

## Scene giữ và Scene không dùng bàn giao

Build Settings chỉ giữ MainMenu, Lobby, Gameplay của nhóm và TestZombieShowcase. Gameplay.unity được giữ nguyên.

Các Scene test khác không còn trong danh sách Build Settings của bản bàn giao: TestArcherAI, TestCombatJuice, TestZombieHearing, TestZombieGroupAI, TestZombieVisualComparison, TestPlayerAbilities, TestPlayerAbilities_P1, TestSprint1_GameplayExpansion, TestAllZombieCombatPlaytest, Hellfire_Zombie_Showcase, Hellfire_Zombie_Combat_Arena, Hellfire_Zombie_Combat_Arena_V2. **Không xóa file Scene/meta/prefab/asset nào**; các Scene có sẵn trong lịch sử nền vẫn được giữ.

## Dependency và Git

`gameplay-showcase-dependencies.json` liệt kê dependency serialized và `.meta` do Unity xuất. Chỉ chuyển demo cùng dependency/tài liệu/ảnh-log cần thiết vào worktree bàn giao. Không đưa Temp_Monsters, recovery, toàn bộ thư mục Art hoặc các thay đổi ngoài phạm vi vào commit.

Nền bàn giao là commit `0111e8b` của nhánh đã public `origin/feature/player-abilities` (chung nền `81e04eb` với nhánh làm việc). Các hook PlayerMovement hiện có khớp byte với nền này; không đưa thay đổi core chưa duyệt vào commit. Working tree gốc đang bẩn được bảo toàn trên nhánh cũ, dùng worktree riêng cho feature/gameplay-showcase. Cache glyph động của font fallback được giữ theo bản đã commit, không đưa cache phát sinh từ Play Mode vào bàn giao.

Worktree bàn giao đã compile riêng 103 source runtime và 9 source Editor bằng compiler/reference của Unity 6000.5.6f1: exit code 0, còn warning API cũ/field chưa dùng. Đây là kiểm tra compile dependency, không phải build standalone hoặc lần mở Unity Editor thứ hai. Không chạy Batchmode.

Xem `ASSET_CREDITS.md` cho ghi công asset đã có. Cần Unity khôi phục đúng các package trong manifest khi mở trên máy khác. Không chạy thêm Unity Batchmode cùng dự án đang mở.
