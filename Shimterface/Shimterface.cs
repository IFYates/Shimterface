﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

// TODO: Could use it to produce a binary of all shims
namespace Shimterface
{
	/// <summary>
	/// Provides facility to create a shim that guarantees an object can be treated as the specified interface type.
	/// </summary>
	public static class Shimterface
	{
		private static void resolveParameters(ILGenerator impl, MethodInfo methodInfo, MethodInfo interfaceMethod)
		{
			var pars1 = methodInfo.GetParameters();
			var pars2 = interfaceMethod.GetParameters();
			for (var i = 0; i < pars1.Length; ++i)
			{
				impl.Emit(OpCodes.Ldarg, i + 1);

				if (pars1[i].ParameterType != pars2[i].ParameterType)
				{
					var valType = pars1[i].ParameterType.ResolveType();
					var paramType = pars1[i].ParameterType.IsArrayType() ? typeof(object[]) : typeof(object);
					var unshimMethod = typeof(Shimterface).BindStaticMethod(nameof(Unshim), new[] { valType }, new[] { paramType });
					impl.Emit(OpCodes.Call, unshimMethod);
				}
			}
		}

		private static readonly List<Type> _IgnoreMissingMembers = new List<Type>();

		/// <summary>
		/// Don't compile the type every time
		/// </summary>
		private static readonly Dictionary<string, Type> _dynamicTypeCache = new Dictionary<string, Type>();

		#region Internal

		private static Type getShimType(Type interfaceType, Type instType)
		{
			var className = instType.Name + "_" + instType.GetHashCode() + "_" + interfaceType.Name + "_" + interfaceType.GetHashCode();
			if (!_dynamicTypeCache.ContainsKey(className))
			{
				var asm = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("Shimterface.dynamic"),
					AssemblyBuilderAccess.Run);
				var mod = asm.DefineDynamicModule("Shimterface.dynamic");
				//var mod = asm.DefineDynamicModule("Shimterface.dynamic", "Shimterface.dynamic.dll", true);
				var tb = mod.DefineType(className, TypeAttributes.Public
					| TypeAttributes.Class
					| TypeAttributes.AutoClass
					| TypeAttributes.AnsiClass
					| TypeAttributes.BeforeFieldInit
					| TypeAttributes.AutoLayout, null, new[] { typeof(IShim), interfaceType });

				var instField = tb.DefineField("_inst", instType, FieldAttributes.Private | FieldAttributes.InitOnly);

				addConstructor(tb, instField);
				addUnshimMethod(tb, instField);

				// Proxy all methods (including events, properties, and indexers)
				foreach (var interfaceMethod in interfaceType.GetMethods())
				{
					// Is return type shimmed?
					var attr = interfaceMethod.GetCustomAttribute<TypeShimAttribute>(false);
					if (attr != null && !interfaceMethod.ReturnType.IsInterfaceType())
					{
						throw new NotSupportedException("Shimmed return type must be an interface: " + interfaceType.FullName);
					}

					// Workout real parameter types
					var paramTypes = interfaceMethod.GetParameters()
						.Select(p =>
						{
							var paramAttr = p.GetCustomAttribute<TypeShimAttribute>();
							if (paramAttr != null && !p.ParameterType.IsInterfaceType())
							{
								throw new NotSupportedException("Shimmed parameter type must be an interface: " + interfaceType.FullName);
							}
							return paramAttr == null ? p.ParameterType : paramAttr.RealType;
						}).ToArray();

					// If really a property, will need to get attr from PropertyInfo
					if ((interfaceMethod.Attributes & MethodAttributes.SpecialName) > 0
						&& (interfaceMethod.Name.StartsWith("get_") || interfaceMethod.Name.StartsWith("set_")))
					{
						var propInfo = interfaceType.GetProperty(interfaceMethod.Name.Substring(4));
						if (propInfo != null)
						{
							attr = propInfo.GetCustomAttribute<TypeShimAttribute>(false);
							if (attr != null && !propInfo.PropertyType.IsInterfaceType())
							{
								throw new NotSupportedException("Shimmed property type must be an interface: " + interfaceType.FullName);
							}
							if (attr != null && interfaceMethod.Name.StartsWith("set_"))
							{
								paramTypes[paramTypes.Length - 1] = attr.RealType;
							}
						}
					}

					// Match real method
					var methodInfo = instType.GetMethod(interfaceMethod.Name, paramTypes);
					if (methodInfo == null)
					{
						if (_IgnoreMissingMembers.Contains(interfaceType))
						{
							dontImplementMethod(tb, instField, interfaceMethod);
						}
						else
						{
							// TODO: Could support default/custom functionality
							throw new InvalidCastException(String.Format("Cannot shim {0} as {1}; missing method: {2}", instType.FullName, interfaceType.FullName, interfaceMethod.Name));
						}
					}
					else
					{
						shimMethod(tb, instField, interfaceMethod, methodInfo);
					}
				}

				_dynamicTypeCache[className] = tb.CreateType();
			}
			return _dynamicTypeCache[className];
		}

		private static void addConstructor(TypeBuilder tb, FieldBuilder instField)
		{
			// .constr(object inst)
			var constr = tb.DefineConstructor(MethodAttributes.Public
				| MethodAttributes.HideBySig
				| MethodAttributes.SpecialName
				| MethodAttributes.RTSpecialName, CallingConventions.Standard, new[] { instField.FieldType });
			constr.DefineParameter(1, ParameterAttributes.None, "inst");
			var impl = constr.GetILGenerator();
			impl.Emit(OpCodes.Ldarg_0); // this
			impl.Emit(OpCodes.Call, typeof(object).GetConstructor(new Type[0])); // Call to base()
			impl.Emit(OpCodes.Nop);
			impl.Emit(OpCodes.Nop);
			impl.Emit(OpCodes.Ldarg_0); // this
			impl.Emit(OpCodes.Ldarg_1); // inst
			impl.Emit(OpCodes.Stfld, instField);
			impl.Emit(OpCodes.Ret);
		}

