# Tasks

> 实施顺序遵循设计文档 §17。Phase 1A 矩阵已在设计文档中产出，此处实现为代码。

## Phase 1A: Invariants Matrix 代码实现

- [x] Task 1: 创建 VisualCompositor.Core 项目骨架与依赖方向
  - [x] SubTask 1.1: 创建 `visualcompositor/core/VisualCompositor.Core.csproj`（net8.0，无 UI/渲染依赖，仅引用 System.Text.Json）
  - [x] SubTask 1.2: 在 `storybrew.sln` 添加 `VisualCompositor.Core` 项目
  - [x] SubTask 1.3: 定义命名空间约定 `VisualCompositor.Core.*`
- [x] Task 2: 实现核心值类型与枚举
  - [x] SubTask 2.1: `VisualCompositor.Core.Primitives`：`Vector2`/`Color3`/`OsbLayer`/`OsbOrigin`/`OsbEasing`/`OsbLoopType`/`ParameterType`/`Vector2ComponentMask`（可复用 common 的枚举定义或独立定义以保持 Core 无依赖）
  - [x] SubTask 2.2: `OpenEndedMode` 枚举（`ExplicitEnd`/`UntilLayerEnd`/`UntilCompositionEnd`）
  - [x] SubTask 2.3: `BlockEditAccess` 枚举（`ReadOnly`/`BlockEditable`/`CommandEditable`）
  - [x] SubTask 2.4: `BlockMaterializationState` 枚举（`PreservedBlock`/`ExpandedVirtual`/`ExpandedToKeyframes`）
  - [x] SubTask 2.5: `CommandRecordLifecycle` 枚举（`ImportedUnchanged`/`Modified`/`Split`/`Merged`/`Deleted`/`Created`）
  - [x] SubTask 2.6: `RawBlockAnchorKind` 枚举（`LayerStart`/`AfterCommand`）
  - [x] SubTask 2.7: `BezierExportMode` 枚举（`WarnOnly`/`FitNearestOsuEasing`/`BakeToSegments`）
  - [x] SubTask 2.8: `RenderQuality` 枚举（`FullPreview`/`InteractiveScrub`/`FastScrub`/`TimelineThumbnail`）
- [x] Task 3: 实现 Snapshot 类型定义（设计文档 §13）
  - [x] SubTask 3.1: `DocumentSnapshot`（execution-scoped, rollback-only）
  - [x] SubTask 3.2: `StoryboardBlockSnapshot`（transaction-persistent, undo-before-state）
  - [x] SubTask 3.3: `CommandRecordSnapshot`（transaction/serialization persistent）
  - [x] SubTask 3.4: `GeneratedCommandSnapshot`（transaction-persistent, redo-after-state）
  - [x] SubTask 3.5: `RawBlockAnchorSnapshot`（transaction-persistent, undo/redo anchor state）
  - [x] SubTask 3.6: `SourceReferenceSnapshot`（embedded-persistent, provenance-only）
  - [x] SubTask 3.7: `BlockInsertionAnchor`（含 `LayerId`/`PreviousSiblingBlockId`/`NextSiblingBlockId`/`OriginalIndexFallback`）
  - [x] SubTask 3.8: 所有 Snapshot 必须深拷贝，不得引用 live model
