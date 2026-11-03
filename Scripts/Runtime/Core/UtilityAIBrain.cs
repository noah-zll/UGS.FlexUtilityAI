using System;
using System.Collections.Generic;
using UnityEngine;

namespace UGS.FlexUtilityAI
{
    public sealed class UtilityAIBrain : MonoBehaviour
    {
        [SerializeField] private UtilityAIProfile profile;
        [SerializeField] private bool evaluateInUpdate = true;
        [SerializeField] private float evaluationIntervalSeconds;
        [SerializeField] private float switchBias;
        [SerializeField] private float switchScoreDelta;
        [SerializeField] private float minTimeInStateSeconds;
        [SerializeField] private bool createEmptyModelIfProfileMissing;
        [SerializeField] private UtilityAIAggregationMode emptyModelAggregationMode = UtilityAIAggregationMode.Product;
        [SerializeField] private bool enableDebugSnapshot = true;
        [SerializeField] private bool enableDecisionRecording;
        [SerializeField] private int decisionRecordingCapacity = 120;
        [SerializeField] private bool recordBreakdown;
        [SerializeField] private bool logOnActionSwitch;

        private readonly UtilityAIBlackboard _blackboard = new UtilityAIBlackboard();
        private readonly Dictionary<string, IUtilityAIActionExecutor> _executorsByActionId = new Dictionary<string, IUtilityAIActionExecutor>();
        private readonly List<UtilityAIDecisionSnapshot> _decisionHistory = new List<UtilityAIDecisionSnapshot>();
        private readonly UtilityAIDecisionCore _core = new UtilityAIDecisionCore();

        private UtilityAIRuntimeModel _model;
        private IUtilityAIActionExecutor _currentExecutor;

        public event EventHandler<UtilityAIActionChangedEventArgs> ActionChanged;

        public UtilityAIProfile Profile => profile;
        public UtilityAIRuntimeModel Model => _model;
        public UtilityAIBlackboard Blackboard => _blackboard;

        public string CurrentActionId => _core.CurrentActionId;
        public float CurrentScore => _core.CurrentScore;

        public bool EvaluateInUpdate => evaluateInUpdate;
        public float EvaluationIntervalSeconds => evaluationIntervalSeconds;

        /// <summary>最近一次 <see cref="ForceReevaluate"/> 传入的原因，便于调试。</summary>
        public string LastReevaluateReason { get; private set; }

        public UtilityAIDecisionSnapshot LastDecisionSnapshot { get; private set; }
        public IReadOnlyList<UtilityAIDecisionSnapshot> DecisionHistory => _decisionHistory;

        private void Awake()
        {
            EnsureModel();
            RefreshExecutors();
        }

        private void OnEnable()
        {
            EnsureModel();
            RefreshExecutors();
        }

        private void Update()
        {
            double now = Time.timeAsDouble;

            if (evaluateInUpdate)
            {
                if (evaluationIntervalSeconds <= 0f || now - _core.LastEvaluationTime >= evaluationIntervalSeconds)
                {
                    EvaluateOnce(now);
                }
            }

            TickCurrentAction(Time.deltaTime);
        }

        public void EnsureModel()
        {
            if (_model != null) return;
            if (profile != null)
            {
                _model = profile.CreateRuntimeModel();
                return;
            }

            if (!createEmptyModelIfProfileMissing) return;
            _model = UtilityAIRuntimeModel.CreateEmpty(emptyModelAggregationMode);
        }

        public void EnsureModelOrCreateEmpty()
        {
            if (_model != null) return;
            if (profile != null)
            {
                _model = profile.CreateRuntimeModel();
                return;
            }

            _model = UtilityAIRuntimeModel.CreateEmpty(emptyModelAggregationMode);
        }

        public void RebuildModelFromProfile()
        {
            if (profile == null)
            {
                _model = createEmptyModelIfProfileMissing ? UtilityAIRuntimeModel.CreateEmpty(emptyModelAggregationMode) : null;
                return;
            }

            _model = profile.CreateRuntimeModel();
        }

