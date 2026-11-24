# 外部资源来源（第二轮贴图，第三轮模型和古画，2026-11-19；第四轮贴图和模型，2026-11-20；第五轮院子的枝叶和石头，2026-11-21；第六轮院墙外和夜空，2026-11-24）

这里的贴图全部是 **CC0（公有领域）**：可以商用、可以改、不用署名。记下来源是为了以后换分辨率、追溯和替换。
材质怎么接、颜色怎么校准见 `Assets/_Art/Editor/ExternalMats.cs`；处理脚本在 `Assets/_Art/Tools~/`（Unity 不导入这个文件夹）。

## Poly Haven（https://polyhaven.com ，CC0）

页面地址都是 `https://polyhaven.com/a/<id>`。大面（地面、墙）用 2K，其余 1K。

| id | 用在哪 | 分辨率 |
|---|---|---|
| tatami_mat | 榻榻米 MD_Tatami / MD_TatamiWorn / MD_Heri（一张图里上下两张席子，UV 按席子单独算） | 2K |
| clay_floor_001 | 土壁 M_Plaster_Tsuchikabe（和室、玄関、縁側）、围墙 M_Fence | 2K |
| painted_plaster_wall | 发黄的漆喰 M_Wall_Plaster（走廊、納戸、物置、浴室厕所）、熏黑的台所 M_Wall_Sooty | 2K |
| dark_wooden_planks | 木地板 MD_FloorWood | 2K |
| white_rough_plaster | 土蔵墙 M_Wall_KuraPlaster、外墙白漆喰 MD_PlasterWhite | 1K |
| japanese_cedar_planks | 天花 MD_Ceiling / MD_CeilingDark、軒裏 MD_Soffit、浅色木料、封门的木板 | 1K（做旧 + 转 90°） |
| weathered_planks | 土蔵地板 MD_KuraFloor、腰板 MD_Wainscot | 1K |
| wood_cabinet_worn_long | 柱、梁、门框、长押 | 1K（做旧） |
| wood_table_worn | 深色 / 中间色木料（代码生成的家具） | 1K（做旧） |
| bamboo_wall | 竹篱笆 MD_BambooDry | 1K |
| japanese_stone_wall | 外墙墙基、围墙底石 MX_StoneWall | 1K |
| brown_mud_leaves_01 | 庭院地面 | 1K |
| brown_mud_dry | 土间三和土 MD_Tataki | 1K |
| ground_grey | 砂利 MD_Gravel | 1K |
| pine_bark | 树皮 | 1K |
| rust_coarse_01 | 锈铁 | 1K |
| raw_plank_wall | 暂未使用（第三轮家具备用） | 1K |

## ambientCG（https://ambientcg.com ，CC0）

页面地址都是 `https://ambientcg.com/view?id=<id>`，下载的是 1K-JPG。

| id | 用在哪 |
|---|---|
| Tiles019 | 浴室、厕所瓷砖 MD_BathTile |
| Wicker010B | 藤编 |
| Leaking001 / Leaking003 / Leaking005 | 墙上的漏水痕贴花 MH_Leak*（Color + Opacity 合成一张 RGBA） |

## 处理过的地方

- **mask**：Poly Haven 的 ARM（AO / 粗糙度 / 金属度）和 ambientCG 的 Roughness（+AO）合成 URP 用的 `*_mask.png`：
  R = 金属度，G = AO，B = 0，A = 光滑度（1 − 粗糙度），最大 512 px。（prep_tex.py）
- **做旧**：杉木、柜子、桌子三张原图偏橙红，降饱和、调亮度（杉木 饱和 0.42 / 亮度 0.85，柜子 0.5 / 1.15，桌子 0.7 / 1.05），原图已删。（age_wood.py）
- **杉木转向**：做旧后的漫反射、法线、mask 转 90°，让木板沿 u 方向（法线向量一起转）。（make_textures.py）
- **平均色**：每张漫反射在线性空间的平均色写在 `ExternalMats.Load` 里，按“这个表面平均看起来是什么颜色”反推材质的 _BaseColor。（make_textures.py）

