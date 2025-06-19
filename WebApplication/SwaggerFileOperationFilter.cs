using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace WebApplication
{
    public class SwaggerFileOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var fileParameters = context.MethodInfo.GetParameters()
                .Where(p => p.ParameterType == typeof(IFormFile) || 
                           p.ParameterType == typeof(IEnumerable<IFormFile>) ||
                           p.ParameterType == typeof(IFormFile[]) ||
                           p.ParameterType == typeof(List<IFormFile>))
                .ToList();

            if (fileParameters.Any())
            {
                operation.RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["multipart/form-data"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, OpenApiSchema>()
                            }
                        }
                    }
                };

                // 添加所有参数到form-data schema
                foreach (var parameter in context.MethodInfo.GetParameters())
                {
                    if (parameter.ParameterType == typeof(IFormFile))
                    {
                        operation.RequestBody.Content["multipart/form-data"].Schema.Properties[parameter.Name] = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "binary"
                        };
                    }
                    else if (parameter.ParameterType == typeof(string))
                    {
                        operation.RequestBody.Content["multipart/form-data"].Schema.Properties[parameter.Name] = new OpenApiSchema
                        {
                            Type = "string"
                        };
                    }
                    else if (parameter.ParameterType == typeof(Guid))
                    {
                        operation.RequestBody.Content["multipart/form-data"].Schema.Properties[parameter.Name] = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "uuid"
                        };
                    }
                    else if (parameter.ParameterType == typeof(DateTime))
                    {
                        operation.RequestBody.Content["multipart/form-data"].Schema.Properties[parameter.Name] = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "date-time"
                        };
                    }
                }
            }
        }
    }
} 