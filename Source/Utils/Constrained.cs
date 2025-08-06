using System;

namespace CaptureTools.Utils
{
    public class Constrained
    {
        private float _value;

        public readonly float? min;
        public readonly float? max;
        public readonly int? digits;

        public float Value
        {
            get => _value;
            set
            {
                if (value != _value)
                    _value = ApplyConstraints(value);
            }
        }

        public Constrained(float value, float? min = null, float? max = null, int? digits = null)
        {
            this.min = min;
            this.max = max;
            this.digits = digits;
            _value = 0;
            _value = ApplyConstraints(value);
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
