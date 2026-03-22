using System;
using UnityEngine;

namespace UGS.FlexUtilityAI.Samples
{
    public sealed class XiuXianRuntimeModelMutator : MonoBehaviour
    {
        [SerializeField] private UtilityAIBrain brain;
        [SerializeField] private bool enableMutations = true;
        [SerializeField] private float updateIntervalSeconds = 0.25f;

        [Header("Rules")]
        [SerializeField] private float enableCraftPillHerbThreshold = 0.5f;
        [SerializeField] private float enableBreakthroughQiThreshold = 0.75f;
        [SerializeField] private float enableBreakthroughDangerMax = 0.25f;
        [SerializeField] private float enableBreakthroughProgressMax = 0.95f;

        private float _nextUpdateTime;

        private void Reset()
        {
            brain = GetComponent<UtilityAIBrain>();
        }

        private void Update()
        {
            if (!enableMutations) return;
            if (brain == null) return;
            if (brain.Model == null) return;

            float now = Time.time;
            if (now < _nextUpdateTime) return;
            _nextUpdateTime = now + Mathf.Max(0.02f, updateIntervalSeconds);

            float hasHerb = Get01("HasHerb");
            float hasPill = Get01("HasPill");
            float qi = Get01("Qi");
            float danger = Get01("Danger");
            float progress = Get01("RealmProgress");

            SetEnabled("CraftPill", hasHerb >= enableCraftPillHerbThreshold && hasPill < 0.5f);
            SetEnabled("Breakthrough", qi >= enableBreakthroughQiThreshold && danger <= enableBreakthroughDangerMax && progress <= enableBreakthroughProgressMax);
        }

        private float Get01(string key)
        {
            return Mathf.Clamp01(brain.Blackboard.GetFloatOrDefault(key, 0f));
        }

        private void SetEnabled(string actionId, bool enabled)
        {
            UtilityAIRuntimeAction a = brain.Model.FindAction(actionId);
            if (a == null) return;
            if (a.Enabled == enabled) return;
            a.Enabled = enabled;
            brain.Model.Touch();
        }
    }
}

