using System.ComponentModel.Design;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using Echo.Concrete.Emulation;
using Echo.Platforms.AsmResolver.Emulation;
using Echo.Platforms.AsmResolver.Emulation.Dispatch;
using Echo.Platforms.AsmResolver.Emulation.Invocation;
using Echo.Platforms.AsmResolver.Emulation.Values;
using Echo.Platforms.AsmResolver.Tests.Mock;
using Xunit;

namespace Echo.Platforms.AsmResolver.Tests.Emulation.Dispatch
{
    public class DispatcherTestBase : IClassFixture<MockModuleFixture>
    {
        public DispatcherTestBase(MockModuleFixture moduleFixture)
        {
            ModuleFixture = moduleFixture;
            const bool is32Bit = false;
            
            var dummyModule = moduleFixture.GetModule();
            var dummyMethod = new MethodDefinition(
                "MockMethod",
                MethodAttributes.Static,
                MethodSignature.CreateStatic(dummyModule.CorLibTypeFactory.Void));
            dummyMethod.CilMethodBody = new CilMethodBody(dummyMethod);

            var environment = new MockCilRuntimeEnvironment(dummyModule, is32Bit)
            {
                Architecture = new CilArchitecture(dummyMethod.CilMethodBody),
            };
            
            var methodInvoker = new ReturnUnknownMethodInvoker(environment.ValueFactory);
            environment.MethodInvoker = new HookedMethodInvoker(methodInvoker);

            var container = new ServiceContainer();
            container.AddService(typeof(ICilRuntimeEnvironment), environment);
            
            Dispatcher = new CilDispatcher();
            ExecutionContext = new CilExecutionContext(
                container, 
                new CilProgramState(environment.ValueFactory), 
                default);
        }

        public MockModuleFixture ModuleFixture
        {
            get;
        }

        public CilExecutionContext ExecutionContext
        {
            get;
        }

        public CilDispatcher Dispatcher
        {
            get;
        }
    }
}