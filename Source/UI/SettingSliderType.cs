using System;
using UnityEngine;

namespace CaptureTools.UI
{
    public class SettingSliderType
    {
        public string name = string.Empty;
        public float min = 0f;
        public float max = 1f;
        public int rounding = 2;
        public bool softClamp = false;
        public bool useToggle = false;

        public static float sliderWidth = 220f;

        private string text;
        private float sliderMin;
        private float sliderMax;

        public float Update(float setting)
        {
            bool update = false;
            return Update(setting, false, ref update).setting;
        }

        public (float setting, bool toggle) Update(float setting, bool toggle)
        {
            bool update = false;
            return Update(setting, toggle, ref update);
        }

        public void Update(ref float setting, ref bool toggle)
        {
            (setting, toggle) = Update(setting, toggle);
        }

        public (float setting, bool toggle) Update(float setting, bool toggle, ref bool updated)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(3);

            // Toggle.
            if (useToggle)
            {
                toggle = GUILayout.Toggle(toggle, "", GUILayout.Width(12));
                bool guiEnabled = GUI.enabled;

                if (!toggle)
                    GUIEnabled.Push(false);
            }

            // Label.
            if (name != "")
                GUILayout.Label(name);

            // Slider
            sliderMin = !softClamp ? min : Mathf.Min(setting, min);
            sliderMax = !softClamp ? max : Mathf.Max(setting, max);
            setting = (float)Math.Round(GUILayout.HorizontalSlider(setting, sliderMin, sliderMax, GUILayout.Width(sliderWidth)), rounding);

            // Box
            text = GUILayout.TextField(setting.ToString("N" + rounding.ToString()), CaptureTools.textBoxStyle, GUILayout.Width(38));
            if (float.TryParse(text, out float result))
                setting = result;
            else if (text == "")
                setting = 0;

            // Toggle GUI enabled reset.
            if (useToggle && !toggle)
                GUIEnabled.Pop();

            GUILayout.Space(3);
            GUILayout.EndHorizontal();

            return (setting, toggle);
        }
    }
}
