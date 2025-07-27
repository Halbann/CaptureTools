using System;
using System.IO;
using UnityEngine;

namespace CaptureTools.UI
{
    public class SaveAs
    {
        public bool valid = true;
        public string text = "";
        public Func<string, bool> validity;
        public Action onSave;
        public Action onCancel;
        public bool complete = false;

        public SaveAs(Action onSave, Action onCancel, string initialText = "", Func<string, bool> validity = null)
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

            if (!valid)
                GUI.color = guiColour;

            valid = !string.IsNullOrEmpty(text) && text.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            if (validity != null)
                valid = valid && validity(text);

            GUIEnabled.Push(valid);
            if ((GUILayout.Button("Save", GUILayout.Width(ContentSizeCache.Size("Save", CaptureTools.buttonStyle))) || Input.GetKey(KeyCode.KeypadEnter)) && valid)
            {
                complete = true;
                onSave?.Invoke();
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
