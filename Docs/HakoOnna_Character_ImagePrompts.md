# 箱女 — 角色概念图提示词 V1.1（ChatGPT 生图 → Tripo 建模）

> 2026-11-20 重写。V1.1：按用户给的凝视肖像参考，改了眼睛（乳白色）、头发（中分、湿碎刘海）、肤色（纸白）、嘴唇（暗红）和和服颜色（深红）；默认画风改成电影剧照（FILM）；图 A 改成凝视肖像。外形和 8 个藏匿姿势以美术设定文档 6.2 为准。
> - 提示词用**英文**写：生图模型对镜头、光线、材质这类术语的英文理解更准。每条上面有中文说明，方便你看懂和改。
> - 姿势原则：**以恐怖感为准**，身体可以多处折成不可能的角度；但每条肢体都要能从关节到关节看得清，不能画成一团。
> - 原创要求：提示词里不出现任何游戏名、桌游名或现有作品名，也不让 ChatGPT 照着某张封面画。我们只要那种感觉。

---

## 0. 工作流程

1. 开一个**新的** ChatGPT 对话，第一条消息发第 1 节（角色和风格设定），等它回一句确认。
2. 做**图 A**（走廊尽头的凝视肖像），反复改到脸满意为止。你喜欢的那张参考图可以一起发过去，但要说明只参考光线、配色和气质，不要照着那张脸画。
3. 之后每次提需求，都**附上图 A**，提示词里已经写了 “Same character as the attached image A”。
4. 做**图 0**（三视图），裁开后交给 Tripo，这是唯一要建模的图。
5. 做 8 张容器图（图 1-1 ~ 1-8）和 3 张猎杀图（图 2-1 ~ 2-3），这些都只当参考，不交给 Tripo。
6. 画得不对时，用第 7 节的说法局部修改，不要整张重画。

| 图 | 内容 | 交给 Tripo？ |
|---|---|---|
| A | 凝视肖像：走廊尽头，她静静地看着你 | 否，用来锁定长相 |
| A2 | 定调特写：从木板缝里看见她（可选） | 否 |
| 0 | 正 / 侧 / 背三视图，A 字站姿 | **是**，多视角转模型 + 自动绑定 |
| 1-1 ~ 1-8 | 打开 8 种容器时看到的她 | 否，摆藏匿姿势的参考 |
| 2-1 ~ 2-3 | 出箱、四肢爬行、直立追捕 | 否，动画参考 |

---

## 1. 角色与风格设定（新对话第一条消息，整段复制）

里面定义了三个风格开关（POSTER / FILM / MODEL）、解剖规则和禁止项，后面每条提示词只要写 “Style: FILM” 就能调用，不用重复。默认用 FILM（电影剧照感，就是你喜欢的那张参考图的感觉）；想要封面那种插画感，就换成 POSTER。

