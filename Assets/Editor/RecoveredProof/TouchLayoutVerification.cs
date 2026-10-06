using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;
using DeviceGeneration = HardlightProject.DevicePerformanceMatch_iOS.DeviceGeneration;

namespace ProjectLucid
{
    // Bounded original definition/dependency verification. Raw managed objects
    // are used only where no native Unity object operation is invoked.
    public static class TouchLayoutVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Original touch layout: " + label); checks++; }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
        private static void Set(object value, Type owner, string name, object data) => Field(owner, name).SetValue(value, data);
        private static void Throws<T>(Action action, string label) where T : Exception { try { action(); throw new Exception("Expected fault absent: " + label); } catch (T) { Check(true, label); } }
        private static TouchLayoutConfiguration.TouchControlDefault Setting(string id, Vector2 position, float scale)
        {
            var value = new TouchLayoutConfiguration.TouchControlDefault();
            Set(value, typeof(TouchLayoutConfiguration.TouchControlDefault), "m_saveId", id);
            Set(value, typeof(TouchLayoutConfiguration.TouchControlDefault), "m_position", position);
            Set(value, typeof(TouchLayoutConfiguration.TouchControlDefault), "m_scale", scale);
            return value;
        }
        private static DefinitionDataType<string, TouchLayoutConfiguration>.DefinitionElement<TouchLayoutConfiguration> Row(TouchLayoutConfiguration value)
        {
            var row = Raw<DefinitionDataType<string, TouchLayoutConfiguration>.DefinitionElement<TouchLayoutConfiguration>>();
            row.Data = value;
            return row;
        }
        public static int RunManaged()
        {
            checks = 0;
            var initial = new TouchLayoutConfiguration.TouchControlDefault();
            Check(initial.SaveId == null, "natural default retains null identifier");
            Check(initial.Position.x == 0f && initial.Position.y == 0f, "natural default position is zero");
            Check(initial.Scale == 1f, "native nested constructor initializes scale one");
            var exact = Setting("jump", new Vector2(-12.5f, 19.25f), -2f);
            Check(exact.SaveId == "jump" && exact.Position.x == -12.5f && exact.Position.y == 19.25f && exact.Scale == -2f, "getters expose retained fields without normalizing");
            var other = Setting("jump", new Vector2(7, 8), 0f);
            var empty = Setting("", new Vector2(3, 4), float.NaN);
            var nullId = Setting(null, new Vector2(5, 6), float.PositiveInfinity);
            Check(float.IsNaN(empty.Scale) && float.IsPositiveInfinity(nullId.Scale), "nonfinite authored scale retained");
            var config = Raw<TouchLayoutConfiguration>();
            Set(config, typeof(TouchLayoutConfiguration), "m_controlDefaults", new[] { exact, other, empty, nullId });
            TouchLayoutConfiguration.TouchControlDefault result = null;
            Check(config.TryGetTouchControlDefault("jump", out result) && ReferenceEquals(result, exact), "first exact duplicate identifier wins");
            Check(config.TryGetTouchControlDefault("", out result) && ReferenceEquals(result, empty), "empty identifier remains valid");
            Check(config.TryGetTouchControlDefault(null, out result) && ReferenceEquals(result, nullId), "static String.Equals matches two null identifiers");
            Check(!config.TryGetTouchControlDefault("Jump", out result) && result == null, "ordinal case difference misses and clears out");
            result = exact;
            Check(!config.TryGetTouchControlDefault("absent", out result) && result == null, "ordinary miss overwrites a prior out value");
            var retained = (TouchLayoutConfiguration.TouchControlDefault[])Field(typeof(TouchLayoutConfiguration), "m_controlDefaults").GetValue(config);
            retained[0] = nullId;
            Check(config.TryGetTouchControlDefault(null, out result) && ReferenceEquals(result, nullId), "external retained-array mutation is observed on next call");
            Set(config, typeof(TouchLayoutConfiguration), "m_controlDefaults", Array.Empty<TouchLayoutConfiguration.TouchControlDefault>());
            result = exact;
            Check(!config.TryGetTouchControlDefault(null, out result) && result == null, "empty array misses even null key");
            Set(config, typeof(TouchLayoutConfiguration), "m_controlDefaults", null);
            result = exact;
            Throws<NullReferenceException>(() => config.TryGetTouchControlDefault("jump", out result), "null default array reaches original array fault");
            Check(result == null, "out reset precedes null array access");
            Set(config, typeof(TouchLayoutConfiguration), "m_controlDefaults", new TouchLayoutConfiguration.TouchControlDefault[] { null, exact });
            result = exact;
            Throws<NullReferenceException>(() => config.TryGetTouchControlDefault("jump", out result), "null row is not skipped");
            Check(result == null, "out reset precedes null row getter fault");
            var devices = new[] { DeviceGeneration.iPhone12, DeviceGeneration.iPad1Gen };
            Set(config, typeof(TouchLayoutConfiguration), "m_devices", devices);
            Check(ReferenceEquals(config.Devices, devices), "Devices exposes exact stored array");
            devices[0] = DeviceGeneration.iPhone11;
            Check(config.Devices[0] == DeviceGeneration.iPhone11, "Devices remains externally mutable");
            Set(config, typeof(TouchLayoutConfiguration), "m_devices", null);
            Check(ReferenceEquals(config.Devices, null), "Devices getter preserves null");

            Check(Hardlight.ArrayExtensions.Contains(new[] { 3, 1, 3 }, 3), "genuine generic dependency finds present integer");
            Check(!Hardlight.ArrayExtensions.Contains(new[] { 3, 1, 3 }, 2), "genuine generic dependency misses absent integer");
            Check(!Hardlight.ArrayExtensions.Contains(Array.Empty<int>(), 0), "genuine generic dependency empty array");
            Check(Hardlight.ArrayExtensions.Contains(new[] { DeviceGeneration.iPhone12, (DeviceGeneration)int.MinValue }, (DeviceGeneration)int.MinValue), "shared enum dependency keeps unknown signed value");
            Check(Hardlight.ArrayExtensions.Contains(new string[] { "a", null }, null), "generic reference dependency matches null");
            Check(!Hardlight.ArrayExtensions.Contains(new[] { "a" }, "A"), "generic string dependency retains ordinary equality");
            Check(Hardlight.ArrayExtensions.Contains(new[] { float.NaN }, float.NaN), "Array.IndexOf equality preserves NaN Equals behavior");
            Throws<ArgumentNullException>(() => Hardlight.ArrayExtensions.Contains<int>(null, 1), "no custom null guard replaces Array.IndexOf fault");

            var first = Raw<TouchLayoutConfiguration>(); var second = Raw<TouchLayoutConfiguration>();
            Set(first, typeof(TouchLayoutConfiguration), "m_devices", new[] { DeviceGeneration.iPhone12 });
            Set(second, typeof(TouchLayoutConfiguration), "m_devices", new[] { DeviceGeneration.iPhone12, (DeviceGeneration)int.MaxValue });
            var group = Raw<TouchLayoutConfigurationGroup>();
            group.m_elements = new[] { Row(first), Row(second) };
            Check(ReferenceEquals(group.GetDefaultDeviceLayout(DeviceGeneration.iPhone12), first), "first matching authored row wins before defaults");
            Check(ReferenceEquals(group.GetDefaultDeviceLayout((DeviceGeneration)int.MaxValue), second), "unknown enum can match exact authored row");
            Set(first, typeof(TouchLayoutConfiguration), "m_devices", Array.Empty<DeviceGeneration>());
            Check(ReferenceEquals(group.GetDefaultDeviceLayout(DeviceGeneration.iPhone12), second), "empty devices row advances to next authored row");
            Set(first, typeof(TouchLayoutConfiguration), "m_devices", null);
            Throws<ArgumentNullException>(() => group.GetDefaultDeviceLayout(DeviceGeneration.iPhone12), "null devices array reaches original Contains/IndexOf fault");
            group.m_elements = null;
            Throws<NullReferenceException>(() => group.GetDefaultDeviceLayout(DeviceGeneration.iPhone12), "null inherited elements array is not repaired");
            group.m_elements = new DefinitionDataType<string, TouchLayoutConfiguration>.DefinitionElement<TouchLayoutConfiguration>[] { null };
            Throws<NullReferenceException>(() => group.GetDefaultDeviceLayout(DeviceGeneration.iPhone12), "null original definition row is not skipped");
            group.m_elements = new[] { Row(null) };
            Throws<NullReferenceException>(() => group.GetDefaultDeviceLayout(DeviceGeneration.iPhone12), "null row Data reaches direct Devices getter");
            var comparer = typeof(TouchLayoutConfigurationGroup).GetMethod("GetKeyComparer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Check(ReferenceEquals(comparer.Invoke(group, null), null), "original group comparer is null");
            Set(group, typeof(TouchLayoutConfigurationGroup), "m_simulatedDeviceInEditor", (DeviceGeneration)int.MinValue);
            Check(group.SimulatedDevice == (DeviceGeneration)int.MinValue, "simulated-device getter retains arbitrary signed authored enum");
            return checks;
        }

        public static int RunEngine()
        {
            checks = 0;
            var owned = new List<UnityEngine.Object>();
            try
            {
                var first = ScriptableObject.CreateInstance<TouchLayoutConfiguration>(); owned.Add(first);
                var second = ScriptableObject.CreateInstance<TouchLayoutConfiguration>(); owned.Add(second);
                var phone = ScriptableObject.CreateInstance<TouchLayoutConfiguration>(); owned.Add(phone);
                var pad = ScriptableObject.CreateInstance<TouchLayoutConfiguration>(); owned.Add(pad);
                var group = ScriptableObject.CreateInstance<TouchLayoutConfigurationGroup>(); owned.Add(group);
                Check(first != null && second != null && phone != null && pad != null && group != null, "genuine original concrete ScriptableObject creation");
                Check(first.Devices != null && first.Devices.Length == 0, "original constructor supplies empty device array");
                Check(ReferenceEquals(first.Devices, second.Devices), "original constructors share Array.Empty enum array");
                var firstDefaults = Field(typeof(TouchLayoutConfiguration), "m_controlDefaults").GetValue(first);
                Check(ReferenceEquals(firstDefaults, Field(typeof(TouchLayoutConfiguration), "m_controlDefaults").GetValue(second)), "original constructors share Array.Empty setting array");
                Check(((Array)firstDefaults).Length == 0, "original control defaults begin empty");
                Check(group.SimulatedDevice == DeviceGeneration.iPhone12 && (int)group.SimulatedDevice == 57, "native group constructor stores enum57");
                Check(group.m_elements == null, "genuine original definition base retains null elements");
                first.name = "first-touch"; second.name = "second-touch";
                Set(first, typeof(TouchLayoutConfiguration), "m_devices", new[] { DeviceGeneration.iPhone12 });
                Set(second, typeof(TouchLayoutConfiguration), "m_devices", new[] { DeviceGeneration.iPhone12, DeviceGeneration.iPad1Gen });
                group.m_elements = new[] { new DefinitionDataType<string, TouchLayoutConfiguration>.DefinitionElement<TouchLayoutConfiguration>(first), new DefinitionDataType<string, TouchLayoutConfiguration>.DefinitionElement<TouchLayoutConfiguration>(second) };
                Set(group, typeof(TouchLayoutConfigurationGroup), "m_iPhoneDefault", phone);
                Set(group, typeof(TouchLayoutConfigurationGroup), "m_iPadDefault", pad);
                Check(ReferenceEquals(group.GetDefaultDeviceLayout(DeviceGeneration.iPhone12), first), "authored first match precedes nonnull phone fallback");
                Check(ReferenceEquals(group.GetDefaultDeviceLayout(DeviceGeneration.iPad1Gen), second), "authored row precedes nonnull tablet fallback");
                var data = group.GetData();
                Check(data.Count == 2 && ReferenceEquals(data["first-touch"], first) && ReferenceEquals(data["second-touch"], second), "genuine name key callback and real definition dictionary");
                group.m_elements = Array.Empty<DefinitionDataType<string, TouchLayoutConfiguration>.DefinitionElement<TouchLayoutConfiguration>>();
                Check(ReferenceEquals(group.GetDefaultDeviceLayout(DeviceGeneration.iPhone12), phone), "phone enum string selects exact phone default");
                Check(ReferenceEquals(group.GetDefaultDeviceLayout(DeviceGeneration.iPad1Gen), pad), "tablet enum string selects exact iPad default");
                Check(ReferenceEquals(group.GetDefaultDeviceLayout((DeviceGeneration)int.MinValue), phone), "unknown numeric enum string follows phone branch");
                Set(group, typeof(TouchLayoutConfigurationGroup), "m_iPadDefault", null);
                var freshPad = group.GetDefaultDeviceLayout(DeviceGeneration.iPad1Gen); owned.Add(freshPad);
                Check(freshPad != null && !ReferenceEquals(freshPad, phone), "missing tablet default creates real new configuration rather than phone");
                Check(freshPad.Devices.Length == 0, "actual fallback runs original constructor");
                UnityEngine.Object.DestroyImmediate(phone);
                var freshPhone = group.GetDefaultDeviceLayout(DeviceGeneration.iPhone12); owned.Add(freshPhone);
                Check(freshPhone != null && !ReferenceEquals(freshPhone, phone), "destroyed Unity default triggers genuine concrete fallback");
                var again = group.GetDefaultDeviceLayout(DeviceGeneration.iPhone12); owned.Add(again);
                Check(again != null && !ReferenceEquals(again, freshPhone), "absent default is not cached after allocation");
                var setting = Setting("stick", new Vector2(-3.25f, 7.5f), -0.75f);
                var round = JsonUtility.FromJson<TouchLayoutConfiguration.TouchControlDefault>(JsonUtility.ToJson(setting));
                Check(round.SaveId == "stick" && round.Position.x == -3.25f && round.Position.y == 7.5f && round.Scale == -0.75f, "actual Unity serialized nested fields round trip");
                Set(first, typeof(TouchLayoutConfiguration), "m_controlDefaults", new[] { setting });
                Set(first, typeof(TouchLayoutConfiguration), "m_devices", new[] { DeviceGeneration.iPhone11 });
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(first), second);
                TouchLayoutConfiguration.TouchControlDefault read;
                Check(second.Devices.Length == 1 && second.Devices[0] == DeviceGeneration.iPhone11, "actual configuration enum array round trip");
                Check(second.TryGetTouchControlDefault("stick", out read) && read.Scale == -0.75f && read.Position.x == -3.25f, "actual configuration nested settings array round trip");
                return checks;
            }
            finally
            {
                for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            }
        }
        public static void Run()
        {
            int managed = RunManaged(); int engine = RunEngine();
            Debug.Log("Original touch-layout dependency verification: " + managed + " managed + " + engine + " actual-engine checks passed.");
        }
    }
}
