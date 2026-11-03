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

        /// <summary>
        /// 用运行时模型反向生成 Profile（编辑器生成资产、或运行时快照导出）。
        /// <para>注意：生成的是内存对象，保存为资产需调用方自行 <c>AssetDatabase.CreateAsset</c>。</para>
        /// </summary>
        public static UtilityAIProfile CreateFromRuntimeModel(UtilityAIRuntimeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var profile = CreateInstance<UtilityAIProfile>();
            profile.name = nameof(UtilityAIProfile);
            profile.aggregationMode = model.AggregationMode;
            profile.actions = new List<UtilityAIActionDefinition>(model.Actions.Count);

            for (int i = 0; i < model.Actions.Count; i++)
            {
                profile.actions.Add(UtilityAIActionDefinition.FromRuntime(model.Actions[i]));
            }

            return profile;
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

        internal static UtilityAIActionDefinition FromRuntime(UtilityAIRuntimeAction action)
        {
            var definition = new UtilityAIActionDefinition
            {
                id = action.Id,
                enabled = action.Enabled,
                weight = action.Weight,
                cooldownSeconds = action.CooldownSeconds,
                minSwitchIntervalSeconds = action.MinSwitchIntervalSeconds,
                considerations = new List<UtilityAIConsiderationDefinition>(action.Considerations.Count)
            };

            for (int i = 0; i < action.Considerations.Count; i++)
            {
                definition.considerations.Add(
                    UtilityAIConsiderationDefinition.FromRuntime(action.Considerations[i]));
            }

            return definition;
        }
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

        internal static UtilityAIConsiderationDefinition FromRuntime(UtilityAIRuntimeConsideration consideration)
        {
            return new UtilityAIConsiderationDefinition
            {
                id = consideration.Id,
                blackboardFloatKey = consideration.BlackboardFloatKey,
                inputMin = consideration.InputMin,
                inputMax = consideration.InputMax,
                response = consideration.Response,
                weight = consideration.Weight,
                invert = consideration.Invert,
                useFailThreshold = consideration.UseFailThreshold,
                failIfBelow = consideration.FailIfBelow
            };
        }
    }
}

