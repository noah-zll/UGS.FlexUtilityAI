# UGS FlexUtilityAI 方案设计文档

## 目标

以 Unity Package 形式提供一套通用的 Utility AI（效用型 AI）行为决策框架，满足：

- 可配置“有哪些行为”（Action）
- 可配置影响决策的“属性/因素”（Consideration）与权重
- 基于属性与权重计算各行为得分，选择当前要执行的行为
- 支持项目级复用与扩展（不绑定具体游戏业务）
- 提供可视化调试能力，便于调参

## 非目标

- 不直接提供完整的战斗/导航/动画系统集成
- 不强绑定任意第三方行为树或 FSM 框架（可通过适配层集成）
- 不在首版追求“所有常见 Consideration 都内置齐全”，以内置少量通用实现 + 可扩展为主

## 核心概念

### Action（行为）

代表一个可被选中的候选行为，例如：巡逻、追击、攻击、逃跑、寻找掩体、吃药等。

每个 Action 由以下要素构成：

- 行为标识（ID/Name）
- 行为权重（ActionWeight）
- 冷却/最小切换间隔（Cooldown / MinSwitchInterval，可选）
- 是否可用（Enabled，可由运行时条件控制）
- 影响该行为得分的一组 Consideration（每个带权重与曲线）
- 行为执行体（Executor/Handler）：当该 Action 被选中时如何启动/维持/结束

### Consideration（因素/属性）

对某个 Action 的“适合程度”进行打分，输出范围建议为 0~1 的浮点数。

Consideration 由两部分组成：

- 取值：从上下文读取属性（例如血量百分比、距离、弹药量、目标是否可见）
- 映射：把原始值映射到 0~1 分数（例如使用曲线、阈值、线性归一化、反向等）

每个 Consideration 还可配置：

- 权重（ConsiderationWeight）
- 最小/最大夹取（Clamp，可选）
- 是否反向（Invert，可选）

### Context / Blackboard（上下文 / 黑板）

决策时读取数据的来源。为了通用与可扩展，建议采用“组合式上下文”：

- 通用黑板（Blackboard）：存放可动态变化的数值/引用（float/int/bool/Vector3/Object 等）
- 运行时提供器（Provider）：由代码计算/查询（例如距离、视线检测、导航状态）

框架提供统一访问接口，使 Consideration 不依赖具体业务类型。

## 资源与数据驱动设计

### 配置方式（建议）

以 ScriptableObject 资产作为主要配置载体，便于复用与调参：

- UtilityAIProfile：一套“决策配置”，包含若干 Action 定义
- ActionDefinition：行为定义（权重、冷却、Consideration 列表、执行器引用/类型信息等）
- ConsiderationDefinition：因素定义（取值方式 + 映射方式 + 权重）

运行时由一个“决策器/大脑”组件引用 Profile，并绑定具体 Agent 的 Context。

### 关键点

- 行为和因素尽量“数据化 + 可扩展”
- 运行时执行逻辑与编辑器配置解耦
- 同一个 Profile 可被多个 Agent 复用；差异通过 Context/Blackboard 注入

## 运行时动态更改设计

本插件需要支持“行为、属性等在运行时动态更改”。为同时兼顾可复用的资产配置与运行时灵活性，建议采用“静态模板 + 运行时可变实例”的分层设计：

- Profile/Definition 作为静态模板：适合复用、版本管理、编辑器调参
- Runtime Model 作为可变实例：每个 Agent 在运行时持有自己的 Action/Consideration 集合与参数，可随时增删改

### 动态更改范围

建议支持以下动态变更（均作用于运行时实例，不直接修改原始资产）：

- Action 的增删改：新增临时行为、禁用某行为、调整 ActionWeight、调整冷却/最小切换间隔
- Consideration 的增删改：为某行为动态追加/移除因素、调整 ConsiderationWeight、调整映射曲线/阈值
- 属性来源的动态变更：Blackboard 中的数值随帧更新；Provider 的计算结果随环境变化

### 运行时模型（建议）

为避免 ScriptableObject 被误改动、以及多个 Agent 共享同一份配置导致互相影响：

- 决策器启动时，将 Profile 编译/拷贝为 RuntimeModel
- RuntimeModel 内部持有可变列表：RuntimeAction、RuntimeConsideration
- 对外暴露“修改接口”，修改后标记 Dirty，并在下一次评估前完成必要的重新编译/缓存更新

### 修改接口形态（建议）

对外提供两层 API：

- 面向配置层（高层）：按 ID 操作（AddAction/RemoveAction/SetActionEnabled/SetWeight 等）
- 面向性能层（低层）：直接拿到 RuntimeAction/RuntimeConsideration 引用进行批量修改

并提供变更通知：

- OnModelChanged：用于调试面板刷新、缓存更新
- OnActionSetChanged：当增删行为时触发（影响候选集合）

### 一致性与安全策略

为保证动态更改不引发运行时异常，建议约束：

- 动态增删改只允许在主线程进行
- 评估时对 Action 列表使用快照或版本号校验，避免迭代过程中集合被修改
- 当当前行为被移除/禁用时，按切换策略立即或在下次 Tick 自动选择替代行为

## 评分模型

### 基本流程

1. 收集所有候选 Action
2. 对每个 Action：
   1) 检查是否 Enabled、是否在冷却、是否满足最低切换间隔等
   2) 计算所有 Consideration 的分数（0~1）
   3) 依据聚合方式合成 ActionScore
3. 在所有 ActionScore 中选择最佳行为
4. 触发行为切换（如果与当前行为不同，且切换策略允许）

