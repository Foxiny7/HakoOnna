# 探索者 — 角色三视图提示词 V1.0（ChatGPT 生图 → Tripo 建模）

> 2026-11-23 写。五个人的外观当天通过，以美术设定文档 6.1 为准。
> - 提示词用**英文**写：生图模型对镜头、光线、材质这类术语的英文理解更准。每条上面有中文说明，方便你看懂和改。
> - 原创要求：提示词里不出现任何游戏名、桌游名或现有作品名。
> - 这一轮只做五张三视图（建模用）。箱人版以后直接用这五个模型改，不另外出图。

---

## 0. 工作流程

1. 开一个**新的** ChatGPT 对话，第一条消息发第 1 节（五个人和风格的设定），等它回一句确认。
2. 依次做图 0-1 ~ 0-5，一个人满意了再做下一个。五个人放在同一个对话里做，画风和比例更容易统一。
3. （可选）做图 L：五个人在黑暗里站成一排，看分不分得开。
4. 把每张三视图裁成正面、左侧、背面三张**一样高**的图，分别放进 Tripo 多视角的三个上传位。
5. 画得不对时，用第 5 节的说法局部修改，不要整张重画。
6. 做到后面如果人物开始串（拿了别人的衣服或配件），就开一个新对话：先发第 1 节，再发那个人的提示词，并附上他之前满意的图。

| 图 | 内容 | 交给 Tripo？ |
|---|---|---|
| 0-1 ~ 0-5 | 五个人各一张正 / 侧 / 背三视图，A 字站姿 | **是**，多视角转模型 + 自动绑定 |
| L | 五个人在黑暗里站成一排（可选） | 否，只用来检查轮廓 |

**三视图里特意不画、以后在 Blender 里补的东西：**
- 直樹的圆框眼镜：细金属框 Tripo 做不好，而且箱人版要碎一边，所以单独做。
- 健太盖住半个手背的卫衣袖口：三视图里先停在手腕，保证手干净、好绑定。
- 美穂的头发和由香的马尾：和衣领之间留一道缝，免得 Tripo 把头发和衣服粘成一块。

---

## 1. 五个人和风格的设定（新对话第一条消息，整段复制）

里面定义了五个人、两个风格开关（MODEL / FILM）、三视图的固定版式和禁止项。后面每条提示词只要写人物编号和 “Style: MODEL”。

