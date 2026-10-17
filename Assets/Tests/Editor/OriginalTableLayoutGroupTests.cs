using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of UnityEngine.UI.Extensions.TableLayoutGroup (HLUnityUI.Runtime
    // 0x060000a8..bb) against the shipped x86-64 bodies. Each case owns its root,
    // cells and component. The component is disabled before its GameObject is
    // activated, so in most cases property setters and the layout passes driven
    // here never mark the shared CanvasUpdateRegistry.
    // The OnEnable and enabled-setter cases do touch global state: like the
    // original they reach Canvas.ForceUpdateCanvases, which raises the static
    // willRenderCanvases event for every subscriber and so also drains the global
    // CanvasUpdateRegistry (and TMP update manager) queues, including rebuilds
    // queued by other tests or open scenes. Destroying an enabled group in Dispose
    // runs LayoutGroup.OnDisable, which marks the destroyed rect for rebuild; the
    // registry drops that dead entry on its next update. The counting case removes
    // its willRenderCanvases handler in a finally block.
    [TestFixture]
    public sealed class OriginalTableLayoutGroupTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Declared = Any | BindingFlags.Static | BindingFlags.DeclaredOnly;

        sealed class Table : IDisposable
        {
            public readonly GameObject Root;
            public readonly TableLayoutGroup Group;
            public readonly RectTransform[] Cells;

            // heights holds (min, preferred) per cell for a LayoutElement, or null for a bare cell.
            public Table(int cellCount, Vector2 size, Vector2[] heights = null)
            {
                Root = new GameObject("TableLayoutGroup fixture", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    Root.SetActive(false);
                    ((RectTransform)Root.transform).sizeDelta = size;
                    Group = Root.AddComponent<TableLayoutGroup>();
                    Group.enabled = false;
                    Cells = new RectTransform[cellCount];
                    for (int i = 0; i < cellCount; i++)
                    {
                        var cell = new GameObject("Cell " + i, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                        cell.transform.SetParent(Root.transform, false);
                        Cells[i] = (RectTransform)cell.transform;
                        // Top-left pivot: anchoredPosition is (pos, -pos) of SetChildAlongAxis.
                        Cells[i].pivot = new Vector2(0f, 1f);
                        if (heights != null)
                        {
                            var element = cell.AddComponent<LayoutElement>();
                            element.minHeight = heights[i].x;
                            element.preferredHeight = heights[i].y;
                        }
                    }
                    Root.SetActive(true);
                }
                catch { Dispose(); throw; }
            }

            public void Dispose() => Object.DestroyImmediate(Root);
        }

        static object Get(object target, string name) => typeof(TableLayoutGroup).GetField(name, Any).GetValue(target);
        static void Set(object target, string name, object value) => typeof(TableLayoutGroup).GetField(name, Any).SetValue(target, value);

        static void Call(object target, string name)
        {
            try { target.GetType().GetMethod(name, Any).Invoke(target, null); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static float X(RectTransform cell) => cell.anchoredPosition.x;
        static float Y(RectTransform cell) => -cell.anchoredPosition.y;

        [Test]
        public void DeclarationKeepsOriginalAttributesFieldsAndMembers()
        {
            Type type = typeof(TableLayoutGroup);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("UnityEngine.UI.Extensions.TableLayoutGroup"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(LayoutGroup)));
            Assert.That(type.IsAbstract || type.IsSealed, Is.False);

            var attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Select(a => a.AttributeType).ToArray(),
                Is.EqualTo(new[] { typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute), typeof(AddComponentMenu) }));
            for (int i = 0; i < 2; i++)
            {
                Assert.That(Convert.ToInt32(attributes[i].ConstructorArguments[0].Value), Is.EqualTo(i + 1));
                Assert.That(attributes[i].ConstructorArguments[1].Value, Is.EqualTo(false));
            }
            Assert.That(attributes[2].ConstructorArguments.Select(a => a.Value).ToArray(), Is.EqualTo(new object[] { "Layout/Extensions/Table Layout Group" }));

            Type corner = type.GetNestedTypes(Declared).Single();
            Assert.That(corner, Is.EqualTo(typeof(TableLayoutGroup.Corner)));
            Assert.That(corner.IsNestedPublic && corner.IsEnum, Is.True);
            Assert.That(Enum.GetUnderlyingType(corner), Is.EqualTo(typeof(int)));
            Assert.That(Enum.GetNames(corner), Is.EqualTo(new[] { "UpperLeft", "UpperRight", "LowerLeft", "LowerRight" }));
            Assert.That(Enum.GetValues(corner).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));

            // dump.cs 0x60..0x88, with the attribute order of each field.
            string[] names = { "m_startCorner", "m_columnWidths", "m_minimumRowHeight", "m_flexibleRowHeight", "m_minColumnSpacing",
                "m_uniformColumnSpacing", "m_rowSpacing", "m_columnSpacing", "m_preferredRowHeights" };
            Type[] types = { corner, typeof(float[]), typeof(float), typeof(bool), typeof(float), typeof(bool), typeof(float), typeof(float), typeof(float[]) };
            string[] tooltips = {
                "The starting corner from which the cells should be arranged.",
                "The widths of all the columns in the table.",
                "The minimum height for any row in the table.",
                "Expand rows to fit the cell with the highest preferred height.",
                "The minimum horizontal spacing between each cell in the table.",
                "Space columns to uniformly fit. Otherwise MinColumnSpacing value is used.",
                "The vertical spacing between each row in the table." };
            bool[] tooltipFirst = { true, false, true, true, false, false, true };
            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(names));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(types));
            for (int i = 0; i < fields.Length; i++)
            {
                Assert.That(fields[i].Attributes, Is.EqualTo(i == 8 ? FieldAttributes.Private : FieldAttributes.Family), names[i]);
                var data = fields[i].GetCustomAttributesData();
                if (i >= tooltips.Length)
                {
                    Assert.That(data, Is.Empty, names[i]);
                    continue;
                }
                Type[] expected = tooltipFirst[i] ? new[] { typeof(TooltipAttribute), typeof(SerializeField) } : new[] { typeof(SerializeField), typeof(TooltipAttribute) };
                Assert.That(data.Select(a => a.AttributeType).ToArray(), Is.EqualTo(expected), names[i]);
                Assert.That(data.Single(a => a.AttributeType == typeof(TooltipAttribute)).ConstructorArguments.Single().Value, Is.EqualTo(tooltips[i]));
            }

            string[] properties = { "StartCorner", "ColumnWidths", "MinimumRowHeight", "FlexibleRowHeight", "MinColumnSpacing", "UniformColumnSpacing", "RowSpacing" };
            PropertyInfo[] declared = type.GetProperties(Declared);
            Assert.That(declared.Select(p => p.Name).ToArray(), Is.EqualTo(properties));
            Assert.That(declared.Select(p => p.PropertyType).ToArray(), Is.EqualTo(new[] { corner, typeof(float[]), typeof(float), typeof(bool), typeof(float), typeof(bool), typeof(float) }));
            Assert.That(declared.All(p => p.GetMethod.IsPublic && p.SetMethod.IsPublic), Is.True);

            foreach (string name in new[] { "CalculateLayoutInputHorizontal", "CalculateLayoutInputVertical", "SetLayoutHorizontal", "SetLayoutVertical" })
            {
                MethodInfo method = type.GetMethod(name, Declared);
                Assert.That(method.IsPublic && method.IsVirtual && method.GetBaseDefinition().DeclaringType == typeof(LayoutGroup), Is.True, name);
            }
            MethodInfo onEnable = type.GetMethod("OnEnable", Declared);
            Assert.That(onEnable.IsFamily && onEnable.IsVirtual && onEnable.GetBaseDefinition() != onEnable, Is.True);
            Assert.That(type.GetMethods(Declared).Length, Is.EqualTo(19));
            Assert.That(type.GetConstructors(Declared).Single().IsPublic, Is.True);
        }

        // .ctor 0x1b3efe0: a fresh { 96 } array, 32 minimum row height and flexible rows.
        [Test]
        public void ConstructorSeedsOneColumnOfNinetySixAndFlexibleThirtyTwoRows()
        {
            using (var first = new Table(0, new Vector2(100f, 100f)))
            using (var second = new Table(0, new Vector2(100f, 100f)))
            {
                TableLayoutGroup group = first.Group;
                Assert.That(group.ColumnWidths, Is.EqualTo(new[] { 96f }));
                Assert.That(group.ColumnWidths, Is.Not.SameAs(second.Group.ColumnWidths));
                Assert.That(group.MinimumRowHeight, Is.EqualTo(32f));
                Assert.That(group.FlexibleRowHeight, Is.True);
                Assert.That(group.StartCorner, Is.EqualTo(TableLayoutGroup.Corner.UpperLeft));
                Assert.That(group.MinColumnSpacing, Is.Zero);
                Assert.That(group.UniformColumnSpacing, Is.False);
                Assert.That(group.RowSpacing, Is.Zero);
                Assert.That(Get(group, "m_columnSpacing"), Is.EqualTo(0f));
                Assert.That(Get(group, "m_preferredRowHeights"), Is.Null);
            }
        }

        // 0x060000a8..b5: each accessor reads and SetProperty writes its own field.
        [Test]
        public void PropertiesReadAndWriteTheirOwnFields()
        {
            using (var table = new Table(0, new Vector2(100f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                var widths = new[] { 1f, 2f };
                group.StartCorner = TableLayoutGroup.Corner.LowerRight;
                group.ColumnWidths = widths;
                group.MinimumRowHeight = 3f;
                group.FlexibleRowHeight = false;
                group.MinColumnSpacing = 4f;
                group.UniformColumnSpacing = true;
                group.RowSpacing = 5f;

                Assert.That(Get(group, "m_startCorner"), Is.EqualTo(TableLayoutGroup.Corner.LowerRight));
                Assert.That(Get(group, "m_columnWidths"), Is.SameAs(widths));
                Assert.That(Get(group, "m_minimumRowHeight"), Is.EqualTo(3f));
                Assert.That(Get(group, "m_flexibleRowHeight"), Is.EqualTo(false));
                Assert.That(Get(group, "m_minColumnSpacing"), Is.EqualTo(4f));
                Assert.That(Get(group, "m_uniformColumnSpacing"), Is.EqualTo(true));
                Assert.That(Get(group, "m_rowSpacing"), Is.EqualTo(5f));
                Assert.That(Get(group, "m_columnSpacing"), Is.EqualTo(0f));

                // Every field gets a value distinct from the others (the two bools differ
                // from each other, the floats from each other and m_columnSpacing), so a
                // getter wired to the wrong field fails.
                var otherWidths = new[] { 8f };
                Set(group, "m_startCorner", TableLayoutGroup.Corner.UpperRight);
                Set(group, "m_columnWidths", otherWidths);
                Set(group, "m_minimumRowHeight", 6f);
                Set(group, "m_flexibleRowHeight", true);
                Set(group, "m_minColumnSpacing", 9f);
                Set(group, "m_uniformColumnSpacing", false);
                Set(group, "m_rowSpacing", 7f);
                Set(group, "m_columnSpacing", 10f);
                Assert.That(group.StartCorner, Is.EqualTo(TableLayoutGroup.Corner.UpperRight));
                Assert.That(group.ColumnWidths, Is.SameAs(otherWidths));
                Assert.That(group.MinimumRowHeight, Is.EqualTo(6f));
                Assert.That(group.FlexibleRowHeight, Is.True);
                Assert.That(group.MinColumnSpacing, Is.EqualTo(9f));
                Assert.That(group.UniformColumnSpacing, Is.False);
                Assert.That(group.RowSpacing, Is.EqualTo(7f));
            }
        }

        // 0x1b3e4a0, non-uniform: the widths of the occupied columns plus one minimum
        // spacing for every declared column (not count - 1, as upstream).
        [Test]
        public void HorizontalInputAddsOccupiedWidthsAndOneMinimumSpacingPerColumn()
        {
            using (var table = new Table(2, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                group.padding = new RectOffset(3, 5, 0, 0);
                group.ColumnWidths = new[] { 10f, 20f, 30f };
                group.MinColumnSpacing = 4f;

                group.CalculateLayoutInputHorizontal();

                // 8 padding + 10 + 20 (two cells) + 3 columns * 4.
                Assert.That(group.minWidth, Is.EqualTo(50f));
                Assert.That(group.preferredWidth, Is.EqualTo(50f));
                Assert.That(group.flexibleWidth, Is.Zero);
                Assert.That(Get(group, "m_columnSpacing"), Is.EqualTo(4f));
            }
        }

        // 0x1b3e4a0, uniform: spacing is max(free width / column count, minimum) and
        // the input becomes the content plus the free width less one spacing.
        [Test]
        public void UniformHorizontalInputSharesTheFreeWidthWithTheMinimumAsFloor()
        {
            using (var table = new Table(3, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                group.padding = new RectOffset(3, 5, 0, 0);
                group.ColumnWidths = new[] { 10f, 20f, 30f };
                group.UniformColumnSpacing = true;
                group.MinColumnSpacing = 4f;

                // content 68, free 132, 132 / 3 = 44 > 4.
                group.CalculateLayoutInputHorizontal();
                Assert.That(Get(group, "m_columnSpacing"), Is.EqualTo(44f));
                Assert.That(group.minWidth, Is.EqualTo(156f));
                Assert.That(group.preferredWidth, Is.EqualTo(156f));

                // The minimum wins when the share is smaller.
                group.MinColumnSpacing = 50f;
                group.CalculateLayoutInputHorizontal();
                Assert.That(Get(group, "m_columnSpacing"), Is.EqualTo(50f));
                Assert.That(group.minWidth, Is.EqualTo(150f));
                Assert.That(group.flexibleWidth, Is.Zero);
            }
        }

        // 0x1b3e599 divides the free width by m_columnWidths.Length, not by the
        // occupied column count: two cells in three columns share 162 three ways.
        [Test]
        public void UniformHorizontalInputDividesByTheDeclaredColumnCount()
        {
            using (var table = new Table(2, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                RectTransform[] cells = table.Cells;
                group.padding = new RectOffset(3, 5, 0, 0);
                group.childAlignment = TextAnchor.UpperCenter;
                group.ColumnWidths = new[] { 10f, 20f, 30f };
                group.UniformColumnSpacing = true;
                group.MinColumnSpacing = 4f;

                // content 8 + 10 + 20 = 38, free 162, 162 / 3 = 54 (162 / 2 would give 81).
                group.CalculateLayoutInputHorizontal();
                Assert.That(Get(group, "m_columnSpacing"), Is.EqualTo(54f));
                Assert.That(group.minWidth, Is.EqualTo(146f));
                Assert.That(group.preferredWidth, Is.EqualTo(146f));
                Assert.That(group.flexibleWidth, Is.Zero);

                // Row width 10 + 20 + 54 = 84: start 3 + (200 - 92) / 2 = 57.
                group.SetLayoutHorizontal();
                Assert.That(new[] { X(cells[0]), X(cells[1]) }, Is.EqualTo(new[] { 57f, 121f }));
            }
        }

        // 0x1b3e5f0: ceil(cells / columns) rows; flexible rows take the largest cell
        // minimum and preferred heights (floored by the minimum row height), stop at
        // the last cell, and report flexible height 1.
        [Test]
        public void FlexibleVerticalInputTakesPerRowMaximaAndKeepsThePreferredHeights()
        {
            var heights = new[] { new Vector2(7f, 30f), new Vector2(15f, 10f), new Vector2(3f, 4f) };
            using (var table = new Table(3, new Vector2(200f, 100f), heights))
            {
                TableLayoutGroup group = table.Group;
                group.padding = new RectOffset(0, 0, 2, 6);
                group.ColumnWidths = new[] { 10f, 20f };
                group.MinimumRowHeight = 12f;
                group.RowSpacing = 5f;

                group.CalculateLayoutInputHorizontal();
                group.CalculateLayoutInputVertical();

                // Row 0: min max(12, 7, 15) = 15, preferred max(12, 30, 15) = 30; row 1: 12 and 12.
                Assert.That(Get(group, "m_preferredRowHeights"), Is.EqualTo(new[] { 30f, 12f }));
                Assert.That(group.minHeight, Is.EqualTo(8f + 5f + 15f + 12f));
                Assert.That(group.preferredHeight, Is.EqualTo(8f + 5f + 30f + 12f));
                Assert.That(group.flexibleHeight, Is.EqualTo(1f));

                // Negative control on the same cells: fixed rows ignore the cell heights.
                group.FlexibleRowHeight = false;
                group.CalculateLayoutInputVertical();
                Assert.That(Get(group, "m_preferredRowHeights"), Is.EqualTo(new[] { 12f, 12f }));
                Assert.That(group.minHeight, Is.EqualTo(8f + 5f + 24f));
                Assert.That(group.preferredHeight, Is.EqualTo(8f + 5f + 24f));
                Assert.That(group.flexibleHeight, Is.EqualTo(1f));
            }
        }

        // 0x1b3e5f0: one row adds no row spacing, and each pass allocates a new
        // row-height array. The rowCount >= 2 guard itself is pinned by the zero-row case.
        [Test]
        public void SingleRowVerticalInputAddsNoRowSpacing()
        {
            using (var table = new Table(2, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                group.padding = new RectOffset(0, 0, 2, 6);
                group.ColumnWidths = new[] { 10f, 20f };
                group.MinimumRowHeight = 12f;
                group.RowSpacing = 5f;

                group.CalculateLayoutInputHorizontal();
                group.CalculateLayoutInputVertical();
                object first = Get(group, "m_preferredRowHeights");
                Assert.That(first, Is.EqualTo(new[] { 12f }));
                Assert.That(group.minHeight, Is.EqualTo(20f));
                Assert.That(group.preferredHeight, Is.EqualTo(20f));

                group.CalculateLayoutInputVertical();
                Assert.That(Get(group, "m_preferredRowHeights"), Is.Not.SameAs(first));
            }
        }

        // 0x1b3e702 (cmp r14d, 2; jl): with no rows the (rows - 1) * spacing term is
        // skipped, so the input is the padding alone rather than 8 - 5 = 3.
        [Test]
        public void ZeroRowVerticalInputAddsNoNegativeRowSpacing()
        {
            using (var table = new Table(0, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                group.padding = new RectOffset(0, 0, 2, 6);
                group.RowSpacing = 5f;
                group.MinimumRowHeight = 12f;

                foreach (bool flexible in new[] { true, false })
                {
                    group.FlexibleRowHeight = flexible;
                    group.CalculateLayoutInputHorizontal();
                    group.CalculateLayoutInputVertical();
                    Assert.That(Get(group, "m_preferredRowHeights"), Is.EqualTo(new float[0]), flexible.ToString());
                    Assert.That(group.minHeight, Is.EqualTo(8f), flexible.ToString());
                    Assert.That(group.preferredHeight, Is.EqualTo(8f), flexible.ToString());
                    Assert.That(group.flexibleHeight, Is.EqualTo(1f), flexible.ToString());
                }
            }
        }

        // 0x1b3ea00: cells advance by width + the computed column spacing; a row with
        // fewer cells is aligned on its own width; odd corners run right to left.
        [Test]
        public void HorizontalLayoutAlignsEachRowOnItsOwnCellsAndMirrorsOddCorners()
        {
            using (var table = new Table(3, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                RectTransform[] cells = table.Cells;
                group.padding = new RectOffset(3, 5, 0, 0);
                group.childAlignment = TextAnchor.UpperCenter;
                group.ColumnWidths = new[] { 20f, 30f };
                group.MinColumnSpacing = 4f;

                foreach (TableLayoutGroup.Corner corner in new[] { TableLayoutGroup.Corner.UpperLeft, TableLayoutGroup.Corner.LowerLeft })
                {
                    group.StartCorner = corner;
                    group.CalculateLayoutInputHorizontal();
                    group.SetLayoutHorizontal();
                    // Row 0 needs 54: start 3 + (200 - 62) / 2 = 72. Row 1 needs 20: start 3 + (200 - 28) / 2 = 89.
                    Assert.That(new[] { X(cells[0]), X(cells[1]), X(cells[2]) }, Is.EqualTo(new[] { 72f, 96f, 89f }), corner.ToString());
                    Assert.That(new[] { cells[0].sizeDelta.x, cells[1].sizeDelta.x, cells[2].sizeDelta.x }, Is.EqualTo(new[] { 20f, 30f, 20f }));
                }

                foreach (TableLayoutGroup.Corner corner in new[] { TableLayoutGroup.Corner.UpperRight, TableLayoutGroup.Corner.LowerRight })
                {
                    group.StartCorner = corner;
                    group.CalculateLayoutInputHorizontal();
                    group.SetLayoutHorizontal();
                    Assert.That(new[] { X(cells[0]), X(cells[1]), X(cells[2]) }, Is.EqualTo(new[] { 106f, 72f, 89f }), corner.ToString());
                }
            }
        }

        // 0x1b3ea00: consecutive full rows reuse the cached start offset (je 0x1b3ec42
        // at 0x1b3eb77 when the cell count repeats); only the short last row recomputes it.
        [Test]
        public void HorizontalLayoutReusesTheStartOffsetForConsecutiveFullRows()
        {
            using (var table = new Table(5, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                RectTransform[] cells = table.Cells;
                group.padding = new RectOffset(3, 5, 0, 0);
                group.childAlignment = TextAnchor.UpperCenter;
                group.ColumnWidths = new[] { 20f, 30f };
                group.MinColumnSpacing = 4f;

                // Full rows need 54: start 72; the one-cell row needs 20: start 89.
                group.StartCorner = TableLayoutGroup.Corner.UpperLeft;
                group.CalculateLayoutInputHorizontal();
                group.SetLayoutHorizontal();
                Assert.That(cells.Select(X).ToArray(), Is.EqualTo(new[] { 72f, 96f, 72f, 96f, 89f }));

                // Odd corner: full rows start from 72 + 54 and run right to left.
                group.StartCorner = TableLayoutGroup.Corner.UpperRight;
                group.CalculateLayoutInputHorizontal();
                group.SetLayoutHorizontal();
                Assert.That(cells.Select(X).ToArray(), Is.EqualTo(new[] { 106f, 72f, 106f, 72f, 89f }));
            }
        }

        // 0x1b3ea00 reads m_columnSpacing (here the uniform share), not the minimum.
        [Test]
        public void HorizontalLayoutUsesTheSpacingFromTheInputPass()
        {
            using (var table = new Table(3, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                RectTransform[] cells = table.Cells;
                group.padding = new RectOffset(3, 5, 0, 0);
                group.childAlignment = TextAnchor.UpperCenter;
                group.ColumnWidths = new[] { 20f, 30f };
                group.MinColumnSpacing = 4f;
                group.UniformColumnSpacing = true;

                // Two occupied columns: content 58, free 142, spacing 71.
                group.CalculateLayoutInputHorizontal();
                Assert.That(Get(group, "m_columnSpacing"), Is.EqualTo(71f));
                group.SetLayoutHorizontal();
                // Row 0 needs 121: start 3 + (200 - 129) / 2 = 38.5.
                Assert.That(new[] { X(cells[0]), X(cells[1]), X(cells[2]) }, Is.EqualTo(new[] { 38.5f, 129.5f, 89f }));
            }
        }

        // 0x1b3ea00: an empty width array is replaced by { 300, 300 } before laying out.
        [Test]
        public void EmptyColumnWidthsFallBackToTwoThreeHundredColumns()
        {
            using (var table = new Table(1, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                group.padding = new RectOffset(3, 5, 0, 0);
                group.ColumnWidths = new float[0];

                group.CalculateLayoutInputHorizontal();
                group.SetLayoutHorizontal();

                Assert.That(group.ColumnWidths, Is.EqualTo(new[] { 300f, 300f }));
                Assert.That(table.Cells[0].sizeDelta.x, Is.EqualTo(300f));
                Assert.That(X(table.Cells[0]), Is.EqualTo(3f));
            }
        }

        // 0x1b3ed40: rows use the stored preferred heights and row spacing, lower
        // corners stack upwards, and the stored heights are released afterwards.
        [Test]
        public void VerticalLayoutStacksStoredRowHeightsAndReleasesThem()
        {
            var heights = new[] { new Vector2(7f, 30f), new Vector2(15f, 10f), new Vector2(3f, 4f) };
            using (var table = new Table(3, new Vector2(200f, 100f), heights))
            {
                TableLayoutGroup group = table.Group;
                RectTransform[] cells = table.Cells;
                group.padding = new RectOffset(0, 0, 2, 6);
                group.ColumnWidths = new[] { 10f, 20f };
                group.MinimumRowHeight = 12f;
                group.RowSpacing = 5f;

                foreach (TableLayoutGroup.Corner corner in new[] { TableLayoutGroup.Corner.UpperLeft, TableLayoutGroup.Corner.UpperRight })
                {
                    group.StartCorner = corner;
                    group.CalculateLayoutInputHorizontal();
                    group.CalculateLayoutInputVertical();
                    group.SetLayoutVertical();
                    Assert.That(new[] { Y(cells[0]), Y(cells[1]), Y(cells[2]) }, Is.EqualTo(new[] { 2f, 2f, 37f }), corner.ToString());
                    Assert.That(new[] { cells[0].sizeDelta.y, cells[1].sizeDelta.y, cells[2].sizeDelta.y }, Is.EqualTo(new[] { 30f, 30f, 12f }));
                    Assert.That(Get(group, "m_preferredRowHeights"), Is.Null);
                }

                foreach (TableLayoutGroup.Corner corner in new[] { TableLayoutGroup.Corner.LowerLeft, TableLayoutGroup.Corner.LowerRight })
                {
                    group.StartCorner = corner;
                    group.CalculateLayoutInputHorizontal();
                    group.CalculateLayoutInputVertical();
                    group.SetLayoutVertical();
                    // Required 47 from start 2: row 0 at 49 - 30 = 19, row 1 at 19 - 5 - 12 = 2.
                    Assert.That(new[] { Y(cells[0]), Y(cells[1]), Y(cells[2]) }, Is.EqualTo(new[] { 19f, 19f, 2f }), corner.ToString());
                    Assert.That(Get(group, "m_preferredRowHeights"), Is.Null);
                }
            }
        }

        // 0x1b3ed40: GetStartOffset(1, tableHeight) receives the stored heights plus
        // (rows - 1) * m_rowSpacing (0x1b3ee33), visible once the alignment centres it.
        [Test]
        public void VerticalLayoutAlignsTheTableHeightIncludingRowSpacing()
        {
            var heights = new[] { new Vector2(7f, 30f), new Vector2(15f, 10f), new Vector2(3f, 4f) };
            using (var table = new Table(3, new Vector2(200f, 100f), heights))
            {
                TableLayoutGroup group = table.Group;
                RectTransform[] cells = table.Cells;
                group.padding = new RectOffset(0, 0, 2, 6);
                group.childAlignment = TextAnchor.MiddleLeft;
                group.ColumnWidths = new[] { 10f, 20f };
                group.MinimumRowHeight = 12f;
                group.RowSpacing = 5f;

                // Table 30 + 12 + 5 = 47, required 55: start 2 + (100 - 55) * 0.5 = 24.5
                // (27 if the spacing were left out of the argument).
                foreach (TableLayoutGroup.Corner corner in new[] { TableLayoutGroup.Corner.UpperLeft, TableLayoutGroup.Corner.UpperRight })
                {
                    group.StartCorner = corner;
                    group.CalculateLayoutInputHorizontal();
                    group.CalculateLayoutInputVertical();
                    group.SetLayoutVertical();
                    Assert.That(new[] { Y(cells[0]), Y(cells[1]), Y(cells[2]) }, Is.EqualTo(new[] { 24.5f, 24.5f, 59.5f }), corner.ToString());
                }

                // Lower corners start from 24.5 + 47 = 71.5 and stack upwards.
                foreach (TableLayoutGroup.Corner corner in new[] { TableLayoutGroup.Corner.LowerLeft, TableLayoutGroup.Corner.LowerRight })
                {
                    group.StartCorner = corner;
                    group.CalculateLayoutInputHorizontal();
                    group.CalculateLayoutInputVertical();
                    group.SetLayoutVertical();
                    Assert.That(new[] { Y(cells[0]), Y(cells[1]), Y(cells[2]) }, Is.EqualTo(new[] { 41.5f, 41.5f, 24.5f }), corner.ToString());
                }
            }
        }

        // 0x1b3efc0: LayoutGroup.OnEnable then Canvas.ForceUpdateCanvases, which raises
        // willRenderCanvases once; a plain LayoutGroup's OnEnable raises nothing.
        [Test]
        public void OnEnableForcesOneCanvasUpdate()
        {
            int raised = 0;
            Canvas.WillRenderCanvases handler = () => raised++;
            using (var table = new Table(0, new Vector2(100f, 100f)))
            {
                GameObject control = null;
                Canvas.willRenderCanvases += handler;
                try
                {
                    control = new GameObject("HorizontalLayoutGroup control", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                    control.SetActive(false);
                    var plain = control.AddComponent<HorizontalLayoutGroup>();
                    plain.enabled = false;
                    control.SetActive(true);

                    Call(plain, "OnEnable");
                    Assert.That(raised, Is.Zero);

                    Call(table.Group, "OnEnable");
                    Assert.That(raised, Is.EqualTo(1));
                }
                finally
                {
                    Canvas.willRenderCanvases -= handler;
                    if (control != null)
                        Object.DestroyImmediate(control);
                }
            }
        }

        // 0x1b3efc0 calls LayoutGroup.OnEnable (SetDirty queues this root) before
        // Canvas.ForceUpdateCanvases, so the rebuild has already run when enabling
        // returns. Reversed or without the base call the cells would stay at X 0.
        [Test]
        public void EnablingRebuildsTheLayoutWithinTheSameCall()
        {
            using (var table = new Table(2, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                RectTransform[] cells = table.Cells;
                group.padding = new RectOffset(3, 5, 0, 0);
                group.childAlignment = TextAnchor.UpperCenter;
                group.ColumnWidths = new[] { 20f, 30f };
                group.MinColumnSpacing = 4f;
                Assert.That(new[] { X(cells[0]), X(cells[1]) }, Is.EqualTo(new[] { 0f, 0f }));

                group.enabled = true;

                // Row width 20 + 30 + 4 = 54: start 3 + (200 - 62) / 2 = 72.
                Assert.That(new[] { X(cells[0]), X(cells[1]) }, Is.EqualTo(new[] { 72f, 96f }));
                Assert.That(new[] { cells[0].sizeDelta.x, cells[1].sizeDelta.x }, Is.EqualTo(new[] { 20f, 30f }));
            }
        }

        // Setters 0x060000a9..b5 tail-jump to LayoutGroup.SetProperty<T>: a new value
        // is written and marks the enabled group dirty, an equal value returns first.
        // Cell 0 holds a hand-set sentinel to show whether the forced update rebuilt it.
        [Test]
        public void SettersMarkTheLayoutForRebuildOnlyWhenTheValueChanges()
        {
            using (var table = new Table(2, new Vector2(200f, 100f)))
            {
                TableLayoutGroup group = table.Group;
                RectTransform[] cells = table.Cells;
                group.padding = new RectOffset(3, 5, 0, 0);
                group.childAlignment = TextAnchor.UpperCenter;
                group.ColumnWidths = new[] { 20f, 30f };
                group.MinColumnSpacing = 4f;
                // Enabling flushes the first rebuild, so nothing of this group stays queued.
                group.enabled = true;
                Canvas.ForceUpdateCanvases();
                Assert.That(new[] { X(cells[0]), X(cells[1]) }, Is.EqualTo(new[] { 72f, 96f }));

                // Row width 20 + 30 + 10 = 60: start 3 + (200 - 68) / 2 = 69.
                group.MinColumnSpacing = 10f;
                Canvas.ForceUpdateCanvases();
                Assert.That(new[] { X(cells[0]), X(cells[1]) }, Is.EqualTo(new[] { 69f, 99f }));

                var sentinel = new Vector2(1f, 1f);
                cells[0].sizeDelta = sentinel;
                group.MinColumnSpacing = 10f;
                Canvas.ForceUpdateCanvases();
                Assert.That(cells[0].sizeDelta, Is.EqualTo(sentinel), "MinColumnSpacing with an equal value");

                var setters = new (string Name, Action Same, Action Changed)[]
                {
                    ("StartCorner", () => group.StartCorner = group.StartCorner, () => group.StartCorner = TableLayoutGroup.Corner.UpperRight),
                    ("ColumnWidths", () => group.ColumnWidths = group.ColumnWidths, () => group.ColumnWidths = new[] { 20f, 30f }),
                    ("MinimumRowHeight", () => group.MinimumRowHeight = group.MinimumRowHeight, () => group.MinimumRowHeight = 10f),
                    ("FlexibleRowHeight", () => group.FlexibleRowHeight = group.FlexibleRowHeight, () => group.FlexibleRowHeight = false),
                    ("MinColumnSpacing", () => group.MinColumnSpacing = group.MinColumnSpacing, () => group.MinColumnSpacing = 6f),
                    ("UniformColumnSpacing", () => group.UniformColumnSpacing = group.UniformColumnSpacing, () => group.UniformColumnSpacing = true),
                    ("RowSpacing", () => group.RowSpacing = group.RowSpacing, () => group.RowSpacing = 4f),
                };
                foreach (var setter in setters)
                {
                    cells[0].sizeDelta = sentinel;
                    setter.Same();
                    Canvas.ForceUpdateCanvases();
                    Assert.That(cells[0].sizeDelta, Is.EqualTo(sentinel), setter.Name + " with an equal value");

                    setter.Changed();
                    Canvas.ForceUpdateCanvases();
                    Assert.That(cells[0].sizeDelta, Is.Not.EqualTo(sentinel), setter.Name + " with a new value");
                }
            }
        }
    }
}
