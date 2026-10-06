using System;
using System.Reflection;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid
{
    public static class CollectableStateTierVerification
    {
        private static void Require(bool condition, string message, ref int checks)
        {
            if (!condition) throw new InvalidOperationException(message);
            ++checks;
        }

        public static int RunManaged()
        {
            int checks = 0;
            var state = new CollectableState();
            Require(state.Total == 0 && state.Collected == 0, "initial counters", ref checks);
            state.AddCollectable(12);
            Require(state.Total == 12 && state.Collected == 0, "registration only changes total", ref checks);
            Require(state.AdjustCollected(5, true) == 5 && state.Collected == 5, "collect increment", ref checks);
            Require(state.AdjustCollected(20, true) == 7 && state.Collected == 12, "upper cap returns applied change", ref checks);
            Require(state.AdjustCollected(-4, true) == -4 && state.Collected == 8, "damage decrement", ref checks);
            Require(state.AdjustCollected(-20, true) == -8 && state.Collected == 0, "damage lower cap", ref checks);
            Require(state.AdjustCollected(99, false) == 99 && state.Collected == 99, "no authored-total cap", ref checks);
            Require(state.AdjustCollected(0, true) == -87 && state.Collected == 12, "zero adjustment can restore total cap", ref checks);
            state.Reset();
            Require(state.Total == 12 && state.Collected == 0, "reset preserves authored total", ref checks);
            state.AddCollectable(-20);
            Require(state.Total == -8, "negative registered total is preserved", ref checks);
            Require(state.AdjustCollected(0, true) == -8 && state.Collected == -8, "negative maximum and zero sum", ref checks);
            Require(state.AdjustCollected(-1, true) == 8 && state.Collected == 0, "negative sum lower-clamps before negative maximum", ref checks);
            Require(state.AdjustCollected(3, true) == -8 && state.Collected == -8, "positive sum may clamp to negative total", ref checks);
            Require(state.AdjustCollected(10, false) == 10 && state.Collected == 2, "unlimited adjustment from negative count", ref checks);
            state.Reset();
            Require(state.Total == -8 && state.Collected == 0, "reset negative-total case", ref checks);

            state = new CollectableState();
            state.AddCollectable(int.MaxValue);
            state.AddCollectable(1);
            Require(state.Total == int.MinValue, "registration wraps positive overflow", ref checks);
            Require(state.AdjustCollected(0, true) == int.MinValue && state.Collected == int.MinValue, "minimum total remains minimum", ref checks);
            Require(state.AdjustCollected(-1, false) == -1 && state.Collected == int.MaxValue, "minimum plus negative wraps positive and delta wraps", ref checks);
            Require(state.AdjustCollected(1, false) == -int.MaxValue && state.Collected == 0, "collection positive overflow lower-clamps", ref checks);
            state.AddCollectable(-1);
            Require(state.Total == int.MaxValue, "registration wraps negative overflow", ref checks);
            Require(state.AdjustCollected(int.MaxValue, true) == int.MaxValue && state.Collected == int.MaxValue, "maximum count exact", ref checks);
            Require(state.AdjustCollected(0, true) == 0 && state.Collected == int.MaxValue, "exact maximum is stable", ref checks);
            Require(state.AdjustCollected(int.MinValue, true) == -int.MaxValue && state.Collected == 0, "signed sum negative lower cap", ref checks);

            var tier = new CollectableTier();
            Require(tier.Index == 0 && tier.MaxDamage == 0 && tier.StartingCount == 0 && tier.ReviveMultiplier == 0f, "tier defaults include zero multiplier", ref checks);
            tier.Index = -7;
            tier.MaxDamage = int.MinValue;
            tier.StartingCount = int.MaxValue;
            tier.ReviveMultiplier = -2.5f;
            Require(tier.Index == -7 && tier.MaxDamage == int.MinValue && tier.StartingCount == int.MaxValue && tier.ReviveMultiplier == -2.5f, "tier setters retain unconstrained values", ref checks);
            tier.ReviveMultiplier = float.NaN;
            Require(float.IsNaN(tier.ReviveMultiplier), "tier retains NaN", ref checks);
            tier.ReviveMultiplier = float.PositiveInfinity;
            Require(float.IsPositiveInfinity(tier.ReviveMultiplier), "tier retains positive infinity", ref checks);
            tier.ReviveMultiplier = float.NegativeInfinity;
            Require(float.IsNegativeInfinity(tier.ReviveMultiplier), "tier retains negative infinity", ref checks);
            tier.ReviveMultiplier = BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x80000000)), 0);
            Require(BitConverter.ToInt32(BitConverter.GetBytes(tier.ReviveMultiplier), 0) == int.MinValue, "tier retains signed zero bits", ref checks);
            Require(new CollectableTier().ReviveMultiplier == 0f && new CollectableState().Total == 0, "instances do not share mutable state", ref checks);

            InspectStateAndTier(ref checks);
            return checks;
        }

        private static void InspectStateAndTier(ref int checks)
        {
            Type[] types = { typeof(CollectableState), typeof(CollectableTier) };
            string[][] names = { new[] { "Total", "Collected" }, new[] { "Index", "MaxDamage", "StartingCount", "ReviveMultiplier" } };
            for (int i = 0; i < types.Length; ++i)
            {
                Type t = types[i];
                Require(t.Assembly.GetName().Name == "Game.Runtime" && t.BaseType == typeof(object), "original type and assembly", ref checks);
                Require(t.IsPublic && !t.IsAbstract && !t.IsSerializable, "original public nonserializable concrete type", ref checks);
                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic);
                Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
                Require(fields.Length == names[i].Length, "complete own field count", ref checks);
                for (int n = 0; n < names[i].Length; ++n)
                {
                    Type expected = names[i][n] == "ReviveMultiplier" ? typeof(float) : typeof(int);
                    Require(fields[n].Name == "<" + names[i][n] + ">k__BackingField" && fields[n].FieldType == expected, "ordered natural backing field", ref checks);
                    Require(fields[n].IsPrivate && !fields[n].IsStatic && !fields[n].IsInitOnly, "original field flags", ref checks);
                    var property = t.GetProperty(names[i][n]);
                    Require(property != null && property.PropertyType == expected && property.GetMethod.IsPublic, "public getter", ref checks);
                    Require(property.GetSetMethod(true).IsPrivate == (i == 0), "state private and tier public setters", ref checks);
                }
                var attrs = t.GetCustomAttributesData();
                Require(attrs.Count == 2 && attrs[0].AttributeType == typeof(Il2CppSetOptionAttribute) && attrs[1].AttributeType == typeof(Il2CppSetOptionAttribute), "complete original option attributes", ref checks);
                Require((int)attrs[0].ConstructorArguments[0].Value == (i == 0 ? 1 : 2) && (int)attrs[1].ConstructorArguments[0].Value == (i == 0 ? 2 : 1), "original option order", ref checks);
                Require(!(bool)attrs[0].ConstructorArguments[1].Value && !(bool)attrs[1].ConstructorArguments[1].Value, "original option arguments", ref checks);
            }
        }
    }
}
