using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    // Original HLSplines.Runtime 0x02000014. Overload addresses are matched by body
    // (out-row lookup, list allocation, String.Empty, Int32/Single/Boolean.TryParse);
    // each InterpolateValue throw path names its own MethodDef 0x0600003e..41. Constant
    // branches are ucomiss 1f, t with cmova (cmpltss t, 1f for float), so t < 1f
    // selects a and NaN selects b.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class MetadataUtilities
    {
        // 0x0600002f (x86-64 0x1a53ce0)
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
        // 0x06000030 (x86-64 0x1a53ea0; allocates the list on first match)
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
        // 0x06000031 (x86-64 0x1a54b40; no allocation)
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
        // 0x06000032 (x86-64 0x1a541d0)
        public static bool TryGetValue(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out string outString)
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outString = found ? row.AsString() : string.Empty;
            return found;
        }
        // 0x06000033 (x86-64 0x1a542d0)
        public static bool TryGetValue(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out int outInt)
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outInt = found ? row.AsInt() : 0;
            return found;
        }
        // 0x06000034 (x86-64 0x1a543b0)
        public static bool TryGetValue(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out float outFloat)
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outFloat = found ? row.AsFloat() : 0f;
            return found;
        }
        // 0x06000035 (x86-64 0x1a544c0)
        public static bool TryGetValue(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out bool outBool)
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outBool = found ? row.AsBool() : false;
            return found;
        }
        // 0x06000036 (x86-64 shared 0x979d80)
        public static bool TryGetObject<TObject>(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out TObject outObject) where TObject : UnityEngine.Object
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outObject = found ? row.AsObject<TObject>() : null;
            return found;
        }
        // 0x06000037 (x86-64 shared 0x979e90, Int32Enum 0x979e10)
        public static bool TryGetValue<TEnum>(IReadOnlyList<Metadata> metadataList, MetadataKeyType key, out TEnum outEnum) where TEnum : struct, Enum
        {
            bool found = TryGetMetadata(metadataList, key, out Metadata row);
            outEnum = found ? row.AsEnum<TEnum>() : default;
            return found;
        }
        // Parsing success is intentionally discarded: malformed values become the BCL out default.
        // 0x06000038 (x86-64 0x1a53710)
        public static int ConvertToInt(string value) { int.TryParse(value, out int result); return result; }
        // 0x06000039 (x86-64 0x1a53770)
        public static float ConvertToFloat(string value) { float.TryParse(value, out float result); return result; }
        // 0x0600003a (x86-64 0x1a53800)
        public static bool ConvertToBool(string value) { bool.TryParse(value, out bool result); return result; }
        // 0x0600003b (x86-64 shared 0x979b60, Int32Enum 0x979ae0)
        public static TEnum ConvertToEnum<TEnum>(string value) where TEnum : struct, Enum { Enum.TryParse(value, out TEnum result); return result; }
        // 0x0600003c (x86-64 shared 0x979c40; first name match, then LogError)
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
        // 0x0600003d (x86-64 0x1a54d20; clamps t with 1f)
        public static Metadata Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type)
        {
            Metadata result = null;
            float clampedT = Mathf.Clamp01(t);
            a.Key.ValueType.Interpolate(a, b, clampedT, type, out result);
            return result;
        }
        // 0x0600003e (x86-64 0x1a54d80)
        public static string InterpolateValue(string a, string b, float t, MetadataInterpolationType interpolation)
        {
            if (interpolation == MetadataInterpolationType.Constant) return t < 1f ? a : b;
            throw new Exception("Unhandled interpolation type.");
        }
        // 0x0600003f (x86-64 0x1a54df0)
        public static int InterpolateValue(int a, int b, float t, MetadataInterpolationType interpolation)
        {
            switch (interpolation)
            {
                case MetadataInterpolationType.Constant: return t < 1f ? a : b;
                case MetadataInterpolationType.Linear: return (int)Mathf.Lerp(a, b, t);
                default: throw new Exception("Unhandled interpolation type.");
            }
        }
        // 0x06000040 (x86-64 0x1a54e90)
        public static float InterpolateValue(float a, float b, float t, MetadataInterpolationType interpolation)
        {
            switch (interpolation)
            {
                case MetadataInterpolationType.Constant: return t < 1f ? a : b;
                case MetadataInterpolationType.Linear: return Mathf.Lerp(a, b, t);
                default: throw new Exception("Unhandled interpolation type.");
            }
        }
        // 0x06000041 (x86-64 0x1a54f30)
        public static bool InterpolateValue(bool a, bool b, float t, MetadataInterpolationType interpolation)
        {
            if (interpolation == MetadataInterpolationType.Constant) return t < 1f ? a : b;
            throw new Exception("Unhandled interpolation type.");
        }
    }
}
