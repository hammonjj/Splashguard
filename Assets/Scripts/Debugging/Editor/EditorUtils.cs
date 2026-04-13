using UnityEditor;
using UnityEngine;

namespace BitBox.Toymageddon.Debugging.Editor
{
    public static class EditorUtils
    {
        private static readonly float IconWidth = 24f;
        private static readonly float IconHeight = 24f;

        public static bool DrawRemoveIconButton(string tooltip)
        {
            var icon = EditorGUIUtility.IconContent("TreeEditor.Trash");
            if (icon == null || icon.image == null)
            {
                icon = new GUIContent("x");
            }

            icon.tooltip = tooltip;
            return GUILayout.Button(icon, GUILayout.Width(IconWidth), GUILayout.Height(IconHeight));
        }

        public static bool DrawOpenIconButton(string tooltip)
        {
            var icon = EditorGUIUtility.IconContent("FolderOpened On Icon");
            if (icon == null || icon.image == null)
            {
                icon = new GUIContent("x");
            }

            icon.tooltip = tooltip;
            return GUILayout.Button(icon, GUILayout.Width(IconWidth), GUILayout.Height(IconHeight));
        }

        public static void DrawDraggableAssetLabel(UnityEngine.Object asset, string label, GUIStyle style, float height)
        {
            var content = asset != null
                ? EditorGUIUtility.ObjectContent(asset, asset.GetType())
                : new GUIContent();

            content.text = label;
            var rect = GUILayoutUtility.GetRect(content, style, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            GUI.Label(rect, content, style);
            HandleAssetDrag(rect, asset, label);
        }

        public static void HandleAssetDrag(Rect rect, UnityEngine.Object asset, string label)
        {
            if (asset == null)
            {
                return;
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Pan);
            var controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            var currentEvent = Event.current;
            switch (currentEvent.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (currentEvent.button != 0 || !rect.Contains(currentEvent.mousePosition))
                    {
                        return;
                    }

                    GUIUtility.hotControl = controlId;
                    currentEvent.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId)
                    {
                        return;
                    }

                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new[] { asset };
                    var assetPath = AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrWhiteSpace(assetPath))
                    {
                        DragAndDrop.paths = new[] { assetPath };
                    }

                    DragAndDrop.StartDrag(label);
                    GUIUtility.hotControl = 0;
                    currentEvent.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId)
                    {
                        return;
                    }

                    GUIUtility.hotControl = 0;
                    currentEvent.Use();
                    break;
            }
        }
    }
}
