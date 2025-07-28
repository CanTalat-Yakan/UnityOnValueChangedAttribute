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

        private static Dictionary<int, List<MethodInfo>> s_monitoredMethodDictionary = new();
        private static Dictionary<int, List<PropertySnapshot>> s_monitoredPropertieDictionary = new();
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
                    s_monitoredMethodDictionary.Clear();
                    s_monitoredPropertieDictionary.Clear();
                }
            };
        }

        public static void OnInitialization()
        {
            int targetId = InspectorHook.Target.GetInstanceID();
            if (s_monitoredMethodDictionary.ContainsKey(targetId))
                return;

            InspectorHook.GetAllProperties(out var allProperties);
            InspectorHook.GetAllMethods(out var allMethods);

            var monitoredMethods = new List<MethodInfo>();
            var monitoredProperties = new List<PropertySnapshot>();

            foreach (var method in allMethods)
            {
                if (!InspectorHookUtilities.TryGetAttribute<OnValueChangedAttribute>(method, out var attribute))
                    continue;

                if (!monitoredMethods.Contains(method))
                    monitoredMethods.Add(method);

                foreach (var property in allProperties)
                {
                    if (!attribute.ReferenceNames.Any(referenceName => referenceName == property.name))
                        continue;

                    var alreadyAdded = monitoredProperties.Any(snapshot =>
                        snapshot.Property.serializedObject == property.serializedObject &&
                        snapshot.Name == property.name);

                    if (!alreadyAdded)
                    {
                        var snapshot = new PropertySnapshot(property);
                        monitoredProperties.Add(snapshot);
                    }
                }
            }

            s_monitoredMethodDictionary[targetId] = monitoredMethods;
            s_monitoredPropertieDictionary[targetId] = monitoredProperties;
        }

        public static void OnPostProcess()
        {
            int targetId = InspectorHook.Target.GetInstanceID();
            var monitoredMethods = s_monitoredMethodDictionary[targetId];
            var monitoredProperties = s_monitoredPropertieDictionary[targetId];

            foreach (var snapshot in monitoredProperties)
                foreach (var method in monitoredMethods)
                {
                    InspectorHookUtilities.TryGetAttribute<OnValueChangedAttribute>(method, out var attribute);

                    if (!attribute.ReferenceNames.Any(refrenceName => refrenceName == snapshot.Name))
                        continue;

                    if (HasPropertyValueChanged(snapshot))
                        SetPropertyValue(snapshot);
                    else continue;

                    var parameters = method.GetParameters();
                    var target = method.IsStatic ? null : InspectorHook.Target;
                    if (parameters.Length == 0)
                        method.Invoke(target, null);
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                        method.Invoke(target, new object[] { snapshot.Name });
                }
        }

        private static bool HasPropertyValueChanged(PropertySnapshot snapshot) =>
            !snapshot.Value?.Equals(InspectorHookUtilities.GetPropertyValue(snapshot?.Property)) ?? true;

        private static void SetPropertyValue(PropertySnapshot snapshot) =>
            snapshot.Value = InspectorHookUtilities.GetPropertyValue(snapshot?.Property);

    }
}
#endif