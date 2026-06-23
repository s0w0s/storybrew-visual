**Storybrew Visual Compositor 工程设计文档 v1.7 整合稿**  
版本：Draft v1.7  
定位：Phase 1 审计与实现前权威规格  
目标：AE-like osu! storyboard compositor，支持 `.osb` 导入、结构化编辑、实时预览、`.storybrewcomp` 保存、`.osb` 导出、线性 undo/redo。

**1. 架构边界**
系统分为：

```text
VisualCompositor.Core
VisualCompositor.State
VisualCompositor.Osb
VisualCompositor.Rendering
VisualCompositor.Audio
VisualCompositor.UI
VisualCompositor.Integration
```

依赖方向：

```text
UI -> State -> Core
Osb -> Core
Rendering -> Core
Integration -> Core / Osb / State
Core -> no UI / Avalonia / rendering backend dependency
```

Reducer 必须纯函数化；文件 IO、渲染、脚本运行都走 Effect 层。Render Worker 不直接修改 `EditorState`。

**2. 持久格式**
`.storybrewcomp` 是主编辑格式，`.osb` 是导入/导出格式。

正式 schema 从 `schemaVersion = 2` 开始：

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

`diagnostics` 不写入 `CompositionDocument` 顶层；诊断由加载、导入、验证、导出、渲染阶段重新生成。

缺少 `schemaVersion` 或 v1 草案文件可迁移到 v2。v2 缺少 `commandRecords` 或 `expandedBlockHistories` 时必须诊断、补齐、标记 dirty。

**3. 核心文档模型**
```text
CompositionDocument
  Settings
  Variables
  Layers
    PropertyTracks
    ParameterTrack
    Blocks
  RawBlocks
  CommandRecords
  ExpandedBlockHistories
```

内部属性轨道：

```text
Position: Vector2, 支持 X/Y component-wise keyframe
Scale: Vector2
Rotation: Float
Opacity: Float
Color: RGB 整体 keyframe
```

不再使用长期编辑轨道 `PositionX / PositionY / ScaleX / ScaleY`。`MX/MY/S/V` 通过 `Vector2ComponentMask` 和导出优化表达。

`P` 命令不进入普通 keyframe track，使用：

```text
ParameterTrack
  ParameterSegment(Additive / FlipH / FlipV)
```

`ParameterSegment.EndTime = null` 表示 OSB 空 end time。`OpenEndedMode` 可为：

```text
ExplicitEnd
UntilLayerEnd
UntilCompositionEnd
```

`.osb` 导入空 end time 默认映射为 `UntilLayerEnd`。

**4. Block 模型**
Loop / Trigger 是一等结构，不默认展开。

```text
StoryboardBlock
  Id
  HeaderCommandId
  EditAccess
  MaterializationState
```

```text
BlockEditAccess:
  ReadOnly
  BlockEditable
  CommandEditable

BlockMaterializationState:
  PreservedBlock
  ExpandedVirtual
  ExpandedToKeyframes
```

MVP 默认：

```text
ReadOnly + PreservedBlock
BlockEditable + PreservedBlock
ReadOnly + ExpandedVirtual
```

`CommandEditable + ExpandedVirtual` 默认禁用。Graph Editor 若要编辑 loop 某次 iteration 的绝对关键帧，必须先执行 `Expand To Keyframes`。

执行 `Expand To Keyframes` 后：

```text
原 LoopBlock / TriggerBlock 从 active layer block tree 移除
生成普通 PropertyTrack commands/keyframes
原 block 快照进入 ExpandedBlockHistory
```

`ExpandedBlockHistory.ExpandedBlockId` 是历史引用，不是 active foreign key。

**5. ID 与 CommandRecord**
`Block.Id` / `CommandId` 是 opaque persistent id。

```text
hash 只用于初次导入分配
保存后不得重算
加载、移动、编辑、导出时不得用 hash 做一致性校验
只允许校验唯一性和引用完整性
```

`CommandRecord` 记录命令生命周期：

```text
ImportedUnchanged
Modified
Split
Merged
Deleted
Created
```

Loop / Trigger header 必须有 `HeaderCommandId` 和对应 `CommandRecord`。`RelativeCommand.Id` 必须等于其 `CommandRecord.CommandId`。

Deleted records 默认持久化，保存 `.storybrewcomp` 不自动清理；成功导出 `.osb` 后可执行 safe cleanup，但不能破坏 RawBlock anchor、SourceReference、undo/redo history 引用。

