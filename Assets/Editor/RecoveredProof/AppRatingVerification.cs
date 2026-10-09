// Project Lucid verification fixture for the original AppRating implementation.
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    public static class AppRatingVerification
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        public static void VerifyUnsupportedProviderAndSilence()
        {
            Type appRatingType = typeof(AppRating);
            Check(appRatingType.TypeInitializer != null && appRatingType.TypeInitializer.IsStatic,
                "AppRating must retain its explicit static initializer.");

            // Verify the loaded routes before exercising the selected provider. Emitted log absence
            // alone cannot detect a wrong route when the genuine output system has no process.
            VerifyLoadedRoutes();
            // This genuine call initializes AppRating and exercises the selected provider.
            AppRating.RequestReview();
            FieldInfo selectedField = appRatingType.GetField("s_appRatingPlugin", PrivateStatic);
            Check(selectedField != null && selectedField.IsPrivate && selectedField.IsInitOnly,
                "AppRating must retain its private readonly provider field.");
            Check(selectedField.FieldType == typeof(AppRatingPlugin),
                "AppRating provider field type must remain AppRatingPlugin.");
            AppRatingPlugin selected = (AppRatingPlugin)selectedField.GetValue(null);
            Check(selected != null && selected.GetType() == typeof(AppRatingPluginUnsupported),
                "The shipped initializer must select AppRatingPluginUnsupported.");

            FieldInfo reasonField = GetUnsupportedField("m_unsupportedReason", typeof(string));
            FieldInfo warningField = GetUnsupportedField("m_warning", typeof(bool));
            string expectedReason = string.Format(
                "App Rating is not supported in platform {0}.", Application.platform);
            Check((bool)warningField.GetValue(selected), "The selected unsupported provider must be warning=true.");
            Check((string)reasonField.GetValue(selected) == expectedReason,
                "The selected provider must retain the platform-formatted unsupported reason.");

            // Repeated original dispatches must remain silent on the retained warning=true provider.
            AppRating.RequestReview();
            AppRating.RequestReview();

            VerifyDirectWarningProvider("fixture custom reason");
            VerifyDirectWarningProvider(null);
        }

        public static void VerifyMacOSNativeDeclaration()
        {
            Type pluginType = typeof(AppRatingPluginMacOS);
            MethodInfo import = pluginType.GetMethod("Unity_RequestReview", PrivateStatic);
            Check(import != null && import.IsPrivate && import.IsStatic && import.IsAssembly == false,
                "The MacOS native import must remain private and static.");
            Check(import.ReturnType == typeof(void) && import.GetParameters().Length == 0,
                "The MacOS native import signature must remain parameterless void.");
            Check((import.Attributes & MethodAttributes.PinvokeImpl) != 0,
                "The MacOS entry point must remain a P/Invoke declaration.");
            Check((import.GetMethodImplementationFlags() & MethodImplAttributes.PreserveSig) != 0,
                "The native import must preserve its signature.");
            DllImportAttribute dllImport = (DllImportAttribute)Attribute.GetCustomAttribute(
                import, typeof(DllImportAttribute));
            Check(dllImport != null && dllImport.PreserveSig,
                "The reflected DllImport must retain PreserveSig.");
            // __Internal is the maintained source's explicit reconstruction inference;
            // stripped original metadata does not establish the historical library string.
            Check(dllImport.EntryPoint == "Unity_RequestReview",
                "The retained native import must address its original entry-point symbol.");
            Check(dllImport.Value == "__Internal",
                "The current maintained declaration's explicit library inference changed.");

            MethodInfo overrideMethod = pluginType.GetMethod("RequestReview", BindingFlags.Public | BindingFlags.Instance);
            Check(overrideMethod != null && overrideMethod.IsPublic && overrideMethod.IsVirtual &&
                  overrideMethod.GetBaseDefinition().DeclaringType == typeof(AppRatingPlugin),
                "MacOS RequestReview must remain a public virtual override of the original slot.");

            // Inspect the managed body only: verify it calls the import then returns. Never invoke it.
            byte[] il = ReadCompleteBody(overrideMethod, 6);
            CheckOpcodes(il, 0, 0x28, 5, 0x2a);
            CheckMember(overrideMethod, il, 1, import);
        }

        private static void VerifyLoadedRoutes()
        {
            FieldInfo selected = typeof(AppRating).GetField("s_appRatingPlugin", PrivateStatic);
            FieldInfo reason = GetUnsupportedField("m_unsupportedReason", typeof(string));
            FieldInfo warning = GetUnsupportedField("m_warning", typeof(bool));
            MethodInfo log = typeof(AppRatingPluginUnsupported).GetMethod("Log", PrivateInstance);
            MethodInfo request = typeof(AppRatingPluginUnsupported).GetMethod("RequestReview");
            MethodInfo dispatch = typeof(AppRating).GetMethod("RequestReview");
            ConstructorInfo constructor = typeof(AppRatingPluginUnsupported).GetConstructor(
                new[] { typeof(string), typeof(bool) });
            ConstructorInfo parent = typeof(AppRatingPlugin).GetConstructor(PrivateInstance, null, Type.EmptyTypes, null);
            MethodInfo output = typeof(HLOutput).GetMethod("LogError", new[] { typeof(object), typeof(UnityEngine.Object) });
            MethodInfo originalSlot = typeof(AppRatingPlugin).GetMethod("RequestReview");
            Check(selected != null && log != null && request != null && dispatch != null &&
                constructor != null && parent != null && output != null && originalSlot != null,
                "Original AppRating route declarations must exist.");

            // Current matching Unity compiler evidence has no debug NOPs, locals, or exception
            // regions in these compact bodies. Check every opcode and resolve every token; an
            // extra call/store or an inverted warning branch must fail without running that route.
            byte[] il = ReadCompleteBody(log, 21);
            CheckOpcodes(il, 0, 0x02, 1, 0x7b, 6, 0x2d, 8, 0x02, 9, 0x7b, 14, 0x14, 15, 0x28, 20, 0x2a);
            CheckMember(log, il, 2, warning);
            Check(8 + unchecked((sbyte)il[7]) == 20,
                "warning=true must branch directly to return before the original output call.");
            CheckMember(log, il, 10, reason);
            CheckMember(log, il, 16, output);

            il = ReadCompleteBody(request, 7);
            CheckOpcodes(il, 0, 0x02, 1, 0x28, 6, 0x2a);
            CheckMember(request, il, 2, log);
            il = ReadCompleteBody(dispatch, 11);
            CheckOpcodes(il, 0, 0x7e, 5, 0x6f, 10, 0x2a);
            CheckMember(dispatch, il, 1, selected);
            CheckMember(dispatch, il, 6, originalSlot);

            il = ReadCompleteBody(constructor, 27);
            CheckOpcodes(il, 0, 0x02, 1, 0x28, 6, 0x02, 7, 0x03, 8, 0x7d,
                13, 0x02, 14, 0x04, 15, 0x7d, 20, 0x02, 21, 0x28, 26, 0x2a);
            CheckMember(constructor, il, 2, parent);
            CheckMember(constructor, il, 9, reason);
            CheckMember(constructor, il, 16, warning);
            CheckMember(constructor, il, 22, log);

            ConstructorInfo initializer = typeof(AppRating).TypeInitializer;
            il = ReadCompleteBody(initializer, 32);
            CheckOpcodes(il, 0, 0x72, 5, 0x28, 10, 0x8c, 15, 0x28, 20, 0x17,
                21, 0x73, 26, 0x80, 31, 0x2a);
            Check(initializer.Module.ResolveString(BitConverter.ToInt32(il, 1)) ==
                "App Rating is not supported in platform {0}.", "Original unsupported reason template.");
            CheckMember(initializer, il, 6, typeof(Application).GetProperty("platform").GetGetMethod());
            Check(initializer.Module.ResolveType(BitConverter.ToInt32(il, 11)) == typeof(RuntimePlatform),
                "The initializer must box the genuine platform enum.");
            CheckMember(initializer, il, 16, typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object) }));
            CheckMember(initializer, il, 22, constructor);
            CheckMember(initializer, il, 27, selected);
        }

        private static byte[] ReadCompleteBody(MethodBase method, int length)
        {
            Check(method != null, "Original route method must exist.");
            MethodBody body = method.GetMethodBody();
            Check(body != null && body.LocalVariables.Count == 0 && body.ExceptionHandlingClauses.Count == 0,
                "Original compact route must have no locals or exception regions: " + method.Name);
            byte[] il = body.GetILAsByteArray();
            Check(il != null && il.Length == length,
                "Complete matching-compiler route length changed: " + method.Name);
            return il;
        }

        private static void CheckOpcodes(byte[] il, params int[] offsetAndOpcode)
        {
            Check(offsetAndOpcode.Length % 2 == 0, "Fixture opcode positions must be paired.");
            for (int i = 0; i < offsetAndOpcode.Length; i += 2)
                Check(il[offsetAndOpcode[i]] == offsetAndOpcode[i + 1],
                    "Original route opcode changed at " + offsetAndOpcode[i]);
        }

        private static void CheckMember(MethodBase owner, byte[] il, int tokenOffset, MemberInfo expected)
        {
            Check(expected != null, "Expected original operand must exist.");
            int token = BitConverter.ToInt32(il, tokenOffset);
            MemberInfo actual = expected is FieldInfo
                ? (MemberInfo)owner.Module.ResolveField(token)
                : owner.Module.ResolveMethod(token);
            Check(actual != null && actual.Module == expected.Module && actual.MetadataToken == expected.MetadataToken,
                "Original route operand changed in " + owner.Name + " at " + tokenOffset);
        }

        private static void VerifyDirectWarningProvider(string reason)
        {
            var provider = new AppRatingPluginUnsupported(reason, true);
            FieldInfo reasonField = GetUnsupportedField("m_unsupportedReason", typeof(string));
            FieldInfo warningField = GetUnsupportedField("m_warning", typeof(bool));
            Check(reasonField.GetValue(provider) == reason,
                "A directly constructed warning provider must retain its exact reason, including null.");
            Check((bool)warningField.GetValue(provider),
                "A directly constructed warning provider must retain warning=true.");
            provider.RequestReview();
            provider.RequestReview();
            Check(reasonField.GetValue(provider) == reason && (bool)warningField.GetValue(provider),
                "Repeated warning-provider requests must retain the original reason and warning flag.");
        }

        private static FieldInfo GetUnsupportedField(string name, Type expectedType)
        {
            FieldInfo field = typeof(AppRatingPluginUnsupported).GetField(name, PrivateInstance);
            Check(field != null && field.IsPrivate && field.IsInitOnly && field.FieldType == expectedType,
                "Unsupported provider field " + name + " must retain its private readonly declaration.");
            return field;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
