using System.Collections.Generic;
using System.Linq;
using Echo.Ast.Construction;
using Echo.Ast.Patterns;
using Echo.Ast.Tests.Patterns;
using Echo.ControlFlow;
using Echo.ControlFlow.Construction;
using Echo.ControlFlow.Construction.Symbolic;
using Echo.Core.Code;
using Echo.Platforms.DummyPlatform.Code;
using Echo.Platforms.DummyPlatform.ControlFlow;
using Xunit;

namespace Echo.Ast.Tests
{
    public class AstParserTest
    {
        private ControlFlowGraph<Statement<DummyInstruction>> ConstructAst(
            IEnumerable<DummyInstruction> instructions)
        {
            var architecture = DummyArchitecture.Instance;
            
            var dfgBuilder = new DummyTransitionResolver();
            var cfgBuilder = new SymbolicFlowGraphBuilder<DummyInstruction>(architecture, instructions, dfgBuilder);

            var cfg = cfgBuilder.ConstructFlowGraph(0);
            var astBuilder = new AstParser<DummyInstruction>(cfg, dfgBuilder.DataFlowGraph);
            return astBuilder.Parse();
        }
        
        [Fact]
        public void SingleInstructionNoArgumentsShouldResultInSingleExpressionStatement()
        {
            var cfg = ConstructAst(new[]
            {
                DummyInstruction.Ret(0)
            });

            var pattern = StatementPattern
                .Instruction(new DummyInstructionPattern(DummyOpCode.Ret));

            var result = pattern.Match(cfg.Nodes[0].Contents.Header);
            Assert.True(result.IsSuccess);
        }
        
        [Fact]
        public void InstructionWithOneStackArgumentShouldResultInAssignmentAndExpressionStatementWithArgument()
        {
            var cfg = ConstructAst(new[]
            {
                DummyInstruction.Push(0, 1),
                DummyInstruction.Pop(1, 1), 
                DummyInstruction.Ret(2)
            });

            var variableCapture = new CaptureGroup("variable");

            var pattern = new SequencePattern<Statement<DummyInstruction>>(
                // stack_slot = push 1()
                StatementPattern
                    .Assignment<DummyInstruction>()
                    .WithExpression(ExpressionPattern.Instruction(new DummyInstructionPattern(DummyOpCode.Push)))
                    .CaptureVariables(variableCapture),

                // pop(stack_slot)
                StatementPattern.Expression(ExpressionPattern
                    .Instruction(new DummyInstructionPattern(DummyOpCode.Pop))
                    .WithArguments(ExpressionPattern
                        .Variable<DummyInstruction>()
                        .CaptureVariable(variableCapture))),

                // ret()
                StatementPattern.Instruction(new DummyInstructionPattern(DummyOpCode.Ret))
            );

            var result = pattern.Match(cfg.Nodes[0].Contents.Instructions);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Captures[variableCapture].Distinct());
        }

        [Fact]
        public void PushingTwoValuesOnStackShouldResultInTwoVariablesAssigned()
        {
            var cfg = ConstructAst(new[]
            {
                DummyInstruction.Push(0, 2),
                DummyInstruction.Pop(1, 2), 
                DummyInstruction.Ret(2)
            });
            
            var variableCapture = new CaptureGroup("variable");
            var argumentsCapture = new CaptureGroup("argument");

            var pattern = new SequencePattern<Statement<DummyInstruction>>(
                // stack_slot_1, stack_slot_2 = push 2()
                StatementPattern
                    .Assignment<DummyInstruction>()
                    .WithVariables(2)
                    .CaptureVariables(variableCapture),

                // pop(?, ?)
                StatementPattern.Expression(ExpressionPattern
                    .Instruction<DummyInstruction>()
                    .WithArguments(2)
                    .CaptureArguments(argumentsCapture)),

                // ret()
                StatementPattern.Instruction(new DummyInstructionPattern(DummyOpCode.Ret))
            );

            var result = pattern.Match(cfg.Nodes[0].Contents.Instructions);
            Assert.True(result.IsSuccess);

            var variables = result.Captures[variableCapture]
                .Cast<IVariable>()
                .ToArray();

            var arguments = result.Captures[argumentsCapture]
                .Cast<VariableExpression<DummyInstruction>>()
                .Select(e => e.Variable)
                .ToArray();

            Assert.Equal(variables, arguments);
        }

