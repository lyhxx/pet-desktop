# 开发文档

## 1. 技术栈与环境

| 项 | 值 |
|---|---|
| 语言 | C# |
| 框架 | .NET 10（`net10.0-windows`） |
| UI | WPF（+ 少量 WinForms 仅用于托盘 `NotifyIcon`） |
| 存档 | `System.Text.Json` |
| 平台 | Windows 10 / 11（x64） |
| 依赖 | 无第三方库 |

构建 / 运行：

```bash
dotnet build
dotnet run
```

## 2. 项目结构

```
Core/        核心逻辑，不依赖任何 WPF 控件
  BehaviorState.cs   行为枚举
  PetState.cs        养成数值与衰减 / 离线结算
  PetBehavior.cs     行为系统（加权随机 + 需求影响）
  PetController.cs   协调行为 / 状态 / 视图
  IPetView.cs        核心与 UI 的边界
  Food.cs            食物定义
  EffectKind.cs      粒子效果类型
  OutfitState.cs     装扮选择
  PetSession.cs      一次运行的会话数据
Animation/   精灵图集与动画播放
  SpriteSheet.cs     固定网格切分 + 帧缓存
  AnimationClip.cs   一段动画的播放定义
  AnimationLibrary.cs 读取 animations.json，按需加载图集
  PetAnimation.cs    由游戏循环驱动的时间轴播放器
UI/          窗口与主题
  Theme.xaml         暖色圆角主题（按钮 / 复选框 / 下拉 / 滑杆 / 进度条 / ToolTip）
  PetWindow.*        宠物窗口（透明 / 拖动 / 悬停 UI / 逐像素命中）
  PetMenuWindow.*    右侧弧形菜单（含食物子弧）
  StatusWindow.*     头顶迷你状态（图标 + 百分比，鼠标穿透）
  SettingsWindow.*   设置
  OutfitWindow.*     装扮
  EffectLayer.cs     独立粒子层
Platform/    平台能力
  AppInfo.cs         版本号 + GitHub 仓库配置
  UpdateService.cs   GitHub Releases 检查更新
  TrayManager.cs     系统托盘
  StartupManager.cs  开机启动（HKCU Run）
  SoundManager.cs    运行时合成音效
  WindowManager.cs   跨屏虚拟桌面边界 + DPI
  AppSettings.cs     程序配置
Save/        本地 JSON 存档
  PetSaveData.cs     存档结构 + 离线结算
  SaveManager.cs     读写
Assets/      图标与图集
  app.ico / app-icon.png
  Pet/Pet_Idle.png      猫图集
  Pet/animations.json   动画表
installer/   Inno Setup 脚本
tools/       SpriteSlicer 切图工具
```

## 3. 架构与数据流

核心逻辑与 WPF UI 分离：`PetBehavior` / `PetController` 只通过 `IPetView` 驱动界面，不直接依赖控件。

```
PetController
  ├─ PetBehavior   （自主行为，纯逻辑）
  ├─ PetState      （数值）
  └─ IPetView ──► PetWindow（实现）──► PetAnimation ──► SpriteSheet
                                     └► EffectLayer（独立粒子层，与状态机解耦）
```

单一时钟：`PetWindow` 用一个 `DispatcherTimer`（约 60fps）驱动 `PetController.Tick(dt)` 与 `PetAnimation.Tick(dt)`，避免多 Timer 漂移。

## 4. 精灵图规范（重要）

### 4.1 画布与网格

| 项 | 标准值 |
|---|---|
| 图集尺寸 | 2048 × 2048 |
| 网格 | 8 × 8（共 64 格） |
| 单格 | 256 × 256 |
| 背景 | 真正透明（RGBA） |

- 每个格子是一帧，**角色不得跨越格子边界**。
- 不需要填满 64 格，空帧保持透明即可。
- 程序按 `像素宽 / 8` 动态计算格宽，所以其它尺寸也能读；但**标准一律用 2048**，避免混乱。

### 4.2 统一锚点

- 每格中，**双脚落地点中心**统一为 **(X = 128, Y = 236)**（格内像素坐标）。
- 站立 / 待机 / 行走等动作双脚都落在这个点附近。
- 程序放置窗口时，把该锚点对齐到桌面的宠物位置。

