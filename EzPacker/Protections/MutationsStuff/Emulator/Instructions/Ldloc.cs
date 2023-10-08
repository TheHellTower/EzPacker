using dnlib.DotNet.Emit;

namespace EzPacker.Protections.MutationsStuff.Emulator.Instructions
{
    internal class Ldloc : InstructionHandler
    {
        internal override OpCode OpCode => OpCodes.Ldloc;

        internal override void Emulate(InstructionEmulator emulator, Instruction instr) => emulator.Push(emulator.GetLocalValue(instr.Operand as Local));
    }
}