using System.Collections.Generic;
using UnityEngine;

namespace CaptureTools.UI
{
    public static class GUIEnabled
    {
        private static readonly Stack<bool> guiState = new Stack<bool>();

        public static void Push(bool state)
        {
            guiState.Push(GUI.enabled);
            GUI.enabled = GUI.enabled && state;
        }

        public static void Pop() =>
            GUI.enabled = guiState.Pop();
    }
}
