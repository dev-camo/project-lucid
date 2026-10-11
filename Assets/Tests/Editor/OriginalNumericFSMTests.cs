using System;
using System.Linq;
using System.Globalization;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectLucid.Tests
{
    // Controlled rebuilt managed/Unity behavior. These cases do not run the
    // shipping native implementation, original services or the authored boot FSM.
    public class OriginalNumericFSMTests
    {
        private static string Name() => "Lucid-numeric-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);
        private static FSMUser User() => new FSMUser(new FSMStorage(0));

        [Test]
        public void SetFloatCapturesArgumentsAndPerformsSingleArithmetic()
        {
            var machine = Machine(); var user = User();
            var args = new FSMStateSetFloat.JSONCtorArgs
            { Name = Name(), Node = Name(), FSM = Name(), Value = 2.5f, Operation = FSMStateSetFloat.Operation.Set };
            var key = new GraphStorageKey(args.Name, args.Node, args.FSM);
            var set = new FSMStateSetFloat(machine, Name(), args);
            args.Name = Name(); args.Value = 99f;
            set.OnEnter(user, null);
            Assert.That(user.Storage.GetValueOnly<float>(key), Is.EqualTo(2.5f));
            Assert.That(machine.States[set.StateId], Is.SameAs(set));
            var add = new FSMStateSetFloat(machine, Name(), new FSMStateSetFloat.JSONCtorArgs
            { Name = FSMState.LookupNameUsingId(key.NameId), Node = FSMState.LookupNameUsingId(key.NodeId),
              FSM = FSMState.LookupNameUsingId(key.GraphId), Value = -0.5f, Operation = FSMStateSetFloat.Operation.Add });
            add.OnEnter(user, null);
            Assert.That(user.Storage.GetValueOnly<float>(key), Is.EqualTo(2f));
            var multiply = new FSMStateSetFloat(machine, Name(), new FSMStateSetFloat.JSONCtorArgs
            { Name = FSMState.LookupNameUsingId(key.NameId), Node = FSMState.LookupNameUsingId(key.NodeId),
              FSM = FSMState.LookupNameUsingId(key.GraphId), Value = -3f, Operation = FSMStateSetFloat.Operation.Multiply });
            multiply.OnEnter(user, null);
            Assert.That(user.Storage.GetValueOnly<float>(key), Is.EqualTo(-6f));
            user.Storage.SetValue(key, float.NaN);
            add.OnEnter(user, null);
            Assert.That(float.IsNaN(user.Storage.GetValueOnly<float>(key)), Is.True);
        }

        [Test]
        public void InvalidSetOperationsInsertZeroBeforeTheirOriginalFault()
        {
            var machine = Machine(); var user = User();
            var f = new FSMStateSetFloat.JSONCtorArgs { Name = Name(), Node = Name(), FSM = Name(), Value = 9f,
                Operation = (FSMStateSetFloat.Operation)77 };
            var i = new FSMStateSetInt.JSONCtorArgs { Name = Name(), Node = Name(), FSM = Name(), Value = 9,
                Operation = (FSMStateSetInt.Operation)77 };
            var fk = new GraphStorageKey(f.Name, f.Node, f.FSM); var ik = new GraphStorageKey(i.Name, i.Node, i.FSM);
            var fs = new FSMStateSetFloat(machine, Name(), f); var ins = new FSMStateSetInt(machine, Name(), i);
            Assert.That(user.Storage.GetCollection().ContainsKey(fk), Is.False);
            Assert.That(user.Storage.GetCollection().ContainsKey(ik), Is.False);
            var fe = Assert.Throws<ArgumentOutOfRangeException>(() => fs.OnEnter(user, null));
            var ie = Assert.Throws<ArgumentOutOfRangeException>(() => ins.OnEnter(user, null));
            Assert.That(fe.ParamName, Is.Null); Assert.That(ie.ParamName, Is.Null);
            Assert.That(user.Storage.GetCollection().ContainsKey(fk), Is.True);
            Assert.That(user.Storage.GetCollection().ContainsKey(ik), Is.True);
            Assert.That(user.Storage.GetValueOnly<float>(fk), Is.EqualTo(0f));
            Assert.That(user.Storage.GetValueOnly<int>(ik), Is.EqualTo(0));
            // Existing values survive the invalid operation after the real read.
            user.Storage.SetValue(fk, 3f); user.Storage.SetValue(ik, -4);
            Assert.Throws<ArgumentOutOfRangeException>(() => fs.OnEnter(user, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => ins.OnEnter(user, null));
            Assert.That(user.Storage.GetValueOnly<float>(fk), Is.EqualTo(3f));
            Assert.That(user.Storage.GetValueOnly<int>(ik), Is.EqualTo(-4));
        }

        [Test]
        public void SetIntRetainsSignedUncheckedArithmeticAndCapturedValues()
        {
            var machine = Machine(); var user = User();
            var args = new FSMStateSetInt.JSONCtorArgs { Name = Name(), Node = Name(), FSM = Name(), Value = 1,
                Operation = FSMStateSetInt.Operation.Add };
            var key = new GraphStorageKey(args.Name, args.Node, args.FSM);
            var add = new FSMStateSetInt(machine, Name(), args);
            args.Value = 37; user.Storage.SetValue(key, int.MaxValue); add.OnEnter(user, null);
            Assert.That(user.Storage.GetValueOnly<int>(key), Is.EqualTo(int.MinValue));
            var multiply = new FSMStateSetInt(machine, Name(), new FSMStateSetInt.JSONCtorArgs
            { Name = FSMState.LookupNameUsingId(key.NameId), Node = FSMState.LookupNameUsingId(key.NodeId),
              FSM = FSMState.LookupNameUsingId(key.GraphId), Value = 2, Operation = FSMStateSetInt.Operation.Multiply });
            user.Storage.SetValue(key, int.MaxValue); multiply.OnEnter(user, null);
            Assert.That(user.Storage.GetValueOnly<int>(key), Is.EqualTo(-2));
            var set = new FSMStateSetInt(machine, Name(), new FSMStateSetInt.JSONCtorArgs
            { Name = FSMState.LookupNameUsingId(key.NameId), Node = FSMState.LookupNameUsingId(key.NodeId),
              FSM = FSMState.LookupNameUsingId(key.GraphId), Value = -17, Operation = FSMStateSetInt.Operation.Set });
            set.OnEnter(user, null);
            Assert.That(user.Storage.GetValueOnly<int>(key), Is.EqualTo(-17));
        }

        [Test]
        public void CheckFloatUsesStrictToleranceAndOrderedComparisonsWithoutInsertion()
        {
            var machine = Machine(); var user = User(); var key = new GraphStorageKey(Name());
            var zero = new FSMTransitionCheckFloat(machine, Name(), key, 0f, FSMTransitionCheckFloat.Comparison.Equal);
            Assert.That(zero.Tolerance, Is.EqualTo(0f));
            Assert.That(zero.Update(user, default), Is.False);
            Assert.That(user.Storage.GetCollection().ContainsKey(key), Is.False);
            var equal = new FSMTransitionCheckFloat(machine, Name(), key, 2f, FSMTransitionCheckFloat.Comparison.Equal, 1f);
            user.Storage.SetValue(key, 3f); Assert.That(equal.Update(user, default), Is.False, "boundary is strict");
            user.Storage.SetValue(key, 2.5f); Assert.That(equal.Update(user, default), Is.True);
            var negative = new FSMTransitionCheckFloat(machine, Name(), key, 2.5f, FSMTransitionCheckFloat.Comparison.Equal, -1f);
            Assert.That(negative.Update(user, default), Is.False);
            user.Storage.SetValue(key, float.NaN);
            foreach (FSMTransitionCheckFloat.Comparison comparison in Enum.GetValues(typeof(FSMTransitionCheckFloat.Comparison)))
                Assert.That(new FSMTransitionCheckFloat(machine, Name(), key, 2f, comparison, 1f).Update(user, default), Is.False);
            user.Storage.SetValue(key, 2f);
            Assert.That(new FSMTransitionCheckFloat(machine, Name(), key, float.NaN,
                FSMTransitionCheckFloat.Comparison.GreaterThanEqual).Update(user, default), Is.False);
            Assert.That(new FSMTransitionCheckFloat(machine, Name(), key, 2f,
                (FSMTransitionCheckFloat.Comparison)77, 1f).Update(user, default), Is.False);
            Assert.That(new FSMTransitionCheckFloat(machine, Name(), key, 1f,
                FSMTransitionCheckFloat.Comparison.GreaterThan).Update(user, default), Is.True);
            Assert.That(new FSMTransitionCheckFloat(machine, Name(), key, 2f,
                FSMTransitionCheckFloat.Comparison.GreaterThanEqual).Update(user, default), Is.True);
            Assert.That(new FSMTransitionCheckFloat(machine, Name(), key, 3f,
                FSMTransitionCheckFloat.Comparison.LessThan).Update(user, default), Is.True);
            Assert.That(new FSMTransitionCheckFloat(machine, Name(), key, 2f,
                FSMTransitionCheckFloat.Comparison.LessThanEqual).Update(user, default), Is.True);
        }

        [Test]
        public void CheckFloatRangeKeepsInclusiveBoundsNaNAndMissingReadSemantics()
        {
            var machine = Machine(); var user = User(); var key = new GraphStorageKey(Name());
            var range = new FSMTransitionCheckFloatInRange(machine, Name(), key, -1f, 1f);
            Assert.That(range.Update(user, default), Is.True);
            Assert.That(user.Storage.GetCollection().ContainsKey(key), Is.False);
            foreach (float boundary in new[] { -1f, 1f })
            { user.Storage.SetValue(key, boundary); Assert.That(range.Update(user, default), Is.True); }
            foreach (float outside in new[] { -2f, 2f, float.NaN })
            { user.Storage.SetValue(key, outside); Assert.That(range.Update(user, default), Is.False); }
            user.Storage.SetValue(key, 1.5f);
            Assert.That(new FSMTransitionCheckFloatInRange(machine, Name(), key, 2f, 1f).Update(user, default), Is.False);
            Assert.That(new FSMTransitionCheckFloatInRange(machine, Name(), key, float.NaN, 2f).Update(user, default), Is.False);
            Assert.That(new FSMTransitionCheckFloatInRange(machine, Name(), key, 0f, float.NaN).Update(user, default), Is.False);
        }

        [Test]
        public void CheckIntUsesSignedComparisonsAndMissingZeroWithoutInsertion()
        {
            var machine = Machine(); var user = User(); var key = new GraphStorageKey(Name());
            var missing = new FSMTransitionCheckInt(machine, Name(), key, 0, FSMTransitionCheckInt.Comparison.Equal);
            Assert.That(missing.Update(user, default), Is.True);
            Assert.That(user.Storage.GetCollection().ContainsKey(key), Is.False);
            user.Storage.SetValue(key, int.MinValue);
            Assert.That(new FSMTransitionCheckInt(machine, Name(), key, 0,
                FSMTransitionCheckInt.Comparison.LessThan).Update(user, default), Is.True);
            Assert.That(new FSMTransitionCheckInt(machine, Name(), key, 0,
                FSMTransitionCheckInt.Comparison.GreaterThan).Update(user, default), Is.False);
            Assert.That(new FSMTransitionCheckInt(machine, Name(), key, int.MinValue,
                FSMTransitionCheckInt.Comparison.LessThanEqual).Update(user, default), Is.True);
            Assert.That(new FSMTransitionCheckInt(machine, Name(), key, int.MinValue,
                FSMTransitionCheckInt.Comparison.GreaterThanEqual).Update(user, default), Is.True);
            Assert.That(new FSMTransitionCheckInt(machine, Name(), key, int.MinValue,
                FSMTransitionCheckInt.Comparison.Equal).Update(user, default), Is.True);
            Assert.That(new FSMTransitionCheckInt(machine, Name(), key, int.MinValue,
                (FSMTransitionCheckInt.Comparison)77).Update(user, default), Is.False);
        }

        [Test]
        public void DiceRollUsesGenuineRangeBeforeStorageAndRestoresOwnedRandomState()
        {
            Random.State saved = Random.state;
            try
            {
                var machine = Machine(); var user = User(); var key = new GraphStorageKey(Name());
                var dice = new FSMStateDiceRoll(machine, Name(), key, -2f, 3f);
                Random.InitState(1532034); float expected = Random.Range(-2f, 3f); float expectedNext = Random.value;
                Random.InitState(1532034); dice.OnEnter(user, null);
                Assert.That(user.Storage.GetValueOnly<float>(key), Is.EqualTo(expected));
                Assert.That(Random.value, Is.EqualTo(expectedNext));
                Random.InitState(1532034);
                Assert.Throws<NullReferenceException>(() => dice.OnEnter(null, null));
                Assert.That(Random.value, Is.EqualTo(expectedNext), "range occurs before the genuine null-user fault");
                Assert.That(dice.StorageKey, Is.EqualTo(key)); Assert.That(dice.Low, Is.EqualTo(-2f)); Assert.That(dice.High, Is.EqualTo(3f));
            }
            finally { Random.state = saved; }
        }

        [Test]
        public void GenuineFactoriesRetainKeysCaseInsensitiveEnumsAndJsonRoundTrips()
        {
            var machine = Machine(); var user = User();
            string name = Name(), node = Name(), graph = Name();
            // Plain owned observation data, never a runtime provider or replacement DTO.
            string common = "\"Name\":\"" + name + "\",\"Node\":\"" + node + "\",\"FSM\":\"" + graph + "\"";
            var key = new GraphStorageKey(name, node, graph);
            var dice = (FSMStateDiceRoll)FSMStateDiceRoll.ConstructInstance(machine, Name(), "{" + common + ",\"Low\":-2,\"High\":3}");
            Assert.That(dice.StorageKey, Is.EqualTo(key));
            var dr = JsonUtility.FromJson<NumericJsonObservation>(dice.SerialiseRuntimeToJSON());
            AssertKey(dr, name, node, graph); Assert.That(dr.Low, Is.EqualTo(-2f)); Assert.That(dr.High, Is.EqualTo(3f));
            var range = (FSMTransitionCheckFloatInRange)FSMTransitionCheckFloatInRange.ConstructInstance(machine, Name(), "{" + common + ",\"Low\":-1,\"High\":1}");
            Assert.That(range.StorageKey, Is.EqualTo(key)); Assert.That(machine.Transitions[range.TransitionId], Is.SameAs(range));
            var rr = JsonUtility.FromJson<NumericJsonObservation>(range.SerialiseRuntimeToJSON());
            AssertKey(rr, name, node, graph); Assert.That(rr.Low, Is.EqualTo(-1f)); Assert.That(rr.High, Is.EqualTo(1f));
            var f = (FSMTransitionCheckFloat)FSMTransitionCheckFloat.ConstructInstance(machine, Name(), "{" + common + ",\"TriggerPoint\":2,\"Tolerance\":0.25,\"Compare\":\"eQuAl\"}");
            Assert.That(f.Compare, Is.EqualTo(FSMTransitionCheckFloat.Comparison.Equal)); Assert.That(f.Tolerance, Is.EqualTo(0.25f));
            var fr = JsonUtility.FromJson<NumericJsonObservation>(f.SerialiseRuntimeToJSON());
            AssertKey(fr, name, node, graph); Assert.That(fr.Compare, Is.EqualTo("Equal")); Assert.That(fr.TriggerPoint, Is.EqualTo(2f)); Assert.That(fr.Tolerance, Is.EqualTo(0.25f));
            var i = (FSMTransitionCheckInt)FSMTransitionCheckInt.ConstructInstance(machine, Name(), "{" + common + ",\"TriggerPoint\":-7,\"Compare\":\"gReAtErThAn\"}");
            Assert.That(i.Compare, Is.EqualTo(FSMTransitionCheckInt.Comparison.GreaterThan)); Assert.That(i.TriggerPoint, Is.EqualTo(-7));
            var ir = JsonUtility.FromJson<NumericJsonObservation>(i.SerialiseRuntimeToJSON());
            AssertKey(ir, name, node, graph); Assert.That(ir.Compare, Is.EqualTo("GreaterThan")); Assert.That(ir.TriggerPoint, Is.EqualTo(-7f));
            var sf = (FSMStateSetFloat)FSMStateSetFloat.ConstructInstance(machine, Name(), "{" + common + ",\"Value\":7.5,\"Operation\":0}");
            sf.OnEnter(user, null); Assert.That(user.Storage.GetValueOnly<float>(key), Is.EqualTo(7.5f));
            user.Storage.RemoveValue<float>(key);
            var si = (FSMStateSetInt)FSMStateSetInt.ConstructInstance(machine, Name(), "{" + common + ",\"Value\":-9,\"Operation\":0}");
            si.OnEnter(user, null); Assert.That(user.Storage.GetValueOnly<int>(key), Is.EqualTo(-9));
            var unnamed = (FSMTransitionCheckInt)FSMTransitionCheckInt.ConstructInstance(machine, Name(), "{" + common + ",\"TriggerPoint\":0,\"Compare\":\"77\"}");
            Assert.That((int)unnamed.Compare, Is.EqualTo(77));
            Assert.That(JsonUtility.FromJson<NumericJsonObservation>(unnamed.SerialiseRuntimeToJSON()).Compare, Is.EqualTo("77"));
            int count = machine.Transitions.Count;
            Assert.Throws<ArgumentException>(() => FSMTransitionCheckInt.ConstructInstance(machine, Name(), "{" + common + ",\"Compare\":\"not-valid\"}"));
            Assert.That(machine.Transitions.Count, Is.EqualTo(count), "enum fault precedes registration");
        }

        [Test]
        public void NullSetArgumentsRetainNormalBaseRegistrationBeforeFailure()
        {
            var machine = Machine(); FSMIdentifier f = Name(), i = Name(); int count = machine.States.Count;
            Assert.Throws<NullReferenceException>(() => new FSMStateSetFloat(machine, f, null));
            Assert.That(machine.States.Count, Is.EqualTo(count + 1));
            Assert.That(machine.States[f.Id], Is.TypeOf<FSMStateSetFloat>());
            Assert.Throws<NullReferenceException>(() => new FSMStateSetInt(machine, i, null));
            Assert.That(machine.States.Count, Is.EqualTo(count + 2));
            Assert.That(machine.States[i.Id], Is.TypeOf<FSMStateSetInt>());
        }

        [Test]
        public void ShortcutConstructorsRetainOriginalDefaultIdentifierRoles()
        {
            var machine = Machine(); string variable = Name(); var key = new GraphStorageKey(variable);
            string keyText = "null_null_" + variable;
            Assert.That(key.ToString(), Is.EqualTo(keyText));
            var dice = new FSMStateDiceRoll(machine, key, -2f, 3f);
            var range = new FSMTransitionCheckFloatInRange(machine, key, -1f, 1f);
            var integer = new FSMTransitionCheckInt(machine, key, -7, FSMTransitionCheckInt.Comparison.Equal);
            var equal = new FSMTransitionCheckFloat(machine, key, 2.5f, FSMTransitionCheckFloat.Comparison.Equal, 0.25f);
            var greater = new FSMTransitionCheckFloat(machine, key, 2.5f, FSMTransitionCheckFloat.Comparison.GreaterThan, 99f);
            string number = 2.5f.ToString(CultureInfo.CurrentCulture), tolerance = 0.25f.ToString(CultureInfo.CurrentCulture);
            string minusTwo = (-2f).ToString(CultureInfo.CurrentCulture), minusOne = (-1f).ToString(CultureInfo.CurrentCulture);
            string minusSeven = (-7).ToString(CultureInfo.CurrentCulture);
            Assert.That(FSMState.LookupNameUsingId(dice.StateId), Is.EqualTo("DiceRoll_" + keyText + "_" + minusTwo + "_3"));
            Assert.That(FSMTransition.LookupNameUsingId(range.TransitionId), Is.EqualTo("CheckFloatInRange_" + keyText + "_" + minusOne + "_1"));
            Assert.That(FSMTransition.LookupNameUsingId(integer.TransitionId), Is.EqualTo("CheckInt_" + keyText + "_Equal_" + minusSeven));
            Assert.That(FSMTransition.LookupNameUsingId(equal.TransitionId), Is.EqualTo("CheckFloat_" + keyText + "_Equal_" + number + "_" + tolerance));
            Assert.That(FSMTransition.LookupNameUsingId(greater.TransitionId), Is.EqualTo("CheckFloat_" + keyText + "_GreaterThan_" + number));
            Assert.That(machine.States[dice.StateId], Is.SameAs(dice));
            Assert.That(machine.Transitions[greater.TransitionId], Is.SameAs(greater));
        }

        [Test]
        public void NumericFamiliesAndTooltipKeepGenuineMetadataContracts()
        {
            Type[] families = { typeof(FSMStateDiceRoll), typeof(FSMStateSetFloat), typeof(FSMStateSetInt),
                typeof(FSMTransitionCheckFloat), typeof(FSMTransitionCheckFloatInRange), typeof(FSMTransitionCheckInt) };
            for (int index = 0; index < families.Length; index++)
            {
                Type type = families[index];
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityCore.Runtime"));
                Assert.That(type.Namespace, Is.EqualTo("Hardlight")); Assert.That(type.IsSealed, Is.False);
                Assert.That(type.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Core/{0}"));
                var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
                Option[] order = index < 2 ? new[] { Option.ArrayBoundsChecks, Option.NullChecks }
                    : new[] { Option.NullChecks, Option.ArrayBoundsChecks };
                Assert.That(options.Select(o => o.Option).ToArray(), Is.EqualTo(order));
                Assert.That(options.All(o => Equals(o.Value, false)), Is.True);
                Type dto = type.GetNestedType("JSONCtorArgs", BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(dto, Is.Not.Null); Assert.That(dto.IsSerializable, Is.True);
                Assert.That(dto.IsNestedPublic, Is.EqualTo(index == 1 || index == 2));
                Assert.That(dto.GetField("Name"), Is.Not.Null); Assert.That(dto.GetField("Node"), Is.Not.Null); Assert.That(dto.GetField("FSM"), Is.Not.Null);
            }
            Assert.That(Enum.GetNames(typeof(FSMStateSetFloat.Operation)), Is.EqualTo(new[] { "Set", "Add", "Multiply" }));
            Assert.That(Enum.GetNames(typeof(FSMStateSetInt.Operation)), Is.EqualTo(new[] { "Set", "Add", "Multiply" }));
            Assert.That(Enum.GetValues(typeof(FSMTransitionCheckFloat.Comparison)).Cast<FSMTransitionCheckFloat.Comparison>().Select(v => (int)v).ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(Enum.GetValues(typeof(FSMTransitionCheckInt.Comparison)).Cast<FSMTransitionCheckInt.Comparison>().Select(v => (int)v).ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Type diceDto = typeof(FSMStateDiceRoll).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(diceDto.GetCustomAttribute<GraphTooltipAttribute>(false).Tooltip,
                Is.EqualTo("Simulates a random dice roll using the provided `Low` & `High` parameters."));
            var usage = typeof(GraphTooltipAttribute).GetCustomAttribute<AttributeUsageAttribute>(false);
            Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Class | AttributeTargets.Field));
            Assert.That(usage.AllowMultiple, Is.False); Assert.That(usage.Inherited, Is.True);
            FieldInfo tooltip = typeof(GraphTooltipAttribute).GetField("Tooltip");
            Assert.That(tooltip.IsPublic && tooltip.IsInitOnly, Is.True);
            string text = new string('x', 5);
            Assert.That(new GraphTooltipAttribute(text).Tooltip, Is.SameAs(text));
            Assert.That(new GraphTooltipAttribute(null).Tooltip, Is.Null);
        }

        [Serializable]
        private sealed class NumericJsonObservation
        {
            public string Name, Node, FSM, Compare;
            public float Low, High, TriggerPoint, Tolerance;
        }
        private static void AssertKey(NumericJsonObservation value, string name, string node, string graph)
        {
            Assert.That(value.Name, Is.EqualTo(name)); Assert.That(value.Node, Is.EqualTo(node)); Assert.That(value.FSM, Is.EqualTo(graph));
        }
    }
}
