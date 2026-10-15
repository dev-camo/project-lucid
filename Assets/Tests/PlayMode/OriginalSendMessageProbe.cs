using System.Collections.Generic;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Test-only receiver of actual Unity SendMessage calls; it provides no game API.
    public sealed class OriginalSendMessageProbe : MonoBehaviour
    {
        public readonly List<string> Received = new List<string>();

        public void Receive(string data) => Received.Add(data);
    }
}
