using System;
using UnityEngine;

namespace UGS.FlexUtilityAI.Samples
{
    public sealed class XiuXianAgentNeedsAndEffects : MonoBehaviour
    {
        [SerializeField] private UtilityAIBrain brain;
        [SerializeField] private bool autoInitialize = true;

        [Header("Needs")]
        [Range(0f, 1f)] public float health = 1f;
        [Range(0f, 1f)] public float mana = 1f;
        [Range(0f, 1f)] public float qi = 0.5f;
        [Range(0f, 1f)] public float hunger = 0.2f;
        [Range(0f, 1f)] public float fatigue = 0.2f;
        [Range(0f, 1f)] public float realmProgress = 0f;

        [Header("Rates")]
        [SerializeField] private float hungerPerSecond = 0.02f;
        [SerializeField] private float fatiguePerSecond = 0.015f;
        [SerializeField] private float passiveManaRegenPerSecond = 0.01f;
        [SerializeField] private float passiveHealthRegenPerSecond = 0.004f;

        [Header("Action Effects")]
        [SerializeField] private float meditateQiPerSecond = 0.08f;
        [SerializeField] private float meditateManaPerSecond = 0.03f;
        [SerializeField] private float restFatigueDownPerSecond = 0.08f;
        [SerializeField] private float exploreManaDownPerSecond = 0.02f;
        [SerializeField] private float castSpellManaDownPerSecond = 0.12f;
        [SerializeField] private float defendHealthDownPerSecond = 0.03f;
        [SerializeField] private float fleeFatigueUpPerSecond = 0.05f;
        [SerializeField] private float breakthroughQiDownPerSecond = 0.18f;
        [SerializeField] private float breakthroughProgressUpPerSecond = 0.05f;

        private float _lastWriteTime = float.NegativeInfinity;

        private void Reset()
        {
            brain = GetComponent<UtilityAIBrain>();
        }

        private bool EnsureBrain()
        {
            if (brain != null) return true;
            brain = GetComponent<UtilityAIBrain>();
            return brain != null;
        }

        private void OnEnable()
        {
            if (!autoInitialize) return;
            if (!EnsureBrain()) return;
            WriteAllToBlackboard();
        }

        private void Update()
        {
            if (!EnsureBrain()) return;

            float dt = Time.deltaTime;

            hunger = Clamp01(hunger + hungerPerSecond * dt);
            fatigue = Clamp01(fatigue + fatiguePerSecond * dt);
            mana = Clamp01(mana + passiveManaRegenPerSecond * dt);
            health = Clamp01(health + passiveHealthRegenPerSecond * dt);

            string a = brain.CurrentActionId;
            if (!string.IsNullOrEmpty(a))
            {
                ApplyActionEffects(a, dt);
            }

            WriteAllToBlackboard();
        }

        private void ApplyActionEffects(string actionId, float dt)
        {
            if (string.Equals(actionId, "Meditate", StringComparison.Ordinal))
            {
                qi = Clamp01(qi + meditateQiPerSecond * dt);
                mana = Clamp01(mana + meditateManaPerSecond * dt);
                fatigue = Clamp01(fatigue - 0.04f * dt);
                return;
            }

            if (string.Equals(actionId, "Rest", StringComparison.Ordinal))
            {
                fatigue = Clamp01(fatigue - restFatigueDownPerSecond * dt);
                mana = Clamp01(mana + 0.02f * dt);
                return;
            }

            if (string.Equals(actionId, "Explore", StringComparison.Ordinal))
            {
                mana = Clamp01(mana - exploreManaDownPerSecond * dt);
                fatigue = Clamp01(fatigue + 0.03f * dt);
                qi = Clamp01(qi - 0.01f * dt);
                return;
            }

            if (string.Equals(actionId, "Chase", StringComparison.Ordinal))
            {
                fatigue = Clamp01(fatigue + 0.04f * dt);
                mana = Clamp01(mana - 0.01f * dt);
                return;
            }

            if (string.Equals(actionId, "CastSpell", StringComparison.Ordinal))
            {
                mana = Clamp01(mana - castSpellManaDownPerSecond * dt);
                qi = Clamp01(qi - 0.03f * dt);
                return;
            }

            if (string.Equals(actionId, "Defend", StringComparison.Ordinal))
            {
                mana = Clamp01(mana - 0.03f * dt);
                health = Clamp01(health - defendHealthDownPerSecond * dt);
                fatigue = Clamp01(fatigue + 0.02f * dt);
                return;
            }

            if (string.Equals(actionId, "Flee", StringComparison.Ordinal))
            {
                fatigue = Clamp01(fatigue + fleeFatigueUpPerSecond * dt);
                mana = Clamp01(mana - 0.02f * dt);
                return;
            }

            if (string.Equals(actionId, "CraftPill", StringComparison.Ordinal))
            {
                mana = Clamp01(mana - 0.05f * dt);
                qi = Clamp01(qi - 0.02f * dt);
                fatigue = Clamp01(fatigue + 0.01f * dt);
                return;
            }

            if (string.Equals(actionId, "Heal", StringComparison.Ordinal))
            {
                mana = Clamp01(mana - 0.02f * dt);
                health = Clamp01(health + 0.12f * dt);
                fatigue = Clamp01(fatigue + 0.01f * dt);
                return;
            }

            if (string.Equals(actionId, "Breakthrough", StringComparison.Ordinal))
            {
                qi = Clamp01(qi - breakthroughQiDownPerSecond * dt);
                realmProgress = Clamp01(realmProgress + breakthroughProgressUpPerSecond * dt);
                fatigue = Clamp01(fatigue + 0.03f * dt);
                return;
            }
        }

        private void WriteAllToBlackboard()
        {
            if (!EnsureBrain()) return;

            float now = Time.time;
            if (now - _lastWriteTime < 0.02f) return;
            _lastWriteTime = now;

            brain.SetFloat("Health", health);
            brain.SetFloat("Mana", mana);
            brain.SetFloat("Qi", qi);
            brain.SetFloat("Hunger", hunger);
            brain.SetFloat("Fatigue", fatigue);
            brain.SetFloat("RealmProgress", realmProgress);
        }

        private static float Clamp01(float v)
        {
            if (v <= 0f) return 0f;
            if (v >= 1f) return 1f;
            return v;
        }
    }
}
