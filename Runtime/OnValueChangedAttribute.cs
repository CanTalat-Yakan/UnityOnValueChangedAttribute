using System;
using UnityEngine;

namespace UnityEssentials
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class OnValueChangedAttribute : PropertyAttribute
    {
        public readonly string[] FieldNames;

        public OnValueChangedAttribute(params string[] fieldNames) =>
            FieldNames = fieldNames;
    }

}
