using System;

namespace MemberForwarding
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
    public class ObjectReferenceAttribute : MemberSelectAttribute
    {
        internal readonly VariableInfo Variable;
        
        /// <summary>
        /// Set the object reference for a forwarded member
        /// </summary>
        /// <param name="_type">Type to reference the member of</param>
        /// <param name="name">Name of the member to reference</param>
        public ObjectReferenceAttribute(Type _type, string name) :
            base(_type, name)
        {
            Variable = new VariableInfo(type, Name, true);
        }

        /// <summary>
        /// Set the object reference for a forwarded member
        /// </summary>
        /// <param name="FullTypeName">Full name of the type to reference the member of (Namespace.TypeName)</param>
        /// <param name="name">Name of the member to reference</param>
        /// <param name="AssemblyName">Name of the assembly to search the type in (leave empty or null to search in any assembly)</param>
        public ObjectReferenceAttribute(string FullTypeName, string name, string AssemblyName = null) :
            this(ReflectionHelper.FindType(FullTypeName, AssemblyName), name)
        {
        }
    }
}