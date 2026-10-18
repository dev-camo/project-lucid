using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Tests.CrossPromoUIShared
{
    // Test-only CrossPromoPageBase that stores what the full screen view hands it and logs each
    // abstract call as "<page name>.<call>" into a log the fixture shares between its pages.
    public sealed class RecordingCrossPromoPage : CrossPromoPageBase
    {
        public List<string> Log;
        public Sprite Sprite;
        public string Text;
        public bool Ready;

        private void Record(string call)
        {
            if (Log != null) Log.Add(name + "." + call);
        }

        public override void SetSprite(Sprite sprite)
        {
            Record("SetSprite(" + (sprite == null ? "null" : sprite.name) + ")");
            Sprite = sprite;
        }

        public override Sprite GetSprite()
        {
            Record("GetSprite");
            return Sprite;
        }

        public override void SetText(string text)
        {
            Record("SetText(" + text + ")");
            Text = text;
        }

        public override void SetReadyState(bool isReady)
        {
            Record("SetReadyState(" + isReady + ")");
            Ready = isReady;
        }

        public override bool GetReadyState() => Ready;
    }
}
