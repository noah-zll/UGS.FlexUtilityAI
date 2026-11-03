# UGS FlexUtilityAI 使用指南（中文）

本指南以“能在项目里跑起来 + 能调试 + 能扩展”为目标，覆盖从安装到复杂 Demo 调试、运行时动态变更、执行器接入与常见坑位。

适用版本：0.2.0（以 [package.json](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/package.json) 为准）

---

## 1. 你将得到什么

本插件提供一套 Utility AI（效用型 AI）决策框架，核心由以下几部分构成：

- Profile（资产配置）：[UtilityAIProfile](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIProfile.cs)
  - 可配置：Action 列表、聚合方式（Product / WeightedAverage）、每个 Action 的 Consideration 列表、权重、阈值等
- Brain（运行时决策器）：[UtilityAIBrain](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIBrain.cs)
  - 负责：定时评估、选择最佳 Action、切换稳定性（防抖/最小驻留）、输出调试快照、驱动执行器生命周期
- Runtime Model（可变实例）：[UtilityAIRuntimeModel](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIRuntimeModel.cs)
  - 支持：运行时增删 Action/Consideration、启用/禁用、调整权重/冷却/最小切换间隔
- Blackboard（黑板）：[UtilityAIBlackboard](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIBlackboard.cs)
  - 当前版本以 float 为主：`brain.SetFloat(key, value)` 写入、Consideration 通过 key 读取
- 执行器（把决策接到你的游戏逻辑）：[IUtilityAIActionExecutor](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/IUtilityAIActionExecutor.cs) / [UtilityAIActionExecutor](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIActionExecutor.cs)
- 调试快照与 Inspector：快照类型 [UtilityAIDebugSnapshot](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIDebugSnapshot.cs)，Inspector [UtilityAIBrainEditor](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Editor/Inspectors/UtilityAIBrainEditor.cs)

---

## 2. 安装与导入 Samples

### 2.1 安装 Package

将 `UGS.FlexUtilityAI` 作为 Unity Package 引入到项目（例如本地路径或 Git URL）。安装成功后，Unity 会自动加载 Runtime/Editor asmdef。

### 2.2 导入 Samples（推荐）

本包在 `package.json` 中注册了 Sample：

- XiuXian Demo（修仙示例）

在 Package Manager 中找到本包，展开 Samples，点击 Import。

导入后的路径一般类似：

- `Assets/Samples/UGS FlexUtilityAI/0.1.0/XiuXian Demo/...`

---

## 3. 最小可运行：用 Profile 驱动一个 Agent

这一节给你最短路径：在场景里看到 Action 被选中并持续变化。

### 3.1 创建 Profile

在 Project 面板中：

- Create → UGS → FlexUtilityAI → Utility AI Profile

生成资产类型为 [UtilityAIProfile](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIProfile.cs)。

在 Profile 里配置：

- Aggregation Mode：`Product` 或 `WeightedAverage`
- Actions：每个 Action 包含
  - `id`：ActionId（必须唯一）
  - `enabled`：是否候选可用
  - `weight`：Action 权重（最终会乘到得分上）
  - `cooldownSeconds`：冷却（非当前 Action 时若在冷却内会被判定不可用）
  - `minSwitchIntervalSeconds`：最小切换间隔（比全局 minTimeInStateSeconds 更“就地覆盖”）
  - Considerations：每个 Consideration 包含
    - `blackboardFloatKey`：从黑板读取 float 的 key
    - `inputMin/inputMax`：归一化范围
    - `response`：曲线映射（0..1 → 0..1）
    - `weight`：权重（影响强度）
    - `invert`：反向（1 - value）
    - `useFailThreshold/failIfBelow`：否决阈值（低于则 Action 直接变为 0/不可取）

### 3.2 场景对象挂载 Brain

在场景中新建一个 GameObject（Agent），添加组件：

- [UtilityAIBrain](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIBrain.cs)

把 Profile 拖给 `profile` 字段。

### 3.3 往 Blackboard 写值（必须）

