using System.Collections.Generic;

namespace UGS.FlexUtilityAI
{
    public sealed class UtilityAIBlackboard
    {
        private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();

        public void SetFloat(string key, float value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _floats[key] = value;
        }

        public bool TryGetFloat(string key, out float value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = default;
                return false;
            }

            return _floats.TryGetValue(key, out value);
        }

        public float GetFloatOrDefault(string key, float defaultValue = 0f)
        {
            if (TryGetFloat(key, out float value)) return value;
            return defaultValue;
        }

        public bool RemoveFloat(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _floats.Remove(key);
        }

        public void Clear()
        {
            _floats.Clear();
        }
    }
}

