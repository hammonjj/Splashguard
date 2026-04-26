using System.IO;
using ConsolePilot.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BitBox.Toymageddon.Debugging.Editor
{
    public static class ConsolePilotProjectSettingsInstaller
    {
        private const string AssetFolderPath = "Assets/Resources/ConsolePilot";
        private const string AssetPath = AssetFolderPath + "/ConsolePilotSettings.asset";
        private const string ConsoleVisualTreePath = "Packages/com.consolepilot.debugconsole/Runtime/UI/UXML/ConsolePilot.uxml";
        private const string ThemeStyleSheetPath = "Packages/com.consolepilot.debugconsole/Runtime/UI/USS/ConsolePilotTheme.uss";

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            EditorApplication.delayCall += EnsureSettingsAsset;
            EditorApplication.projectChanged += EnsureSettingsAsset;
        }

        [MenuItem("Tools/Debug/ConsolePilot/Create Or Update Settings Asset")]
        public static void EnsureSettingsAsset()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            VisualTreeAsset consoleVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ConsoleVisualTreePath);
            StyleSheet themeStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeStyleSheetPath);

            if (consoleVisualTree == null || themeStyleSheet == null)
            {
                return;
            }

            EnsureFolderExists(AssetFolderPath);

            ConsolePilotSettings settings = AssetDatabase.LoadAssetAtPath<ConsolePilotSettings>(AssetPath);
            bool created = false;

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ConsolePilotSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                created = true;
            }

            SerializedObject serializedObject = new SerializedObject(settings);
            SerializedProperty useBuiltInToggleInputProperty = serializedObject.FindProperty("_useBuiltInToggleInput");
            SerializedProperty visualTreeProperty = serializedObject.FindProperty("_consoleVisualTree");
            SerializedProperty styleSheetProperty = serializedObject.FindProperty("_themeStyleSheet");

            bool changed = false;

            if (useBuiltInToggleInputProperty != null && useBuiltInToggleInputProperty.boolValue)
            {
                useBuiltInToggleInputProperty.boolValue = false;
                changed = true;
            }

            if (visualTreeProperty != null && visualTreeProperty.objectReferenceValue != consoleVisualTree)
            {
                visualTreeProperty.objectReferenceValue = consoleVisualTree;
                changed = true;
            }

            if (styleSheetProperty != null && styleSheetProperty.objectReferenceValue != themeStyleSheet)
            {
                styleSheetProperty.objectReferenceValue = themeStyleSheet;
                changed = true;
            }

            if (!changed && !created)
            {
                return;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parentFolderPath = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);

            if (string.IsNullOrWhiteSpace(parentFolderPath))
            {
                return;
            }

            EnsureFolderExists(parentFolderPath);
            AssetDatabase.CreateFolder(parentFolderPath, folderName);
        }
    }
}