- [x] Task 4: 实现 12 条 Validator（设计文档 Phase 1A Invariants Matrix）
  - [x] SubTask 4.1: `ValidateGeneratedCommandSetConsistency`（A）— `Set(GeneratedCommandIds) == Set(GeneratedCommandsAfter.CommandId)`
  - [x] SubTask 4.2: `ValidateGeneratedCommandUniqueness`（B）— ids 无重复、snapshot CommandId 无重复且非空
  - [x] SubTask 4.3: `ValidateOriginalBlockRecordCompleteness`（C）— before records 包含 header + relative ids
  - [x] SubTask 4.4: `ValidateRedoAfterRecordCompleteness`（D）— after records 包含 generated + source split records
  - [x] SubTask 4.5: `ValidateDerivedCommandPartition`（E）— header DerivedCommandIds == disjoint union(non-header)
  - [x] SubTask 4.6: `ValidateRawBlockAnchorSnapshotSetConsistency`（F）— before/after RawBlockId set 相等
  - [x] SubTask 4.7: `ValidateRawBlockAnchorTargetExistence`（G）— LayerStart/AfterCommand 合法组合 + target 存在
  - [x] SubTask 4.8: `ValidateRelativeCommandRecordIdentity`（H）— RelativeCommand.Id 存在于 records 且匹配
  - [x] SubTask 4.9: `ValidateHeaderCommandIdentity`（I）— HeaderCommandId 存在且 parent 匹配
  - [x] SubTask 4.10: `ValidateOpaquePersistentIds`（J）— ids 非空/唯一/引用有效，不重算 hash
  - [x] SubTask 4.11: `ValidateRedoStackIntegrity`（K）— 成功 edit 清空 redo，失败不改栈
  - [x] SubTask 4.12: `ValidateInsertionAnchorLayerConsistency`（L）— InsertionAnchor.LayerId == transaction.LayerId
- [x] Task 5: 实现 Validation 入口分发器
  - [x] SubTask 5.1: `ValidationEntryPoint` 枚举（`create`/`deserialize-load`/`undo-restore`/`redo-restore`/`pre-osb-export`/`pre-storybrewcomp-save`/`debug-integrity-scan`）
  - [x] SubTask 5.2: `ValidationScope` 枚举（`document-state`/`transaction-history`/`serialization`）
  - [x] SubTask 5.3: `ValidationDispatcher`：按 entry point 调用对应 scope 的 validators
  - [x] SubTask 5.4: `Diagnostic`/`DiagnosticSeverity`（`hard-error`/`warning`）+ failure code 常量
  - [x] SubTask 5.5: `pre-osb-export` 只跑 document-state validators（E/G/H/I/J）
  - [x] SubTask 5.6: `pre-storybrewcomp-save` 跑 document + transaction validators
- [x] Task 6: Phase 1A 单元测试
  - [x] SubTask 6.1: 为每条 validator（A-L）实现矩阵 Minimum Tests 列指定的测试场景
  - [x] SubTask 6.2: 测试 validation dispatcher 在各 entry point 调用正确的 validator 子集

## Phase 1B: Core 模型 + Schema v2 + Snapshot 定义

- [x] Task 7: 实现 CompositionDocument 核心模型
  - [x] SubTask 7.1: `CompositionDocument`（`SchemaVersion`/`Settings`/`Variables`/`Layers`/`Markers`/`RawBlocks`/`CommandRecords`/`ExpandedBlockHistories`）
  - [x] SubTask 7.2: `Layer`（`Id`/`Name`/`OsbLayer`/`DiffSpecific`/`PropertyTracks`/`ParameterTrack`/`Blocks`）
  - [x] SubTask 7.3: `PropertyTrack<T>`（Position/Scale/Rotation/Opacity/Color）含 keyframe 列表与 `Vector2ComponentMask`
  - [x] SubTask 7.4: `Keyframe<T>`（`Time`/`Value`/`Easing`/handles）
  - [x] SubTask 7.5: `ParameterTrack`（`ParameterSegment` 列表，`EndTime=null` + `OpenEndedMode`）
  - [x] SubTask 7.6: `StoryboardBlock`（`Id`/`HeaderCommandId`/`EditAccess`/`MaterializationState`）
  - [x] SubTask 7.7: `LoopBlock`/`TriggerBlock`（继承 `StoryboardBlock`，含 `RelativeCommand` 列表）
  - [x] SubTask 7.8: `CommandRecord`（`CommandId`/`Lifecycle`/`DerivedCommandIds`/`ParentBlockId`/`ParentLayerId`/`SourceReference`）
  - [x] SubTask 7.9: `RawBlock`（`Id`/`Content`/`AnchorKind`/`AnchorCommandId`）
  - [x] SubTask 7.10: `ExpandedBlockHistory`（`ExpandedBlockId`/`Snapshot`/`TransactionId`）
  - [x] SubTask 7.11: `RelativeCommand`（`Id`/`Type`/`StartTime`/`EndTime`/`StartValue`/`EndValue`/`Easing`/`ParentBlockId`）
