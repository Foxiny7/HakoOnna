# 箱女 HakoOnna — 转移音效制作方案（给 ChatGPT / ElevenLabs）

> 用法：把整份文档发给 ChatGPT。它要按下面的清单用 ElevenLabs Sound Effects 生成音效、挑选、剪辑、导出，然后按第 6 节接进游戏。
> 这是 `HakoOnna_Audio_ElevenLabs_Brief.md` 的续篇，统一风格和导出格式都沿用那一份的第 0 节，这里只写不一样的地方。
> 写于 2026-11-22，对应 `HakoOnna_DevPlan_V1.3_Iterations.md` 的模块 T。

---

## 1. 这组声音是什么

箱女（一个小女孩模样的鬼）藏在老宅的容器里。她**每次从一个容器转移到另一个容器，必定发出一段滚动声**——这是已确认的规则，也是探索者追踪她的唯一线索。

现在游戏里只有一个文件 `HiderRoll.wav`，所有情况都播它。这次要换成一整组。

**她是怎么移动的（给声音找画面）：** 她不走路。一个蜷成一团、关节折得不对劲的小身体，在墙板后面、天花板上面、地板底下**滚过去、拖过去**，最后“咚”地落进另一个箱子里。听起来应该是：**空心的老木头里有个不大但很沉的东西在翻滚**，夹着指甲刮木头、关节磕在木板上的声音。

**统一要求（和上一份一致）：** 写实、干燥、近距离的拟音；不要音乐、不要电影化混响、不要低频“恐怖垫音”；**不要人声**（没有喘气、笑声、哭声）。材料是老旧干燥的日本木器。

## 2. 两类听众，两套文件

| 听众 | 听到什么 | 怎么播 |
|---|---|---|
| **探索者** | 一段从某个大致方向传来的滚动声。只分远近三档，**不分路线** | 3D 声，放在探索者周围八个方向之一 |
| **箱女自己**（箱人也一样） | 自己这次转移的声音，**按路线分四种** | 2D 声（不带方位），只有她自己听得到 |

## 3. 隐藏信息：探索者那一套绝对不能按路线区分

**这一条是硬性要求，接代码的时候不能违反。**

探索者听到的滚动声，只能告诉他“大概哪个方向、大概多远”。所以：

- 探索者那一套的 A / B / C 三个变体，**由客户端随机选**，和箱女这次是同房间换箱、上楼还是去土蔵**毫无关系**。
- **不要**给探索者做“楼梯版”“后院版”，也不要按目标容器的种类换声音。否则探索者一听就知道她换了楼层或者进了哪种柜子，这等于多给了规则里没有的信息。
- 箱女自己的那一套可以按路线分，因为那本来就是她自己的信息，而且只在她自己的客户端上播。

## 4. 文件清单

全部放进 `Assets/_Game/Resources/Foley/`。文件名区分大小写，一字不差。导出格式：WAV、48 kHz、16-bit、**单声道**、开头静音不超过 10 ms、峰值不超过 −1 dBFS。

### 4.1 探索者听到的（9 个文件：生成 3 个，另外 6 个由它们加工而来）

**先生成这三个（近距离版）：**

| 文件 | 时长 | 内容 |
|---|---|---|
| `Roll_Near_A.wav` | 1.6–2.4 s | 完整的一次转移：起头一下磕碰 → 一段翻滚 / 拖行 → 结尾一声空心的落定 |
| `Roll_Near_B.wav` | 1.6–2.4 s | 同一件事的另一次，节奏不同（比如翻滚更急、落定更重） |
| `Roll_Near_C.wav` | 1.6–2.4 s | 第三种，拖行的成分多一些，指甲刮木头更明显 |

三个变体要听得出是“同一种东西在动”，但细节不同，免得每次都一模一样。

**再用上面三个加工出中、远两档（不要重新生成，保证是同一个声音隔了墙的样子）：**

| 文件 | 加工方式 | 听感 |
|---|---|---|
| `Roll_Mid_A/B/C.wav` | 低通约 1400 Hz，之后把峰值重新归一化到 −1 dBFS | 隔了一堵墙：细节没了，剩下闷闷的翻滚和磕碰 |
| `Roll_Far_A/B/C.wav` | 低通约 450 Hz、高通约 40 Hz，之后把峰值重新归一化到 −1 dBFS | 隔了好几间屋子或一层楼：只剩几下沉闷的“咚、咚咚” |

