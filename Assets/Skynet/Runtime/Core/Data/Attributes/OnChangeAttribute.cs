using System;

namespace Skynet.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class OnChangeAttribute : Attribute
    {
        public string FieldName { get; }
        public OnChangeAttribute(string fieldName) => FieldName = fieldName;
    }
}