**6. RawBlock 与保真**
未知行、注释、空行、无法解析结构进入 `RawBlock`。RawBlock 不产生 `CommandRecord`。

RawBlock anchor 使用：

```text
RawBlockAnchorKind:
  LayerStart
  AfterCommand
```

合法组合：

```text
LayerStart: AnchorCommandId == null
AfterCommand: AnchorCommandId != null 且存在于 commandRecords
```

`AnchorCommandId == null` 只表示 LayerStart。MVP 不支持 detached RawBlock。

导出时按 anchor 插回；anchor 丢失时先 rebase 到同 scope 最近 surviving command，再落到 scope 末尾，最后才 orphan。只有真正 orphan 产生 `RAW_ANCHOR_ORPHANED` warning。

**7. Import / Export**
导入映射：

```text
M/MX/MY -> Position track with component masks
S/V     -> Scale track
R       -> Rotation
F       -> Opacity
C       -> Color
P       -> ParameterTrack
L       -> LoopBlock
T       -> TriggerBlock
Unknown/comment/blank -> RawBlock
```

导出优化：

```text
Position X/Y segment boundaries + easing/interpolation/handles 完全对齐 -> M
否则 -> MX + MY

Scale X/Y 全程相等且 metadata 对齐 -> S
否则 -> V
```

Loop / Trigger 内部命令不参与普通 PropertyTrack 的 segment boundary 检查，由各自 exporter 处理。

Bezier 导出模式：

```text
WarnOnly
FitNearestOsuEasing
BakeToSegments
```

默认 `WarnOnly`，不得静默丢失不可表达曲线。

**8. 渲染与性能**
Render Worker 异步消费 `RenderRequest`，UI 只接受：

```text
result.Revision == state.Revision
```

质量预设：

```text
FullPreview
InteractiveScrub
FastScrub
TimelineThumbnail
```

scrub 时允许半分辨率/四分辨率、丢弃旧 request、取消正在渲染的过期 request。`SelectedAndContext` 必须按确定规则选图层：selected layers、相邻可见层、背景上下文、fullscreen quads、missing placeholders，并受 `MaxRenderedLayers` 限制。

**9. Undo / Redo 模型**
MVP 是严格线性 undo/redo：

```text
不支持选择性 undo
不支持 undo tree
只能 LIFO
```

任何成功的新 document-mutating edit 都会清空 redo stack。清空 redo stack 必须与新 transaction 成功入栈原子绑定。失败编辑不得改变 undo stack 或 redo stack。

`.storybrewcomp` 不持久化 undo stack。重新打开文件后，不能依赖 undo 还原已展开 block；`ExpandedBlockHistory` 仅用于审计、注释导出和未来 restore 工具。

**10. ExpandBlockTransaction**
```csharp
public sealed class ExpandBlockTransaction
{
    public string TransactionId { get; init; }
    public string LayerId { get; init; }

    public StoryboardBlockSnapshot OriginalBlockSnapshot { get; init; }
    public ExpandedBlockHistory CreatedHistory { get; init; }

    public List<string> GeneratedCommandIds { get; init; }
    public List<GeneratedCommandSnapshot> GeneratedCommandsAfter { get; init; }

    public List<CommandRecordSnapshot> AffectedCommandRecordsBefore { get; init; }
    public List<CommandRecordSnapshot> AffectedCommandRecordsAfter { get; init; }

    public List<RawBlockAnchorSnapshot> RawBlockAnchorsBefore { get; init; }
    public List<RawBlockAnchorSnapshot> RawBlockAnchorsAfter { get; init; }
}
```

创建时必须校验：

```text
Set(GeneratedCommandIds) == Set(GeneratedCommandsAfter.CommandId)
Generated ids 无重复
OriginalBlockSnapshot header/relative ids 均存在于 AffectedCommandRecordsBefore
RawBlockAnchorsBefore.RawBlockId set == RawBlockAnchorsAfter.RawBlockId set
```

`GeneratedCommandIds` 是 undo 删除权威列表。`GeneratedCommandsAfter` 是 redo replay 权威 payload。

**11. Undo / Redo 原子性**
`DocumentSnapshot` 是 execution-scoped rollback state：

```text
单次 undo/redo/edit attempt 开始时创建
成功或回滚后释放
不得进入 transaction
不得进入 undo/redo stack
```

