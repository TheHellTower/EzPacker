using dnlib.DotNet.Emit;

namespace EzPacker.Protections.MutationsStuff.Emulator.Instructions
{
    internal class Stloc : InstructionHandler
    {
        internal override OpCode OpCode => OpCodes.Stloc;

        internal override void Emulate(InstructionEmulator emulator, Instruction instr) => emulator.SetLocalValue(instr.Operand as Local, emulator.Pop());
    }
}