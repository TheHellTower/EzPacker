using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Linq;
using System.Text;

namespace EzPacker.Protections
{
    internal static class StringsHider
    {
        internal static void Execute(ModuleDefMD Module)
        {
            int hidden = 0;
            foreach (TypeDef type in Module.Types.Where(t => t.HasMethods))
            {
                MethodDef cctor = type.FindOrCreateStaticConstructor();
                if (cctor.HasBody && cctor.Body.HasInstructions && cctor.Body.Instructions.Last().OpCode == OpCodes.Ret) cctor.Body.Instructions.Remove(cctor.Body.Instructions.Last());

                foreach (MethodDef method in type.Methods.Where(m => m.HasBody && m.Body.HasInstructions))
                {
                    int ii = 0;
                    for (int iii = 0; iii < method.Body.Instructions.Count; iii++)
                    {
                        Instruction Instruction = method.Body.Instructions[iii];
                        if (Instruction.OpCode == OpCodes.Ldstr)
                        {
                            string str = (string)Instruction.Operand;
                            if (str != ""/* && str.Length > 1*/)
                            {
                                byte[] bytes = Encoding.UTF8.GetBytes(str);

                                string newName = $"[{method.MDToken.ToString()}-THT]_{ii.ToString()}";

                                FieldDefUser field = new FieldDefUser(newName, new FieldSig(Module.CorLibTypes.String), FieldAttributes.Assembly | FieldAttributes.Static);

                                type.Fields.Add(field);

                                cctor.Body.Instructions.Insert(0, OpCodes.Stsfld.ToInstruction(field));
                                cctor.Body.Instructions.Insert(0, new Instruction(OpCodes.Callvirt, Module.Import(typeof(Encoding).GetMethod("GetString", new Type[] { typeof(byte[]) }))));

                                for (int i = 0; i < bytes.Length; i++)
                                {
                                    cctor.Body.Instructions.Insert(0, OpCodes.Stelem_I1.ToInstruction());
                                    cctor.Body.Instructions.Insert(0, new Instruction(OpCodes.Ldc_I4, (int)bytes[i]));
                                    cctor.Body.Instructions.Insert(0, new Instruction(OpCodes.Ldc_I4, (int)i));
                                    cctor.Body.Instructions.Insert(0, OpCodes.Dup.ToInstruction());
                                }

                                cctor.Body.Instructions.Insert(0, new Instruction(OpCodes.Newarr, Module.CorLibTypes.Byte.ToTypeDefOrRef()));
                                cctor.Body.Instructions.Insert(0, new Instruction(OpCodes.Ldc_I4, (int)bytes.Length));
                                cctor.Body.Instructions.Insert(0, new Instruction(OpCodes.Call, Module.Import(typeof(Encoding).GetMethod("get_UTF8", new Type[] { }))));

                                Instruction.OpCode = OpCodes.Ldsfld;
                                Instruction.Operand = field;

                                ii++;
                                hidden++;
                            }
                        }
                    }

                    for (int iii = 0; iii < method.Body.Instructions.Count; iii++) //Fix branches pointing the field directly.
                    {
                        Instruction Instruction = method.Body.Instructions[iii];
                        if (Instruction.OpCode == OpCodes.Brfalse || Instruction.OpCode == OpCodes.Brtrue || Instruction.OpCode == OpCodes.Brfalse_S || Instruction.OpCode == OpCodes.Brtrue_S)
                            if (Instruction.Operand.ToString().ToLower().Contains("ldsfld"))
                            {
                                object operand = Instruction.Operand;
                                Instruction targetedInstruction = (operand as Instruction);

                                //Instruction.Operand = method.Body.Instructions[method.Body.Instructions.IndexOf(targetedInstruction) - 1];
                            }
                    }

                    if (cctor.Body.Instructions.Count > 0) //Fix last instructions
                    {
                        while (cctor.Body.Instructions.Last().OpCode == OpCodes.Stloc)
                        {
                            Instruction instr = cctor.Body.Instructions.Last();
                            cctor.Body.Instructions.Remove(instr);
                        }

                        while (cctor.Body.Instructions.Last().OpCode == OpCodes.Ret)
                        {
                            Instruction instr = cctor.Body.Instructions.Last();
                            cctor.Body.Instructions.Remove(instr);
                        }
                    }

                    method.Body.Instructions.SimplifyBranches();
                    method.Body.Instructions.OptimizeBranches();
                }

                if (cctor.Body.Instructions[cctor.Body.Instructions.Count() - 1].OpCode != OpCodes.Ret) cctor.Body.Instructions.Add(new Instruction(OpCodes.Ret));
            }
            if (hidden > 0)
                Console.WriteLine($"[S2B] Hidden {hidden} Strings !");
        }
    }
}