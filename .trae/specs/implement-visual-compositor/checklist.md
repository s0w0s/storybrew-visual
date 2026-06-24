# Implementation Checklist

> 验证 `tasks.md` 实现是否满足设计文档 `/workspace/Storybrew Visual Compositor工程设计文档v1.7freeze.md`（v1.7 整合稿 + Freeze Patch + Final Freeze Addendum）的检查点清单。每完成一项验证即勾选。

## 架构边界（设计文档 §1）

- [ ] `VisualCompositor.Core` 项目无 UI / Avalonia / 渲染后端依赖（仅 System.Text.Json 等基础库）
- [ ] 依赖方向严格遵循 `UI -> State -> Core`、`Osb -> Core`、`Rendering -> Core`、`Integration -> Core/Osb/State`
- [ ] Reducer 为纯函数；文件 IO / 渲染 / 脚本运行均走 Effect 层
- [ ] Render Worker 不直接修改 `EditorState`

## 持久格式（设计文档 §2）

- [ ] `.storybrewcomp` schema v2 JSON 包含 `schemaVersion`/`settings`/`variables`/`layers`/`markers`/`rawBlocks`/`commandRecords`/`expandedBlockHistories`
- [ ] `diagnostics` 不写入 `CompositionDocument` 顶层（由加载/导入/验证/导出/渲染阶段重新生成）
- [ ] 缺少 `schemaVersion` 或 v1 草案文件可迁移到 v2
- [ ] v2 缺少 `commandRecords` 或 `expandedBlockHistories` 时诊断、补齐、标记 dirty

## 核心文档模型（设计文档 §3-§6）

- [ ] 内部属性轨道仅含 `Position(Vector2)`/`Scale(Vector2)`/`Rotation`/`Opacity`/`Color`，不再使用 `PositionX/PositionY/ScaleX/ScaleY` 长期编辑轨道
- [ ] `MX/MY/S/V` 通过 `Vector2ComponentMask` 与导出优化表达
- [ ] `P` 命令进入 `ParameterTrack`（含 `ParameterSegment` Additive/FlipH/FlipV），不进入普通 keyframe track
- [ ] `ParameterSegment.EndTime = null` 表示 OSB 空 end time；`OpenEndedMode` 三态（ExplicitEnd/UntilLayerEnd/UntilCompositionEnd）
- [ ] `.osb` 导入空 end time 默认映射为 `UntilLayerEnd`
- [ ] Loop/Trigger 是一等结构，不默认展开
- [ ] `StoryboardBlock` 含 `Id`/`HeaderCommandId`/`EditAccess`/`MaterializationState`
- [ ] `BlockEditAccess` 三态（ReadOnly/BlockEditable/CommandEditable）；`BlockMaterializationState` 三态（PreservedBlock/ExpandedVirtual/ExpandedToKeyframes）
- [ ] MVP 默认组合：`ReadOnly+PreservedBlock`、`BlockEditable+PreservedBlock`、`ReadOnly+ExpandedVirtual`；`CommandEditable+ExpandedVirtual` 默认禁用
- [ ] `Expand To Keyframes` 后：原 block 从 active tree 移除 → 生成 PropertyTrack commands → 原 block 快照进入 `ExpandedBlockHistory`
- [ ] `ExpandedBlockHistory.ExpandedBlockId` 是历史引用，不是 active foreign key
- [ ] `Block.Id`/`CommandId` 是 opaque persistent id；hash 只用于初次导入分配；保存后不得重算；加载/移动/编辑/导出时不得用 hash 做一致性校验；只允许校验唯一性和引用完整性
- [ ] `CommandRecord` 生命周期六态（ImportedUnchanged/Modified/Split/Merged/Deleted/Created）
- [ ] Loop/Trigger header 必须有 `HeaderCommandId` 和对应 `CommandRecord`；`RelativeCommand.Id == CommandRecord.CommandId`
- [ ] Deleted records 默认持久化；成功导出 `.osb` 后可 safe cleanup，但不破坏 RawBlock anchor / SourceReference / undo/redo history 引用
- [ ] 未知行/注释/空行/无法解析结构进入 `RawBlock`；RawBlock 不产生 `CommandRecord`
- [ ] `RawBlockAnchorKind`：`LayerStart`（AnchorCommandId == null）/`AfterCommand`（AnchorCommandId != null 且存在于 commandRecords）
- [ ] MVP 不支持 detached RawBlock
- [ ] 导出时 anchor 丢失 rebase → scope 末尾 → orphan（`RAW_ANCHOR_ORPHANED` warning）

## Phase 1A Invariants Matrix — 12 条 Validator（A-L）

