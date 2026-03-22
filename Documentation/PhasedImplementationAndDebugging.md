# UGS FlexUtilityAI 分阶段实现与调试文档

## 文档目的

本文件用于指导插件从 0 到可用的分阶段实现，并提供调试与排错手册，确保：

- 每阶段都有可运行、可验证的产出
- 调参过程可观测（分数明细、切换原因、时序）
- 运行时支持动态更改行为与属性，不破坏稳定性

## 分阶段实现

### Phase 0：包骨架与最小运行验证

**目标**

- 形成可被 Unity Package Manager 引入的包结构
- Runtime asmdef 可编译，基础脚本能在场景里跑起来

**产出**

- package.json（包名、版本、Unity 版本约束）
- Runtime/Editor asmdef（如需 Editor）
- 一个最小示例组件：挂到 GameObject 后能每帧 Tick（哪怕只打印状态）

**验证**

- Unity 控制台无编译错误
- 场景运行后 Tick 正常执行

### Phase 1：决策核心（静态配置 + 评分 + 选择）

**目标**

- 实现 Utility AI 核心：候选行为评分、聚合、选择 best action
- 具备最小可用的 Consideration：从黑板读取 float，并经曲线映射到 0~1

**建议范围**

- Profile（ScriptableObject）：包含若干 ActionDefinition
- ActionDefinition：ActionWeight、Consideration 列表
- ConsiderationDefinition：ValueSource（黑板键）、Response（AnimationCurve）、权重
- 聚合方式：默认 Product（先实现一种）

**验证（建议场景）**

- 3 个 Action（Attack/Chase/Flee），3~6 个 Consideration
- 通过 Inspector 修改黑板数值（血量、距离、可见性），观察输出选择是否符合预期

**完成标准**

- 每帧可输出：当前 best action、best score
- Action 分数随输入变化稳定，无 NaN/无异常

### Phase 2：运行时模型（动态增删改 Action/Consideration/权重）

**目标**

- 引入“静态模板 + 运行时实例”分层
- 支持运行时动态变更：增删 Action、启用/禁用、改权重、改曲线/阈值

**建议范围**

- RuntimeModel：从 Profile 编译/拷贝得到
- RuntimeAction / RuntimeConsideration：可变集合与参数
- Dirty/Version：模型变更后能安全刷新缓存

**关键策略**

- 修改只影响当前 Agent 的 RuntimeModel，不直接改动 ScriptableObject
- 评估循环对候选集合使用快照或版本号校验，避免迭代中被修改

**验证**

- 运行时按键：临时添加一个“UseItem”行为并赋予高分因素，能即时成为 best
- 运行时禁用当前行为：能自动切换到次优行为且无异常

### Phase 3：切换策略与稳定性（防抖/迟滞/驻留）

**目标**

- 降低抖动：分数接近时避免频繁切换
- 行为生命周期明确：Enter/Tick/Exit 触发时机稳定

**建议范围**

- 最小驻留时间（MinTimeInState）
- 迟滞（Hysteresis / SwitchBias）：新行为需明显更优才切换
- 冷却（Cooldown）：行为结束/触发后进入不可选期

**验证**

- 构造两行为分数非常接近的情况，确认切换不会每帧反复
- 在当前行为禁用/不可用时，仍能及时切换

### Phase 4：执行适配层（与业务逻辑解耦）

**目标**

- 决策输出与游戏逻辑执行解耦
- 用户可按项目风格接入：Animator、NavMesh、技能系统等

**建议范围**

- IActionExecutor：Enter/Tick/Exit
- Executor 绑定方式：
  - 通过组件引用绑定到 Agent
  - 或通过类型/工厂创建（需要管理生命周期）

**验证**

- 示例：Chase 行为控制 NavMeshAgent 追目标；Flee 行为朝远离方向移动

### Phase 5：编辑器调试（分数可视化与调参效率）

**目标**

