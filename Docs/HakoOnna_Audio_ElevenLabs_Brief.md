# 箱女 HakoOnna — 音效制作方案（给 ChatGPT / ElevenLabs）

> 用法：把整份文档发给 ChatGPT。它要为下面每一条音效写出 ElevenLabs Sound Effects 的英文提示词和参数，并在生成后按“验收标准”挑选、剪辑、导出。
> 文件名必须一字不差，游戏代码按文件名从 `Assets/_Game/Resources/Foley/` 加载。

---

## 0. 给 ChatGPT 的总说明

你是一名游戏音效设计师。游戏是**第一人称多人恐怖捉迷藏**，场景是**日本乡下一栋废弃多年的老宅（昭和时代木结构民居）**，带庭院、竹篱笆、石板路、砂石枯山水。夜晚，没有音乐，声音全靠写实拟音。

鬼叫“箱女”（Hako Onna），外形是**一个小女孩**，藏在宅子里的某个容器（柜子、箱子、壁橱）里。玩家（探索者）逐个打开容器寻找她，打开她藏身的那个时，她会扑出来，并发出尖叫。

**统一风格要求：**
- 写实、干燥、近距离的拟音（foley），**不要**电影化的混响、不要音乐、不要低频轰鸣“恐怖垫音”、不要人声（尖叫除外）。
- 声音应该像在安静的木屋里**近距离（约 1 米）收音**，只带一点点小房间的自然反射。
- 材料是**老旧、干燥、略有松动的日本木器**：桐木、杉木、漆木、铁件合页、藤编、纸拉门。
- 每条提示词用英文写给 ElevenLabs，长度适中、具体描述材质 + 动作 + 节奏 + 结尾。
- 每条至少生成 **4 个候选**，挑最好的一条。

**导出格式（全部一致）：**
- WAV，48 kHz，16-bit
- 一次性音效（容器、脚步、尖叫）：**单声道（mono）**（游戏里是 3D 空间声，立体声会被折叠）
- 环境循环：**立体声（stereo）**
- 开头不留静音（≤ 10 ms），峰值不超过 −1 dBFS
- 文件名区分大小写，严格照抄下表

---

## 1. 容器开/关声（16 个文件）

老宅里共 **45 个容器，分 8 类**。每类有独立的**开启声**和**关闭声**，同类容器共用一套。

**这一组的核心要求：真实，并且有拖尾、时长长一点。**
- 开启：先有一个“动作起头”（卡扣松开 / 木头摩擦起步），然后是**持续的移动声**（推拉、合页转动、抽屉滑出），最后有**自然的余音拖尾**：木板轻微回弹的吱声、铁件轻晃、灰尘落下的细碎声，慢慢消失。
- 关闭：移动声 → **一次明确的落定/撞合声**（这是玩家判断“关上了”的听觉提示）→ 撞合后的**木头共鸣拖尾**、铁件余震，慢慢衰减。
- 拖尾要自然淡出到完全静音，不能被硬切。
- 不要“咚”一下就结束的短促音效。

| # | 容器（数量） | 结构 / 开法 | 开启文件 | 关闭文件 | 建议时长 |
|---|---|---|---|---|---|
| 1 | 押入れ Oshiire 壁橱（4） | 大型纸糊推拉门（fusuma），木框在木轨道上滑动 | `Open_Oshiire.wav` | `Close_Oshiire.wav` | 开 2.5–3.5 s / 关 2.5–3.5 s |
| 2 | 洋服箪笥 YofukuTansu 西式衣柜（4） | 高大双开木门，铁合页 | `Open_YofukuTansu.wav` | `Close_YofukuTansu.wav` | 开 2.5–3.5 s / 关 2–3 s |
| 3 | 箪笥 Tansu 桐木抽屉柜（8） | 大抽屉拉出，桐木与柜体摩擦，铁拉手晃动 | `Open_Tansu.wav` | `Close_Tansu.wav` | 开 2–3 s / 关 2–3 s |
| 4 | 茶箪笥 ChaTansu 餐具柜（4） | 双开小木门，柜内有空的瓷器层板（可有极轻的瓷器轻碰） | `Open_ChaTansu.wav` | `Close_ChaTansu.wav` | 开 2–2.5 s / 关 2–2.5 s |
| 5 | 下駄箱 GetaBako 鞋柜（4） | 单开薄木门，小铁合页，轻、空心感 | `Open_GetaBako.wav` | `Close_GetaBako.wav` | 开 1.5–2.5 s / 关 1.5–2.5 s |
| 6 | 長持 Nagamochi 大木箱（8） | 沉重的掀盖木箱，铁包角，盖子很重 | `Open_Nagamochi.wav` | `Close_Nagamochi.wav` | 开 3–4 s / 关 2.5–3.5 s |
| 7 | 葛籠 Tsuzura 藤编箱（9） | 漆过的藤编箱，掀盖，轻，有藤条的沙沙/嘎吱声 | `Open_Tsuzura.wav` | `Close_Tsuzura.wav` | 开 2–3 s / 关 2–3 s |
| 8 | 米びつ Komebitsu 米柜（4） | 中等木箱，掀盖，盖子木头厚实；里面是空的 | `Open_Komebitsu.wav` | `Close_Komebitsu.wav` | 开 2–3 s / 关 2–3 s |

