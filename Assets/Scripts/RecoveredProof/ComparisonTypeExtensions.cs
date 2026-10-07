using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Game.Runtime020009f2: complete original fieldless extension owner.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class ComparisonTypeExtensions
    {
        // 06003931. Float equality is the actual Mathf.Approximately test;
        // unordered values fail ordered comparisons, and NotEqual negates that test.
        public static bool Evaluate(this ComparisonType comparisonType, float value, float criteria)
        {
            switch (comparisonType)
            {
                case ComparisonType.None: return true;
                case ComparisonType.Equal: return Mathf.Approximately(value, criteria);
                case ComparisonType.NotEqual: return !Mathf.Approximately(value, criteria);
                case ComparisonType.GreaterThan: return value > criteria;
                case ComparisonType.GreaterThanEqual: return value >= criteria;
                case ComparisonType.LessThan: return value < criteria;
                case ComparisonType.LessThanEqual: return value <= criteria;
                default:
                    throw new ArgumentOutOfRangeException("comparisonType", comparisonType,
                        "Comparison type " + comparisonType.GetString() + " is not implemented for float.");
            }
        }

        // 06003932. Integer equality is exact and relational comparisons are signed.
        public static bool Evaluate(this ComparisonType comparisonType, int value, int criteria)
        {
            switch (comparisonType)
            {
                case ComparisonType.None: return true;
                case ComparisonType.Equal: return value == criteria;
                case ComparisonType.NotEqual: return value != criteria;
                case ComparisonType.GreaterThan: return value > criteria;
                case ComparisonType.GreaterThanEqual: return value >= criteria;
                case ComparisonType.LessThan: return value < criteria;
                case ComparisonType.LessThanEqual: return value <= criteria;
                default:
                    throw new ArgumentOutOfRangeException("comparisonType", comparisonType,
                        "Comparison type " + comparisonType.GetString() + " is not implemented for int.");
            }
        }
    }
}
