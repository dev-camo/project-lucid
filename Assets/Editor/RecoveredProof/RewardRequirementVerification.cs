using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // The original requirement is a complete, method-free value type. These
    // checks establish its field identity and Unity JSON representation only.
    public static class RewardRequirementVerification
    {
        public static void Run()
        {
            int checks = 0;
            Action<bool, string> check = (value, label) =>
            {
                if (!value) throw new InvalidOperationException(label);
                ++checks;
            };
            Type type = typeof(RewardRequirement);
            check(type.Assembly.GetName().Name == "Game.Runtime" && type.FullName == "HardlightProject.RewardRequirement", "original assembly/type");
            check(type.IsPublic && type.IsValueType && type.IsSerializable && type.IsLayoutSequential, "original public sequential serializable value type");
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .OrderBy(field => field.MetadataToken).ToArray();
            check(fields.Length == 2 && fields[0].Name == "RewardType" && fields[1].Name == "Count", "complete original field order");
            check(fields[0].FieldType == typeof(CollectableType) && fields[1].FieldType == typeof(int), "genuine field types");
            check(fields.All(field => field.IsPublic && !field.IsInitOnly && field.IsDefined(typeof(SerializeField), false)), "original mutable serialized fields");
            check(type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0
                && type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0, "no invented own methods");
            check(Marshal.OffsetOf(type, "RewardType").ToInt32() == 0 && Marshal.OffsetOf(type, "Count").ToInt32() == 4 && Marshal.SizeOf(type) == 8, "original two integer field offsets");
            RewardRequirement zero = default(RewardRequirement);
            check(zero.RewardType == CollectableType.None && zero.Count == 0, "original value-type defaults");
            foreach (var item in new[] {
                zero,
                new RewardRequirement { RewardType = CollectableType.Ring, Count = 25 },
                new RewardRequirement { RewardType = (CollectableType)int.MinValue, Count = int.MaxValue },
                new RewardRequirement { RewardType = (CollectableType)int.MaxValue, Count = int.MinValue } })
            {
                string json = JsonUtility.ToJson(item);
                RewardRequirement loaded = JsonUtility.FromJson<RewardRequirement>(json);
                check(loaded.RewardType == item.RewardType && loaded.Count == item.Count, "actual Unity JSON preserves enum integers/counts");
            }
            Debug.Log("Project Lucid RewardRequirement checks=" + checks + "; field and JSON proof, zero recovered methods");
        }
    }
}
