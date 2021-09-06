using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using Echo.Concrete.Values;
using Echo.Concrete.Values.ValueType;
using Echo.Platforms.AsmResolver.Emulation;
using Echo.Platforms.AsmResolver.Tests.Mock;
using Xunit;

namespace Echo.Platforms.AsmResolver.Tests.Emulation.Dispatch.Pointers
{
    public class InitObjTest : DispatcherTestBase
    {
        public InitObjTest(MockModuleFixture moduleProvider)
            : base(moduleProvider)
        {
        }

        [Fact]
        public void InitializePrimitiveObject()
        {
            var environment = ExecutionContext.GetService<ICilRuntimeEnvironment>();
            
            var int32Type = environment.Module.CorLibTypeFactory.Int32;
            
            var pointer = environment.ValueFactory
                .AllocateMemory(sizeof(int) * 2, false)
                .MakePointer(environment.Is32Bit);
            
            var stack = ExecutionContext.ProgramState.Stack;
            stack.Push(environment.CliMarshaller.ToCliValue(pointer, int32Type.MakePointerType()));

            var result = Dispatcher.Execute(ExecutionContext, new CilInstruction(CilOpCodes.Initobj, int32Type.Type));
            
            Assert.True(result.IsSuccess);
            Assert.Equal(new Integer32Value(0), pointer.ReadInteger32(0));
            Assert.False(pointer.ReadInteger32(sizeof(int)).IsKnown);
        }
    }
}