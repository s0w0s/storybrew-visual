# storybrew Code Wiki

> **storybrew** 是一个 osu! storyboard 编辑器，面向"不想和 Ctrl-L 快捷键建立亲密关系"的玩家：当代码或贴图被保存时，编辑器会立即在画布上反映变化。
>
> 本文档基于源码静态分析生成，覆盖项目整体架构、各模块职责、关键类与函数说明、依赖关系以及运行方式。

---

## 目录

1. [项目概览](#1-项目概览)
2. [解决方案结构](#2-解决方案结构)
3. [整体架构](#3-整体架构)
4. [模块详解](#4-模块详解)
   - 4.1 [`common` — 共享领域模型](#41-common--共享领域模型)
   - 4.2 [`editor` — GUI 编辑器](#42-editor--gui-编辑器)
   - 4.3 [`scripts` — 内置示例脚本](#43-scripts--内置示例脚本)
   - 4.4 [`test` — 单元测试](#44-test--单元测试)
   - 4.5 [`brewlib` — 底层框架（子模块）](#45-brewlib--底层框架子模块)
5. [关键流程](#5-关键流程)
6. [依赖关系](#6-依赖关系)
7. [项目运行方式](#7-项目运行方式)
8. [附录](#8-附录)

---

## 1. 项目概览

| 项目元信息 | 值 |
| --- | --- |
| 名称 | storybrew editor |
| 仓库 | `Damnae/storybrew` |
| 类型 | Windows 桌面应用（osu! storyboard 编辑器） |
| 目标框架 | .NET 8.0 (`net8.0-windows`) |
| 平台目标 | x86（为支持 BASS 音频库） |
| 主语言 | C# |
| 解决方案文件 | [storybrew.sln](file:///workspace/storybrew.sln) |
| 许可证 | 见 [LICENSE](file:///workspace/LICENSE) |

**核心特性**

- 实时预览：保存脚本/贴图后立即在画布看到变化。
- C# 脚本编写 storyboard，通过 Roslyn 在进程内编译、热重载（collectible `AssemblyLoadContext`）。
- 通过 `[Configurable]` 特性把脚本字段暴露到编辑器 UI。
- 支持 FFT 音频分析、字幕/字体生成、3D 场景投影、曲线计算等高级能力。
- 可导出 `.osb` 与按难度写入 `.osu` 的 `[Events]` 段。

---

## 2. 解决方案结构

`storybrew.sln` 包含 5 个项目（其中 `brewlib` 为 git 子模块）：

| 项目 | 输出类型 | 程序集名 | 命名空间 | 路径 |
| --- | --- | --- | --- | --- |
| `common` | Library | `StorybrewCommon` | `StorybrewCommon` | [common/](file:///workspace/common) |
| `editor` | WinExe | `StorybrewEditor` | `StorybrewEditor` | [editor/](file:///workspace/editor) |
| `scripts` | Library | — | `StorybrewScripts` | [scripts/](file:///workspace/scripts) |
| `test` | Library (MSTest) | `Test` | `Test` | [test/](file:///workspace/test) |
| `brewlib` | Library | `BrewLib` | `BrewLib` | [brewlib/](file:///workspace/brewlib)（子模块，未检出） |

> `brewlib` 通过 [.gitmodules](file:///workspace/.gitmodules) 引入：`url = https://github.com/Damnae/brewlib.git`。它提供 OpenGL 渲染、UI widget、音频、screen-layer 框架等底层能力。本仓库的 `common`、`editor`、`test` 均引用它。

### 顶层目录树

```
/workspace
├── common/         # 共享领域模型（脚本与编辑器共用）
├── editor/         # GUI 编辑器主程序
├── scripts/        # 内置示例 storyboard 脚本
├── test/           # MSTest 单元测试
├── brewlib/        # 子模块（底层框架，需 git submodule update）
├── storybrew.sln
├── README.md
├── LICENSE
├── .editorconfig
├── .gitignore
└── .gitmodules
```

---

## 3. 整体架构

storybrew 采用经典的"领域模型 + 编辑器宿主 + 用户脚本"三层架构：

```
┌─────────────────────────────────────────────────────────────┐
│                      editor (StorybrewEditor)               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │ ScreenLayers │  │ UserInterface│  │  Storyboarding   │  │
│  │ (菜单/项目页) │  │ (Widget/皮肤)│  │  (Project/Effect)│  │
│  └──────┬───────┘  └──────┬───────┘  └────────┬─────────┘  │
│         │                 │                   │            │
│         └────────┬────────┴───────────────────┘            │
│                  ▼                                          │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Scripting (Roslyn 编译 + AssemblyLoadContext 热重载)  │  │
│  │ Mapset (EditorBeatmap .osu 解析 + FileSystemWatcher) │  │
│  │ Util (AsyncActionQueue, NetHelper, OsuHelper ...)    │  │
│  └──────────────────────┬───────────────────────────────┘  │
└─────────────────────────┼───────────────────────────────────┘
                          │ 引用
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                  common (StorybrewCommon)                   │
│  Storyboarding  │  Storyboarding3d  │  Mapset  │  Curves   │
│  Animations     │  Subtitles        │  Scripting│  Util    │
└─────────────────────────┬───────────────────────────────────┘
                          │ 引用
                          ▼
┌─────────────────────────────────────────────────────────────┐
│              brewlib (BrewLib) — 子模块                     │
│  Graphics (OpenGL) │ Audio (BASS) │ UserInterface │ ...    │
└─────────────────────────────────────────────────────────────┘
```

**关键设计原则**

- **领域模型与宿主解耦**：`common` 不依赖 `editor`，可被用户脚本独立引用；编辑器在 `common` 类型之上构建 `Editor*` 派生类（如 `EditorOsbSprite : OsbSprite`）。
- **脚本热重载**：用户脚本编译为独立 collectible 程序集，文件变更触发重新编译并原子替换 storyboard 图层。
- **单线程主循环 + 后台 worker**：所有 GL/UI 操作在主线程，effect 更新在 `AsyncActionQueue<Effect>` 的多线程池上执行，结果通过 `Program.Schedule` 回主线程。
- **osu!stable 兼容**：命令解析、重叠优先级、显示时间计算等严格复刻 osu!stable 的（有时反直觉的）行为，单元测试专门锁定这些边界。

---

## 4. 模块详解

### 4.1 `common` — 共享领域模型

命名空间 `StorybrewCommon`，输出 `StorybrewCommon.dll`。被 `editor`、`scripts`、`test` 引用。依赖 `brewlib`、`OpenTK 2.0`、`System.Drawing.Common 8.0.8`、`System.ValueTuple 4.5.0`、`Damnae.Tiny 1.2.0`（YAML 序列化）。

#### 4.1.1 `Storyboarding/` — osu! storyboard 对象模型

脚本通过 `StoryboardObjectGenerator` 创建 `OsbSprite`/`OsbAnimation`/`OsbSample`，挂载 `ICommand`，最终导出为 `.osb` 文本。

| 文件 | 关键类/类型 | 职责 |
| --- | --- | --- |
| [StoryboardObject.cs](file:///workspace/common/Storyboarding/StoryboardObject.cs) | `StoryboardObject` (abstract) | 所有可写入 `.osb` 对象的基类：`StartTime`/`EndTime`、`WriteOsb(...)` |
| [OsbSprite.cs](file:///workspace/common/Storyboarding/OsbSprite.cs) | `OsbSprite` | **核心类**。一个 storyboard 精灵及其全部命令。提供 `Move/MoveX/MoveY/Scale/ScaleVec/Rotate/Fade/Color/ColorHsb/Parameter` 等命令方法、`StartLoopGroup`/`StartTriggerGroup`/`EndGroup` 分组、`PositionAt/ScaleAt/...` 显示查询 API、`WriteOsb` 导出。定义枚举 `OsbLayer`、`OsbOrigin`、`OsbLoopType`、`OsbEasing`（35 种）、`ParameterType` |
| [OsbAnimation.cs](file:///workspace/common/Storyboarding/OsbAnimation.cs) | `OsbAnimation : OsbSprite` | 帧动画精灵。`FrameCount`/`FrameDelay`/`LoopType`；`GetFrameAt(time)`、`GetTexturePathAt(time)` 在文件名前插入帧索引 |
| [StoryboardSegment.cs](file:///workspace/common/Storyboarding/StoryboardSegment.cs) | `StoryboardSegment` (abstract) | 可变换的容器组：`Origin`/`Position`/`Rotation`/`Scale`/`ReverseDepth`；工厂 `CreateSprite/CreateAnimation/CreateSample/CreateSegment/GetSegment/Discard` |
| [StoryboardLayer.cs](file:///workspace/common/Storyboarding/StoryboardLayer.cs) | `StoryboardLayer` (abstract) | 带 `Identifier` 的 `StoryboardSegment`，具体实现位于 editor |
| [GeneratorContext.cs](file:///workspace/common/Storyboarding/GeneratorContext.cs) | `GeneratorContext` (abstract) | 传给脚本的服务提供者：路径、`Beatmap`/`Beatmaps`、`GetLayer`、`AudioDuration`/`GetFft`/`GetFftFrequency`、`AddDependency`、`CancellationToken`、`Multithreaded` |
| [EffectConfig.cs](file:///workspace/common/Storyboarding/EffectConfig.cs) | `EffectConfig` | 脚本可配置字段的键值存储（`Dictionary<string,ConfigField>`）。`UpdateField`/`SetValue`/`GetValue`/`SortedFields` |
| [ExportSettings.cs](file:///workspace/common/Storyboarding/ExportSettings.cs) | `ExportSettings` | 导出选项：`UseFloatForMove`、`UseFloatForTime`、`OptimiseSprites`、`NumberFormat` |
| [StoryboardTransform.cs](file:///workspace/common/Storyboarding/StoryboardTransform.cs) | `StoryboardTransform` | 由父变换 + origin/position/rotation/scale 组合的 2D `Affine2`；`ApplyToPosition/ApplyToPositionXY/ApplyToRotation/ApplyToScale` |
| [OsbSample.cs](file:///workspace/common/Storyboarding/OsbSample.cs) | `OsbSample` | 声音样本：`AudioPath`/`Time`/`Volume`，导出 `Sample,<time>,<layer>,"<path>",<volume>` |
| [ConfigurableAttribute.cs](file:///workspace/common/Storyboarding/ConfigurableAttribute.cs) | `[Configurable]` | 标记字段为用户可配置 |
| [DescriptionAttribute.cs](file:///workspace/common/Storyboarding/DescriptionAttribute.cs) | `[Description]` | 字段 tooltip |
| [GroupAttribute.cs](file:///workspace/common/Storyboarding/GroupAttribute.cs) | `[Group]` | 字段分组 |

##### `Storyboarding/Commands/` — 命令模式

实现 osu! storyboard 命令（`F`/`M`/`MX`/`MY`/`S`/`V`/`R`/`C`/`P` 与 `L`/`T` 分组）。

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [ICommand.cs](file:///workspace/common/Storyboarding/Commands/ICommand.cs) | `ICommand : IComparable<ICommand>` | `StartTime`/`EndTime`/`Cost`/`IsFragmentableAt(time)`/`WriteOsb(...)` |
| [ITypedCommand.cs](file:///workspace/common/Storyboarding/Commands/ITypedCommand.cs) | `ITypedCommand<TValue>` | 增加 `Easing`/`StartValue`/`EndValue`/`Duration`/`ValueAtTime(time)` |
| [IOffsetable.cs](file:///workspace/common/Storyboarding/Commands/IOffsetable.cs) | `IOffsetable` | `Offset(double)` 时间平移（用于 `LoopCommand.EndGroup`） |
| [Command.cs](file:///workspace/common/Storyboarding/Commands/Command.cs) | `Command<TValue>` (abstract) | 泛型基类。`ValueAtTime` 在范围外按 `MaintainValue` 钳制、范围内按 `Easing.Ease` 插值；`ToOsbString` 格式化 |
| [CommandComparer.cs](file:///workspace/common/Storyboarding/Commands/CommandComparer.cs) | `CommandComparer` | 按舍入后的 `StartTime` 再 `EndTime` 排序 |
| [CommandGroup.cs](file:///workspace/common/Storyboarding/Commands/CommandGroup.cs) | `CommandGroup` (abstract) | loop/trigger 容器：`Add`/`EndGroup`/`Commands`/`Cost` |
| [LoopCommand.cs](file:///workspace/common/Storyboarding/Commands/LoopCommand.cs) | `LoopCommand` | `L,<startTime>,<loopCount>`；`EndGroup` 把子命令时间偏移到最早子命令处 |
| [TriggerCommand.cs](file:///workspace/common/Storyboarding/Commands/TriggerCommand.cs) | `TriggerCommand` | `T,<name>,<start>,<end>,<group>`；`IsFragmentableAt` 恒 false |
| [FadeCommand.cs](file:///workspace/common/Storyboarding/Commands/FadeCommand.cs) | `FadeCommand` | `F`，`CommandDecimal`，线性插值 |
| [MoveCommand.cs](file:///workspace/common/Storyboarding/Commands/MoveCommand.cs) | `MoveCommand` | `M`，`CommandPosition`，`ApplyToPosition` |
| [MoveXCommand.cs](file:///workspace/common/Storyboarding/Commands/MoveXCommand.cs) | `MoveXCommand` | `MX`，`ApplyToPositionX` |
| [MoveYCommand.cs](file:///workspace/common/Storyboarding/Commands/MoveYCommand.cs) | `MoveYCommand` | `MY`，`ApplyToPositionY` |
| [ScaleCommand.cs](file:///workspace/common/Storyboarding/Commands/ScaleCommand.cs) | `ScaleCommand` | `S`，结果钳到 `>=0`，`ApplyToScale` |
| [VScaleCommand.cs](file:///workspace/common/Storyboarding/Commands/VScaleCommand.cs) | `VScaleCommand` | `V`，`CommandScale`，`ApplyToScale(Vector2)` |
| [RotateCommand.cs](file:///workspace/common/Storyboarding/Commands/RotateCommand.cs) | `RotateCommand` | `R`，`ApplyToRotation` |
| [ColorCommand.cs](file:///workspace/common/Storyboarding/Commands/ColorCommand.cs) | `ColorCommand` | `C`，`CommandColor` |
| [ParameterCommand.cs](file:///workspace/common/Storyboarding/Commands/ParameterCommand.cs) | `ParameterCommand` | `P`，`CommandParameter`；`MaintainValue=(StartTime==EndTime)`；`ExportEndValue=false` |

##### `Storyboarding/CommandValues/` — 命令值类型

均为 `readonly struct`，实现 `CommandValue`（`float DistanceFrom(object)`、`string ToOsbString(ExportSettings)`）。

| 文件 | 类型 | 说明 |
| --- | --- | --- |
| [CommandValue.cs](file:///workspace/common/Storyboarding/CommandValues/CommandValue.cs) | `CommandValue` (interface) | 值类型接口 |
| [CommandDecimal.cs](file:///workspace/common/Storyboarding/CommandValues/CommandDecimal.cs) | `CommandDecimal` | 包装 `double`（拒绝 NaN/Infinity），与 `double`/`float` 互转 |
| [CommandPosition.cs](file:///workspace/common/Storyboarding/CommandValues/CommandPosition.cs) | `CommandPosition` | 2D 位置，与 `Vector2` 互转；`ToOsbString` 受 `UseFloatForMove` 控制 |
| [CommandScale.cs](file:///workspace/common/Storyboarding/CommandValues/CommandScale.cs) | `CommandScale` | 2D 缩放，静态 `One` |
| [CommandColor.cs](file:///workspace/common/Storyboarding/CommandValues/CommandColor.cs) | `CommandColor` | RGB（0..1），暴露 `byte R/G/B`；与 `Color4`/`System.Drawing.Color`/hex 互转；工厂 `FromRgb`/`FromHsb`/`FromHtml` |
| [CommandParameter.cs](file:///workspace/common/Storyboarding/CommandValues/CommandParameter.cs) | `CommandParameter` | 包装 `ParameterType`；静态 `FlipHorizontal`/`FlipVertical`/`AdditiveBlending` |

##### `Storyboarding/Display/` — 运行时求值

供 `OsbSprite` 回答"在时间 T 时属性 X 的值是多少"，用于编辑器预览与优化。

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [CommandTimeline.cs](file:///workspace/common/Storyboarding/Display/CommandTimeline.cs) | `CommandTimeline` / `CommandTimeline<TValue>` | 每个属性一条时间线，持有一个默认 channel + loop/trigger channel；`ValueAtTime` 按优先级（present-earliest > future-earliest > past-latest）选结果；`FindStartEdge`/`FindEndEdge` 找可见边界 |
| [CommandChannel.cs](file:///workspace/common/Storyboarding/Display/CommandChannel.cs) | `CommandChannel<TValue>` | 排序列表（二分插入），跟踪 `HasOverlap`；`CommandAtTime` 处理重叠（最早开始的优先） |
| [CommandChannelLoop.cs](file:///workspace/common/Storyboarding/Display/CommandChannelLoop.cs) | `CommandChannelLoop<TValue>` | 增加 `LoopCount`/`LoopStartTime`/`LoopDuration`；按迭代展开 `CommandResults` |
| [CommandChannelTrigger.cs](file:///workspace/common/Storyboarding/Display/CommandChannelTrigger.cs) | `CommandChannelTrigger<TValue>` | 增加 `ListenStartTime`/`ListenEndTime`/`Active`/`TriggerTime` |
| [CommandResult.cs](file:///workspace/common/Storyboarding/Display/CommandResult.cs) | `CommandResult<TValue>` (struct) | 命令 + `timeOffset` 包装 |

##### `Storyboarding/Util/`

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [CommandGenerator.cs](file:///workspace/common/Storyboarding/Util/CommandGenerator.cs) | `CommandGenerator` (DEBUG-only) | 状态驱动的命令生成器（被 3D 对象使用）。维护 `KeyframedValue` 的 position/scale/rotation/color/opacity/flags；`Add(State)`/`GenerateCommands(...)` 检查可见性、提交并简化关键帧、转成 sprite 命令 |
| [CommandSplitter.cs](file:///workspace/common/Storyboarding/Util/CommandSplitter.cs) | `CommandSplitter` (static) | 当 `OsbSprite` 命令数超阈值时拆分为多个精灵；`Split(...)` 在可分片时间点切分，迁移起始状态 |
| [OsbSpritePool.cs](file:///workspace/common/Storyboarding/Util/OsbSpritePool.cs) | `OsbSpritePool` | 按 texture path + origin 回收精灵；`Get(startTime,endTime)` 在 `MaxPoolDuration`（默认 60000ms）内复用 |
| [OsbAnimationPool.cs](file:///workspace/common/Storyboarding/Util/OsbAnimationPool.cs) | `OsbAnimationPool : OsbSpritePool` | 重写 `CreateSprite` 创建帧动画 |
| [OsbSpritePools.cs](file:///workspace/common/Storyboarding/Util/OsbSpritePools.cs) | `OsbSpritePools` | 聚合多个 pool，按复合键索引；支持 `additive` 与 `finalizeSprite` 回调 |

#### 4.1.2 `Storyboarding3d/` — 3D 场景投影（DEBUG-only）

把 3D 几何投影为 2D `OsbSprite` 命令。所有文件 `#if DEBUG` 包裹。

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Camera.cs](file:///workspace/common/Storyboarding3d/Camera.cs) | `Camera` (abstract) | `Resolution`（默认 1366×768）、`ResolutionScale`、`AspectRatio`；抽象 `StateAt(time)` |
| [CameraState.cs](file:///workspace/common/Storyboarding3d/CameraState.cs) | `CameraState` | 不可变快照：`ViewProjection`、`FocusDistance`、clip/fade 平面；`ToScreen` 把 3D 点投到 storyboard 像素；`OpacityAt(distance)` 距离淡入淡出 |
| [PerspectiveCamera.cs](file:///workspace/common/Storyboarding3d/PerspectiveCamera.cs) | `PerspectiveCamera : Camera` | 用 `KeyframedValue` 描述 position/target/up/FOV/clip；`StateAt` 构造 LookAt + 透视矩阵 |
| [Object3d.cs](file:///workspace/common/Storyboarding3d/Object3d.cs) | `Object3d` | 场景图节点基类：children、coloring/opacity、`WorldTransformAt`、树遍历 `GenerateTreeSprite`/`GenerateTreeStates`/`GenerateTreeCommands`/`GenerateTreeLoopCommands` |
| [Object3dState.cs](file:///workspace/common/Storyboarding3d/Object3dState.cs) | `Object3dState` | 不可变：`WorldTransform`/`Color`/`Opacity` |
| [Node3d.cs](file:///workspace/common/Storyboarding3d/Node3d.cs) | `Node3d : Object3d` | 带 position/scale/rotation(`Quaternion` slerp) 关键帧；`WorldTransformAt` 组合 scale→rotation→translation |
| [Sprite3d.cs](file:///workspace/common/Storyboarding3d/Sprite3d.cs) | `Sprite3d : Node3d, HasOsbSprites` | 纹理四边形；`RotationMode`(`Fixed`/`UnitX`/`UnitY`)；投影 origin 与单位轴到屏幕算旋转/缩放 |
| [Line3d.cs](file:///workspace/common/Storyboarding3d/Line3d.cs) | `Line3d : Node3d, HasOsbSprites` | 两点间线条；投影端点算长度/角度/缩放 |
| [Line3dEx.cs](file:///workspace/common/Storyboarding3d/Line3dEx.cs) | `Line3dEx` | 带主体 + 上下边 + 起止端帽的扩展线；5 个 `CommandGenerator` |
| [Triangle3d.cs](file:///workspace/common/Storyboarding3d/Triangle3d.cs) | `Triangle3d : Node3d, HasOsbSprites` | 三角形（两个精灵）；确定"固定边"后投影 |
| [HasOsbSprite.cs](file:///workspace/common/Storyboarding3d/HasOsbSprite.cs) | `HasOsbSprites` (interface) | `IEnumerable<OsbSprite> Sprites` |
| [Scene3d.cs](file:///workspace/common/Storyboarding3d/Scene3d.cs) | `Scene3d` | 顶层容器，`Root` 为 `Node3d`；`Generate` 按固定步长或 beatmap tick 采样 |

#### 4.1.3 `Mapset/` — osu! 谱面解析

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Beatmap.cs](file:///workspace/common/Mapset/Beatmap.cs) | `Beatmap` (abstract) | 谱面抽象：difficulty、`HitObjects`、`ControlPoints`、`TimingPoints`、`ComboColors`、`Breaks`；`GetControlPointAt`/`GetTimingPointAt`；静态 `GetDifficultyRange`（osu! 分段难度缩放） |
| [BeatmapExtensions.cs](file:///workspace/common/Mapset/BeatmapExtensions.cs) | static ext | `ForEachTick(start,end,snapDivisor,tickAction)` 按 timing point 步进；`AsSamplePoints` 把 hitobject 映射为采样点 |
| [ControlPoint.cs](file:///workspace/common/Mapset/ControlPoint.cs) | `ControlPoint : IComparable<ControlPoint>` | `Offset`/`BeatPerMeasure`/`IsInherited`/`IsKiai`/`OmitFirstBarLine`；`BeatDuration`/`Bpm`/`SliderMultiplier`；静态 `Parse(line)`、`Default` |
| [OsuHitObject.cs](file:///workspace/common/Mapset/OsuHitObject.cs) | `OsuHitObject` (abstract) | hitobject 基类。常量 `PlayfieldSize`(512×384)、`StoryboardSize`(640×480)、`WidescreenStoryboardBounds` 等；`PlayfieldPositionAtTime`/`PlayfieldEndPosition`；静态 `Parse` 按标志分派到子类。枚举 `HitObjectFlag`/`HitSoundAddition`/`SampleSet` |
| [OsuCircle.cs](file:///workspace/common/Mapset/OsuCircle.cs) | `OsuCircle` | 圆圈 hitobject，解析可选 special 字段 |
| [OsuSlider.cs](file:///workspace/common/Mapset/OsuSlider.cs) | `OsuSlider` | 滑条。`nodes`/`controlPoints`/`Curve`；`EndTime = StartTime + TravelCount*TravelDuration`；`PlayfieldPositionAtTime` 沿曲线重复/行进；`generateCurve` 按 `SliderCurveType` 选 `CatmullCurve`/`BezierCurve`/`CircleCurve`/`CompositeCurve`。含 `OsuSliderNode`/`OsuSliderControlPoint`/枚举 `SliderCurveType` |
| [OsuSpinner.cs](file:///workspace/common/Mapset/OsuSpinner.cs) | `OsuSpinner` | 转盘 |
| [OsuHold.cs](file:///workspace/common/Mapset/OsuHold.cs) | `OsuHold` | osu!mania 长按 |
| [OsuBreak.cs](file:///workspace/common/Mapset/OsuBreak.cs) | `OsuBreak` (struct) | 休息时段 `StartTime`/`EndTime` |
| [OsuSamplePoint.cs](file:///workspace/common/Mapset/OsuSamplePoint.cs) | `OsuSamplePoint` (interface) | 采样点接口 |

#### 4.1.4 `Scripting/` — 脚本基类

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Script.cs](file:///workspace/common/Scripting/Script.cs) | `Script` (abstract) | 一次性 `Identifier` 属性 |
| [StoryboardObjectGenerator.cs](file:///workspace/common/Scripting/StoryboardObjectGenerator.cs) | `StoryboardObjectGenerator : Script` | **用户脚本基类**。`[ThreadStatic] Current`；`GetLayer`/`Beatmap`/`GetBeatmap`；`GetProjectBitmap`/`GetMapsetBitmap`（带缓存）；`Random`（受 `RandomSeed` 控制）；`AudioDuration`/`GetFft`；`LoadSubtitles`/`LoadFont`；`UpdateConfiguration`/`ApplyConfiguration`（反射 `[Configurable]` 字段）；抽象 `Generate()` |

#### 4.1.5 `Animations/` — 关键帧动画

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Keyframe.cs](file:///workspace/common/Animations/Keyframe.cs) | `Keyframe<TValue>` (struct) | `Time`/`Value`/`Ease`(`Func<double,double>`)，按 `Time` 比较 |
| [KeyframedValue.cs](file:///workspace/common/Animations/KeyframedValue.cs) | `KeyframedValue<TValue>` | 关键帧集合 + `interpolate` 委托。`Add`/`AddRange`/`Until`；`ValueAt(time)` 二分插值；`ForEachPair` 遍历相邻对（处理 step/explicit/loopable）；`Linearize(timestep)`；`Simplify1dKeyframes`/`Simplify2dKeyframes`/`Simplify3dKeyframes`（Douglas-Peucker） |
| [KeyframedValueExtensions.cs](file:///workspace/common/Animations/KeyframedValueExtensions.cs) | static ext | `ForEachFlag`（bool 时间线为 true 的区间）；`Vector2`/`Vector3`/`Quaternion` 的 `Add` 重载 |
| [EasingFunctions.cs](file:///workspace/common/Animations/EasingFunctions.cs) | static | 全部标准 easing（Step/Linear/Quad/Cubic/Quart/Quint/Sine/Expo/Circ/Back/Bounce/Elastic 各 In/Out/InOut）；`Ease(OsbEasing,value)`/`ToEasingFunction(OsbEasing)` |
| [InterpolatingFunctions.cs](file:///workspace/common/Animations/InterpolatingFunctions.cs) | static | 插值委托：`Float`/`FloatAngle`/`Double`/`Vector2`/`Vector3`/`QuaternionSlerp`/`CommandColor` 等 |

#### 4.1.6 `Curves/` — 曲线计算

供 `OsuSlider` 计算滑条球位置。

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Curve.cs](file:///workspace/common/Curves/Curve.cs) | `Curve` (interface) | `StartPosition`/`EndPosition`/`Length`/`PositionAtDistance`/`PositionAtDelta` |
| [BaseCurve.cs](file:///workspace/common/Curves/BaseCurve.cs) | `BaseCurve` (abstract) | 懒构建 `(distance,position)` 列表；`PositionAtDistance` 二分+线性插值 |
| [BezierCurve.cs](file:///workspace/common/Curves/BezierCurve.cs) | `BezierCurve` | De Casteljau 算法（`intermediatePoints` 为 `[ThreadStatic]`） |
| [CatmullCurve.cs](file:///workspace/common/Curves/CatmullCurve.cs) | `CatmullCurve` | Catmull-Rom 基函数，端点用幻影点 |
| [CircleCurve.cs](file:///workspace/common/Curves/CircleCurve.cs) | `CircleCurve` | 三点定圆（外接圆），静态 `IsValid` |
| [CompositeCurve.cs](file:///workspace/common/Curves/CompositeCurve.cs) | `CompositeCurve` | 多曲线串联，长度求和 |
| [TransformedCurve.cs](file:///workspace/common/Curves/TransformedCurve.cs) | `TransformedCurve` | 包装另一曲线，加 offset/scale/可选 reversed |

#### 4.1.7 `Subtitles/` — 字幕与字体生成

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [SubtitleLine.cs](file:///workspace/common/Subtitles/SubtitleLine.cs) | `SubtitleLine` | 不可变 `StartTime`/`EndTime`/`Text` |
| [SubtitleSet.cs](file:///workspace/common/Subtitles/SubtitleSet.cs) | `SubtitleSet` | `List<SubtitleLine>` 包装 |
| [FontEffect.cs](file:///workspace/common/Subtitles/FontEffect.cs) | `FontEffect` (interface) | `Overlay`/`Measure()`/`Draw(...)` |
| [FontBackground.cs](file:///workspace/common/Subtitles/FontBackground.cs) | `FontBackground` | 实心背景填充 |
| [FontGlow.cs](file:///workspace/common/Subtitles/FontGlow.cs) | `FontGlow` | 高斯卷积 alpha 染色发光 |
| [FontGradient.cs](file:///workspace/common/Subtitles/FontGradient.cs) | `FontGradient` | 线性渐变画刷 |
| [FontOutline.cs](file:///workspace/common/Subtitles/FontOutline.cs) | `FontOutline` | 4 对角方向描边 |
| [FontShadow.cs](file:///workspace/common/Subtitles/FontShadow.cs) | `FontShadow` | 偏移阴影 |
| [FontGenerator.cs](file:///workspace/common/Subtitles/FontGenerator.cs) | `FontGenerator` | 用 `System.Drawing` 生成并缓存每字形 PNG。`FontTexture`/`FontDescription`；`GetTexture(text)`；YAML 缓存（`.cache/font/`，MD5 键） |
| [Parsers/SrtParser.cs](file:///workspace/common/Subtitles/Parsers/SrtParser.cs) | `SrtParser` | SRT 解析 |
| [Parsers/AssParser.cs](file:///workspace/common/Subtitles/Parsers/AssParser.cs) | `AssParser` | ASS/SSA 解析 |
| [Parsers/SbvParser.cs](file:///workspace/common/Subtitles/Parsers/SbvParser.cs) | `SbvParser` | YouTube sbv 解析 |

#### 4.1.8 `Util/` — 通用工具

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Affine2.cs](file:///workspace/common/Util/Affine2.cs) | `Affine2` (struct) | 2D 仿射变换（两行 `Vector3`）；`Translate`/`Scale`/`Rotate`/`Multiply`；`Transform`/`TransformSeparate`/`TransformX`/`TransformY` |
| [BitmapHelper.cs](file:///workspace/common/Util/BitmapHelper.cs) | static | `CalculateGaussianKernel`/`Convolute`/`ConvoluteAlpha`/`Blur`/`FindTransparencyBounds`；嵌套 `PinnedBitmap` |
| [Box2Extensions.cs](file:///workspace/common/Util/Box2Extensions.cs) | static ext | `IntersectWith` |
| [Misc.cs](file:///workspace/common/Util/Misc.cs) | static | 包装 `BrewLib.Util.Misc.WithRetries`（文件 I/O 重试） |
| [NamedValue.cs](file:///workspace/common/Util/NamedValue.cs) | `NamedValue` (struct) | `Name`+`Value`，用于枚举可选值 |
| [ObjectSerializer.cs](file:///workspace/common/Util/ObjectSerializer.cs) | `ObjectSerializer`/`SimpleObjectSerializer<T>` | 配置字段值的二进制/字符串序列化；预注册 int/float/double/string/bool/Vector2/Vector3/Color4 |
| [OrientedBoundingBox.cs](file:///workspace/common/Util/OrientedBoundingBox.cs) | `OrientedBoundingBox` | 可旋转矩形，分离轴定理（SAT）求交；用于 `OsbSprite.InScreenBounds` |
| [Pool.cs](file:///workspace/common/Util/Pool.cs) | `Pool<T>` | 通用对象池 |
| [StreamReaderExtensions.cs](file:///workspace/common/Util/StreamReaderExtensions.cs) | static ext | INI 风格解析：`ParseSections`/`ParseSectionLines`/`ParseKeyValueSection` |
| [VectorHelper.cs](file:///workspace/common/Util/VectorHelper.cs) | static | `GetAngle`/`SegmentClosestPoint`/`SegmentsIntersect` |

---

### 4.2 `editor` — GUI 编辑器

命名空间 `StorybrewEditor`，输出 `StorybrewEditor.exe`（WinExe，x86）。引用 `brewlib`、`common`、`OpenTK 2.0`、`Microsoft.CodeAnalysis.Common/CSharp 4.11.0`、`Microsoft.Net.Compilers.Toolset 4.11.0`。所有 `Resources/*` 以嵌入资源形式打包。

#### 4.2.1 顶层文件

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Program.cs](file:///workspace/editor/Program.cs) | `Program` (static) | **入口点**。`[STAThread] Main`：分发 `update`/`build` 参数，否则 `startEditor`。`runMainLoop` 固定步长 update（`1/Settings.UpdateRate`，每帧最多 2 次）+ 可变步长 draw + tween。全局 `AudioManager`/`Settings`/`IsMainThread`/`CheckMainThread`。调度区：`Schedule`/`RunMainThread`（`ManualResetEvent` 同步）/`RunScheduledTasks`。日志：`logs/trace.log`/`exception.log`/`crash.log` |
| [Editor.cs](file:///workspace/editor/Editor.cs) | `Editor : IDisposable` | 编排窗口、GL 上下文、皮肤、输入、`ScreenLayerManager`。`Initialize` 构建 `DrawContext`、注册 `TextureContainerAtlas`(1024×1024)/`QuadRendererBuffered`/`LineRendererBuffered`、加载 `skin.json`、创建 `InputManager`、`Restart` 到 `StartMenu`。`Update`/`Draw` 推进帧时钟并绘制。嵌套 `FormsWindow` 包装 OpenTK 句柄供 WinForms 对话框使用 |
| [Builder.cs](file:///workspace/editor/Builder.cs) | `Builder` (static) | 生成发布 zip（`storybrew.<Major>.<Minor>.zip`）并跑 `testUpdate` 验证更新链路 |
| [Settings.cs](file:///workspace/editor/Settings.cs) | `Settings`/`Setting<T>` | 持久化到 `settings.cfg`（`Key: value` 行）。字段：`Id`/`FrameRate`/`UpdateRate`(60)/`Volume`/`FitStoryboard`/`ShowStats`/`VerboseVsCode`/`EffectThreads`(0=ProcessorCount-1)/`TimeCopyFormat` |
| [Updater.cs](file:///workspace/editor/Updater.cs) | `Updater` (static) | 自更新：`Update(dest,fromVersion)` 替换文件（尊重 `ignoredPaths`/`readOnlyPaths`）+ 版本迁移（`updateData`）；`NotifyEditorRun` 处理 `firstrun` |

#### 4.2.2 `Mapset/`

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [EditorBeatmap.cs](file:///workspace/editor/Mapset/EditorBeatmap.cs) | `EditorBeatmap : Beatmap` | `.osu` 解析器。`Load(path)` 分派各 section；`postProcessHitObjects` 实现 osu! stacking 逻辑（`StackIndex`/`StackOffset`） |
| [MapsetManager.cs](file:///workspace/editor/Mapset/MapsetManager.cs) | `MapsetManager : IDisposable` | 拥有谱面文件夹的全部 beatmap；`FileSystemWatcher`（递归）经 `ThrottledActionScheduler` 触发 `OnFileChanged` |
| [BeatmapLoadingException.cs](file:///workspace/editor/Mapset/BeatmapLoadingException.cs) | `BeatmapLoadingException` | 谱面加载异常 |

#### 4.2.3 `Scripting/` — Roslyn 编译与热重载

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [ScriptCompiler.cs](file:///workspace/editor/Scripting/ScriptCompiler.cs) | `ScriptCompiler` (static) | `Compile(sourcePaths, outputPath, referencedAssemblies)`：`SyntaxFactory.ParseSyntaxTree` → `MetadataReference` → `CSharpCompilation`（DLL）→ `Emit`；失败抛 `ScriptCompilationException`（含文件/行号/源行） |
| [ScriptContainer.cs](file:///workspace/editor/Scripting/ScriptContainer.cs) | `ScriptContainer<TScript>` | 单脚本的编译 + 加载上下文。`currentVersion`/`targetVersion`（volatile）惰性重编译；`AssemblyLoadContext`（collectible）；`CreateScript` 编译到 GUID 命名 DLL、加载、解析 `StorybrewScripts.<ScriptName>`、`Activator.CreateInstance`；`ReloadScript` bump target + 触发 `OnScriptChanged` |
| [ScriptManager.cs](file:///workspace/editor/Scripting/ScriptManager.cs) | `ScriptManager<TScript> : IDisposable` | 管理全部脚本容器。两个 `FileSystemWatcher`（脚本/库），经 `ThrottledActionScheduler` 调 `ReloadScript`；`Get(scriptName)` 按需创建并从 common 复制；`updateSolutionFiles` 写 `storyboard.sln`/`scripts.csproj` 到脚本文件夹 |
| [ScriptCompilationException.cs](file:///workspace/editor/Scripting/ScriptCompilationException.cs) | `ScriptCompilationException` | 编译异常 |
| [ScriptLoadingException.cs](file:///workspace/editor/Scripting/ScriptLoadingException.cs) | `ScriptLoadingException` | 加载异常（含"类名须与文件名一致"提示） |

#### 4.2.4 `Storyboarding/`

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Project.cs](file:///workspace/editor/Storyboarding/Project.cs) | `Project : IDisposable` | **项目根模型**。常量 `Version=7`、`DataFolder=".sbrew"`、`TextExtension=".yaml"`。路径：`ProjectFolderPath`/`ScriptsPath`/`AudioPath`/`OsbPath`。拥有 `LayerManager`/`TextureContainer`/`AudioContainer`/`MapsetManager`/`FrameStats`/`ExportSettings`/`ScriptManager`。Effect 列表 + `AsyncActionQueue<Effect>`（线程数 = `Settings.EffectThreads`）。`Save` 写 YAML（`index.yaml`/`user.yaml`/`effect.<guid>.yaml`，用 `Tiny` 库）；`Load` 优先文本格式；`ExportToOsb` 写 `.osb` 与按难度 `.osu` 的 `[Events]`。`Draw` 委托 `LayerManager` |
| [Effect.cs](file:///workspace/editor/Storyboarding/Effect.cs) | `Effect` (abstract) | 效果基类：`Guid`/`Name`/`Status`/`Config`/layers；`Refresh` → `Project.QueueEffectUpdate`；抽象 `Update(CancellationTokenSource)`/`CancelUpdate`。枚举 `EffectStatus`（Initializing/Loading/Configuring/Updating/ReloadPending/Ready/CompilationFailed/LoadingFailed/ExecutionFailed/UpdateCanceled） |
| [ScriptedEffect.cs](file:///workspace/editor/Storyboarding/ScriptedEffect.cs) | `ScriptedEffect : Effect` | 包装 `ScriptContainer<StoryboardObjectGenerator>`。`Update`：`Loading`→`Configuring`（`UpdateConfiguration`/`ApplyConfiguration`，主线程）→`Updating`（`Generate`）→后处理→主线程换层。捕获编译/加载/取消异常并设状态 |
| [EditorGeneratorContext.cs](file:///workspace/editor/Storyboarding/EditorGeneratorContext.cs) | `EditorGeneratorContext : GeneratorContext` | 传给脚本 `Generate` 的上下文。`GetLayer` 找/建 `EditorStoryboardLayer`；`AddDependency` 注册到 `MultiFileWatcher`；懒加载 `FftStream` |
| [EditorOsbSprite.cs](file:///workspace/editor/Storyboarding/EditorOsbSprite.cs) | `EditorOsbSprite : OsbSprite, DisplayableObject, HasPostProcess` | **可绘制精灵**。静态 `Draw(...)`：跳过不可见、更新 `FrameStats`、Alt 左键强制可见、解析贴图（mapset→asset，吞 IOException）、采样命令算 position/rotation/scale/color、应用 `StoryboardTransform` 与 `DimFactor`、`DrawState.Prepare(QuadRenderer,...).Draw` |
| [EditorOsbAnimation.cs](file:///workspace/editor/Storyboarding/EditorOsbAnimation.cs) | `EditorOsbAnimation : OsbAnimation, DisplayableObject, HasPostProcess` | 委托 `Draw` 给 `EditorOsbSprite.Draw` |
| [EditorOsbSample.cs](file:///workspace/editor/Storyboarding/EditorOsbSample.cs) | `EditorOsbSample : OsbSample, EventObject` | `TriggerEvent` 解析样本路径并播放 |
| [EditorStoryboardLayer.cs](file:///workspace/editor/Storyboarding/EditorStoryboardLayer.cs) | `EditorStoryboardLayer : StoryboardLayer, IComparable<...>` | `Guid`/`Name`/`Effect`/`Visible`/`OsbLayer`/`DiffSpecific`；变换透传到根 segment；`CompareTo` 按 `OsbLayer` 再 `DiffSpecific` 排序 |
| [EditorStoryboardSegment.cs](file:///workspace/editor/Storyboarding/EditorStoryboardSegment.cs) | `EditorStoryboardSegment : StoryboardSegment, DisplayableObject, HasPostProcess` | 递归容器。`storyboardObjects`/`displayableObjects`/`eventObjects`/`segments`/`namedSegments` + 懒 `displayableBuckets[]`（10s 时间桶剔除）。`Draw` 跳过区间外、高亮正弦调制、<1000 直接遍历否则用桶。`WriteOsb` 用 `Parallel.For` 并行序列化 |
| [LayerManager.cs](file:///workspace/editor/Storyboarding/LayerManager.cs) | `LayerManager` | 有序 `EditorStoryboardLayer` 列表。`Add` 二分插入；`Replace`（单→单/列表→列表/单→列表）；`MoveUp/Down/ToTop/ToBottom/MoveToOsbLayer/MoveToLayer` |
| [EventObject.cs](file:///workspace/editor/Storyboarding/EventObject.cs) | `EventObject` (interface) | `EventTime`/`TriggerEvent(Project,currentTime)` |
| [DisplayableObject.cs](file:///workspace/editor/Storyboarding/DisplayableObject.cs) | `DisplayableObject` (interface) | `StartTime`/`EndTime`/`Draw(...)` |
| [HasPostProcess.cs](file:///workspace/editor/Storyboarding/HasPostProcess.cs) | `HasPostProcess` (interface) | `PostProcess()` |
| [FrameStats.cs](file:///workspace/editor/Storyboarding/FrameStats.cs) | `FrameStats` | 每帧指标：`SpriteCount`/`Batches`/`CommandCount`/`OverlappedCommands`/`ScreenFill`/`GpuPixelsFrame`/`GpuMemoryFrameMb` 等，供 `ProjectMenu.buildWarningMessage` 显示性能警告 |

#### 4.2.5 `ScreenLayers/` — 屏幕层（菜单栈）

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [UiScreenLayer.cs](file:///workspace/editor/ScreenLayers/UiScreenLayer.cs) | `UiScreenLayer : ScreenLayer` | 所有编辑器屏幕基类。`Load` 建 `WidgetManager` + `CameraOrtho`；`Resize` 保持 1024×768 最小虚拟坐标；`MakeTabs` 互斥 tab |
| [StartMenu.cs](file:///workspace/editor/ScreenLayers/StartMenu.cs) | `StartMenu` | 启动屏：New/Open/Preferences/Close/Discord/Wiki + 更新按钮；检查 .NET SDK ref 目录；`checkLatestVersion` 查 GitHub releases API（15 分钟缓存） |
| [NewProjectMenu.cs](file:///workspace/editor/ScreenLayers/NewProjectMenu.cs) | `NewProjectMenu` | 新建项目：名称 + 谱面路径校验 → `Project.Create` |
| [ProjectMenu.cs](file:///workspace/editor/ScreenLayers/ProjectMenu.cs) | `ProjectMenu` | **主编辑屏**（~750 行）。持有 `Project`、主/预览 `StoryboardDrawable`、`AudioStream`+`TimeSourceExtender`、`TimelineSlider`、`EffectList`/`LayerList`/`SettingsMenu`/`EffectConfigUi`。`Update` 驱动时间源、按钮状态、重复区循环、时间显示、警告、音频事件、预览。`OnKeyDown`：方向键/Space/Ctrl-S/Ctrl-C/O。`exportProject`/`exportProjectAll` |
| [UpdateMenu.cs](file:///workspace/editor/ScreenLayers/UpdateMenu.cs) | `UpdateMenu` | 下载 zip → 解压 → 启动新 exe `update "<path>" <version>` |
| [ReferencedAssemblyConfig.cs](file:///workspace/editor/ScreenLayers/ReferencedAssemblyConfig.cs) | `ReferencedAssemblyConfig` | 管理 `project.ImportedAssemblies` |
| [ScreenLayerManagerExtensions.cs](file:///workspace/editor/ScreenLayers/ScreenLayerManagerExtensions.cs) | static ext | `OpenFolderPicker`/`OpenFilePicker`/`OpenSaveLocationPicker`（后台 STA 线程跑 WinForms 对话）；`AsyncLoading`/`ShowMessage`/`ShowPrompt`/`ShowContextMenu`/`ShowOpenProject` |
| [Util/ContextMenu.cs](file:///workspace/editor/ScreenLayers/Util/ContextMenu.cs) | `ContextMenu<T>` | 可搜索选项列表弹窗 |
| [Util/LoadingScreen.cs](file:///workspace/editor/ScreenLayers/Util/LoadingScreen.cs) | `LoadingScreen` | 后台 STA 线程跑 action + 底部标签 |
| [Util/MessageBox.cs](file:///workspace/editor/ScreenLayers/Util/MessageBox.cs) | `MessageBox` | 滚动消息 + Ok/Yes/No/Cancel，Ctrl-C 复制 |
| [Util/PromptBox.cs](file:///workspace/editor/ScreenLayers/Util/PromptBox.cs) | `PromptBox` | 标题 + 描述 + 文本框 |

#### 4.2.6 `UserInterface/`

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Drawables/StoryboardDrawable.cs](file:///workspace/editor/UserInterface/Drawables/StoryboardDrawable.cs) | `StoryboardDrawable : Drawable` | 包装 `Project` 渲染。`Time`/`Clip`/`UpdateFrameStats`；`Draw` 设 `project.DisplayTime`、可选 `DrawState.Clip`、委托 `project.Draw` |
| [Components/EffectList.cs](file:///workspace/editor/UserInterface/Components/EffectList.cs) | `EffectList : Widget` | 效果列表 + 重命名/状态/配置/编辑/删除。"Add effect" 菜单 → `project.AddScriptedEffect`；"New script" 写 `scripttemplate.csx`；`openEffectEditor` 查找 VS Code/VSCodium 并启动 |
| [Components/EffectConfigUi.cs](file:///workspace/editor/UserInterface/Components/EffectConfigUi.cs) | `EffectConfigUi : Widget` | 编辑 `EffectConfig`：按 `BeginsGroup` 分组，按类型生成 `Selectbox`/`Textbox`/`Vector2Picker`/`Vector3Picker`/`HsbColorPicker`；剪贴板复制粘贴（`storybrewEffectConfig` 格式 + `ObjectSerializer`） |
| [Components/LayerList.cs](file:///workspace/editor/UserInterface/Components/LayerList.cs) | `LayerList : Widget` | 按 `OsbLayer`/`DiffSpecific` 分组的图层列表；拖拽重排（`MoveToLayer`/`MoveToOsbLayer`） |
| [Components/SettingsMenu.cs](file:///workspace/editor/UserInterface/Components/SettingsMenu.cs) | `SettingsMenu : Widget` | 项目设置：Help/Referenced Assemblies/Dim 滑块/Export Time as Floating Point |
| [TimelineSlider.cs](file:///workspace/editor/UserInterface/TimelineSlider.cs) | `TimelineSlider : Slider` | 带节拍吸附的时间线。`SnapDivisor`(默认 4)/`ShowHitObjects`/`RepeatStart`/`RepeatEnd`；`DrawBackground` 画 kiai/break/bookmark/重复区/吸附网格/时间标记；`Scroll` 按一个吸附步移动 |
| [HsbColorPicker.cs](file:///workspace/editor/UserInterface/HsbColorPicker.cs) | `HsbColorPicker : Widget, Field` | HSB+A 拾色器 + HTML hex 输入 |
| [PathSelector.cs](file:///workspace/editor/UserInterface/PathSelector.cs) | `PathSelector : Widget` | 文本框 + 浏览按钮；`PathSelectorMode` 枚举 |
| [Selectbox.cs](file:///workspace/editor/UserInterface/Selectbox.cs) | `Selectbox : Widget, Field` | ≤2 选项循环，>2 弹 `ContextMenu` |
| [Vector2Picker.cs](file:///workspace/editor/UserInterface/Vector2Picker.cs) | `Vector2Picker : Widget, Field` | X/Y 文本框 |
| [Vector3Picker.cs](file:///workspace/editor/UserInterface/Vector3Picker.cs) | `Vector3Picker : Widget, Field` | X/Y/Z 文本框 |
| [Skinning/Styles/](file:///workspace/editor/UserInterface/Skinning/Styles) | `ColorPickerStyle`/`PathSelectorStyle`/`SelectboxStyle` | 皮肤样式标记类，由 `Editor.Initialize` 的 `Skin.ResolveStyleType` 解析 |

#### 4.2.7 `Util/`

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [AsyncActionQueue.cs](file:///workspace/editor/Util/AsyncActionQueue.cs) | `AsyncActionQueue<T> : IDisposable` | 多线程工作队列（effect 更新用）。`Queue(target,action,mustRunAlone)`；`runnerCount` 默认 `ProcessorCount-1`；`AbortQueuedActions(stopThreads)` 取消并（必要时 `Native.TerminateThread`）终止 |
| [MultiFileWatcher.cs](file:///workspace/editor/Util/MultiFileWatcher.cs) | `MultiFileWatcher : IDisposable` | 聚合多 `FileSystemWatcher`；`Watch(filename)` 复用目录 watcher 或向上找存在父级建递归 watcher；经 `ThrottledActionScheduler` 节流 |
| [Native.cs](file:///workspace/editor/Util/Native.cs) | `Native` (static) | P/Invoke `kernel32`：`TerminateThread`/`OpenThread`/`CloseHandle` |
| [NetHelper.cs](file:///workspace/editor/Util/NetHelper.cs) | `NetHelper` (static) | 异步 HTTP：`Request`（带磁盘缓存）/`Post`/`BlockingPost`/`Download`（带进度） |
| [OsuHelper.cs](file:///workspace/editor/Util/OsuHelper.cs) | `OsuHelper` (static) | 定位 osu! 安装：`GetOsuExePath`（注册表 `osu\DefaultIcon` → 回退 `%LocalAppData%\osu!\osu!.exe`）/`GetOsuFolder`/`GetOsuSongFolder` |
| [ThrottledActionScheduler.cs](file:///workspace/editor/Util/ThrottledActionScheduler.cs) | `ThrottledActionScheduler` | 去重 + 重试主线程 action；`Delay=100ms`；action 返回 false 则重排 |

#### 4.2.8 `Resources/` — 嵌入资源

全部以 `<EmbeddedResource>` 打包，经 `AssemblyResourceContainer` 读取。

| 资源 | 用途 |
| --- | --- |
| [project/scripts.csproj](file:///workspace/editor/Resources/project/scripts.csproj) | 复制到项目脚本文件夹的 csproj 模板（引用 `System.Drawing.Common.dll`/`OpenTK.dll`/`StorybrewCommon.dll`，相对 `HintPath`） |
| [project/storyboard.sln](file:///workspace/editor/Resources/project/storyboard.sln) | 配套解决方案，供 VS Code 打开 |
| [scripttemplate.csx](file:///workspace/editor/Resources/scripttemplate.csx) | 新建脚本模板，`%CLASSNAME%` 占位符替换 |
| [skin.json](file:///workspace/editor/Resources/skin.json) + `skin_constants.json`/`skin_drawables.json`/`skin_drawables_debug.json`/`skin_styles.json` | UI 皮肤定义 |
| `FontAwesome.ttf`/`Roboto-Light.ttf`/`Roboto-Regular.ttf` | UI 字体 |
| `ui-line.png`/`ui-rounded-borders.png`/`ui-stripes.png` + 对应 `*-opt.json` | 皮肤化 UI 纹理 |
| `icon.ico` | 应用图标 |

---

### 4.3 `scripts` — 内置示例脚本

命名空间 `StorybrewScripts`，全部继承 `StoryboardObjectGenerator` 并重写 `Generate()`。通过 `[Configurable]`/`[Group]`/`[Description]` 暴露编辑器可配置属性。

| 文件 | 效果 | 关键技术 |
| --- | --- | --- |
| [Background.cs](file:///workspace/scripts/Background.cs) | 显示背景图（按 480px 高度缩放铺满），首尾淡入淡出 | 默认 `Beatmap.BackgroundPath`，按 `480.0/bitmap.Height` 缩放 |
| [HitObjectHighlight.cs](file:///workspace/scripts/HitObjectHighlight.cs) | 在每个 hitobject 位置放发光精灵，滑条沿路径移动 | 遍历 `Beatmap.HitObjects`，`hitobject.PositionAtTime(t)`，`Additive` 混合，combo 着色 |
| [ImportOsb.cs](file:///workspace/scripts/ImportOsb.cs) | 导入现有 `.osb` 到 storybrew | `OpenProjectFile`+`StreamReader`，`ParseSections`/`ParseSectionLines`，处理 `Variables` 替换、`Sprite`/`Animation`/`Sample`/`T`/`L` 及全部命令字母 |
| [Jigoku.cs](file:///workspace/scripts/Jigoku.cs) | 完整音乐同步 storyboard（地图 s/183628）：下落音符 + 闪光 + 粒子 + 角色揭示 | `OsbSpritePools` 回收，`ColorHsb` 色相偏移，`ScaleVec` 挤压，随机 easing |
| [Karaoke.cs](file:///workspace/scripts/Karaoke.cs) | 卡拉OK歌词：每个音节按 `\k` 标签高亮（灰→白→灰） | `LoadFont`（含 glow/outline/shadow），正则 `({\\k(\d+)})?([^{]+)` 解析，双 pass（基础+发光层） |
| [Lyrics.cs](file:///workspace/scripts/Lyrics.cs) | 静态歌词：逐行或逐字渲染 | `PerCharacter` 开关，`font.GetTexture(line.Text)` 整串纹理 vs 逐字形纹理 |
| [Particles.cs](file:///workspace/scripts/Particles.cs) | 粒子漂移：按方向 + 随机散布循环 | `Sqrt(Random(1))` 均匀圆盘采样，`StartLoopGroup`/`EndGroup`，`isVisible` 用 `InScreenBounds` 剔除 |
| [RadialSpectrum.cs](file:///workspace/scripts/RadialSpectrum.cs) | 圆形频谱：条形沿圆周径向移动 | `KeyframedValue<Vector2>[]`，`GetFft` 按 `BeatDuration/BeatDivisor` 采样，`Simplify2dKeyframes`+`ForEachPair`，`CommandSplitThreshold=300` |
| [Spectrum.cs](file:///workspace/scripts/Spectrum.cs) | 线性频谱：底部均衡器条 | `KeyframedValue<float>`，`Simplify1dKeyframes`，`ScaleVec` 缩放 |
| [Tetris.cs](file:///workspace/scripts/Tetris.cs) | 俄罗斯方块动画：下落、堆叠、消行 | `Cell[GridWidth,GridHeight]` 状态，Fisher-Yates `shuffle`，双层（块+阴影），`Quaternion.FromEulerAngles` 旋转 |

---

### 4.4 `test` — 单元测试

MSTest（`MSTest.TestAdapter` 3.1.1 + `TestFramework` 3.1.1）+ `Microsoft.NET.Test.Sdk` 17.8.0 + `coverlet.collector` 6.0.0。目标 `net8.0-windows`。引用 `brewlib` 与 `common`。所有测试直接 `new OsbSprite()` 推命令并断言，专门锁定 osu!stable 的（常反直觉的）命令解析语义。

| 文件 | 覆盖范围 | 关键断言 |
| --- | --- | --- |
| [ChannelOverlapTest.cs](file:///workspace/test/ChannelOverlapTest.cs) | 同 channel（如 `MoveX`）重叠，含 loop | 同起始时间时**先结束的命令**在重叠期优先；loop 不改变优先级 |
| [CommandOverlapTest.cs](file:///workspace/test/CommandOverlapTest.cs) | 两条 `MoveX` 重叠 + `HasOverlappedCommands` | 先结束者重叠期优先；文件序打破平局；"被完全包含的命令的结束值会泄漏到包含命令结束之后"（注释称 "Cursed osu!stable behavior"） |
| [CommandReversedTest.cs](file:///workspace/test/CommandReversedTest.cs) | `startTime > endTime` 反转命令 | 不插值，保持起始值直到反转的 start，然后跳到结束值 |
| [CommandTest.cs](file:///workspace/test/CommandTest.cs) | 布尔/参数命令（`Additive`/`FlipH`/`FlipV`），含点命令与 loop | 点命令（零时长）**锁存**（一旦为真恒为真）；范围命令瞬时；loop 按迭代偏移 |
| [LoopOffsetTest.cs](file:///workspace/test/LoopOffsetTest.cs) | loop 内非零/负起始命令 | loop 迭代按 loop 总时长平移命令时间；非零/负偏移正确解析 |
| [LoopOverlapTest.cs](file:///workspace/test/LoopOverlapTest.cs) | loop 等价于展开的普通命令；跨 channel loop 不算重叠 | 三角波 0→100→0→100→200 在 t=-1000..10000 的多个采样点一致 |
| [SpriteDisplayTimeTest.cs](file:///workspace/test/SpriteDisplayTimeTest.cs) | `CommandsStart/EndTime` vs `DisplayStart/EndTime` | 显示时间由 Fade/Scale/ScaleVec 的非零值区域并集决定，可比命令时间窄；loop 按迭代贡献并求和 |

---

### 4.5 `brewlib` — 底层框架（子模块）

`brewlib` 是 git 子模块（`https://github.com/Damnae/brewlib.git`），提供 storybrew 之下的底层能力。本仓库未检出其源码，但从引用方代码可推断其职责：

- `BrewLib.Graphics`：OpenGL 抽象（`DrawContext`/`DrawState`/`Camera`/`CameraOrtho`/`QuadRenderer`/`LineRenderer`/`TextureContainerAtlas`/`TextureContainerSeparate`/`Drawable`）
- `BrewLib.Audio`：`AudioManager`/`AudioStream`/`AudioSampleContainer`/`FftStream`（基于 BASS，故 editor 为 x86）
- `BrewLib.UserInterface`：`Widget`/`WidgetManager`/`Field`/`Slider`/`Textbox`/`Button`/`Label`/`Skin`/皮肤样式
- `BrewLib.ScreenLayers`：`ScreenLayer`/`ScreenLayerManager`
- `BrewLib.Util`：`Misc.WithRetries`/`SafeWriteStream`/`SafeDirectoryWriter`/`FrameClock`/`TimeSourceExtender`
- `BrewLib.Graphics.Drawables`：`Drawable` 基类等

> 如需构建本仓库，须先执行 `git submodule update --init brewlib`。

---

## 5. 关键流程

### 5.1 应用启动与主循环

1. `Program.Main`（`[STAThread]`）→ 分发参数（`update`/`build`）或 `startEditor`。
2. `startEditor`：`enableScheduling` → 加载 `Settings` → `Updater.NotifyEditorRun` → 找 `DisplayDevice` → 建 `GameWindow`（OpenGL 2.0）→ 建 `AudioManager` → 建 `Editor` → `editor.Initialize()` → `runMainLoop`。
3. `runMainLoop`：固定步长 update（`1/Settings.UpdateRate`，每帧最多 2 次）+ 可变步长 draw（tween）+ `RunScheduledTasks`（排空主线程队列）+ `Thread.Sleep`。每秒更新 `Stats`。
4. `Editor.Initialize`：建 `DrawContext`、注册渲染器、加载 `Skin`、建 `InputManager`/`ScreenLayerManager`、`Restart` 到 `StartMenu`。

### 5.2 脚本编译与热重载

1. `Project` 构造 `ScriptManager<StoryboardObjectGenerator>`（命名空间 `StorybrewScripts`，引用程序集 = `DefaultAssemblies` + `ImportedAssemblies`）。
2. 添加 effect → `scriptManager.Get(scriptName)` → `ScriptContainer`。
3. `Project.QueueEffectUpdate(effect)` 入队 `AsyncActionQueue<Effect>`。
4. `ScriptedEffect.Update`：
   - `scriptContainer.CreateScript()` → Roslyn 编译脚本+库到 GUID 命名 DLL（`cache/scripts`）→ 新 collectible `AssemblyLoadContext` 加载 → 解析 `StorybrewScripts.<ScriptName>` → `Activator.CreateInstance`。
   - `Loading`→`Configuring`（`UpdateConfiguration`/`ApplyConfiguration`，主线程）→`Updating`（`script.Generate(context)`）→后处理每个 layer。
   - 主线程换层 `UpdateLayers(context.EditorLayers)`。
5. 文件 watcher（脚本/库/谱面/资产）经 `ThrottledActionScheduler` 节流 → `ReloadScript` → `OnScriptChanged` → `ScriptedEffect.Refresh` → 重新入队。

### 5.3 实时预览渲染

1. `ProjectMenu.Update` 推进 `TimeSourceExtender`（音频通道或手动 seek），设 `mainStoryboardDrawable.Time`，调 `project.TriggerEvents(prevTime,time)` 触发 `EditorOsbSample`。
2. `StoryboardDrawable.Draw` 设 `project.DisplayTime = Time` → `Project.Draw` → `LayerManager.Draw` → 各可见 `EditorStoryboardLayer.Draw` → 根 `EditorStoryboardSegment.Draw`。
3. segment 跳过区间外、高亮正弦调制、组合 `StoryboardTransform`；>1000 对象用 10s 时间桶剔除。
4. `EditorOsbSprite.Draw` 采样命令（`OpacityAt`/`ScaleAt`/`PositionAt`/`RotationAt`/`ColorAt`/`AdditiveAt`/`FlipH/VAt`/`GetTexturePathAt`），解析贴图（mapset→asset），应用变换与 `DimFactor`，`DrawState.Prepare(QuadRenderer,camera,states).Draw`。Alt 左键强制不可见精灵以低透明度渲染便于编辑。
5. `FrameStats` 每帧累积，`ProjectMenu.buildWarningMessage` 转为性能警告。

### 5.4 导出 `.osb`

`Project.ExportToOsb(exportOsb=true)`（非主线程）：
- 读 `MainBeatmap.Path`/`OsbPath` 与可见 layer。
- 写按难度的 `.osu` `[Events]` 段（替换 storyboard layer 块）。
- 写共享 `.osb`，按 `//Storyboard Layer N (Name)` 分段。
- 用 `SafeWriteStream`；`EditorStoryboardSegment.WriteOsb` 用 `Parallel.For` 并行序列化。
- 未用的 `Overlay` layer 跳过。

---

## 6. 依赖关系

### 6.1 项目引用图

```
editor ──▶ common ──▶ brewlib
  │────────▶ brewlib
scripts ──▶ common
test ─────▶ common
        ──▶ brewlib
```

- `common` 引用 `brewlib`（`common.csproj` 的 `<ProjectReference Include="..\brewlib\brewlib.csproj" />`）。
- `editor` 同时引用 `brewlib` 与 `common`。
- `scripts` 仅引用 `common`（用户脚本运行时由 editor 编译，引用程序集由 `Project.DefaultAssemblies` 提供）。
- `test` 引用 `common` 与 `brewlib`。

### 6.2 NuGet 包

| 项目 | 包 |
| --- | --- |
| `common` | `OpenTK 2.0.0`、`System.Drawing.Common 8.0.8`、`System.ValueTuple 4.5.0`、`Damnae.Tiny 1.2.0` |
| `editor` | `Microsoft.CodeAnalysis.Common 4.11.0`、`Microsoft.CodeAnalysis.CSharp 4.11.0`、`Microsoft.Net.Compilers.Toolset 4.11.0`（private）、`OpenTK 2.0.0` |
| `scripts` | `OpenTK 2.0.0` |
| `test` | `coverlet.collector 6.0.0`、`Microsoft.NET.Test.Sdk 17.8.0`、`MSTest.TestAdapter 3.1.1`、`MSTest.TestFramework 3.1.1` |

### 6.3 模块间关键依赖

- **Scripting → Storyboarding**：`StoryboardObjectGenerator.GetLayer` 返回 `StoryboardLayer`，用户经 `StoryboardSegment` 工厂建 `OsbSprite`/`OsbAnimation`/`OsbSample`。
- **Storyboarding → Commands/CommandValues**：`OsbSprite` 命令方法构造 `*Command` 包装 `CommandValue`；`AddCommand` 按运行时类型分派。
- **Storyboarding → Display**：`OsbSprite` 每属性一条 `CommandTimeline<TValue>`，经谓词列表路由命令；`CommandTimeline` 用 channel 求值任意时刻值（预览+优化）。
- **Storyboarding → Util(CommandSplitter)**：`OsbSprite.WriteOsb` 在 `CommandSplitThreshold` 超限时委托 `CommandSplitter.Split`，splitter 用显示查询 API 迁移起始状态。
- **Storyboarding3d → Storyboarding/Util**：3D 节点持 `CommandGenerator`（DEBUG-only）产 `OsbSprite` 命令；`Sprite3d`/`Line3d` 等调 `StoryboardObjectGenerator.Current.GetMapsetBitmap` 量纹理。
- **Mapset → Curves**：`OsuSlider.generateCurve` 按 `SliderCurveType` 选 `BezierCurve`/`CatmullCurve`/`CircleCurve`/`CompositeCurve`。
- **Mapset → Storyboarding**：`OsuHitObject` 定义坐标常量（`StoryboardSize`/`WidescreenStoryboardBounds`）；`BeatmapExtensions.ForEachTick` 被 `Scene3d.Generate` 使用。
- **Animations → Storyboarding**：`EasingFunctions.ToEasingFunction` 映射 `OsbEasing`；`KeyframedValue` 被 `CommandGenerator`、3D 节点、`PerspectiveCamera` 使用。
- **Subtitles → Scripting/Util**：`StoryboardObjectGenerator.LoadFont` 建 `FontGenerator`；`FontGenerator` 用 `BitmapHelper`（glow/trim）与 `StreamReaderExtensions`（经 `AssParser`）。
- **Util → Storyboarding**：`StoryboardTransform` 包装 `Affine2`；`OrientedBoundingBox` 被 `OsbSprite.InScreenBounds` 与 `CommandGenerator.State.IsVisible` 使用；`ObjectSerializer` 决定哪些字段可 `[Configurable]`；`NamedValue` 支撑 `EffectConfig` 枚举可选值。

### 6.4 editor 内部关键依赖

- `Project` 拥有 `LayerManager`/`MapsetManager`/`ScriptManager`/`TextureContainer`/`AudioContainer`/`AsyncActionQueue<Effect>`。
- `ScriptedEffect` 持 `ScriptContainer`，经 `EditorGeneratorContext` 把 `EditorStoryboardLayer` 交给脚本。
- `EditorStoryboardLayer` 委托根 `EditorStoryboardSegment`，后者递归含 `EditorOsbSprite`/`EditorOsbAnimation`/`EditorOsbSample`。
- `ProjectMenu` 持 `Project` + `StoryboardDrawable`（主/预览）+ `TimelineSlider` + `EffectList`/`LayerList`/`SettingsMenu`/`EffectConfigUi`。
- 文件 watcher 链：`ScriptManager`（脚本/库）+ `MapsetManager`（谱面）+ `Project` 资产 watcher + `MultiFileWatcher`（effect 依赖）→ 全部经 `ThrottledActionScheduler` 节流 → `Program.Schedule` 回主线程。

---

## 7. 项目运行方式

### 7.1 前置条件

- **.NET 8.0 SDK**（Windows，含 .NET ref pack，路径由 `Project.GetRuntimeRefDirectory()` 推断）。`StartMenu` 会检查 ref 目录，缺失则禁用 New/Open 并提示安装 .NET 8。
- **Windows**（目标 `net8.0-windows`，editor 为 x86 以支持 BASS 音频）。
- **git 子模块**：`brewlib` 须检出。

### 7.2 克隆与构建

```bash
# 1. 克隆（含子模块）
git clone --recurse-submodules https://github.com/Damnae/storybrew.git
cd storybrew

# 若已克隆但未带子模块：
git submodule update --init brewlib

# 2. 还原与构建（Release）
dotnet restore storybrew.sln
dotnet build storybrew.sln -c Release
```

构建产物：
- `editor/bin/<Config>/net8.0-windows/StorybrewEditor.exe`（主程序）
- `common/bin/<Config>/net8.0-windows/StorybrewCommon.dll`
- `scripts/bin/<Config>/net8.0-windows/*.dll`（内置脚本，作为示例）

> `editor.csproj` 的 `Build` 配置有 `PostBuild` 目标：构建后自动运行 `StorybrewEditor.exe build` 生成发布 zip 并验证更新链路。

### 7.3 运行编辑器

```bash
# 直接运行（Debug 或 Release）
dotnet run --project editor -c Release
# 或直接执行
editor/bin/Release/net8.0-windows/StorybrewEditor.exe
```

启动后进入 `StartMenu`：
- **New project** → `NewProjectMenu`（输入名称 + 选谱面文件夹）→ `Project.Create` → `ProjectMenu`。
- **Open project** → 选 `projects/<name>/.sbrew/project.sbrew.yaml`（或 `.sbp`）→ `Project.Load` → `ProjectMenu`。
- 首次运行（`firstrun` 标记存在）会执行 `Updater.firstRun`：把 `*.exe_` 重命名为 `*.exe`、标记 `scripts/*.cs` 只读。

### 7.4 命令行参数

`StorybrewEditor.exe` 支持以下参数（见 [Program.cs](file:///workspace/editor/Program.cs) `handleArguments`）：

| 参数 | 行为 |
| --- | --- |
| （无） | 启动编辑器 |
| `update <folder> <version>` | 自更新模式：把更新包文件替换到 `<folder>`，执行 `updateData` 版本迁移，启动目标 exe |
| `build` | 发布构建模式：`Builder.Build` 生成 zip + `testUpdate` 验证 |

### 7.5 用户脚本开发流程

1. 在 `ProjectMenu` 点 "Add effect" → 从 `project.GetEffectNames()` 选内置脚本，或 "New script" 输入名称（自动 Title Case、去除非法字符、写 `scripttemplate.csx`）。
2. `ScriptManager.updateSolutionFiles` 把 `storyboard.sln`/`scripts.csproj` 写入项目 `scripts/` 文件夹，并确保 `.vscode/` 存在。
3. 点 effect 的编辑按钮 → `EffectList.openEffectEditor` 查找 VS Code/VSCodium 并以 `"<solutionFolder>" "<effect.Path>" -r` 启动（`--verbose` 若 `Settings.VerboseVsCode`）。
4. 在 VS Code 中编辑 `.cs`，继承 `StoryboardObjectGenerator`，用 `[Configurable]` 字段，重写 `Generate()`。
5. 保存 → `scriptWatcher` 触发 `ReloadScript` → `ScriptedEffect.Refresh` → 重新编译（collectible ALC 卸载旧程序集）→ 重新 `Generate` → 主线程换层 → 画布实时更新。
6. 在 `EffectConfigUi` 调整 `[Configurable]` 字段 → `effect.Refresh`。
7. `Ctrl-S` 保存项目（YAML 到 `.sbrew/`）；导出按钮写 `.osb`（右键按难度导出）。

### 7.6 运行测试

```bash
dotnet test test/test.csproj
```

测试覆盖 osu!stable 命令解析语义（重叠优先级、反转命令、点命令锁存、loop 时间偏移、显示时间计算等）。

### 7.7 配置与日志

- **`settings.cfg`**（editor 同目录）：`Settings` 持久化（`FrameRate`/`UpdateRate`/`Volume`/`FitStoryboard`/`ShowStats`/`VerboseVsCode`/`EffectThreads`/`TimeCopyFormat` 等）。
- **`logs/`**：`trace.log`（跟踪）、`exception.log`（`FirstChanceException`）、`crash.log`（`UnhandledException`）、`update.log`、`build.log`。
- **项目数据**：`projects/<name>/.sbrew/`（`index.yaml`/`user.yaml`/`effect.<guid>.yaml` + `.gitignore` + opener 文件）、`projects/<name>/scripts/`（用户脚本）、`projects/<name>/assetlibrary/`（资产）、`cache/scripts/`（编译产物）、`.cache/font/`（字体缓存）。

---

## 8. 附录

### 8.1 关键枚举速查

| 枚举 | 定义位置 | 取值 |
| --- | --- | --- |
| `OsbLayer` | [OsbSprite.cs](file:///workspace/common/Storyboarding/OsbSprite.cs) | Background, Fail, Pass, Foreground, Overlay |
| `OsbOrigin` | [OsbSprite.cs](file:///workspace/common/Storyboarding/OsbSprite.cs) | TopLeft, TopCentre, TopRight, CentreLeft, Centre, CentreRight, BottomLeft, BottomCentre, BottomRight |
| `OsbLoopType` | [OsbSprite.cs](file:///workspace/common/Storyboarding/OsbSprite.cs) | LoopForever, LoopOnce |
| `OsbEasing` | [OsbSprite.cs](file:///workspace/common/Storyboarding/OsbSprite.cs) | 35 种 osu! easing（None, Linear, Quad/Cubic/Quart/Quint In/Out/InOut, ...） |
| `ParameterType` | [OsbSprite.cs](file:///workspace/common/Storyboarding/OsbSprite.cs) | None, FlipHorizontal, FlipVertical, AdditiveBlending |
| `HitObjectFlag` | [OsuHitObject.cs](file:///workspace/common/Mapset/OsuHitObject.cs) | Circle=1, Slider=2, NewCombo=4, Spinner=8, SkipColor1/2/3=16/32/64, Hold=128 |
| `HitSoundAddition` | [OsuHitObject.cs](file:///workspace/common/Mapset/OsuHitObject.cs) | None, Normal, Whistle, Finish, Clap |
| `SampleSet` | [OsuHitObject.cs](file:///workspace/common/Mapset/OsuHitObject.cs) | None, Normal, Soft, Drum |
| `SliderCurveType` | [OsuSlider.cs](file:///workspace/common/Mapset/OsuSlider.cs) | Unknown, Linear, Catmull, Bezier, Perfect |
| `RotationMode` | [Sprite3d.cs](file:///workspace/common/Storyboarding3d/Sprite3d.cs) | Fixed, UnitX, UnitY |
| `EffectStatus` | [Effect.cs](file:///workspace/editor/Storyboarding/Effect.cs) | Initializing, Loading, Configuring, Updating, ReloadPending, Ready, CompilationFailed, LoadingFailed, ExecutionFailed, UpdateCanceled |
| `PathSelectorMode` | [PathSelector.cs](file:///workspace/editor/UserInterface/PathSelector.cs) | Folder, OpenFile, OpenDirectory, SaveFile |

### 8.2 项目常量速查

| 常量 | 值 | 来源 |
| --- | --- | --- |
| `Beatmap.ControlPointLeniency` | 5 (ms) | [Beatmap.cs](file:///workspace/common/Mapset/Beatmap.cs) |
| `OsuHitObject.PlayfieldSize` | 512×384 | [OsuHitObject.cs](file:///workspace/common/Mapset/OsuHitObject.cs) |
| `OsuHitObject.StoryboardSize` | 640×480 | 同上 |
| `OsuHitObject.WidescreenStoryboardSize` | 1366×768 | 同上 |
| `OsbSpritePool.MaxPoolDuration`（默认） | 60000 (ms) | [OsbSpritePool.cs](file:///workspace/common/Storyboarding/Util/OsbSpritePool.cs) |
| `Project.Version` | 7 | [Project.cs](file:///workspace/editor/Storyboarding/Project.cs) |
| `Project.DataFolder` | `.sbrew` | 同上 |
| `Project.TextExtension` | `.yaml` | 同上 |
| `Project.BinaryExtension` | `.sbp` | 同上 |
| `Project.ProjectsFolder` | `projects` | 同上 |
| `Updater.readOnlyVersion` | 1.8 | [Updater.cs](file:///workspace/editor/Updater.cs) |
| `ThrottledActionScheduler.Delay` | 100 (ms) | [ThrottledActionScheduler.cs](file:///workspace/editor/Util/ThrottledActionScheduler.cs) |
| `FontDescription.FontSize`（默认） | 76 | [FontGenerator.cs](file:///workspace/common/Subtitles/FontGenerator.cs) |
| `Camera.Resolution`（默认） | 1366×768 | [Camera.cs](file:///workspace/common/Storyboarding3d/Camera.cs) |

### 8.3 命令字母与 OSB 格式

| 字母 | 命令类 | 含义 |
| --- | --- | --- |
| `F` | `FadeCommand` | 淡入淡出（0..1） |
| `M` | `MoveCommand` | 2D 移动 |
| `MX`/`MY` | `MoveXCommand`/`MoveYCommand` | 单轴移动 |
| `S` | `ScaleCommand` | 等比缩放 |
| `V` | `VScaleCommand` | 双轴缩放 |
| `R` | `RotateCommand` | 旋转（弧度） |
| `C` | `ColorCommand` | 颜色（RGB 0..255） |
| `P` | `ParameterCommand` | 参数（`H`=FlipH, `V`=FlipV, `A`=Additive） |
| `L` | `LoopCommand` | 循环组 `L,<start>,<count>` |
| `T` | `TriggerCommand` | 触发组 `T,<name>,<start>,<end>,<group>` |
| `Sprite` | `OsbSprite` | `Sprite,<layer>,<origin>,"<path>",<x>,<y>` |
| `Animation` | `OsbAnimation` | `Animation,<layer>,<origin>,"<path>",<x>,<y>,<frameCount>,<frameDelay>,<loopType>` |
| `Sample` | `OsbSample` | `Sample,<time>,<layer>,"<path>",<volume>` |

### 8.4 文档与外部资源

- 官方 Wiki：<https://github.com/Damnae/storybrew/wiki/Getting-Started-%28Without-Programming%29>
- 最新发布：<https://github.com/Damnae/storybrew/releases/latest>
- `brewlib` 子模块：<https://github.com/Damnae/brewlib.git>
- Discord：<https://discord.gg/0qfFOucX93QDNVN7>

---

*本文档由源码静态分析生成，反映仓库当前磁盘状态。*
