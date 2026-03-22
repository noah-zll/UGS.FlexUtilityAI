using System;
using System.Collections.Generic;
using UnityEngine;

namespace UGS.FlexUtilityAI
{
    public sealed class UtilityAIRuntimeModel
    {
        private readonly List<UtilityAIRuntimeAction> _actions;

        public UtilityAIAggregationMode AggregationMode { get; private set; }
        public int Version { get; private set; }
        public IReadOnlyList<UtilityAIRuntimeAction> Actions => _actions;

        private UtilityAIRuntimeModel(UtilityAIAggregationMode aggregationMode, List<UtilityAIRuntimeAction> actions)
        {
            AggregationMode = aggregationMode;
            _actions = actions ?? new List<UtilityAIRuntimeAction>();
        }

        public static UtilityAIRuntimeModel CreateEmpty(UtilityAIAggregationMode aggregationMode)
        {
            return new UtilityAIRuntimeModel(aggregationMode, new List<UtilityAIRuntimeAction>());
        }

        public static UtilityAIRuntimeModel FromProfile(UtilityAIProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var actions = new List<UtilityAIRuntimeAction>(profile.Actions.Count);
            for (int i = 0; i < profile.Actions.Count; i++)
            {
                actions.Add(UtilityAIRuntimeAction.FromDefinition(profile.Actions[i]));
            }

            return new UtilityAIRuntimeModel(profile.AggregationMode, actions);
        }

        public UtilityAIRuntimeAction AddAction(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var action = new UtilityAIRuntimeAction(id);
            _actions.Add(action);
            Version++;
            return action;
        }

        public bool RemoveAction(string id)
        {
            int index = IndexOfAction(id);
            if (index < 0) return false;
            _actions.RemoveAt(index);
            Version++;
            return true;
        }

        public UtilityAIRuntimeAction FindAction(string id)
        {
            int index = IndexOfAction(id);
            if (index < 0) return null;
            return _actions[index];
        }

        public int IndexOfAction(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _actions.Count; i++)
            {
                if (string.Equals(_actions[i].Id, id, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        public void SetAggregationMode(UtilityAIAggregationMode mode)
        {
            if (AggregationMode == mode) return;
            AggregationMode = mode;
            Version++;
        }

        public void Touch()
        {
            Version++;
        }
    }

    public sealed class UtilityAIRuntimeAction
    {
        private readonly List<UtilityAIRuntimeConsideration> _considerations = new List<UtilityAIRuntimeConsideration>();

        public string Id { get; }
        public bool Enabled { get; set; } = true;
        public float Weight { get; set; } = 1f;
        public float CooldownSeconds { get; set; }
        public float MinSwitchIntervalSeconds { get; set; }

        public float LastSelectedTime { get; set; } = float.NegativeInfinity;

        public IReadOnlyList<UtilityAIRuntimeConsideration> Considerations => _considerations;

        public UtilityAIRuntimeAction(string id)
        {
            Id = id;
        }

        public static UtilityAIRuntimeAction FromDefinition(UtilityAIActionDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var action = new UtilityAIRuntimeAction(definition.Id)
            {
                Enabled = definition.Enabled,
                Weight = definition.Weight,
                CooldownSeconds = definition.CooldownSeconds,
                MinSwitchIntervalSeconds = definition.MinSwitchIntervalSeconds
            };

            for (int i = 0; i < definition.Considerations.Count; i++)
            {
                action._considerations.Add(UtilityAIRuntimeConsideration.FromDefinition(definition.Considerations[i]));
            }

            return action;
        }

        public UtilityAIRuntimeConsideration AddConsideration(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var c = new UtilityAIRuntimeConsideration(id);
            _considerations.Add(c);
            return c;
        }

        public bool RemoveConsideration(string id)
        {
            int index = IndexOfConsideration(id);
            if (index < 0) return false;
            _considerations.RemoveAt(index);
            return true;
        }

        public UtilityAIRuntimeConsideration FindConsideration(string id)
        {
            int index = IndexOfConsideration(id);
            if (index < 0) return null;
            return _considerations[index];
        }

        public int IndexOfConsideration(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _considerations.Count; i++)
            {
                if (string.Equals(_considerations[i].Id, id, StringComparison.Ordinal)) return i;
            }

            return -1;
        }
    }

    public sealed class UtilityAIRuntimeConsideration
    {
        public string Id { get; }

        public string BlackboardFloatKey { get; set; }
        public float InputMin { get; set; }
        public float InputMax { get; set; } = 1f;
        public AnimationCurve Response { get; set; } = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public float Weight { get; set; } = 1f;
        public bool Invert { get; set; }
        public bool UseFailThreshold { get; set; }
        public float FailIfBelow { get; set; }

        public UtilityAIRuntimeConsideration(string id)
        {
            Id = id;
        }

        public static UtilityAIRuntimeConsideration FromDefinition(UtilityAIConsiderationDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            return new UtilityAIRuntimeConsideration(definition.Id)
            {
                BlackboardFloatKey = definition.BlackboardFloatKey,
                InputMin = definition.InputMin,
                InputMax = definition.InputMax,
                Response = definition.Response,
                Weight = definition.Weight,
                Invert = definition.Invert,
                UseFailThreshold = definition.UseFailThreshold,
                FailIfBelow = definition.FailIfBelow
            };
        }
    }
}
