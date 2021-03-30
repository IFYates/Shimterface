﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Shimterface.Tests
{
	/// <summary>
	/// Tests around extending/replacing shim functionality
	/// https://github.com/IanYates83/Shimterface/issues/3
	/// </summary>
	[TestClass]
	public class ExtendedFunctionalityTests_Property
	{
		public interface ITestShim
		{
		}

		[ExcludeFromCodeCoverage]
		public class TestClass_NoProperty
		{
		}

		[ExcludeFromCodeCoverage]
		public class TestClass_HasProperty
		{
			public string Property { get; set; }
		}

		public interface ITestShim_AddProperty
		{
			[ShimProxy(typeof(TestImpl_AddProperty), ProxyBehaviour.Add)]
			string Property { get; set; }
		}
		[ExcludeFromCodeCoverage]
		public class TestImpl_AddProperty
		{
			public static string Property { get; set; }
		}

		[TestMethod]
		public void Can_add_property_proxy()
		{
			// Arrange
			var obj = new TestClass_NoProperty();
			var shim = obj.Shim<ITestShim_AddProperty>();

			// Act
			shim.Property = "test";

			// Assert
			Assert.AreEqual("test", TestImpl_AddProperty.Property);
		}
		
		public interface ITestShim_AddPropertyDefault
		{
			[ShimProxy(typeof(TestImpl_AddPropertyDefault))]
			string Property { get; set; }
		}
		[ExcludeFromCodeCoverage]
		public class TestImpl_AddPropertyDefault
		{
			public static string Property { get; set; }
		}

		[TestMethod]
		public void Can_add_property_proxy_by_default()
		{
			// Arrange
			var obj = new TestClass_NoProperty();
			var shim = obj.Shim<ITestShim_AddPropertyDefault>();

			// Act
			shim.Property = "test";

			// Assert
			Assert.AreEqual("test", TestImpl_AddPropertyDefault.Property);
		}

		[TestMethod]
		public void Cannot_add_existing_property()
		{
			// Arrange
			var obj = new TestClass_HasProperty();

			// Act
			Assert.ThrowsException<InvalidCastException>(() =>
			{
				obj.Shim<ITestShim_AddProperty>();
			});
		}
		
		public interface ITestShim_OverrideProperty
		{
			[ShimProxy(typeof(TestImpl_OverrideProperty), ProxyBehaviour.Override)]
			string Property { get; set; }
		}
		[ExcludeFromCodeCoverage]
		public class TestImpl_OverrideProperty
		{
			public static string Property { get; set; }
		}

		[TestMethod]
		public void Can_override_property_proxy()
		{
			// Arrange
			var obj = new TestClass_HasProperty();
			var shim = obj.Shim<ITestShim_OverrideProperty>();

			// Act
			shim.Property = "test";

			// Assert
			Assert.IsNull(obj.Property);
			Assert.AreSame("test", TestImpl_OverrideProperty.Property);
		}

		public interface ITestShim_OverridePropertyDefault
		{
			[ShimProxy(typeof(TestImpl_OverridePropertyDefault))]
			string Property { get; set; }
		}
		[ExcludeFromCodeCoverage]
		public class TestImpl_OverridePropertyDefault
		{
			public static string Property { get; set; }
		}

		[TestMethod]
		public void Can_override_property_proxy_by_default()
		{
			// Arrange
			var obj = new TestClass_HasProperty();
			var shim = obj.Shim<ITestShim_OverridePropertyDefault>();

			// Act
			shim.Property = "test";

			// Assert
			Assert.IsNull(obj.Property);
			Assert.AreSame("test", TestImpl_OverridePropertyDefault.Property);
		}

		[TestMethod]
		public void Cannot_override_missing_property()
		{
			// Arrange
			var obj = new TestClass_NoProperty();

			// Act
			Assert.ThrowsException<InvalidCastException>(() =>
			{
				obj.Shim<ITestShim_OverrideProperty>();
			});
		}
	}
}