## 自己生成的贴花（`Assets/_Art/Textures/Decals`）

make_textures.py 生成：血字四句（ここから出して / 開けるな / みつけた / まだいる）、烟熏、香灰、拖痕、天花水渍、浮尘粒子。
血字用的字体是 **Noto Serif SC**（SIL Open Font License 1.1），只是把字渲染成图片，没有把字体文件放进项目。

## 脚本（`Assets/_Art/Tools~/`）

| 脚本 | 做什么 |
|---|---|
| dl_ph.py | 按 Poly Haven API 的文件清单下载 diff / nor_gl / arm，并校验 md5 |
| acg_links.py | 从 ambientCG API 的结果里列出 1K-JPG 的下载链接 |
| prep_tex.py | 复制贴图到项目里，合成 mask 和漏水痕贴花 |
| age_wood.py | 三张木纹做旧 |
| make_textures.py | 杉木转向、算平均色、生成贴花 |

都用 `python -I 脚本 参数…` 运行（需要 Pillow、numpy）；下载的文件放在单独的空文件夹里再交给 prep_tex.py。

---

# 第三轮（2026-11-19）：CC0 模型和古画

和第二轮一样，全部是 **CC0（公有领域）**。模型在 `Assets/_Art/Models/External/PolyHaven/<id>/`，模型的贴图在
`Assets/_Art/Textures/External/PolyHavenModels/<id>/`，古画在 `Assets/_Art/Textures/External/Met/`。
导入设置、材质（URP Lit / 镂空 / 玻璃）和颜色校准见 `Assets/_Art/Editor/ExternalModels.cs`。

## Poly Haven 模型（https://polyhaven.com ，CC0）

页面地址都是 `https://polyhaven.com/a/<id>`。下载的是 1K 的 FBX 和 1K 贴图（漫反射、OpenGL 法线、ARM，有镂空的另加 alpha）。

| id | 用在哪 |
|---|---|
| Lantern_01 | 玄関、土蔵墙上挂的提灯；台所、納戸、土蔵、物置的油罐旁边放一盏 |
| Television_01 | 居間的显像管电视 |
| antique_ceramic_vase_01 | 座敷、二楼和室床の間的青花瓶 |
| dead_quiver_branch_01 | 花瓶、花盆里的枯枝 |
| dustpan、wooden_broom、wooden_bucket_01、plunger | 便所前间的扫除用具 |
| lightbulb_01、pull_chain_light_socket | 便所前间、便所隔间、洗面所的拉线灯泡 |
| mousetrap | 台所、納戸、土蔵、物置、脱衣所的捕鼠器 |
| nettle_plant、weed_plant_02 | 花盆里的枯草；玄関、台所土间墙脚长出来的枯草（每次只取模型里的一株） |
| oil_tin | 台所、納戸、土蔵、物置的油罐（有一只倒着） |
| planter_pot_clay | 花盆；縁側、玄関地上倒着一个（洒出一摊土） |
| pot_enamel_01、wooden_bucket_02、wooden_cutting_board | 台所作業台上的搪瓷锅、木桶、砧板；台所地上还倒着一只木桶 |
| rubber_boots | 玄関、东縁側、脱衣所的胶靴 |
| rusted_spade_01 | 土蔵的铁锹 |
| watering_can_metal_01 | 縁側的喷壶 |
| wooden_bookshelf_worn | 子供部屋倒在地上的书架 |
| wooden_ladder_02 | 土蔵、物置、脱衣所的梯子 |
| wooden_stool_01、wooden_stool_02 | 台所、土蔵、物置倒着的凳子；洗面所翻倒的小凳 |
| baseball_01、brass_vase_01、dry_branches_medium_01 | 暂未使用（下一轮备用） |

## The Met 开放资源（https://www.metmuseum.org ，CC0）

