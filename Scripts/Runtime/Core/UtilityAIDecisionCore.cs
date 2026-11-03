using System;
using UnityEngine;

namespace UGS.FlexUtilityAI
{
    /// <summary>
    /// 一次决策评估所使用的参数快照。
    /// <para>与 <see cref="UtilityAIBrain"/> 的 Inspector 字段解耦，便于非 MonoBehaviour 的数据层复用。</para>
    /// </summary>
    public struct UtilityAIDecisionSettings
    {
        /// <summary>进入某个 Action 后至少驻留的秒数。</summary>
        public float MinTimeInStateSeconds;

        /// <summary>迟滞比例：新行为分数需高于当前分数 * (1 + SwitchBias)。</summary>
        public float SwitchBias;

        /// <summary>迟滞差值：在比例门槛之上再加的绝对差值。</summary>
        public float SwitchScoreDelta;

        /// <summary>是否生成决策快照（含逐 Action / Consideration 分解）。</summary>
        public bool CaptureSnapshot;

        /// <summary>是否记录 Consideration 明细。关闭时只计算总分。</summary>
        public bool IncludeBreakdown;

        /// <summary>
        /// 是否使用 Consideration 的响应曲线。
        /// <para>数据侧可能运行在后台线程，此时应关闭：<see cref="AnimationCurve.Evaluate"/> 属于 Unity API，不能在后台线程调用。</para>
        /// </summary>
        public bool UseResponseCurves;

        public UtilityAIDecisionSettings(
            float minTimeInStateSeconds,
            float switchBias,
            float switchScoreDelta,
            bool captureSnapshot,
            bool includeBreakdown,
            bool useResponseCurves = true)
        {
            MinTimeInStateSeconds = minTimeInStateSeconds;
            SwitchBias = switchBias;
            SwitchScoreDelta = switchScoreDelta;
            CaptureSnapshot = captureSnapshot;
            IncludeBreakdown = includeBreakdown;
            UseResponseCurves = useResponseCurves;
        }

        public static UtilityAIDecisionSettings Default =>
            new UtilityAIDecisionSettings(0f, 0f, 0f, false, false);
    }

    /// <summary>
    /// 一次评估的结果。字段语义与 <see cref="UtilityAIBrain"/> 原实现保持一致。
    /// </summary>
    public sealed class UtilityAIDecisionResult
    {
        public double Time;
        public string PreviousActionId;
        public string CurrentActionId;
        public float CurrentScore;
        public string BestActionId;
        public float BestScore;
        public string SwitchReason;

        /// <summary>本次评估是否发生了 Action 切换。</summary>
        public bool Changed;

        /// <summary>仅在 <see cref="UtilityAIDecisionSettings.CaptureSnapshot"/> 为 true 时非空。</summary>
        public UtilityAIDecisionSnapshot Snapshot;
    }

    /// <summary>
    /// 纯 C# 决策核心：负责评估、选择、防抖与当前状态维护。
    /// <para>
    /// 不依赖 MonoBehaviour / GameObject，可供实体层（<see cref="UtilityAIBrain"/>）与
    /// 无实体的数据层（大量 NPC）共用，保证两边评分与切换行为完全一致。
    /// </para>
    /// </summary>
    public sealed class UtilityAIDecisionCore
    {
        private double _lastEvaluationTime = double.NegativeInfinity;
        private string _currentActionId;
        private float _currentScore;
        private double _currentActionStartTime = double.NegativeInfinity;

        public string CurrentActionId => _currentActionId;
        public float CurrentScore => _currentScore;
        public double CurrentActionStartTime => _currentActionStartTime;
        public double LastEvaluationTime => _lastEvaluationTime;