        [Fact]
        public void PushingTwoValuesOnStackWithDifferentConsumers()
        {
            var cfg = ConstructAst(new[]
            {
                DummyInstruction.Push(0, 2),
                DummyInstruction.Pop(1, 1), 
                DummyInstruction.Pop(2, 1), 
                DummyInstruction.Ret(3)
            });
            
            var variableCapture = new CaptureGroup("variable");
            var argumentsCapture1 = new CaptureGroup("argument1");
            var argumentsCapture2 = new CaptureGroup("argument2");

            var pattern = new SequencePattern<Statement<DummyInstruction>>(
                // stack_slot_1, stack_slot_2 = push 2()
                StatementPattern
                    .Assignment<DummyInstruction>()
                    .WithVariables(2)
                    .CaptureVariables(variableCapture),

                // pop(?)
                StatementPattern.Expression(ExpressionPattern
                    .Instruction<DummyInstruction>()
                    .WithArguments(1)
                    .CaptureArguments(argumentsCapture1)),

                // pop(?)
                StatementPattern.Expression(ExpressionPattern
                    .Instruction<DummyInstruction>()
                    .WithArguments(1)
                    .CaptureArguments(argumentsCapture2)),

                // ret()
                StatementPattern.Instruction(new DummyInstructionPattern(DummyOpCode.Ret))
            );

            var result = pattern.Match(cfg.Nodes[0].Contents.Instructions);
            Assert.True(result.IsSuccess);

            var variables = result.Captures[variableCapture]
                .Cast<IVariable>()
                .ToArray();

            var argument1 = (VariableExpression<DummyInstruction>) result.Captures[argumentsCapture1][0];
            var argument2 = (VariableExpression<DummyInstruction>) result.Captures[argumentsCapture2][0];
            
            // Note: we expect the first pop statement to use the second variable that was pushed by the push instruction.
            Assert.Equal(variables[1], argument1.Variable);
            Assert.Equal(variables[0], argument2.Variable);
        }

        [Fact]
        public void JoiningControlFlowPathsWithPositiveStackDeltaShouldResultInPhiNode()
        {
            var cfg = ConstructAst(new[]
            {
                DummyInstruction.Push(0, 1),
                DummyInstruction.JmpCond(1,4),

                DummyInstruction.Push(2, 1),
                DummyInstruction.Jmp(3, 5),

                DummyInstruction.Push(4, 1),

                DummyInstruction.Pop(5, 1),
                DummyInstruction.Ret(6)
            });
            
            var phiSourcesCapture = new CaptureGroup("sources");
            var variablesCapture = new CaptureGroup("variables");

            // stack_slot = push 1()
            var assignPattern = StatementPattern
                .Assignment<DummyInstruction>()
                .CaptureVariables(variablesCapture);
            
            // variable = phi(?, ?) 
            var phiPattern = StatementPattern
                .Phi<DummyInstruction>()
                .WithSources(2)
                .CaptureSources(phiSourcesCapture);

            var push1Result = assignPattern.Match(cfg.Nodes[2].Contents.Header);
            var push2Result = assignPattern.Match(cfg.Nodes[4].Contents.Header);
            var phiResult = phiPattern.Match(cfg.Nodes[5].Contents.Header);
            
            Assert.True(push1Result.IsSuccess, "Node 2 was expected to start with an assignment statement.");
            Assert.True(push2Result.IsSuccess, "Node 4 was expected to start with an assignment statement.");
            Assert.True(phiResult.IsSuccess, "Node 5 was expected to start with a phi statement.");

            var sources = phiResult.Captures[phiSourcesCapture]
                .Cast<VariableExpression<DummyInstruction>>()
                .Select(s => s.Variable);

            var allVariables = new[]
            {
                (IVariable) push1Result.Captures[variablesCapture][0],
                (IVariable) push2Result.Captures[variablesCapture][0]
            };

            Assert.Equal(allVariables.ToHashSet(), sources.ToHashSet());
        }

