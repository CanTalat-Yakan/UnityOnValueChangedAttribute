#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

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
        /// <summary>
        /// Represents a snapshot of a serialized property, capturing its name, value, and associated metadata.
        /// </summary>
        /// <remarks>This class is typically used to store and compare the state of a serialized property
        /// at a specific point in time.</remarks>
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

        private static List<MethodInfo> s_monitoredMethods;
        private static List<PropertySnapshot> s_monitoredProperties;

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            InspectorHook.AddInitialization(OnInitialization);
            InspectorHook.AddPostProcess(OnPostProcess);
        }

        /// <summary>
        /// Initializes and configures the monitoring of methods and properties that are annotated with the <see
        /// cref="OnValueChangedAttribute"/>.
        /// </summary>
        /// <remarks>This method scans all available methods and properties, identifies those with the
        /// <see cref="OnValueChangedAttribute"/>, and establishes a mapping between the methods and the properties they
        /// monitor. The identified methods and properties are stored for later use.</remarks>
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

        /// <summary>
        /// Executes post-processing logic for monitored properties and methods.
        /// </summary>
        /// <remarks>This method iterates through a collection of monitored properties and methods,
        /// checking for changes in property values. If a property value has changed, it updates the property and
        /// invokes associated methods.  Methods can be invoked with no parameters or with a single string parameter
        /// representing the property name.</remarks>
        public static void OnPostProcess()
        {
            foreach (PropertySnapshot snapshot in s_monitoredProperties)
                foreach (var method in s_monitoredMethods)
                {
                    InspectorHookUtilities.TryGetAttribute<OnValueChangedAttribute>(method, out var attribute);

                    var foundMatch = false;
                    foreach (var attributeFieldName in attribute.FieldNames)
                        if (attributeFieldName == snapshot.Name)
                            foundMatch = true;
                    if(!foundMatch)
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

        private static bool HasPropertyValueChanged(SerializedProperty source, PropertySnapshot snapshot) =>
            !snapshot.Value.Equals(InspectorHookUtilities.GetPropertyValue(source));

        private static void SetPropertyValue(SerializedProperty source, PropertySnapshot snapshot) =>
            snapshot.Value = InspectorHookUtilities.GetPropertyValue(source);
    }
}
#endif