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
        private UtilityAIRuntimeModel _model;
        private double _lastEvaluationTime = double.NegativeInfinity;
        private string _currentActionId;
        private float _currentScore;
        private double _currentActionStartTime = double.NegativeInfinity;
        private IUtilityAIActionExecutor _currentExecutor;

        public event EventHandler<UtilityAIActionChangedEventArgs> ActionChanged;

        public UtilityAIProfile Profile => profile;
        public UtilityAIRuntimeModel Model => _model;
        public UtilityAIBlackboard Blackboard => _blackboard;

        public string CurrentActionId => _currentActionId;
        public float CurrentScore => _currentScore;

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
                if (evaluationIntervalSeconds <= 0f || now - _lastEvaluationTime >= evaluationIntervalSeconds)
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
            if (removed && string.Equals(_currentActionId, id, StringComparison.Ordinal))
            {
                TryExitCurrentExecutor();
                _currentActionId = null;
                _currentScore = 0f;
                _currentActionStartTime = double.NegativeInfinity;
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

            if (!string.IsNullOrEmpty(_currentActionId) && _executorsByActionId.TryGetValue(_currentActionId, out IUtilityAIActionExecutor current))
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
            _lastEvaluationTime = now;

            if (_model == null || _model.Actions.Count == 0)
            {
                UpdateDecisionSnapshot(now, null, "NoModelOrNoActions");
                return;
            }

            string previousActionId = _currentActionId;

            UtilityAIRuntimeAction currentAction = string.IsNullOrEmpty(_currentActionId) ? null : _model.FindAction(_currentActionId);
            float pinnedSeconds = 0f;
            if (currentAction != null)
            {
                pinnedSeconds = Mathf.Max(0f, minTimeInStateSeconds);
                pinnedSeconds = Mathf.Max(pinnedSeconds, currentAction.MinSwitchIntervalSeconds);
            }
            bool currentPinned = currentAction != null
                                 && currentAction.Enabled
                                 && pinnedSeconds > 0f
                                 && now - _currentActionStartTime < pinnedSeconds;

            float bestScore = -1f;
            string bestActionId = null;
            float currentScore = -1f;

            bool wantSnapshot = enableDebugSnapshot || enableDecisionRecording;
            bool includeBreakdown = enableDebugSnapshot || recordBreakdown;

            UtilityAIDecisionSnapshot snapshot = wantSnapshot ? new UtilityAIDecisionSnapshot() : null;
            if (snapshot != null)
            {
                snapshot.Time = now;
                snapshot.ModelVersion = _model.Version;
                snapshot.CurrentActionId = _currentActionId;
            }

            for (int i = 0; i < _model.Actions.Count; i++)
            {
                UtilityAIRuntimeAction action = _model.Actions[i];
                bool eligible = IsEligible(action, currentAction, now, out string ineligibleReason);

                UtilityAIActionScoreSnapshot actionSnapshot = null;
                float score = 0f;
                if (eligible)
                {
                    actionSnapshot = UtilityAIScorer.EvaluateAction(action, _blackboard, _model.AggregationMode, includeBreakdown: includeBreakdown);
                    score = actionSnapshot.Score;
                }

                if (snapshot != null)
                {
                    if (actionSnapshot == null) actionSnapshot = new UtilityAIActionScoreSnapshot { ActionId = action.Id, Score = score, Weight = action.Weight };
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
                UpdateDecisionSnapshot(now, bestActionId, "PinnedByMinSwitchInterval", snapshot, currentScore, bestScore);
                return;
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
                float threshold = baseCurrentScore <= 0f ? 0f : baseCurrentScore * (1f + Mathf.Max(0f, switchBias));
                threshold += Mathf.Max(0f, switchScoreDelta);
                canSwitch = bestActionId != null && bestScore >= threshold && !string.Equals(bestActionId, _currentActionId, StringComparison.Ordinal);
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

                UtilityAIRuntimeAction selected = _model.FindAction(bestActionId);
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
                    UtilityAIRuntimeAction selected = _model.FindAction(bestActionId);
                    if (selected != null) selected.LastSelectedTime = (float)now;
                    switchReason = "AdoptBest";
                }
            }

            UpdateDecisionSnapshot(now, bestActionId, switchReason, snapshot, _currentScore, bestScore);

            if (!string.Equals(previousActionId, _currentActionId, StringComparison.Ordinal))
            {
                TrySwitchExecutor(previousActionId, _currentActionId);
                if (logOnActionSwitch)
                {
                    Debug.Log($"{name} UtilityAI switch {previousActionId} -> {_currentActionId} score={_currentScore:F3} reason={switchReason}", this);
                }
                ActionChanged?.Invoke(this, new UtilityAIActionChangedEventArgs(previousActionId, _currentActionId, _currentScore, now));
            }
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

            if (!string.IsNullOrEmpty(currentActionId) && _executorsByActionId.TryGetValue(currentActionId, out IUtilityAIActionExecutor next))
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

        private bool IsEligible(UtilityAIRuntimeAction action, UtilityAIRuntimeAction currentAction, double now, out string ineligibleReason)
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

            bool isCurrent = currentAction != null && string.Equals(currentAction.Id, action.Id, StringComparison.Ordinal);
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

        private void UpdateDecisionSnapshot(double now, string bestActionId, string reason)
        {
            UpdateDecisionSnapshot(now, bestActionId, reason, null, _currentScore, _currentScore);
        }

        private void UpdateDecisionSnapshot(double now, string bestActionId, string reason, UtilityAIDecisionSnapshot snapshot, float currentScore, float bestScoreForSnapshot)
        {
            if (snapshot == null && (enableDebugSnapshot || enableDecisionRecording))
            {
                snapshot = new UtilityAIDecisionSnapshot();
            }

            if (snapshot != null)
            {
                snapshot.Time = now;
                snapshot.CurrentActionId = _currentActionId;
                snapshot.CurrentScore = currentScore;
                snapshot.BestActionId = bestActionId;
                snapshot.BestScore = bestScoreForSnapshot;
                snapshot.SwitchReason = reason;
                snapshot.ModelVersion = _model != null ? _model.Version : 0;
                LastDecisionSnapshot = snapshot;

                if (enableDecisionRecording)
                {
                    AddDecisionHistory(snapshot);
                }
            }
            else
            {
                LastDecisionSnapshot = null;
            }
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
