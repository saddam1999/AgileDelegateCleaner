using AsmResolver.PE.DotNet.Cil;
using Echo.Platforms.AsmResolver.Emulation.Values.Cli;
using Echo.Platforms.AsmResolver.Tests.Mock;
using Xunit;

namespace Echo.Platforms.AsmResolver.Tests.Emulation.Dispatch.ControlFlow
{
    public class BleTest : DispatcherTestBase
    {
        public BleTest(MockModuleFixture moduleFixture)
            : base(moduleFixture)
        {
        }
        
        [Theory]
        [InlineData(CilCode.Ble, 0, 0, true)]
        [InlineData(CilCode.Ble_Un, 0, 0, true)]
        [InlineData(CilCode.Ble, 0, 1, true)]
        [InlineData(CilCode.Ble_Un, 0, 1, true)]
        [InlineData(CilCode.Ble, 0, -1, false)]
        [InlineData(CilCode.Ble_Un, 0, -1, true)]
        public void I4Comparison(CilCode code, int a, int b, bool expectedToTakeBranch)
        {
            var instruction = new CilInstruction(code.ToOpCode(), new CilOffsetLabel(0x1234));
            int expectedOffset = expectedToTakeBranch ? 0x1234 : instruction.Offset + instruction.Size;
            
            var stack = ExecutionContext.ProgramState.Stack;
            
            stack.Push(new I4Value(a));
            stack.Push(new I4Value(b));

            var result = Dispatcher.Execute(ExecutionContext, instruction);
            
            Assert.True(result.IsSuccess);
            Assert.Equal(0, stack.Size);
            Assert.Equal(expectedOffset, ExecutionContext.ProgramState.ProgramCounter);
        }
    }
}