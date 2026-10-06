using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class MetadataUtilities
    {
        public static bool TryGetMetadata(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out Metadata outMetadata)
        {
            if (metadataList != null)
            {
                int count = metadataList.Count;
                for (int i = 0; i < count; ++i)
                {
                    Metadata row = metadataList[i];
                    if (row.Key == key)
                    {
                        outMetadata = row;
                        return true;
                    }
                }
            }
            outMetadata = null;
            return false;
        }
        public static bool TryGetMetadata(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out List<Metadata> outMetadataList)
        {
            outMetadataList = null;
            if (metadataList != null)
            {
                int count = metadataList.Count;
                for (int i = 0; i < count; ++i)
                {
                    Metadata row = metadataList[i];
                    if (row.Key == key)
                    {
                        if (outMetadataList == null) outMetadataList = new List<Metadata>();
                        outMetadataList.Add(row);
                    }
                }
            }
            return outMetadataList != null;
        }
        public static bool TryGetMetadata(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, List<Metadata> fillMetadataList)
        {
            if (metadataList != null)
            {
                int count = metadataList.Count;
                for (int i = 0; i < count; ++i)
                {
                    Metadata row = metadataList[i];
                    if (row.Key == key) fillMetadataList.Add(row);
                }
            }
            // Original result describes the supplied list, not whether this query found a row.
            return fillMetadataList != null;
        }
        public static bool TryGetValue(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out string outString)
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outString = found ? row.AsString() : string.Empty;
            return found;
        }
        public static bool TryGetValue(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out int outInt)
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outInt = found ? row.AsInt() : 0;
            return found;
        }
        public static bool TryGetValue(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out float outFloat)
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outFloat = found ? row.AsFloat() : 0f;
            return found;
        }
        public static bool TryGetValue(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out bool outBool)
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outBool = found ? row.AsBool() : false;
            return found;
        }
        public static bool TryGetObject<TObject>(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out TObject outObject) where TObject : UnityEngine.Object
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outObject = found ? row.AsObject<TObject>() : null;
            return found;
        }
        public static bool TryGetValue<TEnum>(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out TEnum outEnum) where TEnum : struct, Enum
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outEnum = found ? row.AsEnum<TEnum>() : default;
            return found;
        }
        // Parsing success is intentionally discarded: malformed values become the BCL out default.
        public static int ConvertToInt(string value) { int.TryParse(value, out int result); return result; }
        public static float ConvertToFloat(string value) { float.TryParse(value, out float result); return result; }
        public static bool ConvertToBool(string value) { bool.TryParse(value, out bool result); return result; }
        public static TEnum ConvertToEnum<TEnum>(string value) where TEnum : struct, Enum { Enum.TryParse(value, out TEnum result); return result; }
        public static TScriptableObject ConvertToScriptableObject<TScriptableObject>(string value) where TScriptableObject : ScriptableObject
        {
            TScriptableObject found = null;
            if (!string.IsNullOrEmpty(value))
            {
                foreach (TScriptableObject row in Resources.FindObjectsOfTypeAll<TScriptableObject>())
                    if (row.name == value) { found = row; break; }
                if (found == null) HLOutput.LogError("Referenced scriptable object '" + value + "' not found", null);
            }
            return found;
        }
        public static Metadata Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type)
        {
            Metadata result = null;
            float clampedT = Mathf.Clamp01(t);
            a.Key.ValueType.Interpolate(a, b, clampedT, type, out result);
            return result;
        }
        public static string InterpolateValue(string a, string b, float t, MetadataInterpolationType interpolation)
        {
            if (interpolation == MetadataInterpolationType.Constant) return t < 1f ? a : b;
            throw new Exception("Unhandled interpolation type.");
        }
        public static int InterpolateValue(int a, int b, float t, MetadataInterpolationType interpolation)
        {
            switch (interpolation)
            {
                case MetadataInterpolationType.Constant: return t < 1f ? a : b;
                case MetadataInterpolationType.Linear: return (int)Mathf.Lerp(a, b, t);
                default: throw new Exception("Unhandled interpolation type.");
            }
        }
        public static float InterpolateValue(float a, float b, float t, MetadataInterpolationType interpolation)
        {
            switch (interpolation)
            {
                case MetadataInterpolationType.Constant: return t < 1f ? a : b;
                case MetadataInterpolationType.Linear: return Mathf.Lerp(a, b, t);
                default: throw new Exception("Unhandled interpolation type.");
            }
        }
        public static bool InterpolateValue(bool a, bool b, float t, MetadataInterpolationType interpolation)
        {
            if (interpolation == MetadataInterpolationType.Constant) return t < 1f ? a : b;
            throw new Exception("Unhandled interpolation type.");
        }
    }
}
