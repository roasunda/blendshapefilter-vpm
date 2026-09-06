using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlendShapeFilter
{
    /// <summary>
    /// EditorWindow that searches, filters and edits the BlendShape weights of a
    /// SkinnedMeshRenderer. Only renderer weights are written; the Mesh asset is read only.
    /// </summary>
    public class BlendShapeFilterWindow : EditorWindow
    {
        private const string UndoChangeWeight = "Change BlendShape Weight";
        private const string UndoResetWeight = "Reset BlendShape";
        private const string UndoResetVisible = "Reset Visible BlendShapes";

        private const float IndexColumnWidth = 42f;
        private const float NameColumnWidth = 160f;
        private const float ResetColumnWidth = 52f;
        private const float ScrollBarWidth = 16f;
        private const float RowIndent = 14f;
        private const float NonZeroBarWidth = 3f;

        /// <summary>
        /// The target survives a domain reload. Search, filters and sort are session state
        /// and are deliberately not serialized.
        /// </summary>
        [SerializeField] private SkinnedMeshRenderer _renderer;

        private string _search = string.Empty;
        private bool _filterNonZero;

        /// <summary>Face part filter. Null means every part is shown.</summary>
        private BlendShapeCategory? _categoryFilter;

        /// <summary>
        /// Sub part filter inside the selected face part. Null means the whole part.
        /// Cleared whenever the face part changes.
        /// </summary>
        private BlendShapeSubCategory? _subCategoryFilter;

        private BlendShapeSortMode _sortMode = BlendShapeSortMode.OriginalIndex;
        private Vector2 _scrollPosition;

        /// <summary>All BlendShapes of the current Mesh, in original index order.</summary>
        private readonly List<BlendShapeData> _shapes = new List<BlendShapeData>();

        /// <summary>The subset that passed every filter, in the current sort order.</summary>
        private readonly List<BlendShapeData> _visibleShapes = new List<BlendShapeData>();

        /// <summary>How many BlendShapes fall into each face part, indexed by category.</summary>
        private readonly int[] _categoryCounts = new int[BlendShapeCategoryClassifier.CategoryCount];

        /// <summary>How many BlendShapes fall into each sub part, indexed by sub category.</summary>
        private readonly int[] _subCategoryCounts = new int[BlendShapeCategoryClassifier.SubCategoryCount];

        /// <summary>
        /// One entry per drawn line: either a face part header or a BlendShape row.
        /// Headers and rows share a height, which keeps the scroll maths simple enough to
        /// draw only the lines inside the viewport.
        /// </summary>
        private struct DisplayRow
        {
            public bool IsHeader;
            public BlendShapeCategory Category;
            public int GroupCount;
            public BlendShapeData Shape;
        }

        private readonly List<DisplayRow> _displayRows = new List<DisplayRow>();

        /// <summary>Face parts whose group is folded shut.</summary>
        private readonly HashSet<BlendShapeCategory> _collapsedCategories = new HashSet<BlendShapeCategory>();

        private bool _displayRowsDirty = true;
        private GUIStyle _groupHeaderStyle;
        private GUIStyle _selectedChipStyle;
        private GUIStyle _nonZeroNameStyle;

        private Mesh _cachedMesh;
        private bool _visibleListDirty = true;
        private bool _weightsChanged;

        /// <summary>Undo group opened when a weight edit starts, so one drag collapses to one Undo step.</summary>
        private int _weightUndoGroup = -1;

        [MenuItem("Tools/BlendShape Filter")]
        private static void Open()
        {
            BlendShapeFilterWindow window = GetWindow<BlendShapeFilterWindow>();
            window.titleContent = new GUIContent("BlendShape Filter");
            window.Show();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            InitializeTarget();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            CloseWeightUndoGroup();
        }

        private void OnUndoRedoPerformed()
        {
            BlendShapeFilterUtility.RefreshWeights(_renderer, _shapes);
            _visibleListDirty = true;
            Repaint();
        }

        /// <summary>
        /// Runs when the target Renderer or its Mesh changes: re-reads the BlendShapes.
        /// </summary>
        private void InitializeTarget()
        {
            _cachedMesh = BlendShapeFilterUtility.GetMesh(_renderer);
            BlendShapeFilterUtility.CollectBlendShapes(_renderer, _shapes);
            RecountCategories();
            _scrollPosition = Vector2.zero;
            _visibleListDirty = true;
        }

        /// <summary>
        /// Re-reads names and weights. A Mesh swap needs the favorites reloaded too, so that
        /// case falls back to a full initialization.
        /// </summary>
        private void RefreshShapes()
        {
            if (BlendShapeFilterUtility.GetMesh(_renderer) != _cachedMesh)
            {
                InitializeTarget();
                return;
            }

            BlendShapeFilterUtility.CollectBlendShapes(_renderer, _shapes);
            RecountCategories();
            _visibleListDirty = true;
        }

        /// <summary>
        /// Counts the BlendShapes of each face part so the buttons can show their size and
        /// parts the Mesh does not have can be hidden. Recounted when the list is rebuilt,
        /// never per frame.
        /// </summary>
        private void RecountCategories()
        {
            for (int i = 0; i < _categoryCounts.Length; i++)
            {
                _categoryCounts[i] = 0;
            }

            for (int i = 0; i < _subCategoryCounts.Length; i++)
            {
                _subCategoryCounts[i] = 0;
            }

            for (int i = 0; i < _shapes.Count; i++)
            {
                int index = (int)_shapes[i].Category;
                if (index >= 0 && index < _categoryCounts.Length)
                {
                    _categoryCounts[index]++;
                }

                int subIndex = (int)_shapes[i].SubCategory;
                if (subIndex >= 0 && subIndex < _subCategoryCounts.Length)
                {
                    _subCategoryCounts[subIndex]++;
                }
            }
        }

        /// <summary>
        /// Applies Search, face part and Non-Zero as AND conditions, then sorts.
        /// </summary>
        private void RebuildVisibleList()
        {
            BlendShapeFilterUtility.RefreshWeights(_renderer, _shapes);
            _visibleShapes.Clear();

            for (int i = 0; i < _shapes.Count; i++)
            {
                BlendShapeData data = _shapes[i];

                if (!BlendShapeFilterUtility.MatchesSearch(data.Name, _search))
                {
                    continue;
                }

                if (_categoryFilter.HasValue && data.Category != _categoryFilter.Value)
                {
                    continue;
                }

                if (_subCategoryFilter.HasValue && data.SubCategory != _subCategoryFilter.Value)
                {
                    continue;
                }

                if (_filterNonZero && !BlendShapeFilterUtility.IsNonZero(data.Weight))
                {
                    continue;
                }

                _visibleShapes.Add(data);
            }

            BlendShapeSorter.Sort(_visibleShapes, _sortMode);
            RebuildDisplayRows();

            _visibleListDirty = false;
            _weightsChanged = false;
        }

        /// <summary>
        /// Groups the filtered BlendShapes under one header per face part, keeping the sort
        /// order inside each group and skipping the rows of folded groups.
        /// </summary>
        private void RebuildDisplayRows()
        {
            _displayRows.Clear();

            for (int c = 0; c < BlendShapeCategoryClassifier.DisplayOrder.Length; c++)
            {
                BlendShapeCategory category = BlendShapeCategoryClassifier.DisplayOrder[c];

                int groupCount = 0;
                for (int i = 0; i < _visibleShapes.Count; i++)
                {
                    if (_visibleShapes[i].Category == category)
                    {
                        groupCount++;
                    }
                }

                if (groupCount == 0)
                {
                    continue;
                }

                DisplayRow header = new DisplayRow();
                header.IsHeader = true;
                header.Category = category;
                header.GroupCount = groupCount;
                _displayRows.Add(header);

                if (_collapsedCategories.Contains(category))
                {
                    continue;
                }

                for (int i = 0; i < _visibleShapes.Count; i++)
                {
                    BlendShapeData data = _visibleShapes[i];
                    if (data.Category != category)
                    {
                        continue;
                    }

                    DisplayRow row = new DisplayRow();
                    row.Category = category;
                    row.Shape = data;
                    _displayRows.Add(row);
                }
            }

            _displayRowsDirty = false;
        }

        private void OnGUI()
        {
            DetectTargetChange();

            DrawTargetSection();
            EditorGUILayout.Space();
            DrawFilterSection();
            EditorGUILayout.Space();

            if (_renderer == null)
            {
                EditorGUILayout.HelpBox("Select a SkinnedMeshRenderer.", MessageType.Info);
                return;
            }

            if (_renderer.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("The selected SkinnedMeshRenderer has no Mesh.", MessageType.Warning);
                return;
            }

            if (_shapes.Count == 0)
            {
                EditorGUILayout.HelpBox("This Mesh has no BlendShapes.", MessageType.Info);
                return;
            }

            // Search, filter and sort changes rebuild the list immediately so results update
            // as the user types. A weight edit can reorder or hide its own row, so that rebuild
            // waits until the slider is released and the value field is left.
            bool editingValue = GUIUtility.hotControl != 0 || EditorGUIUtility.editingTextField;
            if (_visibleListDirty && GUIUtility.hotControl == 0)
            {
                RebuildVisibleList();
            }
            else if (_weightsChanged && !editingValue)
            {
                RebuildVisibleList();
            }
            else if (_displayRowsDirty)
            {
                // Folding a group only re-groups; it does not re-filter.
                RebuildDisplayRows();
            }

            DrawShapeList();
            DrawFooter();

            // One drag of a slider becomes one Undo step. The slider takes the hot control
            // while the list is drawn, so this has to read the state again rather than reuse
            // the value sampled above.
            if (_weightUndoGroup >= 0 && GUIUtility.hotControl == 0 && !EditorGUIUtility.editingTextField)
            {
                CloseWeightUndoGroup();
            }
        }

        /// <summary>
        /// Rebuilds everything when the Renderer points at a different Mesh than before.
        /// </summary>
        private void DetectTargetChange()
        {
            if (BlendShapeFilterUtility.GetMesh(_renderer) != _cachedMesh)
            {
                InitializeTarget();
            }
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            SkinnedMeshRenderer newRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                _renderer, typeof(SkinnedMeshRenderer), true);
            if (EditorGUI.EndChangeCheck() && newRenderer != _renderer)
            {
                SetTarget(newRenderer);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected", GUILayout.Width(110f)))
                {
                    SkinnedMeshRenderer found = BlendShapeFilterUtility.FindRendererFromSelection();
                    if (found != null)
                    {
                        if (found != _renderer)
                        {
                            SetTarget(found);
                        }
                    }
                    else
                    {
                        Debug.Log("BlendShape Filter: No SkinnedMeshRenderer found on the selected GameObject.");
                    }
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
                {
                    RefreshShapes();
                }

                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>
        /// Switching the target reloads its BlendShapes from scratch.
        /// </summary>
        private void SetTarget(SkinnedMeshRenderer renderer)
        {
            _renderer = renderer;
            InitializeTarget();
        }

        private void DrawFilterSection()
        {
            EditorGUILayout.LabelField("Search", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _search = EditorGUILayout.TextField(_search);
            if (EditorGUI.EndChangeCheck())
            {
                _visibleListDirty = true;
                _scrollPosition.y = 0f;
            }

            EditorGUILayout.Space();

            DrawCategoryButtons();

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _filterNonZero = GUILayout.Toggle(
                    _filterNonZero, "Non-Zero", EditorStyles.miniButton, GUILayout.Width(80f));
                if (EditorGUI.EndChangeCheck())
                {
                    _visibleListDirty = true;
                    _scrollPosition.y = 0f;
                }

                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _sortMode = (BlendShapeSortMode)EditorGUILayout.Popup(
                    "Sort", (int)_sortMode, BlendShapeSorter.SortModeLabels);
                if (EditorGUI.EndChangeCheck())
                {
                    _visibleListDirty = true;
                }

                GUILayout.Space(6f);

                using (new EditorGUI.DisabledScope(_visibleShapes.Count == 0))
                {
                    if (GUILayout.Button("Reset Visible", GUILayout.Width(110f)))
                    {
                        ResetVisible();
                    }
                }
            }
        }

        /// <summary>
        /// One button per face part found in this Mesh, guessed from the BlendShape names.
        /// Pressing a part shows only its BlendShapes; pressing the active button again, or
        /// All, clears the filter. Parts the Mesh has none of are not shown.
        /// Boxed so the filter controls read as one group, separate from Search above and
        /// Non-Zero below.
        /// </summary>
        private void DrawCategoryButtons()
        {
            if (_shapes.Count == 0)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Face Part", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            float available = Mathf.Max(120f, position.width - 28f);
            float used = 0f;

            EditorGUILayout.BeginHorizontal();

            DrawCategoryButton(null, "All", _shapes.Count, available, ref used);

            for (int i = 0; i < BlendShapeCategoryClassifier.DisplayOrder.Length; i++)
            {
                BlendShapeCategory category = BlendShapeCategoryClassifier.DisplayOrder[i];
                int count = _categoryCounts[(int)category];
                if (count == 0)
                {
                    continue;
                }

                DrawCategoryButton(
                    category, BlendShapeCategoryClassifier.GetLabel(category), count, available, ref used);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawSubCategoryButtons();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Second row of buttons, drilling into the selected face part. It is only drawn when
        /// that part actually splits into more than one sub part on this Mesh, so a Mesh
        /// without, say, eyelash shapes never shows an empty Lash button, and a part whose
        /// shapes all land in one sub part shows no second row at all.
        /// The leading arrow ties the row visually to the face part button selected above.
        /// </summary>
        private void DrawSubCategoryButtons()
        {
            if (!_categoryFilter.HasValue)
            {
                return;
            }

            BlendShapeSubCategory[] subCategories =
                BlendShapeCategoryClassifier.GetSubCategories(_categoryFilter.Value);
            if (subCategories.Length == 0)
            {
                return;
            }

            int presentSubCategories = 0;
            for (int i = 0; i < subCategories.Length; i++)
            {
                if (_subCategoryCounts[(int)subCategories[i]] > 0)
                {
                    presentSubCategories++;
                }
            }

            if (presentSubCategories < 2)
            {
                return;
            }

            EditorGUILayout.Space(3f);

            float indent = 20f;
            float available = Mathf.Max(120f, position.width - 28f - indent);
            float used = 0f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                new GUIContent("↳ " + BlendShapeCategoryClassifier.GetLabel(_categoryFilter.Value)),
                EditorStyles.centeredGreyMiniLabel,
                GUILayout.Width(indent + 60f));

            int parentCount = _categoryCounts[(int)_categoryFilter.Value];
            if (DrawChip(new GUIContent("All (" + parentCount + ")"), !_subCategoryFilter.HasValue,
                    available, ref used)
                != !_subCategoryFilter.HasValue)
            {
                SetSubCategoryFilter(null);
            }

            for (int i = 0; i < subCategories.Length; i++)
            {
                BlendShapeSubCategory subCategory = subCategories[i];
                int count = _subCategoryCounts[(int)subCategory];
                if (count == 0)
                {
                    continue;
                }

                GUIContent content = new GUIContent(
                    BlendShapeCategoryClassifier.GetSubLabel(subCategory) + " (" + count + ")");
                bool selected = _subCategoryFilter.HasValue && _subCategoryFilter.Value == subCategory;

                if (DrawChip(content, selected, available, ref used) != selected)
                {
                    SetSubCategoryFilter(selected ? (BlendShapeSubCategory?)null : subCategory);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws one face part button, wrapping to a new row when the current one is full.
        /// A null category is the All button.
        /// </summary>
        private void DrawCategoryButton(
            BlendShapeCategory? category, string label, int count, float available, ref float used)
        {
            GUIContent content = new GUIContent(label + " (" + count + ")");

            bool selected = _categoryFilter.HasValue == category.HasValue
                && (!category.HasValue || _categoryFilter.Value == category.Value);

            if (DrawChip(content, selected, available, ref used) != selected)
            {
                // Pressing the active button again falls back to All.
                _categoryFilter = selected ? null : category;
                _subCategoryFilter = null;
                _visibleListDirty = true;
                _scrollPosition.y = 0f;
            }
        }

        /// <summary>
        /// One toggle button, wrapping to a new row when the current one is full. A selected
        /// chip gets a tinted background and bold text so the active filter stays obvious even
        /// once the row wraps across two or three lines.
        /// Returns the toggle state after the click.
        /// </summary>
        private bool DrawChip(GUIContent content, bool selected, float available, ref float used)
        {
            GUIStyle style = selected ? SelectedChipStyle : EditorStyles.miniButton;
            float width = style.CalcSize(content).x + 8f;

            if (used > 0f && used + width > available)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                used = 0f;
            }

            Color previousColor = GUI.backgroundColor;
            if (selected)
            {
                GUI.backgroundColor = SelectedChipColor;
            }

            bool pressed = GUILayout.Toggle(selected, content, style, GUILayout.Width(width));

            GUI.backgroundColor = previousColor;
            used += width + 2f;
            return pressed;
        }

        /// <summary>Bold variant of the mini button style, used for the selected chip.</summary>
        private GUIStyle SelectedChipStyle
        {
            get
            {
                if (_selectedChipStyle == null)
                {
                    _selectedChipStyle = new GUIStyle(EditorStyles.miniButton);
                    _selectedChipStyle.fontStyle = FontStyle.Bold;
                }

                return _selectedChipStyle;
            }
        }

        private static Color SelectedChipColor
        {
            get
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(0.30f, 0.55f, 0.90f, 1f)
                    : new Color(0.55f, 0.75f, 1.00f, 1f);
            }
        }

        private void SetSubCategoryFilter(BlendShapeSubCategory? subCategory)
        {
            _subCategoryFilter = subCategory;
            _visibleListDirty = true;
            _scrollPosition.y = 0f;
        }

        /// <summary>
        /// Draws the list inside a scroll view. Only the rows inside the viewport are drawn,
        /// so a Mesh with several hundred BlendShapes stays responsive.
        /// </summary>
        private void DrawShapeList()
        {
            float rowHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            float contentHeight = _displayRows.Count * rowHeight;

            Rect viewportRect = GUILayoutUtility.GetRect(
                0f, position.height, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float contentWidth = Mathf.Max(0f, viewportRect.width - ScrollBarWidth);
            Rect contentRect = new Rect(0f, 0f, contentWidth, contentHeight);

            _scrollPosition = GUI.BeginScrollView(viewportRect, _scrollPosition, contentRect);

            int firstVisible = Mathf.Max(0, Mathf.FloorToInt(_scrollPosition.y / rowHeight) - 1);
            int lastVisible = Mathf.Min(
                _displayRows.Count - 1,
                Mathf.CeilToInt((_scrollPosition.y + viewportRect.height) / rowHeight) + 1);

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                Rect fullRect = new Rect(0f, i * rowHeight, contentWidth, rowHeight);
                DisplayRow row = _displayRows[i];

                if (row.IsHeader)
                {
                    DrawGroupHeader(fullRect, row);
                }
                else
                {
                    DrawShapeRow(fullRect, row.Shape, i);
                }
            }

            GUI.EndScrollView();
        }

        /// <summary>
        /// Face part header. Clicking it folds the group shut; the BlendShapes stay in the
        /// filter result either way, so the footer count and Reset Visible are unaffected.
        /// </summary>
        private void DrawGroupHeader(Rect fullRect, DisplayRow row)
        {
            EditorGUI.DrawRect(fullRect, GroupHeaderColor);

            Rect lineRect = new Rect(
                fullRect.x + 4f, fullRect.y, fullRect.width - 8f, EditorGUIUtility.singleLineHeight);

            bool expanded = !_collapsedCategories.Contains(row.Category);

            string label = BlendShapeCategoryClassifier.GetLabel(row.Category);
            if (_subCategoryFilter.HasValue
                && BlendShapeCategoryClassifier.GetParent(_subCategoryFilter.Value) == row.Category)
            {
                label += " > " + BlendShapeCategoryClassifier.GetSubLabel(_subCategoryFilter.Value);
            }

            label += "  (" + row.GroupCount + ")";

            EditorGUI.BeginChangeCheck();
            bool nowExpanded = EditorGUI.Foldout(lineRect, expanded, label, true, GroupHeaderStyle);
            if (EditorGUI.EndChangeCheck())
            {
                if (nowExpanded)
                {
                    _collapsedCategories.Remove(row.Category);
                }
                else
                {
                    _collapsedCategories.Add(row.Category);
                }

                // Rebuilt at the top of the next OnGUI so the loop above keeps its indices.
                _displayRowsDirty = true;
            }
        }

        private void DrawShapeRow(Rect fullRect, BlendShapeData data, int displayIndex)
        {
            if (displayIndex % 2 == 1)
            {
                EditorGUI.DrawRect(fullRect, RowStripeColor);
            }

            Rect rowRect = new Rect(
                fullRect.x, fullRect.y, fullRect.width, EditorGUIUtility.singleLineHeight);

            // Only the rows currently on screen are read back, so the display stays in sync
            // without re-reading every BlendShape of the Mesh each frame.
            data.Weight = _renderer.GetBlendShapeWeight(data.Index);

            // Highlighting a non-zero weight is purely a visual cue: it never affects which
            // rows are shown, that is still what the Non-Zero filter chip is for.
            bool isNonZero = BlendShapeFilterUtility.IsNonZero(data.Weight);
            if (isNonZero)
            {
                Rect barRect = new Rect(fullRect.x, fullRect.y, NonZeroBarWidth, fullRect.height);
                EditorGUI.DrawRect(barRect, NonZeroIndicatorColor);
            }

            Rect indexRect = new Rect(
                rowRect.x + 2f + RowIndent, rowRect.y, IndexColumnWidth, rowRect.height);
            Rect nameRect = new Rect(indexRect.xMax + 2f, rowRect.y, NameColumnWidth, rowRect.height);
            Rect resetRect = new Rect(rowRect.xMax - ResetColumnWidth - 2f, rowRect.y, ResetColumnWidth, rowRect.height);
            float weightWidth = Mathf.Max(60f, resetRect.x - nameRect.xMax - 6f);
            Rect weightRect = new Rect(nameRect.xMax + 2f, rowRect.y, weightWidth, rowRect.height);

            // The Mesh internal index, unaffected by the current sort order.
            GUI.Label(indexRect, "#" + data.Index, EditorStyles.miniLabel);
            GUI.Label(nameRect, new GUIContent(data.Name, data.Name), isNonZero ? NonZeroNameStyle : EditorStyles.label);

            // Opening the Undo group before the slider handles the event means every record
            // made during the drag lands in one group and collapses into a single Undo step.
            if (Event.current.type == EventType.MouseDown && weightRect.Contains(Event.current.mousePosition))
            {
                BeginWeightUndoGroup();
            }

            EditorGUI.BeginChangeCheck();
            float newWeight = EditorGUI.Slider(
                weightRect, data.Weight, BlendShapeFilterUtility.MinWeight, BlendShapeFilterUtility.MaxWeight);
            if (EditorGUI.EndChangeCheck())
            {
                BlendShapeFilterUtility.SetWeight(_renderer, data.Index, newWeight, UndoChangeWeight);
                data.Weight = _renderer.GetBlendShapeWeight(data.Index);
                _weightsChanged = true;
            }

            using (new EditorGUI.DisabledScope(!BlendShapeFilterUtility.IsNonZero(data.Weight)))
            {
                if (GUI.Button(resetRect, "Reset"))
                {
                    CloseWeightUndoGroup();
                    Undo.IncrementCurrentGroup();
                    BlendShapeFilterUtility.SetWeight(_renderer, data.Index, 0f, UndoResetWeight);
                    data.Weight = _renderer.GetBlendShapeWeight(data.Index);
                    _weightsChanged = true;
                }
            }
        }

        /// <summary>Bold foldout used for the face part headers.</summary>
        private GUIStyle GroupHeaderStyle
        {
            get
            {
                if (_groupHeaderStyle == null)
                {
                    _groupHeaderStyle = new GUIStyle(EditorStyles.foldout);
                    _groupHeaderStyle.fontStyle = FontStyle.Bold;
                }

                return _groupHeaderStyle;
            }
        }

        private static Color GroupHeaderColor
        {
            get
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(0.22f, 0.22f, 0.22f, 1f)
                    : new Color(0.76f, 0.76f, 0.76f, 1f);
            }
        }

        private static Color RowStripeColor
        {
            get
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.03f)
                    : new Color(0f, 0f, 0f, 0.03f);
            }
        }

        /// <summary>Bold, tinted label for the name of a BlendShape whose weight is non-zero.</summary>
        private GUIStyle NonZeroNameStyle
        {
            get
            {
                if (_nonZeroNameStyle == null)
                {
                    _nonZeroNameStyle = new GUIStyle(EditorStyles.label);
                    _nonZeroNameStyle.fontStyle = FontStyle.Bold;
                }

                // isProSkin can flip while the window stays open (a light/dark toggle), so the
                // color itself is refreshed every time rather than baked in once.
                _nonZeroNameStyle.normal.textColor = NonZeroIndicatorColor;
                return _nonZeroNameStyle;
            }
        }

        private static Color NonZeroIndicatorColor
        {
            get
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(1.00f, 0.70f, 0.25f, 1f)
                    : new Color(0.80f, 0.45f, 0.00f, 1f);
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.LabelField("Showing: " + _visibleShapes.Count + " / " + _shapes.Count);
        }

        /// <summary>
        /// Sets every BlendShape that passed the filters to 0 within a single Undo record.
        /// </summary>
        private void ResetVisible()
        {
            if (_renderer == null || _visibleShapes.Count == 0)
            {
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "BlendShape Filter",
                "Reset " + _visibleShapes.Count + " visible BlendShapes to 0?",
                "Reset",
                "Cancel");

            if (!confirmed)
            {
                return;
            }

            bool anyModified = false;
            for (int i = 0; i < _visibleShapes.Count; i++)
            {
                if (BlendShapeFilterUtility.IsNonZero(_renderer.GetBlendShapeWeight(_visibleShapes[i].Index)))
                {
                    anyModified = true;
                    break;
                }
            }

            if (!anyModified)
            {
                return;
            }

            CloseWeightUndoGroup();
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(_renderer, UndoResetVisible);

            for (int i = 0; i < _visibleShapes.Count; i++)
            {
                _renderer.SetBlendShapeWeight(_visibleShapes[i].Index, 0f);
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(_renderer);

            BlendShapeFilterUtility.RefreshWeights(_renderer, _shapes);
            _visibleListDirty = true;
        }

        private void BeginWeightUndoGroup()
        {
            CloseWeightUndoGroup();
            Undo.IncrementCurrentGroup();
            _weightUndoGroup = Undo.GetCurrentGroup();
        }

        private void CloseWeightUndoGroup()
        {
            if (_weightUndoGroup < 0)
            {
                return;
            }

            Undo.CollapseUndoOperations(_weightUndoGroup);
            Undo.SetCurrentGroupName(UndoChangeWeight);
            _weightUndoGroup = -1;
        }
    }
}