```
I'm art-directing an original Japanese horror video game. In this conversation you will draw one recurring character across many images, and her identity must stay locked from image to image. Read this brief, reply with one short sentence to confirm, then wait for my image requests.

== CHARACTER: "the girl in the box" ==
A yūrei — the ghost of a girl of about seven, roughly 120 cm tall, who was shut inside a far-too-small wooden box for years and never let out. She is a supernatural entity, not a living child.

Body — painfully thin and small; narrow shoulders; wrists and ankles like sticks; skin pulled tight over bone.
Skin — paper-white and bloodless, with a faint cold grey-blue undertone, faintly translucent; thin dark veins at the temples, throat and backs of the hands; dark smudges of grime on her cheek and chin, dirt ground into every crease; very faint fold lines across the forearms and shins, like paper that was kept folded for too long.
Hair — very long, straight, jet-black, soaked through and glossy as if she has just come out of water; parted in the centre; it hangs straight down in front of both shoulders and over her chest like a black curtain, clumped into thick wet strands with ragged, uneven ends. A ragged fringe of thin, wet, separated strands falls over her forehead and down past her eyes, some stuck to her skin. NOT a neat straight-cut fringe, NOT a bob, NOT a hime cut — nothing tidy or doll-like.
Eyes — pale, milky, almost colourless irises, clouded like the eyes of a corpse, with only a faint dark pupil; they never blink and always find the viewer; deep, sunken sockets with smoky grey-violet shadow all around; the lower lids faintly red-rimmed.
Mouth — dark wine-red lips, dry and cracked, the only colour in her face; slightly parted over small yellowed baby teeth.
Expression — usually completely blank: a cold, unblinking stare from under her brow, chin slightly lowered. When she moves: desperate and hungry, begging to be let out and wanting to pull you in.
Hands — unnaturally long, thin fingers with swollen knuckles; long, dirty, split nails, some blackened.
Clothing — a single-layer child's kimono in deep crimson red, plain and still vivid, darker only in the folds and where it is damp; it hangs heavy and clings slightly to her body; narrow sleeves hanging limp; a plain narrow cloth sash knotted small at the back, no bow; hem and sleeve ends torn irregularly and frayed; wrapped left side over right, the way the dead are dressed. The kimono always stays wrapped around her body; only her face, neck, forearms, hands, shins and feet show skin. Barefoot, soles black with dirt.
Palette — black hair, paper-white skin, crimson kimono, dark wine-red lips; nothing else about her is colourful.

== STYLE TOKENS (I will name one at the end of each request) ==
STYLE: POSTER — gritty semi-realistic horror illustration in the manner of a vintage Japanese horror film poster: photographic detail under rough, textured painterly brushwork; desaturated grey-black palette with cold green-grey shadows; the only colours are the crimson of the kimono and her dark red lips; heavy grain and grime; crushed blacks.
STYLE: FILM (the default) — photoreal still from a Japanese horror film shot on 35 mm film: low-key lighting from one soft, cold source; deep shadows graded cold teal-green; her paper-white face and the crimson kimono are the only bright things in the frame; shallow depth of field, fine film grain, slight halation around bright light.
STYLE: MODEL — clean, physically based 3D character render for image-to-3D conversion: plain light-grey background, soft even studio light, no grain, no depth of field, no post effects, everything in sharp focus.

== ANATOMY RULES (apply to every image) ==
- Exactly one head, two arms, two legs and five fingers per hand, unless I say a limb is hidden.
- When I ask for an impossible pose, every limb stays continuous and traceable from joint to joint: the joint bends the wrong way, but the limb is never duplicated, melted, cut off or fused with the container.
- Where a joint bends backwards, show the skin stretched tight and the bone pressing up beneath it.
- If a limb would be ambiguous, hide it in shadow, hair or the kimono rather than drawing it unclearly.

== ALWAYS AVOID ==
anime or manga style, cute or doll-like features, a sad or pitiful expression, blunt bangs, a bow on the sash, costume-like zigzag hems, red tears or streaks on the face, blood, open wounds, exposed bone, gore, text, captions, logos, watermarks, borders, extra limbs or fingers, modern objects.
```

> 衣襟左前 / 右前在美术设定里还是 **[待定]**。上面写的是左前（"left side over right, the way the dead are dressed"）；想用正常穿法，把这半句换成 "wrapped right side over left"。

---

## 2. 图 A：凝视肖像（定长相，先做这张）

**画面：**照着你喜欢的那张参考图的感觉：老宅走廊的尽头，手电光照到她，她一动不动地站着，已经在看你了。正面、对称、胸口以上；中分的湿长发像帘子一样垂在两肩前面，碎发挂在额头和眼前；头微微低着，乳白色的眼睛从眉骨下面往上盯着镜头；纸白的脸、暗红的嘴唇；背景是发绿的暗处，只有她的脸和深红的和服是亮的。整套图的长相都以这张为准。

```
IMAGE A — Portrait: "The stare"
Format: 2:3 portrait.
Moment: at the end of a dark corridor in the old house, the flashlight finds her standing perfectly still — already looking at you.
Framing: centred, symmetrical medium close-up from mid-chest up, her face in the upper middle of the frame. Her long wet hair, parted in the centre, hangs straight down in front of both shoulders and over her chest like a black curtain; thin wet strands of fringe lie across her forehead and down past her eyes.
Her: head lowered very slightly, chin down, eyes looking up from under her brow straight into the lens. Pale, milky, almost colourless eyes in deep, smoky, dark sockets. Paper-white skin with a faint grey-blue undertone; dark wine-red lips, barely parted; dark smudges of grime on one cheek and her chin. Expression completely blank. The crossed collar of her crimson kimono shows at the bottom of the frame.
Light: one soft, cold light from just above the lens falls on her face; everything else sinks into dark teal-green shadow; her pale face and the crimson kimono are the only bright things in the frame.
Background: the dim corridor behind her, out of focus.
Camera: 85 mm lens, f/1.8, eye level, focus on her eyes.
Mood: she isn't doing anything — and that is the worst part.
Style: FILM.
```

### 图 A2（可选）：木板缝里

**画面：**夜里撬开旧木箱的一条缝，她的脸就贴在缝后面。一道手电光斜切过她的脸，一只眼睛死盯着镜头，另一只藏在湿发后面；细长的手指扒着木板边。

