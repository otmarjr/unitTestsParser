using System;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy;
using ICSharpCode.Decompiler.Ast;
using ICSharpCode.Decompiler;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.Decompiler.Ast.Transforms;

namespace unitTestsParser
{
    public static class ReflectionHelper
    {
        public static Predicate<Instruction> IsMethodCall = i => i.Operand != null && i.Operand as MethodReference != null;

        public static string MethodCallAsString(string originalClassName, string originalMethodName)
        {
            return string.Format("{0}_{1}", originalClassName.Replace("`", "_"), originalMethodName.Replace(".", "_"));
        }

        public static List<MethodReference> GetSequenceOfMethodCallsInsideMethod(MethodReference caller)
        {
            var calls = new List<MethodReference>();

            var textOutput = new AvalonEditTextOutput();
            var metDef = caller.Resolve();
            var context = new DecompilerContext(metDef.DeclaringType.Module) { CurrentType = metDef.DeclaringType };
            var builder = new AstBuilder(context);
            builder.AddMethod(metDef);
            var syntaxTree = builder.SyntaxTree;
            TransformationPipeline.RunTransformationsUntil(syntaxTree, null, context);

            syntaxTree.AcceptVisitor(new InsertParenthesesVisitor { InsertParenthesesForReadability = true });
            var outputFormatter = new TextOutputFormatter(textOutput) { FoldBraces = context.Settings.FoldBraces };
            var formattingPolicy = context.Settings.CSharpFormattingOptions;
            var visitor = new CSharpParameterCollectorVisitor(outputFormatter, formattingPolicy, new List<string>(), null, null);
            syntaxTree.AcceptVisitor(visitor);


            return visitor.VisitedMethods.ToList();
        }


        public static List<Tuple<string, List<String>>> ComputeMethodCalls(MethodDefinition callerMethod)
        {
            var calls = new List<Tuple<string, List<String>>>();

            return calls;
        }
    
    
    }
}

