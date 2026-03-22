using System;
using System.Collections.Generic;

namespace UGS.FlexUtilityAI
{
    public sealed class UtilityAIDecisionSnapshot
    {
        public double Time { get; set; }
        public string CurrentActionId { get; set; }
        public float CurrentScore { get; set; }
        public string BestActionId { get; set; }
        public float BestScore { get; set; }
        public string SwitchReason { get; set; }
        public int ModelVersion { get; set; }
        public List<UtilityAIActionScoreSnapshot> Actions { get; set; } = new List<UtilityAIActionScoreSnapshot>();
    }

    public sealed class UtilityAIActionScoreSnapshot
    {
        public string ActionId { get; set; }
        public bool Eligible { get; set; }
        public string IneligibleReason { get; set; }
        public float Score { get; set; }
        public float Weight { get; set; }
        public List<UtilityAIConsiderationScoreSnapshot> Considerations { get; set; } = new List<UtilityAIConsiderationScoreSnapshot>();
    }

    public sealed class UtilityAIConsiderationScoreSnapshot
    {
        public string ConsiderationId { get; set; }
        public string BlackboardFloatKey { get; set; }
        public float RawValue { get; set; }
        public float NormalizedValue { get; set; }
        public float ResponseValue { get; set; }
        public float WeightedValue { get; set; }
        public float Weight { get; set; }
        public bool Invert { get; set; }
        public bool Failed { get; set; }
    }

    public sealed class UtilityAIActionChangedEventArgs : EventArgs
    {
        public string PreviousActionId { get; }
        public string CurrentActionId { get; }
        public float CurrentScore { get; }
        public double Time { get; }

        public UtilityAIActionChangedEventArgs(string previousActionId, string currentActionId, float currentScore, double time)
        {
            PreviousActionId = previousActionId;
            CurrentActionId = currentActionId;
            CurrentScore = currentScore;
            Time = time;
        }
    }
}

