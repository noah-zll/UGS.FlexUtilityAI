using UnityEngine;

namespace UGS.FlexUtilityAI.Samples
{
    public sealed class XiuXianScenarioCurves : MonoBehaviour
    {
        [SerializeField] private UtilityAIBrain brain;
        [SerializeField] private float loopSeconds = 120f;
        [SerializeField] private bool writeDerivedEnemyDistance01 = true;

        [Header("World Curves")]
        [SerializeField] private AnimationCurve danger01 = AnimationCurve.EaseInOut(0f, 0.2f, 1f, 0.8f);
        [SerializeField] private AnimationCurve enemyDistance = AnimationCurve.EaseInOut(0f, 25f, 1f, 3f);
        [SerializeField] private AnimationCurve weatherBad01 = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Inventory Pulses")]
        [SerializeField] private AnimationCurve hasHerb01 = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve hasPill01 = AnimationCurve.Linear(0f, 0f, 1f, 0f);

        private void Reset()
        {
            brain = GetComponent<UtilityAIBrain>();
        }

        private void Update()
        {
            if (brain == null) return;

            float dur = Mathf.Max(0.01f, loopSeconds);
            float t01 = Mathf.Repeat(Time.time, dur) / dur;

            float danger = Mathf.Clamp01(danger01.Evaluate(t01));
            float enemyDist = Mathf.Max(0f, enemyDistance.Evaluate(t01));
            float badWeather = Mathf.Clamp01(weatherBad01.Evaluate(t01));
            float herb = Mathf.Clamp01(hasHerb01.Evaluate(t01));
            float pill = Mathf.Clamp01(hasPill01.Evaluate(t01));

            brain.SetFloat("Danger", danger);
            brain.SetFloat("EnemyDistance", enemyDist);
            brain.SetFloat("WeatherBad", badWeather);
            brain.SetFloat("HasHerb", herb >= 0.5f ? 1f : 0f);
            brain.SetFloat("HasPill", pill >= 0.5f ? 1f : 0f);

            if (writeDerivedEnemyDistance01)
            {
                float enemy01 = Mathf.Clamp01(enemyDist / 30f);
                brain.SetFloat("EnemyDistance01", enemy01);
            }
        }
    }
}