```
I'm art-directing an original Japanese horror video game. In this conversation you will draw five recurring characters, one at a time, mostly as clean turnaround sheets for image-to-3D conversion. Each character's identity must stay locked from image to image, and the five must look like they belong to the same game. Read this brief, reply with one short sentence to confirm, then wait for my image requests.

== THE GROUP ==
Five members of a university ghost-story club in Japan, around 1998, on an autumn night trip to explore a rumoured abandoned house. They are ordinary, believable young people: Japanese, aged 19 to 22, natural healthy skin tones, plain original faces with real character, no beauty filter, no resemblance to any real person. All five share the same body proportions: an average, slim build with normal adult proportions. The men are about 172 cm tall, the women about 166 cm. Their clothes are period-accurate Japanese street wear of the late 1990s in medium-weight autumn layers — lived-in and slightly worn, never costume-like, and close enough to the body that nothing is bulky.

CHARACTER 1 — DAISUKE (man, 21). The club president who organised the trip; bossy and confident, secretly scared.
Navy-blue baseball cap worn forwards over short black hair. Colour-blocked nylon windbreaker: a navy body with light-grey panels and a light-grey stripe along the outer forearm of each sleeve. Light-grey crew-neck T-shirt underneath. Light stone-wash straight-leg jeans. Grey sneakers. Black digital wristwatch on his left wrist. A small black nylon waist pouch on his right hip.

CHARACTER 2 — MIHO (woman, 20). The club's most serious member, determined to capture a real spirit photograph; quiet and curious.
Black ribbed knit beanie; chestnut-brown, shoulder-length hair. Olive-drab cotton field jacket with four patch pockets and buttoned cuffs, worn open over an oatmeal-coloured turtleneck sweater. Dark charcoal straight-leg trousers. Dark-brown lace-up ankle boots. A thin braided cord bracelet on her right wrist. A small black compact film camera on a thin strap worn across her body, resting on her left hip.

CHARACTER 3 — KENTA (man, 19). The timid newcomer who was dragged along; keeps saying he wants to go home.
Heather-grey pullover hoodie with the hood always up, a little of his black fringe showing; he pulls the ribbed cuffs down over the backs of his hands. A thin, flat, mustard-yellow quilted down vest over the hoodie — lightweight, not puffy. Dark indigo jeans. Dark navy canvas sneakers. A small dark-blue brocade shrine amulet (omamori) hanging from the vest's zip pull.

CHARACTER 4 — YUKA (woman, 20). The ghost-story lover who brought the rumour about the house; lively, always stirring things up.
Black hair in a high ponytail tied with a large black fabric scrunchie; thin, arched late-1990s eyebrows and light natural makeup. Violet zip-up track jacket with a stand collar and a single white piping line down the outer side of each sleeve, over a black turtleneck. Mid-blue flared jeans. Black platform sneakers with thick cream soles. A spare black scrunchie on her left wrist. A rolled-up magazine sticking out of her back jeans pocket, with no legible text.

CHARACTER 5 — NAOKI (man, 22). The senior who drove everyone here in his family's car; calm and sceptical, looks after the others.
Short black hair with a neat side parting, ears showing; round wire-rimmed glasses; large silver-and-black over-ear headphones resting around his neck. Lake-blue polar fleece zip-up jacket — a clear, medium cerulean blue with a slight teal tint and a soft fuzzy texture — over a navy-and-grey plaid flannel shirt whose cuffs show just past the fleece cuffs. Dark-grey cargo trousers. Black leather ankle boots. A slim wristwatch with a brown leather strap on his left wrist.

== STYLE TOKENS (I will name one at the end of each request) ==
STYLE: MODEL (the default) — clean, physically based 3D character render for image-to-3D conversion: plain light-grey background, soft even studio light from the front with no cast shadows, no grain, no depth of field, no post effects, everything in sharp focus, true material colours.
STYLE: FILM — photoreal still from a Japanese horror film shot on 35 mm film: low-key night lighting from cold blue moonlight and one warm-white flashlight beam; deep shadows graded cold teal-green; fine film grain.

== TURNAROUND SHEET (when I ask for one) ==
A technical sheet, not a mood piece. Format: 3:2 landscape. The same person three times, full body, identical scale, feet on one shared invisible baseline, evenly spaced with clear empty space between the views — left: front view; centre: left-side profile; right: back view. Head and feet fully inside the frame, with a margin around everything.
Pose: relaxed A-pose — standing straight, feet hip-width apart and flat, arms straight and angled about 45° down and away from the body, palms facing down, fingers slightly spread and clearly separated. Head level; a neutral, relaxed expression; mouth closed; eyes looking straight ahead.
Camera: long lens, orthographic-looking views with no perspective distortion; front and back views perfectly straight-on, side view a true 90° profile.
Consistency: proportions, face, hair, clothes, colours and accessories identical in all three views.

== RULES FOR EVERY IMAGE ==
- Exactly one head, two arms, two legs and five fingers per hand for every person.
- Each character keeps exactly the face, hair, clothes, colours and accessories defined above; never move an item from one character to another.
- Hands are always empty.

== ALWAYS AVOID ==
anime or manga style, idol or fashion-model looks, heavy makeup, any red or crimson clothing or accessories, large pure-white areas, backpacks, long coats, long skirts, bulky puffer jackets, logos, brand marks, readable text, mobile phones or any other gadget from after 2000, weapons, blood, injuries, horror make-up, captions, watermarks, borders.
```

> 几处之前没定颜色的地方，这里补上了，都是为了避开红色：健太的护身符是深蓝色（神社护身符常见红色），直樹的格子衬衫是藏青配灰。另外给由香加了 90 年代末流行的细挑眉和淡妆，给美穂的头发定成栗棕色、由香定成黑色，两个人一眼能分开。

---

## 2. 图 0-1 ~ 0-5：五张三视图

### 0-1 中村大輔