否则 Consideration 读不到数据，默认值通常会导致 ActionScore 很低或不可用。

最简单方式：写一个 MonoBehaviour 每帧写黑板。

```csharp
using UnityEngine;
using UGS.FlexUtilityAI;

public sealed class FeedBlackboardExample : MonoBehaviour
{
    [SerializeField] private UtilityAIBrain brain;

    private void Reset()
    {
        brain = GetComponent<UtilityAIBrain>();
    }

    private void Update()
    {
        if (brain == null) return;

        brain.SetFloat("Health", 0.7f);
        brain.SetFloat("Danger", Mathf.PingPong(Time.time * 0.1f, 1f));
        brain.SetFloat("EnemyDistance", Mathf.PingPong(Time.time, 30f));
    }
}
```

### 3.4 观察结果

进入 Play Mode 后，你应该能在 Inspector 的 “Decision Debug / Decision History” 里看到：

- Current / Best
- 每个 Action 的 Score 与 Eligible
- 每个 Consideration 的 raw/normalized/response/weighted

见 [UtilityAIBrainEditor](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Editor/Inspectors/UtilityAIBrainEditor.cs)。

---

## 4. Brain 运行参数说明（调参最常用）

以下字段在 [UtilityAIBrain](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIBrain.cs) 中：

### 4.1 评估频率

- `evaluateInUpdate`
  - 勾选：每帧（或按 interval）自动评估
  - 关闭：你只能手动调用 `EvaluateOnce()`，或用 Inspector 的 “Evaluate Now”
- `evaluationIntervalSeconds`
  - `<= 0`：每帧评估
  - `> 0`：达到间隔才评估（适合大量 Agent 降低成本）

### 4.2 行为切换稳定性（防抖）

- `minTimeInStateSeconds`
  - 进入某个 Action 后至少持续这么久（同时会与 Action 自身的 `MinSwitchIntervalSeconds` 取更大值）
- `switchBias`
  - 迟滞比例：新最佳行为需要满足 `bestScore >= currentScore * (1 + switchBias)` 才切换
- `switchScoreDelta`
  - 迟滞差值：在比例门槛基础上再加一个绝对差值（避免分数很小也频繁切）

调试建议：

- “一直抖动”：增大 `minTimeInStateSeconds` 或 `switchScoreDelta`
- “太保守不切”：减小上述参数，或检查是否某些 Action 长期冷却/禁用

### 4.3 无 Profile 时是否自动建空模型（纯代码 Demo 常用）

- `createEmptyModelIfProfileMissing`
  - 勾选后：Brain 在没有 profile 的情况下也会创建空 RuntimeModel
- `emptyModelAggregationMode`
  - 空模型默认使用的聚合方式

另外还有一个更直接的 API：

- `brain.EnsureModelOrCreateEmpty()`

### 4.4 调试与录制

- `enableDebugSnapshot`
  - 生成 `LastDecisionSnapshot`，供 Inspector/HUD 读取
- `enableDecisionRecording`
  - 启用历史录制：`DecisionHistory`
- `decisionRecordingCapacity`
  - 最大历史条数（超过会丢弃最旧记录）
- `recordBreakdown`
  - 即使不启用 `enableDebugSnapshot` 也记录细分（Consideration 列表）
- `logOnActionSwitch`
  - Action 发生切换时输出日志（含 reason/score）

快照结构见 [UtilityAIDebugSnapshot](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIDebugSnapshot.cs)。

---

## 5. 评分机制（Product / WeightedAverage）

评分实现见 [UtilityAIScorer](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIScorer.cs)。

### 5.1 Consideration 从黑板取值与归一化

- 从黑板读取 `raw`：`blackboard.GetFloatOrDefault(key, 0f)`
- 归一化：`normalized = InverseLerp(inputMin, inputMax, raw)`，并 Clamp 到 0..1
- 曲线映射：`responseValue = response.Evaluate(normalized)`，Clamp 到 0..1
- 反向：`invert` 时 `responseValue = 1 - responseValue`

### 5.2 权重如何影响分数

