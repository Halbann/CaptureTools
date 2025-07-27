using System.Collections.Generic;
using UnityEngine;

namespace CaptureTools.UI
{
    public static class ContentSizeCache
    {
        private static Dictionary<string, float> contentSizes = new Dictionary<string, float>();

        public static float Size(string content, GUIStyle style)
        {
            if (contentSizes.TryGetValue(content, out float size))
                return size;

            size = style.CalcSize(new GUIContent(content)).x;
            contentSizes[content] = size;

            return size;
        }
    }
}
