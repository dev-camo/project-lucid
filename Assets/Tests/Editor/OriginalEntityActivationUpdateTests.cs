using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // EntityActivationManager.OnUpdate, original 0x06003bb4 (x86-64 0x6c3220, 704 bytes). The native
    // body enumerates the dictionary at +0x18 (m_inactive) before the one at +0x10 (m_active). Each
    // list is walked from the count read once down to zero, calling Activate/Deactivate with the
    // instance's Entity (+0x10). An instance activated in the first pass is therefore visited again by
    // the second. Entities, definitions and the manager are built through their real constructors and
    // factories; only the logic predicates are fixture-controlled.
    public sealed class OriginalEntityActivationUpdateTests
    {
        private static readonly List<string> s_trace = new List<string>();
        private static readonly Dictionary<string, bool> s_answers = new Dictionary<string, bool>();

        private sealed class ProbeEntity : IEntityActivatable
        {
            public readonly string Name;
            public ProbeEntity(string name, EntityActivationDefinition definition, Transform transform)
            {
                Name = name; Definition = definition; Transform = transform;
            }
            public EntityActivationDefinition Definition { get; }
            public Transform Transform { get; }
        }

        private sealed class ProbeLogicDefinition : EntityActivationLogicDefinition
        {
            public string Kind;
            public override EntityActivationLogicType Type => default;
            protected override Type ScriptType => typeof(ProbeLogic);
        }

        private sealed class ProbeLogic : EntityActivationLogic
        {
            private string m_key;
            public override void Initialise(EntityActivationLogicDefinition definition, IEntityActivatable entity)
            {
                m_key = ((ProbeLogicDefinition)definition).Kind + " " + ((ProbeEntity)entity).Name;
            }
            public override bool CanTrigger()
            {
                s_trace.Add("can-" + m_key);
                return s_answers.TryGetValue(m_key, out bool answer) && answer;
            }
        }

        private static void SetField(object target, string name, object value) =>
            typeof(EntityActivationDefinition).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static List<string> Names(EntityActivationManager manager, string field)
        {
            var rows = (Dictionary<EntityActivationType, List<EntityActivationInstance>>)typeof(EntityActivationManager)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            return rows[EntityActivationType.Collectable].ConvertAll(instance => ((ProbeEntity)instance.Entity).Name);
        }

        private static void RunUpdate(EntityActivationManager manager) =>
            typeof(EntityActivationManager).GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, new object[] { null });

        [Test]
        public void UpdateRunsTheInactivePassFirstAndBothListsInReverse()
        {
            var owned = new List<Object>();
            EntityActivationManager manager = null;
            s_trace.Clear(); s_answers.Clear();
            try
            {
                var activate = ScriptableObject.CreateInstance<ProbeLogicDefinition>(); activate.Kind = "activate"; owned.Add(activate);
                var deactivate = ScriptableObject.CreateInstance<ProbeLogicDefinition>(); deactivate.Kind = "deactivate"; owned.Add(deactivate);
                var definition = ScriptableObject.CreateInstance<EntityActivationDefinition>(); owned.Add(definition);
                SetField(definition, "m_type", EntityActivationType.Collectable);
                SetField(definition, "m_activateLogicDefinitions", new List<EntityActivationLogicDefinition> { activate });
                SetField(definition, "m_deactivateLogicDefinitions", new List<EntityActivationLogicDefinition> { deactivate });

                manager = new EntityActivationManager();
                var entities = new Dictionary<string, ProbeEntity>();
                foreach (string name in new[] { "A", "B", "C", "D" })
                {
                    var host = new GameObject("Lucid entity activation " + name); owned.Add(host);
                    entities[name] = new ProbeEntity(name, definition, host.transform);
                    string captured = name;
                    manager.Register(entities[name], name == "D",
                        () => s_trace.Add("activated " + captured), () => s_trace.Add("deactivated " + captured));
                }
                Assert.That(Names(manager, "m_inactive"), Is.EqualTo(new[] { "A", "B", "C" }));
                Assert.That(Names(manager, "m_active"), Is.EqualTo(new[] { "D" }));

                // Only B can activate, and B (once active) can immediately deactivate again.
                s_answers["activate B"] = true;
                s_answers["deactivate B"] = true;
                s_trace.Clear();
                RunUpdate(manager);

                // A forward walk would visit A first; running the active pass first would never see B
                // as active, so B would stay active and D would be the only deactivation check.
                Assert.That(s_trace, Is.EqualTo(new[] {
                    "can-activate C", "can-activate B", "activated B", "can-activate A",
                    "can-deactivate B", "deactivated B", "can-deactivate D" }));
                Assert.That(Names(manager, "m_inactive"), Is.EqualTo(new[] { "A", "C", "B" }));
                Assert.That(Names(manager, "m_active"), Is.EqualTo(new[] { "D" }));
            }
            finally
            {
                if (manager != null)
                {
                    manager.UnsubscribeFromAction(SystemAction.Update);
                    manager.Clear();
                }
                foreach (Object item in owned) Object.DestroyImmediate(item);
                s_trace.Clear(); s_answers.Clear();
            }
        }
    }
}
