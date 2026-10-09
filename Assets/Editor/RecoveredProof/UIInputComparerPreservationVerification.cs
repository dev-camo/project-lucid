using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // These clients exercise the original comparer-backed serialization used by
    // prompt overrides. They never create input devices or call audio services.
    public static class UIInputComparerPreservationVerification
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void InputOverridesSurviveSerializedRoundTrip()
        {
            // Shipping enum literals are serialized identities, rather than an
            // ordinal list. Keep a numeric oracle independent of the C# enum.
            string[] names = { "DefaultController", "Keyboard", "MfiController", "Mouse",
                "NpadController", "PS4Controller", "PS5Controller", "Remote", "Touch",
                "Unknown", "Unsupported", "XboneController" };
            int[] values = { 897719803, -1162288346, -594121215, -1482781790,
                -1000566503, -1901056579, 1329525373, 1437681611, -31649603,
                1838982690, 1208867107, -1984142849 };
            var inputs = new SerializableDictionary<UIInputType, string>(
                HardlightUIEnumComparers.UIInputTypeComparer);
            var actions = new SerializableDictionary<UIInputType, int>(
                HardlightUIEnumComparers.UIInputTypeComparer);
            for (int i = 0; i < names.Length; ++i)
            {
                var key = (UIInputType)Enum.Parse(typeof(UIInputType), names[i]);
                Require((int)key == values[i], "Original UI key changed: " + names[i]);
                inputs.Add(key, names[i]);
                actions.Add(key, i);
            }
            ((ISerializationCallbackReceiver)inputs).OnBeforeSerialize();
            ((ISerializationCallbackReceiver)actions).OnBeforeSerialize();
            inputs.Clear();
            actions.Clear();
            ((ISerializationCallbackReceiver)inputs).OnAfterDeserialize();
            ((ISerializationCallbackReceiver)actions).OnAfterDeserialize();
            Require(inputs.Count == 12 && actions.Count == 12, "Override rows were lost");
            for (int i = 0; i < names.Length; ++i)
            {
                var authoredKey = (UIInputType)values[i];
                Require(inputs[authoredKey] == names[i] && actions[authoredKey] == i,
                    "Serialized override no longer resolves: " + names[i]);
            }
            inputs.Remove(UIInputType.Keyboard);
            Require(!inputs.ContainsKey(UIInputType.Keyboard) && actions[UIInputType.Keyboard] == 1,
                "Input and action override tables must remain independent");
        }

        public static void UnknownSignedKeysRemainDistinct()
        {
            // Zero and extrema are not listed UI literals. Rebuilt dictionary
            // clients must preserve them, including boxing the audio struct.
            int[] payloads = { int.MinValue, -1, 0, 1, int.MaxValue };
            IEqualityComparer<HLAudioTypes> audio = default(HLAudioTypeEqualityComparer);
            IEqualityComparer<UIInputType> input = HardlightUIEnumComparers.UIInputTypeComparer;
            var audioKeys = new SerializableDictionary<HLAudioTypes, int>(audio);
            var inputKeys = new SerializableDictionary<UIInputType, int>(input);
            for (int i = 0; i < payloads.Length; ++i)
            {
                int value = payloads[i];
                Require(audio.GetHashCode((HLAudioTypes)value) == value &&
                    input.GetHashCode((UIInputType)value) == value, "Signed hash changed");
                audioKeys.Add((HLAudioTypes)value, i);
                inputKeys.Add((UIInputType)value, i);
            }
            ((ISerializationCallbackReceiver)audioKeys).OnBeforeSerialize();
            ((ISerializationCallbackReceiver)inputKeys).OnBeforeSerialize();
            audioKeys.Clear();
            inputKeys.Clear();
            ((ISerializationCallbackReceiver)audioKeys).OnAfterDeserialize();
            ((ISerializationCallbackReceiver)inputKeys).OnAfterDeserialize();
            for (int i = 0; i < payloads.Length; ++i)
            {
                Require(audioKeys[(HLAudioTypes)payloads[i]] == i &&
                    inputKeys[(UIInputType)payloads[i]] == i, "Unknown key was collapsed");
                for (int j = 0; j < payloads.Length; ++j)
                    Require(audio.Equals((HLAudioTypes)payloads[i], (HLAudioTypes)payloads[j]) == (i == j) &&
                        input.Equals((UIInputType)payloads[i], (UIInputType)payloads[j]) == (i == j),
                        "Distinct unknown keys compared equal");
            }
            Require(ReferenceEquals(input, HardlightUIEnumComparers.UIInputTypeComparer),
                "Registry must retain its original shared reference comparer");
        }
    }
}