- [x] Task 8: 实现 ID 生成器
  - [x] SubTask 8.1: `PersistentIdGenerator`：导入时基于 hash 分配，保存后不重算
  - [x] SubTask 8.2: 唯一性校验（不重算 hash-content validation）
- [x] Task 9: 实现 .storybrewcomp schema v2 序列化
  - [x] SubTask 9.1: `StorybrewCompSerializer`：`CompositionDocument` ↔ JSON（System.Text.Json）
  - [x] SubTask 9.2: schema v2 往返保真测试
  - [x] SubTask 9.3: v1 草案迁移到 v2（标记 dirty）
  - [x] SubTask 9.4: 缺少 `commandRecords`/`expandedBlockHistories` 时诊断、补齐、标记 dirty
- [x] Task 10: Phase 1B 单元测试
  - [x] SubTask 10.1: CompositionDocument 构建与不变量测试
  - [x] SubTask 10.2: schema v2 序列化往返测试
  - [x] SubTask 10.3: ID 唯一性与 opaque 测试

## Phase 2: OSB Parser / Import Mapper

- [x] Task 11: 创建 VisualCompositor.Osb 项目
  - [x] SubTask 11.1: `visualcompositor/osb/VisualCompositor.Osb.csproj`（引用 Core）
  - [x] SubTask 11.2: 添加到 `storybrew.sln`
- [x] Task 12: 实现 .osb parser
  - [x] SubTask 12.1: `OsbParser`：解析 `[Events]` 段，识别 `Sprite`/`Animation`/`Sample`/`L`/`T`/命令字母
  - [x] SubTask 12.2: 解析 `[Variables]` 段并做变量替换
  - [x] SubTask 12.3: 未知行/注释/空行 → `RawBlock`
  - [x] SubTask 12.4: 参考 `common/Storyboarding/OsbSprite.cs` 的 OSB 格式与 `scripts/ImportOsb.cs` 的解析逻辑
- [x] Task 13: 实现 Import Mapper（设计文档 §7）
  - [x] SubTask 13.1: `M/MX/MY` → Position track（含 `Vector2ComponentMask`）
  - [x] SubTask 13.2: `S/V` → Scale track
  - [x] SubTask 13.3: `R` → Rotation；`F` → Opacity；`C` → Color
  - [x] SubTask 13.4: `P` → ParameterTrack（空 end time → `UntilLayerEnd`）
  - [x] SubTask 13.5: `L` → LoopBlock（含 header CommandRecord + relative commands）
  - [x] SubTask 13.6: `T` → TriggerBlock
  - [x] SubTask 13.7: 为每个命令分配 persistent id（hash-based 初次分配）
  - [x] SubTask 13.8: 生成 `CommandRecord`（`ImportedUnchanged`）
  - [x] SubTask 13.9: RawBlock anchor 分配（`LayerStart`/`AfterCommand`）
- [x] Task 14: Phase 2 单元测试
  - [x] SubTask 14.1: 解析含全部命令类型的 `.osb` 样本
  - [x] SubTask 14.2: 变量替换测试
  - [x] SubTask 14.3: RawBlock 保真测试
  - [x] SubTask 14.4: 导入后 document-state validation 通过

## Phase 3: State / Reducer / History 骨架

- [x] Task 15: 创建 VisualCompositor.State 项目
  - [x] SubTask 15.1: `visualcompositor/state/VisualCompositor.State.csproj`（引用 Core）
  - [x] SubTask 15.2: 添加到 `storybrew.sln`
