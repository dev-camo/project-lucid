using System.Collections.Generic;
using Hardlight;

namespace ProjectLucid.Tests
{
    // Test-only concrete element for the abstract original HLRecyclingScrollRectElement`1
    // (no original subclass exists). It records every Initialise (slot 4) and OnScroll
    // (slot 5) dispatch. It is a top-level class in its own same-named file because
    // AddComponent needs a MonoScript for it, and it lives in this runtime test assembly
    // because Unity refuses to attach a component whose script is Editor-only.
    public sealed class OriginalRecyclingScrollRectElementProbe : HLRecyclingScrollRectElement<int>
    {
        public readonly List<int> Received = new List<int>();
        [System.NonSerialized] public int ScrollCount;

        public override void Initialise(int data) => Received.Add(data);

        public override void OnScroll()
        {
            ScrollCount++;
            base.OnScroll();
        }
    }
}
