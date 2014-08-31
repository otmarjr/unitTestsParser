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

        public static bool IsExtensionMethod(MethodDefinition md)
        {
            return md.CustomAttributes.Any(a => Type.GetType(a.AttributeType.FullName) == typeof(System.Runtime.CompilerServices.ExtensionAttribute));
        }

        public static bool IsExtensionMethodFirstArgument(MethodReference mr, ParameterReference pr)
        {
            if (IsExtensionMethod(mr.Resolve()) && pr.Index == 0)
            {
                return true;
            }

            return false;
        }

        public static string ResolveParameterValue(ParameterReference pr, MethodReference callee, MethodDefinition caller)
        {
            
            var context = new DecompilerContext(caller.DeclaringType.Module) { CurrentType = caller.DeclaringType };
            var builder = new AstBuilder(context);
            builder.AddMethod(caller);
            var syntaxTree = builder.SyntaxTree;
            TransformationPipeline.RunTransformationsUntil(syntaxTree, null, context);
            syntaxTree.AcceptVisitor(new InsertParenthesesVisitor { InsertParenthesesForReadability = true });

            var descs = syntaxTree.DescendantsAndSelf.ToList();
            var nodes = descs.Where(n => n.Annotations.OfType<MethodReference>().Any(mr => mr.FullName.Equals(callee.FullName))).ToList();

            bool isExtensionMethod = IsExtensionMethod(callee.Resolve());

            foreach (var node in nodes)
            {
                var mr = node.Annotations.OfType<MethodReference>().Where(m => m.FullName.Equals(callee.FullName)).FirstOrDefault();

                if (node.Parent is AssignmentExpression)
                {
                    var assign = node.Parent as AssignmentExpression;
                
                    if (mr != null)
                    {
                        if (mr.Resolve().Parameters.Any(p => p.Name.Equals(pr.Name)))
                            return assign.Right.GetText();
                    }
                }

                if (node is InvocationExpression)
                {
                    var inv = node as InvocationExpression;
                    var index = pr.Index;

                    if (isExtensionMethod) {
                        index--;
                    }

                    return inv.Arguments.ToList()[index].GetText();
                }
            }


            return string.Empty;
        }
    
    
    }
}

