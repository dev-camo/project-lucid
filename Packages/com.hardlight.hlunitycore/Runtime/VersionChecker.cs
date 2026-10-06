using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class VersionChecker
    {
        public enum VersionComponent { None = 0, Major = 1, Minor = 2, Point = 3 }

        // HLUnityCore.Runtime:Hardlight.VersionChecker:0x06001018; arm64 0x1b26b18.
        // Split the expected input first, but parse all three current components
        // before the expected components. Parsing exceptions leave the out value
        // untouched; components after point are ignored.
        public static bool IsVersionUpToDate(string currentVersion, string expectedVersion, out VersionComponent outdatedComponent)
        {
            string[] expected = expectedVersion.Split('.', StringSplitOptions.None);
            string[] current = currentVersion.Split('.', StringSplitOptions.None);
            int currentMajor = int.Parse(current[0]);
            int currentMinor = current.Length >= 2 ? int.Parse(current[1]) : 0;
            int currentPoint = current.Length >= 3 ? int.Parse(current[2]) : 0;
            int expectedMajor = int.Parse(expected[0]);
            int expectedMinor = expected.Length >= 2 ? int.Parse(expected[1]) : 0;
            int expectedPoint = expected.Length >= 3 ? int.Parse(expected[2]) : 0;
            if (currentMajor < expectedMajor) { outdatedComponent = VersionComponent.Major; return false; }
            if (currentMajor > expectedMajor) { outdatedComponent = VersionComponent.None; return true; }
            if (currentMinor < expectedMinor) { outdatedComponent = VersionComponent.Minor; return false; }
            if (currentMinor > expectedMinor) { outdatedComponent = VersionComponent.None; return true; }
            if (currentPoint < expectedPoint) { outdatedComponent = VersionComponent.Point; return false; }
            outdatedComponent = VersionComponent.None;
            return true;
        }

        // Original 0x06001019; arm64 0x1b26c90: System.Object constructor only.
        public VersionChecker() { }
    }
}
