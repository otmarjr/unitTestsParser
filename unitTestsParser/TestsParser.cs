using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace unitTestsParser
{
    public class TestsParser
    {
        public TestsParser(string unitTestsDllPath, string testedLibraryDllPath)
        {
            this.resolver = new DefaultAssemblyResolver();

            this.resolver = new DefaultAssemblyResolver();

            this.resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(unitTestsDllPath));
            this.resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(testedLibraryDllPath));

            this.unitTestsAssembly = AssemblyDefinition.ReadAssembly(unitTestsDllPath, new ReaderParameters() { AssemblyResolver = this.resolver});
            this.libraryAssembly = AssemblyDefinition.ReadAssembly(testedLibraryDllPath, new ReaderParameters() { AssemblyResolver = this.resolver });
            
        }

        List<MethodDefinition> unitTestsMethods;

        
        private Predicate<MethodReference> IsAssertion = mr => mr.DeclaringType.Name.Equals("Assert");

        private AssemblyDefinition unitTestsAssembly;
        private AssemblyDefinition libraryAssembly;
        private BaseAssemblyResolver resolver;

        private void LoadUnitTestsEntryMethodsContainingAssertions()
        {
            var testClasses = unitTestsAssembly.Modules.SelectMany(m => m.Types).Cast<TypeDefinition>()
                                .Where(t => t.CustomAttributes.Any(a => a.AttributeType.Name.Equals("TestFixtureAttribute"))).ToList();


            unitTestsMethods = testClasses.SelectMany(t => t.Methods).Cast<MethodDefinition>()
                                .Where(m => m.HasBody)
                                .Where(m => m.CustomAttributes.Any(a => a.AttributeType.Name.Equals("TestAttribute")))
				.Where(m => m.Body.Instructions.Cast<Instruction>().Where(i=>ReflectionHelper.IsMethodCall(i)).Select(i => i.Operand as MethodReference).Where(mr => IsAssertion(mr)).Count() > 0)
                .ToList();
        }

        public void Parse()
        {
            this.LoadUnitTestsEntryMethodsContainingAssertions();

            foreach (var item in this.unitTestsMethods)
            {
                // Console.WriteLine(item.FullName);
            }

            this.LoadTestedClasses();
        }

        internal class TestCallSequence
        {
            public List<MethodReference> Sequence { get; set; }

            public List<MethodReference> Assertions {get;set;}
        }

        private List<TestCallSequence> testSequences;

        private void LoadTestedClasses()
        {
            this.testSequences = new List<TestCallSequence>();
            foreach (var md in this.unitTestsMethods)
            {
                var methodBodyCalls = md.Body.Instructions.Cast<Instruction>().Where(i => ReflectionHelper.IsMethodCall(i)).Select(i => i.Operand as MethodReference);
                var assertions = md.Body.Instructions.Cast<Instruction>().Where(i => ReflectionHelper.IsMethodCall(i)).Select(i => i.Operand as MethodReference).Where(mr => IsAssertion(mr)).ToList();
                var sequenceOfLibraryCalls
                    = methodBodyCalls.Except(assertions).Where(mr => mr.DeclaringType.Resolve().Module.FullyQualifiedName.Equals(this.libraryAssembly.MainModule.FullyQualifiedName)).ToList();
                if (sequenceOfLibraryCalls.Count > 0 )
                    this.testSequences.Add(new TestCallSequence() { Sequence = sequenceOfLibraryCalls, Assertions = assertions });
            }   
        }
		public List<string> GenerateNuSMVModulesPerUnitTest()
		{
            var modules = new List<string>();

            this.testSequences.OrderBy(ts => ReflectionHelper.MethodCallAsString(ts.Sequence.First().DeclaringType.Name, ts.Sequence.First().Name));

            var sbSFM = new StringBuilder();

            var methodTransitions = new Dictionary<string, List<Tuple<int, int>>>();
            var existingMethods = new List<string>();
            
            foreach (var unitTest in this.testSequences)
            {
                methodTransitions.Clear();
                existingMethods.Clear();
                
                var @class = unitTest.Sequence.First().DeclaringType.Name;
                var currentState = 1;
                var lastCreatedState = currentState;
                var transitions = new List<Tuple<int, int, string>>();
                var acceptingStates = new List<int>();

                sbSFM.Clear();
                sbSFM.AppendLine(string.Format("MODULE {0} (called_method)", ReflectionHelper.MethodCallAsString(unitTest.Sequence.First().DeclaringType.Name, unitTest.Sequence.First().Name)));
                sbSFM.AppendLine("\tVAR state : { s1");

                currentState = 1;

                foreach (var step in unitTest.Sequence)
                {
                    var newState = lastCreatedState + 1;
                    lastCreatedState = newState;
                    string calledMethod =  ReflectionHelper.MethodCallAsString(step.DeclaringType.Name, step.Name);
                    transitions.Add(new Tuple<int, int, string>(currentState, newState, calledMethod));
                    if (!existingMethods.Contains(calledMethod))
                        existingMethods.Add(calledMethod);

                    sbSFM.Append(", s" + newState);

                    if (!methodTransitions.ContainsKey(calledMethod))
                    {
                        methodTransitions[calledMethod] = new List<Tuple<int, int>>();
                    }

                    methodTransitions[calledMethod].Add(new Tuple<int, int>(currentState, newState));
                    currentState = newState;
                }

                acceptingStates.Add(lastCreatedState);

                sbSFM.Append("};");
                sbSFM.AppendLine();
                sbSFM.AppendLine(string.Format("-- accepting states: s{0}", string.Join(", s", acceptingStates)));
                sbSFM.Append("methods: {");
                sbSFM.Append(string.Join(",", existingMethods));
                sbSFM.AppendLine("} ;");
                sbSFM.AppendLine("ASSIGN");
                sbSFM.AppendLine("init(state) := s1;");
                sbSFM.AppendLine("next(state) := case ");

                foreach (var methodPaths in methodTransitions)
                {
                    foreach (var transition in methodPaths.Value)
                    {
                        sbSFM.AppendLine(string.Format("(called_method = {0}) & state = s{1} : s{2};", methodPaths.Key, transition.Item1, transition.Item2));
                    }
                }
                sbSFM.AppendLine("TRUE: state;");
                sbSFM.AppendLine("esac;");

                sbSFM.AppendLine("DEFINE");
                sbSFM.AppendLine("\tat_accepting_state := case");
                foreach (var acs in acceptingStates)
                {
                    sbSFM.AppendLine("\t\t state = s" + acs + " : TRUE;");
                }

                sbSFM.AppendLine("\t\t TRUE: FALSE; -- By default, not at accepting state.");
                sbSFM.AppendLine("esac;");


                modules.Add(sbSFM.ToString());
            }
            return modules;
		}

		public List<string> GenerateNuSMVModules()
		{
			var modules = new List<string> ();

			var testsPerClass = this.testSequences.GroupBy(ts => ts.Sequence.First().DeclaringType.Name).ToList();

			var sbSFM = new StringBuilder();

			var methodTransitions = new Dictionary<string, List<Tuple<int,int>>> ();
			var existingMethods = new List<string> ();
			foreach (var group in testsPerClass) 
			{
				var @class = group.Key;
				var currentState = 1;
				var lastCreatedState = currentState;
				var transitions = new List<Tuple<int, int, string>>();
				var acceptingStates = new List<int>();

				sbSFM.Clear ();
				sbSFM.AppendLine (string.Format("MODULE {0} (called_method)", @class.Replace("`", "_")));
				sbSFM.AppendLine("\tVAR state : { s1");

				foreach (var unitTest in group)
				{
					currentState = 1;

					foreach (var step in unitTest.Sequence)
					{
						var classOfFirstMethodInTheSequence = unitTest.Sequence.First ().DeclaringType.Name;

						if (!classOfFirstMethodInTheSequence.Equals (@class))
							continue;

						var newState = lastCreatedState + 1;
						lastCreatedState=newState;
						string calledMethod = string.Format("{0}_{1}",step.DeclaringType.Name.Replace("`", "_"), step.Name.Replace(".","_"));
						transitions.Add(new Tuple<int, int, string>(currentState, newState, calledMethod));
						if (!existingMethods.Contains (calledMethod))
							existingMethods.Add (calledMethod);

						sbSFM.Append (", s" + newState);

						if (!methodTransitions.ContainsKey(calledMethod)) {
							methodTransitions [calledMethod] = new List<Tuple<int, int>> ();
						}

						methodTransitions [calledMethod].Add (new Tuple<int, int> (currentState, newState));
						currentState = newState;
					}

					acceptingStates.Add(lastCreatedState);
				}

				sbSFM.Append ("};");
				sbSFM.AppendLine ();
                sbSFM.AppendLine(string.Format("-- accepting states: s{0}", string.Join(", s",acceptingStates)));
				sbSFM.Append ("methods: {");
				sbSFM.Append (string.Join (",", existingMethods));
				sbSFM.AppendLine ("} ;");
				sbSFM.AppendLine ("ASSIGN");
				sbSFM.AppendLine ("init(state) := s1;");
				sbSFM.AppendLine ("next(state) := case ");

				foreach (var methodPaths in methodTransitions) {
					foreach (var transition in methodPaths.Value) {
						sbSFM.AppendLine (string.Format("(called_method = {0}) & state = s{1} : s{2};", methodPaths.Key, transition.Item1, transition.Item2));
					}
				}
				sbSFM.AppendLine ("TRUE: state;");
				sbSFM.AppendLine ("esac;");

                sbSFM.AppendLine("DEFINE");
                sbSFM.AppendLine("\tat_accepting_state := case");
                foreach (var acs in acceptingStates)
                {
                    sbSFM.AppendLine("\t\t state = s" + acs + " : TRUE;");
                }

                sbSFM.AppendLine("\t\t TRUE: FALSE; -- By default, not at accepting state.");
                sbSFM.AppendLine("esac;");


				modules.Add(sbSFM.ToString());
			}
			return modules;
		}

        public List<string> GetClassesFiniteStateMachines()
        {
            List<string> fsms = new List<string>();

            var testsPerClass = this.testSequences.GroupBy(ts => ts.Sequence.First().DeclaringType.Name).ToList();

            var sbSFM = new StringBuilder();

            int currentMachine = 0;
            foreach (var group in testsPerClass)
            {
                var @class = group.Key;
                var currentState = 1;
                var lastCreatedState = currentState;
                var transitions = new List<Tuple<int, int, string>>();
                var acceptingStates = new List<int>();
                var initialStates = new List<int>() { currentState };

                foreach (var unitTest in group)
                {
                    currentState = 1;

                    foreach (var step in unitTest.Sequence)
                    {
                        var newState = lastCreatedState + 1;
                        lastCreatedState=newState;
                        string calledMethod = step.FullName;
                        transitions.Add(new Tuple<int, int, string>(currentState, newState, calledMethod));
                        currentState = newState;
                    }

                    acceptingStates.Add(lastCreatedState);
                }

                sbSFM.Clear();
                
                sbSFM.AppendLine(string.Format("FSM {0} - {1}", currentMachine, group.Key));
                sbSFM.Append("S = {");
                sbSFM.Append(string.Join<int>(",",Enumerable.Range(1,lastCreatedState)));
                sbSFM.Append("}");
                sbSFM.Append(Environment.NewLine);
                sbSFM.AppendLine("I={1}");
                sbSFM.Append(string.Format("F = {{"));
                sbSFM.Append(string.Join<int>(",", acceptingStates));
                sbSFM.Append("}");
                sbSFM.Append(Environment.NewLine);
                sbSFM.AppendLine("δ  = {");
                transitions.ForEach(t => sbSFM.AppendLine(string.Format("{0} -- {1} -- > {2}", t.Item1, t.Item3, t.Item2)));
                sbSFM.AppendLine("}");

                fsms.Add(sbSFM.ToString());
                currentMachine = currentMachine + 1;
            }

            return fsms;
        }
    }
}