```
IMAGE A2 — Key art: "She was already looking"
Same character as the attached image A.
Format: 2:3 portrait.
Moment: the instant someone pries open a gap in an old wooden box at night — and finds her face right behind it.
Framing: extreme close-up from just outside the box, looking in through a narrow gap between two warped, split cedar boards. The boards fill the left and right edges of the frame, out of focus; her face fills the centre and upper two thirds.
Light: a single hard beam of cold white flashlight cuts through the gap and falls diagonally across her face from forehead to chin; everything outside the beam drops to near-black. Dust drifts in the beam.
Her: face pressed close to the gap, head tilted about 20°. One eye is fully lit and stares straight into the lens with a tiny pinpoint catchlight; the other is half-hidden behind wet strands of hair stuck to her cheek. Lips parted, a glimpse of small yellow teeth. The long fingers of one hand curl around the edge of the board beside her face — swollen knuckles, split black nails, skin stretched white over the bone.
Camera: 50 mm lens, f/2, focus locked on the lit eye; the hand slightly soft.
Mood: the cold realisation that she was looking at you before you looked in.
Style: FILM.
```

---

## 3. 图 0：Tripo 三视图（唯一要建模的图）

**画面：**同一张图里画正面、左侧面、背面三个全身 A 字站姿，灰底、平光、无透视。
**为什么头发要拨到肩后：**Tripo 会把头发做成一整块实心的壳，盖在脸和肩膀上的头发会和身体粘死。这张图里先把头发拨到背后，盖脸的湿发以后在 Blender 里用发片加回来。

```
IMAGE 0 — Turnaround sheet for image-to-3D
Same character as the attached image A: same face, eyes, hair, skin, grime and kimono. This is a technical sheet, not a mood piece.
Format: 3:2 landscape.
Layout: the same girl three times, full body, identical scale, feet resting on one shared invisible baseline, evenly spaced with clear empty space between the views — left: front view; centre: left-side profile; right: back view. Head and feet fully inside the frame with a margin around everything.
Pose: relaxed A-pose — standing straight, feet hip-width apart and flat, arms straight and angled about 45° down and away from the body, palms facing down, fingers slightly spread and clearly separated. Head level; a cold, blank, unblinking stare straight ahead (not sad, eyebrows relaxed and level); lips barely parted.
Hair: centre parting; for this sheet only, swept behind the shoulders and down the back, with only the thin wet fringe strands hanging over the forehead and past the eyes; keep a visible gap between the hair and the neck and shoulders so the head, neck and shoulder shapes read clearly.
Kimono: narrow sleeves hang limp but leave the forearms and hands fully visible; the irregularly torn hem ends at mid-shin so the shins and bare feet are fully visible; a plain narrow sash knotted small at the back, no bow, reading as a separate layer.
Camera: long lens, orthographic-looking views with no perspective distortion; front and back views perfectly straight-on, side view a true 90° profile.
Consistency: proportions, face, hair, kimono tears and stains identical in all three views; exactly two arms, two legs and five separated fingers per hand in every view.
Style: MODEL.
```

**三视图对不上时：**先只画正面，满意后附上正面图再发：
```
Draw the left-side profile and the back view of the attached front view, on one 3:2 sheet, matching it exactly — same proportions, pose, hair, kimono tears and stains. Style: MODEL.
```
最后把三个视图裁成三张**一样高**的图，分别放进 Tripo 多视角的正面、左侧、背面上传位。

**已经有一张三视图、只想改长相时（改图，不要重画）：**附上那张三视图再发这段。让 ChatGPT 改图比重画更能保持三个视图一致。
```
Edit the attached turnaround sheet. Keep the layout, the three views, the A-pose, the scale, the grey background and the flat lighting exactly as they are, and change all three views the same way. Change only:
1. Face: colder and thinner — paper-white skin with a faint grey-blue undertone, slightly hollow under the cheekbones, a small pointed chin. Remove every red streak under the eyes; no blood anywhere.
2. Eyes: pale, milky, almost colourless irises with only a faint dark pupil; deep sunken sockets shadowed smoky grey-violet; lower lids faintly red-rimmed.
3. Expression: not sad — a cold, blank, unblinking stare straight ahead, eyebrows relaxed and level.
4. Lips: dark wine-red, dry and cracked; a few dark smudges of grime on one cheek and the chin.
5. Hair: centre parting; thin, wet, ragged fringe strands over the forehead and down past the eyes; the rest stays behind the shoulders as it is now, but soaked, heavy and glossy.
6. Kimono: plain deep crimson instead of the mottled pattern, darker only in the folds; heavier and slightly clinging; a plain narrow sash knotted small at the back instead of the big bow; narrower, limp sleeves; hem torn irregularly and frayed, not a regular zigzag.
7. Fingers: about one and a half times normal length, thin, with swollen knuckles and long dark nails.
Style: MODEL.
```

> 三视图是给 Tripo 建模用的蓝图：灰底、平光、站直，本来就不会吓人；头发也是特意拨到肩后的，挂在胸前的湿发以后用发片补上。判断恐不恐怖要看图 A 和容器图。

---

## 4. 图 1-1 ~ 1-8：打开 8 种容器时看到的她