- [ ] Validator A `ValidateGeneratedCommandSetConsistency`：`Set(GeneratedCommandIds) == Set(GeneratedCommandsAfter.CommandId)`，失败码 `EXPAND_TRANSACTION_GENERATED_SET_MISMATCH`
- [ ] Validator B `ValidateGeneratedCommandUniqueness`：ids 无重复、snapshot CommandId 无重复且非空，失败码 `EXPAND_TRANSACTION_GENERATED_ID_DUPLICATE` / `EXPAND_TRANSACTION_GENERATED_SNAPSHOT_ID_EMPTY`
- [ ] Validator C `ValidateOriginalBlockRecordCompleteness`：`OriginalBlockSnapshot` header/relative ids 均存在于 `AffectedCommandRecordsBefore`
- [ ] Validator D `ValidateRedoAfterRecordCompleteness`：`AffectedCommandRecordsAfter` 包含 generated records 和 source split records
- [ ] Validator E `ValidateDerivedCommandPartition`：`HeaderCommandRecord.DerivedCommandIds == non-header split records DerivedCommandIds 的不相交并集`
- [ ] Validator F `ValidateRawBlockAnchorSnapshotSetConsistency`：`RawBlockAnchorsBefore.RawBlockId set == RawBlockAnchorsAfter.RawBlockId set`
- [ ] Validator G `ValidateRawBlockAnchorTargetExistence`：LayerStart/AfterCommand 合法组合 + AfterCommand target 存在于 commandRecords
- [ ] Validator H `ValidateRelativeCommandRecordIdentity`：`RelativeCommand.Id` 存在于 records 且 `== CommandRecord.CommandId`
- [ ] Validator I `ValidateHeaderCommandIdentity`：`Block.HeaderCommandId` record 存在且 parent 匹配
- [ ] Validator J `ValidateOpaquePersistentIds`：ids 非空/唯一/引用有效，不重算 hash-content validation
- [ ] Validator K `ValidateRedoStackIntegrity`：成功 edit 清空 redo（原子绑定），失败 edit/undo/redo 不改栈
- [ ] Validator L `ValidateInsertionAnchorLayerConsistency`：`InsertionAnchor.LayerId == transaction.LayerId`

## Validation Scope & Entry Points（设计文档 §14）

- [ ] `ValidationEntryPoint` 七态：`create`/`deserialize-load`/`undo-restore`/`redo-restore`/`pre-osb-export`/`pre-storybrewcomp-save`/`debug-integrity-scan`
- [ ] `ValidationScope` 三态：`document-state`/`transaction-history`/`serialization`
- [ ] `pre-osb-export` 只跑 document-state invariants（E/G/H/I/J）
- [ ] `pre-storybrewcomp-save` 跑 document-state + transaction-history + serialization invariants
- [ ] MVP 无 recovery mode；损坏 `.storybrewcomp` 加载失败并报告 diagnostics，不自动修复、不部分加载 undo stack
- [ ] Diagnostic 含 `Severity`（hard-error/warning）+ failure code

## Snapshot 规则（设计文档 §13）

- [ ] 所有 `*Snapshot` 深拷贝，不得引用 live model
- [ ] `DocumentSnapshot`：execution-scoped, rollback-only，不进 transaction / undo/redo stack
- [ ] `StoryboardBlockSnapshot`：transaction-persistent, undo-before-state
- [ ] `CommandRecordSnapshot`：transaction/serialization persistent
- [ ] `GeneratedCommandSnapshot`：transaction-persistent, redo-after-state
- [ ] `RawBlockAnchorSnapshot`：transaction-persistent, undo/redo anchor state
- [ ] `SourceReferenceSnapshot`：embedded-persistent, provenance-only，不作为独立顶层状态
- [ ] `BlockInsertionAnchor` 含 `LayerId`/`PreviousSiblingBlockId`/`NextSiblingBlockId`/`OriginalIndexFallback`（Freeze Patch §1 补回）

## Undo/Redo 原子性（设计文档 §9/§11）

- [ ] 严格线性 undo/redo（LIFO，不支持选择性 undo / undo tree）
- [ ] 任何成功的新 document-mutating edit 清空 redo stack，且清空与新 transaction 入栈原子绑定
- [ ] 失败编辑不得改变 undo stack 或 redo stack（restore pre-operation DocumentSnapshot）
- [ ] `.storybrewcomp` 不持久化 undo stack
- [ ] 重新打开文件后不能依赖 undo 还原已展开 block（`ExpandedBlockHistory` 仅用于审计/注释导出/未来 restore 工具）
- [ ] Undo 流程完整：capture PreUndo snapshot → validate → remove generated by `GeneratedCommandIds` → remove records → restore before records → restore before anchors → recreate block → insert by `InsertionAnchor` → remove history → post-validate → failure restore PreUndo + 保留 transaction 在 undo stack
- [ ] Redo 是 replay 不是 recompute：capture PreRedo snapshot → validate → remove block → insert `GeneratedCommandsAfter` → restore after records → restore after anchors → add history → post-validate → failure restore PreRedo + 保留 transaction 在 redo stack
- [ ] Redo 不重新运行 expand algorithm，不重新生成 ids

