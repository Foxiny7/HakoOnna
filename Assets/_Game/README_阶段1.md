# 阶段 1 本机联机验收

连接地址为 `127.0.0.1:7777`。本阶段使用探索者灰盒角色与老宅的 5 个出生点，最多接入 5 名测试玩家。联机测试会临时提高环境补光，方便检查移动。移动由服务器执行，玩家位置通过 Netcode 同步。

## 主机模式

1. Unity 菜单 **Hako → Playtest → 1 Create local multiplayer scene** 打开启动场景。
2. 用 **2 Build Windows player** 生成测试游戏。
3. 在 Unity 点 Play，然后点游戏画面的 **Host on this PC**。
4. 用菜单 **3 Start extra local client** 启动第二个玩家。
5. 切换两个窗口，用 WASD 移动、Shift 奔跑、鼠标转向。按 Esc 释放鼠标，用 **Capture mouse** 再次捕获。

两边应显示 `Players visible: 2`，转向后能看到对方的胶囊角色。移动应相互同步，墙体应阻挡玩家。每个窗口显示自己的手臂。

## 独立服务器模式

1. 结束主机测试并关闭额外的客户端。
2. 用 **4 Start local dedicated server** 在本机启动后台服务器。
3. 在 Unity 点 Play，再点 **Join 127.0.0.1**。
4. 用 **3 Start extra local client** 加入第二名玩家，重复移动验收。
5. 结束时退出客户端，并用 **5 Stop local dedicated server** 停止这一编辑器会话启动的服务器。

两种模式共用一个 Windows 程序，输出在 `Builds/PhaseOne/HakoOnna.exe`。可通过参数 `-hakoMode host`、`-hakoMode client` 或 `-batchmode -nographics -hakoMode server` 启动。

## 自动检查

从项目根目录运行以下命令，执行前结束正在运行的联机房间。脚本会启动测试进程、通过正常输入 RPC 驱动玩家，核对服务器移动、墙体碰撞和每个客户端的位置，并在结束后关闭测试进程。报告保存在 `Temp/network-*`。

```powershell
powershell -File Assets/_Game/Tests/RunLocalNetwork.ps1 -Mode host
powershell -File Assets/_Game/Tests/RunLocalNetwork.ps1 -Mode server
```

`GameConfig.asset` 中的阶段 1 占位值为步行 3 米/秒、奔跑 5 米/秒、鼠标灵敏度 0.1。角色高 1.65 米、半径 0.22 米、视点高 1.45 米，也可在该配置中调整。测试运行后检查 Console 错误数为 0。
