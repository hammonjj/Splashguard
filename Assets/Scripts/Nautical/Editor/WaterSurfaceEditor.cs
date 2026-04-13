using Bitbox.Toymageddon.Nautical;
using UnityEditor;
using UnityEngine;

namespace Bitbox.Toymageddon.Nautical.Editor
{
    [CustomEditor(typeof(WaterSurface))]
    [CanEditMultipleObjects]
    public sealed class WaterSurfaceEditor : UnityEditor.Editor
    {
        private const int TargetWaveCount = 4;
        private static readonly GUIContent DirectionLabel = new(
            "Direction",
            "The horizontal travel direction of this wave. The vector is normalized, so only its direction matters.");
        private static readonly GUIContent SteepnessLabel = new(
            "Steepness",
            "Controls how sharp and tall the wave crest feels. Higher values create choppier, more dramatic waves.");
        private static readonly GUIContent WavelengthLabel = new(
            "Wavelength",
            "Controls the distance between crests. Larger values make broad rolling swells, smaller values make tighter ripples.");

        private readonly bool[] _waveFoldouts = { true, true, true, true };

        private SerializedProperty _wavesProp;

        private void OnEnable()
        {
            _wavesProp = serializedObject.FindProperty("_waves");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();
            EditorGUILayout.Space();
            DrawPropertiesExcluding(serializedObject, "m_Script", "_waves");
            DrawWavesSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                MonoScript script = MonoScript.FromMonoBehaviour((WaterSurface)target);
                EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            }
        }

        private void DrawWavesSection()
        {
            EditorGUILayout.LabelField("Waves", EditorStyles.boldLabel);

            if (_wavesProp == null)
            {
                EditorGUILayout.HelpBox("Wave data is unavailable.", MessageType.Error);
                return;
            }

            if (_wavesProp.arraySize != TargetWaveCount)
            {
                _wavesProp.arraySize = TargetWaveCount;
            }

            for (int i = 0; i < TargetWaveCount; i++)
            {
                SerializedProperty waveProp = _wavesProp.GetArrayElementAtIndex(i);
                SerializedProperty directionProp = waveProp.FindPropertyRelative("direction");
                SerializedProperty steepnessProp = waveProp.FindPropertyRelative("steepness");
                SerializedProperty wavelengthProp = waveProp.FindPropertyRelative("wavelength");

                _waveFoldouts[i] = EditorGUILayout.BeginFoldoutHeaderGroup(_waveFoldouts[i], $"Wave {(char)('A' + i)}");
                if (_waveFoldouts[i])
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(directionProp, DirectionLabel);
                        EditorGUILayout.PropertyField(steepnessProp, SteepnessLabel);
                        EditorGUILayout.PropertyField(wavelengthProp, WavelengthLabel);
                    }
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }
    }
}
