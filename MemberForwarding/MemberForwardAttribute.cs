using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace MemberForwarding
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
    public class MemberForwardAttribute : MemberSelectAttribute
    {
        private Type[] Overloads;

        private MethodInfo OriginalMethod => type.GetMethod(Name, AccessTools.all, Type.DefaultBinder, Overloads, null);
        private VariableInfo OriginalVariable => new VariableInfo(type, Name);

        private static Dictionary<string, Harmony> HarmonyInstances = new Dictionary<string, Harmony>();

        private static Dictionary<string, VariableInfo> AccessorVariables =
            new Dictionary<string, VariableInfo>();

        private static Dictionary<string, ObjectReferenceAttribute> ObjectReferences =
            new Dictionary<string, ObjectReferenceAttribute>();
        
#pragma warning disable 649
        internal static bool DebugMode;
#pragma warning restore 649

        private ObjectReferenceAttribute ObjectReference;

        private bool Forwarded;

        static CodeInstruction CreateCodeInstruction(OpCode code, object operand = null)
        {
            var instruction = new CodeInstruction(code, operand);
            if(DebugMode)
                Console.WriteLine(instruction.ToString());
            return instruction;
        }

        private void UpdateParameters(MethodBase method, out bool IsStatic)
        {
            var parameters = method.GetParameters();
            if (ObjectReference != null)
                IsStatic = false;
            else if (parameters.Length > 0 && parameters[0].ParameterType.IsAssignableFrom(type) &&
                    parameters[0].Name == "__instance")
            {
                IsStatic = false;
                parameters = parameters.Skip(1).ToArray();
            }
            else IsStatic = true;
            Overloads = parameters.Select(p => p.ParameterType).ToArray();
        }

        private Harmony GetHarmonyInstance(string HarmonyID)
        {
            if(!HarmonyInstances.ContainsKey(HarmonyID))
                HarmonyInstances.Add(HarmonyID, new Harmony(HarmonyID));
            return HarmonyInstances[HarmonyID];
        }

        private void Patch(string HarmonyID, MethodInfo method)
        {
            if (!method.IsStatic)
                throw new MethodAccessException("Method to patch should be static!");
            UpdateParameters(method, out bool IsStatic);
            MethodInfo MemberMethod = OriginalMethod;
            if (MemberMethod == null)
                throw new MissingMethodException(type.Name, Name);
            if (MemberMethod.IsStatic != IsStatic)
                throw new MissingMethodException(String.Format(
                    "Could not find a {0}static method that matches the parameters", !IsStatic ? "non-" : ""));
            if (!method.ReturnType.IsAssignableFrom(MemberMethod.ReturnType))
                throw new MethodAccessException("Return type mismatch");
            GetHarmonyInstance(HarmonyID).Patch(method,
                transpiler: new HarmonyMethod(typeof(MemberForwardAttribute), "MethodTranspiler"),
                finalizer: new HarmonyMethod(typeof(MemberForwardAttribute), "Finalizer"));
        }

        private void Patch(string HarmonyID, MethodInfo Getter, MethodInfo Setter, Type DeclaringType)
        {
            if((Getter!=null && !Getter.IsStatic) || (Setter!=null && !Setter.IsStatic))
                throw new MethodAccessException("Method to patch should be static!");

            Harmony HarmonyInstance = GetHarmonyInstance(HarmonyID);
            var CurrentVariable = OriginalVariable;

            string key1 = $"{DeclaringType.Assembly.GetName().Name}:{DeclaringType.FullName}.";

            var Finalizer = new HarmonyMethod(typeof(MemberForwardAttribute), "Finalizer");

            if (Getter != null)
            {
                string key = $"{key1}{Getter.Name}";
                if(!AccessorVariables.ContainsKey(key))
                    AccessorVariables.Add(key, CurrentVariable);
                HarmonyInstance.Patch(Getter,
                    transpiler: new HarmonyMethod(typeof(MemberForwardAttribute), "GetterTranspiler"),
                    finalizer: Finalizer);
            }

            if (Setter != null)
            {
                string key = $"{key1}{Setter.Name}";
                if(!AccessorVariables.ContainsKey(key))
                    AccessorVariables.Add(key, CurrentVariable);
                HarmonyInstance.Patch(Setter,
                    transpiler: new HarmonyMethod(typeof(MemberForwardAttribute), "SetterTranspiler"),
                    finalizer: Finalizer);
            }
        }

        static MemberForwardAttribute GetAttribute(MethodBase PatchMethod) => PatchMethod.GetCustomAttributes(typeof(MemberForwardAttribute), true)[0] as MemberForwardAttribute;

        static string MethodToKey(MethodBase method)
        {
            Type DeclaringType = method.DeclaringType;
            return $"{DeclaringType.Assembly.GetName().Name}:{DeclaringType.FullName}.{method.Name}";
        }

        static CodeInstruction MethodToKey(MethodBase method, out string key)
        {
            key = MethodToKey(method);
            return CreateCodeInstruction(OpCodes.Ldstr, key);
        }

        static ObjectReferenceAttribute GetReference(string key) => ObjectReferences[key];
        
        static IEnumerable<CodeInstruction> MethodTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase PatchMethod)
        {
            MemberForwardAttribute attribute = GetAttribute(PatchMethod);
            var _ObjectReferences =
                PatchMethod.GetCustomAttributes(typeof(ObjectReferenceAttribute), true);
            string key = MethodToKey(PatchMethod);
            ObjectReferenceAttribute reference = null;
            if (_ObjectReferences.Length > 0)
            {
                reference = _ObjectReferences[0] as ObjectReferenceAttribute;
                attribute.ObjectReference = reference;
                if(!ObjectReferences.ContainsKey(key))
                    ObjectReferences.Add(key, reference);
            }
            attribute.UpdateParameters(PatchMethod, out bool IsStatic);
            MethodInfo OriginalMethod = attribute.OriginalMethod;
            int i = 0;
            if (!IsStatic)
            {
                if (attribute.ObjectReference != null)
                {
                    yield return CreateCodeInstruction(OpCodes.Ldstr, key);
                    MethodInfo GetReferenceMethod = typeof(MemberForwardAttribute).GetMethod("GetReference", AccessTools.all);
                    yield return CreateCodeInstruction(OpCodes.Call, GetReferenceMethod);
                    FieldInfo VariableField = typeof(ObjectReferenceAttribute).GetField("Variable", AccessTools.all);
                    yield return CreateCodeInstruction(OpCodes.Ldfld, VariableField);
                    MethodInfo GetVariableMethod = typeof(VariableInfo).GetMethod("GetValue", AccessTools.all);
                    yield return CreateCodeInstruction(OpCodes.Ldnull);
                    yield return CreateCodeInstruction(OpCodes.Call, GetVariableMethod);
                    yield return CreateCodeInstruction(reference.Variable.VariableType.IsValueType
                        ? OpCodes.Unbox_Any
                        : OpCodes.Castclass, reference.Variable.VariableType);
                }
                else
                {
                    yield return CreateCodeInstruction(OpCodes.Ldarg_0);
                    i++;
                }
            }
            foreach (var parameter in OriginalMethod.GetParameters())
                yield return CreateCodeInstruction(OpCodes.Ldarg, i++);
            yield return CreateCodeInstruction(OpCodes.Callvirt, OriginalMethod);
            yield return CreateCodeInstruction(OpCodes.Ret);
        }
        
        static VariableInfo GetValue(string key) => AccessorVariables[key];

        static IEnumerable<CodeInstruction> GetterTranspiler(IEnumerable<CodeInstruction> instructions,
            MethodBase OriginalMethod)
        {
            yield return MethodToKey(OriginalMethod, out string key);
            MethodInfo GetValueMethod = typeof(MemberForwardAttribute).GetMethod("GetValue", AccessTools.all);
            yield return CreateCodeInstruction(OpCodes.Call, GetValueMethod);
            MethodInfo GetVariableMethod = typeof(VariableInfo).GetMethod("GetValue", AccessTools.all);
            yield return CreateCodeInstruction(OpCodes.Ldnull);
            yield return CreateCodeInstruction(OpCodes.Call, GetVariableMethod);
            Type ReturnType = AccessorVariables[key].VariableType;
            yield return CreateCodeInstruction(ReturnType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, ReturnType);
            yield return CreateCodeInstruction(OpCodes.Ret);
        }

        static IEnumerable<CodeInstruction> SetterTranspiler(IEnumerable<CodeInstruction> instructions,
            MethodBase OriginalMethod)
        {
            yield return MethodToKey(OriginalMethod, out string key);
            MethodInfo GetValueMethod = typeof(MemberForwardAttribute).GetMethod("GetValue", AccessTools.all);
            yield return CreateCodeInstruction(OpCodes.Call, GetValueMethod);
            MethodInfo SetVariableMethod = typeof(VariableInfo).GetMethod("SetValue", AccessTools.all);
            yield return CreateCodeInstruction(OpCodes.Ldnull);
            yield return CreateCodeInstruction(OpCodes.Ldarg_0);
            Type VariableType = AccessorVariables[key].VariableType;
            if (VariableType.IsValueType)
                yield return CreateCodeInstruction(OpCodes.Box, VariableType);
            else yield return CreateCodeInstruction(OpCodes.Castclass, typeof(object));
            yield return CreateCodeInstruction(OpCodes.Call, SetVariableMethod);
            yield return CreateCodeInstruction(OpCodes.Ret);
        }

        static Exception Finalizer(Exception __exception) => __exception is OutOfMemoryException ? new InvalidCastException() : __exception;

        public static void ForwardTypes(string ID, params Type[] types)
        {
            foreach (Type type in types)
            {
                foreach (var member in type.GetMembers(AccessTools.all))
                {
                    var attributes = member.GetCustomAttributes(typeof(MemberForwardAttribute), true);
                    if (attributes.Length > 0)
                    {
                        var attribute = attributes[0] as MemberForwardAttribute;
                        if (attribute.Forwarded)
                            continue;
                        var _ObjectReferences = member.GetCustomAttributes(typeof(ObjectReferenceAttribute), true);
                        if(_ObjectReferences.Length > 0)
                            attribute.ObjectReference = _ObjectReferences[0] as ObjectReferenceAttribute;
                        if(member is MethodInfo method)
                            attribute.Patch(ID, method);
                        if (member is PropertyInfo property)
                            attribute.Patch(ID, property.GetGetMethod(true), property.GetSetMethod(true),
                                property.DeclaringType);
                        attribute.Forwarded = true;
                    }
                }
                ForwardTypes(ID, type.GetNestedTypes(AccessTools.all));
            }
        }

        public static void ForwardAll(string ID)
        {
            ForwardTypes(ID, Assembly.GetExecutingAssembly().GetSafeTypes().ToArray());
        }

        public MemberForwardAttribute(Type _type, string name) :
            base(_type, name)
        {
        }

        public MemberForwardAttribute(string FullTypeName, string name, string AssemblyName = null) :
            this(ReflectionHelper.FindType(FullTypeName, AssemblyName), name)
        {
        }
    }
}