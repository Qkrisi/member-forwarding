using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace MemberForwarding
{
    internal static class ReflectionHelper
    {
        private static Dictionary<string, Type> FoundTypes = new Dictionary<string, Type>();
        
        internal static Type FindType(string fullName, string assemblyName = null)
        {
            string key = $"{assemblyName}:{fullName}";
            if (FoundTypes.ContainsKey(key))
                return FoundTypes[key];
            Type type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetSafeTypes()).FirstOrDefault(t => t.FullName != null && t.FullName.Equals(fullName) && (String.IsNullOrEmpty(assemblyName) || t.Assembly.GetName().Name.Equals(assemblyName)));
            if (type == null)
                throw new TypeLoadException($"Type '{fullName}' could not be found");
            key = $"{type.Assembly.GetName().Name}:{fullName}";
            if(type != null && !FoundTypes.ContainsKey(key))
                FoundTypes.Add(key, type);
            return type;
        }
        
        internal static IEnumerable<Type> GetSafeTypes(this Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(x => x != null);
            }
            catch (Exception)
            {
                return new List<Type>();
            }
        }
        
        internal static bool IsStatic(this PropertyInfo property) => property.GetAccessors(true)[0].IsStatic;
    }
}