**画面：**会长。藏青鸭舌帽，藏青配浅灰的拼色风衣拉到一半，浅色石洗牛仔裤，黑色腰包挂在右胯。

```
IMAGE 0-1 — TURNAROUND SHEET: Character 1, Daisuke
Navy cap worn forwards; navy-and-light-grey colour-blocked windbreaker zipped halfway over the light-grey T-shirt; light stone-wash straight jeans; grey sneakers; black digital watch on the left wrist; small black waist pouch on the right hip.
For this sheet: the pouch sits on the side of the hip and does not cover the jacket hem; the cap's peak points straight forwards.
Style: MODEL.
```

### 0-2 吉田美穂

**画面：**想拍心灵照片的。黑色毛线帽下面露出栗棕色的头发，军绿野战夹克敞着，里面燕麦色高领毛衣，小相机斜挎在左胯。头发停在衣领上方，留一道缝。

```
IMAGE 0-2 — TURNAROUND SHEET: Character 2, Miho
Black ribbed beanie over chestnut-brown shoulder-length hair; olive-drab field jacket worn open over the oatmeal turtleneck; dark charcoal straight trousers; dark-brown lace-up ankle boots; braided cord bracelet on the right wrist; small black compact camera on a thin strap across the body, resting on the left hip.
For this sheet: her hair ends just above the jacket collar, with a visible gap between the hair and the collar and shoulders; the camera hangs flat against her hip.
Style: MODEL.
```

### 0-3 小野健太

**画面：**被拉来的新人。灰卫衣的兜帽戴着，外面芥末黄的薄羽绒背心，拉链上挂着深蓝色的护身符。这张图里袖口先停在手腕，手要完全露出来。

```
IMAGE 0-3 — TURNAROUND SHEET: Character 3, Kenta
Heather-grey hoodie with the hood up, a little black fringe showing; thin, flat mustard-yellow quilted vest over it; dark indigo jeans; dark navy canvas sneakers; small dark-blue omamori hanging from the vest's zip pull.
For this sheet only: the hoodie cuffs end at the wrists so both hands are completely clear — the pulled-down cuffs will be added in 3D. The hood frames his face without hiding it.
Style: MODEL.
```

### 0-4 藤井由香

**画面：**爱讲怪谈的。黑发高马尾配黑色大发圈，紫色运动外套袖子外侧一道白边，喇叭牛仔裤，黑色厚底运动鞋。马尾不贴着脖子和衣领，侧面和背面看得清。

```
IMAGE 0-4 — TURNAROUND SHEET: Character 4, Yuka
Black high ponytail with a large black scrunchie; thin arched eyebrows, light natural makeup; violet zip-up track jacket with one white piping line down the outer side of each sleeve, over the black turtleneck; mid-blue flared jeans; black platform sneakers with thick cream soles; spare black scrunchie on the left wrist; rolled-up magazine in the back jeans pocket, no legible text.
For this sheet: the ponytail hangs clear of the neck and collar and reads clearly in the side and back views.
Style: MODEL.
```

### 0-5 石川直樹

**画面：**开车的学长。侧分黑色短发，脖子上挂着银黑两色的大耳机，湖蓝抓绒外套拉到一半，袖口露出一点格子衬衫。这张图里**不戴眼镜**。

```
IMAGE 0-5 — TURNAROUND SHEET: Character 5, Naoki
Short black side-parted hair, ears showing; large silver-and-black over-ear headphones resting around his neck on top of the collar; lake-blue polar fleece jacket zipped halfway over the navy-and-grey plaid flannel shirt, the plaid cuffs showing just past the fleece cuffs; dark-grey cargo trousers; black leather ankle boots; brown leather-strap watch on the left wrist.
For this sheet only: draw him without his glasses — they will be modelled separately in 3D.
Style: MODEL.
```

**三视图对不上时：**先只画正面，满意后附上正面图再发：
```
Draw the left-side profile and the back view of the attached front view, on one 3:2 sheet, matching it exactly — same proportions, pose, face, hair, clothes and accessories. Style: MODEL.
```

---

## 3. 图 L：五个人在黑暗里站成一排（可选）

