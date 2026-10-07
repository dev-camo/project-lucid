using System.Collections.Generic;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Test-only observer of actual Unity messages; it provides no game API.
    public sealed class OriginalObjectDestroyProbe : MonoBehaviour
    {
        public List<int> Observations;
        public int Identifier;

        private void OnDestroy()
        {
            if (Observations != null) Observations.Add(Identifier);
        }
    }
}
