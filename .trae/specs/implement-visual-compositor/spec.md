# Storybrew Visual Compositor 实现规格

> 权威输入稿：`/workspace/Storybrew Visual Compositor工程设计文档v1.7freeze.md`（v1.7 整合稿 + Freeze Patch + Final Freeze Addendum，已冻结为 Phase 1A 输入稿）

## Why

当前 storybrew 编辑器是基于脚本的 storyboard 编辑器（用户写 C# 脚本生成 `OsbSprite` 命令）。本规格实现一个全新的 AE-like（After Effects 风格）osu! storyboard compositor：支持 `.osb` 直接导入、结构化可视化编辑、实时预览、`.storybrewcomp` 保存、`.osb` 导出、线性 undo/redo。Loop/Trigger/Variables/RawBlock/CommandRecord 都是一等模型，ID 是持久 opaque identity，Expand To Keyframes 是可 undo/redo 的原子事务。

## What Changes

- **新增** `VisualCompositor.Core` 项目：纯文档模型（`CompositionDocument`/`Layer`/`PropertyTrack`/`ParameterTrack`/`StoryboardBlock`/`CommandRecord`/`RawBlock`/snapshot 类型），无 UI/Avalonia/渲染后端依赖
- **新增** `VisualCompositor.State` 项目：纯函数 Reducer + 线性 undo/redo history + `ExpandBlockTransaction` 事务模型
- **新增** `VisualCompositor.Osb` 项目：`.osb` parser/import mapper + export compiler
- **新增** `VisualCompositor.Rendering` 项目：异步 Render Worker + 质量预设
- **新增** `VisualCompositor.Audio` 项目：音频/节拍/hitsound 支持
- **新增** `VisualCompositor.UI` 项目：compositor UI MVP
- **新增** `VisualCompositor.Integration` 项目：整合层
- **新增** `.storybrewcomp` schema v2 持久格式（JSON）
- **新增** Phase 1A Invariants Matrix 的 12 条 validator（A-L）实现
- **复用** 现有 `common`（`StorybrewCommon`）的 OSB 命令模型作为参考与导出目标格式
- **复用** 现有 `brewlib`（`BrewLib`）的渲染/音频/纹理能力

## Impact

- Affected specs: 全新项目，无既有 spec 受影响
- Affected code:
  - 新增 `visualcompositor/` 目录及子项目
  - 修改 `storybrew.sln` 添加新项目引用
  - 复用 `common/Storyboarding/`（`OsbSprite`/`OsbLayer`/`OsbEasing` 等枚举与命令类作为导出目标）
  - 复用 `brewlib/Graphics/`、`brewlib/Audio/` 作为渲染/音频后端

## 架构边界

依赖方向（来自设计文档 §1）：

```
UI -> State -> Core
Osb -> Core
Rendering -> Core
Integration -> Core / Osb / State
Core -> no UI / Avalonia / rendering backend dependency
```

Reducer 必须纯函数化；文件 IO、渲染、脚本运行都走 Effect 层。Render Worker 不直接修改 `EditorState`。

## 持久格式（设计文档 §2）

`.storybrewcomp` schema v2（JSON）：

```json
{
  "schemaVersion": 2,
  "settings": {},
  "variables": {},
  "layers": [],
  "markers": [],
  "rawBlocks": [],
  "commandRecords": {},
  "expandedBlockHistories": []
}
```

`diagnostics` 不写入顶层；由加载/导入/验证/导出/渲染阶段重新生成。缺少 `schemaVersion` 或 v1 草案可迁移到 v2。v2 缺少 `commandRecords` 或 `expandedBlockHistories` 时必须诊断、补齐、标记 dirty。

## 核心文档模型（设计文档 §3-§6）

### 内部属性轨道

```
Position: Vector2（支持 X/Y component-wise keyframe，通过 Vector2ComponentMask）
Scale: Vector2
Rotation: Float
Opacity: Float
Color: RGB 整体 keyframe
```

不再使用 `PositionX/PositionY/ScaleX/ScaleY` 长期编辑轨道。`MX/MY/S/V` 通过 `Vector2ComponentMask` 和导出优化表达。

