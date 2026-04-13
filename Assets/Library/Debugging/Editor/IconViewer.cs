#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BitBox.Library.Debugging.Editor
{
    public class IconViewer : EditorWindow
    {
        private Vector2 _scroll;
        private List<Texture2D> _allTextures;
        private List<Texture2D> _filteredTextures;
        private string _search = string.Empty;

        private const float IconSize = 40f;
        private const float Padding = 6f;

        [MenuItem("Tools/Built-in Icon Viewer")]
        public static void ShowWindow()
        {
            GetWindow<IconViewer>("Icon Viewer");
        }

        private void OnEnable()
        {
            _allTextures = Resources
                .FindObjectsOfTypeAll<Texture2D>()
                .Where(t => t.width <= 64 && t.height <= 64)
                .OrderBy(t => t.name)
                .ToList();

            _filteredTextures = new List<Texture2D>(_allTextures);
        }

        private void OnGUI()
        {
            DrawSearchBar();
            DrawIconsGrid();
        }

        private void DrawSearchBar()
        {
            EditorGUI.BeginChangeCheck();

            _search = EditorGUILayout.TextField("Search", _search);

            if (EditorGUI.EndChangeCheck())
            {
                FilterIcons();
            }
        }

        private void FilterIcons()
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                _filteredTextures = new List<Texture2D>(_allTextures);
                return;
            }

            string lower = _search.ToLowerInvariant();

            _filteredTextures = _allTextures
                .Where(t => t.name.ToLowerInvariant().Contains(lower))
                .ToList();
        }

        private void DrawIconsGrid()
        {
            if (_filteredTextures == null)
            {
                return;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt(position.width / (IconSize + Padding)));

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int index = 0;

            while (index < _filteredTextures.Count)
            {
                EditorGUILayout.BeginHorizontal();

                for (int i = 0; i < columns && index < _filteredTextures.Count; i++)
                {
                    Texture2D texture = _filteredTextures[index];

                    if (GUILayout.Button(new GUIContent(texture), GUILayout.Width(IconSize), GUILayout.Height(IconSize)))
                    {
                        Debug.Log(texture.name);
                        EditorGUIUtility.systemCopyBuffer = texture.name;
                    }

                    index++;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
