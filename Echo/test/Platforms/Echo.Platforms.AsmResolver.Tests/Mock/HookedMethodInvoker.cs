using System;
using System.Collections.Generic;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Echo.Concrete.Values;
using Echo.Platforms.AsmResolver.Emulation.Invocation;
using Echo.Platforms.AsmResolver.Emulation.Values.Cli;

namespace Echo.Platforms.AsmResolver.Tests.Mock
{
    public sealed class HookedMethodInvoker : IMethodInvoker
    {
        private readonly IMethodInvoker _original;

        public HookedMethodInvoker(IMethodInvoker original)
        {
            _original = original ?? throw new ArgumentNullException(nameof(original));
        }

        public IMethodDescriptor LastInvokedMethod
        {
            get;
            private set;
        }
            
        public IConcreteValue Invoke(IMethodDescriptor method, IEnumerable<IConcreteValue> arguments)
        {
            LastInvokedMethod = method;
            return _original.Invoke(method, arguments);
        }

        public IConcreteValue InvokeIndirect(IConcreteValue address, MethodSignature methodSig,
            IEnumerable<IConcreteValue> arguments)
        {
            return _original.InvokeIndirect(address,methodSig, arguments);
        }
    }
}