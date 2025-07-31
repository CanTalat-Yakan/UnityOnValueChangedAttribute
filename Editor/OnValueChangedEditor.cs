#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
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

        private static readonly Dictionary<Object, List<PropertySnapshot>> s_monitoredPropertiesDictionary = new();
        private static readonly Dictionary<Object, List<MethodInfo>> s_monitoredMethodsDictionary = new();
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
                    s_monitoredPropertiesDictionary.Clear();
                    s_monitoredMethodsDictionary.Clear();
                }
            };
        }

        public static void OnInitialization()
        {
            var propertiesByTarget = new Dictionary<Object, List<SerializedProperty>>();
            InspectorHook.GetAllProperties(out var allProperties);
            foreach (var property in allProperties)
            {
                var target = InspectorHookUtilities.GetTargetObjectOfProperty(property);
                if (!propertiesByTarget.TryGetValue(target, out var list))
                    propertiesByTarget[target] = list = new List<SerializedProperty>();
                list.Add(property);
            }

            foreach (var kvp in propertiesByTarget)
            {
                var target = kvp.Key;
                var properties = kvp.Value;

                InspectorHook.GetAllMethods(target.GetType(), out var allMethods);
                var monitoredMethods = allMethods
                    .Where(method => InspectorHookUtilities.HasAttribute<OnValueChangedAttribute>(method))
                    .ToList();

                if (monitoredMethods.Count > 0)
                    s_monitoredMethodsDictionary[target] = monitoredMethods;
                else continue;

                var monitoredProperties = new List<PropertySnapshot>();
                foreach (var method in monitoredMethods)
                {
                    InspectorHookUtilities.TryGetAttributes<OnValueChangedAttribute>(method, out var attributes);
                    var referenceNames = attributes.SelectMany(attribute => attribute.ReferenceNames).ToHashSet();

                    foreach (var property in properties)
                        if (referenceNames.Contains(property.name))
                            monitoredProperties.Add(new PropertySnapshot(property));
                }

                if (monitoredProperties.Count > 0)
                    s_monitoredPropertiesDictionary[target] = monitoredProperties;
            }
        }

        public static void OnPostProcess()
        {
            if (!s_monitoredPropertiesDictionary.TryGetValue(InspectorHook.Target, out var snapshots) ||
                !s_monitoredMethodsDictionary.TryGetValue(InspectorHook.Target, out var methods))
                return;

            foreach (var snapshot in snapshots)
                foreach (var method in methods)
                {
                    InspectorHookUtilities.TryGetAttribute<OnValueChangedAttribute>(method, out var attribute);
                    if (!attribute.ReferenceNames.Contains(snapshot.Name))
                        continue;

                    if (!Equals(snapshot.Value, InspectorHookUtilities.GetPropertyValue(snapshot.Property)))
                    {
                        snapshot.Value = InspectorHookUtilities.GetPropertyValue(snapshot.Property);

                        var target = method.IsStatic ? null : InspectorHookUtilities.GetTargetObjectOfProperty(snapshot.Property);
                        var parameters = method.GetParameters();
                        if (parameters.Length == 0)
                            method.Invoke(target, null);
                        else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                            method.Invoke(target, new object[] { snapshot.Name });
                    }
                }
        }
    }
}
#endif