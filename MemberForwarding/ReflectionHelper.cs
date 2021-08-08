using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace MemberForwarding
{
    public static class ReflectionHelper
    {
        private static Dictionary<string, Type> FoundTypes = new Dictionary<string, Type>();
        
        public static Type FindType(string fullName, string assemblyName = null)
        {
            string key = $"{assemblyName}:{fullName}";
            if (FoundTypes.ContainsKey(key))
                return FoundTypes[key];
            Type type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetSafeTypes()).FirstOrDefault(t => t.FullName != null && t.FullName.Equals(fullName) && (String.IsNullOrEmpty(assemblyName) || t.Assembly.GetName().Name.Equals(assemblyName)));
            key = $"{type.Assembly.GetName().Name}:{fullName}";
            if(type != null && !FoundTypes.ContainsKey(key))
                FoundTypes.Add(key, type);
            return type;
        }
        
        public static IEnumerable<Type> GetSafeTypes(this Assembly assembly)
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
    }
}