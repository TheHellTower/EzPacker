using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Linq;

namespace EzPacker.Protections
{
    internal static class Watermark
    {
        internal static void Execute(ModuleDefMD Module)
        {
            var attrName = "EzPackerBy";
            var attrRef = Module.CorLibTypes.GetTypeRef("System", "Attribute");
            var attrType = Module.FindNormal(attrName);
            if (attrType == null)
            {
                attrType = new TypeDefUser("", attrName, attrRef);
                Module.Types.Add(attrType);
            }

            var ctor = attrType.FindInstanceConstructors().FirstOrDefault(m => m.Parameters.Count == 1 && m.Parameters[0].Type == Module.CorLibTypes.String);
            if (ctor == null)
            {
                ctor = new MethodDefUser("EzPacker", MethodSig.CreateInstance(Module.CorLibTypes.Void, Module.CorLibTypes.String), MethodImplAttributes.Managed, MethodAttributes.HideBySig | MethodAttributes.Private | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                {
                    Body = new CilBody { MaxStack = 1 }
                };

                ctor.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("TheHellTower | https://github.com/TheHellTower"));
                ctor.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(new MemberRefUser(Module, ".ctor", MethodSig.CreateInstance(Module.CorLibTypes.Void, Module.CorLibTypes.String), Module.CorLibTypes.GetTypeRef("System", "Exception"))));
                ctor.Body.Instructions.Add(OpCodes.Throw.ToInstruction());

                attrType.Methods.Add(ctor);
            }

            var attr = new CustomAttribute(ctor);
            attr.ConstructorArguments.Add(new CAArgument(Module.CorLibTypes.String, "TheHellTower"));

            for (var i = 0; i < 10; i++)
                Module.CustomAttributes.Add(attr);

            foreach (TypeDef Type in Module.Types.Where(T => T.HasMethods))
            {
                Type.CustomAttributes.Add(attr);
                foreach (MethodDef Method in Type.Methods.Where(M => M.HasBody && M.Body.HasInstructions && M.Body.Instructions.Count > 1))
                    Method.CustomAttributes.Add(attr);
                foreach (FieldDef fieldDef in Type.Fields.Where(f => !f.DeclaringType.IsEnum && !f.DeclaringType.IsForwarder && !f.IsRuntimeSpecialName && !f.DeclaringType.IsEnum))
                    fieldDef.CustomAttributes.Add(attr);

                foreach (EventDef eventDef in Type.Events.Where(e => !e.DeclaringType.IsForwarder && !e.IsRuntimeSpecialName))
                    eventDef.CustomAttributes.Add(attr);
            }
        }
    }
}