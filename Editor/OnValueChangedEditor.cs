using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class OnValueChangedEditor : Editor
    {
        private object _targetInstance;
        private Type _targetType;
        private FieldInfo[] _serializedFields;
        private SerializedProperty[] _serializedProperties;
        private MethodInfo[] _methodsWithAttribute;
        private string[] _monitoredFieldNames;

        public void OnEnable()
        {
            _targetInstance = target;
            _targetType = target.GetType();

            _methodsWithAttribute = _targetType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.GetCustomAttributes(typeof(OnValueChangedAttribute), true).Length > 0)
                .ToArray();

            _monitoredFieldNames = _methodsWithAttribute
                .SelectMany(method => method.GetCustomAttributes(typeof(OnValueChangedAttribute), true)
                    .Cast<OnValueChangedAttribute>()
                    .SelectMany(attribute => attribute.FieldNames))
                .Distinct()
                .ToArray();

            _serializedFields = _monitoredFieldNames
                .Select(field => _targetType.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(field => field != null)
                .ToArray();

            _serializedProperties = _serializedFields
                .Select(field => serializedObject.FindProperty(field.Name))
                .Where(property => property != null)
                .ToArray();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            DrawDefaultInspector();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();

                foreach (var field in _serializedFields)
                {
                    string fieldName = field.Name;

                    foreach (var method in _methodsWithAttribute)
                    {
                        var attributes = method.GetCustomAttributes(typeof(OnValueChangedAttribute), true);
                        foreach (OnValueChangedAttribute attribute in attributes)
                        {
                            foreach (var attributeFieldName in attribute.FieldNames)
                                if (attributeFieldName != fieldName)
                                    continue;

                            var parameters = method.GetParameters();
                            if (parameters.Length == 0)
                                method.Invoke(_targetInstance, null);
                            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                                method.Invoke(_targetInstance, new object[] { fieldName });
                        }
                    }
                }
            }
        }
    }
}