**每类之间必须听得出区别**：推拉 vs 双开门 vs 抽屉 vs 掀盖；重 vs 轻；木 vs 藤。玩家闭着眼也应该能分辨出是哪种柜子在开。

**提示词示例（供参考，ChatGPT 可改写）：**
- Open_Nagamochi: *"Close-up foley of a heavy antique Japanese wooden chest lid slowly lifted open, old iron hinges creaking, dry cedar wood groaning, lid swinging back and resting, long natural tail of creaks and faint dust settling, quiet dry room, no music, no reverb"*
- Close_Tansu: *"Close-up foley of a large paulownia wood drawer pushed shut into an old Japanese tansu chest, wood sliding friction, solid wooden thud as it seats, iron handle rattling and settling, soft woody resonance fading out, no music"*

**ElevenLabs 参数建议：** 时长手动设置在上表范围内；Prompt Influence 偏高（约 0.5–0.7），让材质描述更准确。

---

## 2. 脚步声（12 个文件）

6 种地面 × 左右脚各一条。游戏每走约 1.1 m（跑步约 1.5 m）播一次，左右脚交替；**停下时代码不再触发新的脚步**，所以每条录音本身必须是**单步、干净、无拖尾**，否则人停下了还会听到声音。

**核心要求：**
- **只有一个脚步**（脚跟 + 脚掌落地），不要两步、不要连续走路的片段。
- **时长 0.20–0.40 s**，结尾硬性淡出到静音（最后 30–50 ms 做 fade-out），**不能有混响尾巴、不能有余响**。
- 音量可以小、轻柔，像穿袜子 / 软底鞋小心翼翼地走（恐怖游戏里探索者是在蹑手蹑脚）。
- 左脚（A）和右脚（B）**是同一材质的两次不同落脚**，音色一致但细节不同，避免机械重复感。
- 参考：《生化危机 7》、《Visage》、《P.T.》、《Phasmophobia》、《Amnesia: The Bunker》中室内慢走的脚步——近距离、干、短、轻，材质辨识度高。

| 地面 | 说明 | 左脚 | 右脚 |
|---|---|---|---|
| Wood 木地板 | 老旧走廊木地板，偶尔轻微吱声（不要每步都吱） | `Step_Wood.wav` | `Step_Wood_B.wav` |
| Tatami 榻榻米 | 草席，柔软、闷、带一点草编的沙沙 | `Step_Tatami.wav` | `Step_Tatami_B.wav` |
| Stone 石板（土间/玄关脱鞋石） | 硬石面，鞋底轻拍，略带砂粒 | `Step_Stone.wav` | `Step_Stone_B.wav` |
| Tile 浴室瓷砖 | 小块瓷砖，略湿，清脆但不响亮 | `Step_Tile.wav` | `Step_Tile_B.wav` |
| Earth 庭院泥土 | 夯实的泥地，夹少量枯叶、细枝 | `Step_Earth.wav` | `Step_Earth_B.wav` |
| Gravel 枯山水砂石 | 细砂石，清楚的颗粒碾压声，但要短、收得干净 | `Step_Gravel.wav` | `Step_Gravel_B.wav` |

**提示词示例：** *"Single soft footstep on old Japanese wooden floorboards, one step only, careful sneaking walk in socks, very close and dry, short, no reverb, no tail"*

**ElevenLabs 参数建议：** 时长设为最小值（0.5 s），生成后**在剪辑软件里裁到 0.20–0.40 s**，只保留一步，并在结尾加短 fade-out。一条生成结果里如果有多步，可以剪出其中最好的两步分别作为 A / B。

---

## 3. 环境声（2 个必做循环 + 可选扩展）

游戏同时播放两层循环，根据玩家在屋内还是庭院，**两层之间约 1.5 秒交叉渐变**。两层都是**无缝循环**。

