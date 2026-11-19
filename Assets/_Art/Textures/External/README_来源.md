# 外部贴图来源（第二轮，2026-11-19）

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
