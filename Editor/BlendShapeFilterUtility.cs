using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlendShapeFilter
{
    /// <summary>
    /// Reads BlendShape information from a SkinnedMeshRenderer and writes weights back.
    /// The Mesh asset is only ever read through sharedMesh: this class never adds, removes,
    /// renames or reorders BlendShapes, and never saves a Mesh.
    /// </summary>
    public static class BlendShapeFilterUtility
    {
        public const float MinWeight = 0f;
        public const float MaxWeight = 100f;

        /// <summary>Tolerance that keeps floating point noise from counting as a real value.</summary>
        public const float WeightEpsilon = 0.001f;

        public static Mesh GetMesh(SkinnedMeshRenderer renderer)
        {
            return renderer != null ? renderer.sharedMesh : null;
        }

        /// <summary>
        /// Rebuilds the list from the renderer's sharedMesh. Every entry starts with its
        /// snapshot equal to the current weight; callers that must keep an older baseline
        /// restore it afterwards.
        /// Returns false when there is no renderer, no Mesh, or no BlendShape.
        /// </summary>
        public static bool CollectBlendShapes(SkinnedMeshRenderer renderer, List<BlendShapeData> results)
        {
            results.Clear();

            Mesh mesh = GetMesh(renderer);
            if (mesh == null)
            {
                return false;
            }

            int count = mesh.blendShapeCount;
            if (count <= 0)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                results.Add(new BlendShapeData(i, mesh.GetBlendShapeName(i), renderer.GetBlendShapeWeight(i)));
            }

            return true;
        }

        /// <summary>
        /// Refreshes only the cached weights. Index, name and snapshot stay untouched.
        /// </summary>
        public static void RefreshWeights(SkinnedMeshRenderer renderer, List<BlendShapeData> shapes)
        {
            if (renderer == null || shapes == null)
            {
                return;
            }

            Mesh mesh = renderer.sharedMesh;
            int count = mesh != null ? mesh.blendShapeCount : 0;

            for (int i = 0; i < shapes.Count; i++)
            {
                BlendShapeData data = shapes[i];
                if (data.Index >= 0 && data.Index < count)
                {
                    data.Weight = renderer.GetBlendShapeWeight(data.Index);
                }
            }
        }

        /// <summary>
        /// Applies a weight with Undo support. Only the Renderer is recorded, never the Mesh,
        /// and the change is registered as a Prefab override when the Renderer is a Prefab instance.
        /// </summary>
        public static void SetWeight(SkinnedMeshRenderer renderer, int index, float weight, string undoName)
        {
            if (renderer == null)
            {
                return;
            }

            Undo.RecordObject(renderer, undoName);
            renderer.SetBlendShapeWeight(index, Mathf.Clamp(weight, MinWeight, MaxWeight));
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        /// <summary>Non-Zero filter test.</summary>
        public static bool IsNonZero(float weight)
        {
            return Mathf.Abs(weight) > WeightEpsilon;
        }

        /// <summary>
        /// Case insensitive partial match. An empty search matches everything.
        /// </summary>
        public static bool MatchesSearch(string name, string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return true;
            }

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Finds a SkinnedMeshRenderer on the active selection: the GameObject itself first,
        /// then its children recursively. The first one found is used.
        /// </summary>
        public static SkinnedMeshRenderer FindRendererFromSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return null;
            }

            SkinnedMeshRenderer own = selected.GetComponent<SkinnedMeshRenderer>();
            if (own != null)
            {
                return own;
            }

            return selected.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }
    }
}
