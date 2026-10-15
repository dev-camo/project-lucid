using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // CharacterCustomisationManager..ctor 0x06001359, x86-64 0x75a720: Object..ctor
    // (0x75a7af), GetSystemRef<SaveManager> (0x75a7dd), Action<SaveManager> over
    // <.ctor>b__0_0 (0x75a809), SystemRef.InvokeOnValid (0x75a81e), then the
    // OnLanguageChanged Action (0x75a86e) and FastAction op_Addition (0x75a883)
    // stored to StringTable.StringsTableHandlers. A valid save manager runs the
    // registration callback synchronously, so the order is observable at runtime;
    // constructing the manager here would need a live save system, so the
    // compiled call sequence is checked instead. Catches subscribing to the string
    // table before the save registration, or skipping the base constructor first.
    public sealed class OriginalCharacterCustomisationOrderTests
    {
        private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (OpCode)field.GetValue(null))
            .ToDictionary(code => code.Value);

        [Test]
        public void SaveRegistrationIsRequestedBeforeTheStringTableSubscription()
        {
            ConstructorInfo constructor = typeof(CharacterCustomisationManager).GetConstructor(Type.EmptyTypes);
            Assert.IsNotNull(constructor);
            List<KeyValuePair<OpCode, MemberInfo>> members = Members(constructor);

            int baseCall = Index(members, OpCodes.Call, m => m is ConstructorInfo c && c.DeclaringType == typeof(object));
            int saveRef = Index(members, OpCodes.Call, m => m is MethodInfo method && method.Name == "GetSystemRef" &&
                method.DeclaringType == typeof(ProcessManager) && method.GetGenericArguments().SequenceEqual(new[] { typeof(SaveManager) }));
            int callback = Index(members, OpCodes.Newobj, m => m.DeclaringType == typeof(Action<SaveManager>));
            int invoke = Index(members, OpCodes.Callvirt, m => m.Name == "InvokeOnValid" && m.DeclaringType == typeof(SystemRef<SaveManager>));
            int handler = Index(members, OpCodes.Newobj, m => m.DeclaringType == typeof(Action));
            int load = Index(members, OpCodes.Ldsfld, m => m == typeof(StringTable).GetField("StringsTableHandlers"));
            int store = Index(members, OpCodes.Stsfld, m => m == typeof(StringTable).GetField("StringsTableHandlers"));

            Assert.That(new[] { baseCall, saveRef, callback, invoke, load, handler, store },
                Is.Ordered.And.Unique, "Base construction, save registration, then the string-table subscription.");
        }

        // Each call/newobj/field instruction with its resolved member, in IL order.
        private static List<KeyValuePair<OpCode, MemberInfo>> Members(MethodBase method)
        {
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            Module module = method.Module;
            Type[] typeArguments = method.DeclaringType.GetGenericArguments();
            var result = new List<KeyValuePair<OpCode, MemberInfo>>();
            int offset = 0;
            while (offset < il.Length)
            {
                short value = il[offset] == 0xfe ? (short)(0xfe00 | il[offset + 1]) : il[offset];
                OpCode code = OpCodesByValue[value];
                offset += code.Size;
                int operandSize = OperandSize(code, il, offset);
                if (code.OperandType == OperandType.InlineMethod || code.OperandType == OperandType.InlineField)
                {
                    int token = BitConverter.ToInt32(il, offset);
                    result.Add(new KeyValuePair<OpCode, MemberInfo>(code, module.ResolveMember(token, typeArguments, null)));
                }
                offset += operandSize;
            }
            return result;
        }

        private static int OperandSize(OpCode code, byte[] il, int offset)
        {
            switch (code.OperandType)
            {
                case OperandType.InlineNone: return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: return 1;
                case OperandType.InlineVar: return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR: return 8;
                case OperandType.InlineSwitch: return 4 + 4 * BitConverter.ToInt32(il, offset);
                default: return 4;
            }
        }

        private static int Index(List<KeyValuePair<OpCode, MemberInfo>> members, OpCode code, Func<MemberInfo, bool> match)
        {
            int[] found = Enumerable.Range(0, members.Count)
                .Where(i => members[i].Key == code && match(members[i].Value)).ToArray();
            Assert.AreEqual(1, found.Length, "Exactly one " + code.Name + " instruction matches.");
            return found[0];
        }
    }
}
