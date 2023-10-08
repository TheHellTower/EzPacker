using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using EzPacker.Helpers;
using EzPacker.Protections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EzPacker
{
    public class Program
    {
        private static ModuleDefMD Module = null;
        private static ModuleDefMD Stub = null;
        static void Main(string[] args)
        {
            Module = ModuleDefMD.Load(Path.GetFullPath(args[0]));
            Stub = ModuleDefMD.Load("EzPacker_Stub.exe");
            Stub.EntryPoint.Body.Instructions.Where(I => I.OpCode == OpCodes.Ldc_I4 && (int)I.GetLdcI4Value() == 1337).First().Operand = Module.EntryPoint.MDToken.ToInt32();
            Module.EntryPoint = null;
            Module.Kind = ModuleKind.NetModule;
            MemoryStream MS = new MemoryStream();
            Obfuscate(Module);
            Obfuscate(Stub);
            Module.Write(MS);
            InjectArray(Stub, Compress(MS.ToArray()), "data");
            Stub.Name = Module.Name;
            Stub.Assembly.Name = Module.Assembly.Name;
            Stub.EntryPoint.DeclaringType.Namespace = "";
            Stub.EntryPoint.DeclaringType.Name = "<https://github.com/TheHellTower>";
            Stub.EntryPoint.Name = "<https://cracked.io/TheHellTower>";

            string NewLocation = Module.Location.Insert(Module.Location.Length - Module.Name.Length, "Protected\\");
            if (!File.Exists(NewLocation))
                Directory.CreateDirectory(NewLocation.Replace(Module.Name, ""));

            Stub.Write(NewLocation);
            Console.WriteLine($"Output available at: {NewLocation}");
            Process.Start(NewLocation.Replace(Stub.Name, string.Empty));
            Console.ReadLine();
        }

        public static byte[] Compress(byte[] data)
        {
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Optimal))
                dstream.Write(data, 0, data.Length);

            return output.ToArray();
        }

        private static void Obfuscate(ModuleDefMD This)
        {
            string processed = This == Stub ? "Stub" : "Module";

            Console.WriteLine($"========== Processing {processed} ==========");
            MagicRenamer.Execute(This);
            StringsHider.Execute(This);
            if (This == Stub)
            {
                RefProxy.Execute(This);
                Imports.Execute(This);
                for(int I = 0; I < 2; I++)
                    Mutations.Execute(This);
            }
            /*Need to fix stopped to work for no reason but was working at last try*/
            //ControlFlow.Execute(This);
            //Mutations.Execute(This);
            /*Need to fix stopped to work for no reason but was working at last try*/
            Watermark.Execute(This);
        }

        static void InjectArray(ModuleDefMD mod, byte[] injectedData, string injectedName)
        {
            Importer importer = new Importer(mod);
            TypeDef EntryType = Stub.EntryPoint.DeclaringType;

            ITypeDefOrRef valueTypeRef = importer.Import(typeof(System.ValueType));
            TypeDef classWithLayout = new TypeDefUser(Generator.RandomString(6), valueTypeRef);
            classWithLayout.Attributes |= TypeAttributes.Sealed | TypeAttributes.ExplicitLayout | TypeAttributes.NestedPublic;
            classWithLayout.ClassLayout = new ClassLayoutUser(1, (uint)injectedData.Length);
            EntryType.NestedTypes.Add(classWithLayout);

            FieldDef fieldWithRVA = new FieldDefUser(Generator.RandomString(6), new FieldSig(classWithLayout.ToTypeSig()), FieldAttributes.Static | FieldAttributes.Assembly | FieldAttributes.HasFieldRVA);
            fieldWithRVA.InitialValue = injectedData;
            EntryType.Fields.Add(fieldWithRVA);

            ITypeDefOrRef byteArrayRef = importer.Import(typeof(System.Byte[]));
            FieldDef fieldInjectedArray = Stub.EntryPoint.DeclaringType.Fields.FirstOrDefault(F => F.Name == injectedName);
            fieldInjectedArray.Name = Generator.RandomString(6);
            
            ITypeDefOrRef systemByte = importer.Import(typeof(System.Byte));
            ITypeDefOrRef runtimeHelpers = importer.Import(typeof(System.Runtime.CompilerServices.RuntimeHelpers));
            IMethod initArray = importer.Import(typeof(System.Runtime.CompilerServices.RuntimeHelpers).GetMethod("InitializeArray", new Type[] { typeof(System.Array), typeof(System.RuntimeFieldHandle) }));

            MethodDef cctor = EntryType.FindOrCreateStaticConstructor();
            IList<Instruction> instrs = cctor.Body.Instructions;
            instrs.Insert(0, new Instruction(OpCodes.Ldc_I4, injectedData.Length));
            instrs.Insert(1, new Instruction(OpCodes.Newarr, systemByte));
            instrs.Insert(2, new Instruction(OpCodes.Dup));
            instrs.Insert(3, new Instruction(OpCodes.Ldtoken, fieldWithRVA));
            instrs.Insert(4, new Instruction(OpCodes.Call, initArray));
            instrs.Insert(5, new Instruction(OpCodes.Stsfld, fieldInjectedArray));
        }
    }
}