### ParameterTrack

`P` 命令不进入普通 keyframe track，使用 `ParameterTrack` 含 `ParameterSegment(Additive/FlipH/FlipV)`。`ParameterSegment.EndTime = null` 表示 OSB 空 end time。`OpenEndedMode`：`ExplicitEnd`/`UntilLayerEnd`/`UntilCompositionEnd`。`.osb` 导入空 end time 默认映射为 `UntilLayerEnd`。

### Block 模型

Loop/Trigger 是一等结构，不默认展开。

```
StoryboardBlock
  Id
  HeaderCommandId
  EditAccess: ReadOnly | BlockEditable | CommandEditable
  MaterializationState: PreservedBlock | ExpandedVirtual | ExpandedToKeyframes
```

MVP 默认：`ReadOnly+PreservedBlock`、`BlockEditable+PreservedBlock`、`ReadOnly+ExpandedVirtual`。`CommandEditable+ExpandedVirtual` 默认禁用。

执行 `Expand To Keyframes` 后：原 block 从 active tree 移除 → 生成普通 PropertyTrack commands → 原 block 快照进入 `ExpandedBlockHistory`。`ExpandedBlockHistory.ExpandedBlockId` 是历史引用，不是 active foreign key。

### ID 与 CommandRecord

`Block.Id`/`CommandId` 是 opaque persistent id。hash 只用于初次导入分配；保存后不得重算；加载/移动/编辑/导出时不得用 hash 做一致性校验；只允许校验唯一性和引用完整性。

`CommandRecord` 生命周期：`ImportedUnchanged`/`Modified`/`Split`/`Merged`/`Deleted`/`Created`。Loop/Trigger header 必须有 `HeaderCommandId` 和对应 `CommandRecord`。`RelativeCommand.Id` 必须等于其 `CommandRecord.CommandId`。Deleted records 默认持久化。

### RawBlock 与保真

未知行/注释/空行/无法解析结构进入 `RawBlock`。RawBlock 不产生 `CommandRecord`。`RawBlockAnchorKind`：`LayerStart`（`AnchorCommandId == null`）/`AfterCommand`（`AnchorCommandId != null` 且存在于 commandRecords）。MVP 不支持 detached RawBlock。导出时按 anchor 插回；anchor 丢失时 rebase → scope 末尾 → orphan（`RAW_ANCHOR_ORPHANED` warning）。

## ADDED Requirements

### Requirement: Phase 1A Invariants Matrix 实现

系统 SHALL 实现 12 条 validator（A-L），每条至少覆盖其 Minimum Tests 列指定的场景。Validator 分三层：Transaction validators（A/B/C/D/F/L）、Document validators（E/G/H/I/J）、History validators（K）。

#### Scenario: 校验入口覆盖
- **WHEN** 在 `create`/`deserialize-load`/`undo-restore`/`redo-restore`/`pre-osb-export`/`pre-storybrewcomp-save`/`debug-integrity-scan` 入口调用对应 validator
- **THEN** 按矩阵 Entry Points 列执行对应 scope 的 validator
- **AND** `pre-osb-export` 只跑 document-state validators（E/G/H/I/J）
- **AND** `pre-storybrewcomp-save` 跑 document + transaction validators

#### Scenario: Generated command set consistency (A)
- **WHEN** 构造 transaction，`GeneratedCommandIds=[a,b]`，`GeneratedCommandsAfter.CommandId=[a,c]`
- **THEN** create 失败，emit `EXPAND_TRANSACTION_GENERATED_SET_MISMATCH`

### Requirement: Core 文档模型与 schema v2

系统 SHALL 实现 `CompositionDocument` 及其全部子结构（Settings/Variables/Layers/PropertyTracks/ParameterTrack/Blocks/RawBlocks/CommandRecords/ExpandedBlockHistories），支持 `.storybrewcomp` schema v2 JSON 序列化/反序列化。

#### Scenario: schema v2 往返
- **WHEN** 构造合法 `CompositionDocument` 并序列化为 `.storybrewcomp` 再反序列化
- **THEN** 得到结构等价的文档
- **AND** `schemaVersion == 2`

