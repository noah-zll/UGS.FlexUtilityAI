using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UGS.FlexUtilityAI;

namespace UGS.FlexUtilityAI.Editor
{
    [CustomEditor(typeof(UtilityAIBrain))]
    public sealed class UtilityAIBrainEditor : UnityEditor.Editor
    {
        private static readonly List<UtilityAIActionScoreSnapshot> _sorted = new List<UtilityAIActionScoreSnapshot>();
        private bool _showHistory;
        private int _selectedHistoryIndex = -1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var brain = (UtilityAIBrain)target;
            UtilityAIDecisionSnapshot snapshot = brain.LastDecisionSnapshot;
            var history = brain.DecisionHistory;
            if (history != null && history.Count > 0)
            {
                EditorGUILayout.Space(8f);
                _showHistory = EditorGUILayout.Foldout(_showHistory, "Decision History", true);
                if (_showHistory)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Use Latest"))
                        {
                            _selectedHistoryIndex = -1;
                        }

                        using (new EditorGUI.DisabledScope(!Application.isPlaying))
                        {
                            if (GUILayout.Button("Clear"))
                            {
                                brain.ClearDecisionHistory();
                                _selectedHistoryIndex = -1;
                                EditorUtility.SetDirty(brain);
                            }
                        }
                    }

                    int max = history.Count - 1;
                    int idx = _selectedHistoryIndex < 0 ? max : Mathf.Clamp(_selectedHistoryIndex, 0, max);
                    idx = EditorGUILayout.IntSlider("Index", idx, 0, max);
                    _selectedHistoryIndex = idx;
                    snapshot = history[idx];
                }
            }

            if (snapshot == null) return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Decision Debug", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ModelVersion", snapshot.ModelVersion.ToString());
            EditorGUILayout.LabelField("Time", snapshot.Time.ToString("F3"));
            EditorGUILayout.LabelField("Current", $"{snapshot.CurrentActionId} ({snapshot.CurrentScore:F3})");
            EditorGUILayout.LabelField("Best", $"{snapshot.BestActionId} ({snapshot.BestScore:F3})");
            EditorGUILayout.LabelField("Reason", snapshot.SwitchReason);

            _sorted.Clear();
            if (snapshot.Actions != null) _sorted.AddRange(snapshot.Actions);
            _sorted.Sort((a, b) => b.Score.CompareTo(a.Score));

            EditorGUILayout.Space(4f);
            for (int i = 0; i < _sorted.Count; i++)
            {
                UtilityAIActionScoreSnapshot a = _sorted[i];
                string head = $"{a.ActionId}  score={a.Score:F3}  eligible={(a.Eligible ? "Y" : "N")}";
                if (!a.Eligible && !string.IsNullOrEmpty(a.IneligibleReason)) head += $"  reason={a.IneligibleReason}";

                EditorGUILayout.LabelField(head, EditorStyles.miniLabel);

                if (a.Considerations == null || a.Considerations.Count == 0) continue;

                EditorGUI.indentLevel++;
                for (int c = 0; c < a.Considerations.Count; c++)
                {
                    UtilityAIConsiderationScoreSnapshot cs = a.Considerations[c];
                    string line =
                        $"{cs.ConsiderationId}  key={cs.BlackboardFloatKey}  raw={cs.RawValue:F3}  n={cs.NormalizedValue:F3}  r={cs.ResponseValue:F3}  w={cs.WeightedValue:F3}";
                    if (cs.Failed) line += "  failed=Y";
                    EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Evaluate Now"))
                {
                    brain.EvaluateOnce();
                    EditorUtility.SetDirty(brain);
                }
            }
        }
    }
}
