using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace MemberForwarding
{
    internal class VariableInfo
    {
        private static Dictionary<string, VariableInfo> VariableCache = new Dictionary<string, VariableInfo>();
        
        private readonly FieldInfo Field;
        private readonly PropertyInfo Property;

        internal Type VariableType => Field?.FieldType ?? Property?.PropertyType;

        internal object GetValue(object instance) => Field?.GetValue(instance) ?? Property?.GetValue(instance, null);

        internal void SetValue(object instance, object value)
        {
            if(Field != null)
                Field.SetValue(instance, value);
            else if(Property != null)
                Property.SetValue(instance, value, null);
        }
        internal VariableInfo(Type type, string name, bool Static = false)
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
                    throw new MissingMemberException($"Variable '{type.Name}.{name}' not found.");
                if (Static && ((Field != null && !Field.IsStatic) ||
                               (Property != null && !Property.GetAccessors(true)[0].IsStatic)))
                    throw new MissingMemberException($"Static variable '{type.Name}.{name}' not found.");
                VariableCache.Add(key, this);
            }
        }
    }
}