using KSP.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CaptureTools
{
    partial class CaptureTools
    {
        // Build animation.
        private List<Part> buildOrder = new List<Part>();
        public float buildTime = 5f;
        private Coroutine buildCoroutine;
        private Coroutine[] partCoroutines;
        private List<Vector3> originalPositions = new List<Vector3>();
        private List<bool> partsVisible = new List<bool>();

        public float buildPartSpeed = 0.1f;
        public float buildPartMaxSpeed = 500f;

        public bool Building
        {
            get => buildCoroutine != null;
            set
            {
                if (Building == value)
                    return;

                if (value)
                    buildCoroutine = StartCoroutine(StartBuild(buildTime));
                else
                    StopBuild();
            }
        }
        
        #region Animate Build

        private IEnumerator StartBuild(float time)
        {
            float startTime = Time.unscaledTime;

            Vessel activeVessel = FlightGlobals.ActiveVessel;
            List<Part> parts = HighLogic.LoadedSceneIsEditor ? EditorLogic.SortedShipList : activeVessel.Parts;
            Part root = HighLogic.LoadedSceneIsEditor ? EditorLogic.RootPart : activeVessel.rootPart;

            parts = parts.Where(p => p.transform.parent == null).ToList();
            buildOrder = parts.OrderBy(p => DistanceFromRoot(p, root)).ToList();

            buildOrder = buildOrder.Distinct().ToList();
            partsVisible = buildOrder.Select(p => false).ToList();
            originalPositions = buildOrder.Select(p => p.transform.position).ToList();
            partCoroutines = new Coroutine[buildOrder.Count];

            HideStruts(buildOrder, true);

            var awayFromParent = new List<Vector3>();
            Vector3 away;
            Transform active = HighLogic.LoadedSceneIsEditor ? root.transform : activeVessel.ReferenceTransform;

            foreach (var part in buildOrder)
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

            //for (int i = 0; i < buildOrder.Count; i++)
            //{
            //    buildOrder[i].transform.position = originalPositions[i];
            //}

            //buildCoroutine = null;

            StopBuild();
        }

        public void StopBuild()
        {
            foreach (var coroutine in partCoroutines)
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

        void HideStruts(List<Part> parts, bool hide)
        {
            var struts = parts.FindAll(p => p.name == "strutConnector" || p.name == "fuelLine");

            foreach (var strut in struts)
            {
                foreach (var mr in strut.FindModelMeshRenderersCached())
                    mr.enabled = !hide;

                foreach (var smr in strut.FindModelSkinnedMeshRenderersCached())
                    smr.enabled = !hide;
            }
        }

        Vector3 RoundVector6(Vector3 up, Vector3 forwards, Vector3 current)
        {
            Vector3 down = up * -1;
            Vector3 backwards = forwards * -1;
            Vector3 right = Vector3.Cross(up, forwards).normalized;
            Vector3 left = right * -1;

            var directions = new List<Vector3>() { up, down, left, right, forwards, backwards };

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

            //Transform parent = part.transform.parent;
            //part.transform.parent = null;

            if (part.name != "strutConnector" && part.name != "fuelLine")
                buildOrder[partIndex].FindModelMeshRenderersCached().ForEach(mr => mr.enabled = true);

            while (Vector3.Distance(part.transform.position, originalPos) > 0.002f)
            {
                //part.transform.position = Vector3.Lerp(part.transform.position, originalPos, Time.unscaledDeltaTime * partAnimateTime);
                part.transform.position = Vector3.SmoothDamp(part.transform.position, originalPos, ref velocity, 
                    buildPartSpeed, buildPartMaxSpeed, Time.unscaledDeltaTime);
                yield return null;
            }

            //part.transform.parent = parent;
            part.transform.position = originalPos;
            partCoroutines[partIndex] = null;
        }

        private int StepsFromRoot(Part part)
        {
            Part currentPart = part;
            int steps = 0;
            Part root = FlightGlobals.ActiveVessel.rootPart;

            while (currentPart != root)
            {
                currentPart = currentPart.parent;
                steps++;
            }

            return steps;
        }

        private float DistanceFromCoM(Part part)
        {
            return Vector3.Distance(part.transform.position, FlightGlobals.ActiveVessel.CoM);
        }

        private float DistanceFromRoot(Part part, Part root)
        {
            return Vector3.Distance(part.transform.position, root.transform.position);
        }

        #endregion
    }
}
