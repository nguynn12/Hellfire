# HELLFIRE · Phase 1 integration

Nền tích hợp là `backup/pre-integration-20261125` tại `dcd4b65`, hậu duệ của `origin/main` tại `8ce72cc`. Giữ nguyên vòng MainMenu → Lobby → Gameplay, DungeonManager, Netcode authority và dữ liệu floor scaling.

| Nguồn | Quyết định |
|---|---|
| `feature/floor-scaling` và 6 commit trước backup | Giữ toàn bộ. Dungeon theo seed, room themes, NavMesh, gom đội, khóa phòng, scaling quái và HUD tầng tiếp tục là đường chạy chính. |
| `feature/combat-feedback` (`b6be612`) | Dùng pool số sát thương và prefab tham chiếu. Kết nối qua Health hiện tại, chỉ hiện cho người bắn sau khi nhận kết quả từ server. Giữ hitmarker, HUD đạn và chỉ hướng sát thương uGUI hiện có. |
| `feature/player-abilities` (`0111e8b`) | Gắn stamina chạy vào đường input của owner trong PlayerMovement, giữ quyền điều khiển hiện tại và multiplier buff. Chưa đưa crouch/slide từ prefab test vào vì thay đổi collider liên quan đến khóa cổng, revive và đồng bộ người chơi. |
| `feature/gameplay-showcase` (`12f3700`) | Dùng clip âm thanh của Pistol/Rifle trên WeaponData hiện hành. Nhập bộ FPS arms, animation và adapter với GUID gốc để tiếp tục gắn khi duyệt loadout. Bản đồ và prefab showcase tiếp tục tách riêng. |
| `codex/infected-excavation` (`3859354`) | Bản đồ có bootstrap loopback riêng và cần package ngoài repo. Cần chuyển bố cục sang DungeonManager đang dùng trước khi đưa vào flow chính. |
| `test-weapons`, `fix/shrine-and-stats` | Giữ các thay đổi phù hợp đã có trong Player, loot prefab, registry Netcode và hệ thống nâng cấp trên nền chính. Hoãn refactor vũ khí/offline mode, scene test, font cache và HUD OnGUI cũ. |
| `feature/combat-system`, `feature/sprint-1-combat-juice`, `feature/my-gameplay-and-ranged-zombie` | ADS/recoil/sway và ranged zombie đã có qua `integration/combat-feel-and-zombie`. |
| `feature/zombie-ai`, `feature/zombie-feedback` | Các biến thể hearing, projectile, stagger/audio cần port và kiểm tra trên prefab runtime hiện tại. Chưa thay AI hoặc các prefab Test. |
| Các remote ref còn lại | Không có commit ngoài nền tích hợp ở lần fetch đầu, nên không cần merge riêng. |

`WeaponController.cs`, Player, Gameplay, MainMenu, Cube, TMP và `Assets/_Project/Art.meta` kế thừa bản backup. Pistol chỉ nhận liên kết AudioClip. Art.meta tùy biến sẵn được giữ, các tài nguyên mới dùng file .meta của từng asset. Công cụ thử nghiệm Hell’s Whisper được chuyển sang gọi tường minh để không tạo hoặc dựng lại asset khi vừa mở Editor.

## Ghi công

**FPS Arms saiga animations remake** của Cransh (@ccransh), theo [bản ghi giấy phép của nhóm](https://github.com/nguynn12/Hellfire/blob/12f37008f6ef65d22e7160cc04033072903d7e3e/Docs/MyFeatures/ASSET_CREDITS.md), giấy phép [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). [Tác phẩm trên Sketchfab](https://sketchfab.com/3d-models/fps-arms-saiga-animations-remake-74e30b71e4a049b2a82b9d18f58e623c). Giữ các clip, material, texture và FBX do nhóm bàn giao. Adapter Unity được chỉnh để dùng quyền local và thời lượng reload thực tế của nhánh tích hợp.

## Kiểm thử trên Unity 6000.5.6f1

| Hạng mục | Kết quả |
|---|---|
| Unity Editor compile và macOS QA build | PASS, 0 lỗi. Build report còn 358 cảnh báo, gồm API cũ và log build package. Những thay đổi serialize do Editor tạo ở project settings/URP và TMP được loại khỏi commit. |
| Asset scan trong Editor | PASS trên 3 scene, 19 prefab và 34 material. Không missing script hoặc shader error theo kiểm tra. Đối chiếu các GUID của tài nguyên thêm mới với Assets và URP/uGUI trong manifest. |
| MainMenu → Lobby → Gameplay và đồng bộ dungeon | PASS, Host và Client là hai process loopback. Seed 784129 và 549171 đều sinh 11 phòng, cùng loại, size, theme và số nhóm vật cản giữa Host/Client. |
| Tương tác và trạng thái | PASS, mở rương bằng input E, stamina giảm khi sprint, revive bằng giữ E, party 2 người trong phòng khóa, 7 quái, gate reopen, BossFight và Victory. Lượt riêng cả đội gục và hai process nhận GameOver. |
| Phạm vi chưa kiểm thử | NOT TESTED, LAN nhiều máy, chơi tay đầy đủ, nhận vật phẩm rơi sau khi mở rương, kiểm tra bằng tai/ảnh từng asset mới. Không có Unity Test Framework test được phát hiện trong source gameplay. Combat/Boss trong smoke dùng bước gây damage từ server để kiểm tra lifecycle. |

Build QA qua `Hellfire.EditorTools.PhaseOneIntegrationChecks.VerifyAndBuild`. Define riêng `HELLFIRE_PHASE1_SMOKE` chỉ gắn trong QA build. Chạy executable build lần lượt với `-hfPhase1Host` và `-hfPhase1Client` ở hai process. Thêm `-hfPhase1GameOver` cho cả hai ở lượt revive/GameOver. Trước lượt mới, dọn marker `/private/tmp/hf_phase1/loopback-host-ready` do lượt QA trước sinh.
