using System;
using System.Diagnostics;
using System.Linq;

namespace MemberForwarding
{
    public static class MemberForwardControls
    {
        public static void ForwardTypes(string HarmonyID, params Type[] types) =>
            MemberForwardAttribute.ForwardTypes(HarmonyID, types);

        public static void ForwardAll(string HarmonyID) => ForwardTypes(HarmonyID,
            new StackTrace().GetFrame(1).GetMethod().ReflectedType.Assembly.GetSafeTypes().ToArray());
    }
}