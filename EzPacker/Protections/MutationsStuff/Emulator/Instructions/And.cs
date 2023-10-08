using dnlib.DotNet.Emit;

namespace EzPacker.Protections.MutationsStuff.Emulator.Instructions
{
    internal class And : InstructionHandler
    {
        internal override OpCode OpCode => OpCodes.And;

        internal override void Emulate(InstructionEmulator emulator, Instruction instr)
        {
            int right = (int)emulator.Pop();
            int left = (int)emulator.Pop();

            emulator.Push(left & right);
        }
    }
}