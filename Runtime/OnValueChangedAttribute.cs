using System;
using UnityEngine;

namespace UnityEssentials
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class OnValueChangedAttribute : PropertyAttribute
    {
        public readonly string FieldName;

        public OnValueChangedAttribute(string fieldName) =>
            FieldName = fieldName;
    }

}
