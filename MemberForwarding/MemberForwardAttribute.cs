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

        private static Dictionary<string, MemberForwardAttribute> AccessorAttributes =
            new Dictionary<string, MemberForwardAttribute>();

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

        private bool IsStatic(MethodBase method, out bool Skip)
        {
            var parameters = method.GetParameters();
            if (ObjectReference != null)
            {
                Skip = false;
                return false;
            }
            if (parameters.Length > 0 && parameters[0].ParameterType.IsAssignableFrom(type) &&
                parameters[0].Name == "__instance")
            {
                Skip = true;
                return false;
            }
            Skip = false;
            return true;
        }

        private bool IsStatic(MethodBase method) => IsStatic(method, out bool _);

        private void UpdateParameters(MethodBase method, out bool _IsStatic)
        {
            var parameters = method.GetParameters();
            _IsStatic = IsStatic(method, out bool Skip);
            if(Skip)
                parameters = parameters.Skip(1).ToArray();
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
            UpdateParameters(method, out bool _IsStatic);
            MethodInfo MemberMethod = OriginalMethod;
            if (MemberMethod == null)
            {
                
                throw new MissingMethodException(type.Name, Name);
            }
            if (MemberMethod.IsStatic != _IsStatic)
                throw new MissingMethodException(String.Format(
                    "Could not find a {0}static method that matches the parameters", !_IsStatic ? "non-" : ""));
            if (!method.ReturnType.IsAssignableFrom(MemberMethod.ReturnType))
                throw new TargetException("Return type mismatch");
            GetHarmonyInstance(HarmonyID).Patch(method,
                transpiler: new HarmonyMethod(typeof(MemberForwardAttribute), "MethodTranspiler"),
                finalizer: method.GetMethodBody() != null ? new HarmonyMethod(typeof(MemberForwardAttribute), "Finalizer") : null);
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
                if(!AccessorAttributes.ContainsKey(key))
                    AccessorAttributes.Add(key, this);
                if(ObjectReference != null && !ObjectReferences.ContainsKey(key))
                    ObjectReferences.Add(key, ObjectReference);
                HarmonyInstance.Patch(Getter,
                    transpiler: new HarmonyMethod(typeof(MemberForwardAttribute), "GetterTranspiler"),
                    finalizer: Getter.GetMethodBody() != null ? Finalizer : null);
            }

            if (Setter != null)
            {
                string key = $"{key1}{Setter.Name}";
                if(!AccessorVariables.ContainsKey(key))
                    AccessorVariables.Add(key, CurrentVariable);
                if(!AccessorAttributes.ContainsKey(key))
                    AccessorAttributes.Add(key, this);
                if(ObjectReference != null && !ObjectReferences.ContainsKey(key))
                    ObjectReferences.Add(key, ObjectReference);
                HarmonyInstance.Patch(Setter,
                    transpiler: new HarmonyMethod(typeof(MemberForwardAttribute), "SetterTranspiler"),
                    finalizer: Setter.GetMethodBody() != null ? Finalizer : null);
            }
        }

        static MemberForwardAttribute GetAttribute(MethodBase PatchMethod)
        {
            try
            {
                return PatchMethod.GetCustomAttributes(typeof(MemberForwardAttribute), true)[0] as
                    MemberForwardAttribute;
            }
            catch (IndexOutOfRangeException)
            {
                return AccessorAttributes[MethodToKey(PatchMethod)];
            }
        }

        static string MethodToKey(MethodBase method)
        {
            Type DeclaringType = method.ReflectedType;
            return $"{DeclaringType.Assembly.GetName().Name}:{DeclaringType.FullName}.{method.Name}";
        }

        static CodeInstruction MethodToKey(MethodBase method, out string key)
        {
            key = MethodToKey(method);
            return CreateCodeInstruction(OpCodes.Ldstr, key);
        }

        static CodeInstruction GetAttributes(MethodBase PatchMethod, out MemberForwardAttribute ForwardAttribute, out string key)
        {
            MemberForwardAttribute attribute = GetAttribute(PatchMethod);
            var _ObjectReferences =
                PatchMethod.GetCustomAttributes(typeof(ObjectReferenceAttribute), true);
            key = MethodToKey(PatchMethod);
            if (_ObjectReferences.Length > 0)
            {
                var reference = _ObjectReferences[0] as ObjectReferenceAttribute;
                attribute.ObjectReference = reference;
                if(!ObjectReferences.ContainsKey(key))
                    ObjectReferences.Add(key, reference);
            }
            ForwardAttribute = attribute;
            return CreateCodeInstruction(OpCodes.Ldstr, key);
        }

        static ObjectReferenceAttribute GetReference(string key) => ObjectReferences[key];

        static CodeInstruction Unbox(Type _type) =>
            CreateCodeInstruction(_type.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, _type);

        static CodeInstruction Box(Type _type) => _type.IsValueType
            ? CreateCodeInstruction(OpCodes.Box, _type)
            : CreateCodeInstruction(OpCodes.Castclass, typeof(object));

        static IEnumerable<CodeInstruction> LoadReference(ObjectReferenceAttribute ObjectReference, string key, Type ReflectedType, out int ParameterIndex, bool unbox = false)
        {
            List<CodeInstruction> codeInstructions = new List<CodeInstruction>();
            if (ObjectReference != null)
            {
                codeInstructions.Add(CreateCodeInstruction(OpCodes.Ldstr, key));
                MethodInfo GetReferenceMethod = typeof(MemberForwardAttribute).GetMethod("GetReference", AccessTools.all);
                codeInstructions.Add(CreateCodeInstruction(OpCodes.Call, GetReferenceMethod));
                FieldInfo VariableField = typeof(ObjectReferenceAttribute).GetField("Variable", AccessTools.all);
                codeInstructions.Add(CreateCodeInstruction(OpCodes.Ldfld, VariableField));
                MethodInfo GetVariableMethod = typeof(VariableInfo).GetMethod("GetValue", AccessTools.all);
                codeInstructions.Add(CreateCodeInstruction(OpCodes.Ldnull));
                codeInstructions.Add(CreateCodeInstruction(OpCodes.Call, GetVariableMethod));
                if(unbox)
                    codeInstructions.Add(Unbox(ObjectReference.Variable.VariableType));
                ParameterIndex = 0;
            }
            else
            {
                codeInstructions.Add(CreateCodeInstruction(OpCodes.Ldarg_0));
                codeInstructions.Add(unbox ? Unbox(ReflectedType) : Box(ReflectedType));
                ParameterIndex = 1;
            }
            return codeInstructions;
        }

        static IEnumerable<CodeInstruction> MethodTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase PatchMethod)
        {
            GetAttributes(PatchMethod, out MemberForwardAttribute attribute, out string key);
            attribute.UpdateParameters(PatchMethod, out bool _IsStatic);
            MethodInfo OriginalMethod = attribute.OriginalMethod;
            int i = 0;
            if (!_IsStatic)
            {
                foreach (var instruction in LoadReference(attribute.ObjectReference, key, OriginalMethod.ReflectedType,
                    out i, true))
                    yield return instruction;
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
            yield return GetAttributes(OriginalMethod, out MemberForwardAttribute attribute, out string key);
            MethodInfo GetValueMethod = typeof(MemberForwardAttribute).GetMethod("GetValue", AccessTools.all);
            yield return CreateCodeInstruction(OpCodes.Call, GetValueMethod);
            MethodInfo GetVariableMethod = typeof(VariableInfo).GetMethod("GetValue", AccessTools.all);
            if (!attribute.IsStatic(OriginalMethod))
            {
                foreach (var instruction in LoadReference(attribute.ObjectReference, key, OriginalMethod.ReflectedType,
                    out int _))
                    yield return instruction;
            }
            else yield return CreateCodeInstruction(OpCodes.Ldnull);
            yield return CreateCodeInstruction(OpCodes.Call, GetVariableMethod);
            Type ReturnType = AccessorVariables[key].VariableType;
            yield return Unbox(ReturnType);
            yield return CreateCodeInstruction(OpCodes.Ret);
        }

        static IEnumerable<CodeInstruction> SetterTranspiler(IEnumerable<CodeInstruction> instructions,
            MethodBase OriginalMethod)
        {
            yield return GetAttributes(OriginalMethod, out MemberForwardAttribute attribute, out string key);
            MethodInfo GetValueMethod = typeof(MemberForwardAttribute).GetMethod("GetValue", AccessTools.all);
            yield return CreateCodeInstruction(OpCodes.Call, GetValueMethod);
            MethodInfo SetVariableMethod = typeof(VariableInfo).GetMethod("SetValue", AccessTools.all);
            int i = 0;
            if (!attribute.IsStatic(OriginalMethod))
            {
                foreach (var instruction in LoadReference(attribute.ObjectReference, key, OriginalMethod.ReflectedType,
                    out i))
                    yield return instruction;
            }
            else yield return CreateCodeInstruction(OpCodes.Ldnull);
            yield return CreateCodeInstruction(OpCodes.Ldarg, i);
            yield return Box(AccessorVariables[key].VariableType);
            yield return CreateCodeInstruction(OpCodes.Call, SetVariableMethod);
            yield return CreateCodeInstruction(OpCodes.Ret);
        }

        static Exception Finalizer(Exception __exception) => __exception is OutOfMemoryException ? new InvalidCastException() : __exception;

        internal static void ForwardTypes(string ID, params Type[] types)
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
                                property.ReflectedType);
                        attribute.Forwarded = true;
                    }
                }
                ForwardTypes(ID, type.GetNestedTypes(AccessTools.all));
            }
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