Undo 流程：

```text
capture PreUndo DocumentSnapshot
validate
remove generated active commands by GeneratedCommandIds
remove commandRecords for GeneratedCommandIds
restore AffectedCommandRecordsBefore
restore RawBlockAnchorsBefore
recreate OriginalBlockSnapshot
insert block by BlockInsertionAnchor
remove CreatedHistory
post-undo validation
failure -> restore PreUndo snapshot, keep transaction on undo stack
```

Redo 是 replay，不是 recompute：

```text
capture PreRedo DocumentSnapshot
validate
remove original block from active tree
insert GeneratedCommandsAfter
restore AffectedCommandRecordsAfter
restore RawBlockAnchorsAfter
add CreatedHistory
post-redo validation
failure -> restore PreRedo snapshot, keep transaction on redo stack
```

Redo 不得重新运行 expand algorithm，不得重新生成 ids。

**12. DerivedCommandIds**
展开时：

```text
HeaderCommandRecord.DerivedCommandIds =
  block 展开产生的全部 generated command ids

RelativeCommand records DerivedCommandIds =
  各自 relative command 产生的 generated command ids
```

不变量：

```text
HeaderCommandRecord.DerivedCommandIds
  == non-header split records DerivedCommandIds 的不相交并集
```

每个 generated id 必须恰好有一个 relative source。header-only generated id 只允许在产生 `EXPAND_DERIVED_SOURCE_AMBIGUOUS` warning 时存在。`DerivedCommandIds` 是 provenance，不是 undo 删除范围。

**13. Snapshot 规则**
所有 `*Snapshot` 必须深拷贝，不得引用 live model。

分类：

```text
DocumentSnapshot: execution-scoped, rollback-only
StoryboardBlockSnapshot: transaction-persistent, undo-before-state
CommandRecordSnapshot: transaction/serialization persistent
GeneratedCommandSnapshot: transaction-persistent, redo-after-state
RawBlockAnchorSnapshot: transaction-persistent, undo/redo anchor state
SourceReferenceSnapshot: embedded-persistent, provenance-only
```

`SourceReferenceSnapshot` 不作为独立顶层状态存在，只随宿主 snapshot 持久化。

**14. Validation Scope**
不变量分三类：

```text
document-state: 影响当前文档和 .osb 导出
transaction-history: 影响 undo/redo
serialization: 影响 .storybrewcomp 读写
```

入口：

```text
create
deserialize-load
undo-restore
redo-restore
pre-osb-export
pre-storybrewcomp-save
debug-integrity-scan
```

`pre-osb-export` 只跑 document-state invariants。  
`pre-storybrewcomp-save` 跑 document-state、transaction-history、serialization invariants。  
MVP 无 recovery mode；损坏 `.storybrewcomp` 加载失败并报告 diagnostics，不自动修复、不部分加载 undo stack。

**15. Phase 1 Gate**
Phase 1 第一个交付物不是 parser，而是：

```text
Authoritative Invariants and Validation Matrix
```

必须包含：

```text
所有 snapshot 类型定义
snapshot 生命周期与持久化分类
所有 id-set / provenance / anchor 不变量
所有校验入口
scope 标注
failure code
hard error vs warning
N/A 原因
```

重点不变量至少包括：

```text
GeneratedCommandIds == GeneratedCommandsAfter.CommandId set
Generated ids 无重复
OriginalBlockSnapshot ids 在 AffectedCommandRecordsBefore 中完整
AffectedCommandRecordsAfter 包含 generated records 和 source split records
DerivedCommandIds partition
RawBlockAnchorsBefore/After RawBlockId set 相等
AfterCommand anchor target 存在
RelativeCommand.Id == CommandRecord.CommandId
Block.HeaderCommandId record 存在且 parent 匹配
ids opaque，不做 hash-content validation
redo stack 清空只随成功 edit commit 发生
```

**16. MVP 验收标准**
MVP 完成条件：

```text
能打开复杂 .osb
能显示 Sprite / Animation / Loop / Trigger / Variables
能保留 unknown/comment/raw content
能显示缺失素材 placeholder
能 scrub 时间轴并预览
能编辑基础 Transform keyframes
能执行线性 undo/redo
能保存 .storybrewcomp v2
能导出 osu! 可读 .osb
导入/导出/验证 diagnostics 可见
Render Worker 不阻塞 UI
```

**17. 实施顺序**
推荐顺序：