**共同原则：**
- 第一人称：画面就是探索者刚打开这个容器、手电照进去那一刻看到的样子，容器要画出来。
- 她蜷紧塞满空间，脸朝着开口，乳白色的眼睛从湿发里死盯镜头，细长的手指扒着边缘。
- 身体可以多处折成不可能的角度（提示词里标了 IMPOSSIBLE 的地方），重点是怪诞恐怖。
- 每种容器换一种构图（正脸、倒脸、俯视、仰视），8 张放在一起不会重复。
- 这 8 张只给摆姿势的人当参考：游戏里的姿势在图 0 那个绑好骨骼的模型上掰出来，再按美术设定 6.5 检查不穿模。
- 尺寸是游戏里容器的真实内部空间（已含 1.25 倍放大），已经换算进了“脸在玩家胸口 / 眼睛高度”这类描述里。

**顺序建议：**先做 1-6 長持 和 1-2 洋服箪笥（数量多、最显眼），确认风格后再做其他 6 张。

### 1-1 押入れ 壁橱上层：“背面”
**画面：**拉开纸推拉门，她跪在上层、背对着你，湿发像帘子盖住后背。但她的头拧了 180°，脸从发帘中间露出来；两条手臂从肩膀上反折到背后，一只手像蜘蛛一样摊在自己背上，另一只伸出来扒住拉开的纸门边。（游戏里推开的那扇门叠在右半边，开口在左半边。）

```
IMAGE 1-1 — Oshiire, upper tier of a sliding-door closet: "Backwards"
Same character as the attached image A.
Format: 3:2 landscape.
Moment: one panel of an old paper fusuma closet door has just been slid open; the flashlight finds her on the upper tier, her face just above the viewer's eye level.
Set: a frame within the frame — the right half of the closet front is covered by both door panels, slid together one in front of the other: torn, water-stained paper on dark wooden lattices. Through the open left half: the bare upper shelf of grey, dusty planks running back into blackness; the empty lower tier a black void below.
Her (as the viewer sees her):
- She kneels on the upper shelf facing the back wall, so we see her back; her wet hair hangs down her back like a black curtain.
- IMPOSSIBLE: her head has turned a full 180° on her neck — her face looks straight out at us through a parting in that curtain of hair, where the back of her head should be; the skin of her neck is wrung into tight spiral creases.
- IMPOSSIBLE: both arms bend backwards over her shoulders, elbows pointing up at the shelf above. Her left hand lies spread across her own back, long fingers splayed like a spider. Her right hand reaches out of the closet and curls its fingers around the edge of the slid-open door panel, at the right side of the opening.
- Her kimono is wrung and twisted around her waist by the turn of her body.
Camera: viewer standing, 28 mm lens at eye level, tilted slightly up to her face; the mild wide-angle stretch makes the reaching hand feel close.
Light: the flashlight beam hits her face and the hand on the frame; the depth of the closet falls to black; faint blue moonlight from behind the viewer rims the paper door.
Mood: everything about her body says she is facing away — except her face.
Style: FILM.
```

### 1-2 洋服箪笥 西式衣柜：“倒挂”
**画面：**拉开高大衣柜的两扇门，她头朝下倒挂在柜顶，两脚蹬住顶角，膝盖像蜘蛛腿一样反折，两手撑住侧板。倒着的脸正好在你胸口高度。湿发笔直垂下，但和服不受重力、紧贴在身上——只有头发往下掉。

```
IMAGE 1-2 — Yōfuku-dansu, tall Western-style wardrobe: "Hanging"
Same character as the attached image A.
Format: 2:3 portrait.
Moment: both tall doors of an old dark-wood wardrobe have just been pulled open; the flashlight climbs the empty interior and finds her hanging from the top.
Set: a tall, deep, empty interior of dark varnished wood blistered with age; the two open doors frame the image on the left and right.
Her:
- IMPOSSIBLE: she hangs upside down from the ceiling of the wardrobe. Her bare feet are braced into the two top corners, toes curled around the edges.
- IMPOSSIBLE: her knees bend outward and backwards like a spider's legs, the kneecaps pushing up under stretched grey skin.
- Her arms reach down and out, palms pressed flat against the left and right side panels, elbows bent the wrong way.
- Her upside-down face hangs at the viewer's chest height, in the centre of the frame, staring straight into the lens.
- Her long wet hair falls straight down from her head toward the wardrobe floor like a black curtain.
- UNCANNY: her kimono does not fall — it clings to her body as if gravity does not touch the cloth; only her hair obeys gravity.
Camera: viewer standing at the open doors, 24 mm lens at eye level, framing the whole interior from her braced feet at the top to the ends of her hair below.
Light: the beam on her face and hands; the top corners where her feet grip lost in shadow; dust falling through the beam.
Mood: you opened the doors at the height of a person — and the person is upside down.
Style: FILM.
```

