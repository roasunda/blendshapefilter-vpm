using System.Collections.Generic;

namespace BlendShapeFilter
{
    /// <summary>
    /// Sort modes offered by the window. Sorting only changes the display order;
    /// the Mesh internal index of each BlendShape is never touched.
    /// </summary>
    public enum BlendShapeSortMode
    {
        OriginalIndex = 0,
        NameAscending = 1,
        NameDescending = 2,
        ValueAscending = 3,
        ValueDescending = 4,
    }

    /// <summary>
    /// Sorting logic for the displayed BlendShape list.
    /// </summary>
    public static class BlendShapeSorter
    {
        public static readonly string[] SortModeLabels =
        {
            "Original Index",
            "Name Ascending",
            "Name Descending",
            "Value Ascending",
            "Value Descending",
        };

        private static readonly System.Comparison<BlendShapeData> OriginalIndexComparison =
            (a, b) => a.Index.CompareTo(b.Index);

        private static readonly System.Comparison<BlendShapeData> NameAscendingComparison =
            (a, b) =>
            {
                int result = string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
                return result != 0 ? result : a.Index.CompareTo(b.Index);
            };

        private static readonly System.Comparison<BlendShapeData> NameDescendingComparison =
            (a, b) =>
            {
                int result = string.Compare(b.Name, a.Name, System.StringComparison.OrdinalIgnoreCase);
                return result != 0 ? result : a.Index.CompareTo(b.Index);
            };

        private static readonly System.Comparison<BlendShapeData> ValueAscendingComparison =
            (a, b) =>
            {
                int result = a.Weight.CompareTo(b.Weight);
                return result != 0 ? result : a.Index.CompareTo(b.Index);
            };

        private static readonly System.Comparison<BlendShapeData> ValueDescendingComparison =
            (a, b) =>
            {
                int result = b.Weight.CompareTo(a.Weight);
                return result != 0 ? result : a.Index.CompareTo(b.Index);
            };

        /// <summary>
        /// Sorts the list in place. The index tie-breaker keeps the order stable
        /// between BlendShapes that compare equal.
        /// </summary>
        public static void Sort(List<BlendShapeData> shapes, BlendShapeSortMode mode)
        {
            if (shapes == null || shapes.Count < 2)
            {
                return;
            }

            switch (mode)
            {
                case BlendShapeSortMode.NameAscending:
                    shapes.Sort(NameAscendingComparison);
                    break;
                case BlendShapeSortMode.NameDescending:
                    shapes.Sort(NameDescendingComparison);
                    break;
                case BlendShapeSortMode.ValueAscending:
                    shapes.Sort(ValueAscendingComparison);
                    break;
                case BlendShapeSortMode.ValueDescending:
                    shapes.Sort(ValueDescendingComparison);
                    break;
                default:
                    shapes.Sort(OriginalIndexComparison);
                    break;
            }
        }
    }
}
