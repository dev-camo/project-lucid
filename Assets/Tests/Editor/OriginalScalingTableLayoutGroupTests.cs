using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Specification of UnityEngine.UI.Extensions.ScalingTableLayoutGroup
    // (HLUnityUI.Runtime 02000017, MethodDefs 0x06000092..a7) from the shipped
    // x86-64 bodies. Every case owns one hidden, unsaved hierarchy and destroys it.
    // The group is disabled before its GameObject activates, and except for the
    // OnEnable and setter rebuild cases it stays disabled, so neither OnEnable's
    // forced canvas update nor SetDirty touches the shared CanvasUpdateRegistry;
    // the cases drive the ILayoutElement and ILayoutController calls directly in
    // LayoutRebuilder order.
    // The two enabled cases do touch shared state: Canvas.ForceUpdateCanvases is
    // global, so it also drains every layout and graphic rebuild queued by other
    // tests or open scenes in the editor domain, and destroying their enabled
    // group in Dispose runs LayoutGroup.OnDisable, which marks the destroyed rect
    // for rebuild in the shared registry (disabling first would mark it the same
    // way; the registry drops the dead entry on its next update). Children use a
    // (0, 1) pivot, so anchoredPosition reads back the exact pos SetChildAlongAxis
    // received (x = pos, y = -pos). The OnEnable case removes its
    // Canvas.willRenderCanvases handler in a finally block.
    [TestFixture]
    public sealed class OriginalScalingTableLayoutGroupTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        sealed class Table : IDisposable
        {
            public readonly GameObject Root;
            public readonly RectTransform Rect;
            public readonly ScalingTableLayoutGroup Group;
            public readonly List<RectTransform> Cells = new List<RectTransform>();

            public Table(float width, float height, int cells, bool enabled = false)
            {
                Root = new GameObject("ScalingTableLayoutGroup fixture", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    Root.SetActive(false);
                    Rect = (RectTransform)Root.transform;
                    Rect.sizeDelta = new Vector2(width, height);
                    Group = Root.AddComponent<ScalingTableLayoutGroup>();
                    Group.enabled = enabled;
                    Root.SetActive(true);
                    for (int i = 0; i < cells; ++i)
                    {
                        var cell = new GameObject("cell " + i, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                        var rect = (RectTransform)cell.transform;
                        rect.SetParent(Rect, false);
                        rect.pivot = new Vector2(0f, 1f);
                        Cells.Add(rect);
                    }
                }
                catch { Dispose(); throw; }
            }

            public void Dispose() => Object.DestroyImmediate(Root);

            public float[,] Widths
            {
                get => (float[,])Field("m_preferredColumnWidths").GetValue(Group);
                set => Field("m_preferredColumnWidths").SetValue(Group, value);
            }

            public float[] Heights
            {
                get => (float[])Field("m_preferredRowHeights").GetValue(Group);
                set => Field("m_preferredRowHeights").SetValue(Group, value);
            }

            public float[] Lefts => Cells.Select(c => c.anchoredPosition.x).ToArray();
            public float[] Tops => Cells.Select(c => -c.anchoredPosition.y).ToArray();
            public float[] CellWidths => Cells.Select(c => c.sizeDelta.x).ToArray();
            public float[] CellHeights => Cells.Select(c => c.sizeDelta.y).ToArray();
        }

        static FieldInfo Field(string name) => typeof(ScalingTableLayoutGroup).GetField(name, Any);

        // 400 x 300 rect, padding l10 r20 t10 b20, spacing 5 / 6, three columns, seven cells.
        static Table SevenCells(ScalingTableLayoutGroup.Corner corner = ScalingTableLayoutGroup.Corner.UpperLeft)
        {
            var table = new Table(400f, 300f, 7);
            table.Group.padding = new RectOffset(10, 20, 10, 20);
            table.Group.MaximumColumns = 3;
            table.Group.ColumnSpacing = 5f;
            table.Group.RowSpacing = 6f;
            table.Group.StartCorner = corner;
            return table;
        }

        [Test]
        public void DeclarationKeepsTheShippedSurface()
        {
            Type type = typeof(ScalingTableLayoutGroup);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("UnityEngine.UI.Extensions"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(LayoutGroup)));
            Assert.That(type.IsPublic && !type.IsAbstract && !type.IsSealed, Is.True);

            var options = type.GetCustomAttributesData().Where(a => a.AttributeType == typeof(Il2CppSetOptionAttribute)).ToArray();
            Assert.That(options.Select(o => Convert.ToInt32(o.ConstructorArguments[0].Value)).OrderBy(v => v).ToArray(), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(options.Select(o => o.ConstructorArguments[1].Value).ToArray(), Is.EqualTo(new object[] { false, false }));
            var menu = (AddComponentMenu)type.GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Layout/Extensions/Scaling Table Layout Group"));
            Assert.That(menu.componentOrder, Is.EqualTo(0));
            Assert.That(type.GetCustomAttributesData().Count, Is.EqualTo(3));

            // dump.cs TypeDefIndex 11849, offsets 0x60..0x88.
            var fields = type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "m_startCorner", "m_maximumColumns", "m_maximumExpectedRows", "m_RowAddMode", "m_minimumRowHeight",
                "m_minimumColumnWidth", "m_columnSpacing", "m_rowSpacing", "m_preferredRowHeights", "m_preferredColumnWidths",
            }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[]
            {
                typeof(ScalingTableLayoutGroup.Corner), typeof(int), typeof(int), typeof(ScalingTableLayoutGroup.RowAddMode),
                typeof(float), typeof(float), typeof(float), typeof(float), typeof(float[]), typeof(float[,]),
            }));
            Assert.That(fields.Take(8).All(f => f.IsFamily), Is.True);
            Assert.That(fields.Skip(8).All(f => f.IsPrivate && f.GetCustomAttributesData().Count == 0), Is.True);
            Assert.That(fields.Take(8).All(f => f.IsDefined(typeof(SerializeField), false)), Is.True);
            Assert.That(fields.Take(8).Select(f => ((TooltipAttribute)f.GetCustomAttribute(typeof(TooltipAttribute), false)).tooltip).ToArray(), Is.EqualTo(new[]
            {
                "The starting corner from which the cells should be arranged.",
                "The widths of all the columns in the table. Also denotes the maximum number of columns.",
                "the Maximum number of rows expected in this table, can over flow if needed.",
                "The child adding mode used to determine how we react to new rows",
                "The minimum height for any row in the table.",
                "The minimum width for any column in the table.",
                "The minimum horizontal spacing between each cell in the table.",
                "The minimum vertical spacing between each row in the table.",
            }));
            Assert.That(fields.Take(8).Select(f => f.GetCustomAttributesData().Count).ToArray(), Is.EqualTo(new[] { 2, 3, 3, 2, 2, 2, 2, 2 }));
            foreach (var ranged in new[] { fields[1], fields[2] })
            {
                var range = (UnityEngine.RangeAttribute)ranged.GetCustomAttribute(typeof(UnityEngine.RangeAttribute), false);
                Assert.That(new[] { range.min, range.max }, Is.EqualTo(new[] { 1f, 100f }), ranged.Name);
            }

            Assert.That(type.GetProperties(Declared).OrderBy(p => p.MetadataToken).Select(p => p.Name + ":" + p.PropertyType.Name).ToArray(), Is.EqualTo(new[]
            {
                "StartCorner:Corner", "MaximumColumns:Int32", "MaximumExpectedRows:Int32", "NewRowAddMode:RowAddMode",
                "MinimumRowHeight:Single", "MinimumColumnWidth:Single", "ColumnSpacing:Single", "RowSpacing:Single",
            }));
            foreach (var name in new[] { "CalculateLayoutInputHorizontal", "CalculateLayoutInputVertical", "SetLayoutHorizontal", "SetLayoutVertical" })
            {
                var method = type.GetMethod(name, Declared);
                Assert.That(method.IsPublic && method.IsVirtual && method.GetBaseDefinition().DeclaringType != type, Is.True, name);
            }
            var onEnable = type.GetMethod("OnEnable", Declared);
            Assert.That(onEnable.IsFamily && onEnable.IsVirtual && onEnable.GetBaseDefinition().DeclaringType != type, Is.True);

            Assert.That(typeof(ScalingTableLayoutGroup.Corner).IsNestedPublic, Is.True);
            Assert.That(Enum.GetNames(typeof(ScalingTableLayoutGroup.Corner)), Is.EqualTo(new[] { "UpperLeft", "UpperRight", "LowerLeft", "LowerRight" }));
            Assert.That(Enum.GetValues(typeof(ScalingTableLayoutGroup.Corner)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(typeof(ScalingTableLayoutGroup.RowAddMode).IsNestedPublic, Is.True);
            Assert.That(Enum.GetNames(typeof(ScalingTableLayoutGroup.RowAddMode)), Is.EqualTo(new[] { "Fill", "Overflow" }));
            Assert.That(Enum.GetValues(typeof(ScalingTableLayoutGroup.RowAddMode)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1 }));
        }

        // .ctor 0x1B3E1D0 stores 0x100000001 at 0x64 (both row/column counts 1) and
        // the pair (32f, 32f) at 0x70 (minimum row height, minimum column width).
        [Test]
        public void ConstructorSetsTheShippedSerializedDefaults()
        {
            using (var table = new Table(100f, 100f, 0))
            {
                var group = table.Group;
                Assert.That(group.StartCorner, Is.EqualTo(ScalingTableLayoutGroup.Corner.UpperLeft));
                Assert.That(group.MaximumColumns, Is.EqualTo(1));
                Assert.That(group.MaximumExpectedRows, Is.EqualTo(1));
                Assert.That(group.NewRowAddMode, Is.EqualTo(ScalingTableLayoutGroup.RowAddMode.Fill));
                Assert.That(group.MinimumRowHeight, Is.EqualTo(32f));
                Assert.That(group.MinimumColumnWidth, Is.EqualTo(32f));
                Assert.That(group.ColumnSpacing, Is.EqualTo(0f));
                Assert.That(group.RowSpacing, Is.EqualTo(0f));
                Assert.That(table.Widths, Is.Null);
                Assert.That(table.Heights, Is.Null);
            }
        }

        // Getters 0x1B3CF60..0x1B3D200 read 0x60..0x7C; setters 0x1B3CF70..0x1B3D210
        // pass the same field to LayoutGroup.SetProperty<T>.
        [Test]
        public void PropertiesReadAndWriteTheirOwnSerializedFields()
        {
            using (var table = new Table(100f, 100f, 0))
            {
                var group = table.Group;
                group.StartCorner = ScalingTableLayoutGroup.Corner.LowerRight;
                group.MaximumColumns = 7;
                group.MaximumExpectedRows = 9;
                group.NewRowAddMode = ScalingTableLayoutGroup.RowAddMode.Overflow;
                group.MinimumRowHeight = 11f;
                group.MinimumColumnWidth = 13f;
                group.ColumnSpacing = 17f;
                group.RowSpacing = 19f;
                var names = new[] { "m_startCorner", "m_maximumColumns", "m_maximumExpectedRows", "m_RowAddMode", "m_minimumRowHeight", "m_minimumColumnWidth", "m_columnSpacing", "m_rowSpacing" };
                Assert.That(names.Select(n => Convert.ToSingle(Field(n).GetValue(group))).ToArray(), Is.EqualTo(new[] { 3f, 7f, 9f, 1f, 11f, 13f, 17f, 19f }));

                Field("m_startCorner").SetValue(group, ScalingTableLayoutGroup.Corner.UpperRight);
                Field("m_maximumColumns").SetValue(group, 23);
                Field("m_maximumExpectedRows").SetValue(group, 29);
                Field("m_RowAddMode").SetValue(group, ScalingTableLayoutGroup.RowAddMode.Fill);
                Field("m_minimumRowHeight").SetValue(group, 31f);
                Field("m_minimumColumnWidth").SetValue(group, 37f);
                Field("m_columnSpacing").SetValue(group, 41f);
                Field("m_rowSpacing").SetValue(group, 43f);
                Assert.That(new[]
                {
                    (float)group.StartCorner, group.MaximumColumns, group.MaximumExpectedRows, (float)group.NewRowAddMode,
                    group.MinimumRowHeight, group.MinimumColumnWidth, group.ColumnSpacing, group.RowSpacing,
                }, Is.EqualTo(new[] { 1f, 23f, 29f, 0f, 31f, 37f, 41f, 43f }));
            }
        }

        // Setters 0x1B3CF70..0x1B3D210 go through LayoutGroup.SetProperty<T>: an equal
        // value returns before SetDirty, a different one marks the enabled group for
        // a layout rebuild. A sentinel cell size shows whether the global
        // Canvas.ForceUpdateCanvases flush rebuilt this group. A setter that assigned
        // its field directly would leave the sentinel after a change.
        [Test]
        public void SettersMarkTheLayoutForRebuildOnlyWhenTheValueChanges()
        {
            using (var table = new Table(300f, 80f, 2))
            {
                // Enabling flushes the first rebuild, so nothing of this group stays queued.
                var group = table.Group;
                group.enabled = true;
                var sentinel = new Vector2(1f, 1f);
                var setters = new (string Name, Action Same, Action Changed)[]
                {
                    ("StartCorner", () => group.StartCorner = group.StartCorner, () => group.StartCorner = ScalingTableLayoutGroup.Corner.UpperRight),
                    ("MaximumColumns", () => group.MaximumColumns = group.MaximumColumns, () => group.MaximumColumns = 2),
                    ("MaximumExpectedRows", () => group.MaximumExpectedRows = group.MaximumExpectedRows, () => group.MaximumExpectedRows = 3),
                    ("NewRowAddMode", () => group.NewRowAddMode = group.NewRowAddMode, () => group.NewRowAddMode = ScalingTableLayoutGroup.RowAddMode.Overflow),
                    ("MinimumRowHeight", () => group.MinimumRowHeight = group.MinimumRowHeight, () => group.MinimumRowHeight = 10f),
                    ("MinimumColumnWidth", () => group.MinimumColumnWidth = group.MinimumColumnWidth, () => group.MinimumColumnWidth = 10f),
                    ("ColumnSpacing", () => group.ColumnSpacing = group.ColumnSpacing, () => group.ColumnSpacing = 4f),
                    ("RowSpacing", () => group.RowSpacing = group.RowSpacing, () => group.RowSpacing = 4f),
                };
                foreach (var setter in setters)
                {
                    table.Cells[0].sizeDelta = sentinel;
                    setter.Same();
                    Canvas.ForceUpdateCanvases();
                    Assert.That(table.Cells[0].sizeDelta, Is.EqualTo(sentinel), setter.Name + " with an equal value");

                    setter.Changed();
                    Canvas.ForceUpdateCanvases();
                    Assert.That(table.Cells[0].sizeDelta, Is.Not.EqualTo(sentinel), setter.Name + " with a new value");
                }
            }
        }

        // CalculateLayoutInputHorizontal 0x1B3D260: columns = min(count, MaximumColumns),
        // rows = ceil(count / MaximumColumns), last row holds count % columns (or a full row).
        // Full rows split rect.width - (padding.horizontal + spacing * (columns - 1)) evenly;
        // the last row splits rect.width - (padding.horizontal + spacing * (lastCount - 1))
        // over its own cells and leaves the unused columns 0. Min and preferred width are
        // spacing * (columns - 1) + padding.horizontal + that available width; flexible is 0.
        [Test]
        public void HorizontalInputSplitsTheWidthAndStretchesTheShortLastRow()
        {
            using (var table = SevenCells())
            {
                table.Group.CalculateLayoutInputHorizontal();
                Assert.That(table.Widths, Is.EqualTo(new float[,] { { 120f, 120f, 120f }, { 120f, 120f, 120f }, { 370f, 0f, 0f } }));
                Assert.That(table.Group.minWidth, Is.EqualTo(400f));
                Assert.That(table.Group.preferredWidth, Is.EqualTo(400f));
                Assert.That(table.Group.flexibleWidth, Is.EqualTo(0f));
                Assert.That(table.Heights, Is.Null);
            }
        }

        // 0x1B3D329..0x1B3D338: the remainder count % columns (idiv) runs before the
        // columns == 0 guard, so MaximumColumns 0 (the setter only calls SetProperty;
        // Range applies in the inspector) throws before the width table is allocated
        // (GenArrayNew 0x1B3D385) or SetLayoutInputForAxis runs.
        [Test]
        public void HorizontalInputWithZeroMaximumColumnsThrowsBeforeTheDeadGuard()
        {
            using (var table = new Table(100f, 100f, 3))
            {
                table.Group.MaximumColumns = 0;
                Assert.Throws<DivideByZeroException>(() => table.Group.CalculateLayoutInputHorizontal());
                Assert.That(table.Widths, Is.Null);
                Assert.That(table.Group.preferredWidth, Is.EqualTo(0f));
            }
        }

        // 0x1B3D2EF..0x1B3D2F9: fewer cells than MaximumColumns narrows the table to the
        // cell count, one row wide, and spacing counts only between those cells.
        [Test]
        public void HorizontalInputNarrowsTheTableToFewerCellsThanColumns()
        {
            using (var table = new Table(400f, 300f, 2))
            {
                table.Group.padding = new RectOffset(10, 20, 0, 0);
                table.Group.MaximumColumns = 4;
                table.Group.ColumnSpacing = 5f;
                table.Group.CalculateLayoutInputHorizontal();
                Assert.That(table.Widths, Is.EqualTo(new float[,] { { 182.5f, 182.5f } }));
                Assert.That(table.Group.preferredWidth, Is.EqualTo(400f));
            }
        }

        // 0x1B3D463..0x1B3D467 and 0x1B3D52A: both cell widths are raised to
        // MinimumColumnWidth, but the reported size (xmm3, 0x1B3D408) is built from
        // the unclamped available width, so the group still reports the rect width.
        [Test]
        public void HorizontalInputClampsCellsToTheMinimumButReportsTheRectWidth()
        {
            using (var table = new Table(100f, 100f, 8))
            {
                table.Group.MaximumColumns = 4;
                table.Group.CalculateLayoutInputHorizontal();
                Assert.That(table.Widths, Is.EqualTo(new float[,] { { 32f, 32f, 32f, 32f }, { 32f, 32f, 32f, 32f } }));
                Assert.That(table.Group.minWidth, Is.EqualTo(100f));
                Assert.That(table.Group.preferredWidth, Is.EqualTo(100f));
            }
        }

        // CalculateLayoutInputVertical 0x1B3D9D0, Fill (0x1B3DADC): the divisor is the
        // table row count; MaximumExpectedRows is ignored. Every row gets
        // max((rect.height - (padding.vertical + spacing * (divisor - 1))) / divisor,
        // MinimumRowHeight) and the reported size is divisor * row + padding.vertical
        // + spacing * (divisor - 1).
        [Test]
        public void VerticalFillDividesTheRectHeightAcrossTheTableRows()
        {
            using (var table = SevenCells())
            {
                table.Group.MaximumExpectedRows = 2;
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.CalculateLayoutInputVertical();
                Assert.That(table.Heights, Is.EqualTo(new[] { 86f, 86f, 86f }));
                Assert.That(table.Group.minHeight, Is.EqualTo(300f));
                Assert.That(table.Group.preferredHeight, Is.EqualTo(300f));
                Assert.That(table.Group.flexibleHeight, Is.EqualTo(0f));
            }
        }

        // Overflow (0x1B3DAE3..0x1B3DB06): the divisor is Math.Min(MaximumExpectedRows,
        // rows); the array still has one entry per table row.
        [TestCase(2)]
        [TestCase(5)]
        public void VerticalOverflowDividesByTheSmallerOfExpectedAndTableRows(int expectedRows)
        {
            // Two expected rows: (300 - (30 + 6)) / 2; five: Math.Min gives the three table rows.
            float rowHeight = expectedRows == 2 ? 132f : 86f;
            using (var table = SevenCells())
            {
                table.Group.NewRowAddMode = ScalingTableLayoutGroup.RowAddMode.Overflow;
                table.Group.MaximumExpectedRows = expectedRows;
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.CalculateLayoutInputVertical();
                Assert.That(table.Heights, Is.EqualTo(new[] { rowHeight, rowHeight, rowHeight }));
                Assert.That(table.Group.preferredHeight, Is.EqualTo(300f));
            }
        }

        // 0x1B3DB1A: any other mode leaves the divisor 0, so the row height is
        // +Infinity and the reported size is 0 * Infinity + padding, NaN.
        [Test]
        public void VerticalUnknownRowAddModeDividesByZeroRows()
        {
            using (var table = new Table(100f, 300f, 4))
            {
                table.Group.MaximumColumns = 2;
                table.Group.NewRowAddMode = (ScalingTableLayoutGroup.RowAddMode)2;
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.CalculateLayoutInputVertical();
                Assert.That(table.Heights, Is.EqualTo(new[] { float.PositiveInfinity, float.PositiveInfinity }));
                Assert.That(float.IsNaN(table.Group.minHeight) && float.IsNaN(table.Group.preferredHeight), Is.True);
                Assert.That(table.Group.flexibleHeight, Is.EqualTo(0f));
            }
        }

        // 0x1B3DB83..0x1B3DB88: unlike the horizontal input, the reported height is
        // built from the clamped row height.
        [Test]
        public void VerticalInputReportsTheClampedRowHeights()
        {
            using (var table = new Table(100f, 100f, 8))
            {
                table.Group.MaximumColumns = 2;
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.CalculateLayoutInputVertical();
                Assert.That(table.Heights, Is.EqualTo(new[] { 32f, 32f, 32f, 32f }));
                Assert.That(table.Group.preferredHeight, Is.EqualTo(128f));
                Assert.That(table.Group.preferredWidth, Is.EqualTo(100f));
            }
        }

        // The vertical input (cmp r12d, 2 at 0x1B3DB91) and SetLayoutVertical
        // (cmp r15d, 2 at 0x1B3E03D) add the row spacing term only for two or more
        // rows, so one row with infinite spacing keeps finite heights and positions.
        // The horizontal NaN comes from the available width, 100 - (Infinity * 0 + 0),
        // not from the unguarded (columns - 1) * spacing term at 0x1B3D408, which
        // cannot be told apart from a guarded one when columns is 1.
        [Test]
        public void SingleRowSkipsTheGuardedVerticalSpacingTerms()
        {
            using (var table = new Table(100f, 100f, 1))
            {
                table.Group.childAlignment = TextAnchor.MiddleCenter;
                table.Group.ColumnSpacing = float.PositiveInfinity;
                table.Group.RowSpacing = float.PositiveInfinity;

                // The NaN available width clamps the cell to 32.
                table.Group.CalculateLayoutInputHorizontal();
                Assert.That(table.Widths, Is.EqualTo(new float[,] { { 32f } }));
                Assert.That(float.IsNaN(table.Group.minWidth) && float.IsNaN(table.Group.preferredWidth), Is.True);

                // Row height NaN clamps to 32; the guarded spacing term is skipped.
                table.Group.CalculateLayoutInputVertical();
                Assert.That(table.Group.minHeight, Is.EqualTo(32f));
                Assert.That(table.Group.preferredHeight, Is.EqualTo(32f));

                // Total 32 without the spacing term: GetStartOffset(1, 32) = (100 - 32) * 0.5.
                table.Group.SetLayoutVertical();
                Assert.That(table.Tops, Is.EqualTo(new[] { 34f }));
                Assert.That(table.CellHeights, Is.EqualTo(new[] { 32f }));
            }
        }

        // SetLayoutHorizontal 0x1B3DC40: (m_startCorner % 2 == 1) starts each row at its
        // right edge, GetStartOffset(0, rowWidth) + rowWidth, and walks left placing a
        // cell at pos - width then stepping by -spacing; otherwise it walks right from
        // GetStartOffset(0, rowWidth). Cells keep child order inside each row.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SetLayoutHorizontalPlacesCellsFromTheStartCorner(int corner)
        {
            using (var table = SevenCells((ScalingTableLayoutGroup.Corner)corner))
            {
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.SetLayoutHorizontal();
                bool right = corner % 2 == 1;
                Assert.That(table.Lefts, Is.EqualTo(right
                    ? new[] { 260f, 135f, 10f, 260f, 135f, 10f, 10f }
                    : new[] { 10f, 135f, 260f, 10f, 135f, 260f, 10f }), ((ScalingTableLayoutGroup.Corner)corner).ToString());
                Assert.That(table.CellWidths, Is.EqualTo(new[] { 120f, 120f, 120f, 120f, 120f, 120f, 370f }));
                Assert.That(table.Cells.All(c => c.anchorMin == Vector2.up && c.anchorMax == Vector2.up), Is.True);
            }
        }

        // The corner tests are bit tests: right to left when corner % 2 == 1 (and edx,
        // 0x80000001; cmp 1 at 0x1B3DCBF), so 5 counts and -1 does not; bottom to top
        // only when (corner & ~1) == 2 (0x1B3E074), so only 2 and 3.
        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(5)]
        public void SetLayoutAppliesTheCornerBitTestsToOutOfRangeCorners(int corner)
        {
            using (var table = SevenCells((ScalingTableLayoutGroup.Corner)corner))
            {
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.SetLayoutHorizontal();
                table.Group.CalculateLayoutInputVertical();
                table.Group.SetLayoutVertical();
                Assert.That(table.Lefts, Is.EqualTo(corner == 5
                    ? new[] { 260f, 135f, 10f, 260f, 135f, 10f, 10f }
                    : new[] { 10f, 135f, 260f, 10f, 135f, 260f, 10f }), corner.ToString());
                Assert.That(table.Tops, Is.EqualTo(new[] { 10f, 10f, 10f, 102f, 102f, 102f, 194f }), corner.ToString());
            }
        }

        // 0x1B3DDDE: rows wider than the rect are offset by GetStartOffset with the
        // child alignment, here UpperCenter: (100 - 128) * 0.5 = -14.
        [TestCase(0)]
        [TestCase(1)]
        public void SetLayoutHorizontalAlignsOverflowingRowsByTheChildAlignment(int corner)
        {
            var lefts = corner == 1 ? new[] { 82f, 50f, 18f, -14f } : new[] { -14f, 18f, 50f, 82f };
            using (var table = new Table(100f, 100f, 8))
            {
                table.Group.MaximumColumns = 4;
                table.Group.childAlignment = TextAnchor.UpperCenter;
                table.Group.StartCorner = (ScalingTableLayoutGroup.Corner)corner;
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.SetLayoutHorizontal();
                Assert.That(table.Lefts, Is.EqualTo(lefts.Concat(lefts).ToArray()), ((ScalingTableLayoutGroup.Corner)corner).ToString());
                Assert.That(table.CellWidths.All(w => w == 32f), Is.True);
            }
        }

        // 0x1B3DD0F..0x1B3DD22: the row start is recomputed only when the row's cell
        // count differs from the previous row's; equal counts reuse the previous start
        // even if the row widths differ. The width table is replaced after the input
        // pass to make the two rows' widths differ.
        [Test]
        public void SetLayoutHorizontalRealignsOnlyWhenTheRowCellCountChanges()
        {
            using (var table = new Table(200f, 100f, 4))
            {
                table.Group.MaximumColumns = 2;
                table.Group.childAlignment = TextAnchor.UpperCenter;
                table.Group.CalculateLayoutInputHorizontal();
                Assert.That(table.Widths, Is.EqualTo(new float[,] { { 100f, 100f }, { 100f, 100f } }));
                table.Widths = new float[,] { { 40f, 40f }, { 10f, 10f } };
                table.Group.SetLayoutHorizontal();
                Assert.That(table.Lefts, Is.EqualTo(new[] { 60f, 100f, 60f, 70f }));
                Assert.That(table.CellWidths, Is.EqualTo(new[] { 40f, 40f, 10f, 10f }));
            }
        }

        // 0x1B3DC9B / 0x1B3DCAE: columns and rows come from the width table's
        // GetLength(1) and GetLength(0), not MaximumColumns. A 1 x 4 table replacing
        // the 2 x 2 input places all four cells in one row from GetStartOffset 0.
        [Test]
        public void SetLayoutHorizontalTakesTheTableShapeFromTheWidthTable()
        {
            using (var table = new Table(200f, 100f, 4))
            {
                table.Group.MaximumColumns = 2;
                table.Group.CalculateLayoutInputHorizontal();
                table.Widths = new float[,] { { 10f, 20f, 30f, 40f } };
                table.Group.SetLayoutHorizontal();
                Assert.That(table.Lefts, Is.EqualTo(new[] { 0f, 10f, 30f, 60f }));
                Assert.That(table.CellWidths, Is.EqualTo(new[] { 10f, 20f, 30f, 40f }));
            }
        }

        // SetLayoutVertical 0x1B3DF30: total = sum(row heights) + spacing * (rows - 1);
        // lower corners ((m_startCorner & ~1) == 2) start at GetStartOffset(1, total) +
        // total and place each row at pos - height then step by -spacing; upper corners
        // walk down from GetStartOffset(1, total). Each row takes the next GetLength(1)
        // children, stopping at the child count. The row heights are then released
        // (0x1B3E184) while the column widths stay.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SetLayoutVerticalPlacesRowsFromTheStartCornerAndReleasesTheRowHeights(int corner)
        {
            using (var table = SevenCells((ScalingTableLayoutGroup.Corner)corner))
            {
                table.Group.CalculateLayoutInputHorizontal();
                var widths = table.Widths;
                table.Group.CalculateLayoutInputVertical();
                table.Group.SetLayoutVertical();
                bool lower = corner >= 2;
                Assert.That(table.Tops, Is.EqualTo(lower
                    ? new[] { 194f, 194f, 194f, 102f, 102f, 102f, 10f }
                    : new[] { 10f, 10f, 10f, 102f, 102f, 102f, 194f }), ((ScalingTableLayoutGroup.Corner)corner).ToString());
                Assert.That(table.CellHeights.All(h => h == 86f), Is.True);
                Assert.That(table.Heights, Is.Null);
                Assert.That(table.Widths, Is.SameAs(widths));
            }
        }

        // 0x1B3E067: rows taller than the rect are offset by GetStartOffset with the
        // child alignment, here MiddleLeft: (100 - 128) * 0.5 = -14.
        [Test]
        public void SetLayoutVerticalAlignsOverflowingRowsByTheChildAlignment()
        {
            using (var table = new Table(100f, 100f, 8))
            {
                table.Group.MaximumColumns = 2;
                table.Group.childAlignment = TextAnchor.MiddleLeft;
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.CalculateLayoutInputVertical();
                table.Group.SetLayoutVertical();
                Assert.That(table.Tops, Is.EqualTo(new[] { -14f, -14f, 18f, 18f, 50f, 50f, 82f, 82f }));
            }
        }

        // 0x1B3E110..0x1B3E118: a row stops only when the child index equals the
        // child count, so a later row starting past the count indexes the child list
        // out of range. Earlier rows are already placed and the row heights are not
        // released. The tables are replaced after the input pass to add a third row.
        [Test]
        public void SetLayoutVerticalStopsARowOnlyAtTheExactChildCount()
        {
            using (var table = new Table(200f, 300f, 3))
            {
                table.Group.MaximumColumns = 2;
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.CalculateLayoutInputVertical();
                var heights = new[] { 10f, 10f, 10f };
                table.Widths = new float[3, 2];
                table.Heights = heights;
                Assert.Throws<ArgumentOutOfRangeException>(() => table.Group.SetLayoutVertical());
                Assert.That(table.Tops, Is.EqualTo(new[] { 0f, 0f, 10f }));
                Assert.That(table.Heights, Is.SameAs(heights));
            }
        }

        // 0x1B3DF84 / 0x1B3DF97: rows and columns come from the width table, not
        // MaximumColumns or the row heights' length. One row of four (Heights {50, 50}
        // from the input) totals 50 with no spacing term, and MiddleLeft offsets it by
        // GetStartOffset(1, 50) = (100 - 50) * 0.5 = 25.
        [Test]
        public void SetLayoutVerticalTakesTheTableShapeFromTheWidthTable()
        {
            using (var table = new Table(200f, 100f, 4))
            {
                table.Group.MaximumColumns = 2;
                table.Group.childAlignment = TextAnchor.MiddleLeft;
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.CalculateLayoutInputVertical();
                Assert.That(table.Heights, Is.EqualTo(new[] { 50f, 50f }));
                table.Widths = new float[1, 4];
                table.Group.SetLayoutVertical();
                Assert.That(table.Tops, Is.EqualTo(new[] { 25f, 25f, 25f, 25f }));
                Assert.That(table.CellHeights, Is.EqualTo(new[] { 50f, 50f, 50f, 50f }));
                Assert.That(table.Heights, Is.Null);
            }
        }

        // With no layout children every pass returns before touching its state:
        // 0x1B3D2BC skips SetLayoutInputForAxis and the width table, 0x1B3DA21 the
        // height table, and 0x1B3DC84 / 0x1B3DF74 return before reading either table
        // (so the row heights are not released).
        [Test]
        public void NoLayoutChildrenLeavesTheCachedInputsAndTablesUntouched()
        {
            using (var table = new Table(100f, 100f, 3))
            {
                table.Group.MaximumColumns = 2;
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.CalculateLayoutInputVertical();
                var widths = table.Widths;
                var heights = table.Heights;
                Assert.That(widths, Is.EqualTo(new float[,] { { 50f, 50f }, { 100f, 0f } }));
                Assert.That(heights, Is.EqualTo(new[] { 50f, 50f }));

                foreach (var cell in table.Cells) cell.gameObject.SetActive(false);
                table.Rect.sizeDelta = new Vector2(300f, 300f);
                table.Group.CalculateLayoutInputHorizontal();
                table.Group.SetLayoutHorizontal();
                table.Group.CalculateLayoutInputVertical();
                table.Group.SetLayoutVertical();

                Assert.That(table.Group.preferredWidth, Is.EqualTo(100f));
                Assert.That(table.Group.preferredHeight, Is.EqualTo(100f));
                Assert.That(table.Widths, Is.SameAs(widths));
                Assert.That(table.Heights, Is.SameAs(heights));
            }
        }

        // OnEnable 0x1B3E1B0: base.OnEnable (which marks the layout for rebuild), then
        // a tail call to Canvas.ForceUpdateCanvases, so the queued rebuild has run,
        // children placed and the row heights released, before enabling returns. The
        // global update raises Canvas.willRenderCanvases once, which a rebuild of only
        // this group (LayoutRebuilder.ForceRebuildLayoutImmediate) would not.
        [Test]
        public void OnEnableRunsTheQueuedLayoutRebuildImmediately()
        {
            using (var table = new Table(300f, 80f, 2))
            {
                table.Group.MaximumColumns = 2;
                Assert.That(table.CellWidths, Is.EqualTo(new[] { 100f, 100f }));
                Assert.That(table.Widths, Is.Null);

                int fired = 0;
                Canvas.WillRenderCanvases handler = () => fired++;
                Canvas.willRenderCanvases += handler;
                try
                {
                    table.Group.enabled = true;
                }
                finally
                {
                    Canvas.willRenderCanvases -= handler;
                }

                Assert.That(fired, Is.EqualTo(1));
                Assert.That(table.CellWidths, Is.EqualTo(new[] { 150f, 150f }));
                Assert.That(table.CellHeights, Is.EqualTo(new[] { 80f, 80f }));
                Assert.That(table.Lefts, Is.EqualTo(new[] { 0f, 150f }));
                Assert.That(table.Tops, Is.EqualTo(new[] { 0f, 0f }));
                Assert.That(table.Widths, Is.EqualTo(new float[,] { { 150f, 150f } }));
                Assert.That(table.Heights, Is.Null);
            }
        }
    }
}
