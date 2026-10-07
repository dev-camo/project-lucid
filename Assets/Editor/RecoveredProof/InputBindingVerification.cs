using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class InputBindingVerification
    {
        private static readonly FieldInfo Names = typeof(BaseBinding).GetField("m_modifierNames", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Modifiers = typeof(BaseBinding).GetField("m_modifiers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Require(bool value, string label, ref int checks)
        { if (!value) throw new InvalidOperationException(label); checks++; }
        private static void Fault<T>(Action action, string label, ref int checks) where T : Exception
        {
            try { action(); } catch (T) { checks++; return; }
            throw new InvalidOperationException(label);
        }
        private sealed class ReadProbe : IReadOnlyList<InputModifier>
        {
            internal readonly List<string> Reads = new List<string>();
            internal Func<int> GetCount;
            internal Func<int, InputModifier> GetItem;
            public int Count { get { Reads.Add("Count"); return GetCount(); } }
            public InputModifier this[int index] { get { Reads.Add("Item:" + index); return GetItem(index); } }
            public IEnumerator<InputModifier> GetEnumerator() { throw new InvalidOperationException("Original uses indexing"); }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        }

        // Only genuine CLR Axis/Button/Fixed constructors execute here. No ScriptableObject construction or bypass.
        public static int RunManaged()
        {
            int checks = 0;
            Require(Names != null && Modifiers != null, "real private field identities", ref checks);
            var supplied = new List<InputModifier>();
            string axisName = new string(new[] { 'x', '\0', '\u2603' });
            var axis = new AxisBinding(axisName, supplied);
            Require(ReferenceEquals(axis.Axis, axisName), "axis exact string identity", ref checks);
            IAxisProvider<string> axisProvider = axis;
            Require(ReferenceEquals(axisProvider.Axis, axisName), "genuine invariant axis getter interface dispatch", ref checks);
            Require(((IAxisProvider<string>)new AxisBinding(null, null)).Axis == null, "genuine axis contract preserves null", ref checks);
            Require(ReferenceEquals(axis.Modifiers, supplied), "base exact supplied list identity", ref checks);
            Require(Names.GetValue(axis) == null, "name list initially null", ref checks);
            Require(new AxisBinding(null, null).Axis == null, "axis null remains null", ref checks);
            Require(new AxisBinding("", null).Modifiers == null, "null modifier list preserved", ref checks);
            foreach (int bits in new[] { 0, 1, -1, int.MinValue, int.MaxValue, 97, 303, 330 })
            {
                var button = new ButtonBinding((KeyCode)bits, supplied);
                Require((int)button.Key == bits, "button exact enum bits " + bits, ref checks);
                IKeyProvider<KeyCode> keyProvider = button;
                Require((int)keyProvider.Key == bits, "genuine key getter interface dispatch " + bits, ref checks);
                Require(ReferenceEquals(button.Modifiers, supplied), "button exact modifier list", ref checks);
                Require(Names.GetValue(button) == null, "button names initially null", ref checks);
            }
            var fixedBinding = new FixedBindings();
            Require(fixedBinding.Modifiers == null, "fixed base receives null", ref checks);
            Require(Names.GetValue(fixedBinding) == null, "fixed saved names initially null", ref checks);
            string[] fields = { "BindToPointer", "BindToScrollWheel", "BindToTap", "BindToDoubleTap", "BindToTouch", "BindToTouchRelease", "BindToMultiTouch", "BindToMultiTouchRelease", "BindToHold", "BindToHoldPressure" };
            Require(!fixedBinding.AnySet(), "fixed no input initially", ref checks);
            foreach (string name in fields)
            {
                FieldInfo field = typeof(FixedBindings).GetField(name);
                Require(field != null && !(bool)field.GetValue(fixedBinding), "fixed original default " + name, ref checks);
                field.SetValue(fixedBinding, true);
                Require(fixedBinding.AnySet(), "each original fixed input " + name, ref checks);
                field.SetValue(fixedBinding, false);
                Require(!fixedBinding.AnySet(), "restored fixed input " + name, ref checks);
            }
            Require(!fixedBinding.Expand, "expand default false", ref checks);
            fixedBinding.Expand = true;
            Require(!fixedBinding.AnySet(), "expand excluded from input rule", ref checks);
            fixedBinding.BindToHoldPressure = true;
            Require(fixedBinding.AnySet(), "late ordered fixed input", ref checks);

            axis.SaveModifierName();
            var names = (List<string>)Names.GetValue(axis);
            Require(names != null && names.Count == 0, "save creates empty names", ref checks);
            names.Add("stale"); axis.SaveModifierName();
            Require(ReferenceEquals(names, Names.GetValue(axis)) && names.Count == 0, "save reuses and clears names", ref checks);
            var nullBinding = new AxisBinding("null", null);
            Fault<NullReferenceException>(() => nullBinding.SaveModifierName(), "save null list must fault", ref checks);
            var createdBeforeFault = (List<string>)Names.GetValue(nullBinding);
            Require(createdBeforeFault != null && createdBeforeFault.Count == 0, "save allocation precedes modifier fault", ref checks);
            createdBeforeFault.Add("old");
            Fault<NullReferenceException>(() => nullBinding.SaveModifierName(), "second save null fault", ref checks);
            Require(ReferenceEquals(createdBeforeFault, Names.GetValue(nullBinding)) && createdBeforeFault.Count == 0, "save clear-before-fault preserves name receiver", ref checks);

            var loaded = new AxisBinding("load", supplied);
            supplied.Add(null);
            loaded.LoadModifierByName(Array.Empty<InputModifier>());
            Require(ReferenceEquals(supplied, loaded.Modifiers) && supplied.Count == 0, "load clears existing exact list", ref checks);
            Require(Names.GetValue(loaded) == null, "empty load does not create names", ref checks);
            var fresh = new AxisBinding("fresh", null);
            fresh.LoadModifierByName(Array.Empty<InputModifier>());
            Require(fresh.Modifiers != null && fresh.Modifiers.Count == 0, "empty load creates modifiers", ref checks);
            var created = fresh.Modifiers;
            Fault<NullReferenceException>(() => fresh.LoadModifierByName(null), "null all-modifier input fault", ref checks);
            Require(ReferenceEquals(created, fresh.Modifiers) && fresh.Modifiers.Count == 0, "load receiver created/cleared before Count fault", ref checks);
            var probe = new ReadProbe();
            supplied.Add(null);
            probe.GetCount = () => { Require(supplied.Count == 0, "Count observes cleared current receiver", ref checks); return 1; };
            probe.GetItem = i => { throw new ArgumentException("genuine index callback"); };
            Fault<ArgumentException>(() => loaded.LoadModifierByName(probe), "input index executes before null saved-name invocation", ref checks);
            Require(probe.Reads.Count == 2 && probe.Reads[0] == "Count" && probe.Reads[1] == "Item:0", "load original indexed order", ref checks);
            Require(ReferenceEquals(supplied, loaded.Modifiers) && supplied.Count == 0, "index fault keeps cleared list", ref checks);
            probe.Reads.Clear(); probe.GetCount = () => -1;
            loaded.LoadModifierByName(probe);
            Require(probe.Reads.Count == 1 && probe.Reads[0] == "Count", "signed negative Count does no indexing", ref checks);
            return checks;
        }

        // Owned genuine ScriptableObjects only; no fake modifier, instance/cctor bypass or audible/input-device claim.
        public static int RunEngine()
        {
            int checks = 0;
            ModifierInvert first = null, second = null;
            try
            {
                first = ScriptableObject.CreateInstance<ModifierInvert>();
                second = ScriptableObject.CreateInstance<ModifierInvert>();
                Require(first != null && second != null, "genuine modifier instances", ref checks);
                Require(first.ModifierType == InputModifier.InputModifierType.Float, "original inherited zero field", ref checks);
                foreach (float value in new[] { 0f, -0f, 1f, -2f, float.PositiveInfinity, float.NegativeInfinity })
                    Require(BitConverter.ToInt32(BitConverter.GetBytes(first.Modify(value, float.NaN)), 0)
                        == (BitConverter.ToInt32(BitConverter.GetBytes(value), 0) ^ int.MinValue), "invert exact sign bit", ref checks);
                Require(float.IsNaN(first.Modify(float.NaN, 1f)), "invert retains NaN", ref checks);
                first.name = "same"; second.name = "same";
                var modifiers = new List<InputModifier> { first, null, second, first };
                var binding = new AxisBinding("axis", modifiers);
                binding.SaveModifierName();
                var names = (List<string>)Names.GetValue(binding);
                Require(names.Count == 3 && names[0] == "same" && names[1] == "same" && names[2] == "same", "save skips Unity-null and retains duplicate names", ref checks);
                names.Clear(); names.Add("same"); names.Add("absent"); names.Add("same");
                binding.LoadModifierByName(new InputModifier[] { second, first, second });
                Require(ReferenceEquals(binding.Modifiers, modifiers), "load keeps original mutable list", ref checks);
                Require(modifiers.Count == 3 && ReferenceEquals(modifiers[0], second) && ReferenceEquals(modifiers[1], first)
                    && ReferenceEquals(modifiers[2], second), "load supplied order and duplicate instances", ref checks);
                names.Clear(); binding.LoadModifierByName(new InputModifier[] { first });
                Require(modifiers.Count == 0, "unmatched name omitted", ref checks);
                names.Add("same");
                Fault<NullReferenceException>(() => binding.LoadModifierByName(new InputModifier[] { null }), "load null object is not skipped", ref checks);
                Require(modifiers.Count == 0, "load clears before null-name fault", ref checks);
                var destroyed = first;
                UnityEngine.Object.DestroyImmediate(first); first = null;
                modifiers.Add(destroyed); modifiers.Add(second);
                binding.SaveModifierName();
                Require(names.Count == 1 && names[0] == "same", "save skips destroyed Unity object", ref checks);
                var probe = new ReadProbe();
                int countCalls = 0;
                probe.GetCount = () => ++countCalls <= 2 ? 1 : 0;
                int itemReads = 0;
                probe.GetItem = i => { itemReads++; return second; };
                binding.LoadModifierByName(probe);
                Require(itemReads == 2 && countCalls == 2, "load repeats matched item read and rechecks Count", ref checks);
                Require(modifiers.Count == 1 && ReferenceEquals(modifiers[0], second), "load matched original object", ref checks);
                var capturedNames = names;
                var replacementNames = new List<string>();
                itemReads = 0;
                probe.GetCount = () => 1;
                probe.GetItem = i => { itemReads++; Names.SetValue(binding, replacementNames); if (itemReads == 2) throw new ArgumentException("stop after captured-name membership"); return second; };
                Fault<ArgumentException>(() => binding.LoadModifierByName(probe), "Contains receiver captured before index mutation", ref checks);
                Require(itemReads == 2 && capturedNames.Contains("same") && replacementNames.Count == 0, "membership uses captured names and repeated index", ref checks);
                var oldModifiers = modifiers;
                var newModifiers = new List<InputModifier>();
                Names.SetValue(binding, capturedNames); Modifiers.SetValue(binding, oldModifiers);
                int dynamicCount = 0; itemReads = 0;
                probe.GetCount = () => ++dynamicCount == 1 ? 1 : 0;
                probe.GetItem = i => { itemReads++; if (itemReads == 2) Modifiers.SetValue(binding, newModifiers); return second; };
                binding.LoadModifierByName(probe);
                Require(oldModifiers.Count == 1 && ReferenceEquals(oldModifiers[0], second), "Add receiver captured before second index callback", ref checks);
                Require(newModifiers.Count == 0 && ReferenceEquals(binding.Modifiers, newModifiers), "replacement current receiver preserved", ref checks);
                return checks;
            }
            finally
            {
                try { if (first != null) UnityEngine.Object.DestroyImmediate(first); }
                finally { if (second != null) UnityEngine.Object.DestroyImmediate(second); }
            }
        }
        public static int Run() { return RunManaged() + RunEngine(); }
    }
}
