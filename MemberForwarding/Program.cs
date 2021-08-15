using System;
using System.Runtime.CompilerServices;

namespace MemberForwarding
{
    class A {}

    class B : A
    {
        private int ForwardField = 1;

        private void ForwardMethod()
        {
            Console.WriteLine("Called forwarded method");
        }
    }
    class C : B {}
    
    internal class Program
    {
        private static A Instance;

        [MemberForward(typeof(B), "ForwardMethod")]
        [Debug]
        [ObjectReference(typeof(Program), "Instance")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchMethod() { }

        [MemberForward(typeof(B), "ForwardField")]
        [ObjectReference(typeof(Program), "Instance")]
        private static int PatchProperty
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get;
        }

        public static void Main(string[] args)
        {
            Instance = new B();
            MemberForwardControls.ForwardAll("demo");
            PatchMethod();
        }
    }
}