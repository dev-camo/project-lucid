using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OfflineAccessPointTests
    {
        [Test]
        public void DefaultFacadeReportsUnavailableHiddenUnfocusedAndNotPresenting()
        {
            AssertUnavailable();
            AssertGeometryUnavailable();
        }

        [Test]
        public void ShowHideFocusAndHighlightsCannotAcquirePlatformState()
        {
            GameCenterAccessPoint.ShowGameCenter();
            GameCenterAccessPoint.ShowAchievements();
            foreach (GameCenterAccessPoint.AccessPointLocation location in new[]
            {
                GameCenterAccessPoint.AccessPointLocation.TopLeading,
                GameCenterAccessPoint.AccessPointLocation.TopTrailing,
                GameCenterAccessPoint.AccessPointLocation.BottomLeading,
                GameCenterAccessPoint.AccessPointLocation.BottomTrailing
            })
            {
                GameCenterAccessPoint.ShowAccessPoint(location, true);
                GameCenterAccessPoint.SetAccessPointFocused(true);
                GameCenterAccessPoint.SetShowHighlights(true);
                AssertUnavailable();
                GameCenterAccessPoint.HideAccessPoint();
                GameCenterAccessPoint.ShowAccessPoint(location, false);
                GameCenterAccessPoint.SetAccessPointFocused(false);
                GameCenterAccessPoint.SetShowHighlights(false);
                AssertUnavailable();
            }
        }

        [Test]
        public void UnavailableGeometryRemainsStableAfterRepeatedPresentationRequests()
        {
            AssertGeometryUnavailable();
            for (int i = 0; i < 3; i++)
            {
                GameCenterAccessPoint.ShowAccessPoint(GameCenterAccessPoint.AccessPointLocation.TopLeading, true);
                GameCenterAccessPoint.SetAccessPointFocused(true);
                AssertGeometryUnavailable();
                GameCenterAccessPoint.HideAccessPoint();
                GameCenterAccessPoint.SetAccessPointFocused(false);
                AssertGeometryUnavailable();
            }
        }

        // The -1 sentinels above cannot show which plugin slot each facade getter
        // reads. Original x86-64 facade bodies load s_gameCenterAccessPointPlugin and
        // dispatch one vtable slot (klass vtable at +0x138, 16 bytes per slot), e.g.
        // 0x06000504 at 0x1ace5a0 reads +0x1e8 = slot 11 GetAccessPointScreenCoordinateX,
        // and both 0x06000509 at 0x1ace780 and 0x0600050a at 0x1ace7e0 read +0x238 =
        // slot 16 GetAccessPointSizeWidth (the width quirk). ShowAccessPoint 0x06000500
        // at 0x1ace270 dispatches only slot 7. Catches a getter forwarding to a
        // neighbouring slot or the quirk being "fixed" to slot 17.
        [Test]
        public void FacadeForwardsEachCallToItsOriginalPluginSlotIncludingWidthQuirk()
        {
            var expected = new (string Facade, string Plugin)[]
            {
                ("ShowGameCenter", "Show"), ("ShowAchievements", "ShowAchievements"),
                ("IsAccessPointAvailable", "IsAccessPointAvailable"), ("IsAccessPointShown", "IsAccessPointShown"),
                ("HideAccessPoint", "HideAccessPoint"), ("GetAccessPointOriginX", "GetAccessPointOriginX"),
                ("GetAccessPointOriginScreenCoordinateX", "GetAccessPointScreenCoordinateX"),
                ("GetAccessPointOriginY", "GetAccessPointOriginY"),
                ("GetAccessPointScreenCoordinateY", "GetAccessPointScreenCoordinateY"),
                ("GetAccessPointSizeHeight", "GetAccessPointSizeHeight"),
                ("GetAccessPointSizeScreenCoordinateHeight", "GetAccessPointSizeScreenCoordinateHeight"),
                ("GetAccessPointSizeWidth", "GetAccessPointSizeWidth"),
                ("GetAccessPointSizeScreenCoordinateWidth", "GetAccessPointSizeWidth"),
                ("IsPresentingGameCenter", "IsPresentingGameCenter"), ("SetAccessPointFocused", "SetAccessPointFocused"),
                ("IsAccessPointFocused", "IsAccessPointFocused"), ("SetShowHighlights", "SetShowHighlights"),
            };
            FieldInfo plugin = typeof(GameCenterAccessPoint).GetField("s_gameCenterAccessPointPlugin", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(plugin, Is.Not.Null);
            foreach (var (facade, slot) in expected)
            {
                MethodInfo method = typeof(GameCenterAccessPoint).GetMethod(facade, BindingFlags.Static | BindingFlags.Public);
                Assert.That(method, Is.Not.Null, facade);
                List<(OpCode Code, MemberInfo Member)> operands = Operands(method);
                FieldInfo[] fields = operands.Select(operand => operand.Member).OfType<FieldInfo>().ToArray();
                Assert.That(fields.Length, Is.EqualTo(1), facade);
                Assert.That(fields[0].Module == plugin.Module && fields[0].MetadataToken == plugin.MetadataToken, Is.True, facade);
                Assert.That(operands.Count(operand => operand.Member is MethodBase), Is.EqualTo(1), facade + " has one method token");
                MethodBase target = PluginDispatches(method).Single();
                Assert.That(target.Name, Is.EqualTo(slot), facade);
            }
            MethodBase[] show = PluginDispatches(typeof(GameCenterAccessPoint).GetMethod("ShowAccessPoint"));
            Assert.That(show, Is.Not.Empty);
            Assert.That(show.Select(m => m.Name).Distinct(), Is.EqualTo(new[] { "ShowAccessPoint" }));
        }

        // Delegates to plugin slots built through the same static field and never
        // invoked: ldsfld; dup; ldvirtftn; newobj (ECMA-335 III.4.18). A facade
        // written this way performs no dispatch.
        private static class DelegateOnly
        {
            public static GameCenterAccessPointPlugin Plugin;

            public static object Sink;

            public static void Show() => Sink = new Action(Plugin.Show);

            public static void ShowAccessPoint() => Sink = new Action<int, bool>(Plugin.ShowAccessPoint);
        }

        // The forwarding witness counts only callvirt, the IL form of the native
        // vtable dispatch, so a delegate load of the right slot is not forwarding.
        [Test]
        public void PluginSlotDelegatesWithoutDispatchAreNotForwardingCalls()
        {
            foreach (var (body, slot) in new[] { ("Show", "Show"), ("ShowAccessPoint", "ShowAccessPoint") })
            {
                MethodInfo method = typeof(DelegateOnly).GetMethod(body);
                List<(OpCode Code, MemberInfo Member)> operands = Operands(method);
                Assert.That(operands.Where(operand => operand.Member is MethodBase m && m.DeclaringType == typeof(GameCenterAccessPointPlugin))
                    .Select(operand => operand.Code.Name + " " + operand.Member.Name), Is.EqualTo(new[] { "ldvirtftn " + slot }), body);
                Assert.That(PluginDispatches(method), Is.Empty, body);
            }
        }

        // The plugin methods a body dispatches through callvirt, in IL order.
        private static MethodBase[] PluginDispatches(MethodInfo method) =>
            Operands(method).Where(operand => operand.Code == OpCodes.Callvirt).Select(operand => operand.Member).OfType<MethodBase>()
                .Where(target => target.DeclaringType == typeof(GameCenterAccessPointPlugin)).ToArray();

        // Minimal ECMA-335 operand walk: returns the field and method tokens a
        // body references, in order, with the opcode that carries each one.
        // Other operands are skipped by their size.
        private static List<(OpCode Code, MemberInfo Member)> Operands(MethodInfo method)
        {
            var opcodes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)
                .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => (ushort)o.Value);
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            var members = new List<(OpCode Code, MemberInfo Member)>();
            for (int at = 0; at < il.Length;)
            {
                ushort value = il[at] == 0xfe ? (ushort)(0xfe00 | il[at + 1]) : il[at];
                OpCode code = opcodes[value];
                at += code.Size;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: at += 1; break;
                    case OperandType.InlineVar: at += 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: at += 8; break;
                    case OperandType.InlineSwitch: at += 4 + 4 * BitConverter.ToInt32(il, at); break;
                    case OperandType.InlineField: members.Add((code, method.Module.ResolveField(BitConverter.ToInt32(il, at)))); at += 4; break;
                    case OperandType.InlineMethod: members.Add((code, method.Module.ResolveMethod(BitConverter.ToInt32(il, at)))); at += 4; break;
                    default: at += 4; break;
                }
            }
            return members;
        }

        private static void AssertUnavailable()
        {
            Assert.That(GameCenterAccessPoint.IsAccessPointAvailable(), Is.False);
            Assert.That(GameCenterAccessPoint.IsAccessPointShown(), Is.False);
            Assert.That(GameCenterAccessPoint.IsAccessPointFocused(), Is.False);
            Assert.That(GameCenterAccessPoint.IsPresentingGameCenter(), Is.False);
        }

        // Offline plugin policy reusing the original unsupported plugin's
        // sentinel: e.g. 0x0600054d at x86-64 0x1acef40 returns -1f after logging.
        private static void AssertGeometryUnavailable()
        {
            Assert.That(GameCenterAccessPoint.GetAccessPointOriginX(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointOriginScreenCoordinateX(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointOriginY(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointScreenCoordinateY(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointSizeHeight(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointSizeScreenCoordinateHeight(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointSizeWidth(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointSizeScreenCoordinateWidth(), Is.EqualTo(-1f));
        }
    }
}