### 4.3 朝向

- 美术**只制作朝右方向**（3/4 正面朝右）。
- 朝左由程序水平镜像（`ScaleX = -1`），**不要画左右两套**。

### 4.4 透明背景清理

AI 生成的图常见两类问题，切图时必须清理：

1. **低 alpha 噪点**：背景残留 alpha 6~40 的杂点 → 将 `alpha < 40` 的像素置为全透明。
2. **网格辅助线**：AI 有时会画出淡淡的格线（alpha 40~80），会在每格四边留下边框线 → 在每个格子边界清除 8px 宽的 band。

### 4.5 锚点归一化（关键，防抖动）

AI 生成的每帧角色位置往往不一致，直接播放会左右滑动、忽大忽小。切图时对**每帧**做锚点归一化：

1. 计算该帧不透明像素的包围盒。
2. 取包围盒**底部 15% 区域的水平中心**作为脚部中心 X（比用整框中心更稳，避开尾巴摆动）。
3. 取包围盒底部作为 Y。
4. 平移整帧内容，使 `(脚部中心X, 底部Y)` → `(128, 236)`。

> 这一步会改变角色在格内的位置，但**不裁剪、不缩放**，仍是 256×256 的完整网格。

### 4.6 帧号与动画表

- 帧号 **1-based**，顺序为"从左到右、从上到下"：第 1 行 01~08，第 2 行 09~16……
- 播放顺序完全由 `Assets/Pet/animations.json` 指定，程序不自行判断。

```json
{
  "grid": { "columns": 8, "rows": 8, "cellWidth": 256, "cellHeight": 256, "anchorX": 128, "anchorY": 236 },
  "sheets": { "idle": "Pet_Idle.png", "move": "Pet_Move.png" },
  "animations": {
    "Idle": { "sheet": "idle", "frames": [1,2,3,4,5,6,9,10,11,12,13,14], "fps": 6, "loop": true }
  }
}
```

- `sheet`：`sheets` 里的键；图集文件放在 `Assets/Pet/`。
- `frames`：1-based 帧号数组。
- `fps`：帧率。
- `loop`：是否循环；不循环的短动作播完停在最后一帧。

### 4.7 Pet_Idle 动作映射

| 动作 | 帧号 | fps | 循环 |
|---|---|---|---|
| 待机 | 01-06, 09-14 | 6 | 是 |
| 眨眼 | 07-08 | 6 | 否 |
| 东张西望 | 15-20 | 6 | 否 |
| 伸懒腰 | 21-26 | 5 | 否 |
| 整理毛发 | 27-32 | 5 | 否 |
| 好奇待机 | 33-38 | 5 | 否 |
| 尾巴动作 | 39-44 | 6 | 否 |
| 轻微开心 | 45-48 | 5 | 否 |
| 发呆 | 49-54 | 4 | 是 |
| 小动作 | 55-60 | 5 | 否 |
| 特殊待机 | 61-64 | 5 | 否 |

后续图集（`Pet_Move` / `Pet_Action` / `Pet_EatSleep`）同规格，只需在 `animations.json` 增加对应 `sheet` 与动画即可；缺失的图集程序会**回退到 Idle**，不会切回占位形象。

## 5. 切图工具 SpriteSlicer

位于 `tools/SpriteSlicer/`，三种模式：

```bash
# 1) 切图：输入 → 处理后图集 + 64 帧 + 预览
dotnet run --project tools/SpriteSlicer -- <input.png> Assets/Pet/Pet_Idle.png <framesDir>

# 2) 测量：打印每格包围盒（脚底Y / 水平中心 / 宽高），用于验证对齐
dotnet run --project tools/SpriteSlicer -- measure Assets/Pet/Pet_Idle.png

# 3) 生成图标：多尺寸 .ico
dotnet run --project tools/SpriteSlicer -- makeico Assets/app-icon.png Assets/app.ico
```

切图流程（`NormalizeAnchors` 等）依次执行：**缩放到 2048×2048 → 清理低 alpha 噪点 → 清除格边界线 → 锚点归一化 → 保存图集 → 导出 64 帧 + 预览**。

### 5.1 图片尺寸不一样怎么办（重点）

