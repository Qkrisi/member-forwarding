using System;
using System.Diagnostics;
using System.Linq;

namespace MemberForwarding
{
    public static class MemberForwardControls
    {

        private static void TryExecuteForwards(Action action)
        {
            try
            {
                action();
            }
            catch (TypeLoadException e)
            {
                throw e.Message.Contains($"Could not load type '{nameof(MemberForwarding)}.{nameof(MemberForwardAttribute)}'") ? new DllNotFoundException(
                    "Failed to load member forwarding. Please make sure Harmony is installed! (https://github.com/pardeike/Harmony/releases)") : e;
            }
        }

        public static void ForwardTypes(string HarmonyID, params Type[] types) => TryExecuteForwards(() => MemberForwardAttribute.ForwardTypes(HarmonyID, types));

        public static void ForwardAll(string HarmonyID) =>
            TryExecuteForwards(() =>
                ForwardTypes(HarmonyID,
                    new StackTrace().GetFrame(3).GetMethod().ReflectedType.Assembly.GetSafeTypes().ToArray()));
    }
}