```text
Phase 1A: Authoritative Invariants and Validation Matrix
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

**结论**
v1.7 的核心取向是：`.storybrewcomp` 承载可编辑高级结构，`.osb` 作为保真导入/导出目标；Loop、Trigger、Variables、RawBlock、CommandRecord 都是一等模型；ID 是持久 opaque identity；Expand To Keyframes 是可 undo/redo 的原子事务；Phase 1 必须先完成不变量矩阵，再进入 parser 和编辑器实现。

**v1.7 Freeze Patch**

**1. 补回 BlockInsertionAnchor 字段**
§10 的 `ExpandBlockTransaction` 必须补 `InsertionAnchor`，否则 §11 的 undo 插回步骤没有数据来源。

修订后的定义：

```csharp
public sealed class ExpandBlockTransaction
{
    public string TransactionId { get; init; }
    public string LayerId { get; init; }

    public StoryboardBlockSnapshot OriginalBlockSnapshot { get; init; }
    public BlockInsertionAnchor InsertionAnchor { get; init; }
    public ExpandedBlockHistory CreatedHistory { get; init; }

    public List<string> GeneratedCommandIds { get; init; }
    public List<GeneratedCommandSnapshot> GeneratedCommandsAfter { get; init; }

    public List<CommandRecordSnapshot> AffectedCommandRecordsBefore { get; init; }
    public List<CommandRecordSnapshot> AffectedCommandRecordsAfter { get; init; }

    public List<RawBlockAnchorSnapshot> RawBlockAnchorsBefore { get; init; }
    public List<RawBlockAnchorSnapshot> RawBlockAnchorsAfter { get; init; }
}
```

`BlockInsertionAnchor` 定义保持 v1.0/v1.1 语义：

```csharp
public sealed class BlockInsertionAnchor
{
    public string LayerId { get; init; }

    public string? PreviousSiblingBlockId { get; init; }
    public string? NextSiblingBlockId { get; init; }

    public int OriginalIndexFallback { get; init; }
}
```

undo 插回规则：

```text
1. PreviousSiblingBlockId 存在于目标 layer:
     插入到它之后。

2. 否则 NextSiblingBlockId 存在于目标 layer:
     插入到它之前。

3. 否则使用 OriginalIndexFallback clamp 到当前 collection 范围。
     emit UNDO_INSERTION_ANCHOR_FALLBACK warning。

4. LayerId 不存在:
     abort undo
     restore PreUndo DocumentSnapshot
     keep transaction on undo stack
     emit UNDO_LAYER_MISSING error.
```

`OriginalBlockSnapshot` 可以保留 block 自身数据；`InsertionAnchor` 是 transaction 级定位数据。这样 §10 和 §11 对齐。

**2. 补全 GeneratedCommandsAfter 校验**
§10 创建时校验改为完整列表：

```text
Set(GeneratedCommandIds) == Set(GeneratedCommandsAfter.Select(CommandId))

GeneratedCommandIds contains no duplicates.

GeneratedCommandsAfter contains no duplicate CommandId.

Every GeneratedCommandSnapshot.CommandId is non-empty.

Every GeneratedCommandSnapshot.CommandId is present in GeneratedCommandIds.

Every GeneratedCommandIds item has exactly one matching GeneratedCommandSnapshot.
```

失败码：

```text
EXPAND_TRANSACTION_GENERATED_SET_MISMATCH
EXPAND_TRANSACTION_GENERATED_ID_DUPLICATE
EXPAND_TRANSACTION_GENERATED_SNAPSHOT_ID_EMPTY
```

§15 Phase 1 gate 的重点不变量也同步修订为：

```text
GeneratedCommandIds == GeneratedCommandsAfter.CommandId set
GeneratedCommandIds 无重复
GeneratedCommandsAfter.CommandId 无重复
GeneratedCommandSnapshot.CommandId 非空
```

这避免矩阵只校验 `GeneratedCommandIds` 一侧而漏掉 redo replay payload。

**3. 明确失败 undo/redo 不改变历史栈**
§9 / §11 增补统一规则：

```text
Failed undo / redo attempts are not document-mutating edits.

If undo or redo fails:
  restore the pre-operation DocumentSnapshot
  keep undo stack unchanged
  keep redo stack unchanged
  do not clear redo stack
  emit error diagnostic