### 1-3 茶箪笥 餐具柜：“折进去”
**画面：**拉开餐具柜的两扇小门时有点阻力——是她在里面拉着门。她的两条腿从膝盖反折到背后，再从肩膀上翻过来，两只脏脚底贴在脸两边，脸夹在脚底中间；身体被压得薄得不像人；两只手还扒在两扇门的内侧。

```
IMAGE 1-3 — Cha-dansu, small tea cabinet: "Folded in"
Same character as the attached image A.
Format: 1:1 square.
Moment: the two small doors of a chest-high lacquered tea cabinet have just been pulled open — they resisted, because something inside was holding them.
Set: a shallow, cramped interior of dark lacquered wood, the shelves long gone, cracked black lacquer, tarnished iron fittings; the two small doors swung open toward the viewer at the left and right edges of the frame.
Her:
- She sits on the floor of the cabinet, compressed into its shallow depth.
- IMPOSSIBLE: both legs fold backwards at the knees, travel up behind her back and come over her shoulders, so her two dirty bare feet rest on either side of her face with the black soles turned toward us; her knees are hidden in the dark behind her head. Her face is wedged between her own soles.
- IMPOSSIBLE: her whole body is pressed unnaturally flat — no thicker than the cabinet allows, ribs showing through the damp kimono.
- Her arms stretch out wide: her long fingers are still hooked around the inner edges of the two open doors, left hand on the left door, right hand on the right door, as if she had only just let go.
- Her pale, milky eyes stare out between her feet, unblinking; a strand of wet hair lies across her dark lips.
Camera: viewer bending down, 35 mm lens about 1 m above the floor, looking slightly down into the cabinet.
Light: the beam lands on the soles and the face between them; the back of the cabinet is black.
Mood: she was holding the doors shut from the inside — and she let go.
Style: FILM.
```

### 1-4 下駄箱 鞋柜：“螳螂”
**画面：**拉开又高又窄的鞋柜门，层板都没了。她像昆虫一样撑在半空，手肘和膝盖全部反折，一只前臂像螳螂的前足一样垂着；脖子折断似的歪 90°，头躺在肩膀上，脸却正对你的眼睛。下面半截柜子是空的。

```
IMAGE 1-4 — Geta-bako, tall shoe cabinet: "Mantis"
Same character as the attached image A.
Format: 2:3 portrait.
Moment: the single narrow door of a tall old shoe cabinet swings open; the shelves are gone and the interior is a tall black slot — and she is up in it, at eye level.
Set: a narrow, tall interior of raw grey cedar with pale marks where shelves used to be; the open door at the right edge of the frame.
Her:
- She is suspended in the upper half of the slot, braced like an insect between the two side walls; beneath her ragged kimono hem the lower half of the cabinet is empty and black.
- IMPOSSIBLE: her knees and elbows all bend the wrong way. Her bare feet press flat against the side walls with the knees jutting up and out. Her left arm is raised on a reversed elbow and the forearm hangs down from it like a mantis's foreleg, wrist hooked, long fingers dangling.
- Her right arm reaches out of the cabinet and wraps its long fingers around the edge of the open door.
- IMPOSSIBLE: her neck is bent sideways a full 90°, as if broken, so her head lies on her left shoulder — yet her face is still turned to stare directly into the lens at the viewer's eye level. Her hair hangs sideways from her tilted head in one wet sheet.
Camera: viewer standing at the open door, 28 mm lens at eye level, her face just above the centre of the frame.
Light: the beam on her face, the hooked hand and the fingers on the door; her braced feet barely visible at the edges of the dark.
Mood: an insect waiting in its burrow, wearing a child's face.
Style: FILM.
```

### 1-5 箪笥 抽屉柜上层大抽屉：“压平”
**画面：**拉出最上层的大抽屉往下看。她仰面躺着，头顶朝你，所以你看到的是一张**倒着的脸**。整个人像信纸一样压平折成三层：大腿平压在胸口、膝盖在下巴两边，小腿再折回来压在上面；两条手臂越过头顶伸向你，肘关节反弯，手指扣在抽屉前沿——就在你刚才拉把手的地方。

