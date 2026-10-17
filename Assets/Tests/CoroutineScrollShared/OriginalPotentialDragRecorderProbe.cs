using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests
{
    // Test-only IInitializePotentialDragHandler that records the event data it receives, so
    // a fixture can see which ancestor a parent-forwarding original component reached. It
    // lives in this runtime test assembly because Unity refuses to attach a component
    // whose script is Editor-only.
    public sealed class OriginalPotentialDragRecorderProbe : MonoBehaviour, IInitializePotentialDragHandler
    {
        public readonly List<PointerEventData> Received = new List<PointerEventData>();

        public void OnInitializePotentialDrag(PointerEventData eventData) => Received.Add(eventData);
    }
}
