using System;
using UnityEngine;

namespace UGS.FlexUtilityAI
{
    public static class UtilityAIScorer
    {
        public static UtilityAIActionScoreSnapshot EvaluateAction(
            UtilityAIRuntimeAction action,
            UtilityAIBlackboard blackboard,
            UtilityAIAggregationMode aggregationMode,
            bool includeBreakdown,
            bool useResponseCurves = true)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (blackboard == null) throw new ArgumentNullException(nameof(blackboard));

            var snapshot = includeBreakdown ? new UtilityAIActionScoreSnapshot() : null;
            if (snapshot != null)
            {
                snapshot.ActionId = action.Id;
                snapshot.Weight = action.Weight;
            }

            float score;

            if (aggregationMode == UtilityAIAggregationMode.WeightedAverage)
            {
                score = EvaluateWeightedAverage(action, blackboard, snapshot, useResponseCurves);
            }
            else
            {
                score = EvaluateProduct(action, blackboard, snapshot, useResponseCurves);
            }

            score *= Mathf.Max(0f, action.Weight);

            if (snapshot != null)
            {
                snapshot.Score = score;
            }

            return snapshot ?? new UtilityAIActionScoreSnapshot { ActionId = action.Id, Score = score, Weight = action.Weight };
        }

        private static float EvaluateProduct(UtilityAIRuntimeAction action, UtilityAIBlackboard blackboard,
            UtilityAIActionScoreSnapshot snapshot, bool useResponseCurves)
        {
            float product = 1f;
            for (int i = 0; i < action.Considerations.Count; i++)
            {
                UtilityAIRuntimeConsideration consideration = action.Considerations[i];
                float weighted = EvaluateConsiderationWeighted(consideration, blackboard, snapshot, useResponseCurves);
                if (consideration.UseFailThreshold && weighted < consideration.FailIfBelow)
                {
                    product = 0f;
                    break;
                }

                product *= weighted;
            }

            return Mathf.Clamp01(product);
        }

        private static float EvaluateWeightedAverage(UtilityAIRuntimeAction action, UtilityAIBlackboard blackboard,
            UtilityAIActionScoreSnapshot snapshot, bool useResponseCurves)
        {
            float sum = 0f;
            float weightSum = 0f;
            for (int i = 0; i < action.Considerations.Count; i++)
            {
                UtilityAIRuntimeConsideration consideration = action.Considerations[i];
                float weighted = EvaluateConsiderationWeighted(consideration, blackboard, snapshot, useResponseCurves);
                if (consideration.UseFailThreshold && weighted < consideration.FailIfBelow)
                {
                    sum = 0f;
                    weightSum = 1f;
                    break;
                }

                float w = Mathf.Max(0f, consideration.Weight);
                sum += weighted * w;
                weightSum += w;
            }

            if (weightSum <= 0f) return 0f;
            return Mathf.Clamp01(sum / weightSum);
        }

        private static float EvaluateConsiderationWeighted(UtilityAIRuntimeConsideration consideration,
            UtilityAIBlackboard blackboard, UtilityAIActionScoreSnapshot actionSnapshot, bool useResponseCurves)
        {
            float raw = blackboard.GetFloatOrDefault(consideration.BlackboardFloatKey, 0f);

            float normalized;
            if (Mathf.Approximately(consideration.InputMin, consideration.InputMax))
            {
                normalized = 0f;
            }
            else
            {
                normalized = Mathf.InverseLerp(consideration.InputMin, consideration.InputMax, raw);
                normalized = Mathf.Clamp01(normalized);
            }

            // 后台线程不能调用 AnimationCurve.Evaluate；关闭曲线时退化为线性（normalized）。
            float responseValue;
            if (!useResponseCurves || consideration.Response == null)
            {
                responseValue = normalized;
            }
            else
            {
                responseValue = consideration.Response.Evaluate(normalized);
            }

            responseValue = Mathf.Clamp01(responseValue);

            if (consideration.Invert)
            {
                responseValue = 1f - responseValue;
            }

            float weight01 = NormalizeWeight01(consideration.Weight);
            float weightedValue = Mathf.Lerp(1f, responseValue, weight01);
            weightedValue = Mathf.Clamp01(weightedValue);

            if (actionSnapshot != null)
            {
                var c = new UtilityAIConsiderationScoreSnapshot
                {
                    ConsiderationId = consideration.Id,
                    BlackboardFloatKey = consideration.BlackboardFloatKey,
                    RawValue = raw,
                    NormalizedValue = normalized,
                    ResponseValue = responseValue,
                    WeightedValue = weightedValue,
                    Weight = consideration.Weight,
                    Invert = consideration.Invert,
                    Failed = consideration.UseFailThreshold && weightedValue < consideration.FailIfBelow
                };

                actionSnapshot.Considerations.Add(c);
            }

            return weightedValue;
        }

        private static float NormalizeWeight01(float weight)
        {
            if (weight <= 0f) return 0f;
            return weight >= 1f ? 1f : weight;
        }
    }
}
