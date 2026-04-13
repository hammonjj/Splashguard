#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BitBox.Library.Eventing;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MessageBusEventSender))]
public class MessageBusEventSenderEditor : Editor
{
    private static readonly List<Type> CachedEventTypes = new List<Type>();
    private static readonly List<string> CachedEventTypeNames = new List<string>();
    private static bool _cacheBuilt;

    private SerializedProperty _messageBusProp;
    private SerializedProperty _autoFindProp;
    private SerializedProperty _quickEventsProp;

    private int _addQuickIndex;

    private void OnEnable()
    {
        _messageBusProp = serializedObject.FindProperty("_messageBus");
        _autoFindProp = serializedObject.FindProperty("_autoFindMessageBus");
        _quickEventsProp = serializedObject.FindProperty("_quickEvents");

        BuildEventCache();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_messageBusProp);
        EditorGUILayout.PropertyField(_autoFindProp);

        if (GUILayout.Button("Find Message Bus"))
        {
            var sender = (MessageBusEventSender)target;
            Undo.RecordObject(sender, "Find Message Bus");
            sender.RefreshMessageBus();
            EditorUtility.SetDirty(sender);
        }

        if (_messageBusProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign a MessageBus or enable Auto Find.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        DrawEventSection("Events", _quickEventsProp, ref _addQuickIndex);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEventSection(string label, SerializedProperty listProp, ref int addIndex)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        if (CachedEventTypeNames.Count == 0)
        {
            EditorGUILayout.HelpBox("No event types found.", MessageType.Info);
            return;
        }

        addIndex = Mathf.Clamp(addIndex, 0, CachedEventTypeNames.Count - 1);
        addIndex = EditorGUILayout.Popup("Add Event", addIndex, CachedEventTypeNames.ToArray());

        if (GUILayout.Button("Add"))
        {
            AddEventEntry(listProp, CachedEventTypes[addIndex]);
        }

        EditorGUILayout.Space(2f);

        if (listProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No events added.", MessageType.Info);
            return;
        }

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var entryProp = listProp.GetArrayElementAtIndex(i);
            if (DrawEventEntry(entryProp))
            {
                listProp.DeleteArrayElementAtIndex(i);
                break;
            }
        }
    }

    private bool DrawEventEntry(SerializedProperty entryProp)
    {
        var typeNameProp = entryProp.FindPropertyRelative("TypeName");
        var friendlyNameProp = entryProp.FindPropertyRelative("FriendlyName");
        var expandedProp = entryProp.FindPropertyRelative("Expanded");
        var fieldsProp = entryProp.FindPropertyRelative("Fields");

        var eventType = ResolveEventType(typeNameProp.stringValue);
        var displayName = !string.IsNullOrEmpty(friendlyNameProp.stringValue)
            ? friendlyNameProp.stringValue
            : (eventType != null ? eventType.Name : typeNameProp.stringValue);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        expandedProp.boolValue = EditorGUILayout.Foldout(expandedProp.boolValue, displayName, true);

        using (new EditorGUI.DisabledScope(eventType == null))
        {
            if (GUILayout.Button("Send", GUILayout.Width(60)))
            {
                SendEvent(eventType, fieldsProp);
            }
        }

        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }

        EditorGUILayout.EndHorizontal();

        if (eventType == null)
        {
            EditorGUILayout.HelpBox("Event type not found. It may have been renamed or removed.", MessageType.Warning);
        }
        else if (expandedProp.boolValue)
        {
            SyncFields(eventType, fieldsProp);
            DrawFields(eventType, fieldsProp);
        }

        EditorGUILayout.EndVertical();
        return false;
    }

    private void DrawFields(Type eventType, SerializedProperty fieldsProp)
    {
        var fields = eventType.GetFields(BindingFlags.Public | BindingFlags.Instance);
        if (fields.Length == 0)
        {
            EditorGUILayout.LabelField("No fields.");
            return;
        }

        for (int i = 0; i < fieldsProp.arraySize; i++)
        {
            var fieldProp = fieldsProp.GetArrayElementAtIndex(i);
            var fieldNameProp = fieldProp.FindPropertyRelative("FieldName");
            var fieldTypeNameProp = fieldProp.FindPropertyRelative("FieldTypeName");

            var fieldInfo = fields.FirstOrDefault(f => f.Name == fieldNameProp.stringValue);
            if (fieldInfo == null)
            {
                EditorGUILayout.LabelField(fieldNameProp.stringValue, "Missing field");
                continue;
            }

            var label = ObjectNames.NicifyVariableName(fieldInfo.Name);
            var fieldType = fieldInfo.FieldType;

            using (new EditorGUI.DisabledScope(fieldInfo.IsInitOnly))
            {
                DrawFieldValue(label, fieldType, fieldProp);
            }

            fieldTypeNameProp.stringValue = fieldType.AssemblyQualifiedName;
        }
    }

    private void DrawFieldValue(string label, Type fieldType, SerializedProperty fieldProp)
    {
        if (fieldType == typeof(int))
        {
            var intProp = fieldProp.FindPropertyRelative("IntValue");
            intProp.intValue = EditorGUILayout.IntField(label, intProp.intValue);
            return;
        }

        if (fieldType == typeof(float))
        {
            var floatProp = fieldProp.FindPropertyRelative("FloatValue");
            floatProp.floatValue = EditorGUILayout.FloatField(label, floatProp.floatValue);
            return;
        }

        if (fieldType == typeof(bool))
        {
            var boolProp = fieldProp.FindPropertyRelative("BoolValue");
            boolProp.boolValue = EditorGUILayout.Toggle(label, boolProp.boolValue);
            return;
        }

        if (fieldType == typeof(string))
        {
            var stringProp = fieldProp.FindPropertyRelative("StringValue");
            stringProp.stringValue = EditorGUILayout.TextField(label, stringProp.stringValue);
            return;
        }

        if (fieldType == typeof(Vector2))
        {
            var vecProp = fieldProp.FindPropertyRelative("Vector2Value");
            vecProp.vector2Value = EditorGUILayout.Vector2Field(label, vecProp.vector2Value);
            return;
        }

        if (fieldType == typeof(Vector3))
        {
            var vecProp = fieldProp.FindPropertyRelative("Vector3Value");
            vecProp.vector3Value = EditorGUILayout.Vector3Field(label, vecProp.vector3Value);
            return;
        }

        if (fieldType == typeof(Vector4))
        {
            var vecProp = fieldProp.FindPropertyRelative("Vector4Value");
            vecProp.vector4Value = EditorGUILayout.Vector4Field(label, vecProp.vector4Value);
            return;
        }

        if (fieldType == typeof(Quaternion))
        {
            var quatProp = fieldProp.FindPropertyRelative("QuaternionValue");
            var vec = new Vector4(quatProp.quaternionValue.x, quatProp.quaternionValue.y,
                quatProp.quaternionValue.z, quatProp.quaternionValue.w);
            vec = EditorGUILayout.Vector4Field(label, vec);
            quatProp.quaternionValue = new Quaternion(vec.x, vec.y, vec.z, vec.w);
            return;
        }

        if (fieldType == typeof(Color))
        {
            var colorProp = fieldProp.FindPropertyRelative("ColorValue");
            colorProp.colorValue = EditorGUILayout.ColorField(label, colorProp.colorValue);
            return;
        }

        if (fieldType == typeof(long))
        {
            var longProp = fieldProp.FindPropertyRelative("LongValue");
            longProp.longValue = EditorGUILayout.LongField(label, longProp.longValue);
            return;
        }

        if (fieldType == typeof(double))
        {
            var doubleProp = fieldProp.FindPropertyRelative("DoubleValue");
            doubleProp.doubleValue = EditorGUILayout.DoubleField(label, doubleProp.doubleValue);
            return;
        }

        if (fieldType == typeof(DateTime))
        {
            var stringProp = fieldProp.FindPropertyRelative("StringValue");
            stringProp.stringValue = EditorGUILayout.TextField(label, stringProp.stringValue);
            return;
        }

        if (fieldType.IsEnum)
        {
            var enumProp = fieldProp.FindPropertyRelative("EnumValue");
            var enumValue = (Enum)Enum.ToObject(fieldType, enumProp.intValue);
            enumValue = EditorGUILayout.EnumPopup(label, enumValue);
            enumProp.intValue = Convert.ToInt32(enumValue);
            return;
        }

        if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
        {
            var objProp = fieldProp.FindPropertyRelative("ObjectValue");
            objProp.objectReferenceValue = EditorGUILayout.ObjectField(label, objProp.objectReferenceValue, fieldType, true);
            return;
        }

        EditorGUILayout.LabelField(label, $"Unsupported field type: {fieldType.Name}");
    }

    private void AddEventEntry(SerializedProperty listProp, Type eventType)
    {
        listProp.arraySize++;
        var entryProp = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
        entryProp.FindPropertyRelative("TypeName").stringValue = eventType.AssemblyQualifiedName;
        entryProp.FindPropertyRelative("FriendlyName").stringValue = eventType.Name;
        entryProp.FindPropertyRelative("Expanded").boolValue = true;

        var fieldsProp = entryProp.FindPropertyRelative("Fields");
        fieldsProp.arraySize = 0;

        var fields = eventType.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            fieldsProp.arraySize++;
            var fieldProp = fieldsProp.GetArrayElementAtIndex(fieldsProp.arraySize - 1);
            fieldProp.FindPropertyRelative("FieldName").stringValue = field.Name;
            fieldProp.FindPropertyRelative("FieldTypeName").stringValue = field.FieldType.AssemblyQualifiedName;
        }
    }

    private void SyncFields(Type eventType, SerializedProperty fieldsProp)
    {
        var fields = eventType.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var existing = new HashSet<string>();

        for (int i = fieldsProp.arraySize - 1; i >= 0; i--)
        {
            var fieldProp = fieldsProp.GetArrayElementAtIndex(i);
            var name = fieldProp.FindPropertyRelative("FieldName").stringValue;
            existing.Add(name);
            if (!fields.Any(f => f.Name == name))
            {
                fieldsProp.DeleteArrayElementAtIndex(i);
            }
        }

        foreach (var field in fields)
        {
            if (existing.Contains(field.Name))
            {
                continue;
            }

            fieldsProp.arraySize++;
            var fieldProp = fieldsProp.GetArrayElementAtIndex(fieldsProp.arraySize - 1);
            fieldProp.FindPropertyRelative("FieldName").stringValue = field.Name;
            fieldProp.FindPropertyRelative("FieldTypeName").stringValue = field.FieldType.AssemblyQualifiedName;
        }
    }

    private void SendEvent(Type eventType, SerializedProperty fieldsProp)
    {
        if (eventType == null)
        {
            return;
        }

        var sender = (MessageBusEventSender)target;
        var bus = sender.MessageBus != null ? sender.MessageBus : sender.GetComponentInParent<MessageBus>();
        if (bus == null)
        {
            Debug.LogWarning("MessageBusEventSender: MessageBus not found.");
            return;
        }

        var evt = Activator.CreateInstance(eventType);

        for (int i = 0; i < fieldsProp.arraySize; i++)
        {
            var fieldProp = fieldsProp.GetArrayElementAtIndex(i);
            var fieldName = fieldProp.FindPropertyRelative("FieldName").stringValue;
            var fieldInfo = eventType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo == null || fieldInfo.IsInitOnly)
            {
                continue;
            }

            var value = GetFieldValue(fieldInfo.FieldType, fieldProp);
            if (value != null || fieldInfo.FieldType.IsValueType)
            {
                fieldInfo.SetValue(evt, value);
            }
        }

        var publishMethod = typeof(MessageBus).GetMethod("Publish", BindingFlags.Public | BindingFlags.Instance);
        var genericMethod = publishMethod?.MakeGenericMethod(eventType);
        genericMethod?.Invoke(bus, new[] { evt });
    }

    private object GetFieldValue(Type fieldType, SerializedProperty fieldProp)
    {
        if (fieldType == typeof(int))
        {
            return fieldProp.FindPropertyRelative("IntValue").intValue;
        }

        if (fieldType == typeof(float))
        {
            return fieldProp.FindPropertyRelative("FloatValue").floatValue;
        }

        if (fieldType == typeof(bool))
        {
            return fieldProp.FindPropertyRelative("BoolValue").boolValue;
        }

        if (fieldType == typeof(string))
        {
            return fieldProp.FindPropertyRelative("StringValue").stringValue;
        }

        if (fieldType == typeof(Vector2))
        {
            return fieldProp.FindPropertyRelative("Vector2Value").vector2Value;
        }

        if (fieldType == typeof(Vector3))
        {
            return fieldProp.FindPropertyRelative("Vector3Value").vector3Value;
        }

        if (fieldType == typeof(Vector4))
        {
            return fieldProp.FindPropertyRelative("Vector4Value").vector4Value;
        }

        if (fieldType == typeof(Quaternion))
        {
            return fieldProp.FindPropertyRelative("QuaternionValue").quaternionValue;
        }

        if (fieldType == typeof(Color))
        {
            return fieldProp.FindPropertyRelative("ColorValue").colorValue;
        }

        if (fieldType == typeof(long))
        {
            return fieldProp.FindPropertyRelative("LongValue").longValue;
        }

        if (fieldType == typeof(double))
        {
            return fieldProp.FindPropertyRelative("DoubleValue").doubleValue;
        }

        if (fieldType == typeof(DateTime))
        {
            var text = fieldProp.FindPropertyRelative("StringValue").stringValue;
            if (DateTime.TryParse(text, out var parsed))
            {
                return parsed;
            }

            return DateTime.Now;
        }

        if (fieldType.IsEnum)
        {
            var enumValue = fieldProp.FindPropertyRelative("EnumValue").intValue;
            return Enum.ToObject(fieldType, enumValue);
        }

        if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
        {
            return fieldProp.FindPropertyRelative("ObjectValue").objectReferenceValue;
        }

        return null;
    }

    private static void BuildEventCache()
    {
        if (_cacheBuilt)
        {
            return;
        }

        _cacheBuilt = true;
        CachedEventTypes.Clear();
        CachedEventTypeNames.Clear();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            var assemblyName = assembly.GetName().Name;
            if (assemblyName != "Assembly-CSharp")
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            foreach (var type in types)
            {
                if (type == null || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (!type.IsClass && !type.IsValueType)
                {
                    continue;
                }

                if (!type.Name.EndsWith("Event", StringComparison.Ordinal) &&
                    (type.Namespace == null || !type.Namespace.StartsWith("Core.Eventing", StringComparison.Ordinal)))
                {
                    continue;
                }

                CachedEventTypes.Add(type);
            }
        }

        CachedEventTypes.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
        CachedEventTypeNames.AddRange(CachedEventTypes.Select(t => t.FullName ?? t.Name));
    }

    private static Type ResolveEventType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        var type = Type.GetType(typeName);
        if (type != null)
        {
            return type;
        }

        return CachedEventTypes.FirstOrDefault(t => t.FullName == typeName || t.Name == typeName);
    }
}
#endif
