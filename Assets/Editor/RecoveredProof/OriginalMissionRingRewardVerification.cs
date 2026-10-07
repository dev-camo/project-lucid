using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class OriginalMissionRingRewardVerification
    {
        // Original definition/RewardData identities and signed inclusive authored lookup.
        // Fault assertions apply to the rebuilt managed runtime; original unchecked
        // native accesses are inspected separately rather than executed here.
        public static int RunMetadataBoundaries()
        {
            int checks = 0;
            Type outer = typeof(MissionRingRewardDefinition);
            Type inner = typeof(MissionRingRewardDefinition.RewardData);
            BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo entries = outer.GetField("m_rewardData", fields);
            FieldInfo threshold = inner.GetField("m_ringCount", fields);
            FieldInfo rewardXP = inner.GetField("m_rewardXP", fields);
            Expect(outer.Assembly.GetName().Name == "Game.Runtime", ref checks);
            Expect(outer.FullName == "HardlightProject.MissionRingRewardDefinition", ref checks);
            Expect(outer.BaseType == typeof(ScriptableObjectWithGuid) && !outer.IsSealed, ref checks);
            Expect(inner.IsNestedPublic && inner.IsSerializable && !inner.IsSealed, ref checks);
            Expect(outer.GetFields(fields).Length == 1 && inner.GetFields(fields).Length == 2, ref checks);
            Expect(entries != null && entries.FieldType == inner.MakeArrayType() && entries.IsDefined(typeof(SerializeField), false), ref checks);
            Expect(threshold != null && threshold.FieldType == typeof(int) && threshold.IsDefined(typeof(SerializeField), false), ref checks);
            Expect(rewardXP != null && rewardXP.FieldType == typeof(int) && rewardXP.IsDefined(typeof(SerializeField), false), ref checks);
            var menu = outer.GetCustomAttribute<CreateAssetMenuAttribute>();
            Expect(menu != null && menu.fileName == "MissionRingRewardDefinition" && menu.menuName == "HardlightProject/DefinitionData/Definitions/MissionRingRewardDefinition", ref checks);
            var options = outer.GetCustomAttributes<Il2CppSetOptionAttribute>();
            int optionCount = 0;
            bool nullSeen = false, arraySeen = false;
            foreach (var option in options)
            {
                Expect((option.Option == Option.NullChecks || option.Option == Option.ArrayBoundsChecks) && option.Value is bool && !(bool)option.Value, ref checks);
                nullSeen |= option.Option == Option.NullChecks;
                arraySeen |= option.Option == Option.ArrayBoundsChecks;
                optionCount++;
            }
            Expect(optionCount == 2 && nullSeen && arraySeen, ref checks);
            var data = new MissionRingRewardDefinition.RewardData();
            Expect(data.RingCount == 0 && data.RewardXP == 0, ref checks);
            threshold.SetValue(data, int.MinValue);
            rewardXP.SetValue(data, int.MaxValue);
            Expect(data.RingCount == int.MinValue && data.RewardXP == int.MaxValue, ref checks);

            return checks;
        }

        public static int RunOriginalBoundaries()
        {
            int checks = RunMetadataBoundaries();
            BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo entries = typeof(MissionRingRewardDefinition).GetField("m_rewardData", fields);
            FieldInfo threshold = typeof(MissionRingRewardDefinition.RewardData).GetField("m_ringCount", fields);
            FieldInfo rewardXP = typeof(MissionRingRewardDefinition.RewardData).GetField("m_rewardXP", fields);
            var definition = ScriptableObject.CreateInstance<MissionRingRewardDefinition>();
            var restored = ScriptableObject.CreateInstance<MissionRingRewardDefinition>();
            try
            {
                Expect(entries.GetValue(definition) == null, ref checks);
                ExpectThrows<NullReferenceException>(() => definition.GetRingRewardXP(0), ref checks);
                entries.SetValue(definition, new MissionRingRewardDefinition.RewardData[0]);
                Expect(definition.GetRingRewardXP(int.MinValue) == 0, ref checks);
                Expect(definition.GetRingRewardXP(int.MaxValue) == 0, ref checks);
                entries.SetValue(definition, MakeEntries(threshold, rewardXP, new[] { -5, 0, 5, 10 }, new[] { -100, 10, 50, 100 }));
                int[] inputs = { -6, -5, -4, 0, 4, 5, 9, 10, int.MaxValue, int.MinValue };
                int[] expected = { 0, -100, -100, 10, 10, 50, 50, 100, 100, 0 };
                for (int i = 0; i < inputs.Length; i++)
                    Expect(definition.GetRingRewardXP(inputs[i]) == expected[i], ref checks);
                entries.SetValue(definition, MakeEntries(threshold, rewardXP, new[] { 10, 0, 5 }, new[] { 100, 7, 9 }));
                Expect(definition.GetRingRewardXP(10) == 9, ref checks);
                Expect(definition.GetRingRewardXP(0) == 7, ref checks);
                Expect(definition.GetRingRewardXP(4) == 7, ref checks);
                Expect(definition.GetRingRewardXP(5) == 9, ref checks);
                string json = JsonUtility.ToJson(definition);
                Expect(json.Contains("m_rewardData") && json.Contains("m_ringCount") && json.Contains("m_rewardXP"), ref checks);
                JsonUtility.FromJsonOverwrite(json, restored);
                var restoredEntries = (MissionRingRewardDefinition.RewardData[])entries.GetValue(restored);
                Expect(restoredEntries.Length == 3 && restoredEntries[0].RingCount == 10 && restoredEntries[1].RingCount == 0 && restoredEntries[2].RingCount == 5, ref checks);
                Expect(restored.GetRingRewardXP(10) == 9 && restored.GetRingRewardXP(0) == 7, ref checks);
                entries.SetValue(definition, MakeEntries(threshold, rewardXP, new[] { 5, 5 }, new[] { 1, -9 }));
                Expect(definition.GetRingRewardXP(5) == -9, ref checks);
                Expect(definition.GetRingRewardXP(4) == 0, ref checks);
                entries.SetValue(definition, MakeEntries(threshold, rewardXP, new[] { int.MinValue, int.MaxValue }, new[] { int.MaxValue, int.MinValue }));
                Expect(definition.GetRingRewardXP(int.MinValue) == int.MaxValue, ref checks);
                Expect(definition.GetRingRewardXP(0) == int.MaxValue, ref checks);
                Expect(definition.GetRingRewardXP(int.MaxValue) == int.MinValue, ref checks);
                entries.SetValue(definition, new MissionRingRewardDefinition.RewardData[] { null });
                ExpectThrows<NullReferenceException>(() => definition.GetRingRewardXP(0), ref checks);
                return checks;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(restored);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static MissionRingRewardDefinition.RewardData[] MakeEntries(FieldInfo threshold, FieldInfo xp, int[] counts, int[] values)
        {
            var result = new MissionRingRewardDefinition.RewardData[counts.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new MissionRingRewardDefinition.RewardData();
                threshold.SetValue(result[i], counts[i]);
                xp.SetValue(result[i], values[i]);
            }
            return result;
        }

        private static void Expect(bool condition, ref int checks)
        {
            if (!condition) throw new InvalidOperationException("Original mission ring reward assertion " + checks + " failed");
            checks++;
        }

        private static void ExpectThrows<T>(Action action, ref int checks) where T : Exception
        {
            try { action(); }
            catch (T) { checks++; return; }
            throw new InvalidOperationException("Expected managed fault " + typeof(T).Name);
        }
    }
}
