﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace SwashbuckleDocTools.Tests;

public static class SwaggerValidator
{
    /// <summary>
    /// Validates a swagger (aka OpenAPI) file generated by a particular DotNet API service against a list
    /// of rules you specify.
    /// </summary>
    /// <param name="apiTitles">The titles of API files to be tested.  Each one will be fetched and compared
    /// against the list of rules you specify.</param>
    /// <typeparam name="T">
    /// A Startup object that can instantiate a test instance of your web server with Swagger
    /// enabled.  This type should at least have the methods `Configure` and `ConfigureServices` defined.  I wish
    /// DotNet had a standard interface type for this, but since it doesn't, I'm leaving this as a blank class.
    /// </typeparam>
    /// <returns>A list of errors in your swagger file or files according to the rules you specified</returns>
    public static List<string> Validate<T>(IEnumerable<string> apiTitles) where T : class
    {
        var errors = new List<string>();
        var server = new TestServer(new WebHostBuilder().UseStartup<T>());
        var swagger = server.Services.GetService<ISwaggerProvider>();
        if (swagger == null)
        {
            errors.Add($"ISwaggerProvider not found within type {nameof(T)}");
        }
        else
        {
            foreach (var title in apiTitles)
            {
                InspectSwaggerDoc(swagger, title, errors);
            }
        }

        return errors;
    }

    private static void InspectSwaggerDoc(ISwaggerProvider swagger, string title, List<string> errors)
    {
        var openApiDoc = swagger.GetSwagger(title);
        if (openApiDoc == null)
        {
            errors.Add($"Swagger definition for {title} not found");
        }
        else if (openApiDoc.Paths.Count == 0)
        {
            errors.Add($"Swagger definition for {title} has no endpoints");
        }
        else
        {
            foreach (var path in openApiDoc.Paths)
            {
                foreach (var method in path.Value.Operations)
                {
                    InspectEndpoint(method, path, errors);
                }
            }

            foreach (var schema in openApiDoc.Components.Schemas)
            {
                InspectSchema(schema.Key, schema.Value, errors);
            }
        }
    }

    private static void InspectSchema(string key, OpenApiSchema schema, List<string> errors)
    {
        // Ignore schemas that are enums
        if (schema.Enum.Count > 0)
        {
            return;
        }

        Inspect(schema.Description, $"Schema {key} does not have a <summary> xmldoc on the class.", errors);
        foreach (var property in schema.Properties)
        {
            Inspect(property.Value.Description, $"Schema {key} does not have a <summary> xmldoc for property {property.Key}.", errors);
        }
    }

    private static void Inspect(string text, string errorIfMissing, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add(errorIfMissing);
        }
    }

    private static void InspectEndpoint(KeyValuePair<OperationType, OpenApiOperation> method, KeyValuePair<string, OpenApiPathItem> path, List<string> errors)
    {
        Inspect(method.Value.Summary,
            $"Method {method.Key} for path {path.Key} does not have a <summary> xmldoc.", errors);
        Inspect(method.Value.Description,
            $"Method {method.Key} for path {path.Key} does not have a <remarks> xmldoc.", errors);
        foreach (var parameter in method.Value.Parameters)
        {
            Inspect(parameter.Description,
                $"Method {method.Key} for path {path.Key} does not have a <param name=\"{parameter.Name}\"> xmldoc.", errors);
        }

        foreach (var response in method.Value.Responses)
        {
            Inspect(response.Value.Description,
                $"Method {method.Key} for path {path.Key} does not have a <returns> xmldoc for return type {response.Key}.", errors);
        }

        if (method.Value.RequestBody != null)
        {
            Inspect(method.Value.RequestBody.Description,
                $"Method {method.Key} for path {path.Key} does not have a <param> xmldoc for the request body.", errors);
        }
    }
}
