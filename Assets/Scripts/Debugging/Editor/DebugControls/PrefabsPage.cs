#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using BitBox.Toymageddon.Debugging.Editor;
using UnityEditor;
using UnityEngine;

namespace Bitbox.Toymageddon.Debugging.Editor.DebugControls
{
    public sealed class PrefabsPage : EditorWindow
    {
        private const int FixedColumnCount = 2;
        private const float DropAreaHeight = 68f;
        private const float ScrollViewPadding = 18f;
        private const float PrefabContentHorizontalMargin = 8f;
        private const float MinimumWindowHeight = 220f;

        private Vector2 _scrollPosition;

        [MenuItem("Tools/Debug Controls/Prefabs Shortcuts")]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabsPage>();
            window.titleContent = new GUIContent("Prefabs");
            var minimumSize = new Vector2(GetMinimumWindowWidth(), MinimumWindowHeight);
            window.minSize = minimumSize;
            if (window.position.width < minimumSize.x || window.position.height < minimumSize.y)
            {
                window.position = new Rect(
                    window.position.x,
                    window.position.y,
                    Mathf.Max(window.position.width, minimumSize.x),
                    Mathf.Max(window.position.height, minimumSize.y));
            }
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawMainTab();
            EditorGUILayout.EndScrollView();
        }

        private void DrawMainTab()
        {
            BeginPageContent();
            EditorGUILayout.LabelField("Tracked Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);
            DrawTrackedPrefabsList();
            EditorGUILayout.Space(8f);
            DrawDropArea();
            EndPageContent();
        }

        private static void BeginPageContent()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(PrefabContentHorizontalMargin);
            EditorGUILayout.BeginVertical();
        }

        private static void EndPageContent()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(PrefabContentHorizontalMargin);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDropArea()
        {
            var dropArea = GUILayoutUtility.GetRect(0f, DropAreaHeight, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag prefab assets here from the Project window. Drag cards out, or use the open icon.", EditorStyles.helpBox);

            var currentEvent = Event.current;
            if (!dropArea.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform)
            {
                return;
            }

            DragAndDrop.visualMode = HasSupportedPrefabs(DragAndDrop.objectReferences)
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;

            if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddDraggedPrefabs(DragAndDrop.objectReferences);
            }

            currentEvent.Use();
        }

        private void DrawTrackedPrefabsList()
        {
            var trackedPrefabGuids = DebugControlsWindowState.instance.TrackedPrefabGuids;
            if (trackedPrefabGuids.Count == 0)
            {
                EditorGUILayout.HelpBox("No prefabs tracked yet. Use the drop area below to add shortcuts.", MessageType.Info);
                return;
            }

            var availableWidth = GetAvailableContentWidth();
            TrackedAssetCardGrid.DrawFixedCardGrid(
                trackedPrefabGuids.Count,
                availableWidth,
                FixedColumnCount,
                (index, rect) => DrawTrackedPrefabCard(index, rect));
        }

        private bool DrawTrackedPrefabCard(int index, Rect rect)
        {
            var prefabGuid = DebugControlsWindowState.instance.TrackedPrefabGuids[index];
            var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            var hasValidPrefab = !string.IsNullOrWhiteSpace(prefabPath) &&
                prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            var prefabName = hasValidPrefab ? Path.GetFileNameWithoutExtension(prefabPath) : "Missing Prefab";
            var prefabAsset = hasValidPrefab ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) : null;
            var tooltip = hasValidPrefab ? prefabPath : "Tracked prefab asset is missing";
            var result = TrackedAssetCardGrid.DrawTrackedAssetCard(
                rect,
                prefabAsset,
                prefabName,
                tooltip,
                hasValidPrefab,
                "Open prefab",
                "Remove tracked prefab");

            if (result.OpenClicked)
            {
                OpenPrefab(prefabPath);
            }

            if (result.RemoveClicked)
            {
                RemoveTrackedPrefabAt(index);
                return true;
            }

            return false;
        }

        private void AddDraggedPrefabs(UnityEngine.Object[] draggedObjects)
        {
            if (draggedObjects == null || draggedObjects.Length == 0)
            {
                return;
            }

            bool hasChanges = false;
            List<string> trackedPrefabGuids = DebugControlsWindowState.instance.TrackedPrefabGuids;
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject == null)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                if (string.IsNullOrWhiteSpace(assetPath) ||
                    !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string prefabGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrWhiteSpace(prefabGuid) || trackedPrefabGuids.Contains(prefabGuid))
                {
                    continue;
                }

                trackedPrefabGuids.Add(prefabGuid);
                hasChanges = true;
            }

            if (hasChanges)
            {
                DebugControlsWindowState.instance.SaveState();
            }
        }

        private void RemoveTrackedPrefabAt(int index)
        {
            List<string> trackedPrefabGuids = DebugControlsWindowState.instance.TrackedPrefabGuids;
            if (index < 0 || index >= trackedPrefabGuids.Count)
            {
                return;
            }

            trackedPrefabGuids.RemoveAt(index);
            DebugControlsWindowState.instance.SaveState();
        }

        private void OpenPrefab(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                return;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                return;
            }

            AssetDatabase.OpenAsset(prefabAsset);
        }

        private float GetAvailableContentWidth()
        {
            var availableWidth = EditorGUIUtility.currentViewWidth
                - (PrefabContentHorizontalMargin * 2f)
                - ScrollViewPadding;
            return Mathf.Max(TrackedAssetCardGrid.PreferredCardWidth * 0.5f, availableWidth);
        }

        private static float GetMinimumWindowWidth()
        {
            return TrackedAssetCardGrid.GetRequiredWidthForColumns(FixedColumnCount)
                + (PrefabContentHorizontalMargin * 2f)
                + ScrollViewPadding;
        }

        private static bool HasSupportedPrefabs(UnityEngine.Object[] draggedObjects)
        {
            if (draggedObjects == null || draggedObjects.Length == 0)
            {
                return false;
            }

            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject == null)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                if (!string.IsNullOrWhiteSpace(assetPath) &&
                    assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