- 让调参有“可解释性”：每个 Action 的得分构成、每个 Consideration 分数
- 支持“观察一段时间”而不是只看当前帧

**建议范围**

- Inspector 调试面板：
  - 当前选中行为、候选列表、排序后的分数
  - 选中某 Action 时显示 Consideration 明细（原始值、映射值、权重后值）
  - 切换原因（超过迟滞阈值、当前行为失效、冷却到期等）
- 可选：录制最近 N 帧快照（环形缓冲）

**验证**

- 调整曲线/权重后，分数变化能立即反映
- 切换发生时能看到清晰原因与关键输入

### Phase 6：Samples 与回归测试

**目标**

- 提供可运行样例帮助快速上手
- 提供基础测试保障核心逻辑不会被后续改动破坏

**建议范围**

- Samples~：一个简单 Agent（目标点、敌人对象、血量/距离输入）
- Tests（如项目已有测试框架则接入）：验证聚合、权重、切换策略、动态增删改安全性

**验证**

- 新项目导入包后，打开样例场景即可运行并观察调试信息

## 调试与排错指南

### 调试信号（建议统一口径）

建议决策器对外暴露如下调试数据（至少在 Editor 下可用）：

- CurrentActionId / CurrentScore
- BestActionId / BestScore（若与当前不同）
- ActionScores：每个候选 Action 的最终分数
- ConsiderationBreakdown：每个 Action 下各 Consideration 的原始值、映射分、权重后分
- SwitchReason：切换/不切换原因
- ModelVersion：运行时模型版本号（发生动态更改时递增）

### 常见问题与定位

#### 1）所有行为分数都是 0

排查顺序：

- 是否所有 Action 被禁用（Enabled=false）或在冷却中
- 是否启用了否决阈值，且所有 Consideration 都低于阈值
- Consideration 映射曲线是否输出恒 0（曲线关键帧配置问题）
- 原始值是否一直为默认值（黑板键写错/未设置）

#### 2）行为频繁抖动（每帧切换）

排查顺序：

- 是否未启用迟滞/最小驻留时间
- 两个行为分数是否接近（检查 ActionScores）
- Consideration 输入是否高频噪声（距离/可见性边缘跳变）

解决建议：

- 增加 SwitchBias 或 MinTimeInState
- 对输入做平滑（例如距离用插值、可见性加“最近一次可见时间”因素）
- 对关键否决条件用显式 Gate（例如不可见直接 0）

#### 3）分数出现 NaN 或异常大

排查顺序：

- 归一化/映射是否除以 0（最小最大相同）
- 曲线是否在某些输入范围外返回异常（建议对输入 clamp）
- 权重是否负数或极端值（建议对权重做约束与校验）

#### 4）运行时动态更改后崩溃或行为不更新

排查顺序：

- 是否在评估过程中修改了候选集合（需要版本号/快照机制）
- 修改后是否标记 Dirty 并触发缓存刷新
- 是否误修改了 ScriptableObject 资产导致多个 Agent 互相影响

解决建议：

- 强制动态更改走统一 API，并在内部做版本递增
- 评估循环只读快照，或在版本变化时重启评估/推迟到下一帧

### 调参流程建议

1. 先用最少 Consideration 做出可控决策（每个 Action 1~2 个因素）
2. 再逐步增加因素，保持每新增一个因素都能解释其贡献
3. 优先把“硬条件”做成 Gate（0/1 或阈值），把“偏好程度”做成曲线
4. 每次调参都观察一段时间的快照，避免只看单帧误判

### 建议的调试开关

建议提供统一开关（至少 Editor 下）：

- EnableDebugView：显示分数面板
- EnableScoreRecording：记录最近 N 帧快照
- EnableSwitchLog：仅在切换发生时输出一条日志（避免刷屏）
- ScoreUpdateRate：调试采样率（例如每 0.2s 更新一次 UI，降低 Editor 开销）

