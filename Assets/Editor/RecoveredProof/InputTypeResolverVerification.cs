// Project Lucid controlled checks for the original input-type resolver.
using System;
using System.Reflection;
using Hardlight;

namespace ProjectLucid.Verification
{
    public static class InputTypeResolverVerification
    {
        public static void VerifyTouchResolution()
        {
            Type type = typeof(InputTypeResolver);
            FieldInfo touch = type.GetField("m_touchDevice", BindingFlags.Static | BindingFlags.NonPublic);
            Check(touch != null && touch.FieldType == typeof(bool) && touch.IsPrivate && touch.IsStatic,
                "The original private static touch flag is retained.");
            Check(type.TypeInitializer == null,
                "No platform detector or static initializer is added to the original resolver.");
            bool saved = (bool)touch.GetValue(null);
            try
            {
                touch.SetValue(null, false);
                Check((int)InputTypeResolver.ResolveTouchInput() == -1482781790,
                    "The original false flag resolves to the signed Mouse identifier.");
                touch.SetValue(null, true);
                Check((int)InputTypeResolver.ResolveTouchInput() == -31649603,
                    "The original true flag resolves to the signed Touch identifier.");
                Check((int)InputTypeResolver.ResolveButtonInput(-1, InputType.Unsupported) == -1162288346,
                    "The touch flag does not change the original keyboard fallback.");
                Check((int)InputTypeResolver.ResolveButtonInput(0, InputType.Unsupported) == 897719803,
                    "The touch flag does not change the original controller fallback.");
            }
            finally
            {
                touch.SetValue(null, saved);
            }
            Check((bool)touch.GetValue(null) == saved, "Restore the shared flag after controlled verification.");
        }

        public static void VerifyButtonResolution()
        {
            // Expected results come from the shipped signed-index branch and
            // original enum identifiers. Use literal rows for its boundary cases.
            int[] indexes = { int.MinValue, -2, -1, 0, 1, 42, int.MaxValue };
            int[] fallbacks = { -1162288346, -1162288346, -1162288346,
                897719803, 897719803, 897719803, 897719803 };
            for (int i = 0; i < indexes.Length; ++i)
            {
                Check((int)InputTypeResolver.ResolveButtonInput(indexes[i], InputType.Unsupported, false) == fallbacks[i],
                    "Original signed joystick fallback at " + indexes[i]);
                Check((int)InputTypeResolver.ResolveButtonInput(indexes[i], InputType.Unsupported) == fallbacks[i],
                    "The omitted mouse argument retains its original false default.");
                Check((int)InputTypeResolver.ResolveButtonInput(indexes[i], InputType.Unsupported, true) == -1482781790,
                    "Original mouse fallback takes precedence over the joystick index.");
            }

            // Overrides are opaque identifiers. Cover every shipped value and
            // unknown signed values, including zero, rather than accepting only named enums.
            int[] overrides = { 897719803, -1162288346, -594121215, -1482781790,
                -1000566503, -1901056579, 1329525373, 1437681611, -31649603,
                1838982690, -1984142849, int.MinValue, -1, 0, 1, int.MaxValue };
            foreach (int value in overrides)
                foreach (int index in indexes)
                {
                    Check((int)InputTypeResolver.ResolveButtonInput(index, (InputType)value, false) == value,
                        "An explicit override retains all signed bits.");
                    Check((int)InputTypeResolver.ResolveButtonInput(index, (InputType)value, true) == value,
                        "An explicit override takes precedence over mouse fallback.");
                }

            MethodInfo method = typeof(InputTypeResolver).GetMethod("ResolveButtonInput", BindingFlags.Public | BindingFlags.Static);
            ParameterInfo mouse = method.GetParameters()[2];
            Check(mouse.Name == "isMouse" && mouse.ParameterType == typeof(bool) && mouse.IsOptional &&
                mouse.HasDefaultValue && Equals(mouse.DefaultValue, false),
                "The original optional parameter identity and false default are preserved.");
            Check((int)InputTrigger.Held == 0 && (int)InputTrigger.Down == 1 && (int)InputTrigger.Up == 2,
                "Original trigger identifiers retain their declared order and values.");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
