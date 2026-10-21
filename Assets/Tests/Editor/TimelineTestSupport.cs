using System;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Authoring and playable-graph helpers shared by the sequenced effect, subtitle and time machine fixtures.
    internal static class TimelineTestSupport
    {
        // Writes serialized fields as the inspector does.
        internal static void Author(Object target, Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(target);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // A playable graph that the fixture's ownership scope destroys.
        internal static PlayableGraph NewGraph(Owned owned)
        {
            PlayableGraph graph = PlayableGraph.Create("Lucid timeline graph");
            owned.Lease(new ReleaseAction(() => { if (graph.IsValid()) graph.Destroy(); }));
            return graph;
        }
    }
}
