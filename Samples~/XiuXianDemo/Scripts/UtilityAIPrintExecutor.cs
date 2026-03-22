using UnityEngine;

namespace UGS.FlexUtilityAI.Samples
{
    public sealed class UtilityAIPrintExecutor : UtilityAIActionExecutor
    {
        [SerializeField] private bool logTick;
        [SerializeField] private float tickLogIntervalSeconds = 0.5f;

        private float _nextTickLogTime;

        public override void OnEnter(UtilityAIBrain brain)
        {
            Debug.Log($"[UtilityAI] Enter {ActionId}", brain);
            _nextTickLogTime = 0f;
        }

        public override void OnExit(UtilityAIBrain brain)
        {
            Debug.Log($"[UtilityAI] Exit {ActionId}", brain);
        }

        public override void OnTick(UtilityAIBrain brain, float deltaTime)
        {
            if (!logTick) return;

            float now = Time.time;
            if (now < _nextTickLogTime) return;
            _nextTickLogTime = now + Mathf.Max(0f, tickLogIntervalSeconds);

            Debug.Log($"[UtilityAI] Tick {ActionId} score={brain.CurrentScore:F3}", brain);
        }
    }
}

