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

`brewlib` 是 git 子模块（`https://github.com/Damnae/brewlib.git`，提交 `138c71119bbc82516aee69acfd1e99942a60fc0f`），命名空间 `BrewLib`，输出 `BrewLib.dll`。基于 **ManagedBass 1.0.2** + **ManagedBass.Fx 1.0.2**（音频）、**OpenTK 2.0.0**（OpenGL 窗口/输入）、**Damnae.Tiny 1.2.0**（YAML）、**System.Management 8.0.0**。打包 `bass.dll`/`bass_fx.dll`（x86，故 editor 为 x86）。SDK 为 `Microsoft.NET.Sdk.WindowsDesktop` + `UseWindowsForms`。

提供 storybrew 之下的全部底层能力：OpenGL 渲染、音频、输入、屏幕层、UI widget、皮肤、时间源、资源容器与大量工具。被 `common`、`editor`、`test` 引用。

#### 4.5.1 `Audio/` — 音频引擎（BASS 封装）

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [AudioManager.cs](file:///workspace/brewlib/Audio/AudioManager.cs) | `AudioManager` | 初始化 BASS（`Bass.Init`），全局 `Volume`；`LoadStream(filename)`/`LoadSample(filename)`/`CreateStream(...)`；`Update()` 回收结束的临时 channel |
| [AudioChannel.cs](file:///workspace/brewlib/Audio/AudioChannel.cs) | `AudioChannel` | BASS channel 句柄包装。`Time`/`Duration`/`Playing`/`Loop`/`Volume`/`TimeFactor`/`Pitch`/`Pan`/`GetFft(...)`；音量用 `SoundUtil.FromLinearVolume`（x^4 感知曲线） |
| [AudioChannelTimeSource.cs](file:///workspace/brewlib/Audio/AudioChannelTimeSource.cs) | `AudioChannelTimeSource` | 把 `AudioChannel` 适配为 `TimeSource`，用于音频同步计时 |
| [AudioSample.cs](file:///workspace/brewlib/Audio/AudioSample.cs) | `AudioSample` | BASS sample（`MaxSimultaneousPlayBacks=8`）；`Play()` 返回临时 `AudioChannel` |
| [AudioSampleContainer.cs](file:///workspace/brewlib/Audio/AudioSampleContainer.cs) | `AudioSampleContainer` | 按文件名缓存 `AudioSample` |
| [AudioStream.cs](file:///workspace/brewlib/Audio/AudioStream.cs) | `AudioStream` | 经 `BassFx.TempoCreate` 的可调速流（quick tempo 算法） |
| [AudioStreamPull.cs](file:///workspace/brewlib/Audio/AudioStreamPull.cs) | `AudioStreamPull` | 拉模式流，消费者提供 `CallbackDelegate(IntPtr buffer, int sampleCount)` |
| [AudioStreamPush.cs](file:///workspace/brewlib/Audio/AudioStreamPush.cs) | `AudioStreamPush` | 推模式流，消费者调 `PushData(short[], int)` |
| [FftStream.cs](file:///workspace/brewlib/Audio/FftStream.cs) | `FftStream` | 仅解码流，支持任意时刻 FFT：`GetFft(double time, bool splitChannels)` |
| [SoundUtil.cs](file:///workspace/brewlib/Audio/SoundUtil.cs) | `SoundUtil` (static) | `FromLinearVolume`（x^4）、`GetNoteFrequency`、波形生成 `Square`/`Saw`/`Sine`/`Triangle` |

**storybrew 用法**：编辑器音频播放引擎与 storyboard 音频同步（`AudioChannelTimeSource` 驱动 `TimeSourceExtender`）；`FftStream` 支撑频谱脚本；`AudioSampleContainer` 缓存音效。

#### 4.5.2 `Data/` — 资源容器

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [ResourceContainer.cs](file:///workspace/brewlib/Data/ResourceContainer.cs) | `ResourceContainer` (interface) | `ResourceNames`/`GetStream`/`GetBytes`/`GetString`/`GetWriteStream`；`ResourceSource` 标志枚举（`Embedded`/`Relative`/`Absolute`，组合 `Local`/`Any`） |
| [AssemblyResourceContainer.cs](file:///workspace/brewlib/Data/AssemblyResourceContainer.cs) | `AssemblyResourceContainer` | 从清单资源（`baseNamespace.path`）或文件系统（`basePath`）加载；剥离 UTF-8 BOM |
| [SubResourceContainer.cs](file:///workspace/brewlib/Data/SubResourceContainer.cs) | `SubResourceContainer` | 包装另一容器加路径前缀，回退到无前缀名 |

**storybrew 用法**：`Editor.Initialize` 用 `AssemblyResourceContainer` 透明加载嵌入的 shader/字体/默认皮肤/项目资源。

#### 4.5.3 `Graphics/` 顶层 — 渲染骨干

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [DrawContext.cs](file:///workspace/brewlib/Graphics/DrawContext.cs) | `DrawContext` | 轻量 DI 容器：`Get<T>()`/`Register<T>(obj, dispose)` 注册渲染器与共享服务 |
| [DrawState.cs](file:///workspace/brewlib/Graphics/DrawState.cs) | `DrawState` (static) | OpenGL 状态管理中枢。`Initialize`/`Cleanup`/`CompleteFrame`；`Renderer` 属性自动 flush；纹理绑定（`BindPrimaryTexture`/`BindTexture`/`BindTextures` + sampler 回收）；`Viewport`/`ClipRegion` + `Clip()` IDisposable；能力缓存（`HasCapabilities`/`HasExtensions`/`CheckError`）；单例 `WhitePixel`/`NormalPixel`/`TextGenerator`/`TextFontManager` |
| [GpuCommandSync.cs](file:///workspace/brewlib/Graphics/GpuCommandSync.cs) | `GpuCommandSync` | 基于 fence 的 GPU 同步：`LockRange`/`WaitForRange`/`WaitForAll`（`GL.FenceSync`/`ClientWaitSync`） |
| [RenderStates.cs](file:///workspace/brewlib/Graphics/RenderStates.cs) | `RenderState`/`RenderStates` | 复合渲染状态（BlendingFactor/BlendingEquation/Depth/CullFace/PointSprite）；`BlendingMode` 枚举：`Off`/`Alphablend`/`Color`/`Additive`/`BlendAdd`/`Premultiply`/`Premultiplied` |
| [Shader.cs](file:///workspace/brewlib/Graphics/Shader.cs) | `Shader` | 编译 vs+fs 并链接程序；`Begin`/`End`；`GetAttributeLocation`/`TryGetUniformLocation`/`HasUniform`；`SortId` 用于批合并 |
| [VertexAttribute.cs](file:///workspace/brewlib/Graphics/VertexAttribute.cs) | `VertexAttribute` | 顶点属性定义（`Name`/`Type`/`ComponentSize`/`ComponentCount`/`Normalized`/`Offset`/`Usage`）；工厂 `CreatePosition2d/3d`/`CreateNormal`/`CreateColor` 等；`AttributeUsage` 枚举 |
| [VertexDeclaration.cs](file:///workspace/brewlib/Graphics/VertexDeclaration.cs) | `VertexDeclaration` | `IEnumerable<VertexAttribute>` + `VertexSize`；`ActivateAttributes`/`DeactivateAttributes(Shader)` |

#### 4.5.4 `Graphics/Cameras/` — 相机

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Camera.cs](file:///workspace/brewlib/Graphics/Cameras/Camera.cs) | `Camera` (interface) | 输入：`Viewport`/`Position`/`Forward`/`Up`/`NearPlane`/`FarPlane`；输出：`Projection`/`View`/`ProjectionView`/`InvertedProjectionView`/`InternalViewport`/`ExtendedViewport`；`FromScreen`/`ToScreen`；`Changed` 事件 |
| [CameraBase.cs](file:///workspace/brewlib/Graphics/Cameras/CameraBase.cs) | `CameraBase` (abstract) | 懒 `Validate()`/`Invalidate()`；`FromScreen`/`ToScreen`；`LookAt`/`Rotate`；订阅 `DrawState.ViewportChanged` |
| [CameraOrtho.cs](file:///workspace/brewlib/Graphics/Cameras/CameraOrtho.cs) | `CameraOrtho` | 正交相机，`VirtualWidth`/`VirtualHeight`/`Zoom`/`HeightScaling`/`yDown`。**storybrew storyboard 画布默认相机**（640×480 虚拟空间） |
| [CameraPerspective.cs](file:///workspace/brewlib/Graphics/Cameras/CameraPerspective.cs) | `CameraPerspective` | 透视相机，`FieldOfView`（默认 67）/`NearPlaneHeight` |
| [CameraIso.cs](file:///workspace/brewlib/Graphics/Cameras/CameraIso.cs) | `CameraIso` | 等距相机，`Target` + sqrt(1/3) forward |
| [CameraExtensions.cs](file:///workspace/brewlib/Graphics/Cameras/CameraExtensions.cs) | static ext | `ToCamera` 在相机间转换 `Vector2`/`Vector3`/`Box2` |

**storybrew 用法**：`CameraOrtho` 是 storyboard 画布默认 2D 相机；`FromScreen`/`ToScreen` 支撑编辑器鼠标到画布坐标转换。

#### 4.5.5 `Graphics/Drawables/` — 可绘制对象

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Drawable.cs](file:///workspace/brewlib/Graphics/Drawables/Drawable.cs) | `Drawable` (interface) | `MinSize`/`PreferredSize`/`Draw(DrawContext, Camera, Box2, float opacity)` |
| [CompositeDrawable.cs](file:///workspace/brewlib/Graphics/Drawables/CompositeDrawable.cs) | `CompositeDrawable` | 持有 `Drawable` 列表，绘制全部子项 |
| [NullDrawable.cs](file:///workspace/brewlib/Graphics/Drawables/NullDrawable.cs) | `NullDrawable` | 单例空操作 drawable |
| [Sprite.cs](file:///workspace/brewlib/Graphics/Drawables/Sprite.cs) | `Sprite` | `Texture2dRegion` + `Rotation`/`Color`/`ScaleMode`（`None`/`Fill`/`Fit`/`Repeat`/`RepeatFit`），处理 repeat 平铺 |
| [TextDrawable.cs](file:///workspace/brewlib/Graphics/Drawables/TextDrawable.cs) | `TextDrawable` | 基于 `TextLayout` 的文本；`FontName`/`FontSize`/`MaxSize`/`Scaling`/`Alignment`/`Trimming`；导航 `GetCharacterBounds`/`GetCharacterIndexAt`/`Above`/`Below` |
| [NinePatch.cs](file:///workspace/brewlib/Graphics/Drawables/NinePatch.cs) | `NinePatch` | 九宫格缩放，`Borders`/`Outset`/`BordersOnly`；经 `QuadRenderer` 绘制中心/边/角 |

**storybrew 用法**：`Sprite`/`TextDrawable` 是屏幕对象图标、预览缩略图、widget 前景的构建块；`NinePatch` 用于皮肤化按钮/面板背景。`editor` 的 [StoryboardDrawable.cs](file:///workspace/editor/UserInterface/Drawables/StoryboardDrawable.cs) 即实现 `Drawable`。

#### 4.5.6 `Graphics/RenderTargets/` — 帧缓冲对象

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [RenderTarget.cs](file:///workspace/brewlib/Graphics/RenderTargets/RenderTarget.cs) | `RenderTarget` | FBO + 纹理 + 可选 renderbuffer；`Begin(clear)`/`End`；保存/恢复 viewport+framebuffer |
| [MultiRenderTarget.cs](file:///workspace/brewlib/Graphics/RenderTargets/MultiRenderTarget.cs) | `MultiRenderTarget` | 多 `RenderTexture` 颜色附件 + 可选 depth/stencil；`GL.DrawBuffers` MRT |
| [RenderTexture.cs](file:///workspace/brewlib/Graphics/RenderTargets/RenderTexture.cs) | `RenderTexture` | 独立纹理 + `RenderbufferStorage`；`Resize` 触发 `OnChanged` |

**storybrew 用法**：离屏合成（如把 storyboard 渲染到纹理做预览缩略图、后处理）。

#### 4.5.7 `Graphics/Renderers/` — 图元渲染器

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Renderer.cs](file:///workspace/brewlib/Graphics/Renderers/Renderer.cs) | `Renderer` (interface) | `Camera`/`BeginRendering`/`EndRendering`/`Flush(canBuffer)` |
| [QuadRenderer.cs](file:///workspace/brewlib/Graphics/Renderers/QuadRenderer.cs) | `QuadRenderer` (interface) | 扩展 `Renderer`：`Shader`/`TransformMatrix`/stats/`Draw(ref QuadPrimitive, Texture2dRegion)` |
| [QuadRendererBuffered.cs](file:///workspace/brewlib/Graphics/Renderers/QuadRendererBuffered.cs) | `QuadRendererBuffered` | 默认实现；`ShaderBuilder` 生成默认 shader（`u_combinedMatrix mat4`/`u_texture sampler2D`）；批合并最多 `maxQuadsPerBatch=4096`；`CustomTextureBinder`/`FlushAction` |
| [QuadPrimitive.cs](file:///workspace/brewlib/Graphics/Renderers/QuadPrimitive.cs) | `QuadPrimitive` (struct) | 4 顶点（x, y, u, v, color） |
| [LineRenderer.cs](file:///workspace/brewlib/Graphics/Renderers/LineRenderer.cs) / [LineRendererBuffered.cs](file:///workspace/brewlib/Graphics/Renderers/LineRendererBuffered.cs) | `LineRenderer`/`LineRendererBuffered` | 同模式，`LinePrimitive`（x, y, z, color ×2） |
| [SpriteRenderer.cs](file:///workspace/brewlib/Graphics/Renderers/SpriteRenderer.cs) / [SpriteRendererBuffered.cs](file:///workspace/brewlib/Graphics/Renderers/SpriteRendererBuffered.cs) | `SpriteRenderer`/`SpriteRendererBuffered` | 高层 sprite API（旋转/缩放/origin/纹理坐标）；`SpritePrimitive` |
| [QuadRendererExtensions.cs](file:///workspace/brewlib/Graphics/Renderers/QuadRendererExtensions.cs) | static ext | `Draw` 重载 + `DrawArc`（圆形） |

##### `Graphics/Renderers/PrimitiveStreamers/` — 顶点流策略

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [PrimitiveStreamer.cs](file:///workspace/brewlib/Graphics/Renderers/PrimitiveStreamers/PrimitiveStreamer.cs) | `PrimitiveStreamer<T>` (interface) | `Bind`/`Unbind`/`Render(primitiveType, primitives, count, drawCount, canBuffer)`；`CreatePrimitiveStreamerDelegate<T>` |
| [PrimitiveStreamerUtil.cs](file:///workspace/brewlib/Graphics/Renderers/PrimitiveStreamers/PrimitiveStreamerUtil.cs) | static | 默认工厂，按能力选 `PersistentMap` > `BufferData` > `Vbo` |
| [PrimitiveStreamerVao.cs](file:///workspace/brewlib/Graphics/Renderers/PrimitiveStreamers/PrimitiveStreamerVao.cs) | `PrimitiveStreamerVao<T>` (abstract) | VAO + VBO + 可选 IBO；按 shader `setupVertexArray` |
| [PrimitiveStreamerVbo.cs](file:///workspace/brewlib/Graphics/Renderers/PrimitiveStreamers/PrimitiveStreamerVbo.cs) | `PrimitiveStreamerVbo<T>` | GL 2.0 基线，每次 draw `BufferData` |
| [PrimitiveStreamerBufferData.cs](file:///workspace/brewlib/Graphics/Renderers/PrimitiveStreamers/PrimitiveStreamerBufferData.cs) | `PrimitiveStreamerBufferData<T>` | GL 1.5，同 `BufferData` + VAO |
| [PrimitiveStreamerPersistentMap.cs](file:///workspace/brewlib/Graphics/Renderers/PrimitiveStreamers/PrimitiveStreamerPersistentMap.cs) | `PrimitiveStreamerPersistentMap<T>` | GL 4.4 + `ARB_buffer_storage`，持久映射缓冲 + `GpuCommandSync`；满时 1.75× 扩容（上限 8MB） |

**storybrew 用法**：`QuadRendererBuffered` 绘制每个 storyboard 精灵/矩形/渐变；`SpriteRenderer` 提供动画对象用的旋转/缩放/origin API；`DrawArc` 画圆形。流策略自动选最优路径。`Editor.Initialize` 注册 `QuadRendererBuffered`/`LineRendererBuffered` 到 `DrawContext`。

#### 4.5.8 `Graphics/Shaders/` — 声明式 Shader 构建

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [ShaderBuilder.cs](file:///workspace/brewlib/Graphics/Shaders/ShaderBuilder.cs) | `ShaderBuilder` | 由 `VertexDeclaration` + vs/fs snippet 构建 shader；`AddUniform`/`AddVarying`/`AddVertexVariable`/`AddFragmentVariable`/`AddStruct`；内建 `GlPosition`/`GlPointSize`/`GlPointCoord`/`GlFragColor`/`GlFragDepth`；`MinVersion=110`；`Build(log)` 产 `Shader` |
| [ShaderContext.cs](file:///workspace/brewlib/Graphics/Shaders/ShaderContext.cs) | `ShaderContext` | 跟踪变量依赖/使用，死代码消除；`MarkUsedVariables`/`GenerateCode`；`Declare`/`Assign`/`Condition`/`Comment`/`Preprocessor` |
| [ShaderSnippet.cs](file:///workspace/brewlib/Graphics/Shaders/ShaderSnippet.cs) | `ShaderSnippet` (abstract) | `Empty` 单例；`RequiredExtensions`/`MinVersion`/`Generate`；可从 `Action<ShaderContext>` 隐式转换 |
| [ShaderType.cs](file:///workspace/brewlib/Graphics/Shaders/ShaderType.cs) | `ShaderType` | 结构体定义，`AddField`/`FieldAsVariable` |
| [ShaderVariable.cs](file:///workspace/brewlib/Graphics/Shaders/ShaderVariable.cs) | `ShaderVariable` | 命名变量 + `Reference`/`Assign`/`RecordDependency` |
| [ShaderFieldVariable.cs](file:///workspace/brewlib/Graphics/Shaders/ShaderFieldVariable.cs) | `ShaderFieldVariable` | 字段访问变量（`base.field`） |
| [ProgramScope.cs](file:///workspace/brewlib/Graphics/Shaders/ProgramScope.cs) | `ProgramScope` | 管理 struct/uniform/varying；`DeclareTypes`/`DeclareUniforms`/`DeclareVaryings`（仅已用） |
| [ShaderPartScope.cs](file:///workspace/brewlib/Graphics/Shaders/ShaderPartScope.cs) | `ShaderPartScope` | 每 shader 阶段（vs/fs）变量 |

##### `Graphics/Shaders/Snippets/`

| 文件 | 类型 | 说明 |
| --- | --- | --- |
| [Assign.cs](file:///workspace/brewlib/Graphics/Shaders/Snippets/Assign.cs) | `Assign` | `result = expression` |
| [Condition.cs](file:///workspace/brewlib/Graphics/Shaders/Snippets/Condition.cs) | `Condition` | `if`/`else`，继承扩展/版本 |
| [CustomSnippet.cs](file:///workspace/brewlib/Graphics/Shaders/Snippets/CustomSnippet.cs) | `CustomSnippet` | 包装 `Action<ShaderContext>` |
| [Discard.cs](file:///workspace/brewlib/Graphics/Shaders/Snippets/Discard.cs) | `Discard` | `discard;` |
| [Sequence.cs](file:///workspace/brewlib/Graphics/Shaders/Snippets/Sequence.cs) | `Sequence` | 组合多 snippet |
| [TextureSampling.cs](file:///workspace/brewlib/Graphics/Shaders/Snippets/TextureSampling.cs) | `TextureSampling` | `result = texture2D(sampler, coord)` |

**storybrew 用法**：shader 构建器让 storybrew 声明式组合 shader（加 tint/flipbook/additive 等 pass）而无需写裸 GLSL。每个 buffered renderer 用 `ShaderBuilder` 生成默认程序。

#### 4.5.9 `Graphics/Text/` — 文本渲染

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [TextFont.cs](file:///workspace/brewlib/Graphics/Text/TextFont.cs) | `TextFont` (interface) | `Name`/`Size`/`LineHeight`/`GetGlyph(char)` |
| [FontGlyph.cs](file:///workspace/brewlib/Graphics/Text/FontGlyph.cs) | `FontGlyph` | `Texture2dRegion` + `Width`/`Height`/`Size`；纹理为 null 时 `IsEmpty` |
| [TextFontAtlased.cs](file:///workspace/brewlib/Graphics/Text/TextFontAtlased.cs) | `TextFontAtlased` | 按需生成字形到 `TextureMultiAtlas2d`（512×512）；空白字形纹理为 null |
| [TextFontManager.cs](file:///workspace/brewlib/Graphics/Text/TextFontManager.cs) | `TextFontManager` | 引用计数字体缓存，键 `"name|size|scaling"`；返回 `TextFontProxy` |
| [TextFontProxy.cs](file:///workspace/brewlib/Graphics/Text/TextFontProxy.cs) | `TextFontProxy` | 引用计数代理，委托底层 `TextFont` |
| [TextGenerator.cs](file:///workspace/brewlib/Graphics/Text/TextGenerator.cs) | `TextGenerator` | GDI+ 文本渲染 + `PrivateFontCollection`（嵌入字体）；LRU 缓存（max 64，驱逐到 32）；`CreateBitmap`/`CreateTexture`；阴影画笔做描边 |
| [TextLayout.cs](file:///workspace/brewlib/Graphics/Text/TextLayout.cs) | `TextLayout` | 经 `LineBreaker` 换行；类型 `TextLayoutLine`/`TextLayoutGlyph`；导航 `GetCharacterIndexAt`/`ForTextBounds`/`Above`/`Below` |

**storybrew 用法**：所有屏幕文本（对象标签、时间线、属性面板、文本框）经 `TextFontManager` → `TextFontAtlased` → `TextLayout`。`TextGenerator` 为嵌入字体提供 GDI+ 栅格化桥接。

#### 4.5.10 `Graphics/Textures/` — 纹理管理

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Texture.cs](file:///workspace/brewlib/Graphics/Textures/Texture.cs) | `Texture` (interface) | `Description`/`BindableTexture` |
| [BindableTexture.cs](file:///workspace/brewlib/Graphics/Textures/BindableTexture.cs) | `BindableTexture` (interface) | `TextureId`/`TexturingMode` |
| [Texture2d.cs](file:///workspace/brewlib/Graphics/Textures/Texture2d.cs) | `Texture2d` | 具体 GL 纹理；静态 `Load`/`LoadBitmap`/`Create`/`LoadTextureOptions`；`Update(bitmap, x, y, options)`；`Dispose` 删 GL 纹理 |
| [Texture2dRegion.cs](file:///workspace/brewlib/Graphics/Textures/Texture2dRegion.cs) | `Texture2dRegion` | `Texture2d` 子区域 + `Box2` bounds；`UvBounds`/`UvRatio` |
| [TextureAtlas2d.cs](file:///workspace/brewlib/Graphics/Textures/TextureAtlas2d.cs) | `TextureAtlas2d` | 打包器（`currentX`/`currentY`/`nextY`）；`FillRatio`；`AddRegion` 无空间返回 null |
| [TextureMultiAtlas2d.cs](file:///workspace/brewlib/Graphics/Textures/TextureMultiAtlas2d.cs) | `TextureMultiAtlas2d` | 多 `TextureAtlas2d` 栈；满时自动 push 新图集；超大纹理单独存 |
| [TextureContainer.cs](file:///workspace/brewlib/Graphics/Textures/TextureContainer.cs) | `TextureContainer` (interface) | `ResourceNames`/`UncompressedMemoryUseMb`/`ResourceLoaded` 事件/`Get(filename)` |
| [TextureContainerAtlas.cs](file:///workspace/brewlib/Graphics/Textures/TextureContainerAtlas.cs) | `TextureContainerAtlas` | 按 `TextureOptions` 分组到不同 `TextureMultiAtlas2d` |
| [TextureContainerSeparate.cs](file:///workspace/brewlib/Graphics/Textures/TextureContainerSeparate.cs) | `TextureContainerSeparate` | 每文件一个 `Texture2d` |
| [TextureOptions.cs](file:///workspace/brewlib/Graphics/Textures/TextureOptions.cs) | `TextureOptions` | `Srgb`/`PreMultiply`/`GenerateMipmaps` + filter/wrap；从 JSON sidecar（`filename-opt.json`）加载；反射字段解析 |

**storybrew 用法**：纹理图集系统对性能至关重要——每个精灵、字形、皮肤图经 `TextureContainerAtlas` 打包到共享图集。`TextureOptions` sidecar 让 storybrew 指定每纹理 filter/wrap（如 repeat 平铺背景）。`Project` 持 `TextureContainerSeparate` 实例。

#### 4.5.11 `Input/` — 输入管理

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [InputHandler.cs](file:///workspace/brewlib/Input/InputHandler.cs) | `InputHandler` (interface) | `OnFocusChanged`/`OnClickDown`/`Up`/`OnMouseWheel`/`OnMouseMove`/`OnKeyDown`/`Up`/`OnKeyPress`/`OnGamepadConnected`/`OnButtonDown`/`Up`（bool 返回表示是否消费） |
| [InputAdapter.cs](file:///workspace/brewlib/Input/InputAdapter.cs) | `InputAdapter` (abstract) | 空操作基类，供子类化 |
| [InputManager.cs](file:///workspace/brewlib/Input/InputManager.cs) | `InputManager` | 包装 `GameWindow` 事件；`Mouse`/`Keyboard` 设备；`Control`/`Shift`/`Alt` + `*Only` 辅助；手柄管理；滚轮去重 |
| [InputDispatcher.cs](file:///workspace/brewlib/Input/InputDispatcher.cs) | `InputDispatcher` | 广播到 `InputHandler` 列表；bool 事件在首个返回 true 的 handler 停止 |
| [GamepadManager.cs](file:///workspace/brewlib/Input/GamepadManager.cs) | `GamepadManager` | 轮询 `GamePadState`；断开时键盘回退；deadzone；`GamepadButton` 标志枚举（25 按钮）；`OnConnected`/`OnButtonDown`/`OnButtonUp` |

**storybrew 用法**：`InputManager` 是接到 OpenTK `GameWindow` 的单一入口；`InputDispatcher` 是 `WidgetManager`/`ScreenLayerManager` 注册接收冒泡输入的渠道。bool 返回模式支持输入捕获（如模态对话框吃掉点击）。

#### 4.5.12 `ScreenLayers/` — 屏幕层栈

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [ScreenLayer.cs](file:///workspace/brewlib/ScreenLayers/ScreenLayer.cs) | `ScreenLayer` (abstract) | `InputAdapter` + `IDisposable`。`State` 枚举：`Hidden`/`FadingIn`/`Active`/`FadingOut`；`TransitionIn`/`OutDuration`；`Update(isTopFocus, isCovered)` 驱动状态机；生命周期 `OnStart`/`OnTransitionIn`/`Out`/`OnActive`/`OnHidden`/`OnExit`；`Exit(skipTransition)`；默认 Escape 关闭；持 `InputDispatcher` |
| [ScreenLayerManager.cs](file:///workspace/brewlib/ScreenLayers/ScreenLayerManager.cs) | `ScreenLayerManager` | 层栈 + 焦点管理；`Add`/`Set`/`Remove`/`Close`/`Exit`；`Update(isFixedRateUpdate)` 处理焦点转换 + popup/covered 逻辑；`Draw` 带 tween；栈空时退出窗口 |

**storybrew 用法**：storybrew 的主编辑器、对话框、storyboard 预览各为 `ScreenLayer` 子类（`UiScreenLayer`）。栈式 manager 提供模态对话框行为（如偏好设置覆盖但不替换编辑器）。

#### 4.5.13 `Time/` — 时间源

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [TimeSource.cs](file:///workspace/brewlib/Time/TimeSource.cs) | `ReadOnlyTimeSource`/`TimeSource` | 只读：`Current`/`TimeFactor`/`Playing`；可变增加 setter + `Seek` |
| [Clock.cs](file:///workspace/brewlib/Time/Clock.cs) | `Clock : TimeSource` | 基于 `Stopwatch`，调 `TimeFactor` 时保持当前时间 |
| [FrameClock.cs](file:///workspace/brewlib/Time/FrameClock.cs) | `FrameClock` (`FrameTimeSource`) | `Current`/`Previous`/`Elapsed`；`AdvanceFrame(duration)`/`AdvanceFrameTo(time)`/`Reset`；`Changed` 事件 |
| [TimeSourceExtender.cs](file:///workspace/brewlib/Time/TimeSourceExtender.cs) | `TimeSourceExtender` | 用 `Clock` 包装 `TimeSource`；源暂停时回退到 clock；`Update()` 同步 clock 到源 |

**storybrew 用法**：`AudioChannelTimeSource`（Audio 模块）被 `TimeSourceExtender` 包装，使编辑器时间线在音频暂停/停止时仍能（经回退 clock）前进。`FrameClock` 驱动每帧 delta 计算（`Editor.TimeSource`）。

#### 4.5.14 `UserInterface/` — Widget 框架

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Widget.cs](file:///workspace/brewlib/UserInterface/Widget.cs) | `Widget` | **基类**。身份/父子：`Id`/`Manager`/`Displayed`/`Visible`/`Hoverable`/`ClipChildren`/`Opacity`/`StyleName`/`Background`/`Foreground`/`Tooltip`/`Add`/`Remove`/`HasAncestor`/`Descendant`。布局：`Offset`/`Size`/`AbsolutePosition`/`Bounds`/`AnchorTarget`/`AnchorFrom`/`AnchorTo`/`MinSize`/`MaxSize`/`PreferredSize`/`Pack`/`InvalidateLayout`/`ValidateLayout`。事件：`OnClickDown`/`Up`/`OnClickMove`/`OnMouseWheel`/`OnKeyDown`/`Up`/`OnKeyPress`/`OnHovered`/`OnFocusChange`/`OnGamepadButtonDown`/`Up`（`HandleableWidgetEventHandler<T>`）。拖放：`GetDragData`/`HandleDrop`。样式：`RefreshStyle`/`ApplyStyle`/`BuildStyleName` |
| [WidgetManager.cs](file:///workspace/brewlib/UserInterface/WidgetManager.cs) | `WidgetManager` | `InputHandler` + `IDisposable`。根 widget + tooltip 覆盖；`HoveredWidget`/`KeyboardFocus`；`Camera` + `Changed`；`RefreshHover`；tooltip 注册；锚定迭代（max 8）；`SnapToPixel`；拖放状态；输入冒泡分发 |
| [WidgetEvent.cs](file:///workspace/brewlib/UserInterface/WidgetEvent.cs) | `WidgetEvent` 等 | `Target`/`RelatedTarget`/`Listener`/`Handled`；`WidgetHoveredEventArgs`/`WidgetFocusEventArgs` |
| [Button.cs](file:///workspace/brewlib/UserInterface/Button.cs) | `Button : Widget, Field` | `Label` 子 + `ClickBehavior`；`Text`/`Icon`/`Padding`/`Checkable`/`Checked`/`Disabled`；`OnClick`/`OnValueChanged`；hover/pressed/disabled 样式 |
| [Label.cs](file:///workspace/brewlib/UserInterface/Label.cs) | `Label : Widget` | `TextDrawable` 包装；`Text`/`Icon`/`TextBounds`；`GetCharacterBounds`/`ForTextBounds`/`GetCharacterIndexAt`/`Above`/`Below` |
| [Textbox.cs](file:///workspace/brewlib/UserInterface/Textbox.cs) | `Textbox : Widget, Field` | `Label` + 内容 + 光标行；`cursorPosition`/`selectionStart`/`SelectionLeft`/`Right`/`Length`；`Value`/`SetValueSilent`/`AcceptMultiline`/`EnterCommits`；完整键盘处理（BackSpace/Delete/A/C/V/X/Left/Right/Up/Down/Home/End/Enter）；`OnValueChanged`/`OnValueCommited`；剪贴板 |
| [Slider.cs](file:///workspace/brewlib/UserInterface/Slider.cs) | `Slider : ProgressBar` | `Step`/`Disabled`；拖拽；`GetValueForPosition`/`OnValueCommited`；虚 `DragStart`/`Update`/`End` |
| [Image.cs](file:///workspace/brewlib/UserInterface/Image.cs) | `Image : Widget` | `Sprite` 包装；`Texture`；`PreferredSize` 取自 sprite |
| [ProgressBar.cs](file:///workspace/brewlib/UserInterface/ProgressBar.cs) | `ProgressBar : Widget, Field` | bar `Drawable`；`MinValue`/`MaxValue`/`Value`/`SetValueSilent`/`OnValueChanged` |
| [LinearLayout.cs](file:///workspace/brewlib/UserInterface/LinearLayout.cs) | `LinearLayout` | 水平/垂直；`Spacing`/`Padding`/`FitChildren`/`Fill`；空间分配算法尊重 `MinSize`/`MaxSize`/`CanGrow`；虚 `PlaceChildren` |
| [StackLayout.cs](file:///workspace/brewlib/UserInterface/StackLayout.cs) | `StackLayout` | 单子适配；`FitChildren`；虚 `PlaceChildren` |
| [FlowLayout.cs](file:///workspace/brewlib/UserInterface/FlowLayout.cs) | `FlowLayout` | 换行布局；`Spacing`/`LineSpacing`/`Padding`/`FitChildren`/`Fill`；逐行测量 + 行内垂直对齐 |
| [ScrollArea.cs](file:///workspace/brewlib/UserInterface/ScrollArea.cs) | `ScrollArea` | `ClipChildren` 容器 + `StackLayout`；`ScrollsVertically`/`Horizontally`/`ScrollableX`/`Y`；滚动指示器；拖拽 + 滚轮 |
| [Field.cs](file:///workspace/brewlib/UserInterface/Field.cs) | `Field` (interface) | `FieldValue` + `OnValueChanged` + `OnDisposed` |
| [ClickBehavior.cs](file:///workspace/brewlib/UserInterface/ClickBehavior.cs) | `ClickBehavior` | 跟踪 `Hovered`/`Pressed`/`Disabled`；`OnStateChanged`/`OnClick`；按下需同键释放 |
| [DrawableContainer.cs](file:///workspace/brewlib/UserInterface/DrawableContainer.cs) | `DrawableContainer` | 持 `Drawable` 的 widget；`SetFromSkin(name)` |

##### `UserInterface/Skinning/`

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Skinning/Skin.cs](file:///workspace/brewlib/UserInterface/Skinning/Skin.cs) | `Skin` | `TextureContainer` + drawables/styles 字典；`GetDrawable`/`GetStyle<T>` 含隐式父样式解析（`"default #hover"` → `"default"`）；加载 JSON（含 includes/constants）；类型解析委托；字段解析器（string/float/int/bool/`Texture2dRegion`/`Drawable`/`Vector2`/`Color4`/`FourSide`） |

##### `UserInterface/Skinning/Styles/`

| 文件 | 类型 | 说明 |
| --- | --- | --- |
| [WidgetStyle.cs](file:///workspace/brewlib/UserInterface/Skinning/Styles/WidgetStyle.cs) | `WidgetStyle` | `Background`/`Foreground` Drawables |
| [ButtonStyle.cs](file:///workspace/brewlib/UserInterface/Skinning/Styles/ButtonStyle.cs) | `ButtonStyle` | `Padding`/`LabelStyle`/`LabelOffset` |
| [LabelStyle.cs](file:///workspace/brewlib/UserInterface/Skinning/Styles/LabelStyle.cs) | `LabelStyle` | `FontName`/`FontSize`/`TextAlignment`/`Trimming`/`Color` |
| [ImageStyle.cs](file:///workspace/brewlib/UserInterface/Skinning/Styles/ImageStyle.cs) | `ImageStyle` | `Color`/`ScaleMode` |
| [LinearLayoutStyle.cs](file:///workspace/brewlib/UserInterface/Skinning/Styles/LinearLayoutStyle.cs) | `LinearLayoutStyle` | `Spacing` |
| [ProgressBarStyle.cs](file:///workspace/brewlib/UserInterface/Skinning/Styles/ProgressBarStyle.cs) | `ProgressBarStyle` | `Bar`(Drawable)/`Height` |
| [StackLayoutStyle.cs](file:///workspace/brewlib/UserInterface/Skinning/Styles/StackLayoutStyle.cs) | `StackLayoutStyle` | 空，继承 `WidgetStyle` |
| [TextboxStyle.cs](file:///workspace/brewlib/UserInterface/Skinning/Styles/TextboxStyle.cs) | `TextboxStyle` | `LabelStyle`/`ContentStyle` |

**storybrew 用法**：编辑器每个面板、对话框、按钮、滑块、文本框都建在此 widget 树上。`Skin` 提供 JSON 驱动主题（用户可换肤）；`LinearLayout`/`FlowLayout`/`StackLayout` 是属性面板布局原语；`ScrollArea` 包裹长列表（对象列表、图层列表）。`editor` 的 `HsbColorPicker`/`Selectbox`/`PathSelector`/`Vector2Picker`/`TimelineSlider` 等均继承 `Widget` 并实现 `Field`。

#### 4.5.15 `Util/` — 通用工具

| 文件 | 关键类型 | 说明 |
| --- | --- | --- |
| [Misc.cs](file:///workspace/brewlib/Util/Misc.cs) | `Misc` (static) | `WithRetries(action, timeout, canThrow)` 指数退避重试 |
| [SafeWriteStream.cs](file:///workspace/brewlib/Util/SafeWriteStream.cs) | `SafeWriteStream` | 写 `.tmp` 的 `FileStream`；`Commit()` 后 `File.Replace`/`Move` |
| [SafeDirectoryWriter.cs](file:///workspace/brewlib/Util/SafeDirectoryWriter.cs) | `SafeDirectoryWriter` | 临时目录 + 备份目录，`Commit` 时交换 |
| [SafeDirectoryReader.cs](file:///workspace/brewlib/Util/SafeDirectoryReader.cs) | `SafeDirectoryReader` | 从目标或 `.bak` 备份读取 |
| [ByteCounterStream.cs](file:///workspace/brewlib/Util/ByteCounterStream.cs) | `ByteCounterStream` | 只写计数流 |
| [PathHelper.cs](file:///workspace/brewlib/Util/PathHelper.cs) | `PathHelper` (static) | `WithPlatformSeparators`/`WithStandardSeparators`(`/`)/`FolderContainsPath`/`GetRelativePath`/`IsValidPath`/`IsValidFilename` |
| [Native.cs](file:///workspace/brewlib/Util/Native.cs) | `Native` (static) | P/Invoke `memcpy`/`memset`/`memcmp` + user32（`SwitchToThisWindow`/`EnumThreadWindows`/`GetWindowText`）；`FindProcessWindow` 按标题找窗口 |
| [MathUtil.cs](file:///workspace/brewlib/Util/MathUtil.cs) | `MathUtil` (static) | `FloatEquals`/`DoubleEquals`/`NextPowerOfTwo`/`ShortestAngleDelta` |
| [VectorExtensions.cs](file:///workspace/brewlib/Util/VectorExtensions.cs) | static ext | `Round`/`ClampLength`/`Project`/`Side`（`Vector2`/`Vector3`） |
| [ColorExtensions.cs](file:///workspace/brewlib/Util/ColorExtensions.cs) | static ext | `Multiply`/`ToRgba`/`ToColor4`/`Lerp`/`LerpColor`/`ToHsba`/`WithOpacity`/`Premultiply`/`ToLinear`/`ToSrgb` |
| [StringHelper.cs](file:///workspace/brewlib/Util/StringHelper.cs) | `StringHelper` (static) | `ToByteSize`（b/kb/mb/gb/tb） |
| [StringExtensions.cs](file:///workspace/brewlib/Util/StringExtensions.cs) | static ext | `StripUtf8Bom`/`PrettifyDashSeparated`（TitleCase + dash→space） |
| [DateTimeExtensions.cs](file:///workspace/brewlib/Util/DateTimeExtensions.cs) | static ext | `ToTimeAgo`（"a minute ago"/"yesterday" 等） |
| [LineBreaker.cs](file:///workspace/brewlib/Util/LineBreaker.cs) | `LineBreaker` | Unicode TR14 换行；`Breakability`（`Opportunity`/`Allowed`/`Prohibited`）；字符表 `breakOpportunityAfter`/`Before`/`Prohibited`/`causesBreakAfter` |
| [TraceLogger.cs](file:///workspace/brewlib/Util/TraceLogger.cs) | `TraceLogger` | 写文件的 `TraceListener`（带锁） |
| [ChangedHandler.cs](file:///workspace/brewlib/Util/ChangedHandler.cs) | `ChangedHandler`/`ChangedEventArgs` | `PropertyName` + `All` 单例；`ChangedHandler` 委托 |
| [EventHelper.cs](file:///workspace/brewlib/Util/EventHelper.cs) | `EventHelper` (static) | `InvokeStrict` 跳过调用期间被移除的委托 |
| [ActionDisposable.cs](file:///workspace/brewlib/Util/ActionDisposable.cs) | `ActionDisposable` | 包装 `Action` 的 `IDisposable` |
| [ClipboardHelper.cs](file:///workspace/brewlib/Util/ClipboardHelper.cs) | `ClipboardHelper` (static) | `SetText`/`GetText`/`SetData`/`GetData`（500ms 重试） |
| [HashHelper.cs](file:///workspace/brewlib/Util/HashHelper.cs) | `HashHelper` (static) | `GetMd5(string/byte[])`/`GetFileMd5`/`GetFileMd5Bytes` |
| [BoxAlignment.cs](file:///workspace/brewlib/Util/BoxAlignment.cs) | `BoxAlignment` (flags enum) | `Centre=0`/`Top=1`/`Bottom=2`/`Right=4`/`Left=8` + 组合 + `Vertical`/`Horizontal` |
| [FourSide.cs](file:///workspace/brewlib/Util/FourSide.cs) | `FourSide`/`FourSide<T>` (struct) | `Top`/`Right`/`Bottom`/`Left` + `Horizontal`/`Vertical`；`GetHorizontalOffset`/`GetVerticalOffset`/`GetOffset(BoxAlignment)` |
| [ScaleMode.cs](file:///workspace/brewlib/Util/ScaleMode.cs) | `ScaleMode` (enum) | `None`/`Fill`/`Fit`/`Repeat`/`RepeatFit` |
| [Line.cs](file:///workspace/brewlib/Util/Line.cs) | `Line` (struct) | `Start`/`End`（`Vector2`） |
| [BitmapHelper.cs](file:///workspace/brewlib/Util/BitmapHelper.cs) | static | `Blur`/`Premultiply`/`CalculateGaussianKernel`/`Convolute`/`ConvoluteAlpha`/`FindTransparencyBounds`；内 `PinnedBitmap`（`GCHandle`） |
| [VectorHelper.cs](file:///workspace/brewlib/Util/VectorHelper.cs) | static | `FromPolar`/`GetAngle`/`SegmentClosestPoint`/`SegmentsIntersect` |
| [GameWindowExtensions.cs](file:///workspace/brewlib/Util/GameWindowExtensions.cs) | static ext | `GetWindowHandle`（`Native.FindProcessWindow` 回退） |
| [IconFont.cs](file:///workspace/brewlib/Util/IconFont.cs) | `IconFont` (enum) | FontAwesome unicode 码点（`Adjust=0xf042` 起） |
| [ZipArchiveExtensions.cs](file:///workspace/brewlib/Util/ZipArchiveExtensions.cs) | static ext | `ExtractToDirectoryOverwrite` |
| [ListExtensions.cs](file:///workspace/brewlib/Util/ListExtensions.cs) | static ext | `Move<T>(from, to)` |

**storybrew 用法**：安全 I/O 三件套（`SafeWriteStream`/`SafeDirectoryWriter`/`SafeDirectoryReader`）保护项目文件写入防崩溃损坏——是 storybrew 保存逻辑的核心。`LineBreaker` 支撑 `TextLayout` 换行。`IconFont` 提供编辑器 UI 全部图标字形。`ColorExtensions`（`Premultiply`/`ToLinear`/`ToSrgb`）处理纹理加载与 shader 输出的色彩空间转换。`PathHelper` 标准化跨平台路径。`ChangedHandler`/`ChangedEventArgs` 被 `EditorStoryboardLayer.OnChanged` 等使用。

#### 4.5.16 brewlib 横切关注点

- **依赖注入**：`DrawContext` 是每帧 DI 作用域，渲染器经它注册/检索。
- **状态管理**：`DrawState`（静态）是 GL 状态唯一真相源，能力检测驱动 `PrimitiveStreamerUtil` 的流策略选择。
- **资源生命周期**：`TextFontManager`/`TextFontProxy` 引用计数；`IDisposable` 模式遍布（`AudioChannel`/`Texture2d`/`RenderTarget`/`WidgetManager`/`ScreenLayer`）。
- **主题化**：`Skin` JSON（含 includes/constants/父样式解析）是唯一主题机制；每个 widget 经 `BuildStyleName` + `ApplyStyle` 参与。
- **计时**：`TimeSource` → `TimeSourceExtender` → `AudioChannelTimeSource` 链保持 storyboard 画布、音频播放、动画时间线同步。

> 如需构建本仓库，须先执行 `git submodule update --init brewlib`（已完成，提交 `138c711`）。

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
| `brewlib` | `ManagedBass 1.0.2`、`ManagedBass.Fx 1.0.2`、`OpenTK 2.0.0`、`Damnae.Tiny 1.2.0`、`System.Management 8.0.0`（+ 打包 `bass.dll`/`bass_fx.dll`） |
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
- **editor → brewlib.Graphics**：`Editor.Initialize` 注册 `TextureContainerAtlas`(1024×1024)/`QuadRendererBuffered`/`LineRendererBuffered` 到 `DrawContext`；`EditorOsbSprite.Draw` 经 `DrawState.Prepare(QuadRenderer, camera, states).Draw` 渲染；`StoryboardDrawable` 实现 `Drawable`；`UiScreenLayer` 用 `CameraOrtho`。
- **editor → brewlib.UserInterface**：`UiScreenLayer.Load` 建 `WidgetManager`；`EffectList`/`LayerList`/`SettingsMenu`/`EffectConfigUi`/`TimelineSlider`/`HsbColorPicker`/`Selectbox`/`PathSelector`/`Vector2Picker`/`Vector3Picker` 均继承 `Widget`（部分实现 `Field`）；`Editor.Initialize` 加载 `Skin`（`skin.json` + includes）。
- **editor → brewlib.Audio**：`Program.AudioManager`（`AudioManager`）；`ProjectMenu` 用 `AudioStream` + `AudioChannelTimeSource` + `TimeSourceExtender` 驱动时间线；`EditorGeneratorContext` 用 `FftStream` 提供频谱；`EditorOsbSample.TriggerEvent` 用 `AudioSampleContainer` 播音效。
- **editor → brewlib.ScreenLayers**：`Editor` 持 `ScreenLayerManager`；`StartMenu`/`NewProjectMenu`/`ProjectMenu`/`UpdateMenu`/`ReferencedAssemblyConfig` 及 `ContextMenu`/`LoadingScreen`/`MessageBox`/`PromptBox` 均为 `UiScreenLayer : ScreenLayer`。
- **editor → brewlib.Time**：`Editor.TimeSource` 为 `FrameClock`；`ProjectMenu` 用 `TimeSourceExtender` 包装音频时间源。
- **editor → brewlib.Data**：`Editor.Initialize` 用 `AssemblyResourceContainer` 加载嵌入资源（皮肤/字体/shader/项目模板）。
- **editor → brewlib.Util**：`Project.Save` 用 `SafeDirectoryWriter`/`SafeWriteStream`；`Project.ExportToOsb` 用 `SafeWriteStream`；`EffectConfigUi` 剪贴板用 `ClipboardHelper`；`EffectList` 图标用 `IconFont`；`EditorStoryboardLayer.OnChanged` 用 `ChangedHandler`/`ChangedEventArgs`；`MapsetManager`/`ScriptManager`/`MultiFileWatcher` 用 `ThrottledActionScheduler`（editor 自有，但模式源自 brewlib）。
- **common → brewlib.Util**：`common/Util/Misc.cs` 直接包装 `BrewLib.Util.Misc.WithRetries`。
- **common → brewlib.Graphics.Textures**：`common` 的 `OsbSprite`/`StoryboardObjectGenerator.GetMapsetBitmap` 间接经 editor 的 `TextureContainer` 使用 brewlib 纹理抽象。

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
| `BlendingMode` | [RenderStates.cs](file:///workspace/brewlib/Graphics/RenderStates.cs) | Off, Alphablend, Color, Additive, BlendAdd, Premultiply, Premultiplied |
| `ScaleMode` | [ScaleMode.cs](file:///workspace/brewlib/Util/ScaleMode.cs) | None, Fill, Fit, Repeat, RepeatFit |
| `BoxAlignment` | [BoxAlignment.cs](file:///workspace/brewlib/Util/BoxAlignment.cs) | Centre=0, Top=1, Bottom=2, Right=4, Left=8（flags，含组合） |
| `ScreenLayer.State` | [ScreenLayer.cs](file:///workspace/brewlib/ScreenLayers/ScreenLayer.cs) | Hidden, FadingIn, Active, FadingOut |
| `ResourceSource` | [ResourceContainer.cs](file:///workspace/brewlib/Data/ResourceContainer.cs) | Embedded, Relative, Absolute（flags；组合 `Local`/`Any`） |
| `GamepadButton` | [GamepadManager.cs](file:///workspace/brewlib/Input/GamepadManager.cs) | 25 个手柄按钮（flags） |
| `AttributeUsage` | [VertexAttribute.cs](file:///workspace/brewlib/Graphics/VertexAttribute.cs) | 顶点属性用途分类 |
| `Breakability` | [LineBreaker.cs](file:///workspace/brewlib/Util/LineBreaker.cs) | Opportunity, Allowed, Prohibited |

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
| `AudioSample.MaxSimultaneousPlayBacks` | 8 | [AudioSample.cs](file:///workspace/brewlib/Audio/AudioSample.cs) |
| `QuadRendererBuffered.maxQuadsPerBatch` | 4096 | [QuadRendererBuffered.cs](file:///workspace/brewlib/Graphics/Renderers/QuadRendererBuffered.cs) |
| `TextFontAtlased` 图集尺寸 | 512×512 | [TextFontAtlased.cs](file:///workspace/brewlib/Graphics/Text/TextFontAtlased.cs) |
| `TextGenerator` LRU 缓存 | max 64，驱逐到 32 | [TextGenerator.cs](file:///workspace/brewlib/Graphics/Text/TextGenerator.cs) |
| `PrimitiveStreamerPersistentMap` 扩容 | 1.75×，上限 8MB | [PrimitiveStreamerPersistentMap.cs](file:///workspace/brewlib/Graphics/Renderers/PrimitiveStreamers/PrimitiveStreamerPersistentMap.cs) |
| `ShaderBuilder.MinVersion` | 110 | [ShaderBuilder.cs](file:///workspace/brewlib/Graphics/Shaders/ShaderBuilder.cs) |
| `CameraPerspective.FieldOfView`（默认） | 67 | [CameraPerspective.cs](file:///workspace/brewlib/Graphics/Cameras/CameraPerspective.cs) |
| `ClipboardHelper` 重试时长 | 500 (ms) | [ClipboardHelper.cs](file:///workspace/brewlib/Util/ClipboardHelper.cs) |
| `WidgetManager` 锚定迭代上限 | 8 | [WidgetManager.cs](file:///workspace/brewlib/UserInterface/WidgetManager.cs) |

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

*本文档由源码静态分析生成，反映仓库当前磁盘状态（含已检出的 `brewlib` 子模块，提交 `138c71119bbc82516aee69acfd1e99942a60fc0f`）。*