| 文件 | 内容 | 时长 | 备注 |
|---|---|---|---|
| `Indoor.wav` | 老宅室内：很低的风声从缝隙钻入，偶尔木结构“咔”“吱”的热胀冷缩声，纸拉门被风轻轻震动，极远处的风 | 30–60 s | 很低调，是“安静本身”；不能有明显的标志性事件（会在循环里被听出重复） |
| `Yard.wav` | 夜晚庭院：竹叶被风吹动的沙沙声（一阵一阵），远处的秋虫鸣叫（蟋蟀/铃虫），极远处的风 | 30–60 s | 虫鸣保持“远”，不要压过竹叶 |

**要求：**
- 立体声，循环首尾无缝（ElevenLabs 若有 Loop 选项就开启；否则生成后手动做交叉淡化接缝）。
- ElevenLabs 单次时长有上限，不够长可以生成 2–3 段再拼接、交叉淡化。
- 不要音乐、不要低频“恐怖嗡嗡”、不要人声、不要鸟叫（夜晚）、不要现代声音（车、电器）。
- 室内那条里的“木头咔哒”要稀疏且随机分布，间隔不规律。

### 可选扩展（我建议加，需要另外接入代码）

下面这些是**新文件，目前游戏代码不会读取**，做好后需要由负责玩法代码的人接入。优先级从高到低：

| 文件 | 内容 | 用途 |
|---|---|---|
| `Creak_01.wav` … `Creak_06.wav` | 单次的木梁 / 地板 / 柱子“咔”“吱”声，每条 0.5–2 s，有自然拖尾 | 在室内随机位置、随机间隔（15–60 s）以 3D 声播放，比写死在循环里更真实、不易听出重复 |
| `Wind_Gust_01.wav` … `_03.wav` | 一阵较强的风扫过屋檐，纸拉门 / 雨户（木防雨板）哐啷轻响，3–6 s | 偶发事件，增加紧张感 |
| `Yard_Bamboo_Knock_01.wav` … `_03.wav` | 竹篱笆 / 竹竿被风吹得相互轻敲，1–2 s | 庭院里的随机 3D 点缀 |
| `Threshold_Wind.wav` | 开着的玄关门 / 后门处的气流声，短循环 10–20 s | 放在门口，让进出房屋的过渡更明显 |

---

## 4. 箱女尖叫（1 个文件）

| 文件 | `HiderShriek.wav` |
|---|---|
| 时长 | 1.5–2.5 s |
| 格式 | 单声道 48 kHz |

**触发时序（已由代码处理，不要把开柜声做进这条里）：**
1. 玩家打开容器 → 先播放该容器自己的开启声（第 1 节的文件）。
2. 约 0.1 秒后，箱女扑向玩家 → **同时**播放 `HiderShriek.wav`。

所以这条音效必须：
- **零起头**：第一帧就是尖叫的爆发，没有吸气、没有前置静音、没有渐入。
- **只有尖叫本身**，不包含柜门声、撞击声、音乐 stinger。

**声音描述：**
- **小女孩**的尖叫（约 8–10 岁），高、尖、刺耳，带撕裂和失真感；不是成年女性的尖叫。
- 非人化处理：带一点不自然的音高扭曲 / 喉音颤动 / 多层叠加，让人觉得“像孩子但不是孩子”。
- 开头最猛烈，然后在 1–2 秒内带着嘶哑的气声迅速衰减。
- 参考：日本恐怖片（《咒怨》《午夜凶铃》）里的尖叫，以及《Visage》《Phasmophobia》中鬼魂扑脸时的尖叫。

**提示词示例：** *"Sudden piercing scream of a ghostly little girl, high-pitched, shrill and distorted, inhuman pitch warble, instant attack with no breath or lead-in, decaying into a raspy hiss, close and dry, horror jump scare, no music"*

> 如果 ElevenLabs 的 Sound Effects 生成的人声不够像小女孩，可以改用 ElevenLabs 的 Voice Design / Voice Changer 生成或处理女孩声音，再叠加 1–2 层失真、降调副本，混合成最终版本。

---

## 5. 交付清单与验收标准

**必须交付的 31 个文件：**
- 容器 16 个：`Open_` / `Close_` × Oshiire, YofukuTansu, Tansu, ChaTansu, GetaBako, Nagamochi, Tsuzura, Komebitsu
- 脚步 12 个：`Step_` × Wood, Tatami, Stone, Tile, Earth, Gravel（各含 `_B`）
- 环境 2 个：`Indoor.wav`, `Yard.wav`
- 尖叫 1 个：`HiderShriek.wav`

