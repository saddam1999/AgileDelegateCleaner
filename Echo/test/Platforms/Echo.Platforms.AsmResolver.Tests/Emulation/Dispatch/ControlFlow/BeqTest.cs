using AsmResolver.PE.DotNet.Cil;
using Echo.Platforms.AsmResolver.Emulation;
using Echo.Platforms.AsmResolver.Emulation.Values.Cli;
using Echo.Platforms.AsmResolver.Tests.Mock;
using Xunit;

namespace Echo.Platforms.AsmResolver.Tests.Emulation.Dispatch.ControlFlow
{
    public class BeqTest : DispatcherTestBase
    {
        public BeqTest(MockModuleFixture moduleFixture)
            : base(moduleFixture)
        {
        }

        [Theory]
        [InlineData("0", "0", true)]
        [InlineData("0", "1", false)]
        [InlineData("000?", "1000", false)]
        public void IntegersOnStack(string left, string right, bool expectedToTakeBranch)
        {
            var instruction = new CilInstruction(CilOpCodes.Beq, new CilOffsetLabel(0x1234));
            int expectedOffset = expectedToTakeBranch ? 0x1234 : instruction.Offset + instruction.Size;
            
            var stack = ExecutionContext.ProgramState.Stack;
            
            stack.Push(new I4Value(left));
            stack.Push(new I4Value(right));

            var result = Dispatcher.Execute(ExecutionContext, instruction);
            
            Assert.True(result.IsSuccess);
            Assert.Equal(0, stack.Size);
            Assert.Equal(expectedOffset, ExecutionContext.ProgramState.ProgramCounter);
        }

        [Theory]
        [InlineData(0.0, 0.0, true)]
        [InlineData(0.0, 0.1, false)]
        public void FloatsOnStack(double left, double right, bool expectedToTakeBranch)
        {
            var instruction = new CilInstruction(CilOpCodes.Beq, new CilOffsetLabel(0x1234));
            int expectedOffset = expectedToTakeBranch ? 0x1234 : instruction.Offset + instruction.Size;
            
            var stack = ExecutionContext.ProgramState.Stack;
            
            stack.Push(new FValue(left));
            stack.Push(new FValue(right));

            var result = Dispatcher.Execute(ExecutionContext, instruction);
            
            Assert.True(result.IsSuccess);
            Assert.Equal(0, stack.Size);
            Assert.Equal(expectedOffset, ExecutionContext.ProgramState.ProgramCounter);
        }

        [Fact]
        public void SameObjectsOnStack()
        {
            var environment = ExecutionContext.GetService<ICilRuntimeEnvironment>();
            var marshaller = environment.CliMarshaller;
            
            var instruction = new CilInstruction(CilOpCodes.Beq, new CilOffsetLabel(0x1234));
            var stringValue = environment.ValueFactory.GetStringValue("Hello, World!");
            
            var stack = ExecutionContext.ProgramState.Stack;
            stack.Push(marshaller.ToCliValue(stringValue, environment.Module.CorLibTypeFactory.String));
            stack.Push(marshaller.ToCliValue(stringValue, environment.Module.CorLibTypeFactory.String));

            var result = Dispatcher.Execute(ExecutionContext, instruction);
            
            Assert.True(result.IsSuccess);
            Assert.Equal(0, stack.Size);
            Assert.Equal(0x1234, ExecutionContext.ProgramState.ProgramCounter);
        }

        [Fact]
        public void DifferentObjectsOnStack()
        {
            var environment = ExecutionContext.GetService<ICilRuntimeEnvironment>();
            var marshaller = environment.CliMarshaller;
            
            var instruction = new CilInstruction(CilOpCodes.Beq, new CilOffsetLabel(0x1234));
            var stringValue = environment.ValueFactory.GetStringValue("Hello, World!");
            
            var stack = ExecutionContext.ProgramState.Stack;
            stack.Push(marshaller.ToCliValue(stringValue, environment.Module.CorLibTypeFactory.String));
            stack.Push(OValue.Null(environment.Is32Bit));

            var result = Dispatcher.Execute(ExecutionContext, instruction);
            
            Assert.True(result.IsSuccess);
            Assert.Equal(0, stack.Size);
            Assert.Equal(instruction.Offset + instruction.Size, ExecutionContext.ProgramState.ProgramCounter);
        }
        
    }
}