- [x] Task 16: 实现 EditorState 与纯函数 Reducer
  - [x] SubTask 16.1: `EditorState`（`Document`/`Revision`/`Selection`/`Viewport`/`UndoStack`/`RedoStack`）
  - [x] SubTask 16.2: `Reducer`：纯函数 `(state, action) -> state`
  - [x] SubTask 16.3: Action 类型（`EditKeyframe`/`AddBlock`/`RemoveBlock`/`MoveBlock`/`ExpandBlock`/`Undo`/`Redo` 等）
  - [x] SubTask 16.4: Effect 层接口（文件 IO/渲染/脚本运行不进 Reducer）
- [x] Task 17: 实现线性 Undo/Redo（设计文档 §9/§11）
  - [x] SubTask 17.1: `UndoStack`/`RedoStack`（LIFO）
  - [x] SubTask 17.2: 成功 edit 清空 redo stack（原子绑定）
  - [x] SubTask 17.3: 失败 edit/undo/redo 不改变栈（restore DocumentSnapshot）
  - [x] SubTask 17.4: `.storybrewcomp` 不持久化 undo stack
- [x] Task 18: 实现 ExpandBlockTransaction（设计文档 §10/§11/§12）
  - [x] SubTask 18.1: `ExpandBlockTransaction` 创建时校验（A/B/C/D/F/L + InsertionAnchor.LayerId == LayerId）
  - [x] SubTask 18.2: Undo 流程（capture snapshot → validate → remove generated → restore before records → recreate block → insert by anchor → remove history → post-validate → failure restore）
  - [x] SubTask 18.3: Redo 流程（replay，不 recompute；capture → validate → remove block → insert GeneratedCommandsAfter → restore after records → add history → post-validate → failure restore）
  - [x] SubTask 18.4: `DerivedCommandIds` partition 维护（§12）
- [x] Task 19: Phase 3 单元测试
  - [x] SubTask 19.1: Reducer 纯函数测试
  - [x] SubTask 19.2: undo/redo 线性测试（成功清空 redo、失败不改栈）
  - [x] SubTask 19.3: ExpandBlockTransaction 创建校验测试
  - [x] SubTask 19.4: undo/redo expand 流程测试（含失败回滚）

## Phase 4: Render Worker / 静态预览

- [ ] Task 20: 创建 VisualCompositor.Rendering 项目
  - [ ] SubTask 20.1: `visualcompositor/rendering/VisualCompositor.Rendering.csproj`（引用 Core + brewlib）
  - [ ] SubTask 20.2: 添加到 `storybrew.sln`
- [ ] Task 21: 实现异步 Render Worker（设计文档 §8）
  - [ ] SubTask 21.1: `RenderRequest`/`RenderResult`（含 `Revision`）
  - [ ] SubTask 21.2: `RenderWorker`：异步消费 request，UI 只接受 `result.Revision == state.Revision`
  - [ ] SubTask 21.3: 质量预设（FullPreview/InteractiveScrub/FastScrub/TimelineThumbnail）
  - [ ] SubTask 21.4: scrub 时降分辨率/丢弃旧 request/取消过期 request
  - [ ] SubTask 21.5: `SelectedAndContext` 图层选择规则（selected + 相邻可见 + 背景 + fullscreen quads + missing placeholders，受 `MaxRenderedLayers` 限制）
- [ ] Task 22: 实现静态预览渲染
  - [ ] SubTask 22.1: 复用 brewlib `QuadRendererBuffered`/`TextureContainer` 渲染 sprite
  - [ ] SubTask 22.2: 缺失素材 placeholder 渲染
  - [ ] SubTask 22.3: 命令采样（Position/Scale/Rotation/Opacity/Color/Parameter at time）

## Phase 5: UI MVP

- [ ] Task 23: 创建 VisualCompositor.UI 项目
  - [ ] SubTask 23.1: `visualcompositor/ui/VisualCompositor.UI.csproj`（引用 State + Rendering + brewlib）
  - [ ] SubTask 23.2: 添加到 `storybrew.sln`