**画面：**夜里，老宅玄関的土间。五个人面朝镜头站成一排，从左到右：大輔、美穂、健太、由香、直樹（这张戴眼镜）。身后障子透进冷蓝的月光，把他们照成剪影；镜头后面一道暗暗的手电光，只勉强照出脸和外套颜色。用来看五个头部轮廓和五种外套颜色在黑暗里分不分得开。

```
IMAGE L — Group check in the dark (optional)
All five characters from the brief, standing in a loose row in the dark earthen-floored entrance hall of an old abandoned Japanese country house at night, facing the camera, each clearly separated from the others. From left to right: Daisuke, Miho, Kenta, Yuka, Naoki. Naoki wears his glasses. Hands empty.
Light: cold blue moonlight through a paper shoji screen behind them turns them into near-silhouettes with a thin rim of light; a dim warm-white flashlight beam from behind the camera catches their faces and the colours of their jackets only faintly.
Camera: 35 mm lens at chest height, full bodies in frame.
Purpose: the five head shapes — cap, beanie, hood, high ponytail, short hair with glasses and headphones — and the five jacket colours must read apart even in this light.
Style: FILM.
```

---

## 4. 交给 Tripo 与后续制作

| 图 | Tripo 设置 | 用途 |
|---|---|---|
| 0-1 ~ 0-5（各裁成正 / 侧 / 背三张） | Multiview 转模型，面数 ≤ 20k，Quad remesh，导出 FBX，**勾选 Auto Rig** | 正式的游戏模型 |
| L | 不交给 Tripo | 只用来检查五个人在黑暗里分不分得开 |

- 缩放（Unity 1 单位 = 1 米）：大輔、健太、直樹约 **1.72 m**；美穂、由香约 **1.66 m**。
- 五个人要换到同一套骨骼上（Tripo 自动绑出来的骨骼各不相同）。箱人的 8 个折叠姿势和第三人称拉人动画都做在这套骨骼上。
- 在 Blender 里补上三视图里特意没画的东西（见第 0 节）。
- 第一人称手臂：只显示模型的手臂部分。袖子和手是玩家自己看得最多的地方，袖子的条纹、扣子、抓绒质感、手表和手绳要在贴图上补细。
- 箱人版以后用这五个模型改（美术设定文档 6.3），不另外出图。

---

## 5. 修图与补救

**局部修改，不要整张重画：**
- 在 ChatGPT 里点开图片，用选择工具圈出画错的地方，然后写：
  `Fix only the selected area: [要改成什么]. Keep everything else exactly as it is.`
- 整体只差一点时：
  `Keep everything exactly the same; only change: [要改的地方].`

**常见问题的补充说法：**

| 问题 | 发这句 |
|---|---|
| 人物串了 | `This is Character [N], [name]. Use only the clothes and accessories the brief gives this character; remove the [item], which belongs to someone else. Keep everything else exactly as it is.` |
| 太好看，像偶像或模特 | `Make this character look like an ordinary Japanese university student of the late 1990s: a plainer face, natural skin texture, no beauty filter. Keep everything else exactly as it is.` |
| 衣服太现代 | `The clothes look too modern. Make every garment period-accurate for Japanese street wear around 1998, slightly worn. Keep the colours and the layout.` |
| 出现 logo 或文字 | `Remove every logo, brand mark and piece of text. Keep everything else exactly as it is.` |
| 出现红色 | `Remove all red: change the [item] to [colour]. Keep everything else exactly as it is.` |
| 手里拿着东西、手指粘在一起 | `Hands empty, fingers slightly spread and clearly separated in all three views. Keep everything else exactly as it is.` |
| 头发贴着衣领 | `Leave a clear gap between the ends of the hair and the collar and shoulders in all three views. Keep everything else exactly as it is.` |

**每张图的检查清单：**
- [ ] 和第 1 节一致：头部轮廓、外套颜色、配件和它们的位置都对
- [ ] 没有拿别人的衣服或配件
- [ ] 没有红色、大面积纯白、logo 和文字
- [ ] 三视图比例一致，灰底，平光，手里没东西，手指分开
- [ ] 特意不画的没画：直樹的眼镜、健太盖住手背的袖口；头发和衣领之间有缝