当前实现的权重影响属于“强度插值”：

- `weightedValue = Lerp(1, responseValue, NormalizeWeight01(weight))`

含义：

- `weight = 0`：该因素几乎不影响（趋近 1）
- `weight >= 1`：完全按 responseValue 影响

### 5.3 Fail Threshold（否决阈值）

若某 Consideration 启用 `useFailThreshold` 且 `weightedValue < failIfBelow`：

- Product：直接将 product 置为 0
- WeightedAverage：直接将 sum 置 0（并 break）

适用于“硬条件”：例如法力不足则不能施法。

---

## 6. 把决策接到你的游戏逻辑：执行器（Executor）

决策器只负责“选哪个 Action”，真正执行行为要靠执行器。

### 6.1 接入方式

在同一个 Agent GameObject 上，挂载若干个实现了 [IUtilityAIActionExecutor](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/IUtilityAIActionExecutor.cs) 的组件。

Brain 会在 `RefreshExecutors()` 时扫描同物体上的所有 `MonoBehaviour`，按 `ActionId` 映射。

### 6.2 生命周期

当 Action 发生切换时：

- 上一个 Action 的 executor：`OnExit(brain)`
- 新 Action 的 executor：`OnEnter(brain)`
- 每帧：当前 executor 的 `OnTick(brain, deltaTime)`

### 6.3 推荐写法（继承基类）

```csharp
using UnityEngine;
using UGS.FlexUtilityAI;

public sealed class ChaseExecutor : UtilityAIActionExecutor
{
    public override void OnEnter(UtilityAIBrain brain)
    {
        // 启动导航/播放动画/锁定目标...
    }

    public override void OnTick(UtilityAIBrain brain, float deltaTime)
    {
        // 持续追击逻辑...
    }

    public override void OnExit(UtilityAIBrain brain)
    {
        // 收尾逻辑...
    }
}
```

基类见 [UtilityAIActionExecutor](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIActionExecutor.cs)。

### 6.4 常见坑：忘了刷新执行器

如果你在运行时动态添加/移除 executor 组件，或动态改 ActionId，需要调用：

- `brain.RefreshExecutors()`

---

## 7. 运行时动态变更（你关心的“可演化 AI”）

动态变更建议通过 RuntimeModel 做，而不要直接改 Profile 资产。

RuntimeModel API 见 [UtilityAIRuntimeModel](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIRuntimeModel.cs)，Brain 提供了一些便捷封装：

- `brain.AddAction(id)`
- `brain.RemoveAction(id)`
- `brain.Model.FindAction(id)`（找到后可直接改 `Enabled/Weight/...`）

### 7.1 动态启用/禁用 Action

```csharp
var action = brain.Model.FindAction("Breakthrough");
if (action != null)
{
    action.Enabled = true;
    brain.Model.Touch(); // 提升 ModelVersion，便于调试侧感知变更
}
```

### 7.2 动态增删 Consideration

```csharp
var action = brain.Model.FindAction("Explore");
if (action != null)
{
    var c = action.AddConsideration("WeatherBad");
    c.BlackboardFloatKey = "WeatherBad";
    c.InputMin = 0f;
    c.InputMax = 1f;
    c.Invert = true;
    c.Weight = 0.6f;
    brain.Model.Touch();
}
```

### 7.3 动态增删 Action

```csharp
brain.AddAction("DrinkElixir");
brain.Model.Touch();
brain.RefreshExecutors(); // 如果你也为它增加了 executor
```

### 7.4 当前 Action 被移除的行为

当你 `RemoveAction(currentActionId)` 时，Brain 会退出当前 executor，并把当前状态清空，下一次评估会选一个替代行为。

见 [UtilityAIBrain.RemoveAction](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIBrain.cs)。

### 7.5 非 MonoBehaviour 决策核心与运行时配置（0.2.0）

0.2.0 起，评估逻辑被抽到纯 C# 核心 [UtilityAIDecisionCore](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIDecisionCore.cs)，`UtilityAIBrain` 只是它的一层 MonoBehaviour 包装。因此：