**验收检查（每一条都要过）：**
- [ ] 文件名、大小写完全一致，WAV 48 kHz 16-bit，单/立体声符合要求
- [ ] 开头无静音（≤ 10 ms），峰值 ≤ −1 dBFS，没有削波爆音
- [ ] 容器：有起头 + 移动 + 落定（关闭）+ 自然拖尾，时长在表格范围内；8 类之间能听出区别
- [ ] 脚步：单步、0.20–0.40 s、结尾无余响，左右两条不完全相同
- [ ] 环境：无缝循环，听 3 遍以上不出现明显的“同一个咔哒声”重复
- [ ] 尖叫：第一帧即爆发，没有开柜声或音乐混在里面，听起来像小女孩而非成年女性
- [ ] 所有文件里没有音乐、没有现代噪声、没有不相关的人声

**响度参考：** 容器开关约 −18 LUFS（短时），脚步约 −24 LUFS，环境循环约 −30 LUFS，尖叫约 −12 LUFS。游戏内还会再调音量，这只是让素材之间大致平衡。

---

## 6. 房门开关声（13 个文件，2026-11-20 加，需要另外接入代码）

老宅里能开关的门有六种：襖、障子、板戸、玄関的格子玻璃拉门、土蔵的大门、便所隔间的铰链门；縁側朝院子的玻璃拉门锁着，只能晃。游戏在**门开始动的那一刻**播放声音，门的动画很短（0.4–1.4 s），所以声音要和动画对上。

**这一组的要求：**
- 剪好后，开门声的“停住”、关门声的“撞合”要落在下表的时间点上（从文件开头算，±0.05 s）。
- 门会被频繁开关，余音比容器短：停住 / 撞合之后只留 0.3–0.8 s 自然衰减（土蔵大门 1–1.5 s），淡出到完全静音，不要硬切。
- 老宅废弃多年：轨道里有灰和砂，木头干裂、有点变形；但不要每扇门都吱吱尖叫。
- 六种门之间要听得出区别：纸门轻、板门闷重、玻璃格子门哗啦响、土蔵门很沉。
- 导出同第 0 节：WAV 48 kHz 16-bit 单声道，开头静音 ≤ 10 ms，峰值 ≤ −1 dBFS；响度约 −20 LUFS（短时），土蔵大门约 −18 LUFS。
- ElevenLabs：Duration 填表里的“生成时长”，Prompt Influence 约 0.6，每条至少 4 个候选。

| 门（游戏里在哪） | 开门文件 | 关门文件 | 生成时长 | 停住 / 撞合在 | 剪好后长度 |
|---|---|---|---|---|---|
| 襖 Fusuma（房间之间的纸拉门） | `DoorOpen_Fusuma.wav` | `DoorClose_Fusuma.wav` | 1.5 s | 开 0.50 s / 关 0.42 s | 0.9–1.2 s |
| 障子 Shoji（房间朝縁側的格子纸门） | `DoorOpen_Shoji.wav` | `DoorClose_Shoji.wav` | 1.5 s | 开 0.50 s / 关 0.42 s | 0.8–1.1 s |
| 板戸 Itado（台所、勝手口、便所、風呂、納戸、物置、土蔵侧门） | `DoorOpen_Itado.wav` | `DoorClose_Itado.wav` | 2 s | 开 0.70 s / 关 0.60 s | 1.2–1.6 s |
| 格子戸 Koshi（玄関正门，木格子 + 玻璃） | `DoorOpen_Koshi.wav` | `DoorClose_Koshi.wav` | 2 s | 开 0.75 s / 关 0.65 s | 1.2–1.6 s |
| 土蔵の扉 Kura（土蔵正门，又厚又重） | `DoorOpen_Kura.wav` | `DoorClose_Kura.wav` | 3 s | 开 1.40 s / 关 1.20 s | 2.2–3.0 s |
| 便所隔间的铰链门 Stall | `DoorOpen_Stall.wav` | `DoorClose_Stall.wav` | 1.5 s | 开 0.60 s / 关 0.50 s | 0.9–1.2 s |

锁着的玻璃拉门：`DoorRattle_Glass.wav`，生成 1 s，晃动集中在前 0.45 s，剪到 0.6–0.9 s。

