using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace MemberForwarding
{
    public class VariableInfo
    {
        private static Dictionary<string, VariableInfo> VariableCache = new Dictionary<string, VariableInfo>();
        
        public readonly FieldInfo Field;
        public readonly PropertyInfo Property;

        public Type VariableType => Field?.FieldType ?? Property?.PropertyType;

        public object GetValue(object instance) => Field?.GetValue(instance) ?? Property?.GetValue(instance, null);

        public void SetValue(object instance, object value)
        {
            if(Field != null)
                Field.SetValue(instance, value);
            else if(Property != null)
                Property.SetValue(instance, value, null);
        }
        public VariableInfo(Type type, string name)
        {
            string key = $"{type.FullName}.{name}";
            if (VariableCache.ContainsKey(key))
            {
                VariableInfo variable = VariableCache[key];
                Field = variable.Field;
                Property = variable.Property;
            }
            else
            {
                Field = type.GetField(name, AccessTools.all);
                Property = type.GetProperty(name, AccessTools.all);
                if (Field == null && Property == null)
                    throw new MissingMemberException(type.Name, name);
                VariableCache.Add(key, this);
            }
        }
    }
}