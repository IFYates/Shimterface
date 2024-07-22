﻿using IFY.Shimr.CodeGen.CodeAnalysis;
using IFY.Shimr.CodeGen.Models.Bindings;
using IFY.Shimr.CodeGen.Models.Members;
using Microsoft.CodeAnalysis;

namespace IFY.Shimr.CodeGen.Models;

/// <param name="symbol">The type that contains the proxy target.</param>
/// <param name="shimTarget">The target definition of the shim.</param>
/// <param name="targetMembers">The list of possible targets of the shim member.</param>
internal class ShimProxyTarget(ITypeSymbol symbol, ShimTarget shimTarget, TargetMember[] targetMembers)
    : ShimTarget(symbol)
{
    public ShimTarget ShimTarget { get; } = shimTarget;

    public override IBinding GetBinding(ShimMember shimMember, TargetMember proxyMember)
    {
        return new ShimMemberProxyBinding(shimMember, ShimTarget, proxyMember, targetMembers.FirstOrDefault());
    }

    public override TargetMember[] GetMatchingMembers(ShimMember shimMember, CodeErrorReporter errors)
    {
        var proxyBehaviour = shimMember.Proxy?.Behaviour ?? ProxyBehaviour.Override;
        if (!targetMembers.Any() && proxyBehaviour == ProxyBehaviour.Override)
        {
            Diag.WriteOutput($"//// No member to override: {FullTypeName}.{shimMember.TargetName} for {shimMember.Type} {shimMember.Definition.FullTypeName}.{shimMember.Name}");
            errors.NoMemberError(Symbol, shimMember.Symbol); // TODO: better error
            return [];
        }
        else if (targetMembers.Any() && proxyBehaviour == ProxyBehaviour.Add)
        {
            // TODO: Error that proxy is adding existing method
            errors.CodeGenError(new Exception($"[ProxyBehaviour.{proxyBehaviour}] {Symbol}, {shimMember.Symbol}"));
            return [];
        }

        var (proxyType, proxyTargetName, behaviour) = shimMember.Proxy!.Value;
        var members = getMatchingMembers(shimMember);
        if (!members.Any())
        {
            // TODO: optional, as per 'IgnoreMissingMembers'
            Diag.WriteOutput($"//// No proxy match: {proxyType.ToFullName()}.{proxyTargetName ?? shimMember.Name} for {shimMember.Definition.FullTypeName}.{shimMember.Name} [ProxyBehaviour.{behaviour}]");
            errors.NoMemberError(Symbol, shimMember.Symbol);
            return [];
        }

        return members;
    }

    protected override TargetMember[] getPotentialMatchingMembers(ShimMember shimMember)
    {
        var members = Symbol.GetAllMembers()
            .Select(m => TargetMember.Parse(m, this))
            .OfType<TargetMember>();

        var proxyTargetName = shimMember.Proxy!.Value.ImplementationName ?? shimMember.Name;
        members = members.Where(m => m.Name == proxyTargetName);

        // If proxy of method, may need to ignore first parameter
        if (shimMember is ShimMember.ShimMethodMember shimMethod)
        {
            members = members.Where(m =>
            {
                if (m is not IParameterisedMember targetMethod
                    || !shimMethod.FirstParameterIsInstance(targetMethod))
                {
                    return shimMember.IsMatch(m);
                }

                return shimMethod.Parameters.Select(isParameterMatch).All(v => v);
                bool isParameterMatch(MemberParameter param1, int idx)
                    => (param1.UnderlyingType ?? param1.Type).IsMatch(targetMethod.Parameters[idx + 1].Type);
            });
        }

        return members.ToArray();
    }
}
