﻿#if SHIMR_CG
using Microsoft.CodeAnalysis;
#endif

namespace IFY.Shimr;

/// <summary>
/// Mark individual properties/fields or methods as being static within another type, or the entire interface.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
public class StaticShimAttribute : Attribute
{
    /// <summary>
    /// The type that implements this member.
    /// </summary>
    public Type? TargetType { get; }
    /// <summary>
    /// True if this member calls a constructor on the target type.
    /// </summary>
    public bool IsConstructor { get; internal set; }

    protected StaticShimAttribute()
    {
    }

    public StaticShimAttribute(Type targetType)
    {
        TargetType = targetType;
    }

#if SHIMR_CG
    public static ITypeSymbol? GetArgument(AttributeData attribute)
    {
        return attribute.ConstructorArguments.Length == 1
            ? (ITypeSymbol)attribute.ConstructorArguments[0].Value!
            : null;
    }
#endif
}