```
IMAGE 1-5 — Tansu, top drawer of a paulownia chest of drawers: "Pressed flat"
Same character as the attached image A.
Format: 3:2 landscape.
Moment: the wide top drawer of a tall paulownia-wood chest has just been pulled out toward the viewer, who looks down into it.
Set: a shallow drawer, barely a hand deep, of pale, age-browned paulownia wood with a dark iron pull on its front; the dark front of the chest below it; the back of the drawer still inside the chest, in shadow.
Her:
- She lies on her back with the top of her head at the drawer's front edge, nearest the viewer — so we see her face upside down, her eyes rolled up toward us, staring.
- IMPOSSIBLE: her whole body is pressed flat as a letter and folded in three layers to fit a drawer barely a hand deep: her thighs lie flat on her chest so her knees sit on either side of her chin, and her shins are folded back flat on top of her thighs, her dirty soles facing up near her hips at the back of the drawer.
- IMPOSSIBLE: both arms stretch back past her head toward the viewer, elbows bent the wrong way, and her long fingers curl over the drawer's front edge right beside the iron pull — where the viewer's hand just was.
- Her wet hair fans out around her head across the drawer floor like black seaweed, a few strands spilling over the front edge.
Camera: viewer standing over the drawer, 35 mm lens looking down at about 50°, her upside-down face in the lower centre of the frame.
Light: the beam pours straight down onto her face and fingers; the folded legs fade into the shadow at the back of the drawer.
Mood: something folded and put away like clothing — and it is looking at you.
Style: FILM.
```

### 1-6 長持 大木箱：“拱桥”
**画面：**掀开又长又重的木箱盖往下看。她跪着、膝盖朝你，脊椎向后弯成不可能的拱，后脑勺搁在自己的脚后跟上，脸朝上越过自己的身体看着你；脊骨的骨节透过湿和服顶出来；两只手向后缠住自己的脚踝，脸被自己的手和脚框在中间。

```
IMAGE 1-6 — Nagamochi, large lidded storage chest: "Bridge"
Same character as the attached image A.
Format: 3:2 landscape.
Moment: the heavy lid of a long, dark wooden storage chest with iron corner fittings has just been heaved open; the viewer looks down into it.
Set: the long interior of a big old chest — dark cedar planks, black iron corner brackets, the thick lid standing open at the top of the frame; the chest is far longer than she is, and its far end disappears into black.
Her:
- She kneels in the chest facing the viewer, knees nearest to us.
- IMPOSSIBLE: her spine bends backwards into an arch no body could make, folding her in half the wrong way so the back of her head rests on her own heels; her upturned face looks back at us past the curve of her own body. The knots of her spine press up through the damp kimono.
- Her arms reach back and her long fingers wrap around her own ankles, right beside her face, so the face is framed by her own hands and feet.
- Her wet hair spreads across the floor of the chest around her head like spilled ink.
Camera: viewer standing at the long side of the chest, 24 mm lens looking steeply down at about 70°, so her face is clearly visible beyond the arch of her body and in sharp focus.
Light: the beam lands on her face; the arch of her body catches its edge; the rest of the chest is black.
Mood: she has held this shape in the dark for years, waiting for the lid.
Style: FILM.
```

### 1-7 葛籠 藤编箱：“倒进去的”
**画面：**掀开只有枕头大的藤编箱，她却整个人塞满了箱子，身体顺着四角变形，像是被倒进去凝固了。膝盖反折、脚贴在耳朵旁边；右臂拧到背后、再从左肩上绕出来扣住箱沿；左手像笼子一样摊在自己脸上，眼睛从指缝里瞪着你。

```
IMAGE 1-7 — Tsuzura, small lacquered wicker trunk: "Poured in"
Same character as the attached image A.
Format: 1:1 square.
Moment: the lid of a small, old lacquered wicker trunk — barely big enough for a pillow — has just been lifted; the viewer looks straight down into it.
Set: tightly woven wicker darkened and cracked with age, flaking black-brown lacquer, frayed edges; the lifted lid at the top edge of the frame.
Her:
- IMPOSSIBLE: she fills the little trunk completely, edge to edge, her body squeezed into its corners as if she had been poured in and set.
- IMPOSSIBLE: her knees bend the wrong way so her shins fold up along her body and her bare feet press against either side of her head, beside her ears.
- IMPOSSIBLE: her right arm twists behind her back and comes out again over her left shoulder; its long fingers hook over the wicker rim at the edge of the frame.
- Her left hand is spread over her own face like a cage, and her pale, milky eyes stare up at us between the long fingers.
- Her head is tipped back; her wet hair coils around her into the corners of the trunk.
Camera: viewer standing over the trunk, 35 mm lens looking straight down, the trunk filling most of the frame.
Light: the beam floods the trunk from above; the deepest corners stay black.
Mood: there should not be room for a person in there.
Style: FILM.
```

### 1-8 米びつ 米柜：“就在盖子下面”
**画面：**掀开米柜的盖子，她的脸就在盖子下面一掌宽的地方，正对着你。她跪着，脖子往后折到底，喉咙绷紧；两条手臂从背后反着伸上来，手指钩在边沿**下面**的内壁（不能扣在边沿上面，否则关盖会穿模）。

