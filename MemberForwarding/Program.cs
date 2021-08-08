using System;
using System.Runtime.CompilerServices;

namespace MemberForwarding
{
    internal class Program
    {
        private static bool ForwardMethod(string str)
        {
            Console.WriteLine("Called forwarded method");
            return str == "yes";
        }
        
        [MemberForward("MemberForwarding.Program", "ForwardMethod")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PatchMethod(string arg) => default;

        private static int ForwardField = 1;

        [MemberForward("MemberForwarding.Program", "ForwardField")]
        private static int PatchProperty
        {
            [MethodImpl(MethodImplOptions.NoInlining)] get; 
            [MethodImpl(MethodImplOptions.NoInlining)] set;
        }
        
        

        public static void Main(string[] args)
        {
            //MemberForwardAttribute.DebugMode = true;
            MemberForwardAttribute.ForwardAll("demo");
            Console.WriteLine(PatchMethod("yes"));
            Console.WriteLine(PatchProperty);
            PatchProperty = 2;
            Console.WriteLine(ForwardField);
        }
    }
}