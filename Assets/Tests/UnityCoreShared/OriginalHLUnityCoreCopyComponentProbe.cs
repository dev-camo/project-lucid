using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Test-owned component for OriginalHLUnityCoreObjectUtilsTests. It records the
    // reflection traffic of ObjectUtils.CopyComponent 0x06001235; the fixture
    // clears the static log before and after every case. Unity refuses to attach
    // scripts from Editor assemblies, so it lives in this runtime test assembly.
    public sealed class OriginalHLUnityCoreCopyComponentProbe : MonoBehaviour
    {
        public static readonly List<string> Log = new List<string>();

        public int m_publicField;

        [SerializeField]
        private string m_privateField;

        public string PrivateField
        {
            get { return m_privateField; }
        }

        // Writable: the setter records the copy's field value at property time.
        public int Recorded
        {
            get { return 7; }
            set { Log.Add("Recorded=" + value + " field=" + m_publicField); }
        }

        // Writable but throwing: CopyComponent swallows it and carries on.
        public int Throwing
        {
            get { return 1; }
            set { throw new InvalidOperationException("probe setter"); }
        }

        // Read-only: CanWrite is false, so its getter is never read.
        public int ReadOnly
        {
            get
            {
                Log.Add("ReadOnly get");
                return 0;
            }
        }

        public void SetPrivateField(string value)
        {
            m_privateField = value;
        }
    }
}