```
IMAGE 1-8 — Kome-bitsu, wooden rice bin: "Right under the lid"
Same character as the attached image A.
Format: 1:1 square.
Moment: the lid of a squat old wooden rice bin has just been lifted — and her face is barely a hand's width below where it was.
Set: thick, worn wooden walls, the inside rubbed smooth and pale; the lifted lid at the top edge of the frame.
Her:
- She kneels inside the bin.
- IMPOSSIBLE: her neck is bent back as far as it can go and further, her face pointing straight up only a hand's width below the rim, filling the centre of the frame; the skin of her throat is stretched taut.
- IMPOSSIBLE: both arms come up from behind her back with the shoulders turned the wrong way, and her long fingers hook under the lip of the rim from the inside, one hand on each side of her face. Her fingertips stay under the rim, never on top of it.
- Her wet hair hangs down from her tilted head into the dark below.
- Her mouth is slightly open; her eyes are already fixed on the viewer.
Camera: viewer standing over the bin, 35 mm lens looking straight down, her face very close to the lens and dead centre.
Light: the beam straight down onto her upturned face; her body vanishes into the dark depth of the bin.
Mood: she was pressed against the lid the whole time, listening.
Style: FILM.
```

---

## 5. 图 2-1 ~ 2-3：猎杀形态

美术设定里猎杀需要：展开出箱、四肢着地奔跑、直立奔跑。三张各对应一个。

### 2-1 出箱：“展开”
**画面：**猎杀开始。榻榻米房间里，长持的盖子自己抬起几厘米，一只手臂先伸出来扒住榻榻米；她的头和一边肩膀从窄得不可能的缝里挤出来；另一条手臂一节一节地掰回原位。

```
IMAGE 2-1 — The hunt begins: "Unfolding"
Same character as the attached image A.
Format: 3:2 landscape.
Moment: the hunt has begun. In a dark tatami room, the lid of a long wooden storage chest has lifted by itself a few centimetres, and she is unfolding out of it.
Scene: an abandoned old Japanese room at night — worn tatami, a torn shoji screen faintly glowing with blue moonlight behind; the chest against the wall in the middle distance.
Her:
- One arm has come out first and lies across the tatami toward the viewer, fingers spread and digging into the mat.
- Her head and one shoulder are squeezing out through a gap far too narrow for them; her neck is bent sideways to fit, her face turned toward the viewer, eyes locked on the lens.
- Her second arm is coming out joint by joint, the elbow still bent the wrong way, as if her bones are snapping back into place one by one.
- Her wet hair spills out of the gap and down the side of the chest onto the floor.
Camera: low, almost at tatami level, 24 mm lens, about 2 m from the chest; the reaching hand large in the foreground.
Light: an explorer's flashlight beam from behind the camera catches her face and hand; moonlight rims her hair.
Mood: the box was never what held her in.
Style: FILM.
```

### 2-2 四肢爬行：“比你快”
**画面：**昏暗的木走廊里，她压低身体、四肢着地像蜘蛛一样冲过来。手臂伸直、手肘反弯；膝盖往两侧反折、脚尖朝后；脖子伸得太长、歪着，脸却水平地盯着你；一只手朝镜头伸过来，近得有点虚焦。

```
IMAGE 2-2 — The hunt: "Crawling"
Same character as the attached image A.
Format: 3:2 landscape.
Scene: a long, dark wooden corridor of the old house at night, worn floorboards, sliding doors along one side; the flashlight beam comes from the viewer.
Her: rushing toward the viewer low on all fours, like a spider.
- Arms long and straight, elbows bent backwards, palms flat on the floorboards, fingers splayed. Her right hand reaches toward the lens in the foreground, so close its fingers are slightly out of focus.
- IMPOSSIBLE: her knees splay out to the sides and bend the wrong way; her feet are flat on the floor with the toes pointing backwards.
- Her back arches high; her neck is stretched too long and tilted about 30°, but her face stays level and stares straight at the viewer; pale, milky eyes wide and unblinking; dark lips open over small yellow teeth.
- Her wet hair hangs forward and sweeps the floor, half covering her face so one eye stares out between the strands; her torn kimono hem drags behind her.
- Slight motion blur on the trailing hair and hem; the face sharp.
Camera: 24 mm lens at the height of her head, close to the floor.
Light: the flashlight beam catches her face and reaching hand; the corridor behind her falls to black.
Mood: she is faster than you.
Style: FILM.
```

### 2-3 直立追捕：“不用跑”
**画面：**同一条走廊，她在手电光的尽头僵硬地走过来，踮着脚尖、像被线吊着；头折断似的歪 90° 躺在肩膀上，脸仍然对着你；一只手伸过来要抓你，另一只手肘反弯、无力地垂着。

