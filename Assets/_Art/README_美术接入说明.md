# _Art 目录说明（美术灰盒 A1 + A2）

## 环境要求
- Unity 6 LTS（2022.3 LTS 应该也能用），**Universal Render Pipeline** 模板
- 依赖包：URP（模板自带）

## 使用步骤
1. 把整个 `Assets/_Art` 文件夹复制到 Unity 项目的 `Assets/` 下，等待编译。
2. 菜单 **Hako → Art → 2 生成老宅灰盒场景**。
   - 会自动先生成容器预制体和材质，再生成场景 `Assets/_Art/Scenes/House_Blockout.unity`。
3. 菜单 **Window → Rendering → Lighting → Generate Lighting** 烘焙光照（可选，不烘焙也能看）。
4. 点 Play，或在 Scene 视图里走动查看。

重复运行第 2 步会重建场景，但**不会覆盖**已存在的材质、预制体和后处理配置。例外：
- 还是灰盒的容器预制体，如果 `ContainerPrefabBuilder.Sizes` 里的尺寸改过，会自动按新尺寸重建（手动换过模型的不动）；
- 常态后处理 `VP_Normal` 里没有 Bloom 时会补一个（强度 0.25、亮度上限 4），压住月光在镜面 / 玻璃上的高光被泛光放大成大光斑的问题。

要无条件重建全部容器预制体，用“1b 强制重建”。

家具尺寸：所有家具（容器、代码家具、手摆陈设、按房间补的家具）统一按真实尺寸 × 1.25（`FurnitureScale`），和放大后的建筑一致；靠墙摆的家具背面离墙面 4 cm。

房间中部：按房间用途摆家具组（座卓 + 坐垫 + 火钵、卓袱台、仏間面向仏壇的坐垫和经机、座敷下了一半的围棋、居間摊开的针线活、衣桁、铺好的被褥、儿童房的小桌和玩具等），离墙至少 0.7 m，和别的家具之间留 0.65 m 走道，不压门口、交互站位；暗门地毯在房间正中的（居間、座敷、納戸、土蔵），家具组摆在地毯旁边，地毯外留 25 cm。放不下的跳过，生成日志里写明原因。桌子、火钵、棋盘带碰撞，坐垫、被褥、针线活可以踩过去。

靠墙的家具放大后如果和邻居、门口挤在一起，先只沿墙滑开，不离开墙面；墙角里的家具只离开其中一面墙。原来那面墙大半是门洞、放大后塞不进门洞两侧墙垛的，在本房间其他墙上找空位，整组转过去背面贴墙（容器、代码家具先放，先找不用挤开别的东西的空位，实在没有才挤开落地的纯陈设，被挤开的陈设再另找地方，找不到就撤掉）。四面墙都放不下的留在原处，生成日志里写明。背后挂着薄件（锅铲、管子、烟道）把家具顶离墙面的，薄件可以嵌进墙面 7 cm 以内，让家具本体贴墙。门口两侧的留空区只管会挡路的家具，坐垫、脚垫这类能踩过去的可以压到。

## 生成内容

| 内容 | 位置 |
|---|---|
| 场景 | `Scenes/House_Blockout.unity` |
| 16 个容器预制体（8 种 × 2 变体） | `Prefabs/Containers/` |
| 秘密出口暗门预制体 | `Prefabs/SecretExit_Hatch.prefab` |
| 灰盒材质 | `Materials/Placeholder/` |
| 后处理配置（常态 / 猎杀） | `Settings/VP_Normal.asset`、`VP_Hunt.asset` |

场景规模（V2，京間模数）：两层主屋（一层 25.5 × 16.4 m，外加东侧縁側；二层 16.4 × 10.9 m）+ 土蔵（7.3 × 5.5 m）+ 围墙庭院，共 **45 个容器**、**4 个秘密出口候选点**、**2 座楼梯**（坡度约 36°）。

- 所有房间边界落在半间（0.91 m）网格上。
- 当前灰盒（2026-11-18）：门楣（鴨居）离地 **2.2 m**，门宽 = V3 宽度 ×1.25（和家具放大比例一致）；玄関正门、台所后门高 2.5 m。**玩家胶囊体高度必须小于门高**（目前 GameConfig 是 1.65，没问题）。
- 只有玄関正门和台所后门通到室外；縁側朝庭院的开口是锁着的玻璃拉门（有碰撞，按 E 只会晃，见下面“门”）。竹篱笆把室外分成门前、后院、主庭园三块（主庭园只能看）。
- 房间开口（2026-11-19 用户批准）：每个房间 1~2 个门，玄関、台所这种土间入口算例外。封掉了 11 处门洞，其中 3 处做成钉死的旧门（只是装饰，后面是实墙）。
- 縁側从南侧绕到东侧，和中廊下连成追逐环路。

### V2 关键坐标（给程序改测试用）
| 位置 | 坐标 (x, y, z) |
|---|---|
| 玄関 ↔ 居間 门中心 | (5.46, 0, 5.46)，宽 1.8 |
| 廊下 ↔ 風呂 门中心 | (9.10, 0, 10.92)，宽 0.9 |
| 台所 ↔ 風呂 门中心 | (7.28, 0, 13.65)，宽 0.8 |
| 二层 廊下 ↔ 物置 门中心 | (2.73, 2.9, 10.465)，宽 0.9 |
| 二层 廊下 ↔ 子供部屋 门中心 | (5.005, 2.9, 12.285)，宽 0.9 |
| 裏階段（台所） | x = 0.91，从 z = 16.28（y 0）爬到 z = 12.285（y 2.9） |
| 主階段 | x = 15.47，从 z = 16.28（y 0）爬到 z = 12.285（y 2.9） |

## 第二轮：贴图、灯光、雾、污渍（2026-11-19）

- **贴图**：墙、地板、天花、木料、竹、石、锈铁换成 CC0 贴图（Poly Haven / ambientCG），来源和处理见 `Textures/External/README_来源.md`。材质只升级一次（材质上记 `HakoArtRev = r2` 标签），之后在 Unity 里手调的参数不会被覆盖；想重来就删掉材质再生成。土蔵墙 `M_Wall_KuraPlaster` 是 `r2b`（贴图放大到 1.8 m 一块，1.2 m 时远看斑块重复太明显）。
- **墙面按房间分**：土壁（和室、玄関、縁側）、发黄的漆喰（走廊、楼梯、納戸、物置、浴室厕所）、熏黑的台所、土蔵的白漆喰。每个房间的墙在自己那一侧，所以隔墙两面可以不同。
- **立方体 UV**：用 Unity 自带立方体搭的墙、楼板、门框、代码家具，生成时换成同形、UV 按米铺的网格（名字、碰撞体、缩放都不变），网格存在 `Generated/HouseDetail_Meshes_NN.asset`（2026-11-21 起按生成顺序分成几个文件，每个 45 MB 以内——GitHub 拒收单个超过 100 MB 的文件，第四轮单个文件到了 225 MB；顶点也压紧了：法线 16 位、切线 8 位，位置和 UV 保持 32 位）。座敷床の間（ZashikiArt 运行时换模型）和容器预制体不动。
- **榻榻米**：每张席子单独取贴图里的一张（随机翻转），约三成是发黑的旧席。
- **污渍**（`Dressing/Grime`）：漏水痕、天花水渍、台所かまど上方和囲炉裏上方的烟熏、仏壇前的香灰、二楼中廊下一串往下滑的血手印 + 地上拖痕、納戸 / 子供部屋的抓痕和小手印、厕所一个小手印。位置用射线确认没被家具挡住。
- **血字**（`Dressing/Grime/Writing_*`）：只是气氛，不是线索。仏間「開けるな」（北墙，挂注连绳那扇钉死的旧门旁）、納戸「ここから出して」（西墙；两扇旧门旁边都靠着收纳箱，退到正对门口的墙）、子供部屋「みつけた」（北墙）、土蔵「まだいる」（东墙，正对门口）。规则：整句在一片墙上、不跨柱子；不写在收纳箱上方（字左右 0.4 m、墙前 1.2 m 以内没有箱子），免得像在提示箱子里有东西；南墙、东墙上的字自动翻转，不会反。
- **浮尘**（`Lighting/Dust`）：每个房间一个粒子系统，只有手电照到才看得见。
- **灯光**：月光 0.22、环境光 (0.011, 0.013, 0.02)、**指数平方雾**（密度 0.17：2.5 m 内基本清楚、5 m 一半、7.5 m 外看不清；用户定的，美术圣经已改），常态后处理更暗更冷（曝光 0.1、对比 14、饱和 −28、色温 −12、暗角 0.4、颗粒 Medium3 0.38、阴影偏蓝灰）。`VP_Normal` 只升级一次（以“还没有 Shadows Midtones Highlights”为准）。
- **截图**：`Hako → Art → 9 场景截图检查` 多了 `moodx_*`（游戏里的光：场景环境光 + 雾 + 强度 25 的手电，看暗度）和 `decal_*` / `decalmood_*`（血字特写）。

> **给程序（2026-11-19 用户已定，请 Codex 改）**：
> 1. **手电强度 5 → 25**（`NetworkPlayer.Inspection.cs`，角度 55°、射程 16 m 不变）。用户看了 5 / 12 / 25 的对比后选的 25：房间中间看得清，远处被雾吃掉；凑近墙和箱子时画面会偏白，这一点用户知道。
> 2. **`LocalSession.cs` 的灰色环境光改成调试开关，默认关**。默认用场景自己的暗环境光，环境探针照 `RenderSettings.ambientLight` 生成（`SphericalHarmonicsL2.AddAmbientLight(RenderSettings.ambientLight)`，和截图工具一致）；打开开关时才临时补成灰色，方便测试走位。

## 第三轮：家具、陈设、废弃感（2026-11-19）

- **外部模型和古画**：28 个 Poly Haven CC0 模型、8 幅 Met CC0 古画（屏风、挂轴），来源和每个用在哪见 `Textures/External/README_来源.md`。模型用 `Kit.Model(id, …)` 摆，和代码家具一样跟着第二遍搬家、统一放大 1.25；导入和材质在 `Editor/ExternalModels.cs`。
- **便所、風呂分小间**（`HouseBlockoutGenerator.WetRooms.cs`）：便所 = 前间（洗手器、扫除用具、架子）+ 里间（朝颜小便器、东北角隔间里的和式蹲便）；風呂 = 浴室 + 洗面所（昭和瓷砖洗脸台、蒙灰的镜子）+ 脱衣所兼洗衣处。新隔墙的门洞不装门、挂暖帘，净宽约 1 m。便所隔间的门是能用 E 开关的铰链门（2026-11-19 用户定，见下面“门”一节）。
- **容器预制体**：换成带贴图的网格（每个预制体一个 `*_Meshes.asset`）；新加的五金件名字都以 `Fit_` 开头，不算进摆放用的包围盒，所以容器位置没有变（每次生成后逐个比对 45 个容器：0 变动）。葛籠的家纹是丸に梅鉢，押入れ的襖上有一个破洞。程序用的名字（`Visual`、`Drawer_*`、`Handle`、`ItemAnchor`、`Lid_Pivot`、`Door_*_Pivot`、`GrabAnchor`、`InteractPoint`、`Collision`…）都没动。
- **房间里的大件**：次の間的金屏风（Met 古画）、寝室的屏风和衣桁（搭着一件和服）、居間的显像管电视、各和室的挂轴（Met 古画）、子供部屋倒下的书架等。
- **废弃感（用户定的“重”）**：`HouseBlockoutGenerator.Abandon.cs`，并进每个房间的 `Furn_Fill_<房间>`。
  - 地上：墙脚掉下来的墙皮和土块、碎纸和旧报纸、摔碎的瓷器、门口和縁側吹进来的枯叶红叶、土蔵 / 台所 / 物置的稻草、便所和風呂的瓷砖碎块、墙角的灰团、半透明的水渍霉斑（土间、浴室厕所不画）；玄関和台所的土间墙脚长出枯草；縁側、玄関倒着一个花盆，台所倒着一只木桶；仏間地上散着线香，子供部屋地上是蜡笔画。
  - 天花四角的蜘蛛网（半透明的网面 + 丝 + 挂着灰团的垂丝）；碰到门头、窗、家具就缩小，缩到很小还碰就不挂。
  - 和室（次の間、仏間、座敷、寝室、二楼和室）各有一扇断了的障子靠在墙上。
  - 物置的天花塌了一块：天花上一个洞、垂下来的天花板和一根斜挂的断梁，地上一个土堆、木板和稻草。
  - 走廊地上一张遗影（玻璃碎了）。
  - 规则：都贴着地面的实际表面（榻榻米面比地板高 6.5 mm）、很薄、没有碰撞体（塌顶的土堆和破障子除外）；不放在门口、门扇停靠处、容器正面 1 m、暗门地毯、交互站位和已有家具的脚下；没有箱子、袋子、包袱，都不能看起来像能搜的东西。想多想少：每个房间的数量改 `RuinFor`，总倍数改 `RuinMore`（现在是 2）。
