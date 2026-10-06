using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    public sealed class CharacterBrainLifecycleTests
    {
        // Test-only input access; all state/update/coroutine behavior is inherited.
        private sealed class Brain : CharacterBrain
        {
            public override void Close() { }
            public void Input(GameAction action, bool value) { SetState(action, value); }
        }

        [UnityTest]
        public IEnumerator OriginalBrainReenablesThroughItsCoroutineHostAndAppliesDeferredActions()
        {
            const BindingFlags own = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo registry = typeof(ProcessManager).GetField("s_systemDictionary", own);
            FieldInfo singleton = typeof(CoroutineUtils).GetField("s_instance", own);
            FieldInfo indices = typeof(CharacterActionFlags).GetField("m_actionIndices", own);
            FieldInfo count = typeof(CharacterActionFlags).GetField("<ActionCount>k__BackingField", own);
            FieldInfo mapping = typeof(CharacterActionFlags).GetField("m_actionMapping", own);
            object oldRegistry = registry.GetValue(null), oldSingleton = singleton.GetValue(null);
            object oldIndices = indices.GetValue(null), oldCount = count.GetValue(null);
            var map = (Dictionary<GameAction, int>)mapping.GetValue(null);
            var oldMap = new Dictionary<GameAction, int>(map);
            GameObject owner = null;
            CoroutineUtils host = null;
            try
            {
                // Separate registry rows protect previously registered real systems.
                registry.SetValue(null, Activator.CreateInstance(registry.FieldType));
                singleton.SetValue(null, null);
                owner = new GameObject("Lucid original brain coroutine proof");
                host = owner.AddComponent<CoroutineUtils>();
                var brain = new Brain();
                brain.Initialise();
                brain.Input(GameAction.CharacterJump, true);
                brain.Input(GameAction.CharacterSpinDashCharge, true);
                brain.Input(GameAction.CharacterRoll, true);
                brain.Update(10f);
                Assert.That(brain.Jump && brain.SpinDashCharge && brain.Roll, Is.True);
                brain.SetEnabled(false, 11f);
                Assert.That(brain.Enabled, Is.False);
                Assert.That(brain.Jump || brain.SpinDashCharge, Is.False, "Original impulses end before disabling.");
                Assert.That(brain.Roll, Is.True, "Roll is retained; SpinDashCharge is the ninth reset action.");
                brain.Input(GameAction.CharacterJump, true);
                Assert.That(brain.GetRawState(GameAction.CharacterJump), Is.True);
                Assert.That(brain.Jump, Is.False, "Disabled input stays deferred.");
                int startFrame = Time.frameCount;
                brain.SetEnabled(true, 12f);
                Assert.That(brain.Enabled, Is.False, "The original callback has not run synchronously.");
                brain.Input(GameAction.CharacterBoost, true);
                for (int i = 0; i < 3 && !brain.Enabled; i++) yield return null;
                Assert.That(brain.Enabled, Is.True, "The real original coroutine host completes re-enable.");
                Assert.That(Time.frameCount, Is.GreaterThan(startFrame), "At least one genuine Unity frame advanced.");
                Assert.That(brain.Jump, Is.False, "Re-enable callback does not apply actions itself.");
                brain.Update(13f);
                Assert.That(brain.Jump && brain.Roll, Is.True, "Update consumes the original deferred snapshot.");
                Assert.That(brain.Boost, Is.False, "Input during activation remains raw rather than being queued.");
                Assert.That(brain.GetRawState(GameAction.CharacterBoost), Is.True);
                Assert.That(brain.GetActionTimestamp(GameAction.CharacterJump), Is.EqualTo(13f));
                Assert.That(brain.GetActionTimestamp(GameAction.CharacterSpinDashCharge), Is.EqualTo(11f));
                Assert.That(brain.GetActionTimestamp(GameAction.CharacterRoll), Is.EqualTo(10f));
                yield return null;
                brain.Update(14f);
                Assert.That(brain.Enabled && brain.Jump && brain.Roll, Is.True);
                Assert.That(brain.GetActionTimestamp(GameAction.CharacterJump), Is.EqualTo(13f), "Unchanged applied state does not retimestamp.");
            }
            finally
            {
                try { if (host != null) host.StopAllCoroutines(); }
                finally
                {
                    try { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
                    finally
                    {
                        try { singleton.SetValue(null, oldSingleton); }
                        finally
                        {
                            try { registry.SetValue(null, oldRegistry); }
                            finally
                            {
                                map.Clear();
                                foreach (var row in oldMap) map.Add(row.Key, row.Value);
                                indices.SetValue(null, oldIndices);
                                count.SetValue(null, oldCount);
                            }
                        }
                    }
                }
            }
        }
    }
}
