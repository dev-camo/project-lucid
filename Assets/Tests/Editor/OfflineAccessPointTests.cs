using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using NUnit.Framework;

#if PROJECT_LUCID_ORIGINAL_GAME_CENTER_ACCESS_POINT
#error Offline access-point fixtures require the default local plugin boundary.
#endif

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
                List<MemberInfo> operands = Operands(method);
                FieldInfo[] fields = operands.OfType<FieldInfo>().ToArray();
                Assert.That(fields.Length, Is.EqualTo(1), facade);
                Assert.That(fields[0].Module == plugin.Module && fields[0].MetadataToken == plugin.MetadataToken, Is.True, facade);
                MethodBase target = operands.OfType<MethodBase>().Single();
                Assert.That(target.DeclaringType, Is.EqualTo(typeof(GameCenterAccessPointPlugin)), facade);
                Assert.That(target.Name, Is.EqualTo(slot), facade);
            }
            MethodBase[] show = Operands(typeof(GameCenterAccessPoint).GetMethod("ShowAccessPoint"))
                .OfType<MethodBase>().Where(m => m.DeclaringType == typeof(GameCenterAccessPointPlugin)).ToArray();
            Assert.That(show, Is.Not.Empty);
            Assert.That(show.Select(m => m.Name).Distinct(), Is.EqualTo(new[] { "ShowAccessPoint" }));
        }

        // Minimal ECMA-335 operand walk: returns the field and method tokens a
        // body references, in order. Other operands are skipped by their size.
        private static List<MemberInfo> Operands(MethodInfo method)
        {
            var opcodes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)
                .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => (ushort)o.Value);
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            var members = new List<MemberInfo>();
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
                    case OperandType.InlineField: members.Add(method.Module.ResolveField(BitConverter.ToInt32(il, at))); at += 4; break;
                    case OperandType.InlineMethod: members.Add(method.Module.ResolveMethod(BitConverter.ToInt32(il, at))); at += 4; break;
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
