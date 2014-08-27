using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace unitTestsParser
{
    class Program
    {
		enum ExecutionMode
		{
			GenerateFiniteStateMachine,
			ModulePerUnitTest,
			ModulePerClass,
			FormalSpecification,
			None
		}

		static ExecutionMode GetExecutionModeFromCommandLineArguments(string[] args)
		{
			var argsList = new List<string> (args);

			if (argsList.Contains("-m"))
			{
				var index = argsList.IndexOf ("-m");

				if (index < argsList.Count - 1) {
					var command = args [index + 1];

					switch (command) {
					case "f":
						return ExecutionMode.GenerateFiniteStateMachine;
					case "t":
						return ExecutionMode.ModulePerUnitTest;
					case "c":
						return ExecutionMode.ModulePerClass;
					case "s":
						return ExecutionMode.FormalSpecification;
					}
				}
			}

			return ExecutionMode.None;
		}
    
        static void Main(string[] args)
        {
            // string pacoteBiblioteca = @"E:\\github-repos\\nhibernate\\src\\NHibernate.Test\\bin\\Debug-2.0\NHibernate.dll";
            // string pacoteTestes = @"E:\\github-repos\\nhibernate\\src\\NHibernate.Test\\bin\\Debug-2.0\NHibernate.Test.dll";

			string pacoteBiblioteca = @"/Users/otmarpereira/Documents/github-repos/nhibernate-core/src/NHibernate.Test/bin/Debug-2.0/NHibernate.dll"; 
			string pacoteTestes = @"/Users/otmarpereira/Documents/github-repos/nhibernate-core/src/NHibernate.Test/bin/Debug-2.0/NHibernate.Test.dll";
			string clientLibPath = "/Users/otmarpereira/Downloads/Cuyahoga-1.7.0-bin/bin";
			string libName = "NHibernate";

			var execMode = GetExecutionModeFromCommandLineArguments (args);

			if (execMode == ExecutionMode.None) {
				Console.WriteLine ("You must supply the argument -m followed by f to generate the FSM, t for NuSMV modules per unit test, c for NuSMV modules per class, s for formal specification.");
				return;
			}

			if (execMode == ExecutionMode.GenerateFiniteStateMachine) {
				var tp = new TestsParser(pacoteTestes, pacoteBiblioteca);

				tp.Parse();


				foreach (var fsm in tp.GetClassesFiniteStateMachines())
				{
					Console.WriteLine(fsm);
				}
			}
            
			if (execMode == ExecutionMode.ModulePerClass) {
				var tp = new TestsParser(pacoteTestes, pacoteBiblioteca);

				tp.Parse();

				foreach (var mod in tp.GenerateNuSMVModules())
				{
					Console.WriteLine(mod);
				}
			}

			if (execMode == ExecutionMode.ModulePerClass) {
				var tp = new TestsParser(pacoteTestes, pacoteBiblioteca);

				tp.Parse();
				foreach (var mod in tp.GenerateNuSMVModulesPerUnitTest()) {
					Console.WriteLine (mod);
				}
			}

			if (execMode == ExecutionMode.FormalSpecification) {
				var cp = new ClientLibraryParser (clientLibPath, libName);
				var formalSpecs = cp.GetSequenceOfCalls ();

				int cont_sepc = 1;
				foreach (var spec in formalSpecs) {
					Console.WriteLine ("=>Spec " + cont_sepc++);
					foreach (var call in spec) {
						Console.WriteLine ("\t" + call);
					}
				}
			}
        }
    }
}
