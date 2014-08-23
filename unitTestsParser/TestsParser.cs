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

        private Predicate<Instruction> IsMethodCall = i => i.Operand != null && i.Operand as MethodReference != null;
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
                                .Where(m => m.Body.Instructions.Cast<Instruction>().Where(i=>IsMethodCall(i)).Select(i => i.Operand as MethodReference).Where(mr => IsAssertion(mr)).Count() > 0)
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
            foreach (var md in this.unitTestsMethods)
            {
                var methodBodyCalls = md.Body.Instructions.Cast<Instruction>().Where(i => IsMethodCall(i)).Select(i => i.Operand as MethodReference);
                var assertions = md.Body.Instructions.Cast<Instruction>().Where(i => IsMethodCall(i)).Select(i => i.Operand as MethodReference).Where(mr => IsAssertion(mr)).ToList();
                var sequenceOfLibraryCalls
                    = methodBodyCalls.Except(assertions).Where(mr => mr.DeclaringType.Resolve().Module.FullyQualifiedName.Equals(this.libraryAssembly.MainModule.FullyQualifiedName)).ToList();

                
                var testSequence = new TestCallSequence() { Sequence = sequenceOfLibraryCalls, Assertions = assertions };
            }   
        }
    }
}
