#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace UnityEssentials
{
    public static class OnValueChangedEditor
    {
        private static List<MethodInfo> s_monitoredMethods;
        private static List<SerializedProperty> s_monitoredProperties;

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
                        if (attribute.FieldNames.Equals(property.name))
                            s_monitoredProperties.Add(property);
                }
        }

        public static void OnPostProcess()
        {
            foreach (var field in s_monitoredProperties)
                foreach (var method in s_monitoredMethods)
                {
                    InspectorHookUtilities.TryGetAttributes<OnValueChangedAttribute>(method, out var attributes);
                    foreach (var attribute in attributes)
                    {
                        foreach (var attributeFieldName in attribute.FieldNames)
                            if (attributeFieldName != field.name)
                                continue;

                        var parameters = method.GetParameters();
                        if (parameters.Length == 0)
                            method.Invoke(InspectorHook.Target, null);
                        else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                            method.Invoke(InspectorHook.Target, new object[] { field.name });
                    }
                }
        }
    }
}
#endif