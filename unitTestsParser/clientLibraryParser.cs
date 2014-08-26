using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace unitTestsParser
{
	public class ClientLibraryParser
	{
		string targetPackage;
		string basePath;

		public ClientLibraryParser(string libraryPath, string targetPackageName)
		{
			this.targetPackage = targetPackageName;
			this.basePath = libraryPath;
		}

		private bool AssemblyIsDependentOnTargetPackage(AssemblyDefinition asm)
		{
			foreach (var dep in asm.MainModule.AssemblyReferences) {
				if (dep.Name.Equals (this.targetPackage, StringComparison.InvariantCultureIgnoreCase))
					return true;
			}

			return false;
		}

		List<MethodDefinition> GetMethodsInstantiatingLibraryVariables (AssemblyDefinition dep)
		{
			var methods = new List<MethodDefinition> ();

			foreach (var t in dep.MainModule.Types) {
				foreach (var m in t.Methods)
				{
					if (m.HasBody && m.Body.HasVariables) {

						foreach (var v in m.Body.Variables) {
							if (v.VariableType.Namespace.StartsWith (this.targetPackage, StringComparison.CurrentCultureIgnoreCase)) {
								methods.Add (m);
								break;
							}
						}
					}
				}
			}

			return methods;
		}

		private List<string> SequenceOfLibraryCallsMadeInsideMethod(MethodDefinition method)
		{
			List<string> sequence = new List<string>();

			sequence.Add ("Client method: " + method.DeclaringType.Namespace + "." + ReflectionHelper.MethodCallAsString (method.DeclaringType.Name, method.Name));
			foreach (var inst in method.Body.Instructions) {
				if (ReflectionHelper.IsMethodCall (inst)) {
					var mr = inst.Operand as MethodReference;

					if (mr.DeclaringType.Namespace.StartsWith (this.targetPackage, StringComparison.InvariantCultureIgnoreCase)) {
						sequence.Add (ReflectionHelper.MethodCallAsString (mr.DeclaringType.Name, mr.Name));
					}
				}
			}

			return sequence;
		}

		public List<List<string>> GetSequenceOfCalls()
		{
			var calls = new List<List<string>>();
			// Sequence of calls inside a single method!
			List<AssemblyDefinition> targetLibDependentAssemblies = new List<AssemblyDefinition> ();

			foreach (var dllFile in System.IO.Directory.GetFiles(this.basePath, "*.dll")) {
				var asm = AssemblyDefinition.ReadAssembly (dllFile);
				if (this.AssemblyIsDependentOnTargetPackage (asm)) {
					targetLibDependentAssemblies.Add (asm);
				}
			}

			foreach (var dep in targetLibDependentAssemblies) {
				var methodsInstantiatingLibraryVaribles = this.GetMethodsInstantiatingLibraryVariables (dep);
				foreach (var m in methodsInstantiatingLibraryVaribles) {
					calls.Add (SequenceOfLibraryCallsMadeInsideMethod (m));
				}
			}

			return calls;
		}
	}
}

