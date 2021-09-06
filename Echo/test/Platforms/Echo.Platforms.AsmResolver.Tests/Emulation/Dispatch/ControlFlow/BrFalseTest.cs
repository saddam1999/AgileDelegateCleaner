using AsmResolver.PE.DotNet.Cil;
using Echo.Platforms.AsmResolver.Emulation.Values.Cli;
using Echo.Platforms.AsmResolver.Tests.Mock;
using Xunit;

namespace Echo.Platforms.AsmResolver.Tests.Emulation.Dispatch.ControlFlow
{
    public class BrFalseTest : DispatcherTestBase
    {
        public BrFalseTest(MockModuleFixture moduleFixture)
            : base(moduleFixture)
        {
        }

        [Theory]
        [InlineData("1", false)]
        [InlineData("0", true)]
        [InlineData("1000", false)]
        [InlineData("10?0", false)]
        public void Integer32OnStack(string value, bool expectedToHaveTakenBranch)
        {
            var instruction = new CilInstruction(CilOpCodes.Brfalse, new CilOffsetLabel(0x1234));
            int expectedOffset = expectedToHaveTakenBranch ? 0x1234 : instruction.Offset + instruction.Size;
            
            ExecutionContext.ProgramState.Stack.Push(new I4Value(value));
            var result = Dispatcher.Execute(ExecutionContext, instruction);
            
            Assert.True(result.IsSuccess);
            Assert.Equal(0, ExecutionContext.ProgramState.Stack.Size);
            Assert.Equal(expectedOffset, ExecutionContext.ProgramState.ProgramCounter);
        }
    }
}