        /// <summary>
        /// 执行一次评估。
        /// </summary>
        /// <param name="model">运行时模型；为 null 或没有 Action 时返回 NoModelOrNoActions。</param>
        /// <param name="blackboard">黑板。</param>
        /// <param name="now">当前时间（秒）。</param>
        /// <param name="settings">本次评估参数。</param>
        public UtilityAIDecisionResult Evaluate(
            UtilityAIRuntimeModel model,
            UtilityAIBlackboard blackboard,
            double now,
            in UtilityAIDecisionSettings settings)
        {
            if (blackboard == null) throw new ArgumentNullException(nameof(blackboard));

            _lastEvaluationTime = now;

            if (model == null || model.Actions.Count == 0)
            {
                UtilityAIDecisionSnapshot emptySnapshot = settings.CaptureSnapshot
                    ? new UtilityAIDecisionSnapshot { Time = now, ModelVersion = 0 }
                    : null;

                return Complete(now, _currentActionId, null, _currentScore,
                    "NoModelOrNoActions", emptySnapshot);
            }

            string previousActionId = _currentActionId;

            UtilityAIRuntimeAction currentAction = string.IsNullOrEmpty(_currentActionId)
                ? null
                : model.FindAction(_currentActionId);

            float pinnedSeconds = 0f;
            if (currentAction != null)
            {
                pinnedSeconds = Mathf.Max(0f, settings.MinTimeInStateSeconds);
                pinnedSeconds = Mathf.Max(pinnedSeconds, currentAction.MinSwitchIntervalSeconds);
            }

            bool currentPinned = currentAction != null
                                 && currentAction.Enabled
                                 && pinnedSeconds > 0f
                                 && now - _currentActionStartTime < pinnedSeconds;

            float bestScore = -1f;
            string bestActionId = null;
            float currentScore = -1f;

            UtilityAIDecisionSnapshot snapshot = settings.CaptureSnapshot
                ? new UtilityAIDecisionSnapshot
                {
                    Time = now,
                    ModelVersion = model.Version,
                    CurrentActionId = _currentActionId,
                }
                : null;

            for (int i = 0; i < model.Actions.Count; i++)
            {
                UtilityAIRuntimeAction action = model.Actions[i];
                bool eligible = IsEligible(action, currentAction, now, out string ineligibleReason);

                UtilityAIActionScoreSnapshot actionSnapshot = null;
                float score = 0f;
                if (eligible)
                {
                    actionSnapshot = UtilityAIScorer.EvaluateAction(
                        action,
                        blackboard,
                        model.AggregationMode,
                        includeBreakdown: settings.IncludeBreakdown,
                        useResponseCurves: settings.UseResponseCurves);
                    score = actionSnapshot.Score;
                }

                if (snapshot != null)
                {
                    if (actionSnapshot == null)
                    {
                        actionSnapshot = new UtilityAIActionScoreSnapshot
                        {
                            ActionId = action.Id,
                            Score = score,
                            Weight = action.Weight,
                        };
                    }

                    actionSnapshot.Eligible = eligible;
                    actionSnapshot.IneligibleReason = eligible ? null : ineligibleReason;
                    snapshot.Actions.Add(actionSnapshot);
                }

                if (string.Equals(action.Id, _currentActionId, StringComparison.Ordinal))
                {
                    currentScore = score;
                }

                if (!eligible) continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestActionId = action.Id;
                }
            }

            if (currentPinned && currentAction != null)
            {
                _currentScore = currentScore < 0f ? 0f : currentScore;
                return Complete(now, previousActionId, bestActionId, bestScore,
                    "PinnedByMinSwitchInterval", snapshot);
            }

            bool canSwitch;
            string switchReason;
            if (currentAction == null || string.IsNullOrEmpty(_currentActionId))
            {
                canSwitch = true;
                switchReason = "NoCurrentAction";
            }
            else if (!currentAction.Enabled)
            {
                canSwitch = true;
                switchReason = "CurrentDisabled";
            }
            else
            {
                float baseCurrentScore = currentScore >= 0f ? currentScore : _currentScore;
                float threshold = baseCurrentScore <= 0f
                    ? 0f
                    : baseCurrentScore * (1f + Mathf.Max(0f, settings.SwitchBias));
                threshold += Mathf.Max(0f, settings.SwitchScoreDelta);
                canSwitch = bestActionId != null
                            && bestScore >= threshold
                            && !string.Equals(bestActionId, _currentActionId, StringComparison.Ordinal);
                switchReason = canSwitch ? "BestBeatsCurrentWithBias" : "KeepCurrent";
            }

