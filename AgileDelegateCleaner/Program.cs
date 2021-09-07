
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using Echo.DataFlow.Analysis;
using Echo.Platforms.AsmResolver;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AgileDelegateCleaner {
    internal static class Program {
        static void Main(string[] args) {

            if (args.Length <= 0) {
                Console.Write("[?] Path: ");
                args = new[] { Console.ReadLine() };
            }


            var module = ModuleDefinition.FromFile(args[0]);

            var importer = new ReferenceImporter(module);

            if (!module.TopLevelTypes.Any(x => x.IsDelegate)) {
                Console.WriteLine("No Delegates Found Closing...");
                Task.Delay(3 * 1000);
                return;
            }

            if (module.TopLevelTypes.Any(x => x.IsDelegate && x.GetStaticConstructor() is null)) {
                Console.WriteLine("No Static Constructors Found Closing...");
                Task.Delay(3 * 1000);
                return;
            }

            var moduleMethods = module.GetAllTypes()
                .SelectMany(x => x.Methods)
                .Where(x => x.CilMethodBody is not null);


            foreach (var method in moduleMethods) {
                var instructions = method.CilMethodBody.Instructions;
                instructions.CalculateOffsets();
                instructions.ExpandMacros();
                method.CilMethodBody.ConstructSymbolicFlowGraph(out var dataFlowGraph);
                for (int x = 0; x < instructions.Count; x++) { 
                    var instr = instructions[x];

                    // Check If instruction Assigned in Graph Nodes.
                    if (!(dataFlowGraph.Nodes.Contains(instr.Offset))) continue;

                    var callExpression = dataFlowGraph.Nodes[instr.Offset]
                        .GetOrderedDependencies(DependencyCollectionFlags.IncludeStackDependencies)
                        .Select(x => x.Contents);

                    // Check If its There Any Delegate Which Its DeclaringType is a Delegate. 
                    if (!(callExpression.Any(x => x is not null && x.OpCode.Code == CilCode.Ldsfld && x.Operand is FieldDefinition delegateField && delegateField.DeclaringType is TypeDefinition fieldType && fieldType.IsDelegate))) continue; 

                    if (!(instr.OpCode.Code is CilCode.Call or CilCode.Callvirt)) continue;

                    if (!(instr.Operand is IMethodDescriptor invokeMethod)) continue;

                    if (!(invokeMethod.Name is "Invoke")) continue;

                    if (!(invokeMethod.DeclaringType.Resolve() is TypeDefinition delegateType)) continue;

                    if (!(delegateType.GetStaticConstructor() is MethodDefinition delegateInitializer)) continue;

                    if (!(delegateInitializer.CilMethodBody is CilMethodBody initializerBody)) continue;

                    if (!(initializerBody.Instructions.Count >= 3)) continue;

                    if (!(initializerBody.Instructions.First().IsLdcI4())) continue;

                    // Get TypeToken From Delegate cctor (Type Initializer).
                    var typeToken = initializerBody.Instructions.First().GetLdcI4Constant();

                    // Try Resolving.
                    if (!module.TryLookupMember(new((uint)(0x2000001 + typeToken)), out _)) continue;

                    // Get Bad Field Instruction.
                    var badInstruction = callExpression.FirstOrDefault(x => x.OpCode.Code == CilCode.Ldsfld && x.Operand is FieldDefinition delegateField && delegateField.DeclaringType is TypeDefinition fieldType && fieldType.IsDelegate);

                    if (badInstruction is null) continue;

                    uint methodToken;
                    IMetadataMember realMember = null;
                    bool isVirtualCall = false;

                    var field = (IFieldDescriptor)badInstruction.Operand;
                    string fieldName = field.Name;
                    isVirtualCall = false;

                    if (fieldName.EndsWith("%")) {
                        isVirtualCall = true;
                        fieldName = fieldName.TrimEnd('%');
                    }

                    methodToken = BitConverter.ToUInt32(Convert.FromBase64String(fieldName), 0);

                    if (!module.TryLookupMember(new(methodToken + 0xA000001U), out realMember)) continue;

                    var realMethod = realMember as IMethodDescriptor;

                    if (realMethod is null) continue; 

                    // Nope BadInstruction.
                    badInstruction.OpCode = CilOpCodes.Nop;
                    badInstruction.Operand = null;

                    // if Field Name Contains '%' Its a Callvirt Instruction.
                    instr.OpCode = isVirtualCall 
                        ? CilOpCodes.Callvirt 
                        : CilOpCodes.Call;
                    // Restore Original Member.
                    instr.Operand = importer.ImportMethod(realMethod);
                }
                instructions.OptimizeMacros();
            }

            Console.WriteLine("Writing...");

            // TODO: clean delegates.

            module.Write(args[0].Insert(args[0].Length - 4, "-Cleaned"));
        }
    }
}