- 无 GameObject 的数据层（例如大量未显示的 NPC）可以直接使用核心；
- 实体层与数据层共享同一套评分/防抖逻辑，调试数据可直接对比；
- `UtilityAIBrain` 的行为与 0.1.0 保持一致。

数据层用法：

```csharp
var core = new UtilityAIDecisionCore();
var blackboard = new UtilityAIBlackboard();
var model = UtilityAIRuntimeModel.CreateEmpty(UtilityAIAggregationMode.Product);
// ... 构建 Action / Consideration ...

var settings = new UtilityAIDecisionSettings(
    minTimeInStateSeconds: 1f,
    switchBias: 0.1f,
    switchScoreDelta: 0.02f,
    captureSnapshot: false,
    includeBreakdown: false);

UtilityAIDecisionResult result = core.Evaluate(model, blackboard, now, settings);
if (result.Changed) { /* 处理新行为 */ }
```

新增的 Brain 运行时 API：

- `SetProfile(profile, rebuildModel = true)` / `ClearProfile()`
- `SetEvaluationEnabled(bool)` / `SetEvaluationInterval(float)`
- `SetSwitchTuning(minTimeInState, bias, scoreDelta)`
- `SetDebugSnapshotEnabled` / `SetDecisionRecording` / `SetRecordBreakdown` / `SetLogOnActionSwitch`
- `SetCreateEmptyModelIfProfileMissing(bool)`
- `ForceReevaluate(reason)`：清空当前 Action 与驻留计时，下一次评估可立即重选
- `ReenterCurrentAction()`：对当前 Action 重新触发执行器 Enter 与 `ActionChanged`

`ForceReevaluate` / `ReenterCurrentAction` 用于解决"外部状态机已结束当前行为，但 Brain 仍停留在旧 Action"的场景。

### 7.6 Profile 资产与线程约束（0.3.0）

0.3.0 起支持把"代码构建的运行时模型"反序列化为 `UtilityAIProfile` 资产，之后在 Inspector 中调权重与曲线：

```csharp
UtilityAIRuntimeModel model = UtilityAIRuntimeModel.CreateEmpty(UtilityAIAggregationMode.Product);
// ... 用代码构建 Action / Consideration ...
UtilityAIProfile profile = UtilityAIProfile.CreateFromRuntimeModel(model);
// 编辑器内：AssetDatabase.CreateAsset(profile, "Assets/XXX.asset");
// 运行时：brain.SetProfile(profile); 或 profile.CreateRuntimeModel()
```

`CreateFromRuntimeModel` 只产生内存对象；保存为资产由调用方（编辑器工具）负责。

后台线程约束：

- `AnimationCurve.Evaluate` 属于 Unity API，不能在后台线程调用。
- 数据层（离线 NPC）应把 `UtilityAIDecisionSettings.UseResponseCurves` 设为 `false`，评分退化为线性（`normalized`），其余评分/防抖逻辑不变。
- 实体层（主线程）保持默认 `true`，曲线效果完整。

---

## 8. 调试工作流（建议按这个顺序）

### 8.1 先确保数据在变化

你要能在黑板里看到关键值在变化（Health/Mana/Danger/EnemyDistance 等）。否则所有分数变化都不可信。

### 8.2 开启快照与录制

建议：

- `enableDebugSnapshot = true`
- `enableDecisionRecording = true`
- `decisionRecordingCapacity = 300`（按需求）
- `recordBreakdown = true`（想看 Consideration 分解必须开）
- `logOnActionSwitch = true`（快速定位切换原因）

### 8.3 用 Inspector 回看历史

在 Brain Inspector 的 “Decision History”：

- `Use Latest`：回到最新一条
- `Index`：拖动查看某一帧的快照
- `Clear`：清空历史（Play 模式下可点）

实现见 [UtilityAIBrainEditor](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Editor/Inspectors/UtilityAIBrainEditor.cs)。

### 8.4 使用事件做业务侧记录

Brain 提供事件：

