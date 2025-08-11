using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CaptureTools
{
    public class BuildAnimation : MonoBehaviour
    {
        // Build animation.
        private List<Part> buildOrder = new List<Part>();
        private Coroutine buildCoroutine;
        private Coroutine[] partCoroutines;
        private List<Vector3> originalPositions = new List<Vector3>();
        private List<bool> partsVisible = new List<bool>();

        public static float buildTime = 5f;
        public static float buildPartSpeed = 0.1f;
        public static float buildPartMaxSpeed = 500f;

        public bool Playing
        {
            get => buildCoroutine != null;
            set
            {
                if (Playing == value)
                    return;

                if (value)
                    buildCoroutine = StartCoroutine(StartBuild(buildTime));
                else
                    StopBuild();
            }
        }

        protected void OnDestroy()
        {
            Playing = false;
        }

        private IEnumerator StartBuild(float time)
        {
            float startTime = Time.unscaledTime;

            Vessel activeVessel = FlightGlobals.ActiveVessel;
            List<Part> parts = HighLogic.LoadedSceneIsEditor ? EditorLogic.SortedShipList : activeVessel.Parts;
            Part root = HighLogic.LoadedSceneIsEditor ? EditorLogic.RootPart : activeVessel.rootPart;

            parts = parts.Where(p => p.transform.parent == null).ToList();
            buildOrder = parts.OrderBy(p => Vector3.Distance(p.transform.position, root.transform.position)).ToList();

            buildOrder = buildOrder.Distinct().ToList();
            partsVisible = buildOrder.Select(p => false).ToList();
            originalPositions = buildOrder.Select(p => p.transform.position).ToList();
            partCoroutines = new Coroutine[buildOrder.Count];

            HideStruts(buildOrder, true);

            List<Vector3> awayFromParent = new List<Vector3>();
            Vector3 away;
            Transform active = HighLogic.LoadedSceneIsEditor ? root.transform : activeVessel.ReferenceTransform;

            foreach (Part part in buildOrder)
            {
                if (part == root)
                {
                    awayFromParent.Add(Vector3.zero);
                    continue;
                }

                away = part.transform.position - root.transform.position;
                awayFromParent.Add(RoundVector6(active.up, active.forward, away.normalized));
            }

            for (int i = 0; i < buildOrder.Count; i++)
            {
                buildOrder[i].transform.position += awayFromParent[i] * 100;
                buildOrder[i].FindModelMeshRenderersCached().ForEach(mr => mr.enabled = false);
            }

            while ((Time.unscaledTime - startTime <= time + 1) && Time.timeScale == 0)
            {
                SetBuildFromRoot((Time.unscaledTime - startTime) / time);

                yield return null;
            }

            HideStruts(buildOrder, false);

            // wait for all part coroutines to finish
            while (partCoroutines.Any(c => c != null) && Time.timeScale == 0)
            {
                yield return null;
            }

            StopBuild();
        }

        private void StopBuild()
        {
            foreach (Coroutine coroutine in partCoroutines)
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
            }

            for (int i = 0; i < buildOrder.Count; i++)
            {
                if (buildOrder[i] != null)
                {
                    buildOrder[i].transform.position = originalPositions[i];
                    buildOrder[i].FindModelMeshRenderersCached().ForEach(mr => mr.enabled = true);
                }
            }

            originalPositions.Clear();
            partsVisible.Clear();
            buildOrder.Clear();
            partCoroutines = null;

            StopCoroutine(buildCoroutine);
            buildCoroutine = null;
        }

        private void HideStruts(List<Part> parts, bool hide)
        {
            List<Part> struts = parts.FindAll(p => p.name == "strutConnector" || p.name == "fuelLine");

            foreach (Part strut in struts)
            {
                foreach (MeshRenderer mr in strut.FindModelMeshRenderersCached())
                    mr.enabled = !hide;

                foreach (SkinnedMeshRenderer smr in strut.FindModelSkinnedMeshRenderersCached())
                    smr.enabled = !hide;
            }
        }

        private Vector3 RoundVector6(Vector3 up, Vector3 forwards, Vector3 current)
        {
            Vector3 down = up * -1;
            Vector3 backwards = forwards * -1;
            Vector3 right = Vector3.Cross(up, forwards).normalized;
            Vector3 left = right * -1;

            List<Vector3> directions = new List<Vector3>() { up, down, left, right, forwards, backwards };

            directions = directions.OrderBy(v => Vector3.Angle(current, v)).ToList();
            return directions.First();
        }

        private void SetBuildFromRoot(float buildFromRoot)
        {
            bool visible;

            for (int i = 0; i < buildOrder.Count; i++)
            {
                if (partsVisible[i])
                    continue;

                visible = (float)i / buildOrder.Count < buildFromRoot;

                if (!visible)
                    continue;

                partsVisible[i] = visible;

                if (i == 0)
                    continue;

                partCoroutines[i] = StartCoroutine(AnimatePart(i));
            }
        }

        private IEnumerator AnimatePart(int partIndex)
        {
            Part part = buildOrder[partIndex];
            Vector3 start = part.transform.position;
            Vector3 originalPos = originalPositions[partIndex];
            float startTime = Time.unscaledTime;
            Vector3 velocity = Vector3.zero;

            if (part.name != "strutConnector" && part.name != "fuelLine")
                buildOrder[partIndex].FindModelMeshRenderersCached().ForEach(mr => mr.enabled = true);

            while (Vector3.Distance(part.transform.position, originalPos) > 0.002f)
            {
                part.transform.position = Vector3.SmoothDamp(part.transform.position, originalPos, ref velocity,
                    buildPartSpeed, buildPartMaxSpeed, Time.unscaledDeltaTime);
                yield return null;
            }

            part.transform.position = originalPos;
            partCoroutines[partIndex] = null;
        }
    }
}
