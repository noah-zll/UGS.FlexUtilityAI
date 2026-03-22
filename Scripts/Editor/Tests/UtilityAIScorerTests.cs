using NUnit.Framework;
using UnityEngine;

namespace UGS.FlexUtilityAI.Tests
{
    public sealed class UtilityAIScorerTests
    {
        [Test]
        public void EvaluateAction_Product_MultipliesWeightedConsiderations()
        {
            var blackboard = new UtilityAIBlackboard();
            blackboard.SetFloat("A", 0.5f);
            blackboard.SetFloat("B", 0.5f);

            var action = new UtilityAIRuntimeAction("Test");

            var c1 = action.AddConsideration("C1");
            c1.BlackboardFloatKey = "A";
            c1.InputMin = 0f;
            c1.InputMax = 1f;
            c1.Response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            c1.Weight = 1f;

            var c2 = action.AddConsideration("C2");
            c2.BlackboardFloatKey = "B";
            c2.InputMin = 0f;
            c2.InputMax = 1f;
            c2.Response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            c2.Weight = 1f;

            UtilityAIActionScoreSnapshot snapshot = UtilityAIScorer.EvaluateAction(action, blackboard, UtilityAIAggregationMode.Product, includeBreakdown: true);

            Assert.AreEqual(0.25f, snapshot.Score, 0.0001f);
            Assert.AreEqual(2, snapshot.Considerations.Count);
        }

        [Test]
        public void EvaluateAction_Product_WeightZeroDoesNotAffectScore()
        {
            var blackboard = new UtilityAIBlackboard();
            blackboard.SetFloat("A", 0.2f);
            blackboard.SetFloat("B", 0.8f);

            var action = new UtilityAIRuntimeAction("Test");

            var c1 = action.AddConsideration("C1");
            c1.BlackboardFloatKey = "A";
            c1.InputMin = 0f;
            c1.InputMax = 1f;
            c1.Response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            c1.Weight = 1f;

            var c2 = action.AddConsideration("C2");
            c2.BlackboardFloatKey = "B";
            c2.InputMin = 0f;
            c2.InputMax = 1f;
            c2.Response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            c2.Weight = 0f;

            UtilityAIActionScoreSnapshot snapshot = UtilityAIScorer.EvaluateAction(action, blackboard, UtilityAIAggregationMode.Product, includeBreakdown: true);

            Assert.AreEqual(0.2f, snapshot.Score, 0.0001f);
        }

        [Test]
        public void EvaluateAction_Product_FailThresholdZerosOutScore()
        {
            var blackboard = new UtilityAIBlackboard();
            blackboard.SetFloat("A", 0.5f);

            var action = new UtilityAIRuntimeAction("Test");

            var c1 = action.AddConsideration("C1");
            c1.BlackboardFloatKey = "A";
            c1.InputMin = 0f;
            c1.InputMax = 1f;
            c1.Response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            c1.Weight = 1f;
            c1.UseFailThreshold = true;
            c1.FailIfBelow = 0.8f;

            UtilityAIActionScoreSnapshot snapshot = UtilityAIScorer.EvaluateAction(action, blackboard, UtilityAIAggregationMode.Product, includeBreakdown: true);

            Assert.AreEqual(0f, snapshot.Score, 0.0001f);
            Assert.IsTrue(snapshot.Considerations[0].Failed);
        }

        [Test]
        public void EvaluateAction_WeightedAverage_AveragesByConsiderationWeight()
        {
            var blackboard = new UtilityAIBlackboard();
            blackboard.SetFloat("A", 0.2f);
            blackboard.SetFloat("B", 0.8f);

            var action = new UtilityAIRuntimeAction("Test");

            var c1 = action.AddConsideration("C1");
            c1.BlackboardFloatKey = "A";
            c1.InputMin = 0f;
            c1.InputMax = 1f;
            c1.Response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            c1.Weight = 1f;

            var c2 = action.AddConsideration("C2");
            c2.BlackboardFloatKey = "B";
            c2.InputMin = 0f;
            c2.InputMax = 1f;
            c2.Response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            c2.Weight = 1f;

            UtilityAIActionScoreSnapshot snapshot = UtilityAIScorer.EvaluateAction(action, blackboard, UtilityAIAggregationMode.WeightedAverage, includeBreakdown: true);

            Assert.AreEqual(0.5f, snapshot.Score, 0.0001f);
        }
    }
}

