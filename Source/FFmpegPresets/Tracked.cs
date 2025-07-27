using System.Collections.Generic;

namespace CaptureTools
{
    public class Tracked
    {
        public bool Editable { get; set; }
        public bool Modified { get; set; }

        protected void SetTracked<T>(ref T field, T value)
        {
            // Set fields only if editing is allowed, and mark modified if the value has changed.

            if (!Editable)
                return;

            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            if (!Modified)
                Modified = true;

            field = value;
        }
    }
}
