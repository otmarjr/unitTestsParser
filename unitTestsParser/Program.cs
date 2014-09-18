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
            string rootNamespace = "Spring.Data.NHibernate";
			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                pacoteBiblioteca = @"E:\\github-repos\\nhibernate-3.3.x\\src\\NHibernate.Test\\bin\\Debug-2.0\\NHibernate.dll";
                pacoteTestes = @"E:\\github-repos\\nhibernate-3.3.x\\src\\NHibernate.Test\\bin\\Debug-2.0\NHibernate.Test.dll";
                clientLibPath = @"e:\github-repos\Spring.Northwind\Spring.Northwind\src\Spring.Northwind.Web\bin";
                // C:\Users\Otmar\Downloads\Orchard.Web.1.8.1\Orchard\bin";
                // C:\Users\Otmar\Downloads\Orchard.Web.1.8.1\Orchard\bin
                // C:\Users\Otmar\Downloads\Spring.Northwind\Spring.Northwind\src\Spring.Northwind.Web\bin // Spring.Data.NHibernate
                // C:\Users\Otmar\Google Drive\Mestrado\SpecMining\nhPaper\unitTestsParser\unitTestsParser\unitTestsParser\bin\Debug
                // C:\Users\Otmar\Downloads\FunnelWeb-master\FunnelWeb-master\build\Published\bin";
                // C:\Users\Otmar\Source\Repos\Who-Can-Help-Me\Solutions\MSpecTests.WhoCanHelpMe\bin\Debug 
                // C:\Users\Otmar\Source\Repos\rhino-security\Rhino.Security\bin\Debug
                // C:\Users\Otmar\Source\Repos\fluent-nhibernate\src\FluentNHibernate.Testing\bin\Debug 
                // C:\Users\Otmar\Downloads\SharpArch.dlls.v2.0.4 
                // C:\\Users\\Otmar\\Downloads\\Cuyahoga-1.7.0-bin\\bin 
                // C:\Users\Otmar\Downloads\FunnelWeb-master\FunnelWeb-master\build\Published\bin 
                // C:\Users\Otmar\Source\Repos\UCDArch\SampleUCDArchApp\SampleUCDArchApp.Core\bin\Debug
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
                var argsList = new List<string>(args);
                var index = argsList.IndexOf("-m");
                if (argsList.Count < index + 2)
                {
                    Console.WriteLine("To generate the formal specification file, you must supply the nusmv modules file after the 's' flag.(example: -m s modules.smv).");
                    return;
                }

                var modulesPath = argsList[index + 2];

                if (!System.IO.File.Exists(modulesPath))
                {
                    Console.WriteLine("Could not locate file at path '{0}'.", modulesPath);
                    return;
                }
				var cp = new ClientLibraryParser (clientLibPath, libName, rootNamespace);
				var formalSpecs = cp.GetSequenceOfCalls (modulesPath);

				var nusmvLibSpecLines = new List<String>(System.IO.File.ReadAllLines (modulesPath));

                var clientMethodsOfTestedClasses = cp.ModulesInSequenceOfCalls.Where(m => cp.ModulesUsedWithSpecification.Contains(cp.ModuleInstancesInsideSequences[m])).Count();
                var clientMethodsOfNonTestedClasses = cp.ClassesWithoutSpecificationClients.Sum(c => c.Value.Count);

                var totalTestedClassesUsed = cp.ModulesUsedWithSpecification.Distinct().Where(m => cp.ModuleInstancesInsideSequences.ContainsValue(m)).Count();
                var totalNonTestedClassesUsed = cp.ModulesUsedWithoutSpecification.Distinct().Where(m => cp.ModuleInstancesInsideSequences.ContainsValue(m)).Count();

                Console.WriteLine("--Total client methods:  " + (clientMethodsOfTestedClasses + clientMethodsOfNonTestedClasses) + " Client methods of tested classes: " + clientMethodsOfTestedClasses + " Client methods of untested classes : " + clientMethodsOfNonTestedClasses);
                Console.WriteLine("--Total of library classes used:  " + (totalTestedClassesUsed + totalNonTestedClassesUsed) + " Total tested classes: " + totalTestedClassesUsed + " Non-tested classes used: " + totalNonTestedClassesUsed);

				foreach (var usedClassWithoutTest in cp.ModulesUsedWithoutSpecification.Distinct())
                {
                    Console.WriteLine("-- Class used without specification by " + cp.ClassesWithoutSpecificationClients[usedClassWithoutTest].Count  + " client methods: " + usedClassWithoutTest + " present in the following methods: " + string.Join(",", cp.ClassesWithoutSpecificationClients[usedClassWithoutTest]));
                }

                var stats = tp.CalculateStatistics(cp, formalSpecs);

                stats.ForEach(l => Console.WriteLine(l));
                Console.WriteLine("--Library modules:");
                nusmvLibSpecLines.SkipWhile(l => l.StartsWith("--")).ToList().ForEach(l => Console.WriteLine(l));

                Console.WriteLine("--Client modules:");
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
