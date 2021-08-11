using System;

namespace MemberForwarding
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public class MemberSelectAttribute : Attribute
    {
        public readonly Type type;
        public readonly string Name;

        public MemberSelectAttribute(Type _type, string name)
        {
            name = name.Trim();
            if (_type == null)
                throw new ArgumentNullException("_type");
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");
            type = _type;
            Name = name;
        }
    }
}