- **防尘布**（`HouseBlockoutGenerator.DustSheet.cs`，用户定：“不用管，玩家发现没有 E 就不会搜了”）：候选是电视、脚踏缝纫机、人台、长桌、长凳（按房间和位置定盖不盖，大约一半到八成，重新生成结果不变），奥の間和二楼和室的座卓一定盖。现在这一版盖了 7 块：玄関的长凳、納戸的缝纫机、土蔵的人台、二楼两条走廊的长桌、奥の間和二楼和室的座卓（居間的电视这次没盖到）。布从顶面边上包过去往下垂，四角多出来的布斜着撑开、垂得低一点，碰到地面堆在地上；顶面落了灰，颜色暗一些（人台除外）；人台的布被脖子上的木帽顶起来、搭过两个肩膀往下垂，像个站着的人。网格线正好落在顶面的边上（原来是均匀的格子，跨边的那一格会切进家具，桌角、桌边从布里戳出来）。两面都画，没有碰撞，家具自己的挡块不变。**容器一律不盖。**盖不盖、盖成什么样改 `SheetFor`（座卓在 `CZataku(…, sheet: true)`）。
- **新的挡块**（`Props/Dressing_Blockers`）：`Blocker_Monooki_CeilingCollapse`（塌顶的土堆，1.5 m 高）、`Blocker_<房间>_ShojiScrap`（破障子，1.1 m 高，上面 5 个和室各一个）。都离步行路线 0.45 m 以上，不碰门口和容器正面。原来的 `Blocker_Benjo_StallDoor` 删了（隔间门扇现在自己有碰撞）。
- **截图**：`rev_*` 是第三轮的审图机位；其中 `rev_stain_*`、`rev_shoji_*`、`rev_web_*`、`rev_sheet_*` 是生成时按水渍、破障子、蜘蛛网、防尘布的实际位置现找的；`rev_stall_open` / `rev_stall_closed` 是便所隔间门开着、关上。
- **下一轮**（用户定）：庭院杂草（沿房子地基、竹篱笆脚的枯草和爬藤），用已经下好的 Poly Haven 模型，不用再下载（第四轮已做）。地上碎屑的量用户看过，保持现在的总倍数 2。

## 第四轮：纹理、真实家具、软布、院子杂草（2026-11-20）

- **起因**（用户截图：洗衣机、毛巾和镜子、一摞被褥、洋床）：程序拼出来的简单几何体没有纹理，“床根本不像现实的床”；要有纹理、有褶皱，游戏里所有类似的东西都要改。
- **贴图**：剩下的纯色材质全部接上 CC0 贴图——布（素色旧布 + 少量格子，用户定）、毛巾、床垫条纹布、瓷（开片）、搪瓷（掉瓷）、金 / 铜、旧镜子、玻璃 / 黑漆 / 塑料（指纹擦痕）、纸、草；场景调色板里的障子纸、天花板、土间石、地毯、木地板、黑漆、浴室砖、屋顶也换了。材质只升级一次（`HakoArtRev = r4`）。来源见 `Textures/External/README_来源.md` 第四轮。
- **软网格**（`HouseBlockoutGenerator.Soft.cs`）：塞了棉花的东西（座布団、床垫、枕头、叠好的被子：四边圆鼓、缝线一圈小褶、可以压出头印、中间塌）、垂布（竖褶、下摆张开、布边往后卷）、搭在杆上的毛巾（前后两片，快滑下来的那条下摆是斜的）、布卷、铺开的被褥（被子从两边垂到地上）、揉皱的一堆脏衣服。褶的深浅统一乘 `FoldBoost`（1.7）。
- **场景家具换样子**（`HouseBlockoutGenerator.Refit.cs`，搬家、污渍都算完以后，只换子物体；根节点位置、朝向、缩放、BoxCollider、Firebox 都不动，桌面高度不变）：
  - 寝室两张旧铁床（old_bed_frame，用户定“旧铁床架”）：一张铺着格子被子、被子掀开一角；一张光床垫、枕头歪着、脚那头一条叠起来的毛毯。子供部屋的床铺红格子被子。床垫是条纹布，中间塌，有黄渍。
  - CC0 模型：台所的餐桌和两把椅子、子供部屋并排两张学生桌和椅子、寝室的床头小桌。
  - 重做成倒角木件 + 车削器物：次の間的茶桌（急須、茶杯）、居間的囲炉裏卓（灰堆、炭、五徳、铁壶、火箸）、二楼的文机（写了一半的信、砚台、笔）、玄関的长凳（蓝布垫）、台所的灶台和水槽台（土かまど + 羽釜、水磨石水槽 + 铜龙头、砧板和菜刀）、浴室的桧木浴桶（铁箍、一半盖着风呂蓋、一汪黑水）和木凳。
  - 居間、仏間、二楼和室的坐垫换成软坐垫；奥の間的客用被褥换成软的一套。
- **陈设换样子**：坐垫一摞、叠起来的被子（加两个荞麦壳枕头）、暖帘、毛巾、抹布、镜台的盖布、衣桁上的振袖、脱衣所的浴衣和毛巾、納戸和物置架子上的布卷和包袱布、地毯卷、草席、晾衣竿上的衣服、洗衣筐（wicker_basket_01 + 一堆揉皱的旧衣服）、二楼和室铺开的被褥都换成软网格；洗衣机重做（绿漆电机座、掉瓷的搪瓷桶、绞干滚筒、摇柄、排水管）；洗面所的洗脸盆改成台面上开洞、车削出来的凹盆；火鉢改成鼓肚卷口的褐釉陶火鉢（灰堆、五徳、铁壶、火箸；先做的蓝釉在手电下像塑料桶，审图后换了）。
- **容器没动**：第一遍量尺寸用的还是原来的方块（`Kit.Swap`），搬家结果不变；每次生成后比对 45 个容器：0 变动。
- **院子杂草**（`HouseBlockoutGenerator.Weeds.cs`，第三轮定的“下一轮”）：房子和土蔵的墙根、院墙内侧、竹篱笆脚下一丛丛枯荨麻 + 贴地的杂草，院墙下偶尔一堆枯枝；门、玻璃拉门前后 0.7 m 不长，躲开树、石灯笼、水井；没有碰撞体。随机数单独一条，院子其余东西的位置不变。
- **截图**：`rev_r4_*` 是第四轮的审图机位（生成时按家具、陈设的实际位置现找的；院子里的用白天的光再加检查光，机位不出院墙、视线不穿过房子）。
- **还是方块 / 圆柱拼的**：院子的松、红叶、灌木、石头、石灯笼、水井、飞石在第五轮换掉了（见下）。室内的雏人形、铠甲、人台、缝纫机、纺车、木马、挂钟、手压水泵、朝颜小便器和蹲便、儿童玩具、围棋盘、小陶瓷也在第五轮换掉了。架子、木箱、长凳、工具架这类本来就是板子拼的，有了贴图先不动。

## 第五轮：院子的树和石头，室内剩下的方块物件（2026-11-21 ～ 22）

- **起因**：第四轮剩下的“球体 + 圆柱”里，院子的树和灌木最显眼。下载清单（5 项 CC0，约 15.1 MB）用户批准；来源见 `Textures/External/README_来源.md` 第五轮。
- **用户定的**：荒废程度 = “长乱但看得出形”（还看得出原来修剪的云片、圆丛，枝叶乱冒、有枯黄的空洞）；纺车不用欧式的 CC0 模型，按日本的糸車程序化重做（室内那批，还没做）。
- **树和灌木**（`HouseBlockoutGenerator.Flora.cs`）：枝干是弯曲、逐渐变细的圆管（贴 pine_bark）；枝叶是透明裁切的“小枝”面片（`Textures/External/Foliage/`，一张四格）。
  - 松 2 棵：弯曲的主干、六层横枝，枝头一片片扁圆的“云”（松针簇中间朝上、边上朝外），云片上冒徒长枝、底下垂着枯针。
  - 红叶 4 棵：主干分四枝再分叉，小枝一层层平着铺开；树下 70–100 片落叶。
  - 灌木 24 丛：两三个圆丘挤在一起，约四成的圆丘有一面是枯的（小枝大半没了、剩下的是枯叶），几根长枝窜出来。
  - 枯树：同一套分叉的枝干，不挂叶子。
  - 面片的法线用“从树冠 / 灌木中心往外”的方向，不用面片自己的面法线（URP Lit 双面渲染时背面也按正面法线打光，按面法线从背后看是黑的）。
- **石头**：枯山水、东侧庭园、池边一圈、蹲踞旁的石头换成 Poly Haven 的苔石（`RockModel`，底下埋进土里一截）。
- **石件**（`HouseBlockoutGenerator.GardenStone.cs`，没有合适的 CC0，程序化重做）：春日灯笼（六角基础、带节的圆竿、中台、开窗的火袋、翘角的笠、宝珠）、雪见灯笼（三条弯腿、大圆笠）、蹲踞的手水钵（车削的石钵）、水井（厚口沿的井圈、半边木盖、滑轮和绳子、CC0 木桶）、飞石（轮廓不规则、边上带斜面）。
- **不变的**：挡路的 `Blocker_*`（松、红叶、枯树、石头、石灯笼、水井、蹲踞）位置和大小都没动；原来用球体拼的那些还照旧“画”进一个不生成的批次，只为了随机数序列和以前一样，院子里别的东西位置不变。容器 45 个：0 变动。
- **网格文件**：枝叶面片让生成的网格多了约 16 MB，加上室内的车削件，`HouseDetail_Meshes_NN.asset` 现在是 6 个（每个按估算 42 MB 封顶，实际最大约 44 MB）。
- **截图**：`rev_r5_*`（白天的光 + 检查光）；离得近的另有一张 `revmood_r5_*`（夜 + 手电，雾里 7 m 以外看不见，所以远景的树没有这张）。
- **室内**（`HouseBlockoutGenerator.Props5.cs` / `Props5b.cs` / `Props5c.cs`，都没有合适的 CC0，用车削、弯管、倒角木件和已有贴图程序化重做）：
  - 糸車（用户定：日本的，不是欧式纺车）：长木台、两扇细竹辐条的大轮（辐条梢用绳之字形连成轮辋）、摇把、铁锭子和一团棉线、调绳。
  - 木马：弧形摇板、外撇的腿、圆桶身、弯脖子长脸，黑鬃黑尾、红鞍垫、头上一根握把。
  - 八角柱时计：八角钟头、发黄的罗马数字钟面（贴花）、黄铜压圈、摆箱里一根停住的钟摆。廊下那只挂钟也换成它。
  - 手压泵：绿漆铸铁泵身（喇叭口、弯出水嘴、长压柄）、石流し、CC0 木桶和柄杓。
  - 铠甲：胴和五道蓝威、六片草摺、大袖、籠手、脛当；兜是锈铁筋兜鉢 + 三段錣 + 金鍬形，黑漆面頬后面是空的。土蔵架子上的兜同款。没有鎧櫃（不放看起来能打开的箱子）。
  - 雏人形：五段红毯台、六曲金屏风、内裏雛、官女、囃子、随身、雪洞、橘和樱、菱饼；一只囃子倒在台上、一只官女滚到下一层。納戸玻璃柜里和土蔵架子上的人形换成市松人形。
  - 人台：包麻布的躯干、木领帽、车削木柱、三条弯腿；没盖布的那只肩上搭红布、挂皮尺、别珠针。
  - 脚踏缝纫机：铸铁侧架、斜拉撑、栅格踏板、飞轮和连杆、皮带、黑色机头。
  - 便所：朝顔小便器（厚口沿的瓷喇叭口）、和式蹲便（长圆瓷盆 + 金隠し + 瓷水箱 + 拉链）。
  - 碁盤：梔子脚、倒角厚盘、漆线（贴花）、扁圆棋子、木碁笥。
  - 玩具：只点了一只眼睛的不倒翁、小芥子、木陀螺、手鞠、折的纸鹤、倒角积木。
  - 瓷器：`Kit.C` 画瓷质圆台（杯、碗、坛、盘、盖，全屋近 90 处）时，第二遍以后按上下半径和高度车削成器形（`Vessel`）；茶盘上是急須和茶杯；土蔵的坛子蒙纸扎绳（封着的）。
