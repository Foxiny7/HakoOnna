# 箱女 — 素材来源与 Tripo 生成清单 V0.1

## 1. 开源素材来源

**GitHub 上基本没有成规模的写实日式素材。**我检索后没有找到 CC0 的日式房屋或家具模型仓库，搜到的都是 CGTrader、TurboSquid 等付费商店。GitHub 上相关的只有一个用种子下载 CC0 贴图合集的仓库：[Kimbatt/cc0-textures](https://www.github.com/Kimbatt/cc0-textures)。

所以贴图建议直接去下面的 CC0 素材站，模型交给 Tripo。

| 用途 | 来源 | 授权 | 说明 |
|---|---|---|---|
| 木材、灰泥、石头、金属、地面贴图 | [Poly Haven](https://polyhaven.com) | CC0 | 有 Unity 可用的 PBR 贴图。日本杉木板 [japanese_cedar_planks](https://polyhaven.com/a/japanese_cedar_planks) 目前锁在赞助者专区 |
| 同上，数量更多 | ambientCG | CC0（据称有附加条款） | 下载前看每个素材页上的授权说明 |
| 榻榻米 | [Milkscope tatami](https://milkscope.gumroad.com/l/tatami) | 页面标注 CC0 | 免费，是 Blender 材质，需要烘焙成贴图再进 Unity |
| 障子纸、障子格 | [TextureCan Paper 0017](https://www.texturecan.com/details/281/)、[Wood 0033](https://www.texturecan.com/details/280/) | 页面未写明 CC0 | 使用前确认授权 |
| 更多免费 PBR 合集 | [OpenGameArt PBR 包](https://opengameart.org/content/pbr-texture-pack-low-resolution) | 未确认 | 含灰泥和木材，授权需要核实 |

**规则：**只用授权写明可商用的素材；每下载一个就记录到 `Assets/_Art/THIRD_PARTY.md`（来源、作者、授权）。

### 贴图对应关系
灰盒材质都在 `Assets/_Art/Materials/Placeholder/`，替换贴图时只改材质，场景和预制体不用动：

| 灰盒材质 | 找什么贴图 |
|---|---|
| M_Plaster_Tsuchikabe | 土墙 / 旧灰泥（plaster, clay wall） |
| M_Wood_Floor / M_Wood_Ceiling | 旧木地板、天花板木板 |
| M_Wood_Dark / Mid / Kiri | 深色漆木、普通木、浅色桐木 |
| M_Tatami | 榻榻米 |
| M_Shoji_Paper / M_Fusuma | 和纸、襖纸 |
| M_Stone_Doma | 土间的夯土或石头 |
| M_Tile_Bath | 旧瓷砖 |
| M_Wicker | 藤编 |
| M_Metal_Dark / Rust | 铸铁、锈铁 |
| M_Carpet_Old | 旧地毯 |

---

## 2. Tripo 生成清单

### 2.1 通用设置
- 提示词用英文，Tripo 对英文理解更好。
- 导出 **FBX**，选 **Quad remesh（四边面重拓扑）**，面数按下表设置。
- 角色勾选 **Auto Rig（自动绑定）**。
- 生成后统一缩放：Unity 里 1 单位 = 1 米，探索者男约 1.72 m、女约 1.66 m，箱女约 1.2 m。
- **原创要求**：提示词不要出现 “Hako Onna”、桌游名或任何现有作品名。

### 2.2 角色

> 2026-11-23：探索者改成 1990 年代末的 5 个固定角色，外观当天通过（美术设定文档 6.1）。和箱女一样走图生模型：三视图提示词在 `HakoOnna_Explorer_ImagePrompts.md`，下表的文字提示词只是备用。旧的探索者 A–D 已经删掉。

| 资产 | 面数 | 绑定 | 提示词 |
|---|---|---|---|
| 探索者 1 中村大輔 | ≤ 20k | 是 | `realistic japanese young man about 21, late 1990s street wear, short black hair, navy baseball cap, navy and light grey colour-blocked nylon windbreaker, light grey t-shirt, light stone-wash straight jeans, grey sneakers, small black waist pouch on the right hip, neutral expression, A-pose, full body, game character, PBR` |
| 探索者 2 吉田美穂 | ≤ 20k | 是 | `realistic japanese young woman about 20, late 1990s street wear, black knit beanie, chestnut-brown shoulder-length hair, open olive drab field jacket, oatmeal turtleneck, dark charcoal straight trousers, dark brown lace-up ankle boots, small black compact camera on a crossbody strap at the left hip, neutral expression, A-pose, full body, game character, PBR` |
| 探索者 3 小野健太 | ≤ 20k | 是 | `realistic japanese young man about 19, late 1990s street wear, heather grey pullover hoodie with the hood up, thin flat mustard-yellow quilted vest, dark indigo jeans, dark navy canvas sneakers, neutral expression, A-pose, full body, game character, PBR` |
| 探索者 4 藤井由香 | ≤ 20k | 是 | `realistic japanese young woman about 20, late 1990s street wear, black hair in a high ponytail with a black scrunchie, violet zip-up track jacket with white piping on the sleeves, black turtleneck, mid-blue flared jeans, black platform sneakers, neutral expression, A-pose, full body, game character, PBR` |
| 探索者 5 石川直樹 | ≤ 20k | 是 | `realistic japanese young man about 22, late 1990s street wear, short black side-parted hair, silver and black over-ear headphones around the neck, lake-blue polar fleece jacket, navy and grey plaid flannel shirt, dark grey cargo trousers, black leather ankle boots, neutral expression, A-pose, full body, game character, PBR` |
| 箱女 | ≤ 30k | 是 | `pale thin little girl about 7 years old, paper-white skin with a faint grey-blue undertone, grime smudges, faint paper fold creases, slightly hollow cheeks, very long soaked glossy straight black hair with a centre parting, swept behind the shoulders, thin wet strands of fringe over the forehead, pale milky clouded eyes in dark sunken sockets, blank cold stare, dark wine-red cracked lips, plain deep crimson child kimono, narrow sash knotted small at the back, narrow sleeves, irregularly torn frayed hem at mid-shin, barefoot, long thin bony fingers with swollen knuckles, japanese horror ghost, A-pose, full body, realistic, PBR` |

**说明：**
- 箱女推荐走图生模型：按 `HakoOnna_Character_ImagePrompts.md` 做图 0 三视图，裁成三张传到 Tripo 多视角；上表的文字提示词只是备用。头发先拨到肩后，挂在胸前的湿发帘以后在 Blender 里用发片加。
- 第一人称手臂用探索者模型本身：在 Unity 里只显示手臂部分，不用单独生成。
- 箱女需要“四肢着地奔跑”“关节反折”这类非标准动作。Tripo 的自动绑定只能保证标准人形骨骼，这些动作要另外找动画或手调。可以先用 Mixamo 的标准动画占位。
- 箱女的 8 个藏匿姿势和跳脸的脸都要手调、手工雕刻，Tripo 只出基础模型（见美术设定文档 6.2、6.5）。
- 开箱死亡改用完整模型扑出来（美术设定文档 4.3），不再单独生成“伸出的手”。
- 箱人不单独生成：用探索者模型改成箱人版（美术设定文档 6.3）。
- 5 个探索者在黑暗里先靠头部轮廓区分，再靠衣服深浅，最后才靠颜色（美术设定文档 6.1）。

### 2.3 关键物品

| 资产 | 面数 | 提示词 |
|---|---|---|
| Mary 玩偶 | ≤ 8k | `antique porcelain doll, faded dark red victorian dress, cracked porcelain face, glass eyes, blonde curly hair, old and dusty, realistic, PBR` |
| 箱女亡骸 | ≤ 8k | `small bundle of human bones wrapped in old faded cloth tied with rope, skull partially visible, japanese, realistic horror prop, PBR` |
| 大门钥匙 | ≤ 2k | `old japanese iron key with wooden name tag tied with string, rusty, realistic prop, PBR` |
| 保险箱 | ≤ 6k | `old japanese cast iron safe from the 1960s, dark green paint chipped, brass combination dial, small and heavy, realistic, PBR` |
| 老虎钳 | ≤ 4k | `large rusty bolt cutter, red rubber handles worn, realistic prop, PBR` |
| 锁链与挂锁 | ≤ 4k | `heavy rusty iron chain with old padlock, realistic prop, PBR` |
| 日记 | ≤ 2k | `old japanese notebook with washi paper cover, stained and worn, tied with string, realistic prop, PBR` |
| 佛龛 | ≤ 10k | `old japanese buddhist family altar butsudan, black lacquer and gold, doors open, dusty, realistic, PBR` |
| 弱点：汽油罐 | ≤ 3k | `old red metal jerry can gasoline container, japanese 1970s style, dented and rusty, screw cap, realistic prop, PBR` |
| 弱点：白木桩 | ≤ 2k | `sharpened white wooden stake, raw unpainted pale wood, hand carved point, about 60cm long, realistic prop, PBR` |
| 弱点：生锈的锁链 | ≤ 4k | `coiled heavy rusty iron chain with large ring, old paper talisman tied to it, japanese horror, realistic prop, PBR` |
| 手电筒 | ≤ 3k | `old japanese metal flashlight from the 1970s, silver body, scratched, round lens, realistic prop, PBR` |

### 2.4 容器（可选）
容器灰盒已经由脚本生成，动画也已经能用。如果要用 Tripo 生成正式模型，**门、抽屉、盖子必须是独立的部件**，否则无法开合。Tripo 生成的通常是一个整体网格，所以容器的正式模型更适合找现成素材或手工建模。

如果只是想先看看效果，可以生成关闭状态的外观，只替换 `Visual/Body` 部分：

| 容器 | 提示词 |
|---|---|
| 箪笥 | `antique japanese paulownia wood chest of drawers tansu, iron handles, worn, closed, realistic, PBR` |
| 長持 | `antique japanese large wooden storage chest nagamochi with iron fittings, lid closed, realistic, PBR` |
| 葛籠 | `old japanese woven wicker storage box tsuzura with lid, dark lacquered, realistic, PBR` |
| 洋服箪笥 | `old 1960s japanese wooden wardrobe, two doors closed, worn varnish, realistic, PBR` |

### 2.5 导入后交给我
生成完成后，把 FBX 放进 `Assets/_Art/Models/<类别>/`，告诉我文件名。我负责：
- 调整比例、轴向和材质
- 对接到预制体的 `Visual` 节点
- 设置碰撞体和 LOD
