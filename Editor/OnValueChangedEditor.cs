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
    [DefaultExecutionOrder(-1999)]
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

        private static Dictionary<Object, List<MethodInfo>> s_monitoredMethodsDictionary = new();
        private static Dictionary<Object, List<PropertySnapshot>> s_monitoredPropertiesDictionary = new();
        private static GameObject s_targetGameObject;

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            InspectorHook.AddInitialization(OnInitialization);
            InspectorHook.AddPostProcess(OnPostProcess);

            Selection.selectionChanged += () =>
            {
                if (Selection.activeGameObject != s_targetGameObject)
                {
                    s_targetGameObject = Selection.activeGameObject;
                    s_monitoredMethodsDictionary.Clear();
                    s_monitoredPropertiesDictionary.Clear();
                }
            };
        }

        public static void OnInitialization()
        {
            InspectorHook.GetAllProperties(out var allProperties);

            foreach (var property in allProperties)
            {
                var targetObject = InspectorHookUtilities.GetTargetObjectOfProperty(property);
                if (!s_monitoredMethodsDictionary.ContainsKey(targetObject))
                    s_monitoredMethodsDictionary.Add(targetObject, new List<MethodInfo>());
            }

            foreach (var targetObject in s_monitoredMethodsDictionary.Keys.ToArray())
            {
                InspectorHook.GetAllMethods(targetObject.GetType(), out var alMethods);
                s_monitoredMethodsDictionary[targetObject] = alMethods
                    .Where(method => InspectorHookUtilities.HasAttribute<OnValueChangedAttribute>(method))
                    .ToList();
            }

            foreach (var targetObject in s_monitoredMethodsDictionary.Keys)
                foreach (var method in s_monitoredMethodsDictionary[targetObject])
                {
                    InspectorHookUtilities.TryGetAttributes<OnValueChangedAttribute>(method, out var attributes);
                    foreach (var property in allProperties)
                    {
                        foreach (var attribute in attributes)
                            if (!attribute.ReferenceNames.Any(referenceName => referenceName == property.name))
                                continue;

                        var propertyTargetObject = InspectorHookUtilities.GetTargetObjectOfProperty(property);
                        if(propertyTargetObject != targetObject)
                            continue;

                        if (!s_monitoredPropertiesDictionary.ContainsKey(targetObject))
                            s_monitoredPropertiesDictionary.Add(targetObject, new List<PropertySnapshot>());
                        s_monitoredPropertiesDictionary[targetObject].Add(new PropertySnapshot(property));
                    }
                }
        }

        public static void OnPostProcess()
        {
            var monitoredMethods = s_monitoredMethodsDictionary[InspectorHook.Target];
            var monitoredProperties = s_monitoredPropertiesDictionary[InspectorHook.Target];
            foreach (var snapshot in monitoredProperties)
            {
                foreach (var method in monitoredMethods)
                {
                    InspectorHookUtilities.TryGetAttribute<OnValueChangedAttribute>(method, out var attribute);

                    if (!attribute.ReferenceNames.Any(referenceName => referenceName == snapshot.Name))
                        continue;

                    if (HasPropertyValueChanged(snapshot))
                        SetPropertyValue(snapshot);
                    else continue;

                    var target = method.IsStatic ? null : InspectorHookUtilities.GetTargetObjectOfProperty(snapshot.Property);

                    var parameters = method.GetParameters();
                    if (parameters.Length == 0)
                        method.Invoke(target, null);
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                        method.Invoke(target, new object[] { snapshot.Name });
                }
            }
        }

        private static bool HasPropertyValueChanged(PropertySnapshot snapshot) =>
            !snapshot.Value?.Equals(InspectorHookUtilities.GetPropertyValue(snapshot?.Property)) ?? true;

        private static void SetPropertyValue(PropertySnapshot snapshot) =>
            snapshot.Value = InspectorHookUtilities.GetPropertyValue(snapshot?.Property);

    }
}
#endif