- **室内这批不影响布局**：糸車、木马、柱时计、泵、铠甲、雏人形、人台、缝纫机、便器、碁盤都是第三遍（按房间补家具）或固定设备里画的，占地不超过原来声明的；画的过程中取随机数的次数和顺序不变。廊下挂钟、玻璃柜人形、土蔵的兜是第一遍就量尺寸的，用 `Kit.Swap` 换。容器 45 个：0 变动。
- **这一版布局里没摆出来的件**：糸車、木马、手压泵沿墙放不下（和以前一样），场景里暂时没有。审图时临时摆在院墙外拍了 `rev_r5_gal_*`（`BuildReviewGallery`，拍完删掉、不存进场景）。
- **贴花**：钟面 `T_ClockDial.png`、碁盤线 `T_GobanGrid.png` 是 `Tools~/make_props_r5.py` 画的（不是下载的）。
- **截图**：室内的是 `rev_r5i_*`。

## 房间图、3D 地图、房间机位、楼梯（2026-11-22）

- **起因**：箱女转移改成“同房间 + 相邻房间”（`Docs/HakoOnna_DevPlan_V1.3_Iterations.md` 模块 T），需要房间的相邻关系、每个房间的俯视机位和一张 3D 地图。用户定：和模型有关的都由美术出。另外用户要求楼梯换纹理、两侧不要有缝。
- **HouseGraph**（`Scripts/HouseGraph.cs`，生成在 `Gameplay/HouseGraph`）：24 个房间（16 个有容器）、45 个容器各自在哪个房间、26 条相邻关系。
  - 同一层的相邻是按房间范围算的：共用一面墙（重叠 ≥ 0.5 m），或隔着一段没有容器的走廊正对着。楼梯两条（台所↔物置、奥の間↔子供部屋）和后院一条（風呂↔土蔵）是手写的（`SpecialLinks`）。
  - 算出来的 26 条和程序 `HiderRoutes` 里手写的 26 条完全一致（生成日志里 `[Hako Art] HouseGraph：` 那一行会列出来，以后改了户型可以对）。任意两个有容器的房间最多隔 5 次换房间。
  - 台所↔土蔵那条当天稍后用户定了要加，见下一节（现在是 27 条）。
- **RoomVantage**（`Gameplay/RoomVantages/Vantage_<房间>`，16 个）：每个有容器的房间一个靠天花板的机位，朝房间中心偏下。从四个角和四面墙正中这八个位置里挑：往每个容器和地面上 3×3 个点打射线，看得见的越多越好，镜头前 1.8 m 内有东西挡着（屏风、高柜子）要扣分；尽量正对着容器的正面，不落在到顶的柜子里；细长的房间（風呂、縁側）从短边正中顺着长边看。
- **HouseMapProxy**（`Scripts/HouseMapProxy.cs`，生成在 `Gameplay/HouseMapProxy`，默认关着，在真房子正下方 300 m）：每个房间一块地板（有容器的亮一点、走廊暗一点、楼梯间是木色并且有一级级升上去的踏步）、0.7 m 高的矮墙（门洞留空，窗照样是墙）、每个容器一个小方块、楼梯和后院的蓝色弧线。一楼 / 二楼 / 土蔵是三个子节点，可以错开摆、单独隐藏。
  - 材质 `Materials/Detail/MP_Map*.mat` 用自己的着色器 `Shaders/HakoMapUnlit.shader`（纯色，不受光、**不受雾**）。
- **楼梯**（`HouseBlockoutGenerator.Stairs.cs`）：
  - 以前每一级是一个方块，两边各留了半米多的空，旁边另有两条斜梁，所以看得到缝和黑洞。现在每一级是一块带圆口的踏板（`D.TrimMid`，木纹顺着踏板长边）+ 一块深色立板（`D.BeamDark`），**从这面墙铺到那面墙**（两头各插进墙里 5 mm）；两面墙上各有一条跟着坡度走的斜踢脚板和压条，踏板、立板的端头都收在踢脚板后面。东墙加了一根木扶手（铁托架）。
  - `Step_NN` 那些方块还在（缩小一圈藏在踏板和立板后面），因为程序的 `HouseFoley` 按它们的范围判断“踩在木头上”。
  - 行走用的斜坡碰撞体 `WalkRamp` 从 2.4 m 加宽到楼梯间的净宽 3.48 m：现在可以贴着墙上下楼，不会踩到斜坡外面掉进缝里。
- **容器检查脚本进仓库了**：`Tools~/containers_diff.py` + `Tools~/baseline/containers.json`（每次重新生成后跑，45 个容器必须 0 变动；确认要动容器以后用 `--save` 更新基准）。
- **截图**：`rev_r5s_*`（楼梯和楼梯贴墙处）、`rev_r5v_<房间>`（16 个机位看出去的样子）、`rev_r5_map_*`（3D 地图，二楼抬高 5 m、土蔵推开 5 m）。

## 检查后的调整：容器分布、台所↔土蔵、认路的微光、三件大件（2026-11-22）

用户看了检查清单后定的：“一个房间 2–4 个容器”，台所↔土蔵加上，其余按建议改。

- **容器（总数还是 45，8 种的数量不变）**：
  - 土蔵 6 → 4、納戸 5 → 4，挪给原来只有 1 个的三个房间：縁側多一个長持（东段靠南墙）、風呂多一个葛籠（脱衣所东墙）、便所多一个葛籠（前间西南角）。现在 16 个有容器的房间都是 2–4 个。
  - 二楼寝室、二階和室原来 3 个容器都排在南墙一条线上：寝室的箪笥挪到北墙（门东边），二階和室的長持挪到东墙南段。
  - **名字变了的**（名字是 `<房间>_<类型>_<房间内序号>`）：新增 `Benjo_Tsuzura_01`、`Furo_Tsuzura_02`、`Engawa_Nagamochi_02`；`Benjo_GetaBako_01` → `Benjo_GetaBako_02`；土蔵、納戸剩下的几个序号往前挪。`Engawa_Tsuzura_01`（程序开发模式的起点）没变。
  - 容器基准 `Tools~/baseline/containers.json` 已更新成这一版。
- **台所↔土蔵**：`HouseGraph` 现在是 **27 条**相邻（原来 26 条 + 这一条，类型 `Yard`）。土蔵不再是只连着風呂的死胡同。**程序的 `HiderRoutes` 手写表还是 26 条，要么加上这一条，要么直接改成读 `HouseGraph`。**
- **认路的微光**（`HouseBlockoutGenerator.Landmarks.cs`，`Lighting/Landmarks/Landmark_<房间>`）：玄関、台所、風呂、土蔵、寝室、二階和室这六间宽 10.9 m，比雾里看得见的距离（7.5 m）还宽。每间在房间中央离地 2.3 m 加一盏朝下的暖色聚光（强度 4、射程 3.4 m、115°、不投影），只照亮灯下一小圈地面，照不到墙、也漏不到楼下。雾、环境光、手电都没动。**2026-11-26 起改成投软阴影**（见下面“阴影”一节）。
- **沿墙放不下的三件大件**改成中央家具组：手压泵（台所）、糸車（納戸）、木马（子供部屋）。
- **给程序的两条小建议**（不是美术能改的）：探索者相机的远裁剪面现在是 1000 m，雾里 7.5 m 外就看不见，改成 40 m 左右能省渲染（箱女视角关了雾，要留长一点）——**第六轮起作废**：院外的树在 5–190 m 外，雾里是天空前面的剪影，远裁至少留 250 m（见下一节）；`Docs/HakoOnna_Map_V3_Spec.md` 的容器表已按这一版更新。

## 第六轮：院墙外的风景、夜空（2026-11-24）

- **用户定的**：设定 A“山坡上的废村”，推荐项全部照做：雾不变、不放探索者的车、路灯只是摆设（不亮）。设定见美术圣经 3.5。用户看了第二批截图后定稿（2026-11-24）。
- **只是视觉，玩法没动**：全在 `Dressing/Outskirts` 下（9 个网格），没有碰撞体、不投影、没有灯、没有脚本。大门、`EscapeZone_Gate`、`Ground_Garden`、`Gameplay` 和 HEAD 比对过：位置、碰撞体都一样；整个场景还是 7 盏灯、760 个碰撞体。容器 45 个：0 变动。EditMode 测试 88/88 通过。**程序不用接任何东西。**
- **代码**：
  - `HouseBlockoutGenerator.Outskirts.cs`：院外地面（北坡、东西两边、8 层梯田和尽头的陡坡）、U 形沟和沟盖、旧柏油村道和路肩、梯田的芒草和灌木、电线杆、电线、剪断的进户线。
  - `HouseBlockoutGenerator.Outskirts2.cs`：北坡杉林、谷边的树、东西两边的林子、西墙外竹林、柿子树、西边邻家空屋、东边铁皮仓房。摆放高度靠临时加在院外地面上的碰撞体往下打射线，摆完就删。
  - `SkyBuilder.cs` + `Shaders/HakoNightSky.shader` → `Materials/Sky/MX_NightSky.mat`：夜空（kloppenheim_07 的云、月亮、三层远山）。
  - `AtmosphereBuilder.cs`：`RenderSettings.skybox` 换成夜空；场景里的预览相机改成按天空盒清屏、远裁 1000 m（和 `Explorer.prefab` 一样）。环境光（Flat）、月光、雾、曝光都没改。
  - `HouseBlockoutGenerator.Garden.cs`：院子地面的**网格**只铺到院墙（以前伸到墙外，会盖住沟、和路面在同一高度上闪）；`Ground_Garden` 的**碰撞体**没动。
  - `DetailMats.cs` / `ExternalMats.cs`：新材质 MD_Asphalt、MD_FieldWithered、MD_FieldBank、MD_FieldFallow、MD_TinRoofRust、MD_ConcreteMoss、MD_ConcretePole、MD_PersimmonDark、MD_Cable；枝叶面片 MX_SugiSpray、MX_BambooSpray、MX_SusukiTuft（`Flora.cs` 的 `FloraMat`）。
  - 素材（4 组 Poly Haven 贴图、1 张 HDRI、3 张自己画的面片）见 `Textures/External/README_来源.md` 第六轮。
- **数量**（生成日志 `[Hako Art] 院外风景` 两行）：
  - 北坡杉 493 棵（离墙近的几排细做）、谷边 110 棵（一丛丛、高矮不齐）、东西两边 202 棵；竹 92 根（竹叶面片 3238 张）；柿子 121 个；梯田田边芒草 1489 丛、田里 513 丛、灌木 172 丛。
  - 一共 153,816 个三角形、9 个渲染器、41 个子网格：

    | 网格 | 三角形 | 子网格 |
    |---|---|---|
    | Outskirts_CedarHill | 52,392 | 2 |
    | Outskirts_SideTrees（含柿子树） | 28,640 | 5 |
    | Outskirts_Bamboo | 23,872 | 2 |
    | Outskirts_Ground | 15,778 | 8 |
    | Outskirts_ValleyTrees | 11,494 | 2 |
    | Outskirts_Grass | 11,258 | 3 |
    | Outskirts_PolesAndWires | 9,092 | 5 |
    | Outskirts_NeighbourRuin | 830 | 7 |
    | Outskirts_Barn | 460 | 7 |

  - **比计划多**：计划是约 15 万三角形、约 20 个 draw call。三角形多 2.5%；draw call 最多 41 个（每个网格每种材质一个：地面 8 种，两间房子各 7 种），是计划的两倍。都走 SRP Batcher，实际开销不大；要压回 20 左右得把材质拼成图集，这次没做。
