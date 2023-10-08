using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace EzPacker.Protections
{
    internal class Helper
    {
        public MethodDef GenerateMethod(TypeDef declaringType, object targetMethod, bool hasThis = false, bool isVoid = false)
        {
            MemberRef memberRef = (MemberRef)targetMethod;
            MethodDef methodDef = new MethodDefUser($"DM{Helpers.Generator.RandomString(5)}", MethodSig.CreateStatic(memberRef.ReturnType), MethodAttributes.FamANDAssem | MethodAttributes.Family | MethodAttributes.Static);
            methodDef.Body = new CilBody();
            if (hasThis)
            {
                methodDef.MethodSig.Params.Add(declaringType.Module.Import(declaringType.ToTypeSig(true)));
            }
            foreach (TypeSig item in memberRef.MethodSig.Params)
            {
                methodDef.MethodSig.Params.Add(item);
            }
            methodDef.Parameters.UpdateParameterTypes();
            foreach (Parameter parameter in methodDef.Parameters)
            {
                methodDef.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, parameter));
            }
            methodDef.Body.Instructions.Add(Instruction.Create(OpCodes.Call, memberRef));
            methodDef.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            return methodDef;
        }

        public MethodDef GenerateMethod(IMethod targetMethod, MethodDef md)
        {
            MethodDef methodDef = new MethodDefUser($"DM{Helpers.Generator.RandomString(5)}", MethodSig.CreateStatic(md.Module.Import(targetMethod.DeclaringType.ToTypeSig(true))), MethodAttributes.FamANDAssem | MethodAttributes.Family | MethodAttributes.Static);
            methodDef.ImplAttributes = MethodImplAttributes.IL;
            methodDef.IsHideBySig = true;
            methodDef.Body = new CilBody();
            for (int i = 0; i < targetMethod.MethodSig.Params.Count; i++)
            {
                methodDef.ParamDefs.Add(new ParamDefUser(Helpers.Generator.RandomString(5), (ushort)(i + 1)));
                methodDef.MethodSig.Params.Add(targetMethod.MethodSig.Params[i]);
            }
            methodDef.Parameters.UpdateParameterTypes();
            for (int j = 0; j < methodDef.Parameters.Count; j++)
            {
                Parameter operand = methodDef.Parameters[j];
                methodDef.Body.Instructions.Add(new Instruction(OpCodes.Ldarg, operand));
            }
            methodDef.Body.Instructions.Add(new Instruction(OpCodes.Newobj, targetMethod));
            methodDef.Body.Instructions.Add(new Instruction(OpCodes.Ret));
            return methodDef;
        }

        public MethodDef GenerateMethod(FieldDef targetField, MethodDef md)
        {
            MethodDef methodDef = new MethodDefUser($"DM{Helpers.Generator.RandomString(5)}", MethodSig.CreateStatic(md.Module.Import(targetField.FieldType)), MethodAttributes.FamANDAssem | MethodAttributes.Family | MethodAttributes.Static);
            methodDef.Body = new CilBody();
            TypeDef declaringType = md.DeclaringType;
            methodDef.MethodSig.Params.Add(md.Module.Import(declaringType).ToTypeSig(true));
            methodDef.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            methodDef.Body.Instructions.Add(Instruction.Create(OpCodes.Ldfld, targetField));
            methodDef.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            md.Module.EntryPoint.DeclaringType.Methods.Add(methodDef);
            //md.DeclaringType.Methods.Add(methodDef);
            return methodDef;
        }
    }

    internal static class RefProxy
    {
        private static Hashtable THTUsed = new Hashtable();
        private static bool canObfuscate(MethodDef methodDef) => !methodDef.HasBody || !methodDef.Body.HasInstructions ? false : !methodDef.DeclaringType.IsGlobalModuleType;
        internal static void Execute(ModuleDefMD Module)
        {
            int proxied = 0;
            Helper helper = new Helper();
            fixProxy(Module);
            foreach (TypeDef typeDef in Module.Types.Where(T => T.HasMethods).ToArray<TypeDef>())
            {
                foreach (MethodDef methodDef in typeDef.Methods.Where(M => M.HasBody && M.Body.HasInstructions && M.Body.Instructions.Count() > 1 && !M.IsConstructor && !M.IsStaticConstructor).ToArray<MethodDef>())
                {
                    if (canObfuscate(methodDef))
                    {
                        Instruction[] array3 = methodDef.Body.Instructions.ToArray<Instruction>();
                        int k = 0;
                        while (k < array3.Length)
                        {
                            Instruction instruction = array3[k];
                            if (instruction.OpCode == OpCodes.Newobj)
                            {
                                IMethodDefOrRef methodDefOrRef = instruction.Operand as IMethodDefOrRef;
                                if (!methodDefOrRef.IsMethodSpec)
                                {
                                    if (methodDefOrRef != null)
                                    {
                                        if(!THTUsed.Contains(methodDefOrRef))
                                        {
                                            MethodDef methodDef2 = helper.GenerateMethod(methodDefOrRef, methodDef);
                                            if (methodDef2 != null)
                                            {
                                                //methodDef.DeclaringType.Methods.Add(methodDef2);
                                                methodDef.Module.EntryPoint.DeclaringType.Methods.Add(methodDef2);
                                                instruction.OpCode = OpCodes.Call;
                                                instruction.Operand = methodDef2;
                                                THTUsed[methodDefOrRef] = methodDef2;
                                                proxied++;
                                            }
                                        } else
                                        {
                                            instruction.OpCode = OpCodes.Call;
                                            instruction.Operand = THTUsed[methodDefOrRef];
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (instruction.OpCode == OpCodes.Stfld)
                                {
                                    FieldDef fieldDef = instruction.Operand as FieldDef;
                                    if (fieldDef != null)
                                    {
                                        if(!THTUsed.Contains(fieldDef))
                                        {
                                            CilBody cilBody = new CilBody();
                                            cilBody.Instructions.Add(OpCodes.Nop.ToInstruction());
                                            cilBody.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                                            cilBody.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
                                            cilBody.Instructions.Add(OpCodes.Stfld.ToInstruction(fieldDef));
                                            cilBody.Instructions.Add(OpCodes.Ret.ToInstruction());
                                            MethodSig methodSig = MethodSig.CreateInstance(Module.CorLibTypes.Void, fieldDef.FieldSig.GetFieldType());
                                            methodSig.HasThis = true;
                                            MethodDefUser methodDefUser = new MethodDefUser($"DM{Helpers.Generator.RandomString(5)}", methodSig)
                                            {
                                                Body = cilBody,
                                                IsHideBySig = true
                                            };
                                            THTUsed[fieldDef] = methodDefUser;
                                            //methodDef.DeclaringType.Methods.Add(methodDefUser);
                                            methodDef.Module.EntryPoint.DeclaringType.Methods.Add(methodDefUser);
                                            instruction.Operand = methodDefUser;
                                            instruction.OpCode = OpCodes.Call;
                                        } else
                                        {
                                            instruction.Operand = THTUsed[fieldDef];
                                            instruction.OpCode = OpCodes.Call;
                                        }
                                    }
                                }
                                else
                                {
                                    if (instruction.OpCode == OpCodes.Ldfld)
                                    {
                                        FieldDef fieldDef2 = instruction.Operand as FieldDef;
                                        if (fieldDef2 != null)
                                        {
                                            if (!THTUsed.ContainsKey(fieldDef2))
                                            {
                                                MethodDef methodDef3 = helper.GenerateMethod(fieldDef2, methodDef);
                                                instruction.OpCode = OpCodes.Call;
                                                instruction.Operand = methodDef3;
                                                THTUsed[fieldDef2] = methodDef3;
                                                proxied++;
                                            } else
                                            {
                                                instruction.OpCode = OpCodes.Call;
                                                instruction.Operand = THTUsed[fieldDef2];
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (instruction.OpCode == OpCodes.Call)
                                        {
                                            if (instruction.Operand is MemberRef)
                                            {
                                                MemberRef memberRef = (MemberRef)instruction.Operand;
                                                if (!memberRef.FullName.Contains("Collections.Generic") && !memberRef.Name.Contains("ToString") && !memberRef.FullName.Contains("Thread::Start"))
                                                {
                                                    if (!THTUsed.ContainsKey(memberRef))
                                                    {
                                                        MethodDef methodDef4 = helper.GenerateMethod(typeDef, memberRef, memberRef.HasThis, memberRef.FullName.StartsWith("System.Void"));
                                                        if (methodDef4 != null)
                                                        {
                                                            //typeDef.Methods.Add(methodDef4);
                                                            Module.EntryPoint.DeclaringType.Methods.Add(methodDef4);
                                                            instruction.Operand = methodDef4;
                                                            methodDef4.Body.Instructions.Add(new Instruction(OpCodes.Ret));
                                                            THTUsed[memberRef] = methodDef4;
                                                            proxied++;
                                                        }
                                                    } else
                                                    {
                                                        instruction.Operand = THTUsed[memberRef];
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        IL_3EB:
                            k++;
                            continue;
                            goto IL_3EB;
                        }
                    }
                    methodDef.Body.OptimizeBranches();
                    methodDef.Body.OptimizeMacros();
                    methodDef.Body.SimplifyBranches();
                }
            }
            if (proxied > 0)
                Console.WriteLine($"[References Proxy] Proxied {proxied} Imports(Duplicates Ignored) !");
        }

        private static void fixProxy(ModuleDefMD Module)
        {
            AssemblyResolver assemblyResolver = new AssemblyResolver();
            ModuleContext moduleContext = new ModuleContext(assemblyResolver);
            assemblyResolver.DefaultModuleContext = moduleContext;
            assemblyResolver.EnableTypeDefCache = true;
            List<AssemblyRef> list = Module.GetAssemblyRefs().ToList<AssemblyRef>();
            Module.Context = moduleContext;
            foreach (AssemblyRef assemblyRef in list)
            {
                bool flag = assemblyRef == null;
                bool flag2 = !flag;
                if (flag2)
                {
                    AssemblyDef assemblyDef = assemblyResolver.Resolve(assemblyRef.FullName, Module);
                    bool flag3 = assemblyDef == null;
                    bool flag4 = !flag3;
                    if (flag4)
                    {
                        ((AssemblyResolver)Module.Context.AssemblyResolver).AddToCache(assemblyDef);
                    }
                }
            }
        }
    }
}