- **输入是任意正方形都能用**：工具会先 `HighQualityBicubic` 缩放到 2048×2048，再按 8×8 切。
  - 例：微信压缩后的 1254×1254 也能处理（先放大回 2048，再切）。
- **必须是 8×8 网格**。如果你的图是别的网格（例如 6×6），改 `tools/SpriteSlicer/Program.cs` 顶部常量：

  ```csharp
  const int Grid = 8;   // 改成你的网格数
  const int Cell = 256;
  const int Size = 2048;
  ```

- **输入必须接近正方形**：非正方形会被拉伸变形，请重新生成正方形图。
- **最佳做法**：直接生成 **2048×2048** 原图，并且**不要用微信等聊天软件传输**（会压缩 / 降分辨率 / 破坏透明通道），用文件方式或网盘传原图。

### 5.2 新增一张图集的步骤

1. 按 4.1~4.3 的规格生成 2048×2048、8×8、朝右、透明背景的 Sprite Sheet。
2. 用 SpriteSlicer 切图，输出到 `Assets/Pet/<名称>.png`；用 `measure` 确认每帧脚底 Y 一致。
3. 在 `Assets/Pet/animations.json` 的 `sheets` 增加键，在 `animations` 增加对应动画（帧号按图内容）。
4. 若该动作对应某个行为状态，在 `Core/PetController.cs` 的 `ClipFor` 里映射。
5. 运行验证。

## 6. 状态系统

| 数值 | 变化规则 |
|---|---|
| 饥饿 | `+4/小时`；喂食降低 |
| 心情 | 抚摸 / 喂食提升；饥饿 > 70 时 `-1.2/小时` |
| 精力 | 醒着 `-3/小时`；睡觉 `+18/小时` |
| 健康 | 饥饿 > 90 或精力 < 5 时 `-1.5/小时`，否则 `+1/小时`；下限 20，**永不死亡** |

- 界面显示的是**饱食度 = 100 − 饥饿**（喂食往上涨，100% 表示吃饱）。
- 离线结算 `ApplyOffline`：按经过时间计算，封顶 72 小时，假设离线期间多在休息。

## 7. 行为系统

`PetBehavior` 每个 tick 用**加权随机 + 需求影响**挑选行为，而非固定时间轴：

- 精力越低 → 越可能睡觉、坐下
- 精力越高（alertness）→ 越可能出现张望 / 好奇 / 尾巴 / 理毛 / 小动作 / 伸懒腰 / 特殊待机
- 走动有 **10~24 秒冷却**，单次距离 60~240px，走完安静待机数秒（避免一直来回走）
- 互动时用 `OverrideState` 强制进入吃东西 / 被摸等状态，结束后回到自主行为
- 悬停菜单打开时 `Autonomous = false`，暂停自主走动但互动照常进行

## 8. 存档

- 位置：`%AppData%\DesktopPet\save.json`
- 内容：名称、位置、四项数值、装扮、程序设置、最后保存时间
- 时机：状态变更、拖动结束、每分钟自动保存、退出时
- 启动时读取并做离线结算

## 9. 版本与更新

- 版本号在 `DesktopPet.csproj` 的 `<Version>`（当前 `1.0.0`），运行时由 `Platform/AppInfo.cs` 读取。
- CI 发布时会用 git tag（去掉 `v`）覆盖版本号。
- 检查更新由 `Platform/UpdateService.cs` 查询 GitHub Releases 的 `latest`，与当前版本比较。
  - 仓库地址在 `Platform/AppInfo.cs`（当前 `lyhxx/pet-desktop`），留空则跳过检查。
  - 启动后延迟 6 秒自动检查；也可在设置窗口或托盘菜单手动检查。
  - 发现新版本时托盘气泡提示，点击打开 Release 页。

## 10. 构建与发布

```bash
# 自包含发布
dotnet publish DesktopPet.csproj -c Release -r win-x64 --self-contained true -o publish
```

GitHub Actions：

- `.github/workflows/build.yml`：push / PR 自动编译
- `.github/workflows/release.yml`：打 `v*` tag 时自动发布绿色版 zip + Inno Setup 安装包

安装包脚本 `installer/DesktopPet.iss`，版本号可由 CI 通过 `/DMyAppVersion=` 注入。