加工可以用任何音频工具。用 ffmpeg 做滤波的话是下面两条；滤完以后再用你的工具把峰值归一化到 −1 dBFS（这两条命令本身不做归一化）：

```powershell
ffmpeg -i Roll_Near_A.wav -af "lowpass=f=1400" -ar 48000 -ac 1 -c:a pcm_s16le Roll_Mid_A.wav
```

```powershell
ffmpeg -i Roll_Near_A.wav -af "highpass=f=40,lowpass=f=450" -ar 48000 -ac 1 -c:a pcm_s16le Roll_Far_A.wav
```

音量的远近差别由游戏代码控制，所以三档文件本身都归一化到同样的峰值，只在音色上有区别。

### 4.2 箱女自己听到的（4 个文件）

| 文件 | 什么时候播 | 时长 | 内容 |
|---|---|---|---|
| `Transfer_Self_Room.wav` | 同一个房间里换箱子 | 0.8–1.2 s | 很短：贴着耳朵的一下翻身、木板一磕、落定。干脆利落 |
| `Transfer_Self_Next.wav` | 去相邻的房间 | 1.3–1.8 s | 钻过墙板：木头摩擦、身体挤过窄缝、落进另一个箱子 |
| `Transfer_Self_Stairs.wav` | 上楼或下楼 | 1.6–2.2 s | 一连串磕在木楼梯上的闷响（四五下，节奏不匀），最后落定 |
| `Transfer_Self_Yard.wav` | 去土蔵或从土蔵回来 | 1.8–2.4 s | 先是屋里的木头声，中间一小段贴着泥地和碎石拖过去的沙沙声，最后是土蔵里厚木头的沉重落定 |

这四条是“从里面听”的：比探索者那一套更近、更闷、低频更足，像耳朵贴在木板上。

### 4.3 两个小提示音（建议加，2 个文件）

转移冷却的时长以后会调（现在的 3 秒只是测试值），所以冷却结束要有个声音提示，玩家不用一直盯着进度条。

| 文件 | 什么时候播 | 时长 | 内容 |
|---|---|---|---|
| `Transfer_Ready.wav` | 冷却结束，可以再次转移 | 0.2–0.4 s | 极轻的一声：木头“嗒”一下松开，或者一根指甲在木板上轻敲一下。不能吓人，不能吵 |
| `Transfer_Denied.wav` | 转移被拒绝（冷却中、目标被占用） | 0.3–0.5 s | 一下发闷的撞击，像肩膀顶在关死的盖子上，没顶开 |

**所有转移音效的时长都不要超过 2.4 秒**，这样冷却调短以后也不会一条压一条。

## 5. 提示词和参数

每条至少生成 **4 个候选**，挑最好的。ElevenLabs 参数：时长手动设在上表范围内；Prompt Influence 偏高（约 0.5–0.7）。下面是英文提示词的参考稿，ChatGPT 可以改写。

- `Roll_Near_A`: *"Close-up foley of something small and heavy tumbling and rolling inside the hollow wooden wall of an old Japanese house: one sharp knock on a dry cedar board, then an uneven rolling tumble with knuckle-like knocks and fingernails scraping across wood, ending in a hollow wooden thud as it drops into an empty chest and settles with a short creak. Dry, close, no reverb, no music, no voice."*
- `Roll_Near_B`: *"Close-up foley of a small heavy bundle rolling quickly behind old wooden panelling: a rapid irregular tumble of dull knocks on thin dry boards, a brief scrape, then a heavier hollow thump landing inside a wooden cabinet, the cabinet door rattling once. Dry, close, no reverb, no music, no voice."*
- `Roll_Near_C`: *"Close-up foley of something being dragged inside a wooden crawl space of an old house: fingernails and cloth dragging slowly across rough dry planks, two soft bony knocks, a pause, then a hollow thud into a wicker and wood trunk with a light creak of woven cane. Dry, close, no reverb, no music, no voice."*
- `Transfer_Self_Room`: *"Very close, muffled foley heard from inside a wooden box: a quick body turn against wooden boards, one bony knock, a short slide, and a soft heavy settle on wood. Tight, boxy, low and dry, under one second, no reverb, no music, no voice."*
- `Transfer_Self_Next`: *"Very close, muffled foley heard from inside a wooden wall: a body squeezing through a narrow gap between dry wooden boards, wood rubbing and flexing, cloth dragging, then dropping into a hollow wooden chest with a low thud. Boxy, low, dry, no reverb, no music, no voice."*
- `Transfer_Self_Stairs`: *"Very close, muffled foley of a small heavy body tumbling along an old wooden staircase heard from underneath the steps: four or five uneven dull thumps on hollow wooden treads, a creak of an old stair board, then a final low thud landing in a wooden box. Boxy, low, dry, no reverb, no music, no voice."*
- `Transfer_Self_Yard`: *"Very close, muffled foley: a wooden knock inside a house, then a short stretch of something dragged low across packed earth and a little gravel outdoors at night, then a deep heavy thud landing on thick timber inside a massive old storehouse, with a low wooden resonance fading out. Dry, low, no music, no voice."*
- `Transfer_Ready`: *"A single very quiet dry click of old wood relaxing, like one fingernail tapping a thin wooden board once. Tiny, close, dry, no reverb, no music."*
- `Transfer_Denied`: *"A single dull muffled thump of a shoulder pushing against a closed heavy wooden lid that does not move, short, dead, low, dry, no reverb, no music, no voice."*

