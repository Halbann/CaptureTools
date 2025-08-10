using System;

namespace CaptureTools.Utils
{
    public class Constrained
    {
        private float value;

        public readonly float? min;
        public readonly float? max;
        public readonly int? digits;

        public float Value
        {
            get => value;
            set
            {
                if (value != this.value)
                    this.value = ApplyConstraints(value);
            }
        }

        public Constrained(float value, float? min = null, float? max = null, int? digits = null)
        {
            this.min = min;
            this.max = max;
            this.digits = digits;
            this.value = 0;
            this.value = ApplyConstraints(value);
        }

        private float ApplyConstraints(float input)
        {
            if (digits.HasValue)
                input = (float)Math.Round(input, digits.Value);

            if (min.HasValue && input < min) return min.Value;
            if (max.HasValue && input > max) return max.Value;
            return input;
        }

        public override string ToString() => Value.ToString();

        public static implicit operator float(Constrained c) => c.Value;
        public static implicit operator int(Constrained c) => (int)c.Value;
    }
}
