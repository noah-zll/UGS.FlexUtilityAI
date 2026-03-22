using UnityEngine;

namespace UGS.FlexUtilityAI.Samples
{
    public sealed class XiuXianDemoDriver : MonoBehaviour
    {
        [SerializeField] private UtilityAIBrain brain;
        [SerializeField] private bool animate = true;

        [Header("Blackboard")]
        [Range(0f, 1f)] public float health = 1f;
        [Range(0f, 1f)] public float mana = 1f;
        [Range(0f, 1f)] public float qi = 1f;
        [Range(0f, 1f)] public float danger = 0f;
        [Range(0f, 30f)] public float enemyDistance = 20f;

        [Header("Animation")]
        [SerializeField] private float tHealth = 0.09f;
        [SerializeField] private float tMana = 0.12f;
        [SerializeField] private float tQi = 0.07f;
        [SerializeField] private float tDanger = 0.16f;
        [SerializeField] private float tEnemy = 0.10f;

        private void Reset()
        {
            brain = GetComponent<UtilityAIBrain>();
        }

        private void Update()
        {
            if (brain == null) return;

            if (animate)
            {
                float t = Time.time;
                health = Mathf.Clamp01(0.65f + 0.35f * Mathf.Sin(t * tHealth));
                mana = Mathf.Clamp01(0.55f + 0.45f * Mathf.Sin(t * tMana + 1.7f));
                qi = Mathf.Clamp01(0.50f + 0.50f * Mathf.Sin(t * tQi + 3.1f));
                danger = Mathf.Clamp01(0.50f + 0.50f * Mathf.Sin(t * tDanger + 0.6f));
                enemyDistance = Mathf.Clamp(15f + 15f * Mathf.Sin(t * tEnemy + 2.2f), 0f, 30f);
            }

            brain.SetFloat("Health", health);
            brain.SetFloat("Mana", mana);
            brain.SetFloat("Qi", qi);
            brain.SetFloat("Danger", danger);
            brain.SetFloat("EnemyDistance", enemyDistance);
        }
    }
}

