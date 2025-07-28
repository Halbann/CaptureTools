using System;
using System.IO;
using UnityEngine;

namespace CaptureTools.UI
{
    public class SaveAs
    {
        public string Text => textTrimmed;
        public bool valid = true;
        public Func<string, bool> validity;
        public Action<string> onSave;
        public Action onCancel;
        public bool complete = false;
        
        private string text = "";
        private string textTrimmed;

        public SaveAs(Action<string> onSave, Action onCancel, string initialText = "", Func<string, bool> validity = null)
        {
            text = initialText;
            this.validity = validity;
            this.onSave = onSave;
            this.onCancel = onCancel;
        }

        public void Update()
        {
            GUILayout.BeginHorizontal();

            Color guiColour = GUI.color;
            if (!valid)
                GUI.color = Color.red;

            text = GUILayout.TextField(text);
            textTrimmed = text.Trim();

            if (!valid)
                GUI.color = guiColour;

            valid = !string.IsNullOrEmpty(textTrimmed) && textTrimmed.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            if (validity != null)
                valid = valid && validity(textTrimmed);

            GUIEnabled.Push(valid);
            if ((GUILayout.Button("Save", GUILayout.Width(ContentSizeCache.Size("Save", CaptureTools.buttonStyle))) || Input.GetKey(KeyCode.KeypadEnter)) && valid)
            {
                complete = true;
                onSave?.Invoke(textTrimmed);
            }
            GUIEnabled.Pop();

            if (GUILayout.Button("Cancel", GUILayout.Width(ContentSizeCache.Size("Cancel", CaptureTools.buttonStyle))) || Input.GetKey(KeyCode.Escape))
            {
                complete = true;
                onCancel?.Invoke();
            }

            GUILayout.EndHorizontal();
        }
    }
}
