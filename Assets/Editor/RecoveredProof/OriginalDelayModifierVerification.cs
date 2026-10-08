using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // Original ModifierDelayUntil 060023e6..060023ea. Scalar and vector timing
    // differ in the supplied release. Pure groups use only local managed fields;
    // RunConstructionEngine8 separately verifies the real Unity constructor.
    public static class OriginalDelayModifierVerification
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static FieldInfo Field(string name)
        {
            FieldInfo result = typeof(ModifierDelayUntil).GetField(name, Instance);
            if (result == null) throw new InvalidOperationException("Missing original modifier field " + name);
            return result;
        }
        private static void Set(ModifierDelayUntil owner, string name, object value) { Field(name).SetValue(owner, value); }
        private static float Held(ModifierDelayUntil owner) { return (float)Field("m_inputHeldSeconds").GetValue(owner); }
        private static Dictionary<GameInput, float> Cache(ModifierDelayUntil owner)
        { return (Dictionary<GameInput, float>)Field("m_cachedTimeByInput").GetValue(owner); }
        private static ModifierDelayUntil Local(float delay)
        {
            var owner = (ModifierDelayUntil)FormatterServices.GetUninitializedObject(typeof(ModifierDelayUntil));
            Set(owner, "m_delaySeconds", delay);
            Set(owner, "m_cachedTimeByInput", new Dictionary<GameInput, float>());
            return owner;
        }
        private static void Check(bool value, ref int count, string message)
        { if (!value) throw new InvalidOperationException(message); count++; }
        private static bool Exact(Vector3 a, Vector3 b) { return a.x == b.x && a.y == b.y && a.z == b.z; }
        private static bool ThrowsNull(Action action)
        { try { action(); return false; } catch (NullReferenceException) { return true; } }

        public static int RunScalar36()
        {
            int count = 0;
            ModifierDelayUntil owner = Local(0.5f);
            Dictionary<GameInput, float> cache = Cache(owner);
            Check(owner.Modify(7f, -9f) == 7f, ref count, "Unkeyed scalar is identity");
            Check(BitConverter.ToInt32(BitConverter.GetBytes(owner.Modify(-0f, 4f)), 0) == unchecked((int)0x80000000), ref count, "Unkeyed negative zero");
            Check(float.IsNaN(owner.Modify(float.NaN, 4f)), ref count, "Unkeyed unordered value");
            Check(float.IsPositiveInfinity(owner.Modify(float.PositiveInfinity, 4f)), ref count, "Unkeyed positive infinity");
            Check(float.IsNegativeInfinity(owner.Modify(float.NegativeInfinity, 4f)), ref count, "Unkeyed negative infinity");
            Check(owner.Modify(7f, 0.25f, GameInput.Left) == 0f, ref count, "First keyed sample waits");
            Check(cache[GameInput.Left] == 0.25f, ref count, "First sample accumulates");
            Check(owner.Modify(7f, 0.25f, GameInput.Left) == 0f, ref count, "Second keyed sample waits");
            Check(cache[GameInput.Left] == 0.5f, ref count, "Second sample reaches threshold");
            Check(owner.Modify(7f, 0f, GameInput.Left) == 0f, ref count, "Equal old timer still waits");
            Check(cache[GameInput.Left] == 0.5f, ref count, "Equal timer with zero delta");
            Check(owner.Modify(7f, 0.25f, GameInput.Left) == 0f, ref count, "Crossing sample uses old timer");
            Check(cache[GameInput.Left] == 0.75f, ref count, "Crossing sample stores new timer");
            Check(owner.Modify(7f, -20f, GameInput.Left) == 7f, ref count, "Released key returns value");
            Check(cache[GameInput.Left] == 0.75f, ref count, "Released key stops accumulating");
            Check(owner.Modify(8f, 0.25f, GameInput.Right) == 0f, ref count, "New key has independent delay");
            Check(cache[GameInput.Right] == 0.25f, ref count, "New key owns timer");
            Check(cache[GameInput.Left] == 0.75f, ref count, "New key retains old key timer");
            Check(cache.Count == 2, ref count, "Two distinct cached keys");
            Check(ReferenceEquals(cache, Cache(owner)), ref count, "Dictionary alias retained");
            owner = Local(0f); cache = Cache(owner);
            Check(owner.Modify(3f, 0.1f, GameInput.Left) == 0f, ref count, "Zero-delay first key still waits");
            Check(cache[GameInput.Left] == 0.1f, ref count, "Zero delay accumulates first key");
            Check(owner.Modify(3f, 99f, GameInput.Left) == 3f, ref count, "Next zero-delay sample releases");
            Check(cache[GameInput.Left] == 0.1f, ref count, "Released zero-delay timer retained");
            owner = Local(float.NegativeInfinity); cache = Cache(owner);
            Check(owner.Modify(5f, 1f, GameInput.Left) == 5f, ref count, "Negative infinity delay releases");
            Check(cache[GameInput.Left] == 0f, ref count, "Missing key inserted before release comparison");
            owner = Local(float.NaN); cache = Cache(owner); cache.Add(GameInput.Left, 1f);
            Check(owner.Modify(5f, 0.25f, GameInput.Left) == 0f, ref count, "Unordered delay retains wait");
            Check(cache[GameInput.Left] == 1.25f, ref count, "Unordered delay still accumulates");
            Set(owner, "m_delaySeconds", 0f); cache[GameInput.Left] = float.NaN;
            Check(owner.Modify(5f, 0.25f, GameInput.Left) == 0f, ref count, "Unordered old time waits");
            Check(float.IsNaN(cache[GameInput.Left]), ref count, "Unordered time retained");
            Set(owner, "m_delaySeconds", float.PositiveInfinity); cache[GameInput.Left] = float.PositiveInfinity;
            Check(owner.Modify(5f, 1f, GameInput.Left) == 0f, ref count, "Equal infinite old time waits");
            Check(float.IsPositiveInfinity(cache[GameInput.Left]), ref count, "Infinite accumulation retained");
            Set(owner, "m_inputHeldSeconds", 17f); Set(owner, "m_cachedTimeByInput", null);
            Check(ThrowsNull(() => owner.Modify(5f, 1f, GameInput.Left)), ref count, "Keyed null cache faults");
            Check(Held(owner) == 17f, ref count, "Keyed fault leaves vector timer");
            Check(owner.Modify(3f, 1f) == 3f, ref count, "Unkeyed path does not read cache");
            Check(Held(owner) == 17f, ref count, "Unkeyed path leaves vector timer");
            return count;
        }

        public static int RunVectorReset24()
        {
            int count = 0;
            ModifierDelayUntil owner = Local(0.5f);
            var value = new Vector3(2f, -3f, 4f);
            Check(Exact(owner.Modify(value, 0.25f), Vector3.zero), ref count, "Vector below threshold waits");
            Check(Held(owner) == 0.25f, ref count, "Vector accumulated time");
            Check(Exact(owner.Modify(value, 0.25f), value), ref count, "Equal NEW vector time releases");
            Check(Held(owner) == 0.5f, ref count, "Equal new timer retained");
            Check(Exact(owner.Modify(value, -0.125f), Vector3.zero), ref count, "Negative delta can resume wait");
            Check(Held(owner) == 0.375f, ref count, "Vector timer can decrease");
            Set(owner, "m_inputHeldSeconds", float.NaN);
            Check(Exact(owner.Modify(value, 0f), value), ref count, "Unordered vector time releases");
            Check(float.IsNaN(Held(owner)), ref count, "Unordered vector time retained");
            Set(owner, "m_inputHeldSeconds", 0f); Set(owner, "m_delaySeconds", float.NaN);
            Check(Exact(owner.Modify(value, 0f), value), ref count, "Unordered vector delay releases");
            Check(Held(owner) == 0f, ref count, "Zero vector delta retained");
            Set(owner, "m_inputHeldSeconds", float.PositiveInfinity); Set(owner, "m_delaySeconds", float.PositiveInfinity);
            Check(Exact(owner.Modify(value, 0f), value), ref count, "Equal infinite vector time releases");
            Check(float.IsPositiveInfinity(Held(owner)), ref count, "Infinite vector time retained");
            Set(owner, "m_inputHeldSeconds", 0f); Set(owner, "m_delaySeconds", float.NegativeInfinity);
            Check(Exact(owner.Modify(value, 0f), value), ref count, "Negative infinite vector delay releases");
            Dictionary<GameInput, float> cache = Cache(owner); cache.Add(GameInput.Left, 1f); cache.Add(GameInput.Right, 2f);
            Check(cache.Count == 2, ref count, "Reset starts with two keys");
            owner.Reset();
            Check(cache.Count == 0, ref count, "Reset clears keys");
            Check(Held(owner) == 0f, ref count, "Reset clears vector timer");
            Check(ReferenceEquals(cache, Cache(owner)), ref count, "Reset retains dictionary alias");
            Check((float)Field("m_delaySeconds").GetValue(owner) == float.NegativeInfinity, ref count, "Reset retains configured delay");
            Set(owner, "m_inputHeldSeconds", 17f); Set(owner, "m_cachedTimeByInput", null);
            Check(ThrowsNull(owner.Reset), ref count, "Reset null cache faults");
            Check(Held(owner) == 17f, ref count, "Reset fault preserves prior vector timer");
            Check(Exact(owner.Modify(value, 0f), value), ref count, "Vector path does not read cache");
            Check(Held(owner) == 17f, ref count, "Independent vector timer with null cache");
            Check(owner.Modify(-4f, 3f) == -4f, ref count, "Unkeyed scalar remains identity");
            Check(Held(owner) == 17f, ref count, "Unkeyed scalar cannot change vector timer");
            return count;
        }

        public static int RunConstructionEngine8()
        {
            int count = 0;
            ModifierDelayUntil owner = ScriptableObject.CreateInstance<ModifierDelayUntil>();
            try
            {
                Check((float)Field("m_delaySeconds").GetValue(owner) == 0f, ref count, "Original default delay");
                Dictionary<GameInput, float> cache = Cache(owner);
                Check(cache != null && cache.Count == 0, ref count, "Original constructor allocates empty cache");
                Check(Held(owner) == 0f, ref count, "Original default vector timer");
                Check(owner.ModifierType == InputModifier.InputModifierType.Float, ref count, "Original base default modifier type");
                Check(owner.Modify(9f, 1f) == 9f, ref count, "Constructed scalar identity");
                Check(owner.Modify(9f, 0.1f, GameInput.Left) == 0f, ref count, "Constructed first keyed input waits");
                Check(owner.Modify(9f, 1f, GameInput.Left) == 9f, ref count, "Constructed second keyed input releases");
                owner.Reset();
                Check(cache.Count == 0 && Held(owner) == 0f, ref count, "Constructed reset");
                return count;
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
    }
}
