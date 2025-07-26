namespace CaptureTools
{
    public static class ConfigNodeExtensions
    {
        public static bool TryGetValue(this ConfigNode node, string name, Constrained value)
        {
            string value2 = node.GetValue(name);
            if (value2 != null && float.TryParse(value2, out var result))
            {
                value.Value = result;
                return true;
            }
            return false;
        }
    }
}