页面地址都是 `https://www.metmuseum.org/art/collection/search/<id>`。下载前用馆方 API 确认 `isPublicDomain = true`，
下的是馆方原图（Open Access），长边缩到 2048 px。画面只取画心：每幅裁掉装裱和边框的范围写在 `HouseBlockoutGenerator.Props3.cs` 的 `MetContent` 里。

| id | 作品（馆方标题 / 作者 / 年代） | 用在哪 |
|---|---|---|
| 65670 | Cherry, Plum and Willow Trees（桜梅柳図屏風）/ 作者不详 / 17 世纪初 | 次の間的金屏风（取中间四扇） |
| 45373 | Immortals（仙人図屏風）/ Hasegawa Sakon 長谷川左近 / 1620–30 年代 | 寝室的屏风（人物在右边四扇） |
| 45072 | Landscape（山水図屏風）/ Sesson Shūkei 雪村周継 / 16 世纪 | 其余的素屏风（仏間等） |
| 845126 | Stamped Images of Fudō Myōō（不動明王の印仏）/ 作者不详 / 14–15 世纪 | 仏間的挂轴 |
| 816190 | Zen Saying by Dahui Zonggao on the Passage of Time（墨跡）/ Jakushitsu Genkō 寂室元光 / 14 世纪中 | 挂轴 |
| 44855 | Reeds and Geese（芦雁図）/ Tesshū Tokusai 鉄舟徳済 / 1343 | 挂轴 |
| 49070 | Mynah Bird on Plum Branch（梅に叭々鳥）/ Saien Hōsai / 18 世纪 | 挂轴 |
| 45739 | Jurōjin（寿老人）/ Kano Tanshin (Moromichi) / 19 世纪初 | 二楼和室的挂轴 |

## 处理过的地方（第三轮）

- **模型**：FBX 原样导入（不要相机、灯光、动画，不加碰撞体，挡路的另放 `Blocker_*`）。不用 FBX 自带的材质，按 `ExternalModels.cs` 的表建 URP 材质：
  1K 漫反射 + OpenGL 法线 + mask（和第二轮一样：R = 金属度，G = AO，B = 0，A = 光滑度，最大 512 px）；有 alpha 的（枯草、提灯铜框、灯泡）合成 RGBA 漫反射做镂空。（prep_models.py）
- **颜色**：模型原图偏新、偏亮，每个材质乘了一个 tint，压到老屋的暗旧调子（表在 `ExternalModels.cs`）。
- **古画**：长边缩到 2048 px；屏风按扇切成竖条，一扇贴一条。
- **暂存**：下载的原文件、Met 的藏品记录（JSON）在 `Assets/_Art/Import~/`（Unity 不导入，约 62 MB）。这一轮验收后可以删掉。

## 脚本（第三轮新增，`Assets/_Art/Tools~/`）

| 脚本 | 做什么 |
|---|---|
| dl_ph_models.py | 按 Poly Haven API 的文件清单下载模型（1K FBX + 1K 贴图），校验 md5，只从 dl.polyhaven.org 下 |
| dl_met.py | 从 Met 开放资源 API 下载原图：藏品必须 `isPublicDomain = true`，图片必须是这件藏品自己的 |
| prep_models.py | 把下载的模型贴图整理成项目用的（mask、带 alpha 的漫反射），古画缩到 2048 px |

---

# 第四轮（2026-11-20）：布、瓷、金属、纸的贴图，床桌椅模型，院子杂草

和前两轮一样，全部是 **CC0（公有领域）**。下载清单用户批准过（2026-11-20）。贴图由 `Tools~/prep_tex_r4.py` 合成（拼大块、做旧、mask），
在 `ExternalMats.cs` 的 `UpgradeDetailR4` / `Upgrade` 里接到材质上（材质上记 `HakoArtRev = r4`，只升级一次）。

## Poly Haven 贴图（https://polyhaven.com/a/<id> ，1K）

