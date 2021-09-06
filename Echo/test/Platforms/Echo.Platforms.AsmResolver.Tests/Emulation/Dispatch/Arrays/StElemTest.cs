using System;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using Echo.Platforms.AsmResolver.Emulation;
using Echo.Platforms.AsmResolver.Emulation.Values.Cli;
using Echo.Platforms.AsmResolver.Tests.Mock;
using Xunit;

namespace Echo.Platforms.AsmResolver.Tests.Emulation.Dispatch.Arrays
{
    public class StElemTest : DispatcherTestBase
    {
        public StElemTest(MockModuleFixture moduleFixture)
            : base(moduleFixture)
        {
        }

        [Theory]
        [InlineData(0, 0xA)]
        [InlineData(1, 0xB)]
        [InlineData(2, 0xC)]
        [InlineData(3, 0xD)]
        public void StelemI4UsingI4Index(int index, int value)
        {
            var environment = ExecutionContext.GetService<ICilRuntimeEnvironment>();
            var marshaller = environment.CliMarshaller;
            
            var array = environment.ValueFactory.AllocateArray(environment.Module.CorLibTypeFactory.Int32, 4);

            var stack = ExecutionContext.ProgramState.Stack;
            stack.Push(marshaller.ToCliValue(array, array.Type));
            stack.Push(new I4Value(index));
            stack.Push(new I4Value(value));

            var result = Dispatcher.Execute(ExecutionContext, new CilInstruction(CilOpCodes.Stelem_I4));
            
            Assert.True(result.IsSuccess);
            Assert.Equal(0, stack.Size);
            Assert.Equal(value, array.LoadElementI4(index, marshaller).I32);
        }

        [Theory]
        [InlineData(0, 0xA)]
        [InlineData(1, 0xB)]
        [InlineData(2, 0xC)]
        [InlineData(3, 0xD)]
        public void StelemI4UsingNativeIntIndex(int index, int value)
        {
            var environment = ExecutionContext.GetService<ICilRuntimeEnvironment>();
            var marshaller = environment.CliMarshaller;
            
            var array = environment.ValueFactory.AllocateArray(environment.Module.CorLibTypeFactory.Int32, 4);

            var stack = ExecutionContext.ProgramState.Stack;
            stack.Push(marshaller.ToCliValue(array, array.Type));
            stack.Push(new NativeIntegerValue(index, environment.Is32Bit));
            stack.Push(new I4Value(value));

            var result = Dispatcher.Execute(ExecutionContext, new CilInstruction(CilOpCodes.Stelem_I4));
            
            Assert.True(result.IsSuccess);
            Assert.Equal(0, stack.Size);
            Assert.Equal(value, array.LoadElementI4(index, marshaller).I32);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void StelemI4OnInt8ArrayShouldSetMultipleElements(int index)
        {
            var environment = ExecutionContext.GetService<ICilRuntimeEnvironment>();
            var marshaller = environment.CliMarshaller;
            
            var array = environment.ValueFactory.AllocateArray(environment.Module.CorLibTypeFactory.Byte, 10);
            
            var stack = ExecutionContext.ProgramState.Stack;
            stack.Push(marshaller.ToCliValue(array, array.Type));
            stack.Push(new I4Value(index));
            stack.Push(new I4Value(0x04030201));

            var result =Dispatcher.Execute(ExecutionContext, new CilInstruction(CilOpCodes.Stelem_I4));
            
            Assert.True(result.IsSuccess);
            Assert.Equal(0x01, array.LoadElementU1(index*4, marshaller).I32);
            Assert.Equal(0x02, array.LoadElementU1(index*4+1, marshaller).I32);
            Assert.Equal(0x03, array.LoadElementU1(index*4+2, marshaller).I32);
            Assert.Equal(0x04, array.LoadElementU1(index*4+3, marshaller).I32);
        }

        [Fact]
        public void StelemI4OnNullShouldThrowNullReferenceException()
        {
            var environment = ExecutionContext.GetService<ICilRuntimeEnvironment>();
            var stack = ExecutionContext.ProgramState.Stack;
            stack.Push(OValue.Null(environment.Is32Bit));
            stack.Push(new I4Value(0));
            stack.Push(new I4Value(0));

            var result = Dispatcher.Execute(ExecutionContext, new CilInstruction(CilOpCodes.Stelem_I4));
            
            Assert.False(result.IsSuccess);
            Assert.IsAssignableFrom<NullReferenceException>(result.Exception);
        }

        [Fact]
        public void StelemOnValueTypeShouldThrowInvalidProgram()
        {
            var stack = ExecutionContext.ProgramState.Stack;
            stack.Push(new I4Value(1234));
            stack.Push(new I4Value(0));
            stack.Push(new I4Value(0));

            var result = Dispatcher.Execute(ExecutionContext, new CilInstruction(CilOpCodes.Stelem_I4));
            
            Assert.False(result.IsSuccess);
            Assert.IsAssignableFrom<InvalidProgramException>(result.Exception);
        }

        [Theory]
        [InlineData(0, 0xA)]
        [InlineData(1, 0xB)]
        [InlineData(2, 0xC)]
        [InlineData(3, 0xD)]
        public void StelemWithInt32ShouldStoreAsInteger32(int index, int value)
        {
            var environment = ExecutionContext.GetService<ICilRuntimeEnvironment>();
            var marshaller = environment.CliMarshaller;
            
            var array = environment.ValueFactory.AllocateArray(environment.Module.CorLibTypeFactory.Int32, 4);

            var stack = ExecutionContext.ProgramState.Stack;
            stack.Push(marshaller.ToCliValue(array, array.Type));
            stack.Push(new I4Value(index));
            stack.Push(new I4Value(value));

            var result = Dispatcher.Execute(ExecutionContext, new CilInstruction(CilOpCodes.Stelem, environment.Module.CorLibTypeFactory.Int32.Type));
            
            Assert.True(result.IsSuccess);
            Assert.Equal(0, stack.Size);
            Assert.Equal(value, array.LoadElementI4(index, marshaller).I32);
        }
    }
}