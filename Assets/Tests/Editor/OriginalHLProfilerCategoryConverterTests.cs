using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using Unity.Profiling;

namespace ProjectLucid.Editor
{
    // Declarations and the category table recovered from the original 1.10.1 static constructors.
    public sealed class OriginalHLProfilerCategoryConverterTests
    {
        private const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private static Option[] Options(System.Type type) =>
            type.GetCustomAttributesData().Where(data => data.AttributeType == typeof(Il2CppSetOptionAttribute))
                .Select(data => (Option)data.ConstructorArguments[0].Value).ToArray();

        [Test]
        public void ConverterAndComparersRetainOriginalDeclarations()
        {
            Assert.That((int)typeof(HLProfilerCategoryConverter).Attributes, Is.EqualTo(0x00100181));
            Assert.That(Options(typeof(HLProfilerCategoryConverter)), Is.EqualTo(new[] { Option.ArrayBoundsChecks, Option.NullChecks }));
            Assert.That((int)typeof(HLProfilerCategoryConverter).GetField("LookupTable", Statics).Attributes, Is.EqualTo(0x31));
            Assert.That((int)typeof(HLProfilerEnumComparers).Attributes, Is.EqualTo(0x00100181));
            Assert.That(Options(typeof(HLProfilerEnumComparers)), Is.EqualTo(new[] { Option.NullChecks, Option.ArrayBoundsChecks }));
            Assert.That((int)typeof(HLProfilerEnumComparers).GetField("CategoryComparer", Statics).Attributes, Is.EqualTo(0x36));
            Assert.That((int)typeof(HLProfilerCategoryEqualityComparer).Attributes, Is.EqualTo(0x00100001));
            Assert.That(Options(typeof(HLProfilerCategoryEqualityComparer)), Is.EqualTo(new[] { Option.ArrayBoundsChecks, Option.NullChecks }));
        }

        [Test]
        public void TableMapsEveryCategoryInOrderUsingTheSharedComparer()
        {
            var table = (Dictionary<HLProfilerCategory, ProfilerCategory>)typeof(HLProfilerCategoryConverter)
                .GetField("LookupTable", Statics).GetValue(null);
            Assert.That(table.Comparer, Is.SameAs(HLProfilerEnumComparers.CategoryComparer));
            var categories = (HLProfilerCategory[])System.Enum.GetValues(typeof(HLProfilerCategory));
            Assert.That(table.Keys, Is.EqualTo(categories));
            ProfilerCategory[] expected =
            {
                ProfilerCategory.Memory, ProfilerCategory.Input, ProfilerCategory.Vr, ProfilerCategory.Loading,
                ProfilerCategory.Network, ProfilerCategory.Lighting, ProfilerCategory.Particles, ProfilerCategory.Video,
                ProfilerCategory.Audio, ProfilerCategory.Ai, ProfilerCategory.Animation, ProfilerCategory.Physics,
                ProfilerCategory.Gui, ProfilerCategory.Scripts, ProfilerCategory.Render, ProfilerCategory.VirtualTexturing,
                ProfilerCategory.Internal
            };
            Assert.That(categories.Select(HLProfilerCategoryConverter.ToProfilerCategory), Is.EqualTo(expected));
            Assert.Throws<KeyNotFoundException>(() => HLProfilerCategoryConverter.ToProfilerCategory((HLProfilerCategory)99));
        }
    }
}
