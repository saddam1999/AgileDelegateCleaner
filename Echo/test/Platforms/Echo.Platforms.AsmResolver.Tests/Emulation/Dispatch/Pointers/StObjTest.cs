using System.Linq;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using Echo.Concrete.Values.ValueType;
using Echo.Platforms.AsmResolver.Emulation;
using Echo.Platforms.AsmResolver.Emulation.Values;
using Echo.Platforms.AsmResolver.Emulation.Values.Cli;
using Echo.Platforms.AsmResolver.Tests.Mock;
using Mocks;
using Xunit;

namespace Echo.Platforms.AsmResolver.Tests.Emulation.Dispatch.Pointers
{
    public class StObjTest : DispatcherTestBase
    {
        private readonly TypeDefinition _structType;
        private readonly FieldDefinition _x;
        private readonly FieldDefinition _y;
        
        public StObjTest(MockModuleFixture moduleFixture)
            : base(moduleFixture)
        {
            var module = moduleFixture.MockModule;
            
            _structType = (TypeDefinition) module.LookupMember(typeof(SimpleStruct).MetadataToken);
            _x = _structType.Fields.First(f => f.Name == nameof(SimpleStruct.X));
            _y = _structType.Fields.First(f => f.Name == nameof(SimpleStruct.Y));
        }

        [Fact]
        public void StoreObjectToPointerShouldSetContents()
        {
            var environment = ExecutionContext.GetService<ICilRuntimeEnvironment>();
            var originalInstance = environment.ValueFactory.AllocateStruct(_structType.ToTypeSignature(), true);
            originalInstance.SetFieldValue(_x, new Integer32Value(0x12345678));
            originalInstance.SetFieldValue(_y, new Integer32Value(0x12345678, 0xFF00FF00));
            
            var stack = ExecutionContext.ProgramState.Stack;

            var destinationAddress = environment.ValueFactory.AllocateMemory(100, true);
            
            // Push unknown pointer and object to write.
            stack.Push(new PointerValue(destinationAddress, environment.Is32Bit));
            stack.Push(environment.CliMarshaller.ToCliValue(originalInstance, _structType.ToTypeSignature()));

            var result = Dispatcher.Execute(ExecutionContext, new CilInstruction(CilOpCodes.Stobj, _structType));

            Assert.True(result.IsSuccess);
            
            // Check structure contents.
            var instance = (IDotNetStructValue) destinationAddress.ReadStruct(0, environment.ValueFactory,
                environment.ValueFactory.GetTypeMemoryLayout(_structType));
            Assert.NotSame(originalInstance, instance);
            Assert.Equal(originalInstance.GetFieldValue(_x), instance.GetFieldValue(_x));
            Assert.Equal(originalInstance.GetFieldValue(_y), instance.GetFieldValue(_y));
        }
        
    }
}