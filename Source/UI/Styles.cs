using UnityEngine;

namespace CaptureTools.UI
{
    public static class Styles
    {
        private static bool initStyles = false;
        public static GUIStyle boxStyle;
        public static GUIStyle textBoxStyle;
        public static GUIStyle buttonStyle;
        public static GUIStyle smallTextButtonStyle;

        public static void Init()
        {
            if (initStyles)
                return;

            initStyles = true;

            boxStyle = GUI.skin.GetStyle("Box");

            textBoxStyle = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleCenter
            };

            buttonStyle = GUI.skin.button;

            smallTextButtonStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight
            };
        }
    }
}
