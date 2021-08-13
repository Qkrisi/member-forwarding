using System;
using System.Runtime.CompilerServices;

namespace MemberForwarding
{
    internal class Program
    {
        private bool ForwardMethod(out string str)
        {
            Console.WriteLine($"Called forwarded method: {str_inst}");
            str = "yes";
            return false;
        }

        private static Program Instance;

        private string str_inst;

        [MemberForward("MemberForwarding.Program", "ForwardMethod")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern bool PatchMethod(object __instance, out string arg);

        private int ForwardField = 1;

        [MemberForward("MemberForwarding.Program", "ForwardField")]
        [ObjectReference(typeof(Program), "Instance")]
        private static int PatchProperty
        {
            [MethodImpl(MethodImplOptions.NoInlining)] get; 
            [MethodImpl(MethodImplOptions.NoInlining)] set;
        }
        
        

        public static void Main(string[] args)
        {
            Instance = new Program
            {
                str_inst =  "d"
            }; 
            //MemberForwardAttribute.DebugMode = true;
            MemberForwardControls.ForwardAll("demo");
            Console.WriteLine(PatchMethod(Instance, out string nice));
            Console.WriteLine(nice);
            Console.WriteLine(PatchProperty);
            PatchProperty = 2;
            Console.WriteLine(PatchProperty);
            Console.WriteLine(Instance.ForwardField);
        }
    }
}