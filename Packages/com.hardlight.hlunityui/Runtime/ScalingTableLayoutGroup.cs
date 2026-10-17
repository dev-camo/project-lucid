// Original HLUnityUI.Runtime UnityEngine.UI.Extensions.ScalingTableLayoutGroup
// 02000017 (TypeDefIndex 11849), MethodDefs 0x06000092..0x060000a7, with the
// nested Corner 02000018 and RowAddMode 02000019. Translated from the x86-64
// bodies at 0x1B3CF60..0x1B3E1F7. No shipped asset references this script, so it
// has a fresh GUID and no script binding.
using System;
using Unity.IL2CPP.CompilerServices;

namespace UnityEngine.UI.Extensions
{
    [AddComponentMenu("Layout/Extensions/Scaling Table Layout Group")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ScalingTableLayoutGroup : LayoutGroup
    {
        public enum Corner
        {
            UpperLeft = 0,
            UpperRight = 1,
            LowerLeft = 2,
            LowerRight = 3
        }

        public enum RowAddMode
        {
            Fill = 0,
            Overflow = 1
        }

        [SerializeField]
        [Tooltip("The starting corner from which the cells should be arranged.")]
        protected Corner m_startCorner;

        [Range(1, 100)]
        [Tooltip("The widths of all the columns in the table. Also denotes the maximum number of columns.")]
        [SerializeField]
        protected int m_maximumColumns = 1;

        [Range(1, 100)]
        [Tooltip("the Maximum number of rows expected in this table, can over flow if needed.")]
        [SerializeField]
        protected int m_maximumExpectedRows = 1;

        [SerializeField]
        [Tooltip("The child adding mode used to determine how we react to new rows")]
        protected RowAddMode m_RowAddMode;

        [SerializeField]
        [Tooltip("The minimum height for any row in the table.")]
        protected float m_minimumRowHeight = 32f;

        [SerializeField]
        [Tooltip("The minimum width for any column in the table.")]
        protected float m_minimumColumnWidth = 32f;

        [Tooltip("The minimum horizontal spacing between each cell in the table.")]
        [SerializeField]
        protected float m_columnSpacing;

        [SerializeField]
        [Tooltip("The minimum vertical spacing between each row in the table.")]
        protected float m_rowSpacing;

        private float[] m_preferredRowHeights;
        private float[,] m_preferredColumnWidths;

        public Corner StartCorner
        {
            get { return m_startCorner; }
            set { SetProperty(ref m_startCorner, value); }
        }

        public int MaximumColumns
        {
            get { return m_maximumColumns; }
            set { SetProperty(ref m_maximumColumns, value); }
        }

        public int MaximumExpectedRows
        {
            get { return m_maximumExpectedRows; }
            set { SetProperty(ref m_maximumExpectedRows, value); }
        }

        public RowAddMode NewRowAddMode
        {
            get { return m_RowAddMode; }
            set { SetProperty(ref m_RowAddMode, value); }
        }

        public float MinimumRowHeight
        {
            get { return m_minimumRowHeight; }
            set { SetProperty(ref m_minimumRowHeight, value); }
        }

        public float MinimumColumnWidth
        {
            get { return m_minimumColumnWidth; }
            set { SetProperty(ref m_minimumColumnWidth, value); }
        }

        public float ColumnSpacing
        {
            get { return m_columnSpacing; }
            set { SetProperty(ref m_columnSpacing, value); }
        }

        public float RowSpacing
        {
            get { return m_rowSpacing; }
            set { SetProperty(ref m_rowSpacing, value); }
        }

        // 0x060000a2: every full row splits the available width evenly; the last row
        // splits the whole width over its own cells and leaves its unused columns 0.
        // The reported size uses the available width before the minimum clamp.
        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            if (rectChildren.Count == 0)
                return;

            Rect rect = rectTransform.rect;
            float totalPreferredWidth = padding.horizontal;

            int columns = Mathf.Min(rectChildren.Count, m_maximumColumns);
            // Computed before the remainder: its inlined CeilToInt System.Math class
            // init runs at 0x1B3D2FD, ahead of the idiv (0x1B3D329) and the guard.
            int rows = Mathf.CeilToInt((float)rectChildren.Count / m_maximumColumns);
            int lastRowCells = rectChildren.Count % columns;
            if (lastRowCells == 0)
                lastRowCells = columns;
            // Shipped guard (je at 0x1B3D338) kept as dead code: the % above already
            // throws DivideByZeroException when columns is 0 (idiv at 0x1B3D329).
            if (columns == 0)
                return;

            m_preferredColumnWidths = new float[rows, columns];

            // The spacing is read before each padding.horizontal call (0x1B3D3B9, 0x1B3D3E6).
            float availableWidth = rect.width - (m_columnSpacing * (columns - 1) + padding.horizontal);
            float lastRowCellWidth = (rect.width - (m_columnSpacing * (lastRowCells - 1) + padding.horizontal)) / lastRowCells;

            totalPreferredWidth += m_columnSpacing * (columns - 1);
            totalPreferredWidth += availableWidth;

            float cellWidth = Mathf.Max(availableWidth / columns, m_minimumColumnWidth);
            for (int row = 0; row < rows; row++)
            {
                bool lastRow = row == rows - 1;
                if (lastRow)
                    cellWidth = Mathf.Max(lastRowCellWidth, m_minimumColumnWidth);

                for (int column = 0; column < columns; column++)
                {
                    if (lastRow && column >= lastRowCells)
                        m_preferredColumnWidths[row, column] = 0f;
                    else
                        m_preferredColumnWidths[row, column] = cellWidth;
                }
            }

            SetLayoutInputForAxis(totalPreferredWidth, totalPreferredWidth, 0f, 0);
        }

        // 0x060000a3: Fill divides the height by the table's rows, Overflow by at most
        // MaximumExpectedRows; any other mode divides by zero rows.
        public override void CalculateLayoutInputVertical()
        {
            if (rectChildren.Count == 0)
                return;

            int rows = Mathf.CeilToInt((float)rectChildren.Count / m_maximumColumns);
            m_preferredRowHeights = new float[rows];
            float totalPreferredHeight = padding.vertical;

            int expectedRows;
            switch (m_RowAddMode)
            {
                case RowAddMode.Fill:
                    expectedRows = rows;
                    break;
                case RowAddMode.Overflow:
                    expectedRows = Math.Min(m_maximumExpectedRows, rows);
                    break;
                default:
                    expectedRows = 0;
                    break;
            }

            float rowHeight = Mathf.Max((rectTransform.rect.height - (m_rowSpacing * (expectedRows - 1) + padding.vertical)) / expectedRows, m_minimumRowHeight);

            totalPreferredHeight += expectedRows * rowHeight;
            if (expectedRows > 1)
                totalPreferredHeight += (expectedRows - 1) * m_rowSpacing;

            for (int row = 0; row < rows; row++)
                m_preferredRowHeights[row] = rowHeight;

            SetLayoutInputForAxis(totalPreferredHeight, totalPreferredHeight, 0f, 1);
        }

        // 0x060000a4: odd corners lay each row out right to left. A row's start is
        // only recomputed when its cell count differs from the previous row's.
        public override void SetLayoutHorizontal()
        {
            if (rectChildren.Count == 0)
                return;

            int columns = m_preferredColumnWidths.GetLength(1);
            int rows = m_preferredColumnWidths.GetLength(0);
            bool rightToLeft = (int)m_startCorner % 2 == 1;

            int remainingCells = rectChildren.Count;
            int childIndex = 0;
            int previousRowCells = -1;
            float rowStart = 0f;
            for (int row = 0; row < rows; row++)
            {
                int rowCells = Mathf.Min(remainingCells, columns);
                if (rowCells != previousRowCells)
                {
                    float rowWidth = 0f;
                    for (int column = 0; column < rowCells; column++)
                        rowWidth += m_preferredColumnWidths[row, column];
                    rowWidth += (rowCells - 1) * m_columnSpacing;

                    rowStart = GetStartOffset(0, rowWidth);
                    if (rightToLeft)
                        rowStart += rowWidth;
                }
                previousRowCells = rowCells;

                float x = rowStart;
                for (int cell = 0; cell < rowCells; cell++)
                {
                    int column = childIndex % columns;
                    // Shipped select (0x1B3DE79): column 0 restarts at rowStart.
                    if (column == 0)
                        x = rowStart;

                    if (rightToLeft)
                        x -= m_preferredColumnWidths[row, column];

                    SetChildAlongAxis(rectChildren[childIndex], 0, x, m_preferredColumnWidths[row, column]);

                    if (rightToLeft)
                        x -= m_columnSpacing;
                    else
                        x += m_preferredColumnWidths[row, column] + m_columnSpacing;

                    childIndex++;
                }

                remainingCells -= rowCells;
            }
        }

        // 0x060000a5: lower corners lay the rows out bottom to top. A row stops only
        // at the exact child count (0x1B3E114), and the row heights are released.
        public override void SetLayoutVertical()
        {
            if (rectChildren.Count == 0)
                return;

            int columns = m_preferredColumnWidths.GetLength(1);
            int rows = m_preferredColumnWidths.GetLength(0);

            float totalHeight = 0f;
            for (int row = 0; row < rows; row++)
                totalHeight += m_preferredRowHeights[row];
            if (rows > 1)
                totalHeight += (rows - 1) * m_rowSpacing;

            bool bottomToTop = m_startCorner == Corner.LowerLeft || m_startCorner == Corner.LowerRight;
            float y = GetStartOffset(1, totalHeight);
            if (bottomToTop)
                y += totalHeight;

            for (int row = 0; row < rows; row++)
            {
                if (bottomToTop)
                    y -= m_preferredRowHeights[row];

                for (int column = 0; column < columns; column++)
                {
                    int childIndex = row * columns + column;
                    if (childIndex == rectChildren.Count)
                        break;

                    SetChildAlongAxis(rectChildren[childIndex], 1, y, m_preferredRowHeights[row]);
                }

                if (bottomToTop)
                    y -= m_rowSpacing;
                else
                    y += m_preferredRowHeights[row] + m_rowSpacing;
            }

            m_preferredRowHeights = null;
        }

        // 0x060000a6: lays the table out immediately instead of waiting for the canvas.
        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas.ForceUpdateCanvases();
        }
    }
}
