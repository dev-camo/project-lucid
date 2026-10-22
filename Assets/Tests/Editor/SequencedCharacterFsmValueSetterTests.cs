using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime SequencedCharacterFSMValueSetter`1 (TypeDef 0x02000617, shared x86-64 bodies 0xBD0210..0xBD0F20,
    // bool instances 0xBD0210/0xBD0250/0xBD0490/0xBD0540/0xBD0550 and int instances 0xBD0640..0xBD0970) with its
    // bool and int leaves (0x02000615, 0x02000616). The effects run on owned inactive hosts. The class caches the
    // CharacterManager reference in a static field per closed type, so a case fills that reference through its own
    // InternalReplaceSystem with a manager built by its real constructor and revokes it again, the way the trigger
    // condition fixtures do (TriggerConditionTestSupport). The delayed removal (a positive m_delayToRemove) needs a
    // running coroutine host and is not covered; RemoveValue is called directly.
    public sealed class SequencedCharacterFsmValueSetterTests
    {
        private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        // The setter's key name is resolved through the graph name lookup; an existing key keeps that lookup unchanged.
        private const string KeyName = "IsEnteringLevel";

        private static readonly MethodInfo RevokeSystem = typeof(SystemRef<CharacterManager>).GetMethod("Hardlight.ISystemRef.InternalRevokeSystem", Instance);
        private static readonly FieldInfo StartupObservers = typeof(SystemRef<CharacterManager>).GetField("OnSystemStartup", Instance);

        // Runs body with the closed type's static reference reading the scene's current-character manager.
        private static void WithSetterReference<T>(Action<Owned, Scene> body) => Run((owned, scene) =>
        {
            FieldInfo field = typeof(SequencedCharacterFSMValueSetter<T>).GetField("m_characterManagerRef", Statics);
            var reference = (SystemRef<CharacterManager>)field.GetValue(null);
            // Run's scene already fills its condition and cached references; this static may be the same object.
            if (ReferenceEquals(reference, scene.ConditionReference) || ReferenceEquals(reference, scene.CachedReference))
            {
                body(owned, scene);
                return;
            }
            TriggerConditionTestSupport.RequireOwnableReference(reference);
            object priorObservers = StartupObservers.GetValue(reference);
            try
            {
                Scene.ReplaceWith(reference, scene.Manager);
                body(owned, scene);
            }
            finally
            {
                if (scene.Characters.Built(reference.GetSafe())) RevokeSystem.Invoke(reference, null);
                StartupObservers.SetValue(reference, priorObservers);
            }
        });

        private static TEffect NewEffect<TEffect>(Owned owned, Action<SerializedObject> configure) where TEffect : Component
        {
            var effect = owned.Host("Lucid owned sequenced FSM setter", false).AddComponent<TEffect>();
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("m_fsmKeyValue").stringValue = KeyName;
            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        private static void Play(SequencedEffect effect, Action onComplete)
        {
            IEffectData data = new TargetedEffect();
            effect.PlayEffect(in data, onComplete);
        }

        // 0xBD0210 (bool) and 0xBD0640 (int): Start builds the storage key from the serialized name (GraphStorageKey
        // constructor 0x060008b1, node and graph 0); nothing resolves it before.
        [Test]
        public void StartResolvesTheStorageKeyFromTheSerializedName()
        {
            WithSetterReference<bool>((owned, scene) =>
            {
                var effect = NewEffect<SequencedCharacterFSMBoolSetter>(owned, _ => { });
                Assert.That(Get<GraphStorageKey>(effect, "m_fsmVarName"), Is.Not.EqualTo(ActorFSMKeys.IsEnteringLevel));
                Call(effect, "Start");
                Assert.That(Get<GraphStorageKey>(effect, "m_fsmVarName"), Is.EqualTo(ActorFSMKeys.IsEnteringLevel));
            });
            WithSetterReference<int>((owned, scene) =>
            {
                var effect = NewEffect<SequencedCharacterFSMIntSetter>(owned, _ => { });
                Call(effect, "Start");
                Assert.That(Get<GraphStorageKey>(effect, "m_fsmVarName"), Is.EqualTo(ActorFSMKeys.IsEnteringLevel));
            });
        }

        // 0xBD0376..0xBD040c: the serialized value is stored under the key in the current character's storage
        // (IGraphStorage.SetValue<T>), then, with no positive delay (0xBD0414..0xBD041a), the stored completion
        // callback runs once (0xBD0465..0xBD0483).
        [Test]
        public void TheBoolValueIsStoredOnTheCurrentCharacterAndCompletes()
        {
            WithSetterReference<bool>((owned, scene) =>
            {
                var effect = NewEffect<SequencedCharacterFSMBoolSetter>(owned, serialized => serialized.FindProperty("m_valueToSet").boolValue = true);
                Call(effect, "Start");
                int completed = 0;
                Play(effect, () => completed++);
                Assert.That(scene.Storage.HasValue(ActorFSMKeys.IsEnteringLevel), Is.True);
                Assert.That(scene.Storage.GetValue<bool>(ActorFSMKeys.IsEnteringLevel), Is.True);
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        [Test]
        public void TheIntValueIsStoredOnTheCurrentCharacterAndCompletes()
        {
            WithSetterReference<int>((owned, scene) =>
            {
                var effect = NewEffect<SequencedCharacterFSMIntSetter>(owned, serialized => serialized.FindProperty("m_valueToSet").intValue = 42);
                Call(effect, "Start");
                int completed = 0;
                Play(effect, () => completed++);
                Assert.That(scene.Storage.GetValue<int>(ActorFSMKeys.IsEnteringLevel), Is.EqualTo(42));
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // 0xBD0356..0xBD035c: with no valid current character nothing is stored but the callback still runs
        // (0xBD0465 is reached from the failed TryGetCurrentCharacter).
        [Test]
        public void WithoutACurrentCharacterNothingIsStoredButTheEffectStillCompletes()
        {
            WithSetterReference<bool>((owned, scene) =>
            {
                scene.SetCurrent(scene.Character, false);
                var effect = NewEffect<SequencedCharacterFSMBoolSetter>(owned, serialized => serialized.FindProperty("m_valueToSet").boolValue = true);
                Call(effect, "Start");
                int completed = 0;
                Play(effect, () => completed++);
                Assert.That(scene.Storage.HasValue(ActorFSMKeys.IsEnteringLevel), Is.False);
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // 0xBD0490: RemoveValue<T> on the character's storage with the resolved key; other keys stay.
        [Test]
        public void RemoveValueDropsOnlyTheSetterKey()
        {
            WithSetterReference<int>((owned, scene) =>
            {
                var effect = NewEffect<SequencedCharacterFSMIntSetter>(owned, serialized => serialized.FindProperty("m_valueToSet").intValue = 7);
                Call(effect, "Start");
                Play(effect, () => { });
                scene.Storage.SetValue(ActorFSMKeys.IsExitingLevel, 1);
                Call(effect, "RemoveValue", scene.Character);
                Assert.That(scene.Storage.HasValue(ActorFSMKeys.IsEnteringLevel), Is.False);
                Assert.That(scene.Storage.HasValue(ActorFSMKeys.IsExitingLevel), Is.True);
            });
        }

        // Field 0x04001598 keeps its former serialized name, the delay field is clamped at zero, and the declared
        // member order and flags of the generic definition are those of the metadata.
        [Test]
        public void SerializedFieldsKeepTheirOriginalNamesAndAttributes()
        {
            Type definition = typeof(SequencedCharacterFSMValueSetter<>);
            Assert.That(definition.IsAbstract, Is.True);
            FieldInfo value = definition.GetField("m_valueToSet", Instance);
            var formerly = (UnityEngine.Serialization.FormerlySerializedAsAttribute)Attribute.GetCustomAttribute(value, typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute));
            Assert.That(formerly.oldName, Is.EqualTo("m_boolValueToSet"));
            var minimum = (MinAttribute)Attribute.GetCustomAttribute(definition.GetField("m_delayToRemove", Instance), typeof(MinAttribute));
            Assert.That(minimum.min, Is.EqualTo(0f));
            Assert.That(definition.GetField("m_characterManagerRef", Statics).Attributes,
                Is.EqualTo(FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly));
            Assert.That(typeof(SequencedCharacterFSMBoolSetter).BaseType, Is.EqualTo(typeof(SequencedCharacterFSMValueSetter<bool>)));
            Assert.That(typeof(SequencedCharacterFSMIntSetter).BaseType, Is.EqualTo(typeof(SequencedCharacterFSMValueSetter<int>)));
        }
    }
}
