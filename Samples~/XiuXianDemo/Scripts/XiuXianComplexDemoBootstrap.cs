using System.Collections.Generic;
using UnityEngine;

namespace UGS.FlexUtilityAI.Samples
{
    public sealed class XiuXianComplexDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private UtilityAIBrain brain;
        [SerializeField] private bool rebuildModelOnStart = true;
        [SerializeField] private UtilityAIAggregationMode aggregationMode = UtilityAIAggregationMode.Product;

        private void Reset()
        {
            brain = GetComponent<UtilityAIBrain>();
        }

        private void Start()
        {
            if (brain == null) return;

            brain.EnsureModelOrCreateEmpty();
            if (brain.Model == null) return;

            brain.Model.SetAggregationMode(aggregationMode);

            if (rebuildModelOnStart)
            {
                var ids = new List<string>(brain.Model.Actions.Count);
                for (int i = 0; i < brain.Model.Actions.Count; i++)
                {
                    ids.Add(brain.Model.Actions[i].Id);
                }

                for (int i = 0; i < ids.Count; i++)
                {
                    brain.RemoveAction(ids[i]);
                }
            }

            BuildActions();
            brain.RefreshExecutors();
        }

        private void BuildActions()
        {
            AddAction_Meditate();
            AddAction_Rest();
            AddAction_Explore();
            AddAction_CraftPill();
            AddAction_Heal();
            AddAction_Breakthrough();

            AddAction_Chase();
            AddAction_CastSpell();
            AddAction_Defend();
            AddAction_Flee();

            brain.Model.Touch();
        }

        private void AddAction_Meditate()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Meditate");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 1.0f;
            a.Weight = 0.9f;

            Add01(a, "QiLow", "Qi", invert: true, weight: 1.0f);
            Add01(a, "DangerLow", "Danger", invert: true, weight: 0.7f);
            Add01(a, "FatigueHigh", "Fatigue", invert: false, weight: 0.5f);
        }

        private void AddAction_Rest()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Rest");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.75f;
            a.Weight = 0.7f;

            Add01(a, "FatigueHigh", "Fatigue", invert: false, weight: 1.0f);
            Add01(a, "DangerLow", "Danger", invert: true, weight: 0.6f);
            Add01(a, "EnemyFar", "EnemyDistance01", invert: false, weight: 0.5f);
        }

        private void AddAction_Explore()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Explore");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.5f;
            a.Weight = 0.9f;

            Add01(a, "HungerLow", "Hunger", invert: true, weight: 0.7f);
            Add01(a, "FatigueLow", "Fatigue", invert: true, weight: 0.7f);
            Add01(a, "DangerLow", "Danger", invert: true, weight: 0.8f);
            Add01(a, "NeedsHerb", "HasHerb", invert: true, weight: 0.9f, failIfBelow: 0.05f);
        }

        private void AddAction_CraftPill()
        {
            UtilityAIRuntimeAction a = brain.AddAction("CraftPill");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.75f;
            a.Weight = 0.8f;
            a.Enabled = false;

            Add01(a, "HasHerb", "HasHerb", invert: false, weight: 1.0f, failIfBelow: 0.5f);
            Add01(a, "HasNoPill", "HasPill", invert: true, weight: 0.8f);
            Add01(a, "ManaHigh", "Mana", invert: false, weight: 0.7f, failIfBelow: 0.2f);
            Add01(a, "DangerLow", "Danger", invert: true, weight: 0.6f);
        }

        private void AddAction_Heal()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Heal");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.25f;
            a.Weight = 1.0f;

            Add01(a, "HealthLow", "Health", invert: true, weight: 1.0f);
            Add01(a, "HasPill", "HasPill", invert: false, weight: 0.8f);
            Add01(a, "DangerLow", "Danger", invert: true, weight: 0.4f);
        }

        private void AddAction_Breakthrough()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Breakthrough");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 1.5f;
            a.Weight = 0.9f;
            a.Enabled = false;

            Add01(a, "QiHigh", "Qi", invert: false, weight: 1.0f, failIfBelow: 0.6f);
            Add01(a, "DangerLow", "Danger", invert: true, weight: 1.0f, failIfBelow: 0.4f);
            Add01(a, "FatigueLow", "Fatigue", invert: true, weight: 0.5f);
            Add01(a, "ProgressLow", "RealmProgress", invert: true, weight: 0.6f);
        }

        private void AddAction_Chase()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Chase");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.25f;
            a.Weight = 0.85f;

            Add01(a, "EnemyFar", "EnemyDistance01", invert: false, weight: 1.0f, failIfBelow: 0.05f);
            Add01(a, "DangerMedium", "Danger", invert: true, weight: 0.2f);
            Add01(a, "HealthNotLow", "Health", invert: false, weight: 0.4f);
        }

        private void AddAction_CastSpell()
        {
            UtilityAIRuntimeAction a = brain.AddAction("CastSpell");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.25f;
            a.Weight = 1.0f;

            Add01(a, "EnemyClose", "EnemyDistance01", invert: true, weight: 0.9f, failIfBelow: 0.1f);
            Add01(a, "ManaHigh", "Mana", invert: false, weight: 1.0f, failIfBelow: 0.15f);
            Add01(a, "DangerHigh", "Danger", invert: false, weight: 0.6f);
        }

        private void AddAction_Defend()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Defend");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.35f;
            a.Weight = 0.9f;

            Add01(a, "DangerHigh", "Danger", invert: false, weight: 1.0f);
            Add01(a, "HealthNotLow", "Health", invert: false, weight: 0.7f);
            Add01(a, "EnemyNotFar", "EnemyDistance01", invert: true, weight: 0.4f);
        }

        private void AddAction_Flee()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Flee");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.35f;
            a.Weight = 1.0f;

            Add01(a, "DangerHigh", "Danger", invert: false, weight: 1.0f);
            Add01(a, "HealthLow", "Health", invert: true, weight: 0.9f);
            Add01(a, "EnemyClose", "EnemyDistance01", invert: true, weight: 0.6f);
        }

        private static UtilityAIRuntimeConsideration Add01(UtilityAIRuntimeAction action, string id, string key, bool invert, float weight, float? failIfBelow = null)
        {
            var c = action.AddConsideration(id);
            if (c == null) return null;

            c.BlackboardFloatKey = key;
            c.InputMin = 0f;
            c.InputMax = 1f;
            c.Response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            c.Weight = weight;
            c.Invert = invert;
            if (failIfBelow.HasValue)
            {
                c.UseFailThreshold = true;
                c.FailIfBelow = failIfBelow.Value;
            }

            return c;
        }
    }
}

