using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CaptureTools.Utils
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class HighlightBlocker : MonoBehaviour
    {
        private static int blockCounter = 0;
        private static bool blocked = false;
        private static bool defaultHighlightState;

        public static bool Blocked
        {
            get => blocked;
            private set
            {
                if (blocked == value)
                    return;

                if (!blocked)
                    defaultHighlightState = GameSettings.INFLIGHT_HIGHLIGHT;

                blocked = value;
                bool highlightSetting = !blocked && defaultHighlightState;

                if (GameSettings.INFLIGHT_HIGHLIGHT == highlightSetting)
                    return;

                if (!highlightSetting)
                {
                    // Reset all current highlights.
                    IEnumerable<Part> parts = HighLogic.LoadedSceneIsFlight ? FlightGlobals.VesselsLoaded.SelectMany(v => v.parts) : EditorLogic.SortedShipList;
                    foreach (Part part in parts)
                        part.SetHighlight(false, false);
                }

                GameSettings.INFLIGHT_HIGHLIGHT = highlightSetting;
            }
        }

        protected void OnDestroy()
        {
            blockCounter = 0;

            if (Blocked)
                GameSettings.INFLIGHT_HIGHLIGHT = defaultHighlightState;
        }

        public static void Push()
        {
            blockCounter++;
            Blocked = true;
        }

        public static void Pop()
        {
            blockCounter = Mathf.Max(blockCounter - 1, 0);
            if (blockCounter == 0)
                Blocked = false;
        }
    }
}
