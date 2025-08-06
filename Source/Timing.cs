using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CaptureTools.Utils;

namespace CaptureTools
{
    public class Timing
    {
        // Wrapper around Unity timing controls that provides toggles, automatic limits and rounding.
        // todo: consider moving to MonoBehaviour (auto update, auto reset to defaults on destruction, but what to do about keeping values between scenes?).

        public const int minFramerate = 1;
        public const int maxFramerate = 240;

        public readonly Setting maxDeltaTime = new Setting(new Constrained(Time.maximumDeltaTime, 0.02f, 0.1f), () => Time.maximumDeltaTime, v => Time.maximumDeltaTime = v);
        public readonly Setting fixedDeltaTime = new Setting(new Constrained(Time.fixedDeltaTime, 0.02f, 0.1f), () => Time.fixedDeltaTime, v => Time.fixedDeltaTime = v);
        public readonly Setting captureFramerate = new Setting(new Constrained(60, minFramerate, maxFramerate), () => Time.captureFramerate, v => Time.captureFramerate = (int)v);
        public readonly Setting timeScale = new Setting(new Constrained(Time.timeScale, 0, 1), () => Time.timeScale, v => Time.timeScale = v);

        public class Setting
        {
            public readonly Constrained constrained;
            private float defaultValue;
            private bool apply;
            private Func<float> getter;
            private Action<float> setter;

            public bool Apply
            {
                get => apply;
                set
                {
                    if (apply == value)
                        return;

                    apply = value;
                    if (value)
                    {
                        defaultValue = getter();
                        setter(constrained);
                    }
                    else
                        setter(defaultValue);
                }
            }

            public Setting(Constrained constrained, Func<float> getter, Action<float> setter)
            {
                this.constrained = constrained;
                this.getter = getter;
                this.setter = setter;
            }

            public void Update()
            {
                if (apply && getter() != constrained)
                    setter(constrained);
            }

            public static implicit operator float(Setting c) => c.constrained;
            public static implicit operator int(Setting c) => (int)c.constrained;
        }

        public float TimeRatio { private set; get; }
        private Queue<float> ptrRollingQ = new Queue<float>();
        private float ptrLast;

        public void Update()
        {
            maxDeltaTime.constrained.Value = Mathf.Clamp(maxDeltaTime, Time.fixedDeltaTime, 1f);
            maxDeltaTime.Update();

            fixedDeltaTime.Update();
            captureFramerate.Update();
            timeScale.Update();

            UpdatePTR(Time.realtimeSinceStartup, Time.deltaTime);
        }

        private void UpdatePTR(float rtss, float deltaTime)
        {
            if (deltaTime > 0)
                ptrRollingQ.Enqueue(deltaTime / (rtss - ptrLast));

            ptrLast = rtss;

            while (ptrRollingQ.Count > 60)
                ptrRollingQ.Dequeue();

            if (ptrRollingQ.Count > 0)
                TimeRatio = ptrRollingQ.Average();
        }

        public void Reset()
        {
            ptrRollingQ.Clear();
            ptrLast = 0;

            maxDeltaTime.Apply = false;
            fixedDeltaTime.Apply = false;
            timeScale.Apply = false;
            captureFramerate.Apply = false;

            // todo: Should remove these?
            Time.captureFramerate = 0;
            Time.maximumDeltaTime = GameSettings.PHYSICS_FRAME_DT_LIMIT;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
