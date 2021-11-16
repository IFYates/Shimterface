﻿using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Shimterface.Tests
{
	[TestClass]
	public class StaticMemberTests
	{
		[ExcludeFromCodeCoverage]
		public class StaticMemberClass
		{
			public static string Value { get; set; }

			public static bool _HasCalled = false;
			public static void Test()
			{
				_HasCalled = true;
			}
		}
		
		public interface IStaticMethod
		{
			[StaticShim(typeof(StaticMemberClass))]
			void Test();
		}

		public interface IStaticAliasedMethod
		{
			[Shim("Test"), StaticShim(typeof(StaticMemberClass))]
			void AnotherTest();
		}

		public interface IStaticProperty
		{
			[StaticShim(typeof(StaticMemberClass))]
			string Value { get; set; }
		}

		public interface IBadStaticMethod
		{
			void Test();
		}

		[TestInitialize]
		public void ResetState()
		{
			ShimBuilder.ResetState();
			StaticMemberClass._HasCalled = false;
		}

		[TestMethod]
		public void Can_define_static_method()
		{
			var factory = ShimBuilder.Create<IStaticMethod>();

			Assert.IsFalse(StaticMemberClass._HasCalled);
			factory.Test();
			Assert.IsTrue(StaticMemberClass._HasCalled);
		}

		[TestMethod]
		public void Can_alias_static_method()
		{
			var factory = ShimBuilder.Create<IStaticAliasedMethod>();

			Assert.IsFalse(StaticMemberClass._HasCalled);
			factory.AnotherTest();
			Assert.IsTrue(StaticMemberClass._HasCalled);
		}

		[TestMethod]
		public void Can_define_static_property()
		{
			var factory = ShimBuilder.Create<IStaticProperty>();

			Assert.IsNull(StaticMemberClass.Value);
			Assert.IsNull(factory.Value);
			StaticMemberClass.Value = "one";
			Assert.AreEqual("one", factory.Value);
			factory.Value = "two";
			Assert.AreEqual("two", StaticMemberClass.Value);
		}

		[TestMethod]
		public void Must_only_define_static_methods()
		{
			var ex = Assert.ThrowsException<InvalidCastException>(() =>
			{
				ShimBuilder.Create<IBadStaticMethod>();
			});

			Assert.AreEqual("Factory shim cannot implement non-static member: Shimterface.Tests.StaticMemberTests+IBadStaticMethod Test", ex.Message);
		}

		[TestMethod]
		public void Normal_shims_cannot_use_StaticShimAttribute()
		{
			var ex = Assert.ThrowsException<InvalidCastException>(() =>
			{
				ShimBuilder.Shim<IStaticMethod>(new object());
			});

			Assert.AreEqual("Instance shim cannot implement static member: Shimterface.Tests.StaticMemberTests+IStaticMethod Test", ex.Message);
		}
	}
}
