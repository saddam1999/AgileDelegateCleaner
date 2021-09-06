using AsmResolver.PE.DotNet.Cil;
using Echo.Platforms.AsmResolver.Emulation.Values.Cli;
using Echo.Platforms.AsmResolver.Tests.Mock;
using Xunit;

namespace Echo.Platforms.AsmResolver.Tests.Emulation.Dispatch.Constants
{
    public class LdcR4Test : DispatcherTestBase
    {
        public LdcR4Test(MockModuleFixture moduleFixture)
            : base(moduleFixture)
        {
        }

        [Fact]
        public void LdcR4()
        {
            var result = Dispatcher.Execute(ExecutionContext, new CilInstruction(CilOpCodes.Ldc_R4, 1.23f));
            Assert.True(result.IsSuccess);
            Assert.Equal(new FValue(1.23f), ExecutionContext.ProgramState.Stack.Top);
        }
    }
}