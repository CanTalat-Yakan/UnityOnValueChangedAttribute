#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    /// <summary>
    /// Provides functionality to monitor and respond to changes in serialized property values within the Unity Editor,
    /// invoking methods annotated with the <see cref="OnValueChangedAttribute"/>.
    /// </summary>
    /// <remarks>This class integrates with the Unity Editor's <c>InspectorHook</c> system to track changes in
    /// serialized properties and automatically invoke methods when specified properties change. It is primarily
    /// intended for use in editor scripting scenarios where dynamic responses to property changes are required.  The
    /// class initializes itself on load and processes property changes during the post-processing phase of the editor's
    /// update cycle. Methods annotated with <see cref="OnValueChangedAttribute"/> are monitored, and their invocation
    /// is triggered when the associated properties change.</remarks>
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
                Value = InspectorHookUtilities.GetPropertyValue(property);
            }
        }

        private static List<MethodInfo> s_monitoredMethods = new();
        private static List<PropertySnapshot> s_monitoredProperties = new();

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            InspectorHook.AddPreProcess(OnPreProcess);
            InspectorHook.AddPostProcess(OnPostProcess);
        }

        public static void OnPreProcess()
        {
            InspectorHook.GetAllProperties(out var allProperties);
            InspectorHook.GetAllMethods(out var allMethods);

            foreach (var method in allMethods)
            {
                if (!InspectorHookUtilities.TryGetAttribute<OnValueChangedAttribute>(method, out var attribute))
                    continue;

                if (!s_monitoredMethods.Contains(method))
                    s_monitoredMethods.Add(method);

                foreach (var property in allProperties)
                {
                    if (!attribute.FieldNames.Any(fieldName => fieldName == property.name))
                        continue;

                    bool alreadyAdded = s_monitoredProperties.Any(p => p.Property.serializedObject == property.serializedObject && p.Name == property.name);
                    if (!alreadyAdded)
                        s_monitoredProperties.Add(new PropertySnapshot(property));
                }
            }
        }

        public static void OnPostProcess()
        {
            foreach (var snapshot in s_monitoredProperties)
                foreach (var method in s_monitoredMethods)
                {
                    InspectorHookUtilities.TryGetAttribute<OnValueChangedAttribute>(method, out var attribute);

                    if (!attribute.FieldNames.Any(fieldName => fieldName == snapshot.Name))
                        continue;

                    if (HasPropertyValueChanged(snapshot))
                        SetPropertyValue(snapshot);
                    else continue;

                    var parameters = method.GetParameters();
                    var target = method.IsStatic ? null : snapshot.Property.serializedObject.targetObject;
                    if (parameters.Length == 0)
                        method.Invoke(target, null);
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                        method.Invoke(target, new object[] { snapshot.Name });
                }
        }

        private static bool HasPropertyValueChanged(PropertySnapshot snapshot) =>
            !snapshot.Value.Equals(InspectorHookUtilities.GetPropertyValue(snapshot.Property));

        private static void SetPropertyValue(PropertySnapshot snapshot) =>
            snapshot.Value = InspectorHookUtilities.GetPropertyValue(snapshot.Property);
    }
}
#endif