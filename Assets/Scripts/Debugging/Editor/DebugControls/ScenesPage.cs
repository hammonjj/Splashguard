#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using BitBox.Toymageddon.Debugging.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bitbox.Toymageddon.Debugging.Editor.DebugControls
{
    public sealed class ScenesPage : EditorWindow
    {
        private const int FixedColumnCount = 2;
        private const float DropAreaHeight = 68f;
        private const float ScrollViewPadding = 18f;
        private const float SceneContentHorizontalMargin = 8f;
        private const float MinimumWindowHeight = 220f;

        private Vector2 _scrollPosition;

        [MenuItem("Tools/Debug Controls/Scene Management")]
        public static void ShowWindow()
        {
            var window = GetWindow<ScenesPage>();
            window.titleContent = new GUIContent("Scenes");
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
            EditorGUILayout.LabelField("Tracked Scenes", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);
            DrawTrackedScenesList();
            EditorGUILayout.Space(8f);
            DrawDropArea();
            EndPageContent();
        }

        private static void BeginPageContent()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(SceneContentHorizontalMargin);
            EditorGUILayout.BeginVertical();
        }

        private static void EndPageContent()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(SceneContentHorizontalMargin);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDropArea()
        {
            var dropArea = GUILayoutUtility.GetRect(0f, DropAreaHeight, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag scene assets here from the Project window. Drag cards out, or use the open icon.", EditorStyles.helpBox);

            var currentEvent = Event.current;
            if (!dropArea.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform)
            {
                return;
            }

            DragAndDrop.visualMode = HasSupportedScenes(DragAndDrop.objectReferences)
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;

            if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddDraggedScenes(DragAndDrop.objectReferences);
            }

            currentEvent.Use();
        }

        private void DrawTrackedScenesList()
        {
            var trackedSceneGuids = DebugControlsWindowState.instance.TrackedSceneGuids;
            if (trackedSceneGuids.Count == 0)
            {
                EditorGUILayout.HelpBox("No scenes tracked yet. Use the drop area below to add shortcuts.", MessageType.Info);
                return;
            }

            var availableWidth = GetAvailableContentWidth();
            TrackedAssetCardGrid.DrawFixedCardGrid(
                trackedSceneGuids.Count,
                availableWidth,
                FixedColumnCount,
                (index, rect) => DrawTrackedSceneCard(index, rect));
        }

        private bool DrawTrackedSceneCard(int index, Rect rect)
        {
            var sceneGuid = DebugControlsWindowState.instance.TrackedSceneGuids[index];
            var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            var hasValidScene = !string.IsNullOrWhiteSpace(scenePath) &&
                scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
            var sceneName = hasValidScene ? Path.GetFileNameWithoutExtension(scenePath) : "Missing Scene";
            var sceneAsset = hasValidScene ? AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) : null;
            var tooltip = hasValidScene ? scenePath : "Tracked scene asset is missing";
            var result = TrackedAssetCardGrid.DrawTrackedAssetCard(
                rect,
                sceneAsset,
                sceneName,
                tooltip,
                hasValidScene,
                "Open scene",
                "Remove tracked scene");

            if (result.OpenClicked)
            {
                OpenScene(scenePath);
            }

            if (result.RemoveClicked)
            {
                RemoveTrackedSceneAt(index);
                return true;
            }

            return false;
        }

        private void AddDraggedScenes(UnityEngine.Object[] draggedObjects)
        {
            if (draggedObjects == null || draggedObjects.Length == 0)
            {
                return;
            }

            bool hasChanges = false;
            List<string> trackedSceneGuids = DebugControlsWindowState.instance.TrackedSceneGuids;
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject == null)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                if (string.IsNullOrWhiteSpace(assetPath) ||
                    !assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string sceneGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrWhiteSpace(sceneGuid) || trackedSceneGuids.Contains(sceneGuid))
                {
                    continue;
                }

                trackedSceneGuids.Add(sceneGuid);
                hasChanges = true;
            }

            if (hasChanges)
            {
                DebugControlsWindowState.instance.SaveState();
            }
        }

        private void RemoveTrackedSceneAt(int index)
        {
            List<string> trackedSceneGuids = DebugControlsWindowState.instance.TrackedSceneGuids;
            if (index < 0 || index >= trackedSceneGuids.Count)
            {
                return;
            }

            trackedSceneGuids.RemoveAt(index);
            DebugControlsWindowState.instance.SaveState();
        }

        private void OpenScene(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        private float GetAvailableContentWidth()
        {
            var availableWidth = EditorGUIUtility.currentViewWidth
                - (SceneContentHorizontalMargin * 2f)
                - ScrollViewPadding;
            return Mathf.Max(TrackedAssetCardGrid.PreferredCardWidth * 0.5f, availableWidth);
        }

        private static float GetMinimumWindowWidth()
        {
            return TrackedAssetCardGrid.GetRequiredWidthForColumns(FixedColumnCount)
                + (SceneContentHorizontalMargin * 2f)
                + ScrollViewPadding;
        }

        private static bool HasSupportedScenes(UnityEngine.Object[] draggedObjects)
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
                    assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