```
IMAGE 2-3 — The hunt: "Walking"
Same character as the attached image A.
Format: 2:3 portrait.
Scene: the same dark corridor; moonlight through shoji paper on one side; she stands at the far edge of the flashlight beam and comes toward the viewer.
Her:
- She walks stiffly, body pitched forward, on the tips of her toes with her heels lifted, as if hung from strings.
- IMPOSSIBLE: her head hangs 90° onto her right shoulder, as if her neck were broken; her hair falls sideways in one wet sheet, yet her face is still turned to stare straight at the viewer, mouth open.
- Her left arm reaches toward the viewer, long fingers spread as if to grab.
- IMPOSSIBLE: her right arm hangs limp with the elbow bent backwards.
- Full body in frame, from the top of her head to her toes.
Camera: 50 mm lens at the explorer's eye level, looking slightly down at her.
Light: the beam on her face and reaching hand; moonlight rims her silhouette.
Mood: she isn't running, because she doesn't need to.
Style: FILM.
```

---

## 6. 交给 Tripo 与后续制作

| 图 | Tripo 设置 | 用途 |
|---|---|---|
| 图 0（裁成正 / 侧 / 背三张） | Multiview 转模型，面数 ≤ 30k，Quad remesh，导出 FBX，**勾选 Auto Rig** | 正式的游戏模型；所有姿势和动画都在它上面做 |
| 图 1-1 ~ 1-8 | 不交给 Tripo | 在图 0 模型上掰藏匿姿势的参考；放进容器后按美术设定 6.5 检查不穿模（离壁 ≥ 1.5 cm，抽屉叠起来 ≤ 0.33 m，米柜的手在边沿下面） |
| 图 2-1 ~ 2-3 | 不交给 Tripo | 出箱、爬行、直立追捕动画的参考 |

- 模型身高统一缩放到约 **1.2 m**（Unity 1 单位 = 1 米）。
- 头发：Tripo 出的是实心壳，正式版在 Blender 里用发片（hair cards）重做，补上盖脸的湿发。
- 乳白色的眼睛、皮肤脏污和折痕靠贴图，Tripo 的贴图不够细，头部贴图之后要手工加强（最高 4K）。
- 跳脸特写的脸（下巴脱臼、嘴角撕开）按美术设定 6.2 手工雕刻，不靠 Tripo。

**可选：静态占位模型。**某张参考图想先做个静态模型占位时，在那张图下面发这句，再把结果用 Tripo 单图转模型（不绑定）。姿势越扭曲，这一步越容易画崩，正式版仍以图 0 为准。
```
Redraw exactly this pose as a clean 3D reference of the character alone. Remove the container and every prop; keep the pose, proportions and every bent joint exactly as they are; full body visible with a margin around it; limbs separated wherever the pose allows. Style: MODEL.
```

---

## 7. 修图与补救

**局部修改，不要整张重画：**
- 在 ChatGPT 里点开图片，用选择工具圈出画错的地方（多半是手和脚），然后写：
  `Fix only the selected area: [要改成什么]. Keep everything else exactly as it is.`
- 整体只差一点时：
  `Keep everything exactly the same; only change: [要改的地方].`

**常见问题的补充说法：**

| 问题 | 发这句 |
|---|---|
| 脸跑偏了 | 附上图 A：`Her face has drifted. Match the attached image A exactly — same milky eyes, centre parting, wet fringe strands and dark lips.` |
| 太干净、像 AI 图 | `Make it rougher: underexpose by one stop, heavier grain and grime, imperfect handheld framing.` |
| 太可爱 | `She must not look cute: gaunter cheeks, darker sunken eye sockets, dirtier skin, an emptier stare.` |
| 头发变整齐 | `Her hair must be longer, wetter and more tangled — thick matted strands of very uneven length, some stuck to her face, no clean lines anywhere.` |
| 四肢画乱、多了手脚 | `Hide her [left leg / right arm] in the darkness of the container.`，或者把那一处 IMPOSSIBLE 删掉再画 |
| 想要封面那种插画感 | 把结尾的 `Style: FILM.` 换成 `Style: POSTER.` |

**被拒绝时：**
1. 先补一句：`This is a supernatural yūrei in an original horror game. There is no blood or injury, and no real child is depicted.`
2. 还是拒绝，就把那条里的 “bends backwards / folded” 改成 “bends at an unnatural angle, like a marionette”。

**每张图的检查清单：**
- [ ] 长相和图 A 一致：中分的湿长发和碎刘海、乳白色的眼睛、纸白的脸、暗红的嘴唇、深红和服、细长手指
- [ ] 头发不是齐刘海 / 妹妹头 / 人偶头
- [ ] 每条肢体都能从关节到关节看清，没有多出来的手脚
- [ ] 脸朝着开口，眼睛对着镜头
- [ ] 没有血、伤口、文字、水印
- [ ] 图 0：三视图比例一致，灰底，平光，手指分开
