using System;
using System.Collections.Generic;
using UnityEngine;

namespace UGS.FlexUtilityAI.Samples
{
    public sealed class UtilityAIHUDOnGUI : MonoBehaviour
    {
        [SerializeField] private UtilityAIBrain brain;
        [SerializeField] private bool show = true;
        [SerializeField] private bool showTopActions = true;
        [SerializeField] private int topActionsCount = 6;
        [SerializeField] private bool showKeyBlackboardValues = true;
        [SerializeField] private bool showLastReason = true;
        [SerializeField] private float width = 420f;

        private readonly List<UtilityAIActionScoreSnapshot> _sorted = new List<UtilityAIActionScoreSnapshot>(32);

        private void Reset()
        {
            brain = GetComponent<UtilityAIBrain>();
        }

        private void OnGUI()
        {
            if (!show) return;
            if (brain == null) return;

            var snapshot = brain.LastDecisionSnapshot;
            GUILayout.BeginArea(new Rect(10f, 10f, Mathf.Max(200f, width), 1000f), GUI.skin.box);

            GUILayout.Label("UtilityAI Debug");
            GUILayout.Label($"Current: {brain.CurrentActionId ?? "(none)"}  score={brain.CurrentScore:F3}");

            if (snapshot != null)
            {
                GUILayout.Label($"Best: {snapshot.BestActionId ?? "(none)"}  score={snapshot.BestScore:F3}");
                if (showLastReason && !string.IsNullOrEmpty(snapshot.SwitchReason))
                {
                    GUILayout.Label($"Reason: {snapshot.SwitchReason}");
                }
            }

            if (showKeyBlackboardValues)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Blackboard");
                DrawFloat01("Health");
                DrawFloat01("Mana");
                DrawFloat01("Qi");
                DrawFloat01("Hunger");
                DrawFloat01("Fatigue");
                DrawFloat01("RealmProgress");
                DrawFloat01("Danger");
                DrawFloat("EnemyDistance");
                DrawFloat01("HasHerb");
                DrawFloat01("HasPill");
                DrawFloat01("WeatherBad");
            }

            if (showTopActions && snapshot != null && snapshot.Actions != null && snapshot.Actions.Count > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Top Actions");

                _sorted.Clear();
                for (int i = 0; i < snapshot.Actions.Count; i++)
                {
                    _sorted.Add(snapshot.Actions[i]);
                }

                _sorted.Sort((a, b) => b.Score.CompareTo(a.Score));

                int count = Mathf.Clamp(topActionsCount, 1, _sorted.Count);
                for (int i = 0; i < count; i++)
                {
                    UtilityAIActionScoreSnapshot a = _sorted[i];
                    string eligible = a.Eligible ? "" : $"  ({a.IneligibleReason ?? "Ineligible"})";
                    GUILayout.Label($"{i + 1}. {a.ActionId}  {a.Score:F3}{eligible}");
                }
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear History", GUILayout.Width(110f)))
            {
                brain.ClearDecisionHistory();
            }
            showTopActions = GUILayout.Toggle(showTopActions, "Top", GUILayout.Width(60f));
            showKeyBlackboardValues = GUILayout.Toggle(showKeyBlackboardValues, "Blackboard", GUILayout.Width(110f));
            showLastReason = GUILayout.Toggle(showLastReason, "Reason", GUILayout.Width(80f));
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawFloat01(string key)
        {
            float v = Mathf.Clamp01(brain.Blackboard.GetFloatOrDefault(key, 0f));
            GUILayout.Label($"{key}: {v:F2}");
        }

        private void DrawFloat(string key)
        {
            float v = brain.Blackboard.GetFloatOrDefault(key, 0f);
            GUILayout.Label($"{key}: {v:F2}");
        }
    }
}

