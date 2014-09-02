using System;
using System.Collections.Generic;
using Mono.Cecil;
using System.Linq;

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

			sequence.Add ("MODULE " + ReflectionHelper.MethodCallAsString (method.DeclaringType.Name, method.Name) + "_" + ReflectionHelper.MethodArgumentsSignature(method)  + "()"); 
			sequence.Add ("-- Client method: " + method.DeclaringType.Namespace + "." + ReflectionHelper.MethodCallAsString (method.DeclaringType.Name, method.Name));
			sequence.Add ("VAR lib_methods : {");
			var methodCalls = new List<string> ();
			var moduleName = string.Empty;

			foreach (var inst in method.Body.Instructions) {
				if (ReflectionHelper.IsMethodCall (inst)) {
					var mr = inst.Operand as MethodReference;

					if (mr.DeclaringType.Namespace.StartsWith (this.targetPackage, StringComparison.InvariantCultureIgnoreCase)) {
						methodCalls.Add (ReflectionHelper.MethodCallAsString (mr.DeclaringType.Name, mr.Name));

						if (moduleName.Equals (string.Empty)) {
							moduleName = mr.DeclaringType.Name;
						}
					}
				}
			}

			if (methodCalls.Count == 0)
				return new List<string> ();

			sequence.Add (string.Join (",", methodCalls));
			sequence.Add ("};");
			sequence.Add ("\t instance : " + moduleName + "(lib_methods);");
			sequence.Add ("ASSIGN");
			sequence.Add ("\t init(lib_methods) := " + methodCalls.First () + ";"); 
			sequence.Add ("\t next(lib_methods) := case ");

			var lastCall = methodCalls.First ();

			foreach (var call in methodCalls.Skip(1)) {
				sequence.Add ("\t\t lib_methods = " + lastCall + " : " + call + ";");
				lastCall = call;
			}

			sequence.Add ("\t\t TRUE: lib_methods;");
			sequence.Add ("\t esac;");
			sequence.Add ("SPEC EF (instance.at_accepting_state = TRUE)");
				

			return sequence;
		}

		public List<String> ModulesInSequenceOfCalls = new List<String>();

		public List<List<string>> GetSequenceOfCalls(string modulesSMVFile)
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
					var spec = SequenceOfLibraryCallsMadeInsideMethod (m);

					if (spec.Count > 0) {
						var argsSignatures = ReflectionHelper.MethodArgumentsSignature (m);
						ModulesInSequenceOfCalls.Add (ReflectionHelper.MethodCallAsString (m) + "_" + argsSignatures);
						calls.Add (SequenceOfLibraryCallsMadeInsideMethod (m));
					} else {
						// calls.Add (new List<string>(){string.Format("--No calls to library found by method " + m.DeclaringType.Namespace + "." + m.DeclaringType.Name + "." + m.Name)});
					}
				}
			}

			return calls;
		}
	}
}

