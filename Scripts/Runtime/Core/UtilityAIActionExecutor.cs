using UnityEngine;

namespace UGS.FlexUtilityAI
{
    public abstract class UtilityAIActionExecutor : MonoBehaviour, IUtilityAIActionExecutor
    {
        [SerializeField] private string actionId;

        public string ActionId => actionId;

        public virtual void OnEnter(UtilityAIBrain brain)
        {
        }

        public virtual void OnExit(UtilityAIBrain brain)
        {
        }

        public virtual void OnTick(UtilityAIBrain brain, float deltaTime)
        {
        }
    }
}