**提示词（直接用，不用改写）：**
- `DoorOpen_Fusuma`: *"Close-up foley of an old Japanese fusuma sliding door slid open by hand: a light wooden frame covered in thick paper gliding along a dry, dusty wooden groove, a tiny catch as it starts, a soft hollow papery whoosh with a faint gritty rumble, slowing to a gentle stop with a soft wooden tap, short natural decay, quiet old wooden house, no music"*
- `DoorClose_Fusuma`: *"Close-up foley of an old Japanese fusuma sliding door slid shut by hand: a light paper-covered wooden frame gliding along a dry, dusty wooden groove, a soft hollow papery whoosh, ending in a clear but gentle hollow wooden knock as the frame meets the doorpost, the paper panel buzzing faintly, short soft woody decay, no music"*
- `DoorOpen_Shoji`: *"Close-up foley of an old Japanese shoji door slid open: a very light wooden lattice frame with taut rice paper sliding along a worn wooden track, a delicate rattle of loose lattice joints, a dry papery flutter, stopping with a light wooden tick, very short decay, quiet room, no music"*
- `DoorClose_Shoji`: *"Close-up foley of an old Japanese shoji door slid shut: a very light wooden lattice frame with taut rice paper sliding along a worn wooden track, loose lattice joints rattling, ending in a light hollow wooden clack against the doorpost, the taut paper trembling with a brief dry buzz, very short decay, no music"*
- `DoorOpen_Itado`: *"Close-up foley of a heavy old wooden plank sliding door dragged open: solid, slightly warped boards scraping and grinding along a dry wooden groove, a low uneven wooden rumble that judders once or twice, a creak from the boards, coming to rest with a dull wooden bump, short woody decay, no music"*
- `DoorClose_Itado`: *"Close-up foley of a heavy old wooden plank sliding door pushed shut: solid, slightly warped boards scraping and rumbling along a dry wooden groove, ending in a solid, heavy wooden thud against the doorpost, the boards rattling briefly in their frame, short woody resonance fading out, no music"*
- `DoorOpen_Koshi`: *"Close-up foley of an old Japanese front entrance door slid open: a wooden lattice door with loose glass panes rolling on small worn metal wheels along a rail, a rhythmic rattling rumble, the glass panes chattering in the frame, slowing to a stop with a light glassy rattle, short decay, no music"*
- `DoorClose_Koshi`: *"Close-up foley of an old Japanese front entrance door slid shut: a wooden lattice door with loose glass panes rolling on small worn metal wheels along a rail, a rattling rumble, ending in a firm wooden clack against the doorframe, the glass panes shivering and rattling for a moment, short decay, no music"*
- `DoorOpen_Kura`: *"Close-up foley of a massive old Japanese storehouse door slowly dragged open: a very thick, heavy wooden door grinding on rusty iron rollers along a gritty track, a deep low rumble, a short iron squeal, the heavy door shuddering, coming to a heavy halt with a dull deep thud, low wooden resonance and dust trickling, no music"*
- `DoorClose_Kura`: *"Close-up foley of a massive old Japanese storehouse door slowly pushed shut: a very thick, heavy wooden door grinding on rusty iron rollers along a gritty track, a deep rumble, ending in a massive deep wooden boom as it seals against the frame, iron fittings rattling, dust trickling, low resonance slowly fading out, no music"*
- `DoorOpen_Stall`: *"Close-up foley of a small, thin wooden toilet stall door swung open on old dry hinges: a short rising hinge creak, a light hollow plank, a soft brush of wood at the end of the swing, very short decay, small tiled room, no music"*
- `DoorClose_Stall`: *"Close-up foley of a small, thin wooden toilet stall door swung shut on old dry hinges: a short hinge creak, ending in a light hollow wooden clap against the frame and a small metal latch click, very short decay, small tiled room, no music"*
- `DoorRattle_Glass`: *"Close-up foley of someone yanking a locked old glass sliding door in a wooden frame: two or three quick, hard rattles as the door jolts against its screw lock, loose glass panes buzzing and chattering in the frame, wooden knocks in the track, then silence, no music"*

**验收：** 文件名一字不差；停住 / 撞合落在表里的时间点；余音自然淡出；六种门闭着眼能分出来；没有音乐和人声。

**接入（给玩法那边）：** 文件放进 `Assets/_Game/Resources/Foley/`，现在的代码还不会读取。`DoorVisual` 已经有 `Opened` / `Closed` / `Rattled` 事件（门开始动、被晃的那一刻触发），在门上挂 3D 声源播放即可，做法和 `ContainerFoley` 一样。`DoorVisual` 现在还不知道自己是哪种门：要美术在生成房子时给每扇门记上种类（Fusuma / Shoji / Itado / Koshi / Kura / Stall / Glass），程序再按 `Foley/DoorOpen_<种类>` 加载。
