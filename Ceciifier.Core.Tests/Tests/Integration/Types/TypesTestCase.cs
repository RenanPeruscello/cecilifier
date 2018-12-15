﻿using NUnit.Framework;

namespace Cecilifier.Core.Tests.Integration.Types
{
	[TestFixture]
	class TypesTestCase : IntegrationResourceBasedTest
	{
		[Test]
		public void SimplestTest()
		{
			AssertResourceTest(@"Types/Simplest");
		}

		[Test]
		public void SealedClassTest()
		{
			AssertResourceTest(@"Types/SealedClass");
		}

		[Test]
		public void AbstractClassTest()
		{
			AssertResourceTest(@"Types/AbstractClass");
		}
		
		[Test]
		public void PartialClassTest()
		{
			AssertResourceTest(@"Types/PartialClass");
		}

		[Test]
		public void InnerClassTest()
		{
			AssertResourceTest(@"Types/InnerClass");
		}

		[Test]
		public void InterfaceImplementationTest()
		{
			AssertResourceTest(@"Types/InterfaceImplementation");
		}
		
		[Test]
		public void ExplicityInterfaceImplementationTest()
		{
			AssertResourceTest(@"Types/ExplicityInterfaceImplementation");
		}
		
		[Test]
		public void InheritanceTest()
		{
			AssertResourceTest(@"Types/Inheritance");
		}

		[Test]
		public void MultipleInterfaceImplementationTest()
		{
			AssertResourceTest(@"Types/MultipleInterfaceImplementation");
		}

		[Test]
		public void InheritanceSameCompilationUnitTest()
		{
			AssertResourceTest(@"Types/InheritanceSameCompilationUnit");
		}
		
		[Test]
		public void InterfaceDefinitionTest()
		{
			AssertResourceTest(@"Types/InterfaceDefinition");
		}
		
		[Test]
		public void SimpleValueTypeTest()
		{
			AssertResourceTest(@"Types/SimpleValueType");
		}
		
		[TestCase("SimpleTypeWithAttribute")]
		[TestCase("AttributeWithProperty")]
		[TestCase("AttributeFromSameAssembly")]
		public void AttributeTests(string typeName)
		{
			AssertResourceTest($@"Types/{typeName}");	
		}

		[Test, Ignore("Not implemented yet")]
		public void TypeInitializeTest()
		{
			AssertResourceTest(@"Types/TypeInitializer");
		}

		[Test, Ignore("Not supported yet")]
		public void ForwardTypeReferenceTest()
		{
			AssertResourceTest(@"Types/ForwardTypeReference");
		}
	}
}