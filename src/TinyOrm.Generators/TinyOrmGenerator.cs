using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TinyOrm.Generators;

[Generator]
public sealed class TinyOrmGenerator : IIncrementalGenerator
{
    /// <summary>
    /// TinyOrm 的 Source Generator，扫描实体类型并生成映射代码（表/列元数据、
    /// 插入/更新/删除 SQL 以及参数绑定、Materializer 等）。
    /// </summary>
    /// <summary>初始化增量生成器。</summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(static (n, _) => IsCandidate(n), static (ctx, _) => GetCandidate(ctx))
            .Where(static c => c is not null);

        var compilationAndCandidates = context.CompilationProvider.Combine(candidates.Collect());

        context.RegisterSourceOutput(compilationAndCandidates, static (spc, source) => Execute(spc, source.Left, source.Right!));
    }

    /// <summary>判断语法节点是否为候选实体类型。</summary>
    private static bool IsCandidate(SyntaxNode node)
        => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0;

    /// <summary>候选实体记录，包含表与模式名。</summary>
    private sealed record Candidate(ClassDeclarationSyntax ClassDecl, string? Table, string? Schema);

    /// <summary>提取候选实体及其表/模式配置。</summary>
    private static Candidate? GetCandidate(GeneratorSyntaxContext ctx)
    {
        var cds = (ClassDeclarationSyntax)ctx.Node;
        foreach (var list in cds.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var si = ctx.SemanticModel.GetSymbolInfo(attr);
                if (si.Symbol is not IMethodSymbol ms) continue;
                var attrType = ms.ContainingType;
                var name = attrType.ToDisplayString();
                if (name == "System.ComponentModel.DataAnnotations.Schema.TableAttribute")
                {
                    string? table = null;
                    if (attr.ArgumentList?.Arguments.Count > 0)
                        table = ctx.SemanticModel.GetConstantValue(attr.ArgumentList!.Arguments[0].Expression).Value?.ToString();
                    string? schema = null;
                    if (attr.ArgumentList is { Arguments.Count: > 0 })
                    {
                        foreach (var arg in attr.ArgumentList!.Arguments)
                        {
                            if (arg.NameEquals?.Name.Identifier.Text == "Schema")
                            {
                                schema = ctx.SemanticModel.GetConstantValue(arg.Expression).Value?.ToString();
                            }
                        }
                    }
                    if (table is null) return null;
                    return new Candidate(cds, table, schema);
                }
                if (name == "System.ComponentModel.DataAnnotations.ComplexTypeAttribute")
                {
                    return new Candidate(cds, null, null);
                }
            }
        }
        return null;
    }

    /// <summary>生成映射代码并注册到运行时映射表。</summary>
    private static void Execute(SourceProductionContext spc, Compilation compilation, ImmutableArray<Candidate> candidates)
    {
        var registrations = new StringBuilder();
        registrations.Append("#nullable enable\n");
        registrations.Append("using System.Linq;\n");
        registrations.Append("namespace TinyOrm.Runtime.Mapping;\n");
        registrations.Append("public static class GeneratedMappingModule\n{\n");
        registrations.Append("    [System.Runtime.CompilerServices.ModuleInitializer]\n    public static void Init(){\n");
        foreach (var candidate in candidates)
        {
            var model = compilation.GetSemanticModel(candidate.ClassDecl.SyntaxTree);
            if (model.GetDeclaredSymbol(candidate.ClassDecl) is not INamedTypeSymbol entitySymbol)
                continue;

            var ns = entitySymbol.ContainingNamespace.IsGlobalNamespace ? null : entitySymbol.ContainingNamespace.ToDisplayString();
            var entityName = entitySymbol.Name;

            var properties = entitySymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.DeclaredAccessibility == Accessibility.Public).ToArray();

            var table = candidate.Table;
            var schema = candidate.Schema;

            var sb = new StringBuilder();
            sb.Append("#nullable enable\n");
            if (!string.IsNullOrEmpty(ns))
            {
                sb.Append("namespace ").Append(ns).Append(";\n\n");
            }
            sb.Append("using System.Linq;\n");
            sb.Append("public static partial class ").Append(entityName).Append("Map\n");
            sb.Append("{\n");
            sb.Append("    public const string Table = \"").Append(table).Append("\";\n");
            if (!string.IsNullOrEmpty(schema))
            {
                sb.Append("    public const string Schema = \"").Append(schema).Append("\";\n");
            }
            sb.Append("    public static TinyOrm.Abstractions.Core.Field<").Append(entityName).Append(", T> Field<T>(string property, string column) => new(property, column);\n");

            foreach (var p in properties)
            {
                var propName = p.Name;
                var colName = GetColumnName(p) ?? propName;
                var propType = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                sb.Append("    public static readonly TinyOrm.Abstractions.Core.Field<").Append(entityName).Append(", ").Append(propType).Append("> ").Append(propName).Append(" = new(\"").Append(propName).Append("\", \"").Append(colName).Append("\");\n");
            }

            // Emit column metadata
            sb.Append("    public static readonly (string Prop, string Col, bool IsKey, bool IsIdentity, bool IsComputed)[] Columns = new[] {\n");
            foreach (var p in properties)
            {
                var propName = p.Name;
                var colName = GetColumnName(p) ?? p.Name;
                var isKey = HasAttribute(p, "System.ComponentModel.DataAnnotations.KeyAttribute");
                var dbGen = GetDatabaseGeneratedOption(p);
                var isIdentity = dbGen == 1;
                var isComputed = dbGen == 2;
                sb.Append("        (\"").Append(propName).Append("\", \"").Append(colName).Append("\", ")
                  .Append(isKey ? "true" : "false").Append(", ")
                  .Append(isIdentity ? "true" : "false").Append(", ")
                  .Append(isComputed ? "true" : "false").Append("),\n");
            }
            sb.Append("    };\n");

            // Build insert SQL
            sb.Append("    public static string BuildInsertSql(TinyOrm.Dialects.IDialectAdapter dialect)\n    {\n        var cols = Columns.Where(c => !c.IsComputed && !c.IsIdentity).Select(c => c.Col).ToArray();\n        var colList = string.Join(\", \", cols.Select(dialect.QuoteIdentifier));\n        var placeholders = string.Join(\", \", cols.Select(c => dialect.Parameter(\"p_\" + c)));\n        var table = dialect.QuoteTable(Table, ");
            if (!string.IsNullOrEmpty(schema)) sb.Append("Schema"); else sb.Append("null");
            sb.Append(");\n        return \"INSERT INTO \" + table + \" (\" + colList + \") VALUES (\" + placeholders + \")\";\n    }\n");

            // Bind insert parameters
            sb.Append("    public static void BindInsertParameters(System.Data.Common.DbCommand cmd, ").Append(entityName).Append(" e, TinyOrm.Dialects.IDialectAdapter dialect)\n    {\n        foreach (var c in Columns)\n        {\n            if (c.IsComputed || c.IsIdentity) continue;\n            var p = cmd.CreateParameter();\n            p.ParameterName = dialect.Parameter(\"p_\" + c.Col);\n            switch (c.Prop)\n            {\n");
            foreach (var p in properties)
            {
                var col = GetColumnName(p) ?? p.Name;
                var typeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                if (p.Type is INamedTypeSymbol nts && nts.TypeKind == TypeKind.Enum)
                {
                    if (IsEnumStoredAsString(p))
                    {
                        sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append(".ToString(); p.DbType = dialect.MapClrType(typeof(string)); break;\n");
                    }
                    else
                    {
                        var underlying = nts.EnumUnderlyingType!;
                        var underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                        sb.Append("            case \"").Append(p.Name).Append("\": p.Value = (object)(").Append(underlyingName).Append(")e.").Append(p.Name).Append("; p.DbType = dialect.MapClrType(typeof(").Append(underlyingName).Append(")); break;\n");
                    }
                }
                else if (p.Type is INamedTypeSymbol n2 && n2.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T && n2.TypeArguments.Length == 1 && n2.TypeArguments[0] is INamedTypeSymbol enumArg && enumArg.TypeKind == TypeKind.Enum)
                {
                    if (IsEnumStoredAsString(p))
                    {
                        sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append(".HasValue ? (object) e.").Append(p.Name).Append(".Value.ToString() : DBNull.Value; p.DbType = dialect.MapClrType(typeof(string)); break;\n");
                    }
                    else
                    {
                        var underlying = enumArg.EnumUnderlyingType!;
                        var underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                        sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append(".HasValue ? (object)(").Append(underlyingName).Append(")e.").Append(p.Name).Append(".Value : DBNull.Value; p.DbType = dialect.MapClrType(typeof(").Append(underlyingName).Append(")); break;\n");
                    }
                }
                else if (p.Type is INamedTypeSymbol n3 && n3.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T && n3.TypeArguments.Length == 1)
                {
                    var underlying = n3.TypeArguments[0];
                    var underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                    sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append(".HasValue ? (object)e.").Append(p.Name).Append(".Value : DBNull.Value; p.DbType = dialect.MapClrType(typeof(").Append(underlyingName).Append(")); break;\n");
                }
                else
                {
                    sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append("; p.DbType = dialect.MapClrType(typeof(").Append(typeName).Append(")); break;\n");
                }
            }
            sb.Append("            }\n            cmd.Parameters.Add(p);\n        }\n    }\n");

            sb.Append("    public static (string Col, object? Val, System.Type Type)[] GetInsertValues(").Append(entityName).Append(" e)\n    {\n        var list = new System.Collections.Generic.List<(string, object?, System.Type)>();\n        foreach (var c in Columns)\n        {\n            if (c.IsComputed || c.IsIdentity) continue;\n            switch (c.Prop)\n            {\n");
            foreach (var p in properties)
            {
                var colNm = GetColumnName(p) ?? p.Name;
                if (p.Type is INamedTypeSymbol nts && nts.TypeKind == TypeKind.Enum)
                {
                    if (IsEnumStoredAsString(p))
                    {
                        sb.Append("            case \"").Append(p.Name).Append("\": list.Add((\"").Append(colNm).Append("\", e.").Append(p.Name).Append(".ToString(), typeof(string))); break;\n");
                    }
                    else
                    {
                        var underlying = nts.EnumUnderlyingType!;
                        var underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                        sb.Append("            case \"").Append(p.Name).Append("\": list.Add((\"").Append(colNm).Append("\", (").Append(underlyingName).Append(")e.").Append(p.Name).Append(", typeof(").Append(underlyingName).Append("))); break;\n");
                    }
                }
                else if (p.Type is INamedTypeSymbol n2 && n2.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T && n2.TypeArguments.Length == 1 && n2.TypeArguments[0] is INamedTypeSymbol enumArg && enumArg.TypeKind == TypeKind.Enum)
                {
                    if (IsEnumStoredAsString(p))
                    {
                        sb.Append("            case \"").Append(p.Name).Append("\": list.Add((\"").Append(colNm).Append("\", e.").Append(p.Name).Append(".HasValue ? (object) e.").Append(p.Name).Append(".Value.ToString() : DBNull.Value, typeof(string))); break;\n");
                    }
                    else
                    {
                        var underlying = enumArg.EnumUnderlyingType!;
                        var underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                        sb.Append("            case \"").Append(p.Name).Append("\": list.Add((\"").Append(colNm).Append("\", e.").Append(p.Name).Append(".HasValue ? (object)(").Append(underlyingName).Append(")e.").Append(p.Name).Append(".Value : DBNull.Value, typeof(").Append(underlyingName).Append("))); break;\n");
                    }
                }
                else if (p.Type is INamedTypeSymbol n3 && n3.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T && n3.TypeArguments.Length == 1)
                {
                    var underlying = n3.TypeArguments[0];
                    var underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                    sb.Append("            case \"").Append(p.Name).Append("\": list.Add((\"").Append(colNm).Append("\", e.").Append(p.Name).Append(".HasValue ? (object)e.").Append(p.Name).Append(".Value : DBNull.Value, typeof(").Append(underlyingName).Append("))); break;\n");
                }
                else
                {
                    var typeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                    sb.Append("            case \"").Append(p.Name).Append("\": list.Add((\"").Append(colNm).Append("\", e.").Append(p.Name).Append(", typeof(").Append(typeName).Append("))); break;\n");
                }
            }
            sb.Append("            }\n        }\n        return list.ToArray();\n    }\n");

            sb.Append("    public static string BuildUpdateSql(TinyOrm.Dialects.IDialectAdapter dialect)\n    {\n        var setCols = Columns.Where(c => !c.IsComputed && !c.IsIdentity && !c.IsKey).Select(c => c.Col).ToArray();\n        var setList = string.Join(\", \", setCols.Select(c => dialect.QuoteIdentifier(c) + \" = \" + dialect.Parameter(\"p_\" + c)));\n        var keyCols = Columns.Where(c => c.IsKey).Select(c => c.Col).ToArray();\n        var whereList = string.Join(\" AND \", keyCols.Select(c => dialect.QuoteIdentifier(c) + \" = \" + dialect.Parameter(\"p_\" + c)));\n        var table = dialect.QuoteTable(Table, ");
            if (!string.IsNullOrEmpty(schema)) sb.Append("Schema"); else sb.Append("null");
            sb.Append(");\n        return \"UPDATE \" + table + \" SET \" + setList + \" WHERE \" + whereList;\n    }\n");

            sb.Append("    public static void BindUpdateParameters(System.Data.Common.DbCommand cmd, ").Append(entityName).Append(" e, TinyOrm.Dialects.IDialectAdapter dialect)\n    {\n        foreach (var c in Columns)\n        {\n            if (c.IsComputed || (!c.IsKey && c.IsIdentity)) continue;\n            var p = cmd.CreateParameter();\n            if (!c.IsKey) p.ParameterName = dialect.Parameter(\"p_\" + c.Col); else p.ParameterName = dialect.Parameter(\"p_\" + c.Col);\n            switch (c.Prop)\n            {\n");
            foreach (var p in properties)
            {
                if (p.Type is INamedTypeSymbol nts && nts.TypeKind == TypeKind.Enum)
                {
                    if (IsEnumStoredAsString(p))
                    {
                        sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append(".ToString(); p.DbType = dialect.MapClrType(typeof(string)); break;\n");
                    }
                    else
                    {
                        var underlying = nts.EnumUnderlyingType!;
                        var underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                        sb.Append("            case \"").Append(p.Name).Append("\": p.Value = (object)(").Append(underlyingName).Append(")e.").Append(p.Name).Append("; p.DbType = dialect.MapClrType(typeof(").Append(underlyingName).Append(")); break;\n");
                    }
                }
                else if (p.Type is INamedTypeSymbol n2 && n2.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T && n2.TypeArguments.Length == 1 && n2.TypeArguments[0] is INamedTypeSymbol enumArg && enumArg.TypeKind == TypeKind.Enum)
                {
                    if (IsEnumStoredAsString(p))
                    {
                        sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append(".HasValue ? (object) e.").Append(p.Name).Append(".Value.ToString() : DBNull.Value; p.DbType = dialect.MapClrType(typeof(string)); break;\n");
                    }
                    else
                    {
                        var underlying = enumArg.EnumUnderlyingType!;
                        var underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                        sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append(".HasValue ? (object)(").Append(underlyingName).Append(")e.").Append(p.Name).Append(".Value : DBNull.Value; p.DbType = dialect.MapClrType(typeof(").Append(underlyingName).Append(")); break;\n");
                    }
                }
                else if (p.Type is INamedTypeSymbol n3 && n3.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T && n3.TypeArguments.Length == 1)
                {
                    var underlyingName = n3.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                    sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append(".HasValue ? (object)e.").Append(p.Name).Append(".Value : DBNull.Value; p.DbType = dialect.MapClrType(typeof(").Append(underlyingName).Append(")); break;\n");
                }
                else
                {
                    var typeNameU = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                    sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append("; p.DbType = dialect.MapClrType(typeof(").Append(typeNameU).Append(")); break;\n");
                }
            }
            sb.Append("            }\n            cmd.Parameters.Add(p);\n        }\n    }\n");

            sb.Append("    public static string BuildDeleteSql(TinyOrm.Dialects.IDialectAdapter dialect)\n    {\n        var keyCols = Columns.Where(c => c.IsKey).Select(c => c.Col).ToArray();\n        var whereList = string.Join(\" AND \", keyCols.Select(c => dialect.QuoteIdentifier(c) + \" = \" + dialect.Parameter(\"p_\" + c)));\n        var table = dialect.QuoteTable(Table, ");
            if (!string.IsNullOrEmpty(schema)) sb.Append("Schema"); else sb.Append("null");
            sb.Append(");\n        return \"DELETE FROM \" + table + \" WHERE \" + whereList;\n    }\n");

            sb.Append("    public static void BindDeleteParameters(System.Data.Common.DbCommand cmd, ").Append(entityName).Append(" e, TinyOrm.Dialects.IDialectAdapter dialect)\n    {\n        foreach (var c in Columns)\n        {\n            if (!c.IsKey) continue;\n            var p = cmd.CreateParameter();\n            p.ParameterName = dialect.Parameter(\"p_\" + c.Col);\n            switch (c.Prop)\n            {\n");
            foreach (var p in properties)
            {
                if (p.Type is INamedTypeSymbol nts && nts.TypeKind == TypeKind.Enum)
                {
                    var underlying = nts.EnumUnderlyingType!;
                    var underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                    sb.Append("            case \"").Append(p.Name).Append("\": p.Value = (object)(").Append(underlyingName).Append(")e.").Append(p.Name).Append("; p.DbType = dialect.MapClrType(typeof(").Append(underlyingName).Append(")); break;\n");
                }
                else if (p.Type is INamedTypeSymbol n2 && n2.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T && n2.TypeArguments.Length == 1 && n2.TypeArguments[0] is INamedTypeSymbol enumArg && enumArg.TypeKind == TypeKind.Enum)
                {
                    var underlying = enumArg.EnumUnderlyingType!;
                    var underlyingName = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                    sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append(".HasValue ? (object)(").Append(underlyingName).Append(")e.").Append(p.Name).Append(".Value : DBNull.Value; p.DbType = dialect.MapClrType(typeof(").Append(underlyingName).Append(")); break;\n");
                }
                else if (p.Type is INamedTypeSymbol n3 && n3.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T && n3.TypeArguments.Length == 1)
                {
                    var underlyingName = n3.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                    sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append(".HasValue ? (object)e.").Append(p.Name).Append(".Value : DBNull.Value; p.DbType = dialect.MapClrType(typeof(").Append(underlyingName).Append(")); break;\n");
                }
                else
                {
                    var typeNameD = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                    sb.Append("            case \"").Append(p.Name).Append("\": p.Value = e.").Append(p.Name).Append("; p.DbType = dialect.MapClrType(typeof(").Append(typeNameD).Append(")); break;\n");
                }
            }
            sb.Append("            }\n            cmd.Parameters.Add(p);\n        }\n    }\n");

            sb.Append("    public sealed class Materializer : TinyOrm.Abstractions.Core.ITinyRowMaterializer<").Append(entityName).Append(">\n");
            sb.Append("    {\n");
            foreach (var p in properties)
            {
                var colName = GetColumnName(p) ?? p.Name;
                sb.Append("        private int _ord_" + colName + ";\n");
            }
            sb.Append("        public void Initialize(System.Data.Common.DbDataReader reader)\n");
            sb.Append("        {\n");
            foreach (var p in properties)
            {
                var colName = GetColumnName(p) ?? p.Name;
                sb.Append("            _ord_").Append(colName).Append(" = reader.GetOrdinal(\"").Append(colName).Append("\");\n");
            }
            sb.Append("        }\n");

            sb.Append("        public ").Append(entityName).Append(" Read(System.Data.Common.DbDataReader reader)\n");
            sb.Append("        {\n");
            sb.Append("            var e = new ").Append(entityName).Append("();\n");
            foreach (var p in properties)
            {
                var colName = GetColumnName(p) ?? p.Name;
                var propName = p.Name;
                var assignExpr = GetReadExpression(p, colName);
                sb.Append(assignExpr);
            }
            sb.Append("            return e;\n");
            sb.Append("        }\n");
            sb.Append("    }\n");

            sb.Append("}\n");

            spc.AddSource(entityName + ".TinyOrmMap.g.cs", sb.ToString());

            var fullEntityName = entitySymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
            registrations.Append("        TinyOrm.Runtime.Mapping.MappingRegistry.Register(typeof(").Append(fullEntityName).Append("), new TinyOrm.Runtime.Mapping.EntityMapEntry { Table = ").Append(fullEntityName).Append("Map.Table, Schema = ");
            if (!string.IsNullOrEmpty(schema)) registrations.Append(fullEntityName).Append("Map.Schema"); else registrations.Append("null");
            registrations.Append(", Columns = ");
            registrations.Append(fullEntityName).Append("Map.Columns.Select(c => new TinyOrm.Runtime.Mapping.ColumnMeta{ Prop=c.Prop, Col=c.Col, IsKey=c.IsKey, IsIdentity=c.IsIdentity, IsComputed=c.IsComputed }).ToArray(), ");
            registrations.Append("BuildInsert = dialect => ").Append(fullEntityName).Append("Map.BuildInsertSql(dialect), ");
            registrations.Append("BindInsert = (cmd,obj,dialect) => ").Append(fullEntityName).Append("Map.BindInsertParameters(cmd, ("+ fullEntityName + ")obj, dialect), ");
            registrations.Append("BuildUpdate = dialect => ").Append(fullEntityName).Append("Map.BuildUpdateSql(dialect), ");
            registrations.Append("BindUpdate = (cmd,obj,dialect) => ").Append(fullEntityName).Append("Map.BindUpdateParameters(cmd, ("+ fullEntityName + ")obj, dialect), ");
            registrations.Append("BuildDelete = dialect => ").Append(fullEntityName).Append("Map.BuildDeleteSql(dialect), ");
            registrations.Append("BindDelete = (cmd,obj,dialect) => ").Append(fullEntityName).Append("Map.BindDeleteParameters(cmd, ("+ fullEntityName + ")obj, dialect), ");
            registrations.Append("MaterializerFactory = () => new ").Append(fullEntityName).Append("Map.Materializer(), ");
            registrations.Append("ExtractInsertValues = obj => ").Append(fullEntityName).Append("Map.GetInsertValues((").Append(fullEntityName).Append(")obj), ");
            registrations.Append("GetColumn = prop => prop switch { ");
            foreach (var p in properties)
            {
                var colName = GetColumnName(p) ?? p.Name;
                registrations.Append("\"").Append(p.Name).Append("\" => \"").Append(colName).Append("\", ");
            }
            registrations.Append("_ => prop } ");
            registrations.Append("});\n");
        }
        registrations.Append("    }\n}\n");
        spc.AddSource("TinyOrm.Generated.ModuleInit.g.cs", registrations.ToString());
    }

    /// <summary>获取属性映射的列名（来自 ColumnAttribute）。</summary>
    private static string? GetColumnName(IPropertySymbol p)
    {
        foreach (var a in p.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.Schema.ColumnAttribute")
            {
                if (a.ConstructorArguments.Length > 0)
                {
                    var val = a.ConstructorArguments[0].Value?.ToString();
                    if (!string.IsNullOrEmpty(val)) return val;
                }
                foreach (var kv in a.NamedArguments)
                {
                    if (kv.Key == "Name")
                    {
                        var v = kv.Value.Value?.ToString();
                        if (!string.IsNullOrEmpty(v)) return v;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>判断属性是否包含指定完整名的特性。</summary>
    private static bool HasAttribute(IPropertySymbol p, string fullName)
        => p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == fullName);

    /// <summary>获取数据库生成选项（Identity/Computed）。</summary>
    private static int GetDatabaseGeneratedOption(IPropertySymbol p)
    {
        foreach (var a in p.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute")
            {
                if (a.ConstructorArguments.Length > 0)
                {
                    var val = a.ConstructorArguments[0].Value;
                    if (val is int i) return i;
                }
            }
        }
        return 0;
    }

    /// <summary>枚举是否以字符串形式存储。</summary>
    private static bool IsEnumStoredAsString(IPropertySymbol p)
    {
        foreach (var a in p.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() == "TinyOrm.Abstractions.Attributes.EnumStorageAttribute")
            {
                if (a.ConstructorArguments.Length > 0)
                {
                    var val = a.ConstructorArguments[0].Value;
                    if (val is int i) return i == 1;
                }
            }
        }
        return false;
    }

    /// <summary>生成从数据读取器读取指定列并赋值到属性的代码。</summary>
    private static string GetReadExpression(IPropertySymbol p, string col)
    {
        var typeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
        var propName = p.Name;
        var nullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        var sb = new StringBuilder();

        INamedTypeSymbol? enumType = null;
        if (p.Type is INamedTypeSymbol nts)
        {
            if (nts.TypeKind == TypeKind.Enum)
            {
                enumType = nts;
            }
            else if (nts.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T && nts.TypeArguments.Length == 1 && nts.TypeArguments[0] is INamedTypeSymbol argEnum && argEnum.TypeKind == TypeKind.Enum)
            {
                enumType = argEnum;
            }
        }

        sb.Append("            if (reader.IsDBNull(_ord_").Append(col).Append(")) { ");
        if (nullable)
            sb.Append("e.").Append(propName).Append(" = default; ");
        else
            sb.Append("e.").Append(propName).Append(" = default(").Append(typeName).Append("); ");
        if (enumType is not null)
        {
            var enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
            var underlying = enumType.EnumUnderlyingType!;
            var numericRead = GetEnumUnderlyingReadCall(underlying);
            sb.Append("} else { var __t_").Append(propName).Append(" = reader.GetFieldType(_ord_").Append(col).Append("); if (__t_").Append(propName).Append(" == typeof(string)) e.").Append(propName).Append(" = System.Enum.Parse<").Append(enumTypeName).Append(">(reader.GetString(_ord_").Append(col).Append(")); else e.").Append(propName).Append(" = (").Append(enumTypeName).Append(")").Append(numericRead.Replace("{ORD}", "_ord_" + col)).Append("; }\n");
        }
        else
        {
            var readerCall = GetReaderCall(p.Type);
            sb.Append("} else { e.").Append(propName).Append(" = ").Append(readerCall.Replace("{ORD}", "_ord_" + col)).Append("; }\n");
        }
        return sb.ToString();
    }

    /// <summary>根据类型生成读取器调用表达式。</summary>
    private static string GetReaderCall(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_String => "reader.GetString({ORD})",
            SpecialType.System_Int32 => "reader.GetInt32({ORD})",
            SpecialType.System_Int64 => "reader.GetInt64({ORD})",
            SpecialType.System_Int16 => "reader.GetInt16({ORD})",
            SpecialType.System_Byte => "reader.GetByte({ORD})",
            SpecialType.System_Boolean => "reader.GetBoolean({ORD})",
            SpecialType.System_Double => "reader.GetDouble({ORD})",
            SpecialType.System_Single => "reader.GetFloat({ORD})",
            SpecialType.System_Decimal => "reader.GetDecimal({ORD})",
            SpecialType.System_DateTime => "reader.GetDateTime({ORD})",
            _ => "reader.GetFieldValue<" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty) + ">({ORD})"
        };
    }

    /// <summary>根据枚举底层类型生成读取表达式。</summary>
    private static string GetEnumUnderlyingReadCall(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Int32 => "reader.GetInt32({ORD})",
            SpecialType.System_Int64 => "reader.GetInt64({ORD})",
            SpecialType.System_Int16 => "reader.GetInt16({ORD})",
            SpecialType.System_Byte => "reader.GetByte({ORD})",
            SpecialType.System_SByte => "System.Convert.ToSByte(reader.GetValue({ORD}))",
            SpecialType.System_UInt16 => "System.Convert.ToUInt16(reader.GetValue({ORD}))",
            SpecialType.System_UInt32 => "System.Convert.ToUInt32(reader.GetValue({ORD}))",
            SpecialType.System_UInt64 => "System.Convert.ToUInt64(reader.GetValue({ORD}))",
            _ => "reader.GetInt32({ORD})"
        };
    }
}