| 源 id | 合成后的贴图（`PolyHaven/`） | 用在哪 |
|---|---|---|
| rough_linen | linen_aged；ticking_aged（加蓝灰窄条纹、床垫上一大片黄渍） | 白布、蓝布、防尘布；洋床的条纹床垫 |
| crepe_satin | crepe_aged | 红色绉绸（振袖、红布） |
| velour_velvet | velour_aged | 紫色天鹅绒（拝み座布団、镜台的盖布）、地毯 |
| terry_cloth | terry_aged | 毛巾（白、蓝） |
| fabric_pattern_05（col_01） | check_aged | 格子被面、二楼和室的坐垫、叠起来的被子里的一条 |
| fabric_pattern_07（col_1 / col_03） | gingham_red_aged / gingham_green_aged | 子供部屋的红格子被子、晾的连衣裙；奥の間的客用被子 |
| reed_roof_04 | reed_roof_04 | 稻草、草席 |
| green_metal_rust | green_metal_rust | 洗衣机的绿漆铁皮 |
| rusty_metal_04 | rusty_metal_04 | 锈铁皮（囲炉裏的内衬） |

## ambientCG 贴图（https://ambientcg.com/view?id=<id> ，1K-JPG）

| 源 id | 合成后的贴图（`ambientCG/`） | 用在哪 |
|---|---|---|
| Porcelain001 | ceramic_aged（加开片、污垢） | 白 / 蓝 / 褐色瓷器、屋脊瓦、屋顶 |
| Smear004 + Fingerprints002 | mirror_old、black_smudged、plastic_smudged | 旧镜子；玻璃、黑漆；灯罩、红塑料、蜡烛 |
| PaintedMetal012 | enamel_chipped | 搪瓷（洗衣机的桶等） |
| Metal048B | gold_dirty | 金、金箔 |
| Metal017 | bronze_old | 铜（水龙头、铭牌、毛巾杆） |
| Paper003 | paper_white | 白纸、障子纸、纸灯罩 |
| Paper006 | paper_aged | 发黄的旧纸、写了字的纸 |

## Poly Haven 模型（第四轮新下的，1K FBX + 1K 贴图）

| id | 用在哪 |
|---|---|
| old_bed_frame | 寝室的两张单人铁床、子供部屋的铁床（按床宽非等比缩放） |
| painted_wooden_table | 台所的餐桌 |
| painted_wooden_chair_01 | 台所餐桌的两把椅子 |
| painted_wooden_chair_02 | 子供部屋学生桌的椅子 |
| SchoolDesk_01 | 子供部屋并排的两张学生桌 |
| side_table_01 | 寝室的床头小桌 |
| wicker_basket_01 | 脱衣所的洗衣筐 |

第三轮备用的 nettle_plant、weed_plant_02、dry_branches_medium_01 这一轮用在院子杂草（房子和土蔵的墙根、院墙内侧、竹篱笆脚下）。

## 处理过的地方（第四轮）

- **拼大块**：布的原图一块只有 0.27–0.37 m，水渍、霉点一眼就看出重复；按 n×n 拼成 0.8–1.9 m 一块（2048 px）再做旧。做旧（水渍、霉点、开片、黄斑）都用周期噪声，拼缝看不出来。
- **素色布**：原图去色、只留织纹，颜色在 Unity 里用 _BaseColor 上（白 / 蓝 / 红 / 紫 / 两种毛巾），一张图给几种布用；格子布保留原色。
- **mask**：和前两轮一样（R = 金属度，G = AO，B = 0，A = 光滑度，最大 512 px）。
- **暂存**：下载的原文件在 `Assets/_Art/Import~/r4/`（Unity 不导入）。验收后可以删掉。

## 脚本（第四轮新增，`Assets/_Art/Tools~/`）

| 脚本 | 做什么 |
|---|---|
| dl_acg.py | 按 ambientCG API 的清单下载 1K-JPG 压缩包，只解出贴图（校验来源、大小、文件名） |
| prep_tex_r4.py | 合成第四轮的布、瓷、金属、纸贴图（拼大块、做旧、mask），打印每张的平均色和一块的米数 |

# 第五轮（2026-11-21）：院子的松、红叶、灌木、石头

全部 CC0，下载清单经用户批准（5 项，共约 15.1 MB）。

