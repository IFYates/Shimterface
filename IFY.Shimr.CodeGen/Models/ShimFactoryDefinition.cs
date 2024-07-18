﻿using IFY.Shimr.CodeGen.CodeAnalysis;
using IFY.Shimr.CodeGen.Models.Bindings;
using IFY.Shimr.CodeGen.Models.Members;
using Microsoft.CodeAnalysis;

namespace IFY.Shimr.CodeGen.Models;

internal class ShimFactoryDefinition : IShimDefinition
{
    private readonly ShimMember[] _members;

    public INamedTypeSymbol Symbol { get; }
    public string FullTypeName { get; }
    public string Name { get; }
    public ShimTarget? StaticTarget { get; }

    public ShimFactoryDefinition(ITypeSymbol symbol)
    {
        var staticAttr = symbol.GetAttribute<StaticShimAttribute>();
        if (staticAttr != null)
        {
            StaticTarget = new((ITypeSymbol)staticAttr.ConstructorArguments[0].Value!);
        }

        Symbol = (INamedTypeSymbol)symbol;
        FullTypeName = symbol.ToFullName();
        Name = $"ShimFactory__{symbol.ToFullName().Hash()}_{symbol.Name}";

        _members = symbol.GetAllMembers()
            .Select(m => ShimMember.Parse(m, this))
            .OfType<ShimMember>().ToArray();
    }

    public void SetMemberType(ISymbol symbol, ITypeSymbol target)
    {
        _members.Single(m => m.Symbol.Equals(symbol, SymbolEqualityComparer.Default))
            .TargetType = new(target);
    }

    private const string OUTER_CLASS_CS = $@"namespace {GlobalCodeWriter.SB_NAMESPACE}
{{{{
    using {GlobalCodeWriter.EXT_NAMESPACE};
    using System.Linq;
    public static partial class {GlobalCodeWriter.SB_CLASSNAME}
    {{{{
        protected class {{0}} : {{1}}
        {{{{
{{2}}
        }}}}
    }}}}
}}}}
";

    public void WriteShimClass(ICodeWriter writer, IEnumerable<IBinding> bindings)
    {
        var classCode = new StringBuilder();
        foreach (var binding in bindings)
        {
            binding.GenerateCode(classCode);
        }

        var code = string.Format(OUTER_CLASS_CS, Name, FullTypeName, classCode.ToString());
        writer.AddSource($"Shimr.{Name}.g.cs", code);
    }

    public void Resolve(IList<IBinding> allBindings, CodeErrorReporter errors, ShimResolver shimResolver)
    {
        // Map shim members against targets
        foreach (var member in _members)
        {
            var target = member.TargetType ?? StaticTarget;
            if (target == null)
            {
                // TODO: error
                Diag.WriteOutput($"//// No static target {Name} {member.Name}");
                continue;
            }

            var targetMembers = target.GetMatchingMembers(member, errors);
            foreach (var targetMember in targetMembers)
            {
                member.ResolveBindings(allBindings, targetMember, errors, shimResolver);
            }
        }
    }
}
