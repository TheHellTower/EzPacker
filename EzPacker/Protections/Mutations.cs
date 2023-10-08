using dnlib.DotNet;
using dnlib.DotNet.Emit;
using EzPacker.Protections.MutationsStuff.Blocks;
using EzPacker.Protections.MutationsStuff.Emulator;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EzPacker.Protections
{
    internal static class Mutations
    {
        static Random rnd = new Random();
        internal static void Execute(ModuleDefMD Module)
        {
            int mutated = 0;
            foreach (TypeDef Type in Module.GetTypes().Where(T => T.HasMethods))
            {
                foreach (MethodDef Method in Type.Methods.Where(M => M.HasBody && M.Body.HasInstructions))
                {
                    if (Method == Module.GlobalType.FindOrCreateStaticConstructor()) continue;
                    Method.Body.SimplifyMacros(Method.Parameters);

                    List<Block> blocks = Method.GetBlocks();

                    InstructionEmulator emulator = new InstructionEmulator();
                    Block firstBlock = new Block();

                    List<Local> locals = new List<Local>();
                    Dictionary<Local, List<Block>> localToBlocks = new Dictionary<Local, List<Block>>();

                    int maxLocals = 2;

                    for (int i = 0; i < maxLocals; i++)
                    {
                        Local newLocal = new Local(Module.CorLibTypes.Int32);

                        locals.Add(newLocal);
                        localToBlocks.Add(newLocal, new List<Block>());

                        firstBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(rnd.Next()));
                        firstBlock.Instructions.Add(OpCodes.Stloc.ToInstruction(newLocal));
                    }

                    emulator.Emulate(firstBlock);

                    List<Block> allBlocks = new List<Block>() { firstBlock };

                    foreach (Block block in blocks)
                    {
                        if (block.IsSafe && !block.IsBranched
                            && !block.IsException)
                        {
                            foreach (Local local in locals)
                            {
                                Block updateValue = new Block();

                                switch (rnd.Next(0, 7))
                                {
                                    case 0:
                                        SimpleUpdateGen(updateValue, local, rnd.Next(0, 2));
                                        break;
                                    case 1:
                                        Instruction nop = new Instruction(OpCodes.Nop);
                                        int currentValue = (int)emulator.GetLocalValue(local);
                                        Block branchBlock = new Block();

                                        updateValue.Instructions.Add(nop);

                                        bool isFake = rnd.Next(0, 2) == 0;
                                        bool isReverse = rnd.Next(0, 2) == 0;

                                        if (isReverse)
                                        {
                                            branchBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(rnd.Next(100, 500)));
                                            branchBlock.Instructions.Add(OpCodes.Ldloc.ToInstruction(local));
                                        }
                                        else
                                        {
                                            branchBlock.Instructions.Add(OpCodes.Ldloc.ToInstruction(local));
                                            branchBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(rnd.Next(100, 500)));
                                        }
                                        branchBlock.Instructions.Add(OpCodes.Ceq.ToInstruction());
                                        branchBlock.Instructions.Add(isFake ? OpCodes.Brfalse.ToInstruction(nop) :
                                            OpCodes.Brtrue.ToInstruction(nop));

                                        Block insideBlock = new Block();
                                        SimpleUpdateGen(insideBlock, local, rnd.Next(0, 2), rnd.Next(2, 5));

                                        if (!isFake)
                                            emulator.Emulate(insideBlock);

                                        branchBlock.Copy(insideBlock.Instructions);

                                        if (rnd.Next(0, 2) == 0)
                                            SimpleUpdateGen(updateValue, local, rnd.Next(0, 2));

                                        allBlocks.Add(branchBlock);
                                        break;
                                    case 2:
                                        int currentLocValue = (int)emulator.GetLocalValue(local);
                                        int max = rnd.Next(currentLocValue + 1000, currentLocValue + 2000);

                                        Instruction backUpNop = new Instruction(OpCodes.Nop);
                                        Instruction backNop = new Instruction(OpCodes.Nop);

                                        Block loopBlock = new Block();

                                        Block outsideLoopBlock = new Block();

                                        updateValue.Instructions.Add(backUpNop);

                                        loopBlock.Instructions.Add(backNop);

                                        loopBlock.Instructions.Add(OpCodes.Ldloc.ToInstruction(local));
                                        loopBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(max));

                                        loopBlock.Instructions.Add(OpCodes.Cgt.ToInstruction());
                                        loopBlock.Instructions.Add(OpCodes.Brtrue.ToInstruction(backUpNop));

                                        Block insideLoopBlock = new Block();
                                        SimpleUpdateGen(insideLoopBlock, local, rnd.Next(0, 2), rnd.Next(2, 3));

                                        while (currentLocValue < max)
                                        {
                                            emulator.Emulate(insideLoopBlock);

                                            currentLocValue = (int)emulator.GetLocalValue(local);
                                        }

                                        loopBlock.Copy(insideLoopBlock.Instructions);
                                        loopBlock.Instructions.Add(OpCodes.Br.ToInstruction(backNop));

                                        allBlocks.Add(loopBlock);
                                        break;
                                    case 3:
                                        SimpleUpdateGen(updateValue, local, rnd.Next(0, 2));
                                        break;
                                    case 4:
                                        SimpleUpdateGen(updateValue, local, rnd.Next(0, 2));
                                        break;
                                    case 5:
                                        int maxCases = rnd.Next(3, 7);
                                        int[] array = new int[maxCases];
                                        Instruction[] targets = new Instruction[maxCases];

                                        for (int i = 0; i < maxCases; i++)
                                        {
                                            int val = rnd.Next(100, 500);

                                            array[i] = val;
                                            targets[i] = OpCodes.Ldc_I4.ToInstruction(val);
                                        }

                                        Block switchBlock = new Block();
                                        int targetIndex = rnd.Next(0, array.Length);
                                        int targetValue = array[targetIndex];
                                        Instruction stlocInstr = OpCodes.Stloc.ToInstruction(local);
                                        int localValue = (int)emulator.GetLocalValue(local);
                                        Instruction switchInstr = OpCodes.Switch.ToInstruction(targets);
                                        Instruction[] newTargets = new Instruction[maxCases];

                                        //int firstPushCalc = Calculate(localValue, targetIndex, out OpCode reversePush);

                                        switchBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(targetIndex));
                                        //switchBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(targetIndex));
                                        switchBlock.Instructions.Add(switchInstr);

                                        switchBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(rnd.Next(100, 500)));
                                        switchBlock.Instructions.Add(OpCodes.Br.ToInstruction(stlocInstr));

                                        int k = 0;
                                        foreach (Instruction instr in targets)
                                        {
                                            int switchValue = instr.GetLdcI4Value();
                                            Instruction ldlocInstr = OpCodes.Ldc_I4.ToInstruction(switchValue);

                                            newTargets[k] = ldlocInstr;

                                            switchBlock.Instructions.Add(ldlocInstr);
                                            switchBlock.Instructions.Add(OpCodes.Br.ToInstruction(stlocInstr));
                                            k++;
                                        }

                                        switchInstr.Operand = newTargets;

                                        switchBlock.Instructions.Add(stlocInstr);

                                        emulator.SetLocalValue(local, targetValue);

                                        allBlocks.Add(switchBlock);
                                        break;
                                    case 6:
                                        TypeSig arrayType = Module.ImportAsTypeSig(typeof(int[]));
                                        Local arrayLoc = new Local(arrayType);
                                        int arrSize = rnd.Next(1, 5);
                                        int[] arrValues = new int[arrSize];
                                        Block arrayBlock = new Block();
                                        int arrIndex = rnd.Next(0, arrValues.Length);

                                        arrayBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(arrSize));
                                        arrayBlock.Instructions.Add(OpCodes.Newarr.ToInstruction(Module.CorLibTypes.Int32));
                                        arrayBlock.Instructions.Add(OpCodes.Stloc.ToInstruction(arrayLoc));

                                        for (int z = 0; z < arrSize; z++)
                                        {
                                            int rndLocalValue = rnd.Next(500, 1000);

                                            arrayBlock.Instructions.Add(OpCodes.Ldloc.ToInstruction(arrayLoc));
                                            arrayBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(z)); //INDEX
                                            arrayBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(rndLocalValue)); //VALUE
                                            arrayBlock.Instructions.Add(OpCodes.Stelem_I4.ToInstruction());

                                            arrValues[z] = rndLocalValue;
                                        }

                                        arrayBlock.Instructions.Add(OpCodes.Ldloc.ToInstruction(arrayLoc));
                                        arrayBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(arrIndex)); //INDEX
                                        arrayBlock.Instructions.Add(OpCodes.Ldelem_I4.ToInstruction());
                                        arrayBlock.Instructions.Add(OpCodes.Stloc.ToInstruction(local));

                                        emulator.SetLocalValue(local, arrValues[arrIndex]);

                                        allBlocks.Add(arrayBlock);
                                        Method.Body.Variables.Add(arrayLoc);
                                        break;
                                        //case 2:
                                        //    Instruction backUpNop = new Instruction(OpCodes.Nop);
                                        //    Instruction targetNop = new Instruction(OpCodes.Nop);
                                        //    Dictionary<Local, List<Block>> currentUpdates = localToBlocks[local];

                                        //    if (currentUpdates.Count == 0)
                                        //    {
                                        //        SimpleUpdateGen(updateValue, local, rnd.Next(0, 3));
                                        //        break;
                                        //    }

                                        //    var rndUpdate = currentUpdates[rnd.Next(0, currentUpdates.Count)];
                                        //    var backUpValue = (int)emulator.GetLocalValue(local);

                                        //    rndUpdate.Instructions.Insert(0, targetNop);

                                        //    emulator.Emulate(rndUpdate);

                                        //    var resultValue = (int)emulator.GetLocalValue(local);

                                        //    if (rndUpdate.MarkedValues.Contains(resultValue)) {
                                        //        emulator.SetLocalValue(local, backUpValue);

                                        //        SimpleUpdateGen(updateValue, local, rnd.Next(0, 3));
                                        //        break;
                                        //    }

                                        //    rndUpdate.MarkedValues.Add(resultValue);

                                        //    var loopBranchBlock = new Block();

                                        //    loopBranchBlock.Instructions.Add(OpCodes.Ldloc.ToInstruction(local));
                                        //    loopBranchBlock.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(resultValue));
                                        //    loopBranchBlock.Instructions.Add(OpCodes.Ceq.ToInstruction());
                                        //    loopBranchBlock.Instructions.Add(OpCodes.Brtrue.ToInstruction(backUpNop));

                                        //    allBlocks.Insert(allBlocks.IndexOf(rndUpdate) + 1, loopBranchBlock);

                                        //    updateValue.Instructions.Add(OpCodes.Br.ToInstruction(targetNop));
                                        //    updateValue.Instructions.Add(backUpNop);

                                        //    break;
                                }

                                emulator.Emulate(updateValue);

                                allBlocks.Add(updateValue);
                                localToBlocks[local].Add(updateValue);
                            }
                        }

                        for (int i = 0; i < block.Instructions.Count; i++)
                        {
                            Instruction instr = block.Instructions[i];

                            if(instr.IsLdcI4() && block.Instructions[i + 1].ToString().Contains("ResolveMethod"))
                            {
                                //
                            } else if (instr.IsLdcI4() && !block.Instructions[i+1].ToString().Contains("ResolveMethod"))
                            {
                                //Console.WriteLine($"1: {instr.ToString()}");
                                int value = instr.GetLdcI4Value();
                                Local rndLocal = locals[rnd.Next(0, locals.Count)];
                                int result = Calculate(value, (int)emulator.GetLocalValue(rndLocal), out OpCode reverse);

                                instr.OpCode = OpCodes.Ldc_I4;
                                instr.Operand = result;

                                block.Instructions.Insert(i + 1, new Instruction(OpCodes.Ldloc, rndLocal));
                                block.Instructions.Insert(i + 2, new Instruction(reverse));
                            }
                        }
                        allBlocks.Add(block);
                    }

                    Method.Body.Instructions.Clear();

                    foreach (Block block in allBlocks)
                        foreach (Instruction instr in block.Instructions)
                            Method.Body.Instructions.Add(instr);

                    foreach (Local local in locals)
                        Method.Body.Variables.Add(local);

                    mutated++;
                }
            }
            if (mutated > 0)
                Console.WriteLine($"[Mutations] Mutated {mutated} Methods !");
        }
        static void SimpleUpdateGen(Block block, Local local, int caseValue, int quantity = 1)
        {
            for (int i = 0; i < quantity; i++)
            {
                switch (caseValue)
                {
                    case 0:
                        block.Instructions.Add(OpCodes.Ldloc.ToInstruction(local));
                        block.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(rnd.Next(100, 350)));
                        block.Instructions.Add(OpCodes.Add.ToInstruction());
                        block.Instructions.Add(OpCodes.Stloc.ToInstruction(local));
                        break;
                    case 1:
                        block.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(rnd.Next(100, 350)));
                        block.Instructions.Add(OpCodes.Ldloc.ToInstruction(local));
                        block.Instructions.Add(OpCodes.Add.ToInstruction());
                        block.Instructions.Add(OpCodes.Stloc.ToInstruction(local));
                        break;
                }
            }
        }

        static int Calculate(int a, int b, out OpCode reverse)
        {
            reverse = OpCodes.Nop;

            switch (rnd.Next(0, 3))
            {
                case 0:
                    reverse = OpCodes.Add;
                    return a - b;
                case 1:
                    reverse = OpCodes.Sub;
                    return a + b;
                case 2:
                    reverse = OpCodes.Xor;
                    return a ^ b;
            }
            return -1;
        }
    }
}