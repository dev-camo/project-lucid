// Reconstructed original HLAppleInput.Runtime type02000036, methods0600009d..9f.
// This shipped vendor helper is absent in upstream1.0.2. It is original behavior;
// portable controller selection is implemented separately.
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Scripting;

namespace Apple.Core.Runtime
{
    [Preserve]
    public static class InteropGCHandleTracker
    {
        private static readonly Dictionary<string, GCHandle> _arrays = new Dictionary<string, GCHandle>();
        private static readonly object _arraysLock = new object();

        public static void Track(string guid, GCHandle gcHandle)
        {
            lock (_arraysLock)
            {
                // Duplicate identifiers retain Dictionary.Add's original fault.
                _arrays.Add(guid, gcHandle);
            }
        }

        public static void UntrackAndFree(string guid)
        {
            lock (_arraysLock)
            {
                if (_arrays.ContainsKey(guid))
                {
                    try
                    {
                        _arrays[guid].Free();
                    }
                    catch
                    {
                        // Original method0600009e catches the Free/getter failure
                        // without rethrowing. Both CPUs use an object catch path.
                    }
                    _arrays.Remove(guid);
                }
            }
        }
    }
}