## 枝叶贴图（`Foliage/`，Tools~/prep_tex_r5.py 合成）

| 合成后的贴图 | 来源 | 用在哪 |
|---|---|---|
| maple_sprig（三格红叶小枝 + 一格 4 × 4 的单片叶子） | ambientCG LeafSet028（https://ambientcg.com/view?id=LeafSet028 ，1K-JPG） | 4 棵红叶的树冠；树下的落叶 |
| shrub_sprig（三格常绿小枝 + 一格半枯的） | ambientCG LeafSet022（https://ambientcg.com/view?id=LeafSet022 ，1K-JPG） | 24 丛灌木 |
| pine_tuft（三格松针簇 + 一格发黄的） | Poly Haven pine_sapling_small（https://polyhaven.com/a/pine_sapling_small ）的 twig 贴图（diff / nor_gl / arm 1K JPG + alpha 1K PNG），只取松针和细枝的颜色 | 2 棵松的云片 |

- 每张 1024 × 1024、带透明通道，四格各 512 px，小枝从格子底边中点往上长；材质是透明裁切、双面（`Materials/External/Foliage/MX_*`）。
- pine_sapling_small 的 twig 贴图是模型的 UV 展开图（一条条松针、松果），不是现成的枝叶面片，所以松针簇是按它的松针颜色画出来的；模型本体（16 MB、约 40 万面）没有下载。
- 枫叶原图偏橙黄，合成时往红里调过；常绿叶压暗、少数叶子做成发黄 / 枯褐。

## Poly Haven 模型（1K FBX + 1K 贴图）

| id | 用在哪 | 处理 |
|---|---|---|
| rock_moss_set_01（6 块苔石） | 枯山水的石组 4 块、东侧庭园 3 块 | Blender 减面到原来的 40%（每块 2000–6600 面） |
| rock_moss_set_02（7 块较扁的苔石） | 池边一圈 30 块、蹲踞的手烛石和汤桶石 | Blender 减面到原来的 20%（每块约 1600 面） |

## 看过但没用的

- Poly Haven shrub_01–04：都是稀疏的地被植物，种在院子里不像灌木。
- ambientCG PineNeedles001：落在地上的枯黄松针，不是活的松叶。
- Poly Haven spinning_wheel_01：维多利亚式的欧式纺车；用户定纺车按日本的糸車程序化重做。

## 暂存和脚本（第五轮新增，`Assets/_Art/Tools~/`）

下载的原文件在 `Assets/_Art/Import~/r5/`（Unity 不导入）。验收后可以删掉。

| 脚本 | 做什么 |
|---|---|
| dl_ph_maps.py | 只下载 Poly Haven 模型某一组材质的 1K 贴图（不下 FBX），校验 md5 |
| prep_tex_r5.py | 把叶片、松针颜色合成三张枝叶面片贴图，打印覆盖率和平均色 |
| decimate_fbx.py | Blender 无界面给 FBX 减面（保留物体名、材质、UV） |
| make_props_r5.py | 画两张贴花（不是下载的）：柱时计的钟面 `Decals/T_ClockDial.png`、碁盤的线 `Decals/T_GobanGrid.png` |

---

# 第六轮（2026-11-24）：院墙外的风景、夜空

## Poly Haven 贴图（https://polyhaven.com/a/<id> ，1K，CC0）

| id | 用在哪 | 材质 |
|---|---|---|
| asphalt_02 | 大门外的旧柏油村道 | MD_Asphalt |
| withered_grass | 路肩、荒掉的梯田田面；梯田的土坎（纹理放大 1.5 倍、压暗） | MD_FieldWithered、MD_FieldBank |
| rusty_corrugated_iron | 东边铁皮仓房的墙、屋顶和披屋 | MD_TinRoofRust |
| concrete_moss | U 形水泥沟和沟盖、北边挡土墙的压顶、仓房的水泥地基 | MD_ConcreteMoss |

电线杆（MD_ConcretePole）没有新贴图，用的是已有的 white_rough_plaster 压暗。

