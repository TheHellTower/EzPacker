using dnlib.DotNet.Emit;

namespace EzPacker.Protections.MutationsStuff.Emulator
{
    internal abstract class InstructionHandler
    {
        internal abstract OpCode OpCode { get; }
        internal abstract void Emulate(InstructionEmulator emulator, Instruction instr);
    }
}