- **网格文件**：院外的网格接在生成顺序的最后，`HouseDetail_Meshes_05.asset` 从 16 MB 涨到 43 MB，新增 `HouseDetail_Meshes_06.asset`（5 MB），仓库多了约 32 MB。前面几个文件只是顺序变了（按网格内容比对过，只有 `GardenGround` 真的变了）。
- **天空和屋里的光**：天空不参与环境光。屋里三个机位有 / 没有天空盒的平均亮度：居間 0.0181 → 0.0182、座敷 0.0241 → 0.0241、土蔵 0.0162 → 0.0162。场景没有烘焙光照，反射用的是 Unity 内置的默认反射，不是夜空。**以后要是烘焙光照，默认反射会换成夜空**，屋里的亮面（漆器、玻璃、金属）会跟着变；到时把 Lighting → Environment → Environment Reflections 的 Source 改成 Custom，指定原来的立方体贴图。
- **审图机位**（`ArtSnapshot.OutskirtsShots`；菜单“Hako/Art/9b 院外风景截图检查”，命令行 `ArtSnapshot.GenerateAndShootOutskirts`，约 1 分钟）：
  - 院子里 9 个（大门关着 / 开着 / 门里抬头、南院、东院、后院、后院东北角、后院竹林、西边），路上 4 个（往西、往东、回头看大门、仓房），高处 4 张；生成时还记下 12 个近景（`r6_*`）。
  - 前缀：`out_` 探索者（夜 + 雾 + 手电），`outghost_` 箱女视角（照 GhostSight.cs），`rev_r6_` 检查光，`revout_r6_` / `revghost_r6_` 近景的探索者 / 箱女视角，`aerial_` 高处（白天的光）。
- **已知问题**：
  - `Ground_Garden` 的碰撞体本来就伸出墙外（南北 5.58 m、东西 11.8 m），在 y = 0 盖着墙外的路面、路肩和 U 形沟。出了大门如果能往两边走，走到沟上（沟盖以外）脚下是空的沟、人却踩在沟面上；走出碰撞体的范围就会掉下去（以前也一样，院外从来没有碰撞体）。出了大门能不能走、能走多远是玩法定的，这个碰撞体美术没动；要放人出去，得把它收到墙内、院外另做碰撞体。
  - 北坡从高处看像一块平顶的台地（只在 `aerial_` 审图里看得到；院子里只看得到近处的杉）。
  - 生成日志里的 `Failed extracting collision mesh … non-finite value`（48 条，`Furn_*` 网格）以前就有，和这一轮无关。
- **给程序的提醒**：探索者相机远裁不要小于 250 m（现在的 1000 m 不用改），不然院外的树和山会被裁掉。

## 探索者地图（2026-11-26）

- **起因**：用户 2026-11-23 定探索者也要地图（方案见 `Docs/HakoOnna_ExplorerMap_Proposal.md`）：按键打开的 HUD 地图，界面自带，不占背包格；画房子、容器、金庫和仏壇、“你在这里”；没有代价（能走能跑，不出声）。2026-11-26 用户又定：容器没开过是黄色，开过是绿色。什么算“开过”、怎么算“在视野里看见”队友开过的，写在 `Docs/HakoOnna_DevPlan_V1.4.md` 3.7 节（【建议】，设计者还没点头）。
- **做法**：不另做模型，在箱女用的 `HouseMapProxy` 上加一个“探索者视图”（`SetExplorerView`）。箱女那边看到的和以前一样。
  - **你在这里**：每个有地板的房间一块红色高亮地板，一共 22 块（二楼两个楼梯井没有地板）。`Highlight(room)` 只亮一间。
  - **探索者专用的东西**都挂在 `groups[0]/ExplorerOnly` 下，平时关着：
    - 院子地面和围墙，大门处留口；
    - 三道竹篱笆，范围取 `Structure/GardenFences` 的碰撞体；
    - 3 个出口：玄関口、勝手口、表門。每个出口是门洞里一条白门槛，加门外一个尖朝外的白箭头；
    - 金庫（青色），仏壇（紫色）。
  - **出口是自动找的**：一楼不是窗的开口里，只有一个房间挨着的那些，再加围墙的大门。钉死的旧门、玻璃拉门、窗都不算。找到的不是 3 个时，生成日志会警告。
  - **名字挂点**：`landmarks` / `landmarkLabels`，3 个出口加金庫、仏壇。写法和房间名一样，由玩法用 OnGUI 写。
  - **容器**：探索者视图一打开，全部刷成 `HouseMapProxy.ExplorerUnopened`（黄）。玩法再按每个人自己的记录，把开过的刷成 `ExplorerOpened`（绿）。绿色比黄色暗一截，分不清红绿的玩家靠亮度也能分开。
  - **后院的两条蓝线不画**（風呂↔土蔵、台所↔土蔵）：它们表示箱女一次转移够得着，不是能走的路。土蔵留在原地，用 `Explode(gap, 0)`。
- **场景里只多了地图**：
  - 全是内置立方体，没有碰撞体，不投影。
  - 细节网格文件按网格内容和 HEAD 比对过，没有变。容器 45 个：0 变动。
  - 新材质：`Materials/Detail/MP_MapHere / Ground / Fence / Bamboo / Exit / Safe / Altar.mat`，都用 `HakoOnna/MapUnlit`。
- **代码**：
  - `Scripts/HouseMapProxy.cs`：新增 `Highlight`、`SetExplorerView`、`ExplorerView`、`roomHighlights`、`explorerOnly`、`landmarks`、`landmarkLabels` 和两个颜色常量。
  - `Editor/HouseBlockoutGenerator.MapExplorer.cs`：生成这些东西，在 `BuildMapProxy` 的最后调用。
- **审图**：
  - 菜单“Hako/Art/9c 3D 地图截图检查（箱女 / 探索者视图）”，命令行 `MapSnapshot.GenerateAndShoot`（含重新生成，约 3 分钟）。
  - 出 6 张图到 `Docs/screenshots/detail/`：`map_ghost_all`（箱女视图，用来确认没变），`map_explorer_1F_ima`、`_1F_yard`、`_2F`、`_all`、`_top`。
  - 房间名是玩法用 OnGUI 写的，相机拍不到。所以每张图旁边存了一个 `.json`，记下字的位置；`python -I Assets/_Art/Tools~/map_labels.py` 把字和颜色说明叠上去，出 `*_labeled.png`。
- **接入步骤**：见下面“给程序”的“探索者地图”一节。

## 第七轮：主庭园的陈设（2026-11-26）

- **起因**：用户说縁側两扇锁着的玻璃门外面的院子太空。清单按推荐定（2026-11-26“按推荐做”）：要孩子的东西（秋千、三轮车；清单里原来还有一个球，定稿时用户说不要了），要小祠和地藏，南段、东段一起做。设定见美术圣经 3.6。用户看图后定稿（2026-11-26）：球不要了，三轮车保持侧躺。
- **在哪、动了什么**：模型全在 `Dressing/Garden/GardenDressing`、`GardenDressingFlora` 下，没有灯、没有脚本。开工时主庭园只能看，没做碰撞；同一天用户在 V1.4（3.4）定了主庭园以后能用室外钥匙开玻璃门走进去，所以给新加的实心东西补了 24 个简单挡块（`Props/Dressing_Blockers/Blocker_Garden7_*`，见下面“碰撞”一条）。生成前后的场景比对过：碰撞体 760 → 784（多的就是这 24 个）、灯 7 盏、脚本 83 个、刚体 43 个没变；容器 45 个：0 变动。EditMode 测试 88 / 88 通过。
- **摆在哪**（雾里 7.5 m 以外全黑，所以都摆在离玻璃门 1–6 m 的地方；更远的只有箱女视角看得到）：
  - 第一扇玻璃门（x 16.3–19.7）：门西边一道袖垣，靠着扫帚、熊手、铁锹，地上倒着洒水壶和一双胶靴；门东边盆栽架（枯盆栽、空盆、摔在地上的一盆）；红叶的横枝上一架秋千；倒掉摔成几截的第三盏石灯笼；池边的添水；池上两块石板；檐下的空鸟笼。
  - 两扇门之间：縁台（翻倒的蚊遣り豚、空花盆）、枯绣球、一根大枯枝。
  - 第二扇玻璃门（x 28.3–31.7）：倒着的三轮车；石臼两扇当飞石；石水钵；藤架（石桌、两只石凳、枯藤）；树桩；檐下的风铃。
  - 东縁側的玻璃门（z 7.3–10.7）：正对着门一座石鸟居、石祠、地藏；门南边一道塌了一半的袖垣。
  - 远处（探索者看不到）：东南角的老柿树、手推车、墙根的花盆和旧瓦、荒掉的菜地（四道畦、歪倒的竹架）。
  - 到处：芒草 26 丛、枯绣球 4 丛、八角金盘 3 丛、落叶堆 7 堆。
- **不放的**：和 9 种物品像的（锁链、油罐、白木桩、娃娃）；像钥匙、挂锁的（V1.4 要在地上放钥匙、在门和容器上挂锁）；看起来能搜的箱子柜子；真人大小的人形（雾里会被当成箱女 / 箱人）；灯。
- **碰撞**（2026-11-26，配合 V1.4 3.4）：
  - 有挡块的：门西的袖垣（连靠着的工具）、盆栽架、秋千、倒掉的石灯笼、添水、縁台、三轮车、石水钵、藤架的 4 根柱、石桌、2 只石凳、石祠、鸟居（只有 1.55 m 高，整个挡住，人钻不过去）、地藏、东边的袖垣、柿树干、手推车、花盆和瓦、菜地的竹架、树桩。
  - 不挡路的：芒草、枯绣球、八角金盘、落叶堆、枯枝、贴地的石臼、畦、檐下的鸟笼和风铃（最低处离地 1.95 m）。
  - **留给 V1.4 第 2 批的**：池塘（现在没有碰撞，人能走进水面）、池上的两块石板、主庭园原有景物里还没有挡块的。
  - 能开的两组门（座敷前 x 28.3–31.7、東縁側东面 z 7.3–10.7）正前方 2 m 没有挡块：座敷前只有贴地的石臼和飞石，三轮车挪到了 (32.4, −2.5)。
  - `PlayerDoorwayTests.FurnitureLeavesInteractionPointsAndOtherFurnitureClear` 不许两个挡块的包围盒相交：倒掉的灯笼用一个挡块包住几截。
- **代码**：`HouseBlockoutGenerator.GardenDressing.cs`、`GardenDressing2.cs`（`BuildGardenDressing`，在 `BuildDetails` 的最后调用，接在院外风景后面）。没有新材质、新贴图，用的都是已有的。
- **数量**（生成日志 `[Hako Art] 主庭园陈设` 两行）：157,687 个三角形、26 个渲染器、56 个子网格。

  | 来源 | 三角形 |
  |---|---|
  | 自己建的（GardenDressing） | 40,351 |
  | 面片（GardenDressingFlora） | 1,924 |
  | rubber_boots（一双） | 28,064 |
  | planter_pot_clay（8 只） | 24,640 |
  | wooden_broom | 14,258 |
  | watering_can_metal_01 | 11,837 |
  | dry_branches_medium_01（2 处） | 11,549 |
  | rusted_spade_01 | 11,436 |
  | wooden_bucket_02 | 7,252 |
  | rock_moss_set_02（4 块） | 6,376 |

  四分之三是项目里已有的 CC0 模型（屋里用的同一份，面数高）。第一遍是 29.6 万，删掉了菜地里 12 棵荨麻、一只棒球（一只就 1 万面）、一只木桶、几只瓦盆和枯枝。还想再省，得给胶靴、扫帚、铁锹、洒水壶另做低面数的副本（没做）。
- **网格文件**：接在生成顺序的最后，`HouseDetail_Meshes_06.asset` 从 5 MB 涨到 14 MB。前面几个文件只是顺序变了。
- **审图机位**（生成时记下的 `r7_*` 31 个；菜单“Hako/Art/9d 主庭园陈设截图检查”，命令行 `ArtSnapshot.GenerateAndShootGarden`，约 1 分钟）：
  - `r7_door*` 7 个：站在縁側里隔着玻璃门往外看（探索者真正能看到的）。其余是院子里的近景和 3 张全景。
  - 前缀：`rev_r7_` 检查光，`revout_r7_` 探索者（夜 + 雾 + 手电），`revghost_r7_` 箱女视角。
  - 给用户看的拼图在 `Docs/screenshots/r7/`。
