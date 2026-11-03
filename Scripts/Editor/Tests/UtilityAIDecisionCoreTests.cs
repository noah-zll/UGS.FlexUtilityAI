using NUnit.Framework;
using UnityEngine;

namespace UGS.FlexUtilityAI.Tests
{
    public sealed class UtilityAIDecisionCoreTests
    {
        private static UtilityAIRuntimeAction AddAction(
            UtilityAIRuntimeModel model,
            string id,
            string blackboardKey,
            float weight = 1f)
        {
            UtilityAIRuntimeAction action = model.AddAction(id);
            UtilityAIRuntimeConsideration consideration = action.AddConsideration("C");
            consideration.BlackboardFloatKey = blackboardKey;
            consideration.InputMin = 0f;
            consideration.InputMax = 1f;
            consideration.Response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            consideration.Weight = weight;
            return action;
        }

        private static UtilityAIDecisionSettings Settings(
            float minTimeInState = 0f,
            float bias = 0f,
            float delta = 0f,
            bool captureSnapshot = false,
            bool includeBreakdown = false)
        {
            return new UtilityAIDecisionSettings(minTimeInState, bias, delta, captureSnapshot, includeBreakdown);
        }

        [Test]
        public void Evaluate_NoModel_ReturnsNoModelOrNoActions()
        {
            var core = new UtilityAIDecisionCore();
            var blackboard = new UtilityAIBlackboard();

            UtilityAIDecisionResult result = core.Evaluate(null, blackboard, 0d, Settings());

            Assert.AreEqual("NoModelOrNoActions", result.SwitchReason);
            Assert.IsNull(result.CurrentActionId);
            Assert.IsFalse(result.Changed);
        }

        [Test]
        public void Evaluate_FirstTime_AdoptsBestAction()
        {
            UtilityAIRuntimeModel model = UtilityAIRuntimeModel.CreateEmpty(UtilityAIAggregationMode.Product);
            AddAction(model, "A", "ScoreA");
            AddAction(model, "B", "ScoreB");

            var blackboard = new UtilityAIBlackboard();
            blackboard.SetFloat("ScoreA", 1f);
            blackboard.SetFloat("ScoreB", 0.5f);

            var core = new UtilityAIDecisionCore();
            UtilityAIDecisionResult result = core.Evaluate(model, blackboard, 0d, Settings());

            Assert.AreEqual("A", result.CurrentActionId);
            Assert.AreEqual("NoCurrentAction", result.SwitchReason);
            Assert.IsTrue(result.Changed);
            Assert.AreEqual(1f, result.CurrentScore, 0.0001f);
        }

        [Test]
        public void Evaluate_MinTimeInState_PinsCurrentAction()
        {
            UtilityAIRuntimeModel model = UtilityAIRuntimeModel.CreateEmpty(UtilityAIAggregationMode.Product);
            AddAction(model, "A", "ScoreA");
            AddAction(model, "B", "ScoreB");

            var blackboard = new UtilityAIBlackboard();
            blackboard.SetFloat("ScoreA", 1f);
            blackboard.SetFloat("ScoreB", 0.1f);

            var core = new UtilityAIDecisionCore();
            core.Evaluate(model, blackboard, 0d, Settings(minTimeInState: 1f));

            blackboard.SetFloat("ScoreA", 0.1f);
            blackboard.SetFloat("ScoreB", 1f);

            UtilityAIDecisionResult result = core.Evaluate(model, blackboard, 0.5d, Settings(minTimeInState: 1f));

            Assert.AreEqual("A", result.CurrentActionId);
            Assert.AreEqual("PinnedByMinSwitchInterval", result.SwitchReason);
            Assert.IsFalse(result.Changed);
        }

