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
		public List<string> GetSequenceOfCalls()
		{
			List<string> calls = new List<string>();
			// Sequence of calls inside a single method!
			List<AssemblyDefinition> targetLibDependentAssemblies = new List<AssemblyDefinition> ();

			foreach (var dllFile in System.IO.Directory.GetFiles(this.basePath, "*.dll")) {
				var asm = AssemblyDefinition.ReadAssembly (dllFile);
				if (this.AssemblyIsDependentOnTargetPackage (asm)) {
					targetLibDependentAssemblies.Add (asm);
				}
			}

			return calls;
		}
	}
}