- **玻璃门的玻璃换成更透的**（用户 2026-11-26 定）。`MD_WindowGlass` 从 (.55, .62, .66, 不透明度 .16) 改成 (.28, .32, .34, .05)，在 `DetailMats.cs` 里。原来被强度 60 的手电一照是一整片白，隔着门看不到院子；现在手电只在玻璃上留一个亮点。所有用这个材质的地方都跟着变：縁側的 4 组玻璃拉门、柱时计的玻璃。对比图 `Docs/screenshots/r7/r7_4_glass_after.jpg`。
- **这一轮没有下载**：Poly Haven 没有手推车、三轮车、鸟笼、石祠的模型，这些是自己建的；树桩有 `tree_stump_01`，自己建的够用，没下。球用户说不要了。
- **已知问题**：
  - 三轮车是侧躺的，从门里看是车把朝上的一团，不如立着好认（用户 2026-11-26：先这样）。
  - 石头的东西（鸟居、地藏、倒掉的灯笼）在手电下很白，和原来两盏石灯笼一样（同一个材质）。

## 阴影（2026-11-26）

用户要求“给游戏场景补阴影”，看过对比图（`Docs/screenshots/shadows/对比1…6_*.jpg`）后定的。

- **原来为什么看不到影子**：阴影功能一直开着（月光、手电都是软阴影）。看不到是因为：手电离眼睛只有 25 cm，影子几乎全被物体自己挡住；屋里其余的光是没有方向的环境光，照不出影子；六盏房间微光不投影，光穿过桌子照到桌底。
- **改了三样**，数都在 `Editor/ShadowSetup.cs`，由 `AtmosphereBuilder.Build` 每次生成时写进 `Assets/Settings/PC_RPAsset.asset` 和 `PC_Renderer.asset`（要改就改代码里的数，直接改资产会在下次生成时被写回去）：
  - **环境光遮蔽**（Screen Space Ambient Occlusion）：强度 0.4 → 2.5，半径 0.3 m，采样档 中 → 高。墙角、家具脚下、缝里会暗下去。**直射光下的强度保持 0.25，不要调高**：0.5 时开箱突脸的和服和脸开始发脏，1.0 时满是黑斑和噪点。
  - **房间微光投影**（`HouseBlockoutGenerator.Landmarks.cs`）：六盏灯改成软阴影，阴影图用中档（512）。手电和这些灯共用的阴影图集上限 2048 → 4096（只是上限，URP 按实际用到的大小分配）。
  - **月光的影子**：阴影图 2048 → 4096，阴影距离 50 → 20 m，级联 4 → 3 级（2 / 6 / 20 m）。縁側地板上能看清玻璃门的格子。雾里 12 m 外已经看不见，画面和 30 m、4 级一样。
- **没有动的**：手电（位置、强度、角度、射程）、环境光的亮度、雾、曝光、软阴影档（高）、箱女的材质和补光。开箱突脸的特写改前改后对比过（座敷的箪笥）：并排看不出差别，量出来略暗一点——脸约 2%、和服约 4%、头发约 5%、她头下面的抽屉底板约 7%。
- **副作用**：箱女视角（关雾、夜视）里墙角和家具脚下也多了一圈压暗，院子的草丛会暗一点；20 m 外不再画月光的影子。
- **开销**：批处理编辑器里 1080p 测的（RTX 4080 笔记本，3 轮取中位数，轮与轮之间波动约 ±0.6 ms）：改之前 12.6 ms / 帧，改之后 13.1 ms / 帧。各项单独开关的差别都在 1 ms 以内。
- **验证**（2026-11-26）：重新生成后容器 45 个 0 变动，场景还是 7 盏灯、784 个碰撞体，EditMode 测试 88/88 通过。`PC_Renderer.asset` 被 Unity 按当前 URP 的格式整个重存了一遍（除了上面两个数，其余是格式上的变化）。
- **检查工具**：`Hako → Art → 9f 阴影检查`（`Editor/ArtSnapshot.Shadows.cs`）。照探索者在游戏里看到的样子拍（手电在眼睛左下前方，不在镜头上），每个机位拍开 / 关手电两张；可以临时改参数各拍一遍、给每组计时、拍箱女突脸特写和箱女视角，用法在文件开头的注释里。
- **手电的发光点放低（已做）**：单纯放到眼睛左下约 50 cm 时，物体在地上和墙上有真正的影子，但面前有齐腰的矮柜、屏风时光会被整个挡住（对比 1 最后两行）。用户要求参考别的游戏：改成起源引擎的做法（正下方、近处和被挡时自动收回），对比图是对比 7、对比 8，2026-11-26 用户看过图后让美术直接改进了 `NetworkPlayer.Inspection.cs`，细节在下面“给程序（GPT）的接口 → 手电：发光点放低”。
- **【待定】烘焙**：现在没有烘焙，全是实时阴影（场景每轮重新生成，烘焙跟不上）。美术圣经 2.2 原写“固定光源全部烘焙”，2026-11-26 用户同意后改成“目前用实时阴影，烘焙待定”。

## 给程序（GPT）的接口

场景中所有对象按名称查找，层级如下：

```
House_Blockout
├─ Structure          静态墙地板，已设为 Static
├─ Containers         45 个容器实例，命名 <房间>_<类型>_<序号>
├─ Gameplay
│   ├─ ExplorerSpawnPoints/SpawnPoint_0..4
│   ├─ SecretExitCandidates/SecretExitCandidate_i_<房间>/{Carpet, HatchAnchor}
│   ├─ Safe_Kinko/{Model, Door/{Dial, Handle}, InteractPoint, KeyAnchor}
│   ├─ MemorialPoint_Butsudan/InteractPoint     （超度地点，具体超度操作待定）
│   ├─ Gate_Main/{GateDoor_L, GateDoor_R, InteractPoint}
│   ├─ EscapeZone_Gate
│   ├─ HouseGraph (HouseGraph)                  房间、相邻关系、容器在哪个房间
│   ├─ RoomVantages/Vantage_<房间>              每个有容器的房间一个俯视机位
│   └─ HouseMapProxy (HouseMapProxy)            3D 地图用的简化房子，默认关着；箱女和探索者共用（探索者视图 2026-11-26）
├─ Props
├─ Doors              所有能开关的门（DoorVisual），程序在运行时挂逻辑
│   ├─ Door_<房间A>_<房间B>/{Leaf_0..n, Rail}   推拉门，默认开着；室外一侧写 Outside
│   ├─ GlassDoor_<房间>_<序号>/{Leaf_0..n}      縁側朝院子的玻璃拉门，锁着
│   └─ Door_Benjo_Stall/Leaf_0                  便所隔间的铰链门，默认开着（排在最后）
└─ Lighting
    ├─ Moonlight
    ├─ PostFX_Normal
    ├─ PostFX_Hunt (HuntAtmosphere)
    └─ BlockoutPreviewCamera_DeleteLater         程序接入玩家相机后删除
```

### 容器
- 根节点留给程序挂逻辑组件；`Visual` 及其子节点归美术，程序不要改。
- 挂点：`InteractPoint`、`ItemAnchor`、`GrabAnchor`。**用递归查找**，因为箪笥的 `ItemAnchor` 在抽屉下面。
- 表现组件 `HakoOnna.Art.ContainerVisual`（在 `Visual` 上）：
  - **读条期间不要碰这个组件**：容器保持关闭，开箱进度只显示在 UI 上。
  - `IsOpen = true`：读条完成后设置，播放打开动画（约 0.35 秒）。
  - `IsOpen = false`：玩家停止交互后设置，播放关闭动画。
  - `SetOpenImmediate(bool)`：跳过动画直接到位，用于重连或中途加入时同步状态。
  - `PlayGrab()`：开箱致死时调用，此时不要设置 `IsOpen`。
  - 事件 `Opened`、`Closed`、`GrabPlayed`：可以用来接音效。
  - 编辑器里预览打开姿态：拖动 Inspector 上的 **Editor Preview** 滑块（运行时无效）。
- 碰撞体在 `Visual/Collision`，用于阻挡移动。交互检测的触发器请程序自己加在根节点上。

### 门（2026-11-19）
房间之间带门扇的门都是推拉门：门扇挂在门洞一侧墙面的轨道上，开着时停在门洞旁边的墙面上，门洞全宽可走。唯一的例外是便所隔间的门（见本节最后）。**开局全部开着。**
分工（用户确认）：美术做门扇和动画；**程序做服务器权威的开关逻辑和 E 键优先级**（准星对着门 = 开关门，对着容器 = 搜索）。

- 表现组件 `HakoOnna.Art.DoorVisual`，在 `Doors/Door_*`、`Doors/GlassDoor_*` 上：
  - `IsOpen = true / false`：播放开 / 关动画（襖、障子约 0.5 秒，板戸 0.7，玄関格子门 0.75，土蔵 1.4）。由服务器决定开关，客户端收到状态后再设置。
  - `SetOpenImmediate(bool)`：跳过动画直接到位，中途加入、重连时同步用。
  - `StartsOpen`：开局状态，服务器初始化门的状态用（锁着的门是 false）。
  - `Locked`：锁着的门，目前只有縁側的玻璃拉门。`IsOpen` 对它无效，玩家按 E 时调用 `Rattle()` 晃一下。
  - `Amount`（0 = 关，1 = 开）、`IsMoving`：动画进度。
  - `WorldClosedVolume`：门扇全部关上时占的世界空间。**关门前请检查这里有没有玩家**，组件自己不检查，关上的门扇有碰撞，会把人卡住。
  - `WorldOpening`：门洞的世界空间范围。
  - 事件 `Opened`、`Closed`、`Rattled`：可以用来接音效。
  - `DoorVisual.All`：场景里所有门，可以按名字建表做网络 ID。名字由两侧房间决定，重新生成场景不会变，除非门的布局改了。
  - `DoorVisual.FromCollider(hit.collider)`：判断准星打到的是不是门，不是门返回 null。
    - 推拉门的碰撞体在 `Leaf_*` 上（BoxCollider + 运动学 Rigidbody，跟着门扇动）：关着时挡人，开着时贴在墙上。
    - 玻璃拉门的门扇没有碰撞。玩家打到的是 `Structure` 里的 `Window_Glass`，上面挂了 `DoorLink` 指回这扇门。
  - 编辑器里预览开关：拖 Inspector 上的 **Editor Preview** 滑块（运行时无效）。
- 场景是生成的，不要在场景里给门加组件，联网逻辑请在运行时挂到 `Door_*` 根节点上（和容器一样）。
- `Rail`（门扇上方的轨道）只是模型，没有碰撞。
- **便所隔间的门**（`Doors/Door_Benjo_Stall`，第三轮，用户定“做成 E 开关的门”）：隔间太窄，旁边没有墙面停推拉门，所以做成铰链门（开き戸）。门扇绕门轴向外转 90°（开 0.6 秒、关 0.5 秒），默认开着；`Leaf_0` 的原点就是门轴。
  - 接口和推拉门完全一样（`IsOpen`、`SetOpenImmediate`、`StartsOpen`、`WorldOpening`、`WorldClosedVolume`、`FromCollider`）。`LocalSession` 用 `GetComponentsInChildren<DoorVisual>()` 收门，它排在所有门的最后，原来的门下标不变，**现有代码不用改就能用 E 开关它**。请重跑 EditMode 测试确认（`PlayerDoorwayTests` 里没有它的用例，`Blocker_Benjo_StallDoor` 删了）。
  - `WorldClosedVolume` 只是门扇关好时占的地方（和推拉门一样，关门前查这里）。门扇转的时候会扫过门外约 0.67 m 的四分之一圆，站在那里的人会被运动学门扇推一下（CharacterController 会被挤出来，不会卡住）。要不要对扫过的范围也做检查：程序定。

#### 封掉的门洞（请删掉对应的测试用例）
下面 11 处现在是实墙。`PlayerDoorwayTests.CanWalkThroughDoor` 里对应的 11 个用例走不通了，请删掉。其余门口用例不受影响：门默认开着，门扇停在门洞旁边，不占门洞。

