// Original HLUnityUI.Runtime UnityEngine.UI.Extensions.TableLayoutGroup 0200001a
// (MethodDefs 0x060000a8..bb) and its nested Corner enum 0200001b, translated from
// the x86-64 bodies. Hardlight's revision of the Unity UI Extensions table layout:
// column spacing is derived each horizontal pass (uniform or minimum), a row
// holding fewer cells than the column count is aligned on its own width, and
// the empty-widths fallback is two 300 unit columns.
using Unity.IL2CPP.CompilerServices;

namespace UnityEngine.UI.Extensions
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [AddComponentMenu("Layout/Extensions/Table Layout Group")]
    public class TableLayoutGroup : LayoutGroup
    {
        public enum Corner
        {
            UpperLeft = 0,
            UpperRight = 1,
            LowerLeft = 2,
            LowerRight = 3
        }

        [Tooltip("The starting corner from which the cells should be arranged.")]
        [SerializeField]
        protected Corner m_startCorner;

        [SerializeField]
        [Tooltip("The widths of all the columns in the table.")]
        protected float[] m_columnWidths = new float[] { 96f };

        [Tooltip("The minimum height for any row in the table.")]
        [SerializeField]
        protected float m_minimumRowHeight = 32f;

        [Tooltip("Expand rows to fit the cell with the highest preferred height.")]
        [SerializeField]
        protected bool m_flexibleRowHeight = true;

        [SerializeField]
        [Tooltip("The minimum horizontal spacing between each cell in the table.")]
        protected float m_minColumnSpacing;

        [SerializeField]
        [Tooltip("Space columns to uniformly fit. Otherwise MinColumnSpacing value is used.")]
        protected bool m_uniformColumnSpacing;

        [Tooltip("The vertical spacing between each row in the table.")]
        [SerializeField]
        protected float m_rowSpacing;

        // Written by CalculateLayoutInputHorizontal, read by SetLayoutHorizontal.
        protected float m_columnSpacing;

        // Filled by CalculateLayoutInputVertical and released by SetLayoutVertical.
        private float[] m_preferredRowHeights;

        public Corner StartCorner
        {
            get { return m_startCorner; }
            set { SetProperty(ref m_startCorner, value); }
        }

        public float[] ColumnWidths
        {
            get { return m_columnWidths; }
            set { SetProperty(ref m_columnWidths, value); }
        }

        public float MinimumRowHeight
        {
            get { return m_minimumRowHeight; }
            set { SetProperty(ref m_minimumRowHeight, value); }
        }

        public bool FlexibleRowHeight
        {
            get { return m_flexibleRowHeight; }
            set { SetProperty(ref m_flexibleRowHeight, value); }
        }

        public float MinColumnSpacing
        {
            get { return m_minColumnSpacing; }
            set { SetProperty(ref m_minColumnSpacing, value); }
        }

        public bool UniformColumnSpacing
        {
            get { return m_uniformColumnSpacing; }
            set { SetProperty(ref m_uniformColumnSpacing, value); }
        }

        public float RowSpacing
        {
            get { return m_rowSpacing; }
            set { SetProperty(ref m_rowSpacing, value); }
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            float horizontalSize = padding.horizontal;

            // Only the columns that actually hold a child contribute their width.
            int actualCellCount = Mathf.Min(rectChildren.Count, m_columnWidths.Length);

            for (int i = 0; i < actualCellCount; i++)
                horizontalSize += m_columnWidths[i];

            if (m_uniformColumnSpacing)
            {
                // 0x060000b6 (0x1b3e591..5c5): spacing = max(free width / declared columns,
                // minimum); the input grows by the free width less one spacing, so it is
                // the rect width less one spacing.
                float freeWidth = rectTransform.rect.width - horizontalSize;
                m_columnSpacing = Mathf.Max(freeWidth / m_columnWidths.Length, m_minColumnSpacing);
                horizontalSize += freeWidth - m_columnSpacing;
            }
            else
            {
                // One spacing per declared column, including the last.
                m_columnSpacing = m_minColumnSpacing;
                horizontalSize += m_columnWidths.Length * m_columnSpacing;
            }

            SetLayoutInputForAxis(horizontalSize, horizontalSize, 0, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            int columnCount = m_columnWidths.Length;
            int rowCount = Mathf.CeilToInt(rectChildren.Count / (float)columnCount);

            m_preferredRowHeights = new float[rowCount];

            float totalMinHeight = padding.vertical;
            float totalPreferredHeight = padding.vertical;

            if (rowCount > 1)
            {
                float rowSpacingTotal = (rowCount - 1) * m_rowSpacing;
                totalMinHeight += rowSpacingTotal;
                totalPreferredHeight += rowSpacingTotal;
            }

            if (m_flexibleRowHeight)
            {
                // Each row takes the largest minimum and preferred height of its cells.
                for (int i = 0; i < rowCount; i++)
                {
                    float rowMinHeight = m_minimumRowHeight;
                    float rowPreferredHeight = m_minimumRowHeight;

                    for (int j = 0; j < columnCount; j++)
                    {
                        int childIndex = (i * columnCount) + j;

                        // A short last row ends at the last child.
                        if (childIndex == rectChildren.Count)
                            break;

                        // The preferred height is read before the minimum.
                        rowPreferredHeight = Mathf.Max(LayoutUtility.GetPreferredHeight(rectChildren[childIndex]), rowPreferredHeight);
                        rowMinHeight = Mathf.Max(LayoutUtility.GetMinHeight(rectChildren[childIndex]), rowMinHeight);
                    }

                    totalMinHeight += rowMinHeight;
                    totalPreferredHeight += rowPreferredHeight;
                    m_preferredRowHeights[i] = rowPreferredHeight;
                }
            }
            else
            {
                // Fixed rows: every row is the minimum row height.
                for (int i = 0; i < rowCount; i++)
                    m_preferredRowHeights[i] = m_minimumRowHeight;

                totalMinHeight += rowCount * m_minimumRowHeight;
                totalPreferredHeight = totalMinHeight;
            }

            totalPreferredHeight = Mathf.Max(totalMinHeight, totalPreferredHeight);
            SetLayoutInputForAxis(totalMinHeight, totalPreferredHeight, 1, 1);
        }

        public override void SetLayoutHorizontal()
        {
            // 0x060000b8 (0x1b3ea00): an empty width array becomes two 300 unit columns.
            if (m_columnWidths.Length == 0)
                m_columnWidths = new float[] { 300f, 300f };

            int columnCount = m_columnWidths.Length;
            int cornerX = (int)m_startCorner % 2;
            int rowCount = Mathf.CeilToInt(rectChildren.Count / (float)columnCount);

            int remainingCells = rectChildren.Count;
            int childIndex = 0;
            int previousCellsInRow = -1;
            float startOffset = 0;

            for (int row = 0; row < rowCount; row++)
            {
                int cellsInRow = Mathf.Min(remainingCells, columnCount);

                // The start offset is aligned to this row's own cells. It is computed for
                // the first row (previousCellsInRow starts at -1, 0x1b3eb29) and recomputed
                // only when the cell count changes (0x1b3eb77), normally a short last row.
                if (cellsInRow != previousCellsInRow)
                {
                    float rowWidth = 0;

                    for (int i = 0; i < cellsInRow; i++)
                        rowWidth += m_columnWidths[i];

                    rowWidth += (cellsInRow - 1) * m_columnSpacing;

                    startOffset = GetStartOffset(0, rowWidth);

                    if (cornerX == 1)
                        startOffset += rowWidth;

                    previousCellsInRow = cellsInRow;
                }

                float positionX = startOffset;

                for (int cell = 0; cell < cellsInRow; cell++, childIndex++)
                {
                    int currentColumnIndex = childIndex % columnCount;

                    // Each row starts again from the start offset.
                    if (currentColumnIndex == 0)
                        positionX = startOffset;

                    if (cornerX == 1)
                        positionX -= m_columnWidths[currentColumnIndex];

                    SetChildAlongAxis(rectChildren[childIndex], 0, positionX, m_columnWidths[currentColumnIndex]);

                    if (cornerX == 1)
                        positionX -= m_columnSpacing;
                    else
                        positionX += m_columnWidths[currentColumnIndex] + m_columnSpacing;
                }

                remainingCells -= cellsInRow;
            }
        }

        public override void SetLayoutVertical()
        {
            int columnCount = m_columnWidths.Length;
            int rowCount = m_preferredRowHeights.Length;

            int cornerY = (int)m_startCorner / 2;

            float tableHeight = 0;

            for (int i = 0; i < rowCount; i++)
                tableHeight += m_preferredRowHeights[i];

            if (rowCount > 1)
                tableHeight += (rowCount - 1) * m_rowSpacing;

            float startOffset = GetStartOffset(1, tableHeight);

            if (cornerY == 1)
                startOffset += tableHeight;

            float positionY = startOffset;

            for (int i = 0; i < rowCount; i++)
            {
                if (cornerY == 1)
                    positionY -= m_preferredRowHeights[i];

                for (int j = 0; j < columnCount; j++)
                {
                    int childIndex = (i * columnCount) + j;

                    if (childIndex == rectChildren.Count)
                        break;

                    SetChildAlongAxis(rectChildren[childIndex], 1, positionY, m_preferredRowHeights[i]);
                }

                if (cornerY == 1)
                    positionY -= m_rowSpacing;
                else
                    positionY += m_preferredRowHeights[i] + m_rowSpacing;
            }

            // The heights belong to one vertical pass; the next input pass reallocates them.
            m_preferredRowHeights = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas.ForceUpdateCanvases();
        }
    }
}