#### Scenario: v1 迁移
- **WHEN** 加载缺少 `schemaVersion` 或 v1 草案文件
- **THEN** 迁移到 v2 并标记 dirty

### Requirement: OSB 导入映射

系统 SHALL 实现 `.osb` parser，按设计文档 §7 映射命令到文档模型。

#### Scenario: 命令映射
- **WHEN** 导入含 `M/MX/MY/S/V/R/F/C/P/L/T` 的 `.osb`
- **THEN** `M/MX/MY` → Position track（含 component mask）；`S/V` → Scale track；`R` → Rotation；`F` → Opacity；`C` → Color；`P` → ParameterTrack；`L` → LoopBlock；`T` → TriggerBlock；未知/注释/空行 → RawBlock

### Requirement: OSB 导出编译

系统 SHALL 实现 export compiler，按设计文档 §7 优化导出。

#### Scenario: Position 合并优化
- **WHEN** Position X/Y segment boundaries + easing/interpolation/handles 完全对齐
- **THEN** 导出为 `M`
- **WHEN** 不对齐
- **THEN** 导出为 `MX + MY`

#### Scenario: Bezier 导出模式
- **WHEN** Bezier 导出模式为 `WarnOnly`（默认）
- **THEN** 不得静默丢失不可表达曲线，emit warning

### Requirement: 线性 Undo/Redo

系统 SHALL 实现严格线性 undo/redo（LIFO，不支持选择性 undo / undo tree）。

#### Scenario: 成功 edit 清空 redo
- **WHEN** undo 后执行成功的新 document-mutating edit
- **THEN** redo stack 清空
- **AND** 清空与新 transaction 入栈原子绑定

#### Scenario: 失败 edit 不改变栈
- **WHEN** edit/undo/redo 失败
- **THEN** restore pre-operation DocumentSnapshot
- **AND** undo stack 不变，redo stack 不变

### Requirement: ExpandBlockTransaction 原子事务

系统 SHALL 实现 `ExpandBlockTransaction`（含 `InsertionAnchor`），支持 undo/redo replay（redo 是 replay 不是 recompute）。

#### Scenario: undo 插回
- **WHEN** undo expand
- **THEN** 按 `InsertionAnchor` 规则插回 original block（PreviousSibling → NextSibling → OriginalIndexFallback clamp）
- **AND** 失败时 restore PreUndo snapshot，transaction 留在 undo stack

### Requirement: Render Worker 异步

系统 SHALL 实现异步 Render Worker，UI 只接受 `result.Revision == state.Revision` 的结果。支持质量预设（FullPreview/InteractiveScrub/FastScrub/TimelineThumbnail），scrub 时允许降分辨率/丢弃旧 request/取消过期 request。

### Requirement: MVP 验收

系统 SHALL 满足设计文档 §16 的 MVP 完成条件：打开复杂 `.osb`、显示 Sprite/Animation/Loop/Trigger/Variables、保留 unknown/comment/raw、显示缺失素材 placeholder、scrub 预览、编辑基础 Transform keyframes、线性 undo/redo、保存 `.storybrewcomp` v2、导出 osu! 可读 `.osb`、diagnostics 可见、Render Worker 不阻塞 UI。

## MODIFIED Requirements

### Requirement: storybrew.sln 解决方案

现有 `storybrew.sln` 添加 `VisualCompositor.*` 新项目引用，保持既有 `common`/`editor`/`scripts`/`test`/`brewlib` 项目不变。

## REMOVED Requirements

无。本规格为纯新增。

## 实施顺序（设计文档 §17）

```
Phase 1A: Authoritative Invariants and Validation Matrix（已在设计文档中产出，需实现为代码）
Phase 1B: Core model + schema v2 + snapshot definitions
Phase 2: OSB parser/import mapper
Phase 3: State/reducer/history skeleton
Phase 4: render worker/static preview
Phase 5: UI MVP
Phase 6: keyframe editing + undo/redo
Phase 7: export compiler
Phase 8: audio/beat/hitsound support
Phase 9: script sync / visual override
Phase 10: graph editor / bezier export modes
```
