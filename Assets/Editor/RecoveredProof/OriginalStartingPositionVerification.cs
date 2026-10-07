using System;
using System.Linq;
using System.Reflection;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class OriginalStartingPositionVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        public static int RunDeclarations()
        {
            int checks = 0;
            Type type = typeof(StartingPosition);
            Require(type.FullName == "HardlightProject.StartingPosition" && type.Assembly.GetName().Name == "Game.Runtime" && (int)type.Attributes == 1056769, "complete original serializable type identity", ref checks);
            Require(type.BaseType == typeof(object) && !type.IsAbstract && !type.IsSealed, "original object base and access", ref checks);
            Require(type.GetMethods(Own).Length == 3 && type.GetConstructors(Own).Length == 1, "all four original declarations", ref checks);
            Require(type.GetNestedTypes(Own).Length == 0 && type.GetEvents(Own).Length == 0, "original type has no generated owner or event", ref checks);
            FieldInfo[] fields = type.GetFields(Own);
            Require(fields.Length == 2 && fields.Select(x => x.Name).SequenceEqual(new[] { "m_location", "m_identifier" }), "original serialized field order", ref checks);
            Type[] fieldTypes = { typeof(Transform), typeof(LevelStartPositionDefinition) };
            for (int i = 0; i < fields.Length; i++)
            {
                Require((int)fields[i].Attributes == 1 && fields[i].FieldType == fieldTypes[i], "original private field " + fields[i].Name, ref checks);
                Require(fields[i].GetCustomAttributesData().Count == 1 && fields[i].IsDefined(typeof(SerializeField), false), "original serialization attribute " + fields[i].Name, ref checks);
            }
            PropertyInfo[] properties = type.GetProperties(Own);
            Require(properties.Length == 2, "original two getter properties", ref checks);
            foreach (PropertyInfo property in properties)
            {
                Require(property.SetMethod == null && property.GetMethod.IsPublic && !property.GetMethod.IsStatic, "original readonly property " + property.Name, ref checks);
                Require((int)property.GetMethod.Attributes == 2182 && property.GetMethod.GetParameters().Length == 0, "original getter declaration " + property.Name, ref checks);
            }
            MethodInfo setData = type.GetMethod("SetData", Own);
            Require((int)setData.Attributes == 134 && setData.ReturnType == typeof(void) && !setData.IsGenericMethod, "original SetData declaration", ref checks);
            ParameterInfo[] parameters = setData.GetParameters();
            Require(parameters.Length == 2 && parameters[0].Name == "location" && parameters[0].ParameterType == typeof(Transform) && (int)parameters[0].Attributes == 0 && !parameters[0].HasDefaultValue, "original location parameter", ref checks);
            Require(parameters[1].Name == "definition" && parameters[1].ParameterType == typeof(LevelStartPositionDefinition) && (int)parameters[1].Attributes == 0 && !parameters[1].HasDefaultValue, "original definition parameter", ref checks);
            ConstructorInfo constructor = type.GetConstructors(Own).Single();
            Require((int)constructor.Attributes == 6278 && constructor.GetParameters().Length == 0, "original object-only constructor declaration", ref checks);
            var options = type.GetCustomAttributesData().Where(x => x.AttributeType == typeof(Il2CppSetOptionAttribute)).ToArray();
            Require(options.Length == 2 && (int)options[0].ConstructorArguments[0].Value == 2 && (bool)options[0].ConstructorArguments[1].Value == false && (int)options[1].ConstructorArguments[0].Value == 1 && (bool)options[1].ConstructorArguments[1].Value == false, "original IL2CPP option order", ref checks);
            return checks;
        }

        public static int RunDefaultBoundaries()
        {
            int checks = 0;
            var startingPosition = new StartingPosition();
            Require(ReferenceEquals(startingPosition.Location, null) && ReferenceEquals(startingPosition.Identifier, null), "object-only constructor retains null references", ref checks);
            startingPosition.SetData(null, null);
            Require(ReferenceEquals(startingPosition.Location, null), "shipped empty setter retains null location", ref checks);
            Require(ReferenceEquals(startingPosition.Identifier, null), "shipped empty setter retains null identifier", ref checks);
            return checks;
        }

        public static int RunAuthoredReferences()
        {
            int checks = 0;
            GameObject authored = null, replacement = null;
            LevelStartPositionDefinition authoredDefinition = null, replacementDefinition = null;
            try
            {
                authored = new GameObject("OriginalStartingPosition_Authored");
                replacement = new GameObject("OriginalStartingPosition_Replacement");
                authoredDefinition = ScriptableObject.CreateInstance<LevelStartPositionDefinition>();
                replacementDefinition = ScriptableObject.CreateInstance<LevelStartPositionDefinition>();
                var startingPosition = new StartingPosition();
                typeof(StartingPosition).GetField("m_location", Own).SetValue(startingPosition, authored.transform);
                typeof(StartingPosition).GetField("m_identifier", Own).SetValue(startingPosition, authoredDefinition);
                Require(ReferenceEquals(startingPosition.Location, authored.transform) && ReferenceEquals(startingPosition.Identifier, authoredDefinition), "getters return authored references", ref checks);
                // Independent original witness: ARM 0x6ad9c8 is RET; x86 0x6d23d0
                // contains only its frame, RET and alignment, with no field writes.
                startingPosition.SetData(replacement.transform, replacementDefinition);
                Require(ReferenceEquals(startingPosition.Location, authored.transform), "replacement argument leaves authored location", ref checks);
                Require(ReferenceEquals(startingPosition.Identifier, authoredDefinition), "replacement argument leaves authored identifier", ref checks);
                startingPosition.SetData(null, null);
                Require(ReferenceEquals(startingPosition.Location, authored.transform), "null argument leaves authored location", ref checks);
                Require(ReferenceEquals(startingPosition.Identifier, authoredDefinition), "null argument leaves authored identifier", ref checks);
            }
            finally
            {
                if (replacementDefinition != null) UnityEngine.Object.DestroyImmediate(replacementDefinition);
                if (authoredDefinition != null) UnityEngine.Object.DestroyImmediate(authoredDefinition);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                if (authored != null) UnityEngine.Object.DestroyImmediate(authored);
            }
            return checks;
        }

        private static void Require(bool condition, string message, ref int checks)
        {
            if (!condition) throw new InvalidOperationException(message);
            ++checks;
        }
    }
}
