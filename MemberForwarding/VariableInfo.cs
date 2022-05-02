using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace MemberForwarding
{
    internal class MissingVariableException : MissingMemberException
    {
        internal readonly bool Rethrow;
        
        internal MissingVariableException(string message, bool rethrow = false) : base(message)
        {
            Rethrow = rethrow;
        }
    }
    
    internal class VariableInfo
    {
        private static Dictionary<string, VariableInfo> VariableCache = new Dictionary<string, VariableInfo>();
        
        private readonly FieldInfo Field;
        private readonly PropertyInfo Property;

        private readonly MethodInfo Getter;
        private readonly MethodInfo Setter;

        internal Type VariableType => Field?.FieldType ?? Property.PropertyType;

        internal bool IsStatic => Field?.IsStatic ?? Property.IsStatic();
        internal bool Gettable => Field != null || Getter != null;
        internal bool Settable => Field != null || Setter != null;

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
                Getter = variable.Getter;
                Setter = variable.Setter;
            }
            else
            {
                Field = type.GetField(name, AccessTools.all);
                Property = type.GetProperty(name, AccessTools.all);
                if (Field == null && Property == null)
                    throw new MissingVariableException($"Variable '{type.Name}.{name}' not found.");
                if (Static && !IsStatic)
                    throw new MissingVariableException($"Static variable '{type.Name}.{name}' not found.");
                if (Property != null)
                {
                    Getter = Property.GetGetMethod(true);
                    Setter = Property.GetSetMethod(true);
                }
                VariableCache.Add(key, this);
            }
        }
    }
}