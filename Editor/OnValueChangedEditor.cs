#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace UnityEssentials
{

    public static class OnValueChangedEditor
    {
        public class PropertySnapshot
        {
            public SerializedProperty Property;
            public string Name;
            public object Value;

            public PropertySnapshot(SerializedProperty property)
            {
                Property = property;
                Name = property.name;
                Value = GetPropertyValue(property);
            }
        }

        private static List<MethodInfo> s_monitoredMethods;
        private static List<PropertySnapshot> s_monitoredProperties;

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            InspectorHook.AddInitialization(OnInitialization);
            InspectorHook.AddPostProcess(OnPostProcess);
        }

        public static void OnInitialization()
        {
            s_monitoredMethods = new();
            s_monitoredProperties = new();

            InspectorHook.GetAllProperties(out var allProperties);
            InspectorHook.GetAllMethods(out var allMethods);

            foreach (var method in allMethods)
                if (InspectorHookUtilities.TryGetAttribute<OnValueChangedAttribute>(method, out var attribute))
                {
                    s_monitoredMethods.Add(method);
                    foreach (var property in allProperties)
                        if (attribute.FieldNames.Any(fieldName => fieldName.Equals(property.name)))
                            s_monitoredProperties.Add(new(property));
                }
        }

        public static void OnPostProcess()
        {
            foreach (PropertySnapshot snapshot in s_monitoredProperties)
                foreach (var method in s_monitoredMethods)
                {
                    InspectorHookUtilities.TryGetAttributes<OnValueChangedAttribute>(method, out var attributes);
                    foreach (var attribute in attributes)
                    {
                        foreach (var attributeFieldName in attribute.FieldNames)
                            if (attributeFieldName != snapshot.Name)
                                continue;

                        var source = snapshot.Property;
                        if (HasPropertyValueChanged(source, snapshot))
                            SetPropertyValue(source, snapshot);
                        else continue;

                        var parameters = method.GetParameters();
                        if (parameters.Length == 0)
                            method.Invoke(InspectorHook.Target, null);
                        else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                            method.Invoke(InspectorHook.Target, new object[] { snapshot.Name });
                    }
                }
        }

        private static bool HasPropertyValueChanged(SerializedProperty source, PropertySnapshot snapshot) =>
            !snapshot.Value.Equals(GetPropertyValue(source));

        private static void SetPropertyValue(SerializedProperty source, PropertySnapshot snapshot) =>
            snapshot.Value = GetPropertyValue(source);

        private static object GetPropertyValue(SerializedProperty property) =>
            property.propertyType switch
            {
                SerializedPropertyType.Integer => property.intValue,
                SerializedPropertyType.Boolean => property.boolValue,
                SerializedPropertyType.Float => property.floatValue,
                SerializedPropertyType.String => property.stringValue,
                SerializedPropertyType.Color => property.colorValue,
                SerializedPropertyType.ObjectReference => property.objectReferenceValue,
                SerializedPropertyType.LayerMask => property.intValue,
                SerializedPropertyType.Enum => property.enumValueIndex,
                SerializedPropertyType.Vector2 => property.vector2Value,
                SerializedPropertyType.Vector3 => property.vector3Value,
                SerializedPropertyType.Vector4 => property.vector4Value,
                SerializedPropertyType.Rect => property.rectValue,
                SerializedPropertyType.ArraySize => property.arraySize,
                SerializedPropertyType.Character => (char)property.intValue,
                SerializedPropertyType.AnimationCurve => property.animationCurveValue,
                SerializedPropertyType.Bounds => property.boundsValue,
                SerializedPropertyType.Quaternion => property.quaternionValue,
                SerializedPropertyType.ExposedReference => property.exposedReferenceValue,
                SerializedPropertyType.Vector2Int => property.vector2IntValue,
                SerializedPropertyType.Vector3Int => property.vector3IntValue,
                SerializedPropertyType.RectInt => property.rectIntValue,
                SerializedPropertyType.BoundsInt => property.boundsIntValue,
                SerializedPropertyType.ManagedReference => property.managedReferenceValue,
                _ => null
            };
    }
}
#endif