## ExpandBlockTransaction（设计文档 §10/§12 + Freeze Patch）

- [ ] `ExpandBlockTransaction` 字段完整：`TransactionId`/`LayerId`/`OriginalBlockSnapshot`/`InsertionAnchor`/`CreatedHistory`/`GeneratedCommandIds`/`GeneratedCommandsAfter`/`AffectedCommandRecordsBefore`/`AffectedCommandRecordsAfter`/`RawBlockAnchorsBefore`/`RawBlockAnchorsAfter`
- [ ] 创建时校验完整（Freeze Patch §2）：set 相等 / ids 无重复 / snapshot CommandId 无重复 / 非空 / 双向匹配
- [ ] `GeneratedCommandIds` 是 undo 删除权威列表；`GeneratedCommandsAfter` 是 redo replay 权威 payload
- [ ] `DerivedCommandIds` partition：header == non-header 不相交并集；每个 generated id 恰好一个 relative source；header-only generated id 只在 `EXPAND_DERIVED_SOURCE_AMBIGUOUS` warning 时存在
- [ ] `DerivedCommandIds` 是 provenance，不是 undo 删除范围
- [ ] Undo 插回规则（Freeze Patch §1）：PreviousSibling → NextSibling → OriginalIndexFallback clamp（emit `UNDO_INSERTION_ANCHOR_FALLBACK` warning）→ LayerId 不存在 abort + restore PreUndo + 保留 transaction + emit `UNDO_LAYER_MISSING` error

## Import / Export（设计文档 §7）

- [ ] 导入映射完整：`M/MX/MY`→Position（component mask）/`S/V`→Scale/`R`→Rotation/`F`→Opacity/`C`→Color/`P`→ParameterTrack/`L`→LoopBlock/`T`→TriggerBlock/未知→RawBlock
- [ ] 导出优化：Position X/Y segment boundaries + easing/interpolation/handles 完全对齐 → `M`，否则 `MX+MY`
- [ ] 导出优化：Scale X/Y 全程相等且 metadata 对齐 → `S`，否则 `V`
- [ ] Loop/Trigger 内部命令不参与普通 PropertyTrack segment boundary 检查，由各自 exporter 处理
- [ ] Bezier 导出模式三态（WarnOnly/FitNearestOsuEasing/BakeToSegments），默认 WarnOnly，不得静默丢失不可表达曲线

## 渲染与性能（设计文档 §8）

- [ ] Render Worker 异步消费 `RenderRequest`
- [ ] UI 只接受 `result.Revision == state.Revision` 的结果
- [ ] 质量预设四态（FullPreview/InteractiveScrub/FastScrub/TimelineThumbnail）
- [ ] scrub 时允许半分辨率/四分辨率、丢弃旧 request、取消正在渲染的过期 request
- [ ] `SelectedAndContext` 按确定规则选图层（selected + 相邻可见 + 背景上下文 + fullscreen quads + missing placeholders，受 `MaxRenderedLayers` 限制）

## MVP 验收标准（设计文档 §16）

- [ ] 能打开复杂 `.osb`
- [ ] 能显示 Sprite / Animation / Loop / Trigger / Variables
- [ ] 能保留 unknown/comment/raw content
- [ ] 能显示缺失素材 placeholder
- [ ] 能 scrub 时间轴并预览
- [ ] 能编辑基础 Transform keyframes
- [ ] 能执行线性 undo/redo
- [ ] 能保存 `.storybrewcomp` v2
- [ ] 能导出 osu! 可读 `.osb`
- [ ] 导入/导出/验证 diagnostics 可见
- [ ] Render Worker 不阻塞 UI

## 实施顺序（设计文档 §17）

- [ ] Phase 1A: Authoritative Invariants and Validation Matrix 已实现为代码（12 validator + dispatcher + tests）
- [ ] Phase 1B: Core model + schema v2 + snapshot definitions 完成
- [ ] Phase 2: OSB parser/import mapper 完成
- [ ] Phase 3: State/reducer/history skeleton 完成
- [ ] Phase 4: render worker/static preview 完成
- [ ] Phase 5: UI MVP 完成
- [ ] Phase 6: keyframe editing + undo/redo 完成
- [ ] Phase 7: export compiler 完成
- [ ] Phase 8: audio/beat/hitsound support 完成
- [ ] Phase 9: script sync / visual override 完成
- [ ] Phase 10: graph editor / bezier export modes 完成

## 解决方案集成

- [ ] `storybrew.sln` 添加 `VisualCompositor.Core/State/Osb/Rendering/Audio/UI/Integration` 七个新项目
- [ ] 既有 `common`/`editor`/`scripts`/`test`/`brewlib` 项目保持不变
- [ ] 新项目命名空间约定 `VisualCompositor.*`