        [Fact]
        public void JoiningControlFlowPathsWithDifferentVariableVersionsShouldResultInPhiNode()
        {
            var variable = new DummyVariable("temp");
            
            var cfg = ConstructAst(new[]
            {
                DummyInstruction.Push(0, 1),
                DummyInstruction.JmpCond(1,5),

                DummyInstruction.Push(2, 1),
                DummyInstruction.Set(3, variable),
                DummyInstruction.Jmp(4, 7),

                DummyInstruction.Push(5, 1),
                DummyInstruction.Set(6, variable),

                DummyInstruction.Get(7, variable),
                DummyInstruction.Pop(8, 1),
                DummyInstruction.Ret(9)
            });
            
            var phiSourcesCapture = new CaptureGroup("sources");
            var variablesCapture = new CaptureGroup("variables");

            // temp_vx = set(?)
            var assignPattern = StatementPattern
                .Assignment<DummyInstruction>()
                .WithExpression(ExpressionPattern
                    .Instruction(new DummyInstructionPattern(DummyOpCode.Set))
                    .WithArguments(1))
                .CaptureVariables(variablesCapture);
            
            // variable = phi(?, ?) 
            var phiPattern = StatementPattern
                .Phi<DummyInstruction>()
                .WithSources(2)
                .CaptureSources(phiSourcesCapture);

            var set1Result = assignPattern.FindFirstMatch(cfg.Nodes[2].Contents.Instructions);
            var set2Result = assignPattern.FindFirstMatch(cfg.Nodes[5].Contents.Instructions);
            var phiResult = phiPattern.Match(cfg.Nodes[7].Contents.Header);
            
            Assert.True(set1Result.IsSuccess, "Node 2 was expected to contain an assignment statement.");
            Assert.True(set2Result.IsSuccess, "Node 5 was expected to contain an assignment statement.");
            Assert.True(phiResult.IsSuccess, "Node 7 was expected to start with a phi statement.");

            var sources = phiResult.Captures[phiSourcesCapture]
                .Cast<VariableExpression<DummyInstruction>>()
                .Select(s => s.Variable);

            var allVariables = new[]
            {
                (IVariable) set1Result.Captures[variablesCapture][0],
                (IVariable) set2Result.Captures[variablesCapture][0]
            };

            Assert.Equal(allVariables.ToHashSet(), sources.ToHashSet());
        }

        [Fact]
        public void LoopCounterShouldResultInOnlyOnePhiNode()
        {  
            var counterVariable = new DummyVariable("i");

            var cfg = ConstructAst(new[]
            {
                // i = initial();
                DummyInstruction.Push(0, 1),
                DummyInstruction.Set(1, counterVariable),

                // loop:
                // if (cond(i)) goto end;
                DummyInstruction.Get(2, counterVariable),
                DummyInstruction.JmpCond(3, 9),

                // Loop body.
                DummyInstruction.Op(4, 0, 0),

                // i = next(i);
                // goto loop
                DummyInstruction.Get(5, counterVariable),
                DummyInstruction.Op(6, 1, 1),
                DummyInstruction.Set(7, counterVariable),
                DummyInstruction.Jmp(8, 2),

                // end:
                DummyInstruction.Ret(9)
            });
            
            // variable = phi(?, ?) 
            var phiPattern = StatementPattern
                .Phi<DummyInstruction>()
                .WithSources(2);

            Assert.True(phiPattern.Matches(cfg.Nodes[2].Contents.Header));
            Assert.All(cfg.Nodes[0].Contents.Instructions, s => Assert.False(phiPattern.Matches(s)));
            Assert.All(cfg.Nodes[4].Contents.Instructions, s => Assert.False(phiPattern.Matches(s)));
            Assert.All(cfg.Nodes[9].Contents.Instructions, s => Assert.False(phiPattern.Matches(s)));
        }