**怎么用 ElevenLabs：** 和上一批音效同样的做法。用网页的 Sound Effects 工具，或者用它的音效生成接口（文本提示词 + 时长 + prompt influence；具体地址和参数以 ElevenLabs 官方文档为准）。API 密钥只放在本机的环境变量里，**不要写进仓库的任何文件**。生成出来如果是 MP3，转成 48 kHz 单声道 16-bit WAV 再放进工程。

## 6. 接进游戏（给负责玩法代码的人）

现状：`NetworkPlayer.Roll.cs` 的 `PlayRollRpc(sector, level)` 和 `NetworkHider.OwnRollRpc()` 都直接读 `Resources/HiderRoll`。

**探索者一侧（`NetworkPlayer.Roll.cs`）**

- RPC 的参数**保持不变**：只有方位 `sector`（0–7）和响度档 `level`（1–3）。不要加任何和路线、目标有关的参数（第 3 节）。
- 收到后在**客户端本地**随机选 A / B / C，按 `level` 选档：3 → `Foley/Roll_Near_*`，2 → `Foley/Roll_Mid_*`，1 → `Foley/Roll_Far_*`。
- 音量做成配置【占位】：近 1.0、中 0.8、远 0.6（现在是 `level / 3`；换成分档文件后音色已经有远近区别，音量不用压那么狠）。
- 找不到新文件时退回 `HiderRoll`，保证没放音频也能跑。

**箱女 / 箱人一侧（`NetworkHider`）**

- 服务器在 `TransferRpc` 里判断这次转移的路线类型，随 `OwnRollRpc(byte kind)` 发给**拥有者自己**：0 = 同房间，1 = 相邻房间，2 = 跨楼层，3 = 起点或终点是土蔵。判断用模块 T 的房间图（连接类型）。
- 拥有者用一个专用的 `AudioSource` 播对应的 `Foley/Transfer_Self_*`，`spatialBlend = 0`（2D）。新的一条开始时停掉上一条。
- `Transfer_Ready`：客户端在本地发现冷却结束时播（按实际冷却值算，不写死秒数）。
- `Transfer_Denied`：在现有的 `DeniedRpc` 里播。

**测试**

- `HouseFoleyTests` 里加：第 4 节列的文件都能从 `Resources/Foley/` 读到。
- 冒烟检查里加：一次同房间转移和一次跨楼层转移，探索者端收到的 RPC 参数形状完全相同（只有方位和响度档）。

全部接好并验收之后，再删掉旧的 `Resources/HiderRoll.wav`。

## 7. 交付清单和验收

**必须交付 13 个文件：** `Roll_Near_A/B/C`、`Roll_Mid_A/B/C`、`Roll_Far_A/B/C`、`Transfer_Self_Room`、`Transfer_Self_Next`、`Transfer_Self_Stairs`、`Transfer_Self_Yard`。
**建议一起交付 2 个：** `Transfer_Ready`、`Transfer_Denied`。

验收（每条都要过）：

1. 文件名、格式、时长都符合第 4 节；没有一条超过 2.4 秒。
2. 没有音乐、没有人声、没有明显的混响尾巴。
3. `Roll_Near` 三个变体听得出是同一种东西，但不是同一段录音。
4. 闭着眼听 `Roll_Near` / `Roll_Mid` / `Roll_Far`，能分出近、中、远。
5. 闭着眼听四条 `Transfer_Self_*`，能分出“同房间 / 隔壁 / 楼梯 / 后院”。
6. **闭着眼听探索者那九条，分不出箱女是上了楼还是去了土蔵。**
7. `Transfer_Ready` 连续听二十次不烦人。
