using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucid.Tests.UISelectionShared
{
    // Test-only Image whose virtual color accessors (Graphic slots 22/23) are recorded.
    // The getter replaces r with ReadBackRed so a caller that reuses its own value instead
    // of reading the colour back is detectable.
    public sealed class ColourReadBackImage : Image
    {
        public const float ReadBackRed = 0.123f;

        public List<string> Log;
        public readonly List<Color> Sets = new List<Color>();

        public override Color color
        {
            get
            {
                if (Log != null) Log.Add("get");
                Color stored = base.color;
                stored.r = ReadBackRed;
                return stored;
            }
            set
            {
                if (Log != null)
                {
                    Log.Add("set");
                    Sets.Add(value);
                }
                base.color = value;
            }
        }
    }
}
