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

重复运行第 2 步会重建场景，但**不会覆盖**已存在的材质、预制体和后处理配置。要重建容器预制体，用“1b 强制重建”。

## 生成内容

| 内容 | 位置 |
|---|---|
| 场景 | `Scenes/House_Blockout.unity` |
| 16 个容器预制体（8 种 × 2 变体） | `Prefabs/Containers/` |
| 秘密出口暗门预制体 | `Prefabs/SecretExit_Hatch.prefab` |
| 灰盒材质 | `Materials/Placeholder/` |
| 后处理配置（常态 / 猎杀） | `Settings/VP_Normal.asset`、`VP_Hunt.asset` |

场景规模：两层主屋（20 × 13 m）+ 土蔵 + 围墙庭院，共 **45 个容器**、**4 个秘密出口候选点**、**2 座楼梯**。

## 给程序（GPT）的接口

场景中所有对象按名称查找，层级如下：

```
House_Blockout
├─ Structure          静态墙地板，已设为 Static
├─ Containers         45 个容器实例，命名 <房间>_<类型>_<序号>
├─ Gameplay
│   ├─ ExplorerSpawnPoints/SpawnPoint_0..4
│   ├─ SecretExitCandidates/SecretExitCandidate_i_<房间>/{Carpet, HatchAnchor}
│   ├─ Safe_Kinko/{InteractPoint, KeyAnchor}
│   ├─ MemorialPoint_Butsudan/InteractPoint     （超度地点，具体超度操作待定）
│   ├─ Gate_Main/{GateDoor_L, GateDoor_R, InteractPoint}
│   └─ EscapeZone_Gate
├─ Props
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
- 室内门洞是空的，没有襖或障子门。
- 地板全部在 y = 0，没有做日式住宅抬高的木地板和玄关台阶，避免灰盒阶段增加台阶碰撞问题。
- 屋顶是平的。
- 楼梯用不可见的斜坡碰撞体行走，台阶模型本身没有碰撞。
- 手电筒是否存在是待定项，所以场景里只有月光，很暗。灰盒测试时如果看不清，可以临时调高 `Moonlight` 强度或 `RenderSettings` 的环境光。
