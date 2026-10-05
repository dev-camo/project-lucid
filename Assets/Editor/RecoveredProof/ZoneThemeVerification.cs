using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ProjectLucid
{
    public static class ZoneThemeVerification
    {
        private static FieldInfo Field(string name) => typeof(ZoneThemeOverride).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        public static int RunManaged()
        {
            int checks = 0;
            Action<bool, string> check = (value, label) =>
            {
                if (!value) throw new InvalidOperationException(label);
                checks++;
            };
            Type type = typeof(ZoneThemeOverride);
            check(type.BaseType == typeof(ScriptableObject) && type.IsPublic && !type.IsSealed, "original real base/public type");
            check(type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 5, "all five original fields");
            foreach (string name in new[] { "m_zoneAccentOverride", "m_zoneGradientOverride", "m_backgroundStartColour", "m_backgroundEndColour" })
                check(Field(name).IsDefined(typeof(SerializeField), false), name + " original serialization");
            foreach (string name in new[] { "m_backgroundStartColour", "m_backgroundEndColour" })
            {
                var attribute = (ShowIfAttribute)Field(name).GetCustomAttributes(typeof(ShowIfAttribute), false).Single();
                check(attribute.ConditionalFields.Length == 1 && attribute.ConditionalFields[0].FieldName == "m_zoneGradientOverride"
                    && attribute.ConditionalFields[0].ComparisonValue == null, name + " exact condition metadata");
            }
            check(Field("<ZoneAccentOverride>k__BackingField").FieldType == typeof(ManagedAddressableAsset<Sprite>), "wrapper field type");
            check(type.GetProperty("ZoneAccentOverride").GetSetMethod(true).IsPrivate, "original private setter");
            var menu = (CreateAssetMenuAttribute)type.GetCustomAttributes(typeof(CreateAssetMenuAttribute), false).Single();
            check(menu.fileName == "ZoneThemeOverride" && menu.menuName == "HardlightProject/DefinitionData/Definitions/ZoneThemeOverride", "exact menu metadata");
            var options = type.GetCustomAttributes(typeof(Il2CppSetOptionAttribute), false).Cast<Il2CppSetOptionAttribute>().ToArray();
            check(options.Length == 2 && options.Any(a => a.Option == Option.ArrayBoundsChecks && Equals(a.Value, false))
                && options.Any(a => a.Option == Option.NullChecks && Equals(a.Value, false)), "exact compiler options");
            return checks;
        }

        // Actual Unity only: no user asset loading, mocks, generated gameplay
        // dependencies or uninitialized engine objects are used by this proof.
        public static void Run()
        {
            int checks = RunManaged();
            ZoneThemeOverride theme = ScriptableObject.CreateInstance<ZoneThemeOverride>();
            Action<bool, string> check = (value, label) =>
            {
                if (!value) throw new InvalidOperationException(label);
                checks++;
            };
            Func<Color, Color, bool> exact = (a, b) => a.r.Equals(b.r) && a.g.Equals(b.g) && a.b.Equals(b.b) && a.a.Equals(b.a);
            try
            {
                check(exact((Color)Field("m_backgroundStartColour").GetValue(theme), Color.white), "original constructor start white");
                check(exact((Color)Field("m_backgroundEndColour").GetValue(theme), Color.white), "original constructor end white");
                check(!(bool)Field("m_zoneGradientOverride").GetValue(theme), "original constructor gradient false");
                check(theme.ZoneAccentOverride != null && !theme.ZoneAccentOverride.IsValid(), "OnEnable fresh null-ref wrapper");
                ManagedAddressableAsset<Sprite> firstWrapper = theme.ZoneAccentOverride;
                MethodInfo enable = typeof(ZoneThemeOverride).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
                enable.Invoke(theme, null);
                check(!ReferenceEquals(firstWrapper, theme.ZoneAccentOverride), "every OnEnable replaces wrapper");
                var reference = new AssetReferenceAtlasedSprite("00112233445566778899aabbccddeeff");
                AssetReferenceAtlasedSprite supplied = reference;
                Color start = new Color(2, 3, 4, 5), end = new Color(6, 7, 8, 9);
                theme.Apply(ref supplied, ref start, ref end);
                check(supplied == null, "null sprite override clears supplied reference");
                check(exact(start, new Color(2, 3, 4, 5)) && exact(end, new Color(6, 7, 8, 9)), "false gradient preserves both colors");
                Field("m_backgroundStartColour").SetValue(theme, new Color(-1, 2, float.NaN, float.PositiveInfinity));
                Field("m_backgroundEndColour").SetValue(theme, new Color(4, -5, 6, 0));
                Field("m_zoneGradientOverride").SetValue(theme, true);
                theme.Apply(ref start, ref end);
                check(exact(start, new Color(-1, 2, float.NaN, float.PositiveInfinity)), "raw start floats retained");
                check(exact(end, new Color(4, -5, 6, 0)), "raw end floats retained");
                Color alias = Color.black;
                theme.Apply(ref alias, ref alias);
                check(exact(alias, end), "aliased color refs use native start-then-end ordering");
                ManagedAddressableAsset<Sprite> suppliedWrapper = firstWrapper;
                theme.GetAccentImage(ref suppliedWrapper);
                check(ReferenceEquals(suppliedWrapper, firstWrapper), "null asset ref preserves caller wrapper");
                Field("m_zoneAccentOverride").SetValue(theme, reference);
                theme.GetAccentImage(ref suppliedWrapper);
                check(ReferenceEquals(suppliedWrapper, theme.ZoneAccentOverride), "raw nonnull ref returns existing stale wrapper");
                enable.Invoke(theme, null);
                check(!ReferenceEquals(suppliedWrapper, theme.ZoneAccentOverride), "changed ref fresh OnEnable wrapper");
                theme.GetAccentImage(ref suppliedWrapper);
                check(ReferenceEquals(suppliedWrapper, theme.ZoneAccentOverride), "nonnull reference returns latest wrapper");
                Field("<ZoneAccentOverride>k__BackingField").SetValue(theme, null);
                suppliedWrapper = firstWrapper;
                theme.GetAccentImage(ref suppliedWrapper);
                check(suppliedWrapper == null, "nonnull ref may clear caller using null current wrapper");
                supplied = null;
                theme.Apply(ref supplied, ref start, ref end);
                check(ReferenceEquals(supplied, reference), "sprite reference retained exactly");
                check(exact(start, new Color(-1, 2, float.NaN, float.PositiveInfinity)) && exact(end, new Color(4, -5, 6, 0)), "three-ref gradient assignment");
            }
            finally { UnityEngine.Object.DestroyImmediate(theme); }
            Debug.Log("Project Lucid ZoneThemeOverride checks=" + checks + "; original field/engine lifecycle, no asset-load parity");
        }
    }
}
