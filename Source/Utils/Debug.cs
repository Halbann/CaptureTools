using System;
using UnityEngine;

namespace CaptureTools.Utils
{
    public static class CTDebug
    {
        public static bool draw = false;
        public static void Log(string msg) =>
            Debug.Log($"[{Meta.name}]: {msg}");

        public static void LogError(string msg) =>
            Debug.LogError($"[{Meta.name}]: {msg}");

        public static void LogWarning(string msg) =>
            Debug.LogWarning($"[{Meta.name}]: {msg}");
    }

    public class DrawTransform : MonoBehaviour
    {
        public static bool drawTransforms = false;
        private bool drawEnabled = false;

        public float scale = 2f;
        public string text = "";

        private LineRenderer xLine;
        private LineRenderer yLine;
        private LineRenderer zLine;

        protected void Start()
        {
            if (!drawTransforms)
            {
                Destroy(this);
                return;
            }

            xLine = new GameObject().AddComponent<LineRenderer>();
            yLine = new GameObject().AddComponent<LineRenderer>();
            zLine = new GameObject().AddComponent<LineRenderer>();

            SetupLine(xLine, Color.red);
            SetupLine(yLine, Color.green);
            SetupLine(zLine, Color.blue);

            xLine.enabled = drawEnabled;
            yLine.enabled = drawEnabled;
            zLine.enabled = drawEnabled;
        }

        protected void OnGUI()
        {
            if (drawTransforms && Event.current.type.Equals(EventType.Repaint))
            {
                // Draw text with GUI.label over the transform position.

                Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
                screenPos.y = Screen.height - screenPos.y;

                GUI.Label(new Rect(screenPos.x, screenPos.y, 100, 100), text);
            }
        }

        // Update is called once per frame
        protected void Update()
        {
            if (CTDebug.draw)
            {
                UpdateLine(xLine, transform.right);
                UpdateLine(yLine, transform.up);
                UpdateLine(zLine, transform.forward);
            }

            if (CTDebug.draw == drawEnabled)
                return;

            if (CTDebug.draw)
            {
                xLine.enabled = true;
                yLine.enabled = true;
                zLine.enabled = true;
            }
            else
            {
                xLine.enabled = false;
                yLine.enabled = false;
                zLine.enabled = false;
            }

            drawEnabled = CTDebug.draw;
        }

        private void SetupLine(LineRenderer line, Color color)
        {
            line.material = new Material(Shader.Find("Unlit/Color"));
            line.material.color = color;
            line.widthMultiplier = 0.03f;
        }

        private void UpdateLine(LineRenderer line, Vector3 direction)
        {
            line.SetPositions(new Vector3[] { transform.position, transform.position + (direction * scale) });
        }
    }
}
