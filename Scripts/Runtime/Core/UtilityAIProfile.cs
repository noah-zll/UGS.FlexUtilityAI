using System;
using System.Collections.Generic;
using UnityEngine;

namespace UGS.FlexUtilityAI
{
    [CreateAssetMenu(menuName = "UGS/FlexUtilityAI/Utility AI Profile", fileName = "UtilityAIProfile")]
    public sealed class UtilityAIProfile : ScriptableObject
    {
        [SerializeField] private UtilityAIAggregationMode aggregationMode = UtilityAIAggregationMode.Product;
        [SerializeField] private List<UtilityAIActionDefinition> actions = new List<UtilityAIActionDefinition>();

        public UtilityAIAggregationMode AggregationMode => aggregationMode;
        public IReadOnlyList<UtilityAIActionDefinition> Actions => actions;

        public UtilityAIRuntimeModel CreateRuntimeModel()
        {
            return UtilityAIRuntimeModel.FromProfile(this);
        }
    }

    [Serializable]
    public sealed class UtilityAIActionDefinition
    {
        [SerializeField] private string id = "Action";
        [SerializeField] private bool enabled = true;
        [SerializeField] private float weight = 1f;
        [SerializeField] private float cooldownSeconds;
        [SerializeField] private float minSwitchIntervalSeconds;
        [SerializeField] private List<UtilityAIConsiderationDefinition> considerations = new List<UtilityAIConsiderationDefinition>();

        public string Id => id;
        public bool Enabled => enabled;
        public float Weight => weight;
        public float CooldownSeconds => cooldownSeconds;
        public float MinSwitchIntervalSeconds => minSwitchIntervalSeconds;
        public IReadOnlyList<UtilityAIConsiderationDefinition> Considerations => considerations;
    }

    [Serializable]
    public sealed class UtilityAIConsiderationDefinition
    {
        [SerializeField] private string id = "Consideration";
        [SerializeField] private string blackboardFloatKey;
        [SerializeField] private float inputMin;
        [SerializeField] private float inputMax = 1f;
        [SerializeField] private AnimationCurve response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private float weight = 1f;
        [SerializeField] private bool invert;
        [SerializeField] private bool useFailThreshold;
        [SerializeField] private float failIfBelow;

        public string Id => id;
        public string BlackboardFloatKey => blackboardFloatKey;
        public float InputMin => inputMin;
        public float InputMax => inputMax;
        public AnimationCurve Response => response;
        public float Weight => weight;
        public bool Invert => invert;
        public bool UseFailThreshold => useFailThreshold;
        public float FailIfBelow => failIfBelow;
    }
}

