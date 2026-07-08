using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace CoAP.SourceGeneration;

[Generator(LanguageNames.CSharp)]
public sealed class CoapEndpointSourceGenerator : IIncrementalGenerator
{
    private const string CoapResourceAttributeName = "CoAP.Server.Routing.CoapResourceAttribute";
    private const string CoapControllerAttributeName = "CoAP.Server.Routing.CoapControllerAttribute";
    private const string CoapRouteAttributeName = "CoAP.Server.Routing.CoapRouteAttribute";
    private const string CoapGetAttributeName = "CoAP.Server.Routing.CoapGetAttribute";
    private const string CoapPostAttributeName = "CoAP.Server.Routing.CoapPostAttribute";
    private const string CoapPutAttributeName = "CoAP.Server.Routing.CoapPutAttribute";
    private const string CoapDeleteAttributeName = "CoAP.Server.Routing.CoapDeleteAttribute";
    private const string CoapObserveAttributeName = "CoAP.Server.Routing.CoapObserveAttribute";
    private const string CoapFromRouteAttributeName = "CoAP.Server.Routing.CoapFromRouteAttribute";
    private const string CoapFromQueryAttributeName = "CoAP.Server.Routing.CoapFromQueryAttribute";
    private const string CoapFromOptionAttributeName = "CoAP.Server.Routing.CoapFromOptionAttribute";
    private const string CoapFromPayloadAttributeName = "CoAP.Server.Routing.CoapFromPayloadAttribute";

