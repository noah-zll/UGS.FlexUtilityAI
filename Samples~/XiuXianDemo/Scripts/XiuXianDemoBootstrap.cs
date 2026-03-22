using System.Collections.Generic;
using UnityEngine;

namespace UGS.FlexUtilityAI.Samples
{
    public sealed class XiuXianDemoBootstrap : MonoBehaviour
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
            AddMeditate();
            AddChase();
            AddCastSpell();
            AddFlee();

            brain.Model.Touch();
        }

        private void AddMeditate()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Meditate");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 1.5f;

            var qiLow = a.AddConsideration("QiLow");
            qiLow.BlackboardFloatKey = "Qi";
            qiLow.InputMin = 0f;
            qiLow.InputMax = 1f;
            qiLow.Invert = true;
            qiLow.Weight = 1f;

            var dangerLow = a.AddConsideration("DangerLow");
            dangerLow.BlackboardFloatKey = "Danger";
            dangerLow.InputMin = 0f;
            dangerLow.InputMax = 1f;
            dangerLow.Invert = true;
            dangerLow.Weight = 0.8f;
        }

        private void AddChase()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Chase");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.5f;

            var enemyFar = a.AddConsideration("EnemyFar");
            enemyFar.BlackboardFloatKey = "EnemyDistance";
            enemyFar.InputMin = 0f;
            enemyFar.InputMax = 30f;
            enemyFar.Weight = 1f;

            var dangerLow = a.AddConsideration("DangerLow");
            dangerLow.BlackboardFloatKey = "Danger";
            dangerLow.InputMin = 0f;
            dangerLow.InputMax = 1f;
            dangerLow.Invert = true;
            dangerLow.Weight = 0.6f;
        }

        private void AddCastSpell()
        {
            UtilityAIRuntimeAction a = brain.AddAction("CastSpell");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.5f;

            var manaHigh = a.AddConsideration("ManaHigh");
            manaHigh.BlackboardFloatKey = "Mana";
            manaHigh.InputMin = 0f;
            manaHigh.InputMax = 1f;
            manaHigh.Weight = 1f;
            manaHigh.UseFailThreshold = true;
            manaHigh.FailIfBelow = 0.2f;

            var enemyClose = a.AddConsideration("EnemyClose");
            enemyClose.BlackboardFloatKey = "EnemyDistance";
            enemyClose.InputMin = 0f;
            enemyClose.InputMax = 15f;
            enemyClose.Invert = true;
            enemyClose.Weight = 0.9f;
        }

        private void AddFlee()
        {
            UtilityAIRuntimeAction a = brain.AddAction("Flee");
            if (a == null) return;

            a.MinSwitchIntervalSeconds = 0.75f;

            var dangerHigh = a.AddConsideration("DangerHigh");
            dangerHigh.BlackboardFloatKey = "Danger";
            dangerHigh.InputMin = 0f;
            dangerHigh.InputMax = 1f;
            dangerHigh.Weight = 1f;

            var healthLow = a.AddConsideration("HealthLow");
            healthLow.BlackboardFloatKey = "Health";
            healthLow.InputMin = 0f;
            healthLow.InputMax = 1f;
            healthLow.Invert = true;
            healthLow.Weight = 0.9f;
        }
    }
}