        [Test]
        public void Evaluate_SwitchBias_KeepsCurrentUntilThresholdPassed()
        {
            UtilityAIRuntimeModel model = UtilityAIRuntimeModel.CreateEmpty(UtilityAIAggregationMode.Product);
            AddAction(model, "A", "ScoreA");
            AddAction(model, "B", "ScoreB");

            var blackboard = new UtilityAIBlackboard();
            blackboard.SetFloat("ScoreA", 0.9f);
            blackboard.SetFloat("ScoreB", 1f);

            var core = new UtilityAIDecisionCore();
            core.Evaluate(model, blackboard, 0d, Settings(bias: 0.5f));

            UtilityAIDecisionResult pinnedByBias = core.Evaluate(model, blackboard, 1d, Settings(bias: 0.5f));
            Assert.AreEqual("A", pinnedByBias.CurrentActionId);
            Assert.AreEqual("KeepCurrent", pinnedByBias.SwitchReason);
            Assert.IsFalse(pinnedByBias.Changed);

            UtilityAIDecisionResult switched = core.Evaluate(model, blackboard, 2d, Settings(bias: 0f));
            Assert.AreEqual("B", switched.CurrentActionId);
            Assert.IsTrue(switched.Changed);
        }

        [Test]
        public void Evaluate_Cooldown_MakesActionIneligible()
        {
            UtilityAIRuntimeModel model = UtilityAIRuntimeModel.CreateEmpty(UtilityAIAggregationMode.Product);
            AddAction(model, "A", "ScoreA");
            UtilityAIRuntimeAction b = AddAction(model, "B", "ScoreB");
            b.CooldownSeconds = 10f;
            b.LastSelectedTime = 0f;

            var blackboard = new UtilityAIBlackboard();
            blackboard.SetFloat("ScoreA", 0.1f);
            blackboard.SetFloat("ScoreB", 1f);

            var core = new UtilityAIDecisionCore();
            core.SetCurrentState("A", 0.1f, 0d, 0d);

            UtilityAIDecisionResult result = core.Evaluate(model, blackboard, 1d,
                Settings(captureSnapshot: true));

            Assert.AreEqual("A", result.CurrentActionId);
            Assert.IsFalse(result.Changed);
            UtilityAIActionScoreSnapshot bSnapshot = result.Snapshot.Actions.Find(s => s.ActionId == "B");
            Assert.IsNotNull(bSnapshot);
            Assert.IsFalse(bSnapshot.Eligible);
            Assert.AreEqual("Cooldown", bSnapshot.IneligibleReason);
        }

        [Test]
        public void ResetCurrentAction_AllowsSameActionToBeAdoptedAgain()
        {
            UtilityAIRuntimeModel model = UtilityAIRuntimeModel.CreateEmpty(UtilityAIAggregationMode.Product);
            AddAction(model, "A", "ScoreA");

            var blackboard = new UtilityAIBlackboard();
            blackboard.SetFloat("ScoreA", 1f);

            var core = new UtilityAIDecisionCore();
            core.Evaluate(model, blackboard, 0d, Settings());
            Assert.AreEqual("A", core.CurrentActionId);

            core.ResetCurrentAction();
            Assert.IsNull(core.CurrentActionId);

            UtilityAIDecisionResult result = core.Evaluate(model, blackboard, 1d, Settings());
            Assert.AreEqual("A", result.CurrentActionId);
            Assert.AreEqual("NoCurrentAction", result.SwitchReason);
            Assert.IsTrue(result.Changed);
        }

        [Test]
        public void Evaluate_CaptureSnapshot_IncludesBreakdown()
        {
            UtilityAIRuntimeModel model = UtilityAIRuntimeModel.CreateEmpty(UtilityAIAggregationMode.Product);
            AddAction(model, "A", "ScoreA");

            var blackboard = new UtilityAIBlackboard();
            blackboard.SetFloat("ScoreA", 0.7f);

            var core = new UtilityAIDecisionCore();
            UtilityAIDecisionResult result = core.Evaluate(model, blackboard, 0d,
                Settings(captureSnapshot: true, includeBreakdown: true));

            Assert.IsNotNull(result.Snapshot);
            Assert.AreEqual(1, result.Snapshot.Actions.Count);
            Assert.AreEqual(1, result.Snapshot.Actions[0].Considerations.Count);
            Assert.AreEqual(0.7f, result.Snapshot.Actions[0].Considerations[0].WeightedValue, 0.0001f);
        }
    }
}