            if (bestActionId == null)
            {
                bestActionId = _currentActionId;
                bestScore = _currentScore;
                switchReason = "NoEligibleAction";
                canSwitch = false;
            }

            if (canSwitch)
            {
                _currentActionId = bestActionId;
                _currentScore = bestScore;
                _currentActionStartTime = now;

                UtilityAIRuntimeAction selected = model.FindAction(bestActionId);
                if (selected != null) selected.LastSelectedTime = (float)now;
            }
            else
            {
                if (currentScore >= 0f) _currentScore = currentScore;
                if (string.IsNullOrEmpty(_currentActionId))
                {
                    _currentActionId = bestActionId;
                    _currentScore = bestScore;
                    _currentActionStartTime = now;

                    UtilityAIRuntimeAction selected = model.FindAction(bestActionId);
                    if (selected != null) selected.LastSelectedTime = (float)now;
                    switchReason = "AdoptBest";
                }
            }

            return Complete(now, previousActionId, bestActionId, bestScore, switchReason, snapshot);
        }

        /// <summary>清空当前 Action 与驻留计时，下一次评估可立即重选（用于 FSM 状态漂移后的强制重评估）。</summary>
        public void ResetCurrentAction()
        {
            _currentActionId = null;
            _currentScore = 0f;
            _currentActionStartTime = double.NegativeInfinity;
        }

        /// <summary>完全清空评估状态（换 Profile / 销毁时使用）。</summary>
        public void Clear()
        {
            ResetCurrentAction();
            _lastEvaluationTime = double.NegativeInfinity;
        }

        /// <summary>
        /// 直接设置当前状态。用于存档恢复、外部接管或调试回放。
        /// </summary>
        public void SetCurrentState(string actionId, float score, double actionStartTime, double lastEvaluationTime)
        {
            _currentActionId = actionId;
            _currentScore = score;
            _currentActionStartTime = actionStartTime;
            _lastEvaluationTime = lastEvaluationTime;
        }

        private UtilityAIDecisionResult Complete(
            double now,
            string previousActionId,
            string bestActionId,
            float bestScore,
            string switchReason,
            UtilityAIDecisionSnapshot snapshot)
        {
            if (snapshot != null)
            {
                snapshot.Time = now;
                snapshot.CurrentActionId = _currentActionId;
                snapshot.CurrentScore = _currentScore;
                snapshot.BestActionId = bestActionId;
                snapshot.BestScore = bestScore;
                snapshot.SwitchReason = switchReason;
            }

            return new UtilityAIDecisionResult
            {
                Time = now,
                PreviousActionId = previousActionId,
                CurrentActionId = _currentActionId,
                CurrentScore = _currentScore,
                BestActionId = bestActionId,
                BestScore = bestScore,
                SwitchReason = switchReason,
                Changed = !string.Equals(previousActionId, _currentActionId, StringComparison.Ordinal),
                Snapshot = snapshot,
            };
        }

        private static bool IsEligible(
            UtilityAIRuntimeAction action,
            UtilityAIRuntimeAction currentAction,
            double now,
            out string ineligibleReason)
        {
            ineligibleReason = null;

            if (action == null)
            {
                ineligibleReason = "NullAction";
                return false;
            }

            if (string.IsNullOrEmpty(action.Id))
            {
                ineligibleReason = "EmptyActionId";
                return false;
            }

            if (!action.Enabled)
            {
                ineligibleReason = "Disabled";
                return false;
            }

            bool isCurrent = currentAction != null &&
                             string.Equals(currentAction.Id, action.Id, StringComparison.Ordinal);
            if (!isCurrent)
            {
                if (action.CooldownSeconds > 0f && now - action.LastSelectedTime < action.CooldownSeconds)
                {
                    ineligibleReason = "Cooldown";
                    return false;
                }
            }

            return true;
        }
    }
}