    private static readonly SymbolDisplayFormat TypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methodEndpointGroups = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is MethodDeclarationSyntax method && method.AttributeLists.Count > 0,
            static (syntaxContext, cancellationToken) => CreateEndpointModels(syntaxContext, cancellationToken));

        context.RegisterSourceOutput(
            methodEndpointGroups.Collect(),
            static (sourceProductionContext, endpointGroups) => Emit(sourceProductionContext, endpointGroups));
    }

    private static ImmutableArray<EndpointModel> CreateEndpointModels(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var methodSyntax = (MethodDeclarationSyntax)context.Node;
        var method = context.SemanticModel.GetDeclaredSymbol(methodSyntax, cancellationToken);
        if (method == null ||
            method.MethodKind != MethodKind.Ordinary ||
            method.IsStatic ||
            method.IsGenericMethod ||
            !IsAccessible(method))
        {
            return ImmutableArray<EndpointModel>.Empty;
        }

        var methodRouteAttributes = method
            .GetAttributes()
            .Where(IsCoapMethodRouteAttribute)
            .ToArray();
        if (methodRouteAttributes.Length == 0)
        {
            return ImmutableArray<EndpointModel>.Empty;
        }

        var resourceType = method.ContainingType;
        if (resourceType == null ||
            resourceType.IsAbstract ||
            resourceType.TypeKind != TypeKind.Class ||
            resourceType.IsGenericType ||
            !IsResourceType(resourceType) ||
            !IsAccessible(resourceType))
        {
            return ImmutableArray<EndpointModel>.Empty;
        }

        var constructor = SelectConstructor(resourceType);
        if (constructor == null)
        {
            return ImmutableArray<EndpointModel>.Empty;
        }

        var constructorParameters = new List<ConstructorParameterModel>(constructor.Parameters.Length);
        foreach (var parameter in constructor.Parameters)
        {
            if (!IsTypeUsable(parameter.Type))
            {
                return ImmutableArray<EndpointModel>.Empty;
            }

            constructorParameters.Add(new ConstructorParameterModel(GetTypeName(parameter.Type)));
        }

        var parameters = new List<ParameterModel>(method.Parameters.Length);
        foreach (var parameter in method.Parameters)
        {
            if (!TryCreateParameterModel(parameter, out var parameterModel))
            {
                return ImmutableArray<EndpointModel>.Empty;
            }

            parameters.Add(parameterModel);
        }

        var resourceKey = GetTypeName(resourceType);
        var resourceModel = new ResourceModel(
            resourceKey,
            resourceKey,
            constructorParameters.ToArray());
        var routePrefixes = GetRoutePrefixes(resourceType.GetAttributes());
        var resourceMetadata = GetMetadataExpressions(resourceType.GetAttributes());
        var methodMetadata = GetMetadataExpressions(method.GetAttributes());
        var builder = ImmutableArray.CreateBuilder<EndpointModel>(methodRouteAttributes.Length * routePrefixes.Length);
        var routeOrdinal = 0;

        foreach (var prefix in routePrefixes)
        {
            foreach (var methodRouteAttribute in methodRouteAttributes)
            {
                var methodRoute = CreateMethodRoute(methodRouteAttribute);
                if (methodRoute == null ||
                    !TryCreateAttributeExpression(methodRouteAttribute, out var methodRouteExpression))
                {
                    continue;
                }

                var routeTemplate = CombineTemplates(prefix, methodRoute.Template);
                var metadataExpressions = new List<string>(resourceMetadata.Count + methodMetadata.Count + 1);
                metadataExpressions.AddRange(resourceMetadata);
                metadataExpressions.AddRange(methodMetadata);
                metadataExpressions.Add(methodRouteExpression);

                builder.Add(new EndpointModel(
                    resourceModel,
                    EscapeIdentifier(method.Name),
                    method.ReturnsVoid,
                    parameters.ToArray(),
                    methodRoute.MethodExpression,
                    routeTemplate,
                    metadataExpressions.ToArray(),
                    GetDisplayName(resourceType, method, methodRoute.MethodName, routeTemplate),
                    methodSyntax.SpanStart,
                    routeOrdinal++));
            }
        }

        return builder.ToImmutable();
    }

    private static void Emit(
        SourceProductionContext context,
        ImmutableArray<ImmutableArray<EndpointModel>> endpointGroups)
    {
        var endpoints = endpointGroups
            .SelectMany(static group => group)
            .OrderBy(static endpoint => endpoint.SourceOrder)
            .ThenBy(static endpoint => endpoint.RouteOrder)
            .ToArray();

        if (endpoints.Length == 0)
        {
            return;
        }

        var resources = endpoints
            .Select(static endpoint => endpoint.Resource)
            .GroupBy(static resource => resource.Key, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static resource => resource.Key, StringComparer.Ordinal)
            .ToArray();
        var resourceIndexes = resources
            .Select((resource, index) => new { resource.Key, Index = index })
            .ToDictionary(static item => item.Key, static item => item.Index, StringComparer.Ordinal);

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable disable");
        source.AppendLine();
        source.AppendLine("internal static class MyGeneratedCoapEndpoints");
        source.AppendLine("{");
        source.AppendLine("    public static global::System.Collections.Generic.IEnumerable<global::CoAP.Server.Routing.CoapEndpoint> Create(global::System.IServiceProvider serviceProvider)");
        source.AppendLine("    {");
        source.AppendLine("        _ = serviceProvider;");
        source.AppendLine("        return new global::CoAP.Server.Routing.CoapEndpoint[]");
        source.AppendLine("        {");

        for (var i = 0; i < endpoints.Length; i++)
        {
            EmitEndpoint(source, endpoints[i], i);
        }

        source.AppendLine("        };");
        source.AppendLine("    }");
        source.AppendLine();

        for (var i = 0; i < resources.Length; i++)
        {
            EmitCreateResource(source, resources[i], i);
            source.AppendLine();
        }

        for (var i = 0; i < endpoints.Length; i++)
        {
            var resourceIndex = resourceIndexes[endpoints[i].Resource.Key];
            EmitInvokeMethod(source, endpoints[i], i, resourceIndex);
            if (i + 1 < endpoints.Length)
            {
                source.AppendLine();
            }
        }

        source.AppendLine("}");

        context.AddSource("MyGeneratedCoapEndpoints.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void EmitEndpoint(StringBuilder source, EndpointModel endpoint, int endpointIndex)
    {
        source.AppendLine("            new global::CoAP.Server.Routing.CoapEndpoint(");
        source.AppendLine("                " + endpoint.MethodExpression + ",");
        source.AppendLine("                " + Literal(endpoint.RouteTemplate) + ",");
        source.AppendLine("                Invoke_" + endpointIndex.ToString(CultureInfo.InvariantCulture) + ",");
        source.AppendLine("                new object[]");
        source.AppendLine("                {");

        foreach (var metadataExpression in endpoint.MetadataExpressions)
        {
            source.AppendLine("                    " + metadataExpression + ",");
        }

        source.AppendLine("                },");
        source.AppendLine("                " + Literal(endpoint.DisplayName) + "),");
    }

    private static void EmitCreateResource(StringBuilder source, ResourceModel resource, int resourceIndex)
    {
        source.AppendLine("    private static " + resource.TypeName + " CreateResource_" + resourceIndex.ToString(CultureInfo.InvariantCulture) + "(global::CoAP.Server.Routing.CoapRouteContext context)");
        source.AppendLine("    {");

        if (resource.ConstructorParameters.Length == 0)
        {
            source.AppendLine("        _ = context;");
            source.AppendLine("        return new " + resource.TypeName + "();");
            source.AppendLine("    }");
            return;
        }

        source.AppendLine("        if (context == null)");
        source.AppendLine("        {");
        source.AppendLine("            throw new global::System.ArgumentNullException(nameof(context));");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        var services = context.RequestServices;");
        source.AppendLine("        if (services == null)");
        source.AppendLine("        {");
        source.AppendLine("            throw new global::System.InvalidOperationException(\"CoAP resource type '" + EscapeForStringContent(resource.TypeName) + "' requires request services but no service provider is available.\");");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        return new " + resource.TypeName + "(");

        for (var i = 0; i < resource.ConstructorParameters.Length; i++)
        {
            var parameter = resource.ConstructorParameters[i];
            var suffix = i + 1 == resource.ConstructorParameters.Length ? string.Empty : ",";
            source.AppendLine("            global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<" + parameter.TypeName + ">(services)" + suffix);
        }

        source.AppendLine("        );");
        source.AppendLine("    }");
    }

    private static void EmitInvokeMethod(
        StringBuilder source,
        EndpointModel endpoint,
        int endpointIndex,
        int resourceIndex)
    {
        source.AppendLine("    private static async global::System.Threading.Tasks.ValueTask<global::CoAP.Server.Routing.CoapRouteResult> Invoke_" + endpointIndex.ToString(CultureInfo.InvariantCulture) + "(global::CoAP.Server.Routing.CoapRouteContext context)");
        source.AppendLine("    {");
        source.AppendLine("        var resource = CreateResource_" + resourceIndex.ToString(CultureInfo.InvariantCulture) + "(context);");
        source.AppendLine("        return await global::CoAP.Server.Routing.CoapGeneratedEndpointHelpers.InvokeWithContextAsync(");
        source.AppendLine("            resource,");
        source.AppendLine("            context,");
        source.AppendLine("            async () =>");
        source.AppendLine("            {");

        var arguments = endpoint.Parameters
            .Select(static parameter => CreateArgumentExpression(parameter))
            .ToArray();
        var invocation = "resource." + endpoint.MethodName + "(" + string.Join(", ", arguments) + ")";

        if (endpoint.ReturnsVoid)
        {
            source.AppendLine("                " + invocation + ";");
            source.AppendLine("                return global::CoAP.Server.Routing.CoapRouteResult.Changed();");
        }
        else
        {
            source.AppendLine("                var result = " + invocation + ";");
            source.AppendLine("                return await global::CoAP.Server.Routing.CoapGeneratedEndpointHelpers.ConvertResultAsync(result).ConfigureAwait(false);");
        }

        source.AppendLine("            }).ConfigureAwait(false);");
        source.AppendLine("    }");
    }

    private static string CreateArgumentExpression(ParameterModel parameter)
    {
        switch (parameter.BindingKind)
        {
            case ParameterBindingKind.Context:
                return "context";
            case ParameterBindingKind.CancellationToken:
                return "default(global::System.Threading.CancellationToken)";
            case ParameterBindingKind.RemoteEndPoint:
                return "global::CoAP.Server.Routing.CoapGeneratedEndpointHelpers.BindRemoteEndPoint<" +
                    parameter.TypeName + ">(context, " + Literal(parameter.ParameterName) + ", " +
                    BoolLiteral(parameter.HasDefaultValue) + ", " + parameter.DefaultValueExpression + ")";
            case ParameterBindingKind.Route:
                return "global::CoAP.Server.Routing.CoapGeneratedEndpointHelpers.BindRouteValue<" +
                    parameter.TypeName + ">(context, " + Literal(parameter.BindingName) + ", " +
                    Literal(parameter.ParameterName) + ", " + BoolLiteral(parameter.HasDefaultValue) + ", " +
                    parameter.DefaultValueExpression + ")";
            case ParameterBindingKind.Query:
                return CreateQueryArgumentExpression(parameter, "BindQuery");
            case ParameterBindingKind.Option:
                return "global::CoAP.Server.Routing.CoapGeneratedEndpointHelpers.BindOption<" +
                    parameter.TypeName + ">(context, " + parameter.OptionExpression + ", " +
                    Literal(parameter.ParameterName) + ", " + BoolLiteral(parameter.HasDefaultValue) + ", " +
                    parameter.DefaultValueExpression + ")";
            case ParameterBindingKind.Payload:
                return "global::CoAP.Server.Routing.CoapGeneratedEndpointHelpers.BindPayload<" +
                    parameter.TypeName + ">(context, " + Literal(parameter.ParameterName) + ", " +
                    BoolLiteral(parameter.HasDefaultValue) + ", " + parameter.DefaultValueExpression + ")";
            case ParameterBindingKind.InferredCollection:
                return CreateQueryArgumentExpression(parameter, "BindInferred");
            default:
                return "global::CoAP.Server.Routing.CoapGeneratedEndpointHelpers.BindInferred<" +
                    parameter.TypeName + ">(context, " + Literal(parameter.ParameterName) + ", " +
                    BoolLiteral(parameter.HasDefaultValue) + ", " + parameter.DefaultValueExpression + ")";
        }
    }

    private static string CreateQueryArgumentExpression(ParameterModel parameter, string prefix)
    {
        if (parameter.CollectionKind == CollectionKind.Array)
        {
            return "global::CoAP.Server.Routing.CoapGeneratedEndpointHelpers." + prefix + "Array<" +
                parameter.ElementTypeName + ">(context, " + Literal(parameter.BindingName) + ", " +
                Literal(parameter.ParameterName) + ", " + BoolLiteral(parameter.HasDefaultValue) + ", " +
                parameter.DefaultValueExpression + ")";
        }

        if (parameter.CollectionKind == CollectionKind.List)
        {
            return "global::CoAP.Server.Routing.CoapGeneratedEndpointHelpers." + prefix + "List<" +
                parameter.ElementTypeName + ">(context, " + Literal(parameter.BindingName) + ", " +
                Literal(parameter.ParameterName) + ", " + BoolLiteral(parameter.HasDefaultValue) + ", " +
                ToListDefaultExpression(parameter) + ")";
        }

        return "global::CoAP.Server.Routing.CoapGeneratedEndpointHelpers." + prefix + "Value<" +
            parameter.TypeName + ">(context, " + Literal(parameter.BindingName) + ", " +
            Literal(parameter.ParameterName) + ", " + BoolLiteral(parameter.HasDefaultValue) + ", " +
            parameter.DefaultValueExpression + ")";
    }

    private static string ToListDefaultExpression(ParameterModel parameter)
    {
        return parameter.HasDefaultValue && parameter.DefaultValueExpression == "null"
            ? "null"
            : "default";
    }

    private static bool TryCreateParameterModel(IParameterSymbol parameter, out ParameterModel model)
    {
        model = null!;
        if (!IsTypeUsable(parameter.Type))
        {
            return false;
        }

        var parameterName = string.IsNullOrEmpty(parameter.Name) ? "parameter" : parameter.Name;
        var typeName = GetTypeName(parameter.Type);
        var defaultValueExpression = parameter.HasExplicitDefaultValue
            ? FormatDefaultValue(parameter.ExplicitDefaultValue, parameter.Type)
            : "default";
        var collectionKind = GetCollectionKind(parameter.Type, out var elementType);
        var elementTypeName = elementType == null ? string.Empty : GetTypeName(elementType);

        if (IsType(parameter.Type, "CoAP.Server.Routing.CoapRouteContext"))
        {
            model = new ParameterModel(
                ParameterBindingKind.Context,
                typeName,
                parameterName,
                parameterName,
                string.Empty,
                parameter.HasExplicitDefaultValue,
                defaultValueExpression,
                CollectionKind.None,
                string.Empty);
            return true;
        }

        if (IsType(parameter.Type, "System.Threading.CancellationToken"))
        {
            model = new ParameterModel(
                ParameterBindingKind.CancellationToken,
                typeName,
                parameterName,
                parameterName,
                string.Empty,
                parameter.HasExplicitDefaultValue,
                defaultValueExpression,
                CollectionKind.None,
                string.Empty);
            return true;
        }

        if (InheritsFromOrEquals(parameter.Type, "System.Net.EndPoint"))
        {
            model = new ParameterModel(
                ParameterBindingKind.RemoteEndPoint,
                typeName,
                parameterName,
                parameterName,
                string.Empty,
                parameter.HasExplicitDefaultValue,
                defaultValueExpression,
                CollectionKind.None,
                string.Empty);
            return true;
        }

        var attributes = parameter.GetAttributes();
        var payloadAttribute = attributes.FirstOrDefault(attribute => IsAttribute(attribute, CoapFromPayloadAttributeName));
        if (payloadAttribute != null)
        {
            model = CreateParameter(
                ParameterBindingKind.Payload,
                typeName,
                parameterName,
                parameterName,
                string.Empty,
                parameter.HasExplicitDefaultValue,
                defaultValueExpression,
                CollectionKind.None,
                string.Empty);
            return true;
        }

        var optionAttribute = attributes.FirstOrDefault(attribute => IsAttribute(attribute, CoapFromOptionAttributeName));
        if (optionAttribute != null)
        {
            var optionExpression = optionAttribute.ConstructorArguments.Length > 0 &&
                TryFormatTypedConstant(optionAttribute.ConstructorArguments[0], out var formattedOption)
                ? formattedOption
                : "default(global::CoAP.OptionType)";

            model = CreateParameter(
                ParameterBindingKind.Option,
                typeName,
                parameterName,
                parameterName,
                optionExpression,
                parameter.HasExplicitDefaultValue,
                defaultValueExpression,
                CollectionKind.None,
                string.Empty);
            return true;
        }

        var routeAttribute = attributes.FirstOrDefault(attribute => IsAttribute(attribute, CoapFromRouteAttributeName));
        if (routeAttribute != null)
        {
            model = CreateParameter(
                ParameterBindingKind.Route,
                typeName,
                parameterName,
                GetConfiguredName(routeAttribute, parameterName),
                string.Empty,
                parameter.HasExplicitDefaultValue,
                defaultValueExpression,
                CollectionKind.None,
                string.Empty);
            return true;
        }

        var queryAttribute = attributes.FirstOrDefault(attribute => IsAttribute(attribute, CoapFromQueryAttributeName));
        if (queryAttribute != null)
        {
            model = CreateParameter(
                ParameterBindingKind.Query,
                typeName,
                parameterName,
                GetConfiguredName(queryAttribute, parameterName),
                string.Empty,
                parameter.HasExplicitDefaultValue,
                defaultValueExpression,
                collectionKind,
                elementTypeName);
            return true;
        }

        if (IsRawPayloadType(parameter.Type))
        {
            model = CreateParameter(
                ParameterBindingKind.Payload,
                typeName,
                parameterName,
                parameterName,
                string.Empty,
                parameter.HasExplicitDefaultValue,
                defaultValueExpression,
                CollectionKind.None,
                string.Empty);
            return true;
        }

        if (collectionKind != CollectionKind.None)
        {
            model = CreateParameter(
                ParameterBindingKind.InferredCollection,
                typeName,
                parameterName,
                parameterName,
                string.Empty,
                parameter.HasExplicitDefaultValue,
                defaultValueExpression,
                collectionKind,
                elementTypeName);
            return true;
        }

        model = CreateParameter(
            ParameterBindingKind.Inferred,
            typeName,
            parameterName,
            parameterName,
            string.Empty,
            parameter.HasExplicitDefaultValue,
            defaultValueExpression,
            CollectionKind.None,
            string.Empty);
        return true;
    }

    private static ParameterModel CreateParameter(
        ParameterBindingKind bindingKind,
        string typeName,
        string parameterName,
        string bindingName,
        string optionExpression,
        bool hasDefaultValue,
        string defaultValueExpression,
        CollectionKind collectionKind,
        string elementTypeName)
    {
        return new ParameterModel(
            bindingKind,
            typeName,
            parameterName,
            bindingName,
            optionExpression,
            hasDefaultValue,
            defaultValueExpression,
            collectionKind,
            elementTypeName);
    }

    private static MethodRouteModel? CreateMethodRoute(AttributeData attribute)
    {
        if (attribute.AttributeClass == null)
        {
            return null;
        }

        var template = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string ?? string.Empty
            : string.Empty;
        var metadataName = GetFullMetadataName(attribute.AttributeClass);

        return metadataName switch
        {
            CoapGetAttributeName => new MethodRouteModel("GET", "global::CoAP.Method.GET", template),
            CoapPostAttributeName => new MethodRouteModel("POST", "global::CoAP.Method.POST", template),
            CoapPutAttributeName => new MethodRouteModel("PUT", "global::CoAP.Method.PUT", template),
            CoapDeleteAttributeName => new MethodRouteModel("DELETE", "global::CoAP.Method.DELETE", template),
            CoapObserveAttributeName => new MethodRouteModel("GET", "global::CoAP.Method.GET", template),
            _ => null
        };
    }

    private static IMethodSymbol? SelectConstructor(INamedTypeSymbol resourceType)
    {
        var constructors = resourceType
            .InstanceConstructors
            .Where(static constructor => !constructor.IsStatic && IsAccessible(constructor))
            .OrderByDescending(static constructor => constructor.Parameters.Length)
            .ToArray();
        if (constructors.Length == 0)
        {
            return null;
        }

        if (constructors.Length > 1 &&
            constructors[0].Parameters.Length == constructors[1].Parameters.Length)
        {
            return null;
        }

        return constructors[0];
    }

    private static bool IsResourceType(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (IsAttribute(attribute, CoapResourceAttributeName) ||
                IsAttribute(attribute, CoapControllerAttributeName))
            {
                return true;
            }
        }

        return type.Name.EndsWith("CoapResource", StringComparison.Ordinal) ||
            type.Name.EndsWith("CoapController", StringComparison.Ordinal);
    }

    private static string[] GetRoutePrefixes(ImmutableArray<AttributeData> attributes)
    {
        var prefixes = attributes
            .Where(attribute => IsAttribute(attribute, CoapRouteAttributeName))
            .Select(static attribute => attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string ?? string.Empty
                : string.Empty)
            .ToArray();

        return prefixes.Length == 0 ? new[] { string.Empty } : prefixes;
    }

    private static List<string> GetMetadataExpressions(ImmutableArray<AttributeData> attributes)
    {
        var metadata = new List<string>();
        foreach (var attribute in attributes)
        {
            if (InheritsFrom(attribute.AttributeClass, CoapRouteAttributeName))
            {
                continue;
            }

            if (TryCreateAttributeExpression(attribute, out var expression))
            {
                metadata.Add(expression);
            }
        }

        return metadata;
    }

    private static bool TryCreateAttributeExpression(AttributeData attribute, out string expression)
    {
        expression = string.Empty;
        if (attribute.AttributeClass == null || !IsAccessible(attribute.AttributeClass))
        {
            return false;
        }

        var arguments = new List<string>(attribute.ConstructorArguments.Length);
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (!TryFormatTypedConstant(argument, out var formattedArgument))
            {
                return false;
            }

            arguments.Add(formattedArgument);
        }

        var initializers = new List<string>(attribute.NamedArguments.Length);
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (!TryFormatTypedConstant(namedArgument.Value, out var formattedValue))
            {
                return false;
            }

            initializers.Add(EscapeIdentifier(namedArgument.Key) + " = " + formattedValue);
        }

        expression = "new " + GetTypeName(attribute.AttributeClass) + "(" + string.Join(", ", arguments) + ")";
        if (initializers.Count > 0)
        {
            expression += " { " + string.Join(", ", initializers) + " }";
        }

        return true;
    }

    private static bool TryFormatTypedConstant(TypedConstant constant, out string expression)
    {
        expression = string.Empty;
        if (constant.IsNull)
        {
            expression = "null";
            return true;
        }

        if (constant.Kind == TypedConstantKind.Array)
        {
            if (constant.Type is not IArrayTypeSymbol arrayType)
            {
                return false;
            }

            var elementType = GetTypeName(arrayType.ElementType);
            var values = new List<string>(constant.Values.Length);
            foreach (var value in constant.Values)
            {
                if (!TryFormatTypedConstant(value, out var formattedValue))
                {
                    return false;
                }

                values.Add(formattedValue);
            }

            expression = "new " + elementType + "[] { " + string.Join(", ", values) + " }";
            return true;
        }

        if (constant.Kind == TypedConstantKind.Type)
        {
            if (constant.Value is not ITypeSymbol typeValue)
            {
                return false;
            }

            expression = "typeof(" + GetTypeName(typeValue) + ")";
            return true;
        }

        if (constant.Type?.TypeKind == TypeKind.Enum)
        {
            expression = "(" + GetTypeName(constant.Type) + ")" +
                Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        expression = FormatPrimitiveValue(constant.Value, constant.Type);
        return expression.Length > 0;
    }

    private static string FormatDefaultValue(object? value, ITypeSymbol type)
    {
        if (value == null)
        {
            return "null";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return "(" + GetTypeName(type) + ")" +
                Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        }

        return FormatPrimitiveValue(value, type);
    }

    private static string FormatPrimitiveValue(object? value, ITypeSymbol? type)
    {
        switch (value)
        {
            case null:
                return "null";
            case string text:
                return Literal(text);
            case char character:
                return SymbolDisplay.FormatLiteral(character, quote: true);
            case bool boolean:
                return boolean ? "true" : "false";
            case byte or sbyte or short or ushort or int:
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            case uint unsignedInteger:
                return unsignedInteger.ToString(CultureInfo.InvariantCulture) + "U";
            case long longInteger:
                return longInteger.ToString(CultureInfo.InvariantCulture) + "L";
            case ulong unsignedLong:
                return unsignedLong.ToString(CultureInfo.InvariantCulture) + "UL";
            case float floatValue:
                if (float.IsNaN(floatValue))
                {
                    return "float.NaN";
                }

                if (float.IsPositiveInfinity(floatValue))
                {
                    return "float.PositiveInfinity";
                }

                if (float.IsNegativeInfinity(floatValue))
                {
                    return "float.NegativeInfinity";
                }

                return floatValue.ToString("R", CultureInfo.InvariantCulture) + "F";
            case double doubleValue:
                if (double.IsNaN(doubleValue))
                {
                    return "double.NaN";
                }

                if (double.IsPositiveInfinity(doubleValue))
                {
                    return "double.PositiveInfinity";
                }

                if (double.IsNegativeInfinity(doubleValue))
                {
                    return "double.NegativeInfinity";
                }

                return doubleValue.ToString("R", CultureInfo.InvariantCulture) + "D";
            case decimal decimalValue:
                return decimalValue.ToString(CultureInfo.InvariantCulture) + "M";
            default:
                if (type != null && type.TypeKind == TypeKind.Enum)
                {
                    return "(" + GetTypeName(type) + ")" +
                        Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                }

                return string.Empty;
        }
    }

    private static string GetConfiguredName(AttributeData attribute, string fallbackName)
    {
        if (attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is string configuredName &&
            !string.IsNullOrWhiteSpace(configuredName))
        {
            return configuredName;
        }

        return fallbackName;
    }

    private static CollectionKind GetCollectionKind(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        elementType = null;
        if (type.SpecialType == SpecialType.System_String || IsByteArray(type))
        {
            return CollectionKind.None;
        }

        if (type is IArrayTypeSymbol arrayType && arrayType.Rank == 1)
        {
            elementType = arrayType.ElementType;
            return CollectionKind.Array;
        }

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var definition = GetFullMetadataName(namedType.OriginalDefinition);
            if (definition == "System.Collections.Generic.IEnumerable`1" ||
                definition == "System.Collections.Generic.IReadOnlyList`1" ||
                definition == "System.Collections.Generic.ICollection`1" ||
                definition == "System.Collections.Generic.IList`1" ||
                definition == "System.Collections.Generic.List`1")
            {
                elementType = namedType.TypeArguments[0];
                return CollectionKind.List;
            }
        }

        return CollectionKind.None;
    }

    private static bool IsRawPayloadType(ITypeSymbol type)
    {
        return IsType(type, "System.ReadOnlyMemory`1", SpecialType.System_Byte) ||
            IsByteArray(type) ||
            InheritsFromOrEquals(type, "System.IO.Stream");
    }

    private static bool IsByteArray(ITypeSymbol type)
    {
        return type is IArrayTypeSymbol arrayType &&
            arrayType.Rank == 1 &&
            arrayType.ElementType.SpecialType == SpecialType.System_Byte;
    }

    private static bool IsTypeUsable(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return IsTypeUsable(arrayType.ElementType);
        }

        if (type is INamedTypeSymbol namedType)
        {
            if (!IsAccessible(namedType))
            {
                return false;
            }

            foreach (var typeArgument in namedType.TypeArguments)
            {
                if (!IsTypeUsable(typeArgument))
                {
                    return false;
                }
            }

            return true;
        }

        return IsAccessible(type);
    }

    private static bool IsAccessible(ISymbol symbol)
    {
        if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
        {
            return false;
        }

        var containingType = symbol.ContainingType;
        while (containingType != null)
        {
            if (containingType.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
            {
                return false;
            }

            containingType = containingType.ContainingType;
        }

        return true;
    }

    private static bool IsCoapMethodRouteAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass == null)
        {
            return false;
        }

        var metadataName = GetFullMetadataName(attribute.AttributeClass);
        return metadataName == CoapGetAttributeName ||
            metadataName == CoapPostAttributeName ||
            metadataName == CoapPutAttributeName ||
            metadataName == CoapDeleteAttributeName ||
            metadataName == CoapObserveAttributeName;
    }

    private static bool IsAttribute(AttributeData attribute, string metadataName)
    {
        return attribute.AttributeClass != null &&
            GetFullMetadataName(attribute.AttributeClass) == metadataName;
    }

    private static bool InheritsFrom(ITypeSymbol? type, string metadataName)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (GetFullMetadataName(current) == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool InheritsFromOrEquals(ITypeSymbol type, string metadataName)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (GetFullMetadataName(current) == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsType(ITypeSymbol type, string metadataName)
    {
        return type is INamedTypeSymbol namedType &&
            GetFullMetadataName(namedType.OriginalDefinition) == metadataName;
    }

    private static bool IsType(ITypeSymbol type, string genericMetadataName, SpecialType singleTypeArgument)
    {
        return type is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.TypeArguments.Length == 1 &&
            namedType.TypeArguments[0].SpecialType == singleTypeArgument &&
            GetFullMetadataName(namedType.OriginalDefinition) == genericMetadataName;
    }

    private static string GetFullMetadataName(ISymbol symbol)
    {
        if (symbol.ContainingType != null)
        {
            return GetFullMetadataName(symbol.ContainingType) + "." + symbol.MetadataName;
        }

        if (symbol.ContainingNamespace == null || symbol.ContainingNamespace.IsGlobalNamespace)
        {
            return symbol.MetadataName;
        }

        return symbol.ContainingNamespace.ToDisplayString() + "." + symbol.MetadataName;
    }

    private static string GetTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(TypeDisplayFormat);
    }

    private static string CombineTemplates(string prefix, string template)
    {
        var normalizedPrefix = (prefix ?? string.Empty).Trim('/');
        var normalizedTemplate = (template ?? string.Empty).Trim('/');
        if (normalizedPrefix.Length == 0)
        {
            return normalizedTemplate;
        }

        if (normalizedTemplate.Length == 0)
        {
            return normalizedPrefix;
        }

        return normalizedPrefix + "/" + normalizedTemplate;
    }

    private static string GetDisplayName(
        INamedTypeSymbol resourceType,
        IMethodSymbol method,
        string methodName,
        string routeTemplate)
    {
        return resourceType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) +
            "." + method.Name + " (" + methodName + " " + routeTemplate + ")";
    }

    private static string Literal(string value)
    {
        return SymbolDisplay.FormatLiteral(value ?? string.Empty, quote: true);
    }

    private static string BoolLiteral(bool value)
    {
        return value ? "true" : "false";
    }

    private static string EscapeIdentifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None &&
            SyntaxFacts.GetContextualKeywordKind(value) == SyntaxKind.None
            ? value
            : "@" + value;
    }

    private static string EscapeForStringContent(string value)
    {
        return Literal(value).Trim('"');
    }

    private sealed class EndpointModel
    {
        public EndpointModel(
            ResourceModel resource,
            string methodName,
            bool returnsVoid,
            ParameterModel[] parameters,
            string methodExpression,
            string routeTemplate,
            string[] metadataExpressions,
            string displayName,
            int sourceOrder,
            int routeOrder)
        {
            Resource = resource;
            MethodName = methodName;
            ReturnsVoid = returnsVoid;
            Parameters = parameters;
            MethodExpression = methodExpression;
            RouteTemplate = routeTemplate;
            MetadataExpressions = metadataExpressions;
            DisplayName = displayName;
            SourceOrder = sourceOrder;
            RouteOrder = routeOrder;
        }

        public ResourceModel Resource { get; }

        public string MethodName { get; }

        public bool ReturnsVoid { get; }

        public ParameterModel[] Parameters { get; }

        public string MethodExpression { get; }

        public string RouteTemplate { get; }

        public string[] MetadataExpressions { get; }

        public string DisplayName { get; }

        public int SourceOrder { get; }

        public int RouteOrder { get; }
    }

    private sealed class ResourceModel
    {
        public ResourceModel(string key, string typeName, ConstructorParameterModel[] constructorParameters)
        {
            Key = key;
            TypeName = typeName;
            ConstructorParameters = constructorParameters;
        }

        public string Key { get; }

        public string TypeName { get; }

        public ConstructorParameterModel[] ConstructorParameters { get; }
    }

    private sealed class ConstructorParameterModel
    {
        public ConstructorParameterModel(string typeName)
        {
            TypeName = typeName;
        }

        public string TypeName { get; }
    }

    private sealed class MethodRouteModel
    {
        public MethodRouteModel(string methodName, string methodExpression, string template)
        {
            MethodName = methodName;
            MethodExpression = methodExpression;
            Template = template;
        }

        public string MethodName { get; }

        public string MethodExpression { get; }

        public string Template { get; }
    }

    private sealed class ParameterModel
    {
        public ParameterModel(
            ParameterBindingKind bindingKind,
            string typeName,
            string parameterName,
            string bindingName,
            string optionExpression,
            bool hasDefaultValue,
            string defaultValueExpression,
            CollectionKind collectionKind,
            string elementTypeName)
        {
            BindingKind = bindingKind;
            TypeName = typeName;
            ParameterName = parameterName;
            BindingName = bindingName;
            OptionExpression = optionExpression;
            HasDefaultValue = hasDefaultValue;
            DefaultValueExpression = defaultValueExpression;
            CollectionKind = collectionKind;
            ElementTypeName = elementTypeName;
        }

        public ParameterBindingKind BindingKind { get; }

        public string TypeName { get; }

        public string ParameterName { get; }

        public string BindingName { get; }

        public string OptionExpression { get; }

        public bool HasDefaultValue { get; }

        public string DefaultValueExpression { get; }

        public CollectionKind CollectionKind { get; }

        public string ElementTypeName { get; }
    }

    private enum ParameterBindingKind
    {
        Inferred,
        InferredCollection,
        Context,
        CancellationToken,
        RemoteEndPoint,
        Route,
        Query,
        Option,
        Payload
    }

    private enum CollectionKind
    {
        None,
        Array,
        List
    }
}