| 封掉的门洞 | 用例 |
|---|---|
| 玄関 ↔ 縁側（钉死的旧门） | `Door_3` |
| 台所 ↔ 廊下 | `Door_5` |
| 次の間 ↔ 仏間 | `Door_8` |
| 居間 ↔ 納戸 | `Door_10` |
| 納戸 ↔ 奥の間（钉死的旧门） | `Door_11` |
| 次の間 ↔ 居間 | `Door_12` |
| 仏間 ↔ 納戸（钉死的旧门） | `Door_13` |
| 座敷 ↔ 奥の間 | `Door_14` |
| 座敷 ↔ 東縁側 | `Door_18` |
| 便所 ↔ 風呂 | `Door_28` |
| 物置 ↔ 子供部屋 | `Door_41` |

表里 3 处“钉死的旧门”只是装饰模型（`Dressing/Architecture/Sealed_*`）：没有 `DoorVisual`，也没有碰撞，挡人的是后面的墙，`DoorVisual.FromCollider` 打到这里返回 null。玩家对着它按 E 要不要有反应：待定。

### 开箱死亡演出（箱女模型第一版已进 Unity，2026-11-21）
2026-11-19 用户确认了新的开箱死亡演出，细节见 `Docs/HakoOnna_ArtBible_V0.1.md` 第 4.3 节（2026-11-21 改成开门的一瞬间就突脸：藏匿姿势只是第一帧，不停留；她张开血盆大口，双手伸出开口抓住探索者的头，把人拖进容器）。

**箱女模型（第一版）**：预制体 `Assets/_Art/Resources/HakoOnna_Ghost.prefab`，组件 `HakoOnna.Art.HakoOnnaGhost`（`HakoOnna.Multiplayer` 已经引用了 `HakoOnna.Art`）。美术文件不要改，有问题告诉美术。

- `HakoOnnaGhost.Spawn(box)`：box = 容器 Visual 上的 `ContainerVisual`；她出现在这个容器的藏匿姿势。
- `ghost.Lunge = t`：0 = 藏匿姿势，1 = 突脸终点（她的胯还在容器里，双手抓住探索者的头两侧），设了立刻生效。
- `ghost.Face`：两眼中间，跟着头动，+Z 从脸朝外。

- `Spawn` 按容器根节点的名字（`<房间>_<类型>_<序号>`）认出 8 种容器（认不出会抛异常），把她挂到容器根节点下（箪笥挂在 `Drawer_0` 下，跟着抽屉动），位置 0、缩放 1/1.25。**不要再移动、旋转、缩放她**，只设 `Lunge`；姿势本来就是按场景里放大 1.25 倍的容器摆好的。
- 突脸终点时她的眼睛离检视镜头 0.25~0.45 m（每种容器不同），差的距离由镜头往她那边拽来补，停在离两眼 0.2 m（美术设定 4.3）。这时两只手在画面左右两边；箪笥抽屉例外（脸是倒着的，手从画面上方两角伸过来）。洋服箪笥的脸也是倒着的，2026-11-22 起手肘垂在头两侧、双手同样从左右两边进画面（之前两臂在脸前交叉、把嘴挡住了，是美术脚本把倒挂时的左右手分反了，已修）。
- 0 到 1 之间（2026-11-22 第三版）是 Blender 里做好的一整段动作（`Lunge_<容器>`，25 帧，`Tools~/ghost_motion.py`），不再是两个姿势直接插值：胯先往回一缩，头先甩出去并冲过头，躯干跟上，手臂晚出发、最后到，手指最后收紧，嘴在半路张开。`Lunge` = 这段动作走到哪了（0 = 第一帧 = 藏匿姿势，1 = 最后一帧 = 以前的突脸终点，骨头位置没变），所以**程序这边的用法和数值都不用改**；时间曲线照旧由程序定，建议用接近匀速的曲线（动作里已经带了缓入和过冲，再套一层缓动会显得拖）。
- 张嘴（2026-11-22）：加了下巴、嘴角、上唇、上眼睑、上下两排牙共 8 根骨头（骨架 148 根），嘴里的牙、牙龈、舌头、内衬用第二个材质 `M_HakoOnna_Mouth`（预制体已配好）。嘴型（同日第三版）按用户给的参考照片做成真人张大嘴的样子：椭圆口、嘴角闭合，上唇下露出整排上牙，下牙沿下唇排开，舌头躺在口底；下巴张 27°。藏匿姿势嘴是闭着的（上下唇合拢，牙收在嘴里），扑出来的路上张到最大。`Lunge` 停在 1 之后她也不是静止的：`HakoOnnaGhost` 运行时自己加下巴发颤、头和脖子微抖、喘气、手指一下下收紧（预制体上的 Alive，可关、可调强度），同样是纯表现、各客户端自己算，`Face` 的位置按动作本身的姿势量（`ghost.Lunge = 1; ghost.Face.position` 不受这层抖动影响）。以后美术改动只换模型和动作，`Spawn`、`Face`、`Lunge` 的用法不变。
- 头发、袖子、下摆（2026-11-21 第二版）：加了 73 根骨头（`Hair_*`、`Sleeve_*`、`SleeveBag_*`、`Hem_*`，骨架从 67 根变成 140 根），每个姿势里它们按重力怎么垂已经在 Blender 里解算好、烘进 16 个动作（`Tools~/ghost_soft.py`）。运行时 `HakoOnnaGhost` 自己再加一层甩动（她冲出来时头发和袖子往后拖、停住后荡回去），纯表现、各客户端自己算、不同步，**程序不用接任何东西**，`Spawn`、`Face`、`Lunge` 的用法和数值都没变。不想要甩动时关掉预制体上的 Sway。
- 编辑器里看：选中场景里的容器，菜单“Hako/Art/箱女：放进选中的容器（预览，不存进场景）”，再拖她身上的 Preview Lunge（编辑器预览里没有甩动和活体细节，只有动作本身）。

#### 接入步骤（2026-11-21：把 `OccupiedOpening` 的原型圆柱换成箱女）

时长都是起点值（4.3 里写的是待定），看过再调。

**1. `OccupiedOpening.Play`**（看这次演出的每个客户端都跑）
- 删掉原型：`HakoOnnaPlayer/Body` 圆柱、红色 glow 灯、`GrabFace` 和两个眼睛球，以及 `bounds / height / start / end / direction` 那套移动。
- 签名加 `bool hiderInside`，在 `LocalSession.PlayOccupied` 里算好传进来：`FindObjectsByType<NetworkHider>(FindObjectsSortMode.None).Any(h => h.IsOwner && h.Current == id)`。
- 现在的顺序是“记零件 → 关 → 记关 → 开 → 记开 → 关”。在“记开”之后、最后那次 `SetOpenImmediate(false)` 之前生成她，量出突脸终点的眼睛位置：
  ```csharp
  var ghost = HakoOnnaGhost.Spawn(box);   // 一定在 parts 记完之后：箪笥里的她挂在 Drawer_0 下，不能进 parts
  ghost.Lunge = 1f;
  var lungeEyes = ghost.Face.position;     // 门 / 抽屉全开时，突脸终点两眼的位置
  ghost.Lunge = 0f;
  box.SetOpenImmediate(false);             // 原来就有的这一行；箪笥里的她跟着抽屉回去
  ```
- `hiderInside` 时把 `ghost` 下所有 `Renderer` 的 `enabled` 设成 false：箱女玩家自己的镜头在箱子里，会在模型里面。她这时看到什么画面：待定。
- 补光：`ShowOccupiedOpening` 一开始就收走了受害者的手电（`lampOn = false`、清空物品），没有灯她在黑屋里看不见。在 `ghost.Face` 下挂一盏 Spot：`localPosition = (0, -0.12, 0.45)`，`localRotation = Quaternion.LookRotation(new Vector3(0, .12f, -.45f))`（从脸前下方往脸上打），spotAngle 70，innerSpotAngle 35，range 2，color (0.82, 0.88, 1)（冷白，不改她纸白的肤色），不投影，intensity 每帧 = 3 × open（门关着时不亮）。
- `victim.TrackGrabFace(face)` 换成 `victim.TrackGrab(ghost.Face, lungeEyes)`（见 2）。
- 循环跑到 t = 1.7 s（原来 1.4），每帧：
  ```csharp
  float open = Mathf.SmoothStep(0, 1, t / .22f) * (1 - Mathf.SmoothStep(0, 1, (t - 1.25f) / .45f));
  float rise = Mathf.SmoothStep(0, 1, (t - .12f) / .24f);
  ghost.Lunge = rise * (1 - Mathf.SmoothStep(0, 1, (t - 1.1f) / .3f));   // 1.1 s 起缩回去（受害者已经黑屏，只有旁边的人看得到）
  victim.SetGrabPull(rise);                                              // 镜头只往前拽，不跟着缩回
  faceLight.intensity = 3f * open;
  ```
  零件插值照旧用 `open`。t ≥ 0.12：`shriek = foley?.Shriek(ghost.Face)`（原来传圆柱）；t ≥ 1.1：`victim.BlackOut(1.8f)`；t ≥ 1.25：`foley?.Close()`（原来 0.95）。
- `finally`：`Object.Destroy(ghost.gameObject)`（补光是她的子物体，一起删），其余照旧。

**2. `NetworkPlayer`**（下面三个方法都只在 `IsOwner` 时生效）
- `TrackGrab(Transform face, Vector3 lungeEyes)`：记下这一刻镜头的位置 P0 和朝向 R0（这时还停在检视姿势）；终点 `P1 = lungeEyes + (P0 - lungeEyes).normalized * 0.2f`，`R1 = Quaternion.LookRotation(lungeEyes - P1, R0 * Vector3.up)`；pull = 0；照旧清掉移动输入。
- `SetGrabPull(float pull)`。`LateUpdate` 里抓人时镜头 = `Vector3.Lerp(P0, P1, pull)`、`Quaternion.Slerp(R0, R1, pull)`，pitch / yaw 从这个朝向算（代替现在“站着的眼睛看着脸”）。抓人期间隐藏受害者自己的第一人称手臂（`arms`），免得挡脸。
- `BlackOut(float seconds)`：`OnGUI` 里最后画一张盖住全屏的黑图，持续 seconds。黑屏结束接现有的转化流程（规则不变）。
- 镜头近裁剪面现在是 0.04，0.2 m 的脸不会被切；不要调大。

**3. `LocalNetworkSmokeCheck.LateUpdate`**：不再找 `GrabFace`，改成 `FindFirstObjectByType<HakoOnnaGhost>()` 的 `Face`；条件改成距离 0.2 ± 0.02 m、镜头朝向和连线夹角 < 1°；去掉 `face.forward` 那条（她的脸是摆出来的，不保证正对镜头）。

**看效果**：用 TestBox 开一个有箱女的容器。0.12 s 她扑出来；0.36 s 脸停在镜头前 0.2 m，双手在画面左右两边（箪笥抽屉例外：手在画面上方两角）；1.1 s 黑屏 1.8 s。8 种容器都开一遍。门开到一半她就开始扑，头几帧手可能穿过门板（过渡帧还没做）。

**这一步不做**：旁边的人看到受害者的身体被拖进容器（等探索者模型）；箱人（模型还没做，先都用箱女）。张嘴和过渡帧 2026-11-22 美术已经做进模型和动作里了，程序不用为它们做任何事。

以后需要程序配合的地方，具体参数做的时候再一起定：
- 镜头高度：`GameConfig` 的 `EyeHeight` 从 1.45 改到约 1.58（探索者模型约 1.70 m，2026-11-18 确认）。这一条现在就可以改。
- `PlayGrab()` 要知道扑出来的是谁：箱女，或者哪一位玩家变成的箱人（箱人用死者自己的探索者模型）。同一个容器里箱女和箱人都在时，播箱女的。
- 隐藏信息：演出用的模型只在服务器确认死亡后生成，由服务器把变体下发给要显示这次演出的客户端。受害者和附近看得到这个容器的人都要看到，不要提前同步到探索者端。
- 受害者的镜头在演出期间交给演出：已写进上面的接入步骤 2。
- 旁边的人看到第三人称版本：她扑出来，把受害者的探索者模型拖进容器，门或盖关上。
- 演出期间隐藏容器里物品的显示（只是画面，物品照样留在容器里）。
- 声音：骨头断裂的声音放在现有的 `grab` 键里。跳脸恐怖音效以后再加，到时会在 DevPlan 第 6 节申请新键。
- 时长都是待定：接入步骤里的数值（门 0.22 s 开完、0.12 s 起扑、1.1 s 黑屏 1.8 s，共约 2.9 s）是起点。
- 对藏有箱女的容器使用错误弱点道具而死亡时，也播同样的演出（2026-11-19 确认）：没有读条，容器是关着的，由演出自己把门或盖猛地打开，所以同样不要设置 `IsOpen`。
- 猎杀中被处决的演出：待定。