        [Fact]
        public void JoiningPathsWithMultipleAssignmentsShouldOnlyUseLatestVersion()
        {
            var variable = new DummyVariable("temp");
            
            var cfg = ConstructAst(new[]
            {
                // if (cond) goto else:
                DummyInstruction.Push(0, 1),
                DummyInstruction.JmpCond(1, 7),

                // temp = some_value (not used).
                DummyInstruction.Push(2, 1),
                DummyInstruction.Set(3, variable),
                // temp = some_value (render previous value useless)
                DummyInstruction.Push(4, 1),
                DummyInstruction.Set(5, variable),
                
                // goto end
                DummyInstruction.Jmp(6, 9),

                // else:
                // temp = some_value
                DummyInstruction.Push(7, 1),
                DummyInstruction.Set(8, variable),

                // end:
                // pop(temp)
                DummyInstruction.Get(9, variable),
                DummyInstruction.Pop(10, 1),
                
                // return
                DummyInstruction.Ret(11)
            });

            var phiSourcesCapture = new CaptureGroup("sources");
            var variablesCapture = new CaptureGroup("variables");
            
            // variable = phi(?, ?) 
            var phiPattern = StatementPattern
                .Phi<DummyInstruction>()
                .WithSources(2)
                .CaptureSources(phiSourcesCapture);
            
            // temp_vx = set(?)
            var assignPattern = StatementPattern
                .Assignment<DummyInstruction>()
                .WithExpression(ExpressionPattern
                    .Instruction(new DummyInstructionPattern(DummyOpCode.Set))
                    .WithArguments(1))
                .CaptureVariables(variablesCapture);

            var assignment1Results = assignPattern
                .FindAllMatches(cfg.Nodes[7].Contents.Instructions)
                .ToArray();
            
            var assignment2Results = assignPattern
                .FindAllMatches(cfg.Nodes[2].Contents.Instructions)
                .ToArray();

            var phiResult = phiPattern.Match(cfg.Nodes[9].Contents.Header);
            
            Assert.True(assignment1Results.Length == 1, "Node 7 was expected to have one assignment to 'temp'.");
            Assert.True(assignment2Results.Length == 2, "Node 2 was expected to have two assignments to 'temp'.");
            Assert.True(phiResult.IsSuccess, "Node 9 was expected to start with a phi node with two sources.");

            var sources = phiResult.Captures[phiSourcesCapture]
                .Cast<VariableExpression<DummyInstruction>>()
                .Select(s => s.Variable);

            var allVariables = new[]
            {
                (IVariable) assignment1Results[0].Captures[variablesCapture][0],
                (IVariable) assignment2Results[1].Captures[variablesCapture][0]
            };

            Assert.Equal(allVariables.ToHashSet(), sources.ToHashSet());
        }

        [Fact]
        public void SwitchWithAssignmentsToSameVariableShouldResultInPhiNodeWithTheSameNumberOfSources()
        {
            var variable = new DummyVariable("temp");
            
            var cfg = ConstructAst(new[]
            {
                // switch(some_value)
                DummyInstruction.Push(0, 1),
                DummyInstruction.Switch(1, 5, 8, 11),

                // default:
                DummyInstruction.Push(2, 1),
                DummyInstruction.Set(3, variable),
                DummyInstruction.Jmp(4, 13),

                // case 0:
                DummyInstruction.Push(5, 1),
                DummyInstruction.Set(6, variable),
                DummyInstruction.Jmp(7, 13),
                
                // case 1:
                DummyInstruction.Push(8, 1),
                DummyInstruction.Set(9, variable),
                DummyInstruction.Jmp(10, 13),

                // case 2:
                DummyInstruction.Push(11, 1),
                DummyInstruction.Set(12, variable),

                // end:
                DummyInstruction.Get(13, variable),
                DummyInstruction.Pop(14, 1),
                DummyInstruction.Ret(15)
            });
            
            // variable = phi(?, ?) 
            var phiPattern = StatementPattern
                .Phi<DummyInstruction>()
                .WithSources(4);

            Assert.True(phiPattern.Matches(cfg.Nodes[13].Contents.Header),
                "Node 13 was expected to start with a phi node with 4 sources.");
        }

        [Fact]
        public void VariableAccessThatHasntBeenWrittenToBeforeShouldntResultInPhi()
        {
            var variable = new DummyVariable("dummy");
            var cfg = ConstructAst(new[]
            {
                DummyInstruction.Get(0, variable), 
                DummyInstruction.Op(1, 1, 0), 
                DummyInstruction.Ret(2)
            });
            
            Assert.Single(cfg.Nodes);
            Assert.All(cfg.Entrypoint.Contents.Instructions, Assert.IsNotType<PhiStatement<DummyInstruction>>);
        }
    }
}