- `ActionChanged`

事件参数类型见 [UtilityAIActionChangedEventArgs](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Scripts/Runtime/Core/UtilityAIDebugSnapshot.cs)。

---

## 9. 修仙 Sample：如何用它做“复杂行为变化调试”

Sample 脚本在包内路径：

- `Samples~/XiuXianDemo/Scripts/...`

导入到项目后会出现在：

- `Assets/Samples/.../XiuXian Demo/Scripts/...`

推荐用“复杂版”组合（支持环境变化 + 需求闭环 + 运行时解锁/禁用）：

- [XiuXianComplexDemoBootstrap](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Samples~/XiuXianDemo/Scripts/XiuXianComplexDemoBootstrap.cs)：构建更多 Action/Consideration
- [XiuXianScenarioCurves](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Samples~/XiuXianDemo/Scripts/XiuXianScenarioCurves.cs)：外部事件输入（危险/敌距/天气/掉落）
- [XiuXianAgentNeedsAndEffects](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Samples~/XiuXianDemo/Scripts/XiuXianAgentNeedsAndEffects.cs)：行动反馈闭环（行动改变自身状态）
- [XiuXianRuntimeModelMutator](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Samples~/XiuXianDemo/Scripts/XiuXianRuntimeModelMutator.cs)：运行时启用/禁用动作（如炼丹/突破）
- [UtilityAIHUDOnGUI](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Samples~/XiuXianDemo/Scripts/UtilityAIHUDOnGUI.cs)：运行时 HUD（Top Actions/黑板值/原因）
- [XiuXianAgentSpawner](file:///e:/Projects/U3D/UGF/UGS.FlexUtilityAI/Samples~/XiuXianDemo/Scripts/XiuXianAgentSpawner.cs)：批量生成多 Agent（看群体抖动/稳定性）

### 9.1 建议的调参起点

为了更容易看出“策略突变”与“防抖效果”：

- `evaluationIntervalSeconds = 0.05 ~ 0.2`
- `minTimeInStateSeconds = 0.2 ~ 1.0`
- `switchBias = 0.05 ~ 0.2`
- `switchScoreDelta = 0.02 ~ 0.08`

---

## 10. 常见问题（FAQ）

### 10.1 为什么所有 Action 分数都很低/不切换？

- 黑板 key 不一致：Consideration 的 `blackboardFloatKey` 和你 `SetFloat` 的 key 必须完全相同（区分大小写）
- `inputMin/inputMax` 设置不匹配：例如 EnemyDistance 实际是 0..30，但 inputMax 还停留在 1
- FailThreshold 把行为否决了：检查 `useFailThreshold/failIfBelow`
- 冷却导致不可用：`cooldownSeconds > 0` 时，非当前 Action 在冷却内会被判定 `Cooldown`

### 10.2 为什么频繁抖动？

按优先级尝试：

- 增大 `minTimeInStateSeconds`
- 增大 `switchScoreDelta`
- 增大 `switchBias`
- 把某些“噪声输入”（例如距离）先做平滑再写入黑板

### 10.3 运行时改了 Model，但 Inspector 看起来没变化？

建议：

- 修改后调用 `brain.Model.Touch()`，提升 `ModelVersion`，便于你定位“哪个时刻发生了模型变更”
- 确保 `enableDebugSnapshot` 或 `enableDecisionRecording` 至少开一个，否则不产生快照数据

### 10.4 NullReferenceException 出现在 Sample 的写黑板里

如果 `OnEnable()` 早于 `Reset()`，`brain` 字段可能还未被赋值。Sample 中已通过 `EnsureBrain()` 规避该问题。

---

## 11. 下一步：你可能想做的扩展

- 扩展 Blackboard：支持 bool/int/Vector3/Object，或做分层命名空间（例如 `World/Danger`、`Agent/Health`）
- 扩展 Consideration：支持“动态取值器”（例如计算距离/可见性），而不仅是黑板读取
- 增强调试：把运行时模型变更（Action Enabled/Weight/增删）也写入快照或独立日志

