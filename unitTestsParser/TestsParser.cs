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
			this.LoadTestedClasses();
		}

		internal class TestCallSequence
		{
			public MethodDefinition OriginalUnitTest { get; set; }

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
				= methodBodyCalls.Except(assertions).Where(mr => mr.DeclaringType.Resolve() != null && mr.DeclaringType.Resolve().Module.FullyQualifiedName.Equals(this.libraryAssembly.MainModule.FullyQualifiedName)).ToList();

				if (sequenceOfLibraryCalls.Count > 0 )
					this.testSequences.Add(new TestCallSequence() { Sequence = sequenceOfLibraryCalls, Assertions = assertions, OriginalUnitTest = md });
			}   
		}
		public List<string> GenerateNuSMVModulesPerUnitTest()
		{
			var modules = new List<string>();

			this.testSequences = this.testSequences.OrderBy(ts => ReflectionHelper.MethodCallAsString(ts.Sequence.First().DeclaringType.Name, ts.Sequence.First().Name)).ToList();

			var sbSFM = new StringBuilder();

			var methodTransitions = new Dictionary<string, List<Tuple<int, int>>>();
			var existingMethods = new List<string>();


			var unitTestOccurrences = new Dictionary<string, int> ();

			foreach (var unitTest in this.testSequences)
			{
				methodTransitions.Clear();
				existingMethods.Clear();

				var @class = unitTest.Sequence.First().DeclaringType.Name;
				var currentState = 1;
				var lastCreatedState = currentState;
				var transitions = new List<Tuple<int, int, string>>();
				var acceptingStates = new List<int>();

				var moduleName = ReflectionHelper.MethodCallAsString (unitTest.Sequence.First ().DeclaringType.Name, unitTest.Sequence.First ().Name);
				int testIndex = 1;

				if (unitTestOccurrences.ContainsKey (moduleName)) {
					testIndex = unitTestOccurrences [moduleName] + 1;
				}

				unitTestOccurrences [moduleName] = testIndex;

				sbSFM.Clear();
				sbSFM.AppendLine(string.Format("MODULE {0}_{1} (called_method) -- original unit test: {2}", moduleName,testIndex, ReflectionHelper.MethodCallAsString(unitTest.OriginalUnitTest.DeclaringType.Name, unitTest.OriginalUnitTest.Name) ));
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

				foreach (var assert in unitTest.Assertions) 
				{
					var sb = new StringBuilder ();

					sb.Append (assert.Name);
					sb.Append (" (");

					/* foreach (var p in assert.Parameters) {
						sb.Append (assert.Resolve().Parameters[p.Index].Name);
						sb.Append (" = ");
						sb.Append (ReflectionHelper.ResolveParameterValue (p, assert, unitTest.OriginalUnitTest));
					}*/

					sb.Append (" )");

					// sbSFM.AppendLine ("--Assertion: " + sb.ToString());		
					// sbSFM.AppendLine ("-- Assertion: " + ReflectionHelper.LogValues(assert.Resolve()));
				}

				modules.Add(sbSFM.ToString());
			}
			return modules;
		}

		public string GenerateNuSMVModule(string className)
		{
			var testsPerClass = this.testSequences.Where(ts => ts.Sequence.First().DeclaringType.Name.Equals(className, StringComparison.InvariantCultureIgnoreCase)).GroupBy(ts => ts.Sequence.First ().DeclaringType.Name).ToList ();

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
				var transitionTests = new Dictionary<Tuple<int,int,string>, List<TestCallSequence>> ();
				var transitionTestsCursor = new Dictionary<Tuple<int,int,string>, int> ();
				var transtionMethods = new Dictionary<Tuple<int,int,string>, MethodDefinition> ();
				var acceptingStatesTests = new Dictionary<int, List<TestCallSequence>> ();

				foreach (var unitTest in group)
				{
					currentState = 1;

					foreach (var step in unitTest.Sequence)
					{
						var classOfFirstMethodInTheSequence = unitTest.Sequence.First ().DeclaringType.Name;

						if (!classOfFirstMethodInTheSequence.Equals (@class))
							continue;

						string calledMethod = string.Format("{0}_{1}",step.DeclaringType.Name.Replace("`", "_"), step.Name.Replace(".","_"));

						int nextState;

						var targetState = transitions.Find(t => t.Item1 == currentState && t.Item3.Equals(calledMethod));

						if (targetState != null)
						{
							nextState = targetState.Item2;
						}
						else
						{
							nextState = lastCreatedState + 1;
							lastCreatedState = nextState;
							sbSFM.Append(", s" + nextState);
							if (!methodTransitions.ContainsKey(calledMethod))
							{
								methodTransitions[calledMethod] = new List<Tuple<int, int>>();
							}
							methodTransitions[calledMethod].Add(new Tuple<int, int>(currentState, nextState));
						}

						var transition = new Tuple<int, int, string> (currentState, nextState, calledMethod); 
						transitions.Add(transition);
						if (!existingMethods.Contains (calledMethod))
							existingMethods.Add (calledMethod);

						currentState = nextState;

						if (!transitionTests.ContainsKey (transition)) {
							transitionTests.Add (transition, new List<TestCallSequence> ());
							transitionTestsCursor.Add (transition, 0);
						}

						if (!transtionMethods.ContainsKey (transition)) 
						{
							transtionMethods.Add (transition, step.Resolve ());
						}

						transitionTests[transition].Add(unitTest);
						if (step.Equals(unitTest.Sequence.Last()))
						{
							acceptingStates.Add(nextState);
							if (!acceptingStatesTests.ContainsKey (nextState)) {
								acceptingStatesTests.Add (nextState, new List<TestCallSequence> ());
							} 

							acceptingStatesTests [nextState].Add (unitTest);
						}
					}
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


				foreach (var t in transitions) {
					var index = transitionTestsCursor [t];
					if (t.Item1 == 1) {
						var test = transitionTests [t][index].OriginalUnitTest;
						sbSFM.AppendLine (String.Format ("--Calls made in test : {0}", ReflectionHelper.MethodCallAsString(test.DeclaringType.Name, test.Name)));
					}

					var @params = new List<String> ();
					foreach (var p in transtionMethods[t].Parameters) {
						@params.Add(p.Name + ": " + p.ParameterType.Name);
					}

					sbSFM.AppendLine (string.Format("(called_method = {0}) & state = s{1} : s{2}; -- method parameters: {3} method return type: {4}", t.Item3, t.Item1, t.Item2, string.Join(", ",@params), transtionMethods[t].ReturnType.Name));
					bool isLastTransitionOfTest = false;

					if (t.Equals (transitions.Last ())) {	
						isLastTransitionOfTest = true;
					} else {
						var position = transitions.IndexOf(t);
						var nextTransiton = transitions [position + 1];

						if (nextTransiton.Item1 == 1) {
							isLastTransitionOfTest = true;
						}
					}
					if (acceptingStates.Contains (t.Item2) && isLastTransitionOfTest) {
						foreach (var a in transitionTests[t][index].Assertions) {
							@params.Clear ();
							foreach (var p in a.Resolve().Parameters) {
								@params.Add(p.Name + ": " + p.ParameterType.Name);
							}
							sbSFM.AppendLine	 (String.Format ("-- Assertion :  {0}.{1} ({2}) ", a.DeclaringType.Name, a.Name, string.Join(", ",@params) ));
						}
						transitionTestsCursor [t] += 1;
					}
				}
				/*
				foreach (var methodPaths in methodTransitions) {
					foreach (var transition in methodPaths.Value) {
						sbSFM.AppendLine (string.Format("(called_method = {0}) & state = s{1} : s{2};", methodPaths.Key, transition.Item1, transition.Item2));
					}
				}*/
				sbSFM.AppendLine ("TRUE: state;");
				sbSFM.AppendLine ("esac;");

				sbSFM.AppendLine("DEFINE");
				sbSFM.AppendLine("\tat_accepting_state := case");
				foreach (var acs in acceptingStates)
				{
					sbSFM.AppendLine("\t\t state = s" + acs + " : TRUE; -- from unit test(s) " + string.Join(", ", acceptingStatesTests[acs].Select(ut => ReflectionHelper.MethodCallAsString(ut.OriginalUnitTest.DeclaringType.Name, ut.OriginalUnitTest.Name)).ToList()));
				}

				sbSFM.AppendLine("\t\t TRUE: FALSE; -- By default, not at accepting state.");
				sbSFM.AppendLine("esac;");
			}

			return sbSFM.ToString ();
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
				var transitionTests = new Dictionary<Tuple<int,int,string>, List<TestCallSequence>> ();
				var transitionTestsCursor = new Dictionary<Tuple<int,int,string>, int> ();
				var transtionMethods = new Dictionary<Tuple<int,int,string>, MethodDefinition> ();
				var acceptingStatesTests = new Dictionary<int, List<TestCallSequence>> ();

				foreach (var unitTest in group)
				{
					currentState = 1;

					foreach (var step in unitTest.Sequence)
					{
						var classOfFirstMethodInTheSequence = unitTest.Sequence.First ().DeclaringType.Name;

						if (!classOfFirstMethodInTheSequence.Equals (@class))
							continue;

						string calledMethod = string.Format("{0}_{1}",step.DeclaringType.Name.Replace("`", "_"), step.Name.Replace(".","_"));

						int nextState;

						var targetState = transitions.Find(t => t.Item1 == currentState && t.Item3.Equals(calledMethod));

						if (targetState != null)
						{
							nextState = targetState.Item2;
						}
						else
						{
							nextState = lastCreatedState + 1;
							lastCreatedState = nextState;
							sbSFM.Append(", s" + nextState);
							if (!methodTransitions.ContainsKey(calledMethod))
							{
								methodTransitions[calledMethod] = new List<Tuple<int, int>>();
							}
							methodTransitions[calledMethod].Add(new Tuple<int, int>(currentState, nextState));
						}

						var transition = new Tuple<int, int, string> (currentState, nextState, calledMethod); 
						transitions.Add(transition);
						if (!existingMethods.Contains (calledMethod))
							existingMethods.Add (calledMethod);

						currentState = nextState;

						if (!transitionTests.ContainsKey (transition)) {
							transitionTests.Add (transition, new List<TestCallSequence> ());
							transitionTestsCursor.Add (transition, 0);
						}

						if (!transtionMethods.ContainsKey (transition)) 
						{
							transtionMethods.Add (transition, step.Resolve ());
						}

						transitionTests[transition].Add(unitTest);
						if (step.Equals(unitTest.Sequence.Last()))
						{
							acceptingStates.Add(nextState);
							if (!acceptingStatesTests.ContainsKey (nextState)) {
								acceptingStatesTests.Add (nextState, new List<TestCallSequence> ());
							} 

							acceptingStatesTests [nextState].Add (unitTest);
						}
					}
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


				foreach (var t in transitions) {
					var index = transitionTestsCursor [t];
					if (t.Item1 == 1) {
						var test = transitionTests [t][index].OriginalUnitTest;
						sbSFM.AppendLine (String.Format ("--Calls made in test : {0}", ReflectionHelper.MethodCallAsString(test.DeclaringType.Name, test.Name)));
					}

					var @params = new List<String> ();
					foreach (var p in transtionMethods[t].Parameters) {
						@params.Add(p.Name + ": " + p.ParameterType.Name);
					}

					sbSFM.AppendLine (string.Format("(called_method = {0}) & state = s{1} : s{2}; -- method parameters: {3} method return type: {4}", t.Item3, t.Item1, t.Item2, string.Join(", ",@params), transtionMethods[t].ReturnType.Name));
					if (acceptingStates.Contains (t.Item2)) {
						foreach (var a in transitionTests[t][index].Assertions) {
							@params.Clear ();
							foreach (var p in a.Resolve().Parameters) {
								@params.Add(p.Name + ": " + p.ParameterType.Name);
							}
							sbSFM.AppendLine	 (String.Format ("-- Assertion :  {0}.{1} ({2}) ", a.DeclaringType.Name, a.Name, string.Join(", ",@params) ));
						}
						transitionTestsCursor [t] += 1;
					}
				}
				/*
				foreach (var methodPaths in methodTransitions) {
					foreach (var transition in methodPaths.Value) {
						sbSFM.AppendLine (string.Format("(called_method = {0}) & state = s{1} : s{2};", methodPaths.Key, transition.Item1, transition.Item2));
					}
				}*/
				sbSFM.AppendLine ("TRUE: state;");
				sbSFM.AppendLine ("esac;");

				sbSFM.AppendLine("DEFINE");
				sbSFM.AppendLine("\tat_accepting_state := case");
				foreach (var acs in acceptingStates)
				{
					sbSFM.AppendLine("\t\t state = s" + acs + " : TRUE; -- from unit test(s) " + string.Join(", ", acceptingStatesTests[acs].Select(ut => ReflectionHelper.MethodCallAsString(ut.OriginalUnitTest.DeclaringType.Name, ut.OriginalUnitTest.Name)).ToList()));
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

			var testsPerClass = this.testSequences.GroupBy(ts => ts.Sequence.First().DeclaringType.Name).OrderBy(g => g.Key).ToList();

			var sbSFM = new StringBuilder();

			var transitionTests = new Dictionary<Tuple<int,int,string>, TestCallSequence > ();
			int currentMachine = 0;
			foreach (var group in testsPerClass)
			{
				var @class = group.Key;
				var currentState = 1;
				var lastCreatedState = currentState;
				var transitions = new List<Tuple<int, int, string>>();
				var acceptingStates = new List<int>();

				foreach (var unitTest in group)
				{
					currentState = 1;

					Dictionary<string, int> methodReferenceOccurences = new Dictionary<string, int>();

					for (int i =0;i<unitTest.Sequence.Count;++i)
					{
						var step = unitTest.Sequence[i];

						string calledMethod = ReflectionHelper.MethodCallAsString(step.DeclaringType.Name, step.Name);

						if (!methodReferenceOccurences.ContainsKey(step.FullName))
						{
							methodReferenceOccurences.Add(step.FullName, 0);
						}

						var @params = new List<string>();
						foreach (var arg in step.Resolve().Parameters)
						{   
							if (!ReflectionHelper.IsExtensionMethodFirstArgument(step, arg))
							{
								@params.Add(string.Format("{0} : {1}", arg.Name, arg.Resolve().ParameterType.Name));
							}
						}

						methodReferenceOccurences[step.FullName] += 1;

						calledMethod = calledMethod + "(" + string.Join(",", @params) + ")";

						int nextState;

						var targetState = transitions.Find(t => t.Item1 == currentState && t.Item3.Equals(calledMethod));

						if (targetState != null)
						{
							nextState = targetState.Item2;
						}
						else
						{
							nextState = lastCreatedState + 1;
							lastCreatedState = nextState;
							sbSFM.Append(", s" + nextState);
						}

						var transition = new Tuple<int, int, string> (currentState, nextState, calledMethod); 

						if (currentState == 1) {
							transitionTests[transition] = unitTest;
						}

						transitions.Add(transition);
						currentState = nextState;

						if (i == unitTest.Sequence.Count - 1) {
							acceptingStates.Add (currentState);
						}
					}
				}

				sbSFM.Clear();

				sbSFM.AppendLine(string.Format("FSM {0} - {1}", currentMachine, group.Key));
				sbSFM.Append("S = {s");
				sbSFM.Append(string.Join<int>(",s",Enumerable.Range(1,lastCreatedState)));
				sbSFM.Append("}");
				sbSFM.Append(Environment.NewLine);
				sbSFM.AppendLine("I={s1}");
				sbSFM.Append(string.Format("F = {{s"));
				sbSFM.Append(string.Join<int>(",s", acceptingStates));
				sbSFM.Append("}");
				sbSFM.Append(Environment.NewLine);
				sbSFM.AppendLine("δ  = {");
				transitions.ForEach(t => {
					if (transitionTests.ContainsKey(t)) sbSFM.AppendLine("//Transitions originally in unit test(s) : " + transitionTests[t].OriginalUnitTest.FullName);
					sbSFM.AppendLine(string.Format("s{0} --  called_method = {1} -- > s{2}", t.Item1, t.Item3, t.Item2));
				});
				sbSFM.AppendLine("}");

				fsms.Add(sbSFM.ToString());
				currentMachine = currentMachine + 1;
			}

			return fsms;
		}
	}
}