        public void ClearDecisionHistory()
        {
            _decisionHistory.Clear();
        }

        public void SetFloat(string key, float value)
        {
            _blackboard.SetFloat(key, value);
        }

        public UtilityAIRuntimeAction AddAction(string id)
        {
            EnsureModel();
            if (_model == null) return null;
            return _model.AddAction(id);
        }

        public bool RemoveAction(string id)
        {
            EnsureModel();
            if (_model == null) return false;
            bool removed = _model.RemoveAction(id);
            if (removed && string.Equals(CurrentActionId, id, StringComparison.Ordinal))
            {
                TryExitCurrentExecutor();
                _core.ResetCurrentAction();
            }

            return removed;
        }

        public void RefreshExecutors()
        {
            _executorsByActionId.Clear();

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour b = behaviours[i];
                if (b == null) continue;
                var executor = b as IUtilityAIActionExecutor;
                if (executor == null) continue;

                string id = executor.ActionId;
                if (string.IsNullOrEmpty(id)) continue;
                _executorsByActionId[id] = executor;
            }

            if (!string.IsNullOrEmpty(CurrentActionId) &&
                _executorsByActionId.TryGetValue(CurrentActionId, out IUtilityAIActionExecutor current))
            {
                _currentExecutor = current;
            }
            else
            {
                _currentExecutor = null;
            }
        }

        public void EvaluateOnce()
        {
            EvaluateOnce(Time.timeAsDouble);
        }

        public void EvaluateOnce(double now)
        {
            EnsureModel();

            UtilityAIDecisionResult result = _core.Evaluate(_model, _blackboard, now, BuildSettings());
            ApplyResult(result);
        }

        /// <summary>
        /// 运行时替换 Profile。新增 API，用于按兵种/角色动态装配决策配置。
        /// </summary>
        public void SetProfile(UtilityAIProfile newProfile, bool rebuildModel = true)
        {
            profile = newProfile;

            if (rebuildModel)
            {
                RebuildModelFromProfile();
                ResetDecisionState();
            }
            else
            {
                EnsureModel();
            }
        }

        /// <summary>清空 Profile 与运行时模型。</summary>
        public void ClearProfile()
        {
            profile = null;
            _model = null;
            ResetDecisionState();
            LastDecisionSnapshot = null;
        }

        /// <summary>是否允许 Brain 自带 Update 自动评估。关闭后需外部调用 <see cref="EvaluateOnce()"/>。</summary>
        public void SetEvaluationEnabled(bool enabled)
        {
            evaluateInUpdate = enabled;
        }

        /// <summary>设置自动评估间隔（秒）。&lt;=0 表示每帧。</summary>
        public void SetEvaluationInterval(float seconds)
        {
            evaluationIntervalSeconds = Mathf.Max(0f, seconds);
        }

        /// <summary>设置行为切换稳定性参数。</summary>
        public void SetSwitchTuning(float minTimeInState, float bias, float scoreDelta)
        {
            minTimeInStateSeconds = Mathf.Max(0f, minTimeInState);
            switchBias = Mathf.Max(0f, bias);
            switchScoreDelta = Mathf.Max(0f, scoreDelta);
        }

        public void SetCreateEmptyModelIfProfileMissing(bool enabled)
        {
            createEmptyModelIfProfileMissing = enabled;
        }

        public void SetDebugSnapshotEnabled(bool enabled)
        {
            enableDebugSnapshot = enabled;
        }

        public void SetDecisionRecording(bool enabled, int capacity = -1)
        {
            enableDecisionRecording = enabled;
            if (capacity >= 0) decisionRecordingCapacity = capacity;
        }

        public void SetRecordBreakdown(bool enabled)
        {
            recordBreakdown = enabled;
        }

        public void SetLogOnActionSwitch(bool enabled)
        {
            logOnActionSwitch = enabled;
        }

