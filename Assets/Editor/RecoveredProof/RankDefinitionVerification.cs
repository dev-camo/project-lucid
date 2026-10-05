using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight.Utils;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ProjectLucid
{
    public static class RankDefinitionVerification
    {
        private static int checks;
        private static readonly string[] names = { "m_type", "m_baseIcon", "m_backgroundIcon", "m_foregroundIcon", "m_audioClipIdentifier", "m_baseAddressable", "m_backgroundAddressable", "m_foregroundAddressable" };
        private static void Require(bool condition, string description)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(description);
        }
        private static FieldInfo Field(string name) => typeof(RankDefinition).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        private static MethodInfo Enable => typeof(RankDefinition).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        public static int RunManaged()
        {
            checks = 0;
            Type type = typeof(RankDefinition);
            Require(type.Assembly.GetName().Name == "Game.Runtime", "original assembly identity");
            Require(type.IsPublic && type.IsSealed && !type.IsAbstract && type.BaseType == typeof(ScriptableObject), "complete original sealed ScriptableObject hierarchy");
            Require(typeof(HashEnumAttribute).Assembly.GetName().Name == "HLUnityCore.Runtime", "genuine original HashEnum dependency identity");
            Require(typeof(AssetReferenceAtlasedSprite).BaseType == typeof(AssetReferenceT<Sprite>), "installed original typed Addressables hierarchy");
            var orderedTypeAttrs = type.GetCustomAttributesData().Select(a => a.AttributeType).ToArray();
            Require(orderedTypeAttrs.SequenceEqual(new[] { typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute), typeof(CreateAssetMenuAttribute) }), "all original ordered class attributes");
            var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Require(options.Length == 2 && options[0].Option == Option.NullChecks && options[1].Option == Option.ArrayBoundsChecks && Equals(options[0].Value, false) && Equals(options[1].Value, false), "exact original IL2CPP option values");
            var menu = type.GetCustomAttribute<CreateAssetMenuAttribute>();
            Require(menu.fileName == "RankDefinition" && menu.menuName == "HardlightProject/DefinitionData/Definitions/RankDefinition", "exact original authoring menu");
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            var types = new[] { typeof(RankType), typeof(AssetReferenceAtlasedSprite), typeof(AssetReferenceAtlasedSprite), typeof(AssetReferenceAtlasedSprite), typeof(HLAudioClipIdentifier), typeof(ManagedAddressableAsset<Sprite>), typeof(ManagedAddressableAsset<Sprite>), typeof(ManagedAddressableAsset<Sprite>) };
            Require(fields.Select(f => f.Name).SequenceEqual(names), "complete eight-field graph and original order");
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                Require(field.FieldType == types[i] && field.IsPrivate && !field.IsInitOnly && !field.IsStatic && !field.IsLiteral, field.Name + " exact original member type/flags");
                Require(field.IsDefined(typeof(SerializeField), false) == (i < 5) && field.IsNotSerialized == (i >= 5), field.Name + " exact original serialization flags");
                var attrs = field.GetCustomAttributesData().Where(a => a.AttributeType != typeof(NonSerializedAttribute)).Select(a => a.AttributeType);
                Require(attrs.SequenceEqual(i == 4 ? new[] { typeof(HashEnumAttribute), typeof(SerializeField) } : i < 5 ? new[] { typeof(SerializeField) } : Type.EmptyTypes), field.Name + " exact original ordered attribute graph");
            }
            var hash = Field("m_audioClipIdentifier").GetCustomAttributesData().Single(a => a.AttributeType == typeof(HashEnumAttribute));
            Require(hash.ConstructorArguments.Count == 1 && hash.ConstructorArguments[0].ArgumentType == typeof(Type) && hash.ConstructorArguments[0].Value == null, "original HashEnum(null) payload");
            string[] properties = { "Type", "BaseAddressable", "BackgroundAddressable", "ForegroundAddressable", "AudioClipIdentifier" };
            string[] getterFields = { "m_type", "m_baseAddressable", "m_backgroundAddressable", "m_foregroundAddressable", "m_audioClipIdentifier" };
            Require(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(p => p.MetadataToken).Select(p => p.Name).SequenceEqual(properties), "complete original five-property API and order");
            for (int i = 0; i < properties.Length; i++)
            {
                var property = type.GetProperty(properties[i]); var code = ReadIL(property.GetMethod).ToArray();
                Require(property.SetMethod == null && property.GetMethod.IsPublic && property.PropertyType == Field(getterFields[i]).FieldType && code.Length == 3 && code[0].op == OpCodes.Ldarg_0 && code[1].op == OpCodes.Ldfld && ((FieldInfo)code[1].value).Name == getterFields[i] && code[2].op == OpCodes.Ret, property.Name + " exact native direct-getter IL");
            }
            Require(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 6 && type.GetConstructors().Length == 1 && type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Length == 0, "all seven original methods without added compiler types or API");
            Require(Enable.IsPrivate && !Enable.IsVirtual && Enable.ReturnType == typeof(void) && Enable.GetParameters().Length == 0, "exact original private lifecycle contract");
            var setup = ReadIL(Enable).ToArray();
            Require(setup.Length == 16 && setup[15].op == OpCodes.Ret, "complete native straight-line wrapper creation with no load/release/null branches");
            for (int i = 0; i < 3; i++)
            {
                int n = i * 5; var ctor = setup[n + 3].value as ConstructorInfo;
                Require(setup[n].op == OpCodes.Ldarg_0 && setup[n + 1].op == OpCodes.Ldarg_0 && setup[n + 2].op == OpCodes.Ldfld && ((FieldInfo)setup[n + 2].value).Name == names[i + 1] && setup[n + 3].op == OpCodes.Newobj && ctor.DeclaringType == typeof(ManagedAddressableAsset<Sprite>) && ctor.GetParameters().Length == 1 && ctor.GetParameters()[0].ParameterType == typeof(AssetReferenceT<Sprite>) && setup[n + 4].op == OpCodes.Stfld && ((FieldInfo)setup[n + 4].value).Name == names[i + 5], "wrapper " + i + " exact native input/generic constructor/store order");
            }
            var construction = ReadIL(type.GetConstructor(Type.EmptyTypes)).ToArray();
            Require(construction.Length == 3 && construction[0].op == OpCodes.Ldarg_0 && construction[1].op == OpCodes.Call && ((MethodBase)construction[1].value).IsConstructor && ((MethodBase)construction[1].value).DeclaringType == typeof(ScriptableObject) && construction[2].op == OpCodes.Ret, "original base-only constructor preserves every zero/default field");
            return checks;
        }

        // Unity proof creates genuine engine objects and genuine
        // package references; it does not load catalog assets or bind original owners.
        public static void Run()
        {
            RunManaged();
            RankDefinition rank = ScriptableObject.CreateInstance<RankDefinition>();
            try
            {
                Require((int)rank.Type == 0 && (int)rank.AudioClipIdentifier == 0, "actual Unity native zero enum defaults");
                var wrapperFields = typeof(ManagedAddressableAsset<Sprite>).GetField("m_assetReference", BindingFlags.NonPublic | BindingFlags.Instance);
                var countField = typeof(ManagedAddressableAsset<Sprite>).GetField("m_refCount", BindingFlags.NonPublic | BindingFlags.Instance);
                var handles = typeof(ManagedAddressableAsset<Sprite>).GetField("m_assetHandle", BindingFlags.NonPublic | BindingFlags.Instance);
                var initial = new[] { rank.BaseAddressable, rank.BackgroundAddressable, rank.ForegroundAddressable };
                for (int i = 0; i < initial.Length; i++)
                    Require(initial[i] != null && ReferenceEquals(wrapperFields.GetValue(initial[i]), Field(names[i + 1]).GetValue(rank)) && (int)countField.GetValue(initial[i]) == 0 && !((UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Sprite>)handles.GetValue(initial[i])).IsValid(), "actual enable creates wrapper" + i + " with exact owned reference and no load");
                var references = new[] { new AssetReferenceAtlasedSprite("00112233445566778899aabbccddeeff"), new AssetReferenceAtlasedSprite("11112233445566778899aabbccddeeff"), new AssetReferenceAtlasedSprite("22112233445566778899aabbccddeeff") };
                for (int i = 0; i < references.Length; i++) Field(names[i + 1]).SetValue(rank, references[i]);
                Enable.Invoke(rank, null);
                var replaced = new[] { rank.BaseAddressable, rank.BackgroundAddressable, rank.ForegroundAddressable };
                for (int i = 0; i < replaced.Length; i++)
                    Require(!ReferenceEquals(initial[i], replaced[i]) && ReferenceEquals(wrapperFields.GetValue(replaced[i]), references[i]) && (int)countField.GetValue(replaced[i]) == 0 && !((UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Sprite>)handles.GetValue(replaced[i])).IsValid(), "actual native wrapper replacement/alias/no-load" + i);
                Field("m_type").SetValue(rank, RankType.S);
                Field("m_audioClipIdentifier").SetValue(rank, (HLAudioClipIdentifier)1234567);
                string json = JsonUtility.ToJson(rank);
                Require(json.Contains("00112233445566778899aabbccddeeff") && json.Contains("11112233445566778899aabbccddeeff") && json.Contains("22112233445566778899aabbccddeeff") && json.Contains("\"m_type\":-543223748") && json.Contains("\"m_audioClipIdentifier\":1234567") && !json.Contains("m_baseAddressable") && !json.Contains("m_backgroundAddressable") && !json.Contains("m_foregroundAddressable"), "genuine package reference and original enum serialization excludes three NonSerialized wrappers");
            }
            finally { UnityEngine.Object.DestroyImmediate(rank); }
            Debug.Log("Project Lucid original rank definition proof: PASS checks=" + checks);
        }

        private static IEnumerable<(OpCode op, object value)> ReadIL(MethodBase method)
        {
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            for (int p = 0; p < il.Length;)
            {
                short key = il[p++]; if (key == 0xfe) key = (short)(0xfe00 | il[p++]);
                OpCode op = codes[key]; object value = null;
                switch (op.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.InlineField: value = method.Module.ResolveField(BitConverter.ToInt32(il, p)); p += 4; break;
                    case OperandType.InlineMethod: value = method.Module.ResolveMethod(BitConverter.ToInt32(il, p)); p += 4; break;
                    default: throw new InvalidOperationException("Unexpected bounded native-matched IL: " + op);
                }
                yield return (op, value);
            }
        }
    }
}