		private static void addUnshimMethod(TypeBuilder tb, FieldBuilder instField)
		{
			// object Unshim()
			var unshimMethod = tb.DefineMethod("Unshim", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual, typeof(object), new Type[0]);
			var impl = unshimMethod.GetILGenerator();
			impl.Emit(OpCodes.Ldarg_0); // this
			impl.Emit(OpCodes.Ldfld, instField);
			if (instField.FieldType.IsValueType)
			{
				impl.Emit(OpCodes.Box, instField.FieldType);
			}
			impl.Emit(OpCodes.Ret);
		}

		private static void shimMethod(TypeBuilder tb, FieldBuilder instField, MethodInfo interfaceMethod, MethodInfo methodInfo)
		{
			var attrs = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual;
			var method = tb.DefineMethod(interfaceMethod.Name, attrs,
				interfaceMethod.ReturnType, interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray());
			var impl = method.GetILGenerator();
			impl.Emit(OpCodes.Ldarg_0); // this
			if (instField.FieldType.IsValueType)
			{
				impl.Emit(OpCodes.Ldflda, instField);
			}
			else
			{
				impl.Emit(OpCodes.Ldfld, instField);
			}
			resolveParameters(impl, methodInfo, interfaceMethod);
			impl.Emit(OpCodes.Callvirt, methodInfo);
			if (interfaceMethod.ReturnType != methodInfo.ReturnType && interfaceMethod.ReturnType != typeof(void))
			{
				if (methodInfo.ReturnType.IsValueType)
				{
					impl.Emit(OpCodes.Box, methodInfo.ReturnType);
				}
				var valType = interfaceMethod.ReturnType.IsArrayType() ? typeof(object[]) : typeof(object);
				var shimType = interfaceMethod.ReturnType.ResolveType();
				var shimMethod = typeof(Shimterface).BindStaticMethod(nameof(Shim), new[] { shimType }, new[] { valType });
				impl.Emit(OpCodes.Call, shimMethod);
			}
			impl.Emit(OpCodes.Ret);
		}

		private static void dontImplementMethod(TypeBuilder tb, FieldBuilder instField, MethodInfo methodInfo)
		{
			var method = tb.DefineMethod(methodInfo.Name, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
				methodInfo.ReturnType, methodInfo.GetParameters().Select(p => p.ParameterType).ToArray());
			var impl = method.GetILGenerator();
			impl.Emit(OpCodes.Ldarg_0); // this

			var notImplementedConstr = typeof(NotImplementedException).GetConstructor(new Type[0]);
			impl.Emit(OpCodes.Newobj, notImplementedConstr);
			impl.Emit(OpCodes.Throw);
		}

		#endregion Internal

		/// <summary>
		/// Gets or sets the creation-time assertion that all TInterface members must exist in the shimmed type.
		/// Execution of such members will throw NotImplementedException.
		/// Once set, cannot be reversed.
		/// </summary>
		public static void IgnoreMissingMembers<TInterface>()
			where TInterface : class
		{
			_IgnoreMissingMembers.Add(typeof(TInterface));
		}

		/// <summary>
		/// Use a shim to make the given object look like the required type.
		/// Result will also implement <see cref="IShim"/>.
		/// </summary>
		public static TInterface Shim<TInterface>(this object inst)
			where TInterface : class
		{
			return (TInterface)Shim(typeof(TInterface), inst);
		}
		/// <summary>
		/// Use a shim to make the given objects look like the required type.
		/// Results will also implement <see cref="IShim"/>.
		/// </summary>
		public static TInterface[] Shim<TInterface>(this object[] inst)
			where TInterface : class
		{
			return (TInterface[])Shim<TInterface>((IEnumerable<object>)inst);
		}
		/// <summary>
		/// Use a shim to make the given objects look like the required type.
		/// Results will also implement <see cref="IShim"/>.
		/// </summary>
		public static IEnumerable<TInterface> Shim<TInterface>(this IEnumerable<object> inst)
			where TInterface : class
		{
			return inst.Select(i => (TInterface)Shim(typeof(TInterface), i)).ToArray();
		}
		/// <summary>
		/// Use a shim to make the given object look like the required type.
		/// Result will also implement <see cref="IShim"/>.
		/// </summary>
		public static object Shim(Type interfaceType, object inst)
		{
			// Run-time test that type is an interface
			if (!interfaceType.IsInterfaceType())
			{
				throw new NotSupportedException("Generic argument must be a direct interface: " + interfaceType.FullName);
			}

			if (interfaceType.IsAssignableFrom(inst.GetType()))
			{
				return inst;
			}

			var shimType = getShimType(interfaceType, inst.GetType());
			var shim = Activator.CreateInstance(shimType, new object[] { inst });
			return shim;
		}

		/// <summary>
		/// Recast shim to original type.
		/// No type-safety checks.
		/// </summary>
		public static T Unshim<T>(object shim)
		{
			return (T)((IShim)shim).Unshim();
		}
		/// <summary>
		/// Recast shims to original type.
		/// No type-safety checks.
		/// </summary>
		public static T[] Unshim<T>(object[] shims)
		{
			return (T[])Unshim<T>((IEnumerable<object>)shims);
		}
		/// <summary>
		/// Recast shims to original type.
		/// No type-safety checks.
		/// </summary>
		public static IEnumerable<T> Unshim<T>(IEnumerable<object> shims)
		{
			return shims.Select(s => (T)((IShim)s).Unshim()).ToArray();
		}
	}
}