        /// <summary>
        /// 强制下一次评估重新决策：清空当前 Action 与驻留计时，并退出当前执行器。
        /// <para>
        /// 适用于 FSM 已自行结束当前行为（例如返回 Idle）但 Brain 仍停留在旧 Action 的场景。
        /// </para>
        /// </summary>
        public void ForceReevaluate(string reason = null)
        {
            LastReevaluateReason = reason;
            TryExitCurrentExecutor();
            _core.ResetCurrentAction();

            if (logOnActionSwitch)
            {
                Debug.Log($"{name} UtilityAI force reevaluate reason={reason ?? "(none)"}", this);
            }
        }

        /// <summary>
        /// 重新进入当前 Action：对执行器重新触发 OnExit/OnEnter，并再次抛出 ActionChanged。
        /// <para>不会重新评估分数，适用于"行为已被外部状态打断，需要重新启动"的场景。</para>
        /// </summary>
        public void ReenterCurrentAction()
        {
            string currentId = CurrentActionId;
            if (string.IsNullOrEmpty(currentId)) return;

            TryExitCurrentExecutor();

            if (_executorsByActionId.TryGetValue(currentId, out IUtilityAIActionExecutor executor))
            {
                _currentExecutor = executor;
                _currentExecutor.OnEnter(this);
            }

            ActionChanged?.Invoke(this, new UtilityAIActionChangedEventArgs(
                currentId, currentId, CurrentScore, Time.timeAsDouble));
        }

        private UtilityAIDecisionSettings BuildSettings()
        {
            bool captureSnapshot = enableDebugSnapshot || enableDecisionRecording;
            bool includeBreakdown = enableDebugSnapshot || recordBreakdown;

            return new UtilityAIDecisionSettings(
                minTimeInStateSeconds,
                switchBias,
                switchScoreDelta,
                captureSnapshot,
                includeBreakdown);
        }

        private void ApplyResult(UtilityAIDecisionResult result)
        {
            if (result == null) return;

            LastDecisionSnapshot = result.Snapshot;

            if (enableDecisionRecording && result.Snapshot != null)
            {
                AddDecisionHistory(result.Snapshot);
            }

            if (!result.Changed) return;

            TrySwitchExecutor(result.PreviousActionId, result.CurrentActionId);

            if (logOnActionSwitch)
            {
                Debug.Log($"{name} UtilityAI switch {result.PreviousActionId} -> {result.CurrentActionId} score={result.CurrentScore:F3} reason={result.SwitchReason}", this);
            }

            ActionChanged?.Invoke(this, new UtilityAIActionChangedEventArgs(
                result.PreviousActionId, result.CurrentActionId, result.CurrentScore, result.Time));
        }

        private void ResetDecisionState()
        {
            TryExitCurrentExecutor();
            _core.ResetCurrentAction();
        }

        private void TickCurrentAction(float deltaTime)
        {
            if (_currentExecutor == null) return;
            _currentExecutor.OnTick(this, deltaTime);
        }

        private void TrySwitchExecutor(string previousActionId, string currentActionId)
        {
            if (!string.IsNullOrEmpty(previousActionId)
                && _currentExecutor != null
                && string.Equals(_currentExecutor.ActionId, previousActionId, StringComparison.Ordinal))
            {
                _currentExecutor.OnExit(this);
            }

            if (!string.IsNullOrEmpty(currentActionId) &&
                _executorsByActionId.TryGetValue(currentActionId, out IUtilityAIActionExecutor next))
            {
                _currentExecutor = next;
                _currentExecutor.OnEnter(this);
            }
            else
            {
                _currentExecutor = null;
            }
        }

        private void TryExitCurrentExecutor()
        {
            if (_currentExecutor == null) return;
            _currentExecutor.OnExit(this);
            _currentExecutor = null;
        }

        private void AddDecisionHistory(UtilityAIDecisionSnapshot snapshot)
        {
            if (snapshot == null) return;

            int capacity = decisionRecordingCapacity;
            if (capacity <= 0) return;

            if (_decisionHistory.Count >= capacity)
            {
                int removeCount = _decisionHistory.Count - capacity + 1;
                _decisionHistory.RemoveRange(0, removeCount);
            }

            _decisionHistory.Add(snapshot);
        }
    }
}
