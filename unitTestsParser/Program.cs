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
			SingleModule,
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
					case "m":
						return ExecutionMode.SingleModule;
					}
				}
			}

			return ExecutionMode.None;
		}

    
        static void Main(string[] args)
        {
            // string pacoteBiblioteca = @"E:\\github-repos\\nhibernate\\src\\NHibernate.Test\\bin\\Debug-2.0\NHibernate.dll";
            // string pacoteTestes = @"E:\\github-repos\\nhibernate\\src\\NHibernate.Test\\bin\\Debug-2.0\NHibernate.Test.dll";

            string pacoteBiblioteca;
            string pacoteTestes;
            string clientLibPath;
			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
				pacoteBiblioteca = @"E:\\github-repos\\nhibernate\\src\\NHibernate.Test\\bin\\Debug-2.0\\NHibernate.dll";
				pacoteTestes = @"E:\\github-repos\\nhibernate\\src\\NHibernate.Test\\bin\\Debug-2.0\NHibernate.Test.dll";
				clientLibPath = @"C:\\Users\\Otmar\\Downloads\\Cuyahoga-1.7.0-bin\\bin";
            }
            else
            {
				pacoteBiblioteca = @"/Users/otmarpereira/Documents/github-repos/nhibernate-core/src/NHibernate.Test/bin/Debug-2.0/NHibernate.dll";
				pacoteTestes = @"/Users/otmarpereira/Documents/github-repos/nhibernate-core/src/NHibernate.Test/bin/Debug-2.0/NHibernate.Test.dll";
				clientLibPath = "/Users/otmarpereira/Downloads/Cuyahoga-1.7.0-bin/bin";
            }

            string libName = "NHibernate";

			var execMode = GetExecutionModeFromCommandLineArguments (args);

			if (execMode == ExecutionMode.None) {
				Console.WriteLine ("You must supply the argument -m followed by f to generate the FSM, t for NuSMV modules per unit test, c for NuSMV modules per class, s for formal specification.");
				return;
			}

            var tp = new TestsParser(pacoteTestes, pacoteBiblioteca);

            tp.Parse();

            
			if (execMode == ExecutionMode.GenerateFiniteStateMachine) {
				foreach (var fsm in tp.GetClassesFiniteStateMachines())
				{
					Console.WriteLine(fsm);
				}
			}
            
			if (execMode == ExecutionMode.ModulePerClass) {
				foreach (var mod in tp.GenerateNuSMVModules())
				{
					Console.WriteLine(mod);
				}
			}

			if (execMode == ExecutionMode.SingleModule) {
				var argsList = new List<string> (args);
				var index = argsList.IndexOf ("-m");
				if (argsList.Count < index + 2)
					Console.WriteLine ("To generate a single NuSMV module you must follow the m flag by the module name (example: -m m ClassA).");
				else {
					var className = argsList [index + 2];
					var mod = tp.GenerateNuSMVModule (className);
					Console.WriteLine (mod);
				}
			}

			if (execMode == ExecutionMode.ModulePerUnitTest) {
				foreach (var mod in tp.GenerateNuSMVModulesPerUnitTest()) {
					Console.WriteLine (mod);
				}
			}

			if (execMode == ExecutionMode.FormalSpecification) {
				var cp = new ClientLibraryParser (clientLibPath, libName);
				var formalSpecs = cp.GetSequenceOfCalls ();

				foreach (var spec in formalSpecs) {
					foreach (var call in spec) {
						Console.WriteLine (call);
					}
				}

				int contTests = 0;

				Console.WriteLine ("MODULE main");
				Console.WriteLine ("\t VAR ");
				foreach (var mod in cp.ModulesInSequenceOfCalls) {
					Console.WriteLine (String.Format ("\t \t test{0}_{1} : {1}();", ++contTests, mod));
				}
			}
        }
    }
}