### 箱女转移：房间图、房间机位、3D 地图（2026-11-22）
对应 `Docs/HakoOnna_DevPlan_V1.3_Iterations.md` 模块 T。三样东西都在 `House_Blockout/Gameplay` 下，每次重新生成都会重做；程序只读，不要改它们的数据。

**1. 房间图 `HakoOnna.Art.HouseGraph`**（`Gameplay/HouseGraph`）
```csharp
var graph = FindFirstObjectByType<HouseGraph>();
int box   = Array.IndexOf(graph.containers, container.GetComponentInChildren<ContainerVisual>()); // 容器下标
int room  = graph.RoomOf(box);                       // 容器在哪个房间（rooms 的下标）
bool ok   = graph.CanTransfer(fromBox, toBox);       // 一次转移能不能到：同房间，或两个房间相邻
graph.Neighbours(room, list);                        // 通道栏：相邻的房间
graph.TryGetLink(room, other, out var kind);         // Wall = 同层隔壁；Stairs = 楼梯；Yard = 后院
graph.LeadsToward(room, other, r => 条件);           // 方向徽标：往 other 走是不是离“满足条件的房间”更近
graph.Hops(roomA, roomB);                            // 最少换几次房间
graph.RoomAt(worldPosition);                         // 一个点在哪个房间（院子、屋外 = -1）
```
- `rooms[i]`：`id`（"Zashiki"）、`label`（"座敷"）、`floor`（0 一楼 / 1 二楼）、`building`（0 主屋 / 1 土蔵）、`center`、`size`、`ceilingY`、`hasContainers`、`vantage`。
- `containers` 的顺序就是 `House_Blockout/Containers` 下 `GetComponentsInChildren<ContainerVisual>()` 的顺序。
- 通道栏的图标：`kind == Stairs` 时比较两个房间的 `floor`（去更高的是 ↑，更低的是 ↓）；`kind == Yard` 时看 `building`（去土蔵是 ⌂，回主屋是 ↩）。
- 相邻关系现在是 27 条：`HiderRoutes` 手写的 26 条 + 台所↔土蔵（用户 2026-11-22 定）。所以 `HiderRoutes` 可以改成读这张图：转移规则换成 `CanTransfer`（“每对相邻房间只连最近的一对容器”那一条按 V1.3 取消），服务器校验也用它。
- 没有容器的房间（廊下、楼梯间等 8 个）也在 `rooms` 里，但不参与相邻：隔着走廊正对的两个房间已经直接连上了。

**2. 房间机位**（视角转移）
- `graph.rooms[i].vantage`（`Gameplay/RoomVantages/Vantage_<id>`）：直接用它的位置和朝向；没有容器的房间是 null。
- 审图截图用的是 FOV 70。机位离天花板 0.4 m、离墙 0.5 m，程序想让玩家转动视角的话，建议只转不移（左右各 60° 以内不会穿墙）。
- 机位只是一个空节点，没有相机。显示什么（要不要显示探索者）是规则，待定（V1.3 T.6）。

**3. 3D 地图 `HakoOnna.Art.HouseMapProxy`**（`Gameplay/HouseMapProxy`，默认整个关着）
```csharp
var map = FindFirstObjectByType<HouseMapProxy>(FindObjectsInactive.Include);
map.Show(true);                   // 只在打开地图的这台客户端上开；别的客户端不开就看不到
map.Explode(5f, 5f);              // 二楼抬高 5 m、土蔵往外推 5 m，三部分互不遮挡；(0, 0) 复原
map.ShowGroup(1, false);          // 单独隐藏：0 一楼、1 二楼、2 土蔵
map.Tint(box, color);             // 给容器的小方块上色（当前所在、能去、冷却中……）
map.MarkerPosition(box);          // 小方块的世界坐标（点选、画线、放图标用）
map.roomAnchors[room].position;   // 房间地板中心（放房间名用）
map.Show(false);                  // 关地图
```
- 地图在真房子正下方 300 m（`HouseMapProxy.Depth`），**要用一台单独的相机看**：`renderPostProcessing = false`（场景的后期会把它压得很暗）、背景用纯色、远裁剪面 80 m 就够。材质不受雾，不用管 `RenderSettings.fog`。
- 相机位置的起点（审图截图用的）：地图包围盒中心 + (−15, 19, −18) 看向中心，或正上方 34 m 俯视。
- 小方块没有碰撞体。点选可以用 `camera.WorldToScreenPoint(map.MarkerPosition(i))` 找离鼠标最近的一个。
- `linkLines[i]` 和 `graph.links[i]` 一一对应（只有楼梯、后院那三条有线，其余是 null）；`Explode` / `ShowGroup` 以后线会自己重画。

**4. 楼梯**
- 不用改代码。`Step_NN` 的名字和位置没变（`HouseFoley` 照旧）；`WalkRamp` 加宽到 3.48 m。

### 探索者地图：`HouseMapProxy` 的探索者视图（2026-11-26）
对应 `Docs/HakoOnna_ExplorerMap_Proposal.md` 第 3 节。

**已定的规则**：
- 按键打开的 HUD 地图，探索者界面自带，不是物品。
- 画房子、容器、金庫、仏壇、你在这里。
- 开着能走能跑，不出声，不给箱女能量；不影响手电。
- 容器没开过是黄色、开过是绿色（用户 2026-11-26 定）。什么算“开过”、怎么算“在视野里看见”队友开过的，见 `Docs/HakoOnna_DevPlan_V1.4.md` 3.7 节（【建议】）。
- 不新增网络数据：地图的一切都只在本地。

**美术给的接口**
```csharp
var map   = session.Map;                         // 和箱女同一个 HouseMapProxy
var graph = session.Routes;
map.SetExplorerView(true);                       // 探索者视图：院子、围墙、竹篱笆、3 个出口、金庫、仏壇；后院两条线不画；容器全刷成黄色
map.SetExplorerView(false);                      // 箱女视图：和以前一样（容器去掉颜色）
map.Highlight(room);                             // 你在这里：只亮这一间（红）；-1 全关。房间没变时什么都不做，可以每帧调
map.Tint(box, HouseMapProxy.ExplorerOpened);     // 开过的刷绿；没开过的黄色是 HouseMapProxy.ExplorerUnopened
map.roomHighlights[room]                         // 和 graph.rooms 一一对应；二楼两个楼梯井没有地板，是 null（这两个不写名字）
map.landmarks[i].position, map.landmarkLabels[i] // 玄関口、勝手口、表門、金庫、仏壇的名字挂点（地面高度，写法同 roomAnchors）
```
- 探索者专用的东西挂在一楼那一组下面（`groups[0]/ExplorerOnly`），`ShowGroup(0, false)` 时会跟着藏起来。
- `SetExplorerView(true)` 会把**所有**容器先刷回黄色。所以先调它，再按自己的记录刷绿。`Tint` 本身不分视图，什么时候都能调。
- 图上没有人、物品、秘密出口和候选地毯；容器里有什么、有没有人，图上看不出来。

**程序要做的事（可以直接照抄给 ChatGPT）**

1. **打开 / 关闭**：写在 `NetworkPlayer` 里，只在本地 owner、角色是 `Role.Explorer` 时生效。
   - 按 `Tab`【占位键位】切换。
   - 正在搜索容器（`searching >= 0` 或 `requested >= 0`）、开着保险箱面板时，按 `Tab` 没反应。
   - 镜头距离和楼层错开高度沿用箱女的 `mapOrbitDistance`（28）和 `mapFloorGap`（5）。
   - 全屏显示，和箱女一样（方案第 5 节第 3 问还没回答，我建议全屏）。要改成只占画面一角，设 `mapCamera.rect` 就行。
```csharp
private Camera mapCamera;
private bool Mapping => mapCamera && mapCamera.enabled;
private float mapOrbit, mapElevation, mapZoom;
private Vector3 mapCenter;

private void ShowExplorerMap(bool open)
{
    var map = session.Map;
    if (!mapCamera)
    {
        // 照抄箱女的地图相机（NetworkHider.ShowMap）
        mapCamera = new GameObject("Explorer map camera").AddComponent<Camera>();
        mapCamera.transform.SetParent(transform, false);
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = new Color(.025f, .032f, .04f);
        mapCamera.farClipPlane = 100;
        mapCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        mapCamera.enabled = false;
    }
    if (open)
    {
        map.SetExplorerView(true);                      // 先切视图（容器全变黄）……
        map.Show(true);
        map.Explode(session.Config.mapFloorGap, 0f);    // 二楼抬高；土蔵留在原地，院子里的路才对得上
        foreach (int box in 自己的开箱记录)              // ……再刷绿（记录怎么来见第 6 步）
            map.Tint(box, HouseMapProxy.ExplorerOpened);
        int room = session.Routes.RoomAt(transform.position);
        FilterMap(room >= 0 && session.Routes.rooms[room].floor == 1 ? 1 : 0);   // 只看自己这一层
        mapOrbit = yaw;                                 // 屏幕上方 = 打开时面朝的方向
        mapElevation = 55f;
        mapZoom = session.Config.mapOrbitDistance;
        mapCenter = MapTarget();
    }
    else
    {
        map.Highlight(-1);
        map.Show(false);
    }
    mapCamera.enabled = open;
    playerCamera.enabled = !open;   // 只关 Camera 组件，别关它的 GameObject：上面挂着 AudioListener、HouseAmbience 和被抓后的补光
}

private void FilterMap(int floor)   // 0 = 一楼（含土蔵、院子）、1 = 二楼、2 = 全部；和箱女的 Filter 一样
{
    var map = session.Map;
    map.ShowGroup(0, floor != 1);
    map.ShowGroup(1, floor != 0);
    map.ShowGroup(2, floor != 1);
}

// 镜头对准哪里：在房间里对准房间中心；在院子里，或者自己那一层被藏起来时，对准自己的位置
private Vector3 MapTarget()
{
    var map = session.Map;
    int room = session.Routes.RoomAt(transform.position);
    var anchor = room >= 0 ? map.roomAnchors[room] : null;
    return anchor && anchor.gameObject.activeInHierarchy
        ? anchor.position
        : new Vector3(transform.position.x, map.transform.position.y, transform.position.z);
}
```

2. **每帧**：地图开着（`Mapping`）时执行，放在 `Update` 里读移动键之前。
```csharp
if (Mapping)
{
    var kb = Keyboard.current;
    var mouse = Mouse.current;
    if (kb.zKey.wasPressedThisFrame) FilterMap(0);
    if (kb.xKey.wasPressedThisFrame) FilterMap(1);
    if (kb.cKey.wasPressedThisFrame) FilterMap(2);
    if (mouse.rightButton.isPressed)
    {
        var d = mouse.delta.ReadValue();
        mapOrbit += d.x * .35f;
        mapElevation = Mathf.Clamp(mapElevation - d.y * .35f, 15, 85);
    }
    mapZoom = Mathf.Clamp(mapZoom - mouse.scroll.ReadValue().y * .025f, 10, 70);
    session.Map.Highlight(session.Routes.RoomAt(transform.position));   // 你在这里；院子里、屋外是 -1，全不亮
    mapCenter = Vector3.Lerp(mapCenter, MapTarget(), 1 - Mathf.Exp(-6 * Time.unscaledDeltaTime));   // 走到别的房间时镜头跟过去
    var rot = Quaternion.Euler(mapElevation, mapOrbit, 0);
    mapCamera.transform.SetPositionAndRotation(mapCenter - rot * Vector3.forward * mapZoom, rot);
}
```

3. **开着地图时的输入**
   - 光标保持锁定。地图不用鼠标指东西；现在 `Update` 里光标一解锁就不读移动键了，所以打开地图时不要解锁光标。
   - `WASD`、`Shift`、空格照常，蹲下（左 `Ctrl`）也照常（第 8 步）。
   - 鼠标不转视角：`Mapping` 时跳过改 `yaw` / `pitch` 的那段。按住右键拖动是转地图。
   - `E`、`F`、`G`、`R` 不起作用：`Mapping` 时不调 `UpdateSearchInput`。
   - 滚轮给地图缩放，不换物品：`ReadEquipmentKeys` 里 `Mapping` 时跳过滚轮。数字键换物品、`T` 开关手电照常。
   - `Tab` 关闭。`Esc` 也关闭；`Esc` 照旧会解锁光标，和现在一样。

