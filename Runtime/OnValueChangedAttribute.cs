using System;
using UnityEngine;

namespace UnityEssentials
{
    /// <summary>
    /// Specifies that a method should be invoked when the value of one or more specified fields changes.
    /// </summary>
    /// <remarks>This attribute can be applied to methods to automatically trigger their execution when the
    /// value of  the specified fields changes. The fields are identified by their names, which are passed as parameters
    /// to the attribute. This is particularly useful for implementing reactive behavior in classes.</remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class OnValueChangedAttribute : PropertyAttribute
    {
        public readonly string[] ReferenceNames;

        public OnValueChangedAttribute(params string[] fieldNames) =>
            ReferenceNames = fieldNames;
    }

}
