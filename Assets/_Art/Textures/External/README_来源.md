# 外部资源来源（第二轮贴图，第三轮模型和古画，2026-11-19）

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
