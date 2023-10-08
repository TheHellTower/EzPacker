using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using static EzPacker.Protections.MutationsStuff.Blocks.BlockParser;

namespace EzPacker.Protections.MutationsStuff.Blocks
{
    public static class Extension
    {
        public static List<Block> GetBlocks(this MethodDef method)
        {
            List<Block> blocks = new List<Block>();
            CilBody body = method.Body;
            body.SimplifyBranches();
            ScopeBlock root = ParseBody(body);
            List<Instruction> branchesInstructions = new List<Instruction>();
            IList<ExceptionHandler> exceptions = method.Body.ExceptionHandlers;
            List<Instruction> range = new List<Instruction>();

            List<Instruction> bodyInstrs = method.Body.Instructions.ToList();

            foreach (ExceptionHandler exception in exceptions)
            {
                switch (exception.HandlerType)
                {
                    case ExceptionHandlerType.Catch:
                    case ExceptionHandlerType.Finally:
                        int tryStartIndex = bodyInstrs.IndexOf(exception.TryStart);
                        int tryEndIndex = bodyInstrs.IndexOf(exception.TryEnd);

                        List<Instruction> tryInstructions = bodyInstrs.GetRange(tryStartIndex, tryEndIndex - tryStartIndex);

                        int handlerStartIndex = bodyInstrs.IndexOf(exception.HandlerStart);
                        int handlerEndIndex = bodyInstrs.IndexOf(exception.HandlerEnd);

                        List<Instruction> handlerInstructions = bodyInstrs.GetRange(handlerStartIndex, handlerEndIndex - handlerStartIndex);

                        range.AddRange(tryInstructions);
                        range.AddRange(handlerInstructions);
                        break;
                }
            }

            Trace trace = new Trace(body, method.ReturnType.RemoveModifiers().ElementType != ElementType.Void);
            foreach (InstrBlock block in GetAllBlocks(root))
            {
                LinkedList<Instruction[]> statements = SplitStatements(block, trace);
                foreach (Instruction[] statement in statements)
                {
                    HashSet<Instruction> statementLast = new HashSet<Instruction>(statements.Select(st => st.Last()));
                    int finished = 0;
                    foreach (Instruction instr in statement)
                    {
                        if (instr.Operand is Instruction)
                            branchesInstructions.Add(instr.Operand as Instruction);

                        if (branchesInstructions.Contains(instr))
                        {
                            branchesInstructions.Remove(instr);
                            finished++;
                        }
                    }
                    bool isInException = statement.Any(x => range.Contains(x));
                    bool isInBranch = branchesInstructions.Count > 0 || finished > 0;
                    bool hasUnknownSource(IList<Instruction> instrs) => instrs.Any(instr =>
                    {
                        if (trace.HasMultipleSources(instr.Offset))
                            return true;
                        if (trace.BrRefs.TryGetValue(instr.Offset, out List<Instruction> srcs))
                        {
                            if (srcs.Any(src => src.Operand is Instruction[]))
                                return true;
                            if (srcs.Any(src => src.Offset <= statements.First.Value.Last().Offset ||
                                                src.Offset >= block.Instructions.Last().Offset))
                                return true;
                            if (srcs.Any(src => statementLast.Contains(src)))
                                return true;
                        }
                        return false;
                    });

                    Block newBlock = new Block();

                    newBlock.Instructions.AddRange(statement);
                    newBlock.IsException = isInException;
                    newBlock.IsBranched = isInBranch;
                    newBlock.IsSafe = !hasUnknownSource(statement);

                    blocks.Add(newBlock);
                }
            }

            return blocks;
        }

        static LinkedList<Instruction[]> SplitStatements(InstrBlock block, Trace trace)
        {
            LinkedList<Instruction[]> statements = new LinkedList<Instruction[]>();
            List<Instruction> currentStatement = new List<Instruction>();

            HashSet<Instruction> requiredInstr = new HashSet<Instruction>();

            for (int i = 0; i < block.Instructions.Count; i++)
            {
                Instruction instr = block.Instructions[i];
                currentStatement.Add(instr);

                bool shouldSplit = i + 1 < block.Instructions.Count && trace.HasMultipleSources(block.Instructions[i + 1].Offset);
                switch (instr.OpCode.FlowControl)
                {
                    case FlowControl.Branch:
                    case FlowControl.Cond_Branch:
                    case FlowControl.Return:
                    case FlowControl.Throw:
                        shouldSplit = true;
                        if (trace.AfterStack[instr.Offset] != 0)
                        {
                            if (instr.Operand is Instruction targetInstr)
                                requiredInstr.Add(targetInstr);
                            else if (instr.Operand is Instruction[] targetInstrs)
                                foreach (Instruction target in targetInstrs)
                                    requiredInstr.Add(target);
                        }
                        break;
                }

                requiredInstr.Remove(instr);

                if (instr.OpCode.OpCodeType != OpCodeType.Prefix && trace.AfterStack[instr.Offset] == 0 && requiredInstr.Count == 0 && (shouldSplit || 90 > new Random().NextDouble()) && (i == 0 || block.Instructions[i - 1].OpCode.Code != Code.Tailcall))
                {
                    statements.AddLast(currentStatement.ToArray());
                    currentStatement.Clear();
                }
            }

            if (currentStatement.Count > 0)
                statements.AddLast(currentStatement.ToArray());

            return statements;
        }

        static IEnumerable<InstrBlock> GetAllBlocks(ScopeBlock scope)
        {
            foreach (BlockBase child in scope.Children)
            {
                if (child is InstrBlock)
                    yield return (InstrBlock)child;
                else
                    foreach (InstrBlock block in GetAllBlocks((ScopeBlock)child))
                        yield return block;
            }
        }
    }
}