### 聚合方式（建议提供可选项）

Utility AI 常见聚合方式：

- 乘积（Product）：倾向于“短板效应”，任何一个因素很低都会拉低总分
- 加权平均（Weighted Average）：更平滑，但可能掩盖关键否决条件
- 最小值（Min）：当你希望任何关键因素不足就直接否决时可用

建议首版实现至少两种：Product 与 Weighted Average，并允许在 Profile 或 Action 级别选择。

### 建议公式（首版默认）

以 Product 为默认，并提供“权重影响强度”的简单实现：

- 记第 i 个 Consideration 分数为 s_i（0~1），权重为 w_i（0~1 或任意正数）
- 先做权重映射：s'_i = Lerp(1, s_i, NormalizeWeight(w_i))
- 乘积聚合：score = ActionWeight * Π(s'_i)

这样做的好处：

- 权重越小，该因素越接近“无影响”（趋向 1）
- 不需要复杂的幂运算与归一化也能工作

另外建议提供一个“否决阈值”机制（可选）：

- 当某个 Consideration 分数低于阈值时，直接认为该 Action 不可用（score = 0）

## 行为执行模型

### 运行时接口（建议）

为保持通用性，建议将“决策”与“执行”分离：

- 决策器负责：算分、选择、切换策略、输出当前选择
- 执行器负责：收到选中 Action 后，调用具体游戏逻辑（动画/技能/导航等）

可采用以下抽象：

- IActionExecutor：执行某个 Action 的生命周期方法（Enter / Tick / Exit）
- IActionInstance（可选）：当行为需要持有运行时状态时使用实例对象

### 行为切换策略（建议）

常见需求：

- 防抖：避免两种行为分数相近导致频繁抖动
- 最小驻留时间：进入行为后至少维持 N 秒
- 迟滞（Hysteresis）：新行为分数必须比当前行为高出一定比例/差值才能切换

建议提供一个基础策略：

- 当前行为仍可用时，只有当 bestScore >= currentScore * (1 + SwitchBias) 才切换

## 扩展点设计

### 自定义行为

支持两种扩展方式（可并存）：

- 数据化：通过创建 ActionDefinition 资产并绑定执行器引用/类型
- 代码化：实现 IActionExecutor（或派生基类），再在编辑器中选择/绑定

### 自定义属性/因素（Consideration）

建议将 Consideration 抽象为：

- 取值器（ValueSource）：从 Context/Blackboard 读取原始值
- 响应曲线（Response）：把原始值映射到 0~1

扩展方式：

- 内置一批通用 ValueSource（黑板取 float、距离、角度、计时器、布尔条件等）
- 允许用户实现 IValueSource / IConsideration 自定义取值与评分

### 调试与可视化

建议提供：

- Inspector 调试：显示每帧各 ActionScore、每个 Consideration 的 s_i、当前选中行为
- 可选：EditorWindow 汇总显示多个 Agent 的决策信息
- 可选：Gizmos 绘制与决策相关的可视元素（目标方向、距离圈等）

## 包结构建议（Unity Package）

参考现有 UGS 包结构（Scripts/Runtime、Scripts/Editor、Documentation 等），建议：

- UGS.FlexUtilityAI/
  - Documentation/
    - DesignDocument.md
  - Scripts/
    - Runtime/
      - Core/
      - Providers/
      - Executors/
    - Editor/
      - Inspectors/
      - Windows/
  - Samples~/
    - ExampleAgent/
  - package.json
  - README.md
  - CHANGELOG.md

首阶段仅需 Runtime/Core + 基础 Editor Inspector 即可跑通。

## 首版范围（MVP）

### 必备能力

- Profile 中可配置多 Action 与其 Consideration 列表
- 至少一种聚合方式（默认 Product）
- 至少一种黑板取值 Consideration（例如：读取 float 并用 AnimationCurve 映射）
- 冷却/最小切换间隔（基础行为防抖）
- 运行时输出当前选中 Action（事件回调或公开属性）
- Inspector 显示当前帧得分明细（用于调参）
- 支持运行时对 Action/Consideration 与权重进行增删改

### 可选增强（后续迭代）

- 迟滞切换策略、最小驻留时间
- 多层级决策：先选“模式”（战斗/逃跑/探索），再选子行为
- 行为并行（例如“移动 + 射击”）的通道化设计
- Batch 评估/Job 化（针对大量 NPC 优化）
- 录制与回放：保存一段时间内的决策结果用于分析

## 示例配置思路（文字示例）

假设一个敌人有三种行为：Attack、Chase、Flee。

- Attack：
  - 距离越近分越高（Distance -> Curve）
  - 弹药越多分越高（AmmoPercent -> Curve）
  - 目标可见才有分（HasLOS -> 0/1）
- Chase：
  - 距离中等分最高（Distance -> Bell Curve）
  - 目标最近一次出现时间越短分越高（LastSeenTime -> Curve）
- Flee：
  - 血量越低分越高（HPPercent -> Inverted Curve）
  - 敌人数越多分越高（NearbyEnemyCount -> Curve）

每个 Consideration 配置权重来表达“该因素对行为的重要程度”，通过聚合得到最终得分，选最高者执行。

## 风险与约束

- 过度数据化可能导致资产数量膨胀：需要合理的复用与模板机制（后续考虑）
- 评分曲线配置不当会产生“极端行为”：必须提供调试面板与分数明细
- 黑板键使用 string 易出错：建议提供 Key 资产化（后续考虑）或集中定义

