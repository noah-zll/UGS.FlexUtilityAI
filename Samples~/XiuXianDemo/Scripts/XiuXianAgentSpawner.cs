using UnityEngine;

namespace UGS.FlexUtilityAI.Samples
{
    public sealed class XiuXianAgentSpawner : MonoBehaviour
    {
        [SerializeField] private int count = 6;
        [SerializeField] private float spacing = 2.5f;
        [SerializeField] private bool addHudToFirstAgent = true;

        private void Start()
        {
            int n = Mathf.Clamp(count, 1, 64);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"XiuXianAgent_{i:00}");
                go.transform.position = transform.position + new Vector3(i * spacing, 0f, 0f);

                var brain = go.AddComponent<UtilityAIBrain>();
                var bootstrap = go.AddComponent<XiuXianComplexDemoBootstrap>();
                var needs = go.AddComponent<XiuXianAgentNeedsAndEffects>();
                var scenario = go.AddComponent<XiuXianScenarioCurves>();
                var mutator = go.AddComponent<XiuXianRuntimeModelMutator>();

                if (i == 0 && addHudToFirstAgent)
                {
                    go.AddComponent<UtilityAIHUDOnGUI>();
                }

                bootstrap.SendMessage("Reset", SendMessageOptions.DontRequireReceiver);
                needs.SendMessage("Reset", SendMessageOptions.DontRequireReceiver);
                scenario.SendMessage("Reset", SendMessageOptions.DontRequireReceiver);
                mutator.SendMessage("Reset", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}