- [ ] Task 24: 实现 UI MVP 骨架
  - [ ] SubTask 24.1: 主窗口 + ScreenLayer 接入（复用 brewlib `ScreenLayerManager`）
  - [ ] SubTask 24.2: 时间轴 scrub UI（复用 brewlib `Slider`/`Widget`）
  - [ ] SubTask 24.3: 图层列表 UI
  - [ ] SubTask 24.4: 预览画布（接入 Render Worker）
  - [ ] SubTask 24.5: Diagnostics 面板
  - [ ] SubTask 24.6: 打开 `.osb` / 保存 `.storybrewcomp` / 导出 `.osb` 菜单

## Phase 6: Keyframe 编辑 + Undo/Redo

- [ ] Task 25: 实现 Transform keyframe 编辑
  - [ ] SubTask 25.1: keyframe 添加/删除/移动（Position/Scale/Rotation/Opacity/Color）
  - [ ] SubTask 25.2: keyframe 编辑经 Reducer 产生 undo/redo transaction
  - [ ] SubTask 25.3: ParameterTrack segment 编辑
- [ ] Task 26: 实现 Block 编辑
  - [ ] SubTask 26.1: Block 移动/删除（经 Reducer）
  - [ ] SubTask 26.2: Expand To Keyframes 操作（触发 `ExpandBlockTransaction`）

## Phase 7: Export Compiler

- [ ] Task 27: 实现 .osb export compiler（设计文档 §7）
  - [ ] SubTask 27.1: Position X/Y 对齐检测 → `M` 或 `MX+MY`
  - [ ] SubTask 27.2: Scale X/Y 全程相等检测 → `S` 或 `V`
  - [ ] SubTask 27.3: Loop/Trigger 内部命令由各自 exporter 处理
  - [ ] SubTask 27.4: RawBlock 按 anchor 插回（rebase → scope 末尾 → orphan warning）
  - [ ] SubTask 27.5: Bezier 导出模式（WarnOnly/FitNearestOsuEasing/BakeToSegments）
  - [ ] SubTask 27.6: 导出前 `pre-osb-export` validation
- [ ] Task 28: Phase 7 单元测试
  - [ ] SubTask 28.1: 导入后导出往返保真测试
  - [ ] SubTask 28.2: M/MX+MY 优化测试
  - [ ] SubTask 28.3: S/V 优化测试
  - [ ] SubTask 28.4: RawBlock anchor 插回测试

## Phase 8-10: 后续阶段（概述）

- [ ] Task 29: Phase 8 — Audio/Beat/Hitsound 支持
- [ ] Task 30: Phase 9 — Script Sync / Visual Override
- [ ] Task 31: Phase 10 — Graph Editor / Bezier Export Modes

# Task Dependencies

- [Task 4] (validators) depends on [Task 3] (snapshot types)
- [Task 5] (validation dispatcher) depends on [Task 4]
- [Task 6] (Phase 1A tests) depends on [Task 5]
- [Task 7] (core model) depends on [Task 2] (value types) — 可并行于 Task 3-6
- [Task 8] (ID generator) depends on [Task 7]
- [Task 9] (schema v2) depends on [Task 7]
- [Task 10] (Phase 1B tests) depends on [Task 8, Task 9]
- [Task 12] (parser) depends on [Task 11] (Osb project) + [Task 7]
- [Task 13] (import mapper) depends on [Task 12]
- [Task 14] (Phase 2 tests) depends on [Task 13]
- [Task 16] (reducer) depends on [Task 15] (State project) + [Task 7]
- [Task 17] (undo/redo) depends on [Task 16]
- [Task 18] (expand transaction) depends on [Task 17, Task 4]
- [Task 19] (Phase 3 tests) depends on [Task 18]
- [Task 21] (render worker) depends on [Task 20] (Rendering project) + [Task 7]
- [Task 22] (static preview) depends on [Task 21]
- [Task 24] (UI MVP) depends on [Task 23] (UI project) + [Task 22, Task 17]
- [Task 25] (keyframe editing) depends on [Task 24, Task 17]
- [Task 26] (block editing) depends on [Task 25, Task 18]
- [Task 27] (export compiler) depends on [Task 7, Task 13]
- [Task 28] (Phase 7 tests) depends on [Task 27]
- [Task 29-31] depend on respective prior phases
