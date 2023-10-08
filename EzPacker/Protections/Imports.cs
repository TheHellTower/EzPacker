using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EzPacker.Protections
{
    internal class Imports
    {
        internal static void Execute(ModuleDefMD Module)
        {
            int imported = 0;
            Dictionary<IMethod, MethodDef> brigdes = new Dictionary<IMethod, MethodDef>();
            Dictionary<IMethod, TypeDef> methods = new Dictionary<IMethod, TypeDef>();
            FieldDefUser field = new FieldDefUser(Helpers.Generator.RandomString(5), new FieldSig(Module.ImportAsTypeSig(typeof(object[]))), FieldAttributes.Public | FieldAttributes.Static);
            MethodDef cctor = Module.GlobalType.FindOrCreateStaticConstructor();

            MethodDefUser initMethod = new MethodDefUser($"{Helpers.Generator.RandomString(5)}", MethodSig.CreateStatic(Module.CorLibTypes.Void), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.Static);
            initMethod.Body = new CilBody();
            Module.GlobalType.Methods.Add(initMethod);
            foreach (TypeDef Type in Module.GetTypes().Where(T => !T.IsDelegate && !T.IsGlobalModuleType && T.Namespace != "Costura").ToArray())
                foreach (MethodDef Method in Type.Methods.Where(M => M.HasBody && M.Body.HasInstructions).ToArray())
                {
                    /*if (Method.IsPrivate)
                        continue;*/

                    IList<Instruction> Instructions = Method.Body.Instructions;

                    for (int i = 0; i < Instructions.Count; i++)
                    {
                        if (Instructions[i].OpCode != OpCodes.Call && Instructions[i].OpCode == OpCodes.Callvirt)
                            continue;
                        if (Instructions[i].Operand is IMethod idef)
                        {
                            if (!idef.IsMethodDef)
                                continue;

                            MethodDef def = idef.ResolveMethodDef();

                            if (def == null || def.HasThis || Instructions[i].ToString().Contains("cpblk"))
                                continue;

                            if (brigdes.ContainsKey(idef))
                            {
                                Instructions[i].OpCode = OpCodes.Call;
                                Instructions[i].Operand = brigdes[idef];
                                continue;
                            }

                            MethodSig sig = CreateProxySignature(Module, def);
                            TypeDef delegateType = CreateDelegateType(Module, sig);
                            Module.Types.Add(delegateType);

                            MethodImplAttributes methImplFlags = MethodImplAttributes.IL | MethodImplAttributes.Managed;
                            MethodAttributes methFlags = MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot;
                            MethodDefUser brigde = new MethodDefUser(Helpers.Generator.RandomString(5), sig, methImplFlags, methFlags);
                            brigde.Body = new CilBody();

                            brigde.Body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(field));
                            brigde.Body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(methods.Count));
                            brigde.Body.Instructions.Add(OpCodes.Ldelem_Ref.ToInstruction());
                            foreach (Parameter parameter in brigde.Parameters)
                            {
                                parameter.Name = Helpers.Generator.RandomString(5);
                                brigde.Body.Instructions.Add(OpCodes.Ldarg.ToInstruction(parameter));
                            }
                            brigde.Body.Instructions.Add(OpCodes.Call.ToInstruction(delegateType.Methods[1]));
                            brigde.Body.Instructions.Add(OpCodes.Ret.ToInstruction());

                            delegateType.Methods.Add(brigde);

                            Instructions[i].OpCode = OpCodes.Call;
                            Instructions[i].Operand = brigde;

                            if (idef.IsMethodDef)
                                methods.Add(def, delegateType);
                            else if (idef.IsMemberRef)
                                methods.Add(idef as MemberRef, delegateType);

                            brigdes.Add(idef, brigde);
                            imported++;
                        }
                    }
                }

            Module.GlobalType.Fields.Add(field);

            List<Instruction> instructions = new List<Instruction>();
            List<Instruction> current = initMethod.Body.Instructions.ToList();
            initMethod.Body.Instructions.Clear();

            instructions.Add(OpCodes.Ldc_I4.ToInstruction(methods.Count));
            instructions.Add(OpCodes.Newarr.ToInstruction(Module.CorLibTypes.Object));
            instructions.Add(OpCodes.Dup.ToInstruction());

            int index = 0;

            foreach (KeyValuePair<IMethod, TypeDef> entry in methods)
            {
                instructions.Add(OpCodes.Ldc_I4.ToInstruction(index));
                instructions.Add(OpCodes.Ldnull.ToInstruction());
                instructions.Add(OpCodes.Ldftn.ToInstruction(entry.Key));
                instructions.Add(OpCodes.Newobj.ToInstruction(entry.Value.Methods[0]));
                instructions.Add(OpCodes.Stelem_Ref.ToInstruction());
                instructions.Add(OpCodes.Dup.ToInstruction());
                index++;
            }

            instructions.Add(OpCodes.Pop.ToInstruction());
            instructions.Add(OpCodes.Stsfld.ToInstruction(field));

            foreach (Instruction instr in instructions)
                initMethod.Body.Instructions.Add(instr);
            foreach (Instruction instr in current)
                initMethod.Body.Instructions.Add(instr);
            if (initMethod.Body.Instructions[initMethod.Body.Instructions.Count - 1].OpCode != OpCodes.Ret)
                initMethod.Body.Instructions.Add(new Instruction(OpCodes.Ret));

            cctor.Body.Instructions.Insert(0, new Instruction(OpCodes.Call, initMethod));

            if (imported > 0)
                Console.WriteLine($"[ImportsProtection] Protected {imported} Imports !");
        }

        public static TypeDef CreateDelegateType(ModuleDef Module, MethodSig sig)
        {
            TypeDefUser ret = new TypeDefUser(Helpers.Generator.RandomString(5), Module.CorLibTypes.GetTypeRef("System", "MulticastDelegate"));
            ret.Attributes = TypeAttributes.Public | TypeAttributes.Sealed;

            MethodDefUser ctor = new MethodDefUser(".ctor", MethodSig.CreateInstance(Module.CorLibTypes.Void, Module.CorLibTypes.Object, Module.CorLibTypes.IntPtr));
            ctor.Attributes = MethodAttributes.Assembly | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName;
            ctor.ImplAttributes = MethodImplAttributes.Runtime;
            ret.Methods.Add(ctor);

            MethodDefUser invoke = new MethodDefUser("Invoke", sig.Clone());
            invoke.MethodSig.HasThis = true;
            invoke.Attributes = MethodAttributes.Assembly | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot;
            invoke.ImplAttributes = MethodImplAttributes.Runtime;
            ret.Methods.Add(invoke);

            return ret;
        }

        public static MethodSig CreateProxySignature(ModuleDef Module, IMethod method)
        {
            IEnumerable<TypeSig> paramTypes = method.MethodSig.Params.Select(type => {
                if (type.IsClassSig && method.MethodSig.HasThis)
                    return Module.CorLibTypes.Object;
                return type;
            });
            if (method.MethodSig.HasThis && !method.MethodSig.ExplicitThis)
            {
                TypeDef declType = method.DeclaringType.ResolveTypeDefThrow();
                if (!declType.IsValueType)
                    paramTypes = new[] { Module.CorLibTypes.Object }.Concat(paramTypes);
                else
                    paramTypes = new[] { declType.ToTypeSig() }.Concat(paramTypes);
            }
            TypeSig retType = method.MethodSig.RetType;
            if (retType.IsClassSig)
                retType = Module.CorLibTypes.Object;
            return MethodSig.CreateStatic(retType, paramTypes.ToArray());
        }
    }
}