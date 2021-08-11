using System;

namespace MemberForwarding
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
    public class ObjectReferenceAttribute : MemberSelectAttribute
    {
        public readonly VariableInfo Variable;
        
        public ObjectReferenceAttribute(Type _type, string name) :
            base(_type, name)
        {
            Variable = new VariableInfo(type, Name, true);
        }

        public ObjectReferenceAttribute(string FullTypeName, string name, string AssemblyName = null) :
            this(ReflectionHelper.FindType(FullTypeName, AssemblyName), name)
        {
        }
    }
}