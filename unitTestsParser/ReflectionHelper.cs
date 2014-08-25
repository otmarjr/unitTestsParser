using System;
using Mono.Cecil.Cil;
using Mono.Cecil;

namespace unitTestsParser
{
	public static class ReflectionHelper
	{
		public static Predicate<Instruction> IsMethodCall = i => i.Operand != null && i.Operand as MethodReference != null;

		public static string MethodCallAsString(string originalClassName, string originalMethodName)
		{
			return string.Format ("{0}_{1}", originalClassName.Replace("`", "_"), originalMethodName.Replace(".","_"));
		}
	}
}

