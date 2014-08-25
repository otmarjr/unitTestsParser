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
    
        static void Main(string[] args)
        {
            // string pacoteBiblioteca = @"E:\\github-repos\\nhibernate\\src\\NHibernate.Test\\bin\\Debug-2.0\NHibernate.dll";
            // string pacoteTestes = @"E:\\github-repos\\nhibernate\\src\\NHibernate.Test\\bin\\Debug-2.0\NHibernate.Test.dll";

			string pacoteBiblioteca = @"/Users/otmarpereira/Documents/github-repos/nhibernate-core/src/NHibernate.Test/bin/Debug-2.0/NHibernate.dll"; 
			string pacoteTestes = @"/Users/otmarpereira/Documents/github-repos/nhibernate-core/src/NHibernate.Test/bin/Debug-2.0/NHibernate.Test.dll";

            var tp = new TestsParser(pacoteTestes, pacoteBiblioteca);

            tp.Parse();

			/*
            foreach (var fsm in tp.GetClassesFiniteStateMachines())
            {
                Console.WriteLine(fsm);
            }

			foreach (var mod in tp.GenerateNuSMVModules())
			{
				Console.WriteLine(mod);
			}
			*/

			var cp = new ClientLibraryParser ("/Users/otmarpereira/Downloads/Cuyahoga-1.7.0-bin/bin", "NHibernate");
			var formalSpecs = cp.GetSequenceOfCalls ();

            // Console.ReadKey();
        }
    }
}
