﻿using System;
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
            string pacoteBiblioteca = @"E:\\github-repos\\nhibernate\\src\\NHibernate.Test\\bin\\Debug-2.0\NHibernate.dll";
            string pacoteTestes = @"E:\\github-repos\\nhibernate\\src\\NHibernate.Test\\bin\\Debug-2.0\NHibernate.Test.dll";

			//string pacoteBiblioteca = @"/Users/otmarpereira/Documents/github-repos/nhibernate-core/src/NHibernate.Test/bin/Debug-2.0/NHibernate.dll"; 
			//string pacoteTestes = @"/Users/otmarpereira/Documents/github-repos/nhibernate-core/src/NHibernate.Test/bin/Debug-2.0/NHibernate.Test.dll";

            var tp = new TestsParser(pacoteTestes, pacoteBiblioteca);

            tp.Parse();

			/*
            foreach (var fsm in tp.GetClassesFiniteStateMachines())
            {
                Console.WriteLine(fsm);
            }
            */

            //foreach (var mod in tp.GenerateNuSMVModules())
            foreach (var mod in tp.GenerateNuSMVModulesPerUnitTest())
			{
				Console.WriteLine(mod);
			}
			/**/

            var cp = new ClientLibraryParser(@"C:\\Users\\Otmar\\Downloads\\Cuyahoga-1.7.0-bin\\bin", "NHibernate"); // ("/Users/otmarpereira/Downloads/Cuyahoga-1.7.0-bin/bin", "NHibernate");
			var formalSpecs = cp.GetSequenceOfCalls ();

			int cont_sepc = 1;
			foreach (var spec in formalSpecs) {
				// Console.WriteLine ("=>Spec " + cont_sepc++);
				foreach (var call in spec) {
					// Console.WriteLine ("\t" + call);
				}
			}

            // Console.ReadKey();
        }
    }
}
