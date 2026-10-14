# 守住圆环 · HOLD THE CIRCLE

**守住灯火，迎战幽影。**

一款面向 PICO 的 VR/MR 手势施法 Roguelike 游戏，基于 Unity 6 与 URP 开发。右手挥斩释放能量刃，左手引导连锁闪电，在前方幽影来袭时守护脚下的灯印。选择路线、收集遗物、组合能力，最终迎战双生守卫。

HOLD THE CIRCLE is a hand-tracking VR/MR roguelike for PICO. Cast energy blades with your right hand, guide chain lightning with your left, and defend the lantern circle through branching encounters and a twin-sentinel boss fight.


## 游戏特色

- **双手分工**：右手负责挥斩与爆发攻击，左手负责电链与多目标牵制。
- **守圈压力**：敌人侵入会削弱圆环稳定性，玩家需要兼顾生命、专注与灯印状态。
- **单局构筑**：六层路线串联战斗、精英、遗物、商店与事件，专精和遗物改变能力组合。
- **双生守卫**：最终挑战包含双 Boss、阶段变化和目标切换。
- **自然交互**：注视配合捏合完成菜单选择；眼动不可用时回退到头部方向瞄准。
- **VR / MR 切换**：在靛蓝夜空和现实房间透视背景之间切换。
- **三种入口**：引导教学、常规挑战、无尽模式；支持简体中文和英文界面。

## 开发环境

| 项目 | 配置 |
| --- | --- |
| 开发系统 | Windows |
| Unity | **6000.3.14f1** |
| 渲染管线 | URP **17.3.0** |
| PICO SDK | **PICO Unity SDK 6.0.0**，需单独安装 |
| XR 模式 | **PICO XR**|
| 主要依赖 | XR Hands、XR Interaction Toolkit、Input System、Localization、Addressables |
| 构建目标 | Android，IL2CPP，ARM64 |
| 主要输入 | 手部追踪；注视或头部瞄准配合捏合 |

头显型号和系统版本需满足 PICO SDK 对手部追踪与 Passthrough 的要求。眼动追踪仅在支持该能力的设备与系统上启用。

## 快速开始

1. 安装 **Unity 6000.3.14f1**，同时安装 **Android Build Support、Android SDK & NDK Tools、OpenJDK**。
2. 打开 `Assets/Scenes/SampleScene.unity`。玩法系统与美术、UI 加载由代码驱动，无需逐个绑定 Inspector。
3. 在 **Edit → Project Settings → XR Plug-in Management** 的 Android 页签启用 **PICO**，检查场景中的 `PXR_Manager`，运行 PICO **Project Validation**。
4. 在 Editor 中进入 Play Mode 查看大厅、玩法与动画；完整手势交互和 MR 合成需在头显上体验。

AI 生成服务用于资源制作，不是启动或运行游戏的依赖。运行时 FBX、材质、动画和 prefab 随分享源码提供，正常运行无需重新生成模型。

## 构建到头显

1. 在 **File → Build Profiles** 中选择 Android，并切换到该平台。
2. 将 `Assets/Scenes/SampleScene.unity` 加入场景列表。
3. 在 **Player Settings → Other Settings** 中确认 **Scripting Backend = IL2CPP**、**Target Architectures = ARM64**，并通过 PICO Project Validation 检查图形和 XR 配置。
4. 连接已启用开发者模式和 USB 调试的 PICO 头显，在 **Run Device** 中选择设备。
5. 执行 **Build And Run**。佩戴头显并启用手部追踪后，按照游戏内教学操作。

## 操作方式

| 操作 | 方式 |
| --- | --- |
| 能量刃 | 右手挥斩，按教学提示完成动作 |
| 连锁闪电 | 左手引导电链瞄准敌人 |
| 选择菜单项 | 注视按钮，任意一只手捏合 |
| 确认选择 | 松开捏合，注视 `CONFIRM / 确认`，再次捏合 |
| 无眼动设备 | 使用头部方向瞄准，配合相同的捏合流程 |
| 语言和背景 | 在设置中切换简体中文 / English、夜空 / MR；设置会保存 |

## 工程结构

```text
Assets/
  Scenes/                 主场景
  Game/
    Scripts/              战斗、圆环、路线、遗物、Boss、菜单与输入
    Resources/            运行时加载的 UI、美术与敌人 prefab
    Models/               模型、动画源文件与制作记录
    Localization/         中英文文本与字体
    Relics/               遗物配置
    Specializations/      专精配置
  Editor/                 资源制作与 Editor 验证工具
Packages/                 UPM 配置；PICO SDK 单独安装
ProjectSettings/          Unity 工程配置
Tools/                    美术制作和公开源码导出脚本
Docs/                     设计、资源来源、验证记录与宣传素材
```

## 美术与许可

Warden / Bulwark 敌人模型和部分动作由腾讯云混元服务生成；项目提供自制骨架、蒙皮处理、PBR 材质配置、LOD 与动画接入。`Tools/Art/rig_lantern_guardians.py` 保留 Blender 制作流程，重新制作时需提供源 GLB。

**本项目的原创代码与原创文档采用 [MIT License](LICENSE)**，包括玩法脚本、自制着色器和工具代码。第三方代码、SDK、模型、贴图、字体和示例资源不因仓库根目录的 MIT 文件而被重新许可

