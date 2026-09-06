namespace BlendShapeFilter
{
    /// <summary>
    /// Display data for a single BlendShape.
    /// Index and Name always mirror the Mesh and are never written back to it.
    /// </summary>
    public class BlendShapeData
    {
        /// <summary>Mesh internal index. Never changed by this tool.</summary>
        public int Index;

        /// <summary>Mesh internal BlendShape name. Never changed by this tool.</summary>
        public string Name;

        /// <summary>Current weight on the SkinnedMeshRenderer.</summary>
        public float Weight;

        /// <summary>Face part guessed from the name. Display grouping only.</summary>
        public BlendShapeCategory Category;

        /// <summary>Finer part inside Eye or Mouth. None for parts that are not split.</summary>
        public BlendShapeSubCategory SubCategory;

        public BlendShapeData(int index, string name, float weight)
        {
            Index = index;
            Name = name;
            Weight = weight;
            Category = BlendShapeCategoryClassifier.Classify(name);
            SubCategory = BlendShapeCategoryClassifier.ClassifySub(Category, name);
        }
    }
}
