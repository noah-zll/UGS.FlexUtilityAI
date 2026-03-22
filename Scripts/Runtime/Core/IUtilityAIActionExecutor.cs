namespace UGS.FlexUtilityAI
{
    public interface IUtilityAIActionExecutor
    {
        string ActionId { get; }

        void OnEnter(UtilityAIBrain brain);
        void OnExit(UtilityAIBrain brain);
        void OnTick(UtilityAIBrain brain, float deltaTime);
    }
}

