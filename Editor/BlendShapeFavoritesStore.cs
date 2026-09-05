using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BlendShapeFilter
{
    /// <summary>
    /// Loads, saves and queries the favorite BlendShapes of one Mesh.
    /// Favorites are keyed by Mesh identity plus BlendShape index, so two BlendShapes
    /// that share a name inside the same Mesh stay distinguishable. The index is only
    /// used as part of that key: the Mesh internal index itself is never modified.
    /// Persisted through EditorPrefs so favorites survive an Editor restart.
    /// </summary>
    public class BlendShapeFavoritesStore
    {
        private const string KeyPrefix = "BlendShapeFilter.Favorites.";
        private const char Separator = ',';

        private readonly HashSet<int> _favorites = new HashSet<int>();
        private readonly StringBuilder _builder = new StringBuilder();
        private string _meshKey = string.Empty;

        /// <summary>
        /// Builds a Mesh identity that stays stable across Editor sessions.
        /// Imported Meshes use their asset GUID and local file id; a Mesh that is not an
        /// asset falls back to its name and BlendShape count.
        /// </summary>
        public static string GetMeshKey(Mesh mesh)
        {
            if (mesh == null)
            {
                return string.Empty;
            }

            string guid;
            long localId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out guid, out localId)
                && !string.IsNullOrEmpty(guid))
            {
                return guid + ":" + localId;
            }

            return "name:" + mesh.name + ":" + mesh.blendShapeCount;
        }

        /// <summary>
        /// Reads the favorites of the given Mesh into memory. Called when the target
        /// Renderer or its Mesh changes, not every frame.
        /// </summary>
        public void Load(Mesh mesh)
        {
            _favorites.Clear();
            _meshKey = GetMeshKey(mesh);

            if (string.IsNullOrEmpty(_meshKey))
            {
                return;
            }

            string stored = EditorPrefs.GetString(KeyPrefix + _meshKey, string.Empty);
            if (string.IsNullOrEmpty(stored))
            {
                return;
            }

            string[] parts = stored.Split(Separator);
            for (int i = 0; i < parts.Length; i++)
            {
                int index;
                if (int.TryParse(parts[i], out index))
                {
                    _favorites.Add(index);
                }
            }
        }

        public bool IsFavorite(int index)
        {
            return _favorites.Contains(index);
        }

        public void Add(int index)
        {
            if (_favorites.Add(index))
            {
                Save();
            }
        }

        public void Remove(int index)
        {
            if (_favorites.Remove(index))
            {
                Save();
            }
        }

        public void Toggle(int index)
        {
            if (_favorites.Contains(index))
            {
                Remove(index);
            }
            else
            {
                Add(index);
            }
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_meshKey))
            {
                return;
            }

            string prefsKey = KeyPrefix + _meshKey;

            if (_favorites.Count == 0)
            {
                EditorPrefs.DeleteKey(prefsKey);
                return;
            }

            _builder.Length = 0;
            foreach (int index in _favorites)
            {
                if (_builder.Length > 0)
                {
                    _builder.Append(Separator);
                }

                _builder.Append(index);
            }

            EditorPrefs.SetString(prefsKey, _builder.ToString());
        }
    }
}