4. **自动关**：下面这些情况调 `ShowExplorerMap(false)`。放在 `Update` 开头的 `if (grabFace || IsBlackedOut) return;` 之前。
   - 被抓（`grabFace`）、黑屏（`IsBlackedOut`）、被拉进容器；
   - 死亡或转化（角色不再是 `Role.Explorer`）；
   - 开始搜索，或者打开保险箱面板。

5. **名字**：照箱女地图的 `OnGUI` 写法。
```csharp
if (Mapping)
{
    var map = session.Map;
    var graph = session.Routes;
    for (int i = 0; i < map.roomAnchors.Length; i++)
    {
        var a = map.roomAnchors[i];
        if (!a || !a.gameObject.activeInHierarchy || !map.roomHighlights[i]) continue;   // 楼梯井不写
        var p = mapCamera.WorldToScreenPoint(a.position + Vector3.up);
        if (p.z <= 0) continue;
        GUI.Label(new Rect(p.x - 25, Screen.height - p.y, 90, 24), graph.rooms[i].label);
    }
    for (int i = 0; i < map.landmarks.Length; i++)
    {
        var l = map.landmarks[i];
        if (!l || !l.gameObject.activeInHierarchy) continue;
        var p = mapCamera.WorldToScreenPoint(l.position + Vector3.up);
        if (p.z <= 0) continue;
        GUI.Label(new Rect(p.x - 25, Screen.height - p.y, 90, 24), map.landmarkLabels[i]);
    }
}
```

6. **容器刷绿**（规则见 V1.4 3.7 节，【建议】）
   - 每个探索者在本地记一份“开过的容器”。容器下标用 `Array.IndexOf(graph.containers, container.GetComponentInChildren<ContainerVisual>())`，见上面“房间图”一节。
   - 规则和做法见 `Docs/HakoOnna_DevPlan_V1.4.md` 3.7 节和“步骤 6：探索者地图的黄和绿”，都是【建议】：
     - 搜索完成、容器打开了才算开过：普通搜索、深度搜索都算；只开了锁没搜不算；打开时被拉进去死了也不算。
     - 队友开过的，要在自己屏幕上、没被墙挡住、10 m 以内（`mapSeenRange`【占位】）看见过才变绿；不要求看见时它还开着。
     - 数据从已经同步的 `openBox` 来，不新增网络数据。地图上不标锁，也不标锁住的房间。
   - 美术这边只要求两件事：打开地图时先 `SetExplorerView(true)`，再把记录里的刷成 `ExplorerOpened`；地图开着时记录变了，就 `Tint` 那一个。
   - 只能用这名玩家自己知道的东西：自己开的，自己看见的。不能用服务器上别人的搜索记录，否则违反“服务器只给每个角色下发它该知道的内容”（V1.2 第 4 节）。

7. **箱女那边**：`NetworkHider.ShowMap(true)` 里，在 `Map.Show(open)` 之前加一句 `Map.SetExplorerView(false);`，其余不变。同一台客户端可能先当探索者、后来变成箱人，所以打开箱女地图时要把探索者视图关掉。

8. **蹲下**：V1.4 3.9 节已定按住蹲下，键位左 `Ctrl`【占位键位】，和地图的 `Z` / `X` / `C` 不冲突。地图开着时也能蹲着走（V1.4 3.9，【建议】），蹲下键和 `WASD` 一样照常读。以后改键位时，避开地图用的 `Tab`、`Z`、`X`、`C`。

**验收**
1. 探索者按 `Tab` 打开地图，按 `Tab` 或 `Esc` 关闭。搜索容器、输保险箱密码时按 `Tab` 没反应。被抓、黑屏、转化时地图自动关。
2. 打开时：
   - 只显示自己这一层，镜头对准自己的房间；
   - 屏幕上方是打开时面朝的方向；
   - 自己的房间是红的。走到隔壁，红色和镜头跟着换；在院子里没有红色。
3. 开着地图时：
   - 能走能跑（`WASD`、`Shift`、空格），按住左 `Ctrl` 能蹲着走，鼠标不转视角；
   - `E`、`F`、`G`、`R` 和滚轮换物品不起作用；
   - 右键拖动转地图，滚轮缩放，`Z` / `X` / `C` 换楼层。
4. 打开地图前手电开着，关上地图后还开着。
5. 容器：没开过是黄色，开过是绿色。箱女藏在里面、里面有什么、别人（自己没看见）开过的，都看不出来。
6. 图上没有任何人，没有秘密出口和候选地毯。
7. 院子、围墙、竹篱笆、3 个出口（玄関口、勝手口、表門）、金庫、仏壇都在图上，出口、金庫、仏壇有名字。后院两条蓝线不显示，土蔵在原地。
8. 没有新增网络数据（同步变量、RPC）。
9. 箱女的地图和以前一样：后院蓝线在，没有院子和出口，容器按能不能转移上色。

### 保险箱（金庫）的模型（2026-11-23）
对应 V1.3 第 9.1 节。`Gameplay/Safe_Kinko`（居間北墙）现在是一只绿漆铸铁保险箱，代码在 `Editor/HouseBlockoutGenerator.Safe.cs`。
```
Safe_Kinko
├─ Body            灰盒方块：不显示了，碰撞体照旧
├─ Model           箱体（空心，里面有一块搁板）
├─ Door            门，轴心在合页上
│   ├─ Dial        转盘，轴心在转盘中心
│   └─ Handle      把手，轴心在把手轴上
├─ InteractPoint   站位（没变）
└─ KeyAnchor       钥匙放的位置（没变，正好在搁板上）
```
- 开门：`door.localRotation = Quaternion.Euler(0, 105, 0)`；关门是 `Quaternion.identity`。门只是模型，没有碰撞体。
- 转盘、把手：绕自己的 Z 轴转（`localRotation = Quaternion.Euler(0, 0, 角度)`），输密码、开门时可以用来做动画。
- 三个会动的部件都不参加静态合批。大门门扇上的细节（`GateDoor_L/R_Detail`）以前是静态合批的，门扇转动时不会跟着走，这次一起改了。
- 密码怎么输（转盘还是按键）在美术圣经里是【待定】；模型上是转盘，界面照 V1.3 的“输入 4 位数字”做就行。

### 手电：发光点放低，参考起源引擎（2026-11-26，已改进游戏）

用户看过对比图后说“你能做就给你做”，所以这一处是美术直接改的 `Assets/_Game/Multiplayer/NetworkPlayer.Inspection.cs`（和 11-23 调手电亮度那次一样）。**程序以后改这个文件时请保留 `beamDrop` 字段和 `PlaceBeam` 方法。**

**为什么：** 手电的发光点原来离眼睛只有 25 cm（左 17、下 19），影子几乎全被物体自己挡住。Valve 的起源引擎（《半条命 2：第二章》那一代，`flashlighteffect.cpp` 的 `CFlashlightEffect::UpdateLightNew`，源码公开）的做法是：发光点在眼睛**正下方 20 英寸（51 cm）**，不往左右偏；光束照向视线前方很远的一点，所以和视线有个小夹角；低头时发光点往眼睛收（收到 8 英寸）；眼睛到发光点之间有东西挡着就退回眼睛的位置；离前面的东西不到 3.25 m 时把灯往后拉，免得光斑缩成一小团。

**这里的做法**（对比图 `Docs/screenshots/shadows/对比7、对比8`）：眼睛比 Valve 的矮（1.45 m 对 1.63 m），放低 45 cm；“离得近时往后拉”改成“往眼睛收”，因为往后拉会让自己的身体挡光。站在容器跟前时发光点自动回到眼睛旁边，检视容器和以前一样照得到。

**改了什么**（只有这一个文件，手电筒模型 `lampModel` 的位置没动）：
- 新字段 `private float beamDrop = .19f;`
- `UpdateEquipmentPose()` 里 `lampModel.SetPositionAndRotation(...)` 之后多一行 `PlaceBeam(eye, rotation);`
- 新方法 `PlaceBeam(Vector3 eye, Quaternion rotation)`：每帧把 `beam` 放到眼睛正下方并转向视线前方 6 m 处。

**数值**（都是 `PlaceBeam` 开头的常量）：平视看远处时低 45 cm（`lowDrop`，可以在 35–51 cm 之间调，越低影子越长）；最近低 19 cm（`nearDrop`，原来的高度）；发光点在眼睛前方 30 cm（`ahead`）；光束照向视线前方 6 m（`aim`）；看的东西 0.8 m 以内全收回、2 m 以外放到最低；低处往前 2.5 m 以内有齐腰的东西挡着也收回；平滑系数 10。强度、角度、射程、软阴影都没变。

**美术验证过的：** 编译通过、EditMode 测试通过；检查工具（`Hako → Art → 9f`，默认用同一套算法）拍的画面里，手臂和手电筒模型的替身没有投出大片影子，站在箪笥、長持、葛籠跟前光斑在画面正中。

**还没在实机里验证的（请程序或用户走一圈）：**
- 这几条射线会不会打到玩家自己的碰撞体（主机上每个玩家都有 `CharacterController`）。打到的话发光点会一直收在眼睛旁边，看起来就和没改一样；需要的话换成不含玩家的层。
- 蹲下、检视容器（镜头凑进抽屉和箱子）时光斑的位置。
- 贴着屏风、矮柜站的时候光斑跳不跳；脚边有矮柜时远处地面会有一条它的影子，这是放低以后正常的现象。
- 别的玩家看你：光从手电筒模型下方约 25 cm 处出来，远看应该不明显。

### 调亮、居間的葛籠（2026-11-23，用户实机后两次要求）
- 环境光 (.011, .013, .02) → (.07, .078, .105)，月光 0.22 → 0.35，后期曝光 0.1 → 0.6（`AtmosphereBuilder.NormalExposure`，每次生成都会写进 `VP_Normal`）。雾没动。
- 手电（`NetworkPlayer.Inspection.cs`，第二次是用户让美术直接改的）：亮度 25 → 60，射程 24 → 30 m，光锥 55° / 内 25° → 70° / 内 35°。审图截图用同样的数。
- `Ima_Tsuzura_03` 沿北墙往东挪了约 1 m：原来挡着西墙箪笥最下面的抽屉。容器基准已更新。

### 秘密出口
- 候选点在被发现前只有地毯，**没有暗门模型**。
- 被发现时，程序隐藏 `Carpet`（或播放掀开动画），然后在 `HatchAnchor` 处实例化 `SecretExit_Hatch.prefab`。
- 暗门预制体中：`Visual/Chain` 是锁链组，剪断后隐藏；`Visual/HatchDoor_Pivot` 可以旋转打开；`EscapePoint` 是逃离判定位置。

### 猎杀画面
- 获取 `PostFX_Hunt` 上的 `HakoOnna.Art.HuntAtmosphere`，调用 `SetHunt(true)` 或 `SetHunt(false)`。
- 玩家相机必须开启 URP 的 Post Processing（`renderPostProcessing = true`）。

### 程序集
- 运行时：`HakoOnna.Art`（asmdef）。程序如果使用 asmdef，需要在引用里加上它。
- 编辑器：`HakoOnna.Art.Editor`。

## 已知限制
- **代码没有在 Unity 中编译测试过**，第一次导入如有报错，把 Console 内容发给我。
- 门还没有音效（事件 `Opened`、`Closed`、`Rattled` 已经留好）。
- 钉死旧门上的符纸、血手印、抓痕还是程序生成的贴图（第二轮新加的血字、漏水痕、烟熏、香灰是外部贴图或 `Tools~/make_textures.py` 生成的贴花）。
- 地板全部在 y = 0，没有做日式住宅抬高的木地板和玄关台阶，避免灰盒阶段增加台阶碰撞问题。
- 屋顶是平的。
- 楼梯用不可见的斜坡碰撞体（`WalkRamp`，宽 3.48 m = 楼梯间净宽）行走，台阶模型本身没有碰撞。
- 第二轮压暗以后，没有手电时只看得清月光照到的地方。在编辑器里检查布局看不清时，用菜单“3 切换预览照明”临时调亮（保存场景前切回去）。
