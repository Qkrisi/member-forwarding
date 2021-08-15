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

        /// <summary>
        /// Forward members of the specified types
        /// </summary>
        /// <param name="HarmonyID">Harmony ID used for patching</param>
        /// <param name="types">Types to forward the members of</param>
        public static void ForwardTypes(string HarmonyID, params Type[] types) => TryExecuteForwards(() => MemberForwardAttribute.ForwardTypes(HarmonyID, types));

        /// <summary>
        /// Forward members of all of the types of the executing assembly
        /// </summary>
        /// <param name="HarmonyID">Harmony ID used for patching</param>
        public static void ForwardAll(string HarmonyID) =>
            TryExecuteForwards(() =>
                ForwardTypes(HarmonyID,
                    new StackTrace().GetFrame(3).GetMethod().ReflectedType.Assembly.GetSafeTypes().ToArray()));
    }
}