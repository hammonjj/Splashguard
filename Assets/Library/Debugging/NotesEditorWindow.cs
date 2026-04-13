#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace BitBox.Library.Debugging
{
  public class NotesEditorWindow : EditorWindow
  {
    private Vector2 _scroll;
    private string notes = "";
    private string lastSavedNotes = "";
    private double nextSaveTime = 0;
    private const double DebounceSeconds = 0.5f;
    private const string NotesFolder = "Assets/DevNotes";
    private const string NotesPath = NotesFolder + "/dev-notes.md";

    [MenuItem("Tools/Notes")]
    public static void ShowWindow()
    {
      GetWindow<NotesEditorWindow>("Notes");
    }

    private void OnEnable()
    {
      LoadNotesFromFile();
    }

    private void OnDisable()
    {
      EditorApplication.update -= AutoSaveUpdate;
    }

    private void OnGUI()
    {
      GUILayout.Label("Notes", EditorStyles.boldLabel);

      _scroll = EditorGUILayout.BeginScrollView(_scroll);

      var style = new GUIStyle(EditorStyles.textArea)
      {
        wordWrap = true
      };

      EditorGUI.BeginChangeCheck();
      notes = EditorGUILayout.TextArea(notes, style, GUILayout.ExpandHeight(true));
      if (EditorGUI.EndChangeCheck())
      {
        nextSaveTime = EditorApplication.timeSinceStartup + DebounceSeconds;
        EditorApplication.update -= AutoSaveUpdate;
        EditorApplication.update += AutoSaveUpdate;
      }

      EditorGUILayout.EndScrollView();
    }

    private void AutoSaveUpdate()
    {
      if (EditorApplication.timeSinceStartup >= nextSaveTime)
      {
        SaveNotesToFile();
        EditorApplication.update -= AutoSaveUpdate;
      }
    }

    private void LoadNotesFromFile()
    {
      if (File.Exists(NotesPath))
      {
        notes = File.ReadAllText(NotesPath);
        lastSavedNotes = notes;
      }
      else
      {
        notes = "";
        lastSavedNotes = "";
      }
    }

    private void SaveNotesToFile()
    {
      if (notes == lastSavedNotes)
      {
        return;
      }

      if (!Directory.Exists(NotesFolder))
      {
        Directory.CreateDirectory(NotesFolder);
        AssetDatabase.Refresh();
      }

      File.WriteAllText(NotesPath, notes);
      lastSavedNotes = notes;
    }
  }
}
#endif
