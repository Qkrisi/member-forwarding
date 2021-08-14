using System;

namespace MemberForwarding
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    internal class DebugAttribute : Attribute
    {
    }
}