using dnlib.DotNet.Emit;
using EzPacker.Protections.MutationsStuff.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EzPacker.Protections.MutationsStuff.Emulator
{
    internal class InstructionEmulator
    {
        Dictionary<OpCode, InstructionHandler> _instructions;
        Dictionary<Local, object> _locals;
        Stack<object> _stack;
        public InstructionEmulator()
        {
            _instructions = new Dictionary<OpCode, InstructionHandler>();
            _locals = new Dictionary<Local, object>();

            List<InstructionHandler> emuInstructions = typeof(InstructionHandler).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(InstructionHandler)) && !t.IsAbstract).Select(t => (InstructionHandler)Activator.CreateInstance(t)).ToList();

            foreach (InstructionHandler instrEmu in emuInstructions)
                _instructions.Add(instrEmu.OpCode, instrEmu);

            _stack = new Stack<object>();
        }

        public void Emulate(Instruction instruction)
        {
            if (_instructions.TryGetValue(instruction.OpCode, out InstructionHandler cilInstr))
                cilInstr.Emulate(this, instruction);
        }

        public void Emulate(Block block)
        {
            foreach (Instruction instr in block.Instructions)
                Emulate(instr);
        }

        public object Pop() => _stack.Pop();
        public void Push(object value) => _stack.Push(value);

        public object GetLocalValue(Local local) => _locals[local];
        public void SetLocalValue(Local local, object value) => _locals[local] = value;
    }
}