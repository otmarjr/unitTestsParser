using System;
using System.Collections.Generic;
using Mono.Cecil;
using System.Linq;
using System.Text.RegularExpressions;
using System.CodeDom;

namespace unitTestsParser
{
	public class ClientLibraryParser
	{
		string consumedLibraryPackageName;
		string basePath;
        string clientApplicationRootNamespace;

		public ClientLibraryParser(string libraryPath, string targetPackageName, string inspectedNamespace)
		{
			this.consumedLibraryPackageName = targetPackageName;
			this.basePath = libraryPath;
            this.clientApplicationRootNamespace = inspectedNamespace;
		}

		private bool AssemblyIsDependentOnTargetPackage(AssemblyDefinition asm)
		{
			foreach (var dep in asm.MainModule.AssemblyReferences) {
				if (dep.Name.Equals (this.consumedLibraryPackageName, StringComparison.InvariantCultureIgnoreCase))
					return true;
			}

			return false;
		}

        private bool AssemblyContainsTargetClientTypes(AssemblyDefinition asm)
        {
            return asm.MainModule.Types.Any(t => t.FullName.StartsWith(this.clientApplicationRootNamespace, StringComparison.InvariantCultureIgnoreCase));
        }

		List<MethodDefinition> GetMethodsInstantiatingLibraryVariables (AssemblyDefinition dep)
		{
			var methods = new List<MethodDefinition> ();

			foreach (var t in dep.MainModule.Types) {
				foreach (var m in t.Methods)
				{
					if (m.HasBody && m.Body.HasVariables) {

						foreach (var v in m.Body.Variables) {
							if (v.VariableType.Namespace.StartsWith (this.consumedLibraryPackageName, StringComparison.CurrentCultureIgnoreCase)) {
								methods.Add (m);
								break;
							}
						}
					}
				}
			}

			return methods;
		}

		public List<string> ModulesUsedWithoutSpecification = new List<String> ();
        public List<string> ModulesUsedWithSpecification = new List<String>();
		private List<List<String>> SequenceOfModulesWithouSpecification = new List<List<String>> ();

		private List<string> SequenceOfLibraryCallsMadeInsideMethod(MethodDefinition method)
		{
			List<string> sequence = new List<string>();

            var clientName = ReflectionHelper.MethodCallAsString(method.DeclaringType.Name, method.Name) + "_" + ReflectionHelper.MethodArgumentsSignature(method);
			sequence.Add ("MODULE " +  clientName + "()"); 
			sequence.Add ("-- Client method: " + method.DeclaringType.Namespace + "." + ReflectionHelper.MethodCallAsString (method.DeclaringType.Name, method.Name));
			sequence.Add ("VAR lib_methods : {dummy,");
			var methodCalls = new List<string> ();
			var moduleName = string.Empty;

            foreach (var inst in method.Body.Instructions) {
				if (ReflectionHelper.IsMethodCall (inst)) {
					var mr = inst.Operand as MethodReference;
					if (mr.DeclaringType.Namespace.StartsWith (this.consumedLibraryPackageName, StringComparison.InvariantCultureIgnoreCase)) {
                        var methodCall = ReflectionHelper.MethodCallAsString (mr.DeclaringType.Name, mr.Name);
						methodCalls.Add (methodCall);
						if (moduleName.Equals (string.Empty)) {
							moduleName = mr.DeclaringType.Name;
						}
					}
				}
			}

			bool moduleHasSpecification = true;

			if (existingLibraryModulesInNusmvFile.Find (m => m.Equals (moduleName)) == null) {
				moduleHasSpecification = false;
			}

			if (methodCalls.Count == 0)
				return new List<string> ();

            sequence.Add (string.Join (",", methodCalls.Distinct()));
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

            if (!moduleHasSpecification)
            {
                SequenceOfModulesWithouSpecification.Add(sequence);
                ModulesUsedWithoutSpecification.Add(moduleName);
                if (!this.ClassesWithoutSpecificationClients.ContainsKey(moduleName))
                {
                    this.ClassesWithoutSpecificationClients.Add(moduleName, new List<string>());
                }

                this.ClassesWithoutSpecificationClients[moduleName].Add(clientName);
                return new List<string>();
            }
            else
            {
                if (!this.ModulesUsedWithSpecification.Contains(moduleName))
                {
                    ModulesUsedWithSpecification.Add(moduleName);
                }
            }
			return sequence;
		}

        public Dictionary<string, List<string>> ClassesWithoutSpecificationClients = new Dictionary<string,List<string>>();
		public List<String> ModulesInSequenceOfCalls = new List<String>();
        		private List<String> existingLibraryModulesInNusmvFile = new List<string>();

		private void LoadExistingLibraryModulesInNuSmvFile(string nusmvFile)
		{
			var moduleDeclarationPattern = new Regex (@"^MODULE (?<module_name>\w.+\s).+$");

			var fileLines = System.IO.File.ReadLines (nusmvFile);

			foreach (var line in fileLines) {
				foreach (Match m in moduleDeclarationPattern.Matches(line)) {
					var moduleName = m.Groups ["module_name"].Value;
					existingLibraryModulesInNusmvFile.Add (moduleName.Trim());
				}
			}
		}

        public List<MethodDefinition> MethodsInstantiatingLibraryVariables = new List<MethodDefinition>();

		public List<List<string>> GetSequenceOfCalls(string modulesSMVFile)
		{
			this.LoadExistingLibraryModulesInNuSmvFile (modulesSMVFile);
			var calls = new List<List<string>>();
			// Sequence of calls inside a single method!
			List<AssemblyDefinition> targetLibDependentAssemblies = new List<AssemblyDefinition> ();

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(this.basePath);

			foreach (var dllFile in System.IO.Directory.GetFiles(this.basePath, "*.dll")) {
                var asm = AssemblyDefinition.ReadAssembly(dllFile, new ReaderParameters() { AssemblyResolver = resolver });
                if (this.AssemblyIsDependentOnTargetPackage(asm) && AssemblyContainsTargetClientTypes(asm))
                {
					targetLibDependentAssemblies.Add (asm);
				}
			}

			foreach (var dep in targetLibDependentAssemblies) {
                var methodsOfLibAsms = this.GetMethodsInstantiatingLibraryVariables(dep);
                this.MethodsInstantiatingLibraryVariables.AddRange(methodsOfLibAsms);
                foreach (var m in methodsOfLibAsms)
                {
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

