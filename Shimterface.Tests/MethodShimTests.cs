﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Shimterface.Tests
{
    [TestClass]
    public class MethodShimTests
    {
        public interface IVoidMethodTest
        {
            void VoidMethod();
        }
        public interface IVoidMethodArgsTest
        {
            void VoidMethodArgs(string arg1, int arg2);
        }
        public interface IStringMethodTest
        {
            string StringMethod();
        }
        public interface IStringMethodArgsTest
        {
            string StringMethodArgs(string arg1, int arg2);
        }
        public interface IDifferentMethodSig
        {
            void DifferentMethodSig(string arg1);
        }

        public class TestClass
        {
            public bool VoidMethodCalled = false;
            public void VoidMethod()
            {
                VoidMethodCalled = true;
            }

            public object[] VoidMethodArgsCalled = null;
            public void VoidMethodArgs(string arg1, int arg2)
            {
                VoidMethodArgsCalled = new object[] { arg1, arg2 };
            }

            public string StringMethod()
            {
                return "result";
            }

            public string StringMethodArgs(string arg1, int arg2)
            {
                return arg1 + "-" + arg2;
            }

            public void DifferentMethodSig(int arg1)
            {
            }
        }

        [TestMethod]
        public void VoidMethod_callable()
        {
            var obj = new TestClass();
            Assert.IsFalse(obj.VoidMethodCalled);

            var shim = Shimterface.Shim<IVoidMethodTest>(obj);
            shim.VoidMethod();

            Assert.IsTrue(obj.VoidMethodCalled);
        }

        [TestMethod]
        public void VoidMethod_with_args_callable()
        {
            var obj = new TestClass();
            Assert.IsNull(obj.VoidMethodArgsCalled);

            var shim = Shimterface.Shim<IVoidMethodArgsTest>(obj);
            shim.VoidMethodArgs("arg1", 2);

            CollectionAssert.AreEquivalent(new object[] { "arg1", 2 }, obj.VoidMethodArgsCalled);
        }

        [TestMethod]
        public void StringMethod_callable()
        {
            var obj = new TestClass();

            var shim = Shimterface.Shim<IStringMethodTest>(obj);
            var res = shim.StringMethod();

            Assert.AreEqual("result", res);
        }

        [TestMethod]
        public void StringMethod_with_args_callable()
        {
            var obj = new TestClass();

            var shim = Shimterface.Shim<IStringMethodArgsTest>(obj);
            var res = shim.StringMethodArgs("arg1", 2);

            Assert.AreEqual("arg1-2", res);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidCastException))]
        public void Method_signatures_must_match()
        {
            var obj = new TestClass();

            Shimterface.Shim<IDifferentMethodSig>(obj);
        }
    }
}
