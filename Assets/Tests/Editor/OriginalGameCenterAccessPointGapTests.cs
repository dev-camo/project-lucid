using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Original GameCenterAccessPoint forwarding targets. The offline plugin
    // reports -1 from every geometry slot and the static readonly plugin field
    // is not replaceable, so the forwarding call is read from the managed body.
    public sealed class OriginalGameCenterAccessPointGapTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // 0x0600050a x86-64 0x1ace7e0 and 0x06000509 x86-64 0x1ace780 both
        // tail-call vtable +0x238 (GetAccessPointSizeWidth); 0x06000508 x86-64
        // 0x1ace720 uses +0x228, its own screen-coordinate height slot. Fails if
        // the width facade is "corrected" to the screen-coordinate width slot.
        [Test]
        public void ScreenCoordinateWidthForwardsToThePluginWidthSlot()
        {
            MethodInfo width = PluginMethod("GetAccessPointSizeWidth");
            Assert.That(PluginMethod("GetAccessPointSizeScreenCoordinateWidth"), Is.Not.Null, "The plugin still declares the unused slot.");
            Assert.That(Calls("GetAccessPointSizeScreenCoordinateWidth"), Is.EqualTo(new[] { width }));
            Assert.That(Calls("GetAccessPointSizeWidth"), Is.EqualTo(new[] { width }));
            // Control: the neighbouring height facade keeps its own slot.
            Assert.That(Calls("GetAccessPointSizeScreenCoordinateHeight"),
                Is.EqualTo(new[] { PluginMethod("GetAccessPointSizeScreenCoordinateHeight") }));
        }

        private static MethodInfo PluginMethod(string name) =>
            typeof(GameCenterAccessPointPlugin).GetMethods(Declared).Single(m => m.Name == name);

        // Decodes the facade body with the ECMA-335 opcode table and returns
        // every call/callvirt target in order.
        private static MethodBase[] Calls(string facade)
        {
            MethodInfo method = typeof(GameCenterAccessPoint).GetMethods(Declared).Single(m => m.Name == facade);
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            var targets = new List<MethodBase>();
            for (int at = 0; at < il.Length;)
            {
                OpCode code;
                if (il[at] == 0xfe) { code = TwoByte[il[at + 1]]; at += 2; }
                else { code = OneByte[il[at]]; at += 1; }
                int size = OperandSize(code.OperandType, il, at);
                if (code == OpCodes.Call || code == OpCodes.Callvirt)
                    targets.Add(method.Module.ResolveMethod(BitConverter.ToInt32(il, at)));
                at += size;
            }
            return targets.ToArray();
        }

        private static int OperandSize(OperandType type, byte[] il, int at)
        {
            switch (type)
            {
                case OperandType.InlineNone: return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: return 1;
                case OperandType.InlineVar: return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR: return 8;
                case OperandType.InlineSwitch: return 4 + 4 * BitConverter.ToInt32(il, at);
                default: return 4;
            }
        }

        private static readonly Dictionary<byte, OpCode> OneByte = new Dictionary<byte, OpCode>();
        private static readonly Dictionary<byte, OpCode> TwoByte = new Dictionary<byte, OpCode>();

        static OriginalGameCenterAccessPointGapTests()
        {
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var code = (OpCode)field.GetValue(null);
                if (code.Size == 1) OneByte[(byte)code.Value] = code;
                else TwoByte[(byte)(code.Value & 0xff)] = code;
            }
        }
    }
}