## Poly Haven HDRI（CC0）

| id | 用在哪 | 处理 |
|---|---|---|
| kloppenheim_07_puresky（https://polyhaven.com/a/kloppenheim_07_puresky ，4K .hdr，19 MB） | 夜空：天空盒材质 `Materials/Sky/MX_NightSky.mat`，shader `Shaders/HakoNightSky.shader`（`Editor/SkyBuilder.cs` 设置） | 见下 |

- 整张图转到原图里的月亮和场景的 Moonlight 同一个方位；原图的月亮比 Moonlight 高约 10°，shader 里用旁边的云盖掉，再在 Moonlight 的方向画一个月亮和一圈光晕。
- 压暗（曝光 0.16）、去饱和 80%，上面加一层慢慢飘的薄云。
- 下半部分画了三层远山和一道树梢线（程序化，不是贴图）。开雾的时候（探索者）远山是比雾色略亮的剪影，关雾的时候（箱女视角）按环境光打亮。
- 比较过 kloppenheim_02_puresky（2K），没用；它和两张 2K 的试看图都留在 `Assets/_Art/Import~/r6/hdri/`。

## 处理过的地方（第六轮）

- mask 的合成方式和第二轮一样（R 金属度、G AO、B 0、A 光滑度，最大 512 px）；漫反射、法线原样复制。
- HDRI 原样复制，导入设置在 `ExternalMats`（Kind.Sky：线性、最大 4096、U 重复 V 夹紧、高质量压缩）。

## 暂存和脚本（第六轮新增，`Assets/_Art/Tools~/`）

下载的原文件在 `Assets/_Art/Import~/r6/`（Unity 不导入）。验收后可以删掉。

| 脚本 | 做什么 |
|---|---|
| dl_ph.py（第二轮的） | 下载四组贴图（diff / nor_gl / arm 1K），校验 md5 |
| dl_ph_hdri.py | 按 Poly Haven API 的文件清单下载 HDRI（.hdr，指定分辨率），校验 md5 |
| prep_tex_r6.py | 复制漫反射、法线、HDRI，合成 mask，打印漫反射的线性平均色（写进 `ExternalMats`） |
| hdri_moon_stats.py | 量 HDRI 里月亮周围的亮度和地平线附近的亮度，用来定月亮的位置和盖掉它的范围 |

## 第六轮 b（同一天，看了第一批截图后改的）

第一批图里杉林是一根根透光的细杆（借的黑松针面片只有 18% 是实的）、竹子没有叶、U 形沟发黄。这次没有新下载，三张面片是脚本画的，颜色都取自已经下载的 CC0 贴图：

| 文件（`Foliage/<名字>/<名字>_diff_a.png`，一张四格） | 画的是什么 | 颜色取自 | 材质 |
|---|---|---|---|
| cedar_spray | 杉的枝叶：一根主枝两边排满往前伸的绳状小枝（第四格是发红褐的枯枝） | 第五轮的 pine_sapling_small（Poly Haven，CC0） | MX_SugiSpray |
| bamboo_spray | 竹叶：一根细枝上一扇扇的长披针形叶 | LeafSet022（ambientCG，CC0） | MX_BambooSpray |
| susuki_tuft | 芒草（薄）：一丛往外弯的枯叶加几根穗子（第四格是矮的枯草） | withered_grass（Poly Haven，CC0） | MX_SusukiTuft |

- `PolyHaven/concrete_moss/concrete_moss_grey_diff_1k.jpg`：concrete_moss 漫反射的去饱和副本（只留 22% 的饱和度）。原图偏黄，材质的调色上限（每个通道最多乘 1.8）拉不回灰，U 形沟和沟盖看起来是黄的；MD_ConcreteMoss 改用这张。
- 脚本：`Tools~/prep_tex_r6b.py`（在 `Assets/_Art` 下运行：`python -I Tools~/prep_tex_r6b.py Import~ Textures/External`），每张面片打印“实的”像素占比（杉 0.42–0.44、竹 0.29–0.36、芒草 0.12–0.28）。