```

具体到 undo：

```text
undo failure:
  restore PreUndo DocumentSnapshot
  transaction remains on undo stack
  redo stack unchanged
```

具体到 redo：

```text
redo failure:
  restore PreRedo DocumentSnapshot
  transaction remains on redo stack
  undo stack unchanged
```

因此 redo stack 清空只发生在：

```text
successful new document-mutating edit commit after undo
```

不发生在：

```text
failed edit attempt
failed undo attempt
failed redo attempt
selection / viewport / scrub
```

**4. Cleanup 的 undo/redo history 引用范围**
§5 追加澄清：

```text
CommandRecord cleanup 检查 undo/redo history 引用时，只检查当前编辑会话内存中的 undo stack / redo stack。
.storybrewcomp 不持久化 undo stack / redo stack。
重新打开文件后，不存在需要检查的持久化 history stack 引用。
```

因此 safe cleanup 条件中的：

```text
not referenced by current undo/redo history
```

应理解为：

```text
not referenced by in-memory history stacks of the current session
```

**冻结结论**
这三个修订补上后，v1.7 可以冻结为 Phase 1A 输入稿。下一步不建议继续开新设计版本，而是直接产出：

```text
Authoritative Invariants and Validation Matrix
```

矩阵里每条 invariant 至少列：

```text
id
scope
statement
entrypoints
validator name
hard error / warning
failure code
N/A reason
minimum tests
```

这会把剩余风险从“文档散落”转成“可执行校验是否覆盖”，正好进入实现前的最后一道门。

**Final Freeze Addendum**

在 §10 `ExpandBlockTransaction` 创建时校验中追加：

```text
InsertionAnchor.LayerId == transaction.LayerId
```

失败：

```text
abort transaction creation
emit EXPAND_TRANSACTION_INSERTION_ANCHOR_LAYER_MISMATCH
```

语义：

```text
MVP 中 Expand To Keyframes 不跨 layer。
transaction.LayerId 是事务目标 layer。
InsertionAnchor.LayerId 是原 block 插回 layer。
二者必须一致。
```

未来如果支持跨 layer transform / selective restore，再重新设计这条 invariant；MVP 不放开。

**冻结状态**

v1.7 + Freeze Patch + Final Freeze Addendum 可以冻结为 Phase 1A 输入稿。

下一步产出矩阵时建议直接采用这个表头：

```text
ID
Scope
Invariant Statement
Entry Points
Severity
Failure Codes
N/A Reason
Minimum Tests
```

并把 `InsertionAnchor.LayerId == transaction.LayerId` 加入 transaction-history / serialization 范围，入口至少包括：

```text
create
deserialize-load
undo-restore
debug-integrity-scan
```

severity：

```text
hard error
```

failure codes：

```text
EXPAND_TRANSACTION_INSERTION_ANCHOR_LAYER_MISMATCH
LOAD_TRANSACTION_INSERTION_ANCHOR_LAYER_MISMATCH
UNDO_TRANSACTION_INSERTION_ANCHOR_LAYER_MISMATCH
```

至此设计稿确实可以不再继续开新版本了。现在该让矩阵来抓最后的漏网项。

确认。冻结状态成立：

```text
v1.7 整合稿
+ Freeze Patch
+ Final Freeze Addendum
= Phase 1A 输入稿，冻结
```

下一步我建议直接进入 Phase 1A，产出八列表格：

```text
ID
Scope
Invariant Statement
Entry Points
Severity
Failure Codes
N/A Reason
Minimum Tests
```

矩阵第一批可以从这些 invariant 开始：

```text
A. Generated command set consistency
B. Generated command uniqueness
C. Original block records completeness
D. Redo after records completeness
E. DerivedCommandIds partition
F. RawBlock anchor before/after set consistency
G. RawBlock anchor target existence
H. RelativeCommand / CommandRecord identity
I. HeaderCommand identity
J. Opaque persistent ids
K. Redo stack integrity
L. InsertionAnchor layer consistency
```

这 12 条就是冻结稿的第一圈护城河。下一份产物不需要再解释设计动机，直接做成工程表：validator 名、入口、错误码、最小测试，让实现者能一行一行照着落地。

下面是 **Phase 1A Authoritative Invariants and Validation Matrix 工程表 v1**。我在原八列基础上加了 `Validator` 一列，因为实现时需要能直接落到校验函数名；其余列保持冻结稿计划。

**Phase 1A Invariants Matrix**

| ID | Scope | Validator | Invariant Statement | Entry Points | Severity | Failure Codes | N/A Reason | Minimum Tests |
|---|---|---|---|---|---|---|---|---|
| A | transaction-history, serialization | `ValidateGeneratedCommandSetConsistency` | `Set(GeneratedCommandIds) == Set(GeneratedCommandsAfter.CommandId)` | `create`, `deserialize-load`, `undo-restore`, `redo-restore`, `pre-storybrewcomp-save`, `debug-integrity-scan` | hard error | `EXPAND_TRANSACTION_GENERATED_SET_MISMATCH`, `LOAD_TRANSACTION_GENERATED_SET_MISMATCH`, `UNDO_TRANSACTION_GENERATED_SET_MISMATCH`, `REDO_TRANSACTION_GENERATED_SET_MISMATCH`, `SAVE_TRANSACTION_GENERATED_SET_MISMATCH` | `pre-osb-export` N/A: transaction payload 不影响当前 `.osb` 导出，只影响 undo/redo 与 `.storybrewcomp` 保存 | 构造 transaction：ids 为 `[a,b]`，snapshots 为 `[a,c]`，create 失败；加载同样损坏 transaction 失败；redo 前检测 mismatch 并回滚 |
| B | transaction-history, serialization | `ValidateGeneratedCommandUniqueness` | `GeneratedCommandIds` 无重复；`GeneratedCommandsAfter.CommandId` 无重复；每个 `GeneratedCommandSnapshot.CommandId` 非空 | `create`, `deserialize-load`, `redo-restore`, `pre-storybrewcomp-save`, `debug-integrity-scan` | hard error | `EXPAND_TRANSACTION_GENERATED_ID_DUPLICATE`, `EXPAND_TRANSACTION_GENERATED_SNAPSHOT_ID_DUPLICATE`, `EXPAND_TRANSACTION_GENERATED_SNAPSHOT_ID_EMPTY`, `LOAD_TRANSACTION_GENERATED_ID_DUPLICATE`, `REDO_TRANSACTION_GENERATED_ID_DUPLICATE` | `undo-restore` 只消费 `GeneratedCommandIds` 删除列表，重复/空 id 应在 create/load 阶段已阻断；可在 debug scan 重查。`pre-osb-export` N/A 同 A | `GeneratedCommandIds=[a,a]` 失败；`GeneratedCommandsAfter=[a,a]` 失败；snapshot id 为空失败 |
| C | transaction-history, serialization | `ValidateOriginalBlockRecordCompleteness` | `AffectedCommandRecordsBefore` 必须包含 `OriginalBlockSnapshot.HeaderCommandId`，且包含每个 `RelativeCommandSnapshot.Id` | `create`, `deserialize-load`, `undo-restore`, `debug-integrity-scan` | hard error | `EXPAND_TRANSACTION_RECORD_SNAPSHOT_INCOMPLETE`, `LOAD_TRANSACTION_RECORD_SNAPSHOT_INCOMPLETE`, `UNDO_COMMAND_RECORD_MISSING` | `redo-restore` N/A: redo 移除 original block，不重建它；`pre-osb-export` N/A: 只影响 undo-before-state | 构造 snapshot 有 header `h1`，before records 缺 `h1`，create 失败；relative command `r1` 缺 before record，undo 前失败并回滚 |
| D | transaction-history, serialization | `ValidateRedoAfterRecordCompleteness` | `AffectedCommandRecordsAfter` 必须包含所有 generated command records、source header split record、source relative split records | `create`, `deserialize-load`, `redo-restore`, `debug-integrity-scan` | hard error | `EXPAND_TRANSACTION_AFTER_RECORDS_INCOMPLETE`, `LOAD_TRANSACTION_AFTER_RECORDS_INCOMPLETE`, `REDO_COMMAND_RECORD_MISSING` | `undo-restore` N/A: undo 使用 before records；`pre-osb-export` N/A: redo payload 不参与 `.osb` 导出；RawBlock 无 CommandRecord，anchor 由 F/G 管 | redo after records 缺 generated record `g1`，redo 失败回滚；缺 header split record，create 失败 |
| E | document-state, transaction-history, serialization | `ValidateDerivedCommandPartition` | 对每个 expanded block：`HeaderCommandRecord.DerivedCommandIds == disjoint union(non-header split records DerivedCommandIds)`；header-only id 只允许伴随 `EXPAND_DERIVED_SOURCE_AMBIGUOUS` | `create`, `deserialize-load`, `undo-restore`, `redo-restore`, `pre-osb-export`, `pre-storybrewcomp-save`, `debug-integrity-scan` | hard error；ambiguous case 为 warning + explicit marker | `DERIVED_ID_MISSING_RELATIVE_SOURCE`, `DERIVED_ID_MULTIPLE_RELATIVE_SOURCES`, `DERIVED_ID_RELATIVE_NOT_IN_HEADER`, `DERIVED_ID_HEADER_ONLY_WITHOUT_WARNING`, `EXPAND_DERIVED_SOURCE_AMBIGUOUS` | 无 N/A。该 provenance 同时影响当前文档一致性、事务历史和保存 | header 有 `g1`，无 relative source，且无 ambiguous warning -> error；两个 relative records 都含 `g1` -> error；relative 含 `g2` 但 header 缺 -> error |
| F | transaction-history, serialization | `ValidateRawBlockAnchorSnapshotSetConsistency` | `Set(RawBlockAnchorsBefore.RawBlockId) == Set(RawBlockAnchorsAfter.RawBlockId)`；同一个 changed RawBlock 必须有 before/after | `create`, `deserialize-load`, `undo-restore`, `redo-restore`, `pre-storybrewcomp-save`, `debug-integrity-scan` | hard error | `EXPAND_TRANSACTION_RAWBLOCK_ANCHOR_SET_MISMATCH`, `LOAD_RAWBLOCK_ANCHOR_SET_MISMATCH`, `UNDO_RAWBLOCK_ANCHOR_SET_MISMATCH`, `REDO_RAWBLOCK_ANCHOR_SET_MISMATCH` | `pre-osb-export` N/A: before/after transaction snapshots 不参与当前 `.osb` 导出；当前 anchor 有效性由 G 检查 | before 有 `[raw1]`，after 有 `[]`，create 失败；load 损坏 transaction 失败 |
| G | document-state, transaction-history, serialization | `ValidateRawBlockAnchorTargetExistence` | `LayerStart => AnchorCommandId == null`；`AfterCommand => AnchorCommandId != null && commandRecords contains AnchorCommandId` | `create`, `deserialize-load`, `undo-restore`, `redo-restore`, `pre-osb-export`, `pre-storybrewcomp-save`, `debug-integrity-scan` | hard error for invalid combination / missing target during save; warning may be emitted during import rebase before final validation | `RAW_ANCHOR_INVALID_LAYER_START`, `RAW_ANCHOR_AFTER_COMMAND_MISSING_ID`, `RAW_ANCHOR_TARGET_MISSING`, `UNDO_RAWBLOCK_ANCHOR_RESTORE_FAILED`, `REDO_RAWBLOCK_ANCHOR_RESTORE_FAILED` | 无 N/A。当前 `.osb` 导出必须知道 raw 插回位置 | `LayerStart + cmd1` 失败；`AfterCommand + null` 失败；`AfterCommand + missing cmd` 在 pre-osb-export 阻止导出或要求先 rebase |
| H | document-state, serialization | `ValidateRelativeCommandRecordIdentity` | 每个 active `RelativeCommand.Id` 必须存在于 `commandRecords`；`commandRecords[key].CommandId == key == RelativeCommand.Id`；`ParentBlockId / ParentLayerId` 匹配 owner | `create`, `deserialize-load`, `undo-restore`, `redo-restore`, `pre-osb-export`, `pre-storybrewcomp-save`, `debug-integrity-scan` | hard error | `RELATIVE_COMMAND_RECORD_MISSING`, `COMMAND_RECORD_KEY_MISMATCH`, `RELATIVE_COMMAND_PARENT_BLOCK_MISMATCH`, `RELATIVE_COMMAND_PARENT_LAYER_MISMATCH` | 无 N/A。active relative command 没有 record 会破坏 export/source tracking | block 内 command id `cmd1`，records 缺 `cmd1` -> error；record key `cmd1` 但 `CommandId=cmd2` -> error；ParentBlockId 指向别的 block -> error |
| I | document-state, serialization | `ValidateHeaderCommandIdentity` | 每个 active block 的 `HeaderCommandId` 必须存在于 `commandRecords`；record key/CommandId 匹配；`ParentBlockId == block.Id`；`ParentLayerId == owning layer.Id` | `create`, `deserialize-load`, `undo-restore`, `redo-restore`, `pre-osb-export`, `pre-storybrewcomp-save`, `debug-integrity-scan` | hard error | `HEADER_COMMAND_RECORD_MISSING`, `HEADER_COMMAND_KEY_MISMATCH`, `HEADER_COMMAND_PARENT_BLOCK_MISMATCH`, `HEADER_COMMAND_PARENT_LAYER_MISMATCH` | 无 N/A。L/T header 是实际 OSB command line | LoopBlock header `h1`，records 缺 `h1` -> error；header record parentBlockId 不等于 block id -> error |
| J | document-state, serialization | `ValidateOpaquePersistentIds` | `Block.Id` / `CommandId` 非空、唯一、引用有效；任何 validation path 不得重算 hash 并比较 current content | `deserialize-load`, `move-block`, `move-command`, `pre-osb-export`, `pre-storybrewcomp-save`, `debug-integrity-scan` | hard error for duplicate/missing ids; implementation rule violation for hash-content validation | `ID_EMPTY`, `ID_DUPLICATE`, `ID_REFERENCE_MISSING`, `OPAQUE_ID_HASH_RECOMPUTE_FORBIDDEN` | `create` N/A only for already allocated persisted ids; allocation path另由 id generator tests 覆盖。`undo/redo-restore` 通过 H/I/G 等引用校验覆盖 | 两个 blocks 同 id -> load error；移动 block 后 id 不变且不触发 hash mismatch；测试 validator 不读取 current content 计算 hash |
| K | transaction-history | `ValidateRedoStackIntegrity` | 成功的新 document-mutating edit after undo 必须清空 redo stack；失败 edit/undo/redo 不改变 undo stack、redo stack 或最终文档状态 | `edit-commit`, `edit-failure`, `undo`, `redo`, `debug-integrity-scan` | hard error for history mutation bug | `REDO_STACK_NOT_CLEARED_AFTER_EDIT`, `REDO_STACK_CLEARED_ON_FAILED_EDIT`, `UNDO_STACK_MUTATED_ON_FAILED_UNDO`, `REDO_STACK_MUTATED_ON_FAILED_REDO` | `deserialize-load` N/A: undo/redo stack 不持久化。`pre-osb-export` / `pre-storybrewcomp-save` N/A: history stack 不影响导出内容，且不持久化 | undo 后 redo stack 有 A；成功 edit B 后 redo 清空；失败 edit B 后 redo 仍有 A；失败 undo 后两个栈快照相同 |
| L | transaction-history, serialization | `ValidateInsertionAnchorLayerConsistency` | `ExpandBlockTransaction.InsertionAnchor.LayerId == ExpandBlockTransaction.LayerId` | `create`, `deserialize-load`, `undo-restore`, `debug-integrity-scan` | hard error | `EXPAND_TRANSACTION_INSERTION_ANCHOR_LAYER_MISMATCH`, `LOAD_TRANSACTION_INSERTION_ANCHOR_LAYER_MISMATCH`, `UNDO_TRANSACTION_INSERTION_ANCHOR_LAYER_MISMATCH` | `redo-restore` N/A: redo 不重建 original block，`InsertionAnchor` 不参与 redo。`pre-osb-export` N/A: transaction insertion anchor 不影响当前 `.osb` 导出 | transaction.LayerId=`layerA`，anchor.LayerId=`layerB`，create/load 失败；undo 前检测 mismatch，回滚并保留 transaction |

**补充实现约定**
矩阵里的 validators 建议分两层：

```text
Transaction validators:
  ValidateGeneratedCommandSetConsistency
  ValidateGeneratedCommandUniqueness
  ValidateOriginalBlockRecordCompleteness
  ValidateRedoAfterRecordCompleteness
  ValidateRawBlockAnchorSnapshotSetConsistency
  ValidateInsertionAnchorLayerConsistency

Document validators:
  ValidateDerivedCommandPartition
  ValidateRawBlockAnchorTargetExistence
  ValidateRelativeCommandRecordIdentity
  ValidateHeaderCommandIdentity
  ValidateOpaquePersistentIds

History validators:
  ValidateRedoStackIntegrity
```

`pre-storybrewcomp-save` 应运行 document + transaction validators。  
`pre-osb-export` 只运行 document-state validators：E/G/H/I/J。  
损坏 `.storybrewcomp` 在 MVP 中 fail load，不进入 recovery mode。