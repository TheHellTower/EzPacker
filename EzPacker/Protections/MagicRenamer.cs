using dnlib.DotNet;
using System;
using System.Linq;

namespace EzPacker.Protections
{
    internal static class MagicRenamer
    {
        static string dnSpyFriendlyName = "<TheHellTower>";
        internal static void Execute(ModuleDefMD Module)
        {
            int renamed = 0;
            string text = null;
            foreach (TypeDef typeDef in Module.Types.Where(t => !t.IsGlobalModuleType && !t.Namespace.Contains("My") && t.Interfaces.Count == 0 && !t.IsSpecialName && !t.IsRuntimeSpecialName))
            {
                if (typeDef.Namespace != $"{Module.Assembly.Name}.Properties") //They don't get hidden so it's useless.
                {
                    if (typeDef.IsPublic)
                        text = typeDef.Name;

                    if (!typeDef.Name.Contains("PrivateImplementationDetails"))
                        typeDef.Name = $"<{typeDef.Name}>";

                    //foreach (MethodDef methodDef in typeDef.Methods.Where(m => !m.IsConstructor && !m.IsStaticConstructor && !m.DeclaringType.IsForwarder && !m.IsFamily && !m.IsRuntimeSpecialName && !m.DeclaringType.IsForwarder && !m.DeclaringType.IsGlobalModuleType))
                    foreach (MethodDef methodDef in typeDef.Methods.Where(m => !m.DeclaringType.IsForwarder && !m.IsFamily && !m.IsRuntimeSpecialName && !m.DeclaringType.IsForwarder))
                    {
                        methodDef.CustomAttributes.Add(new CustomAttribute(new MemberRefUser(Module, "THT", MethodSig.CreateInstance(Module.Import(typeof(void)).ToTypeSig(true)), Module.CorLibTypes.GetTypeRef("System.Runtime.CompilerServices", "CompilerGeneratedAttribute"))));
                        methodDef.Name = dnSpyFriendlyName; //dnSpy love this combo
                        foreach (Parameter parameter in methodDef.Parameters)
                            parameter.Name = dnSpyFriendlyName;
                        if (typeDef.Name.Contains(methodDef.Name))
                            methodDef.Name = typeDef.Name;
                        renamed++;
                    }
                    if(renamed > 0)
                    {
                        Console.WriteLine($"Renamed {renamed} Methods !");
                        renamed = 0;
                    }

                    foreach (FieldDef fieldDef in typeDef.Fields.Where(f => !f.DeclaringType.IsEnum && !f.DeclaringType.IsForwarder && !f.IsRuntimeSpecialName && !f.DeclaringType.IsEnum && f.Name != "data"))
                    {
                        fieldDef.Name = dnSpyFriendlyName;
                        renamed++;
                    }

                    if(renamed > 0)
                    {
                        Console.WriteLine($"Renamed {renamed} Fields !");
                        renamed = 0;
                    }

                    foreach (EventDef eventDef in typeDef.Events.Where(e => !e.DeclaringType.IsForwarder && !e.IsRuntimeSpecialName))
                    {
                        eventDef.Name = dnSpyFriendlyName;
                        renamed++;
                    }

                    if(renamed > 0)
                    {
                        Console.WriteLine($"Renamed {renamed} Events !");
                        renamed = 0;
                    }

                    if (typeDef.IsPublic)
                        foreach (Resource resource in Module.Resources.Where(r => r.Name.Contains(text)))
                        {
                            resource.Name = resource.Name.Replace(text, typeDef.Name); //Fix resources
                            renamed = 0;
                        }
                    if (renamed > 0)
                    {
                        Console.WriteLine($"[Fixed Resources] Renamed {renamed} Resources !");
                        renamed = 0;
                    }
                }
            }
        }
    }
}