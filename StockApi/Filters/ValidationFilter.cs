using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Collections.Generic;

namespace StockApi.Filters;

public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var arg = ctx.Arguments.FirstOrDefault(a => a is T) as T;
        if (arg is null) return await next(ctx);

        var results = new List<ValidationResult>();
        var context = new ValidationContext(arg);
        if (!Validator.TryValidateObject(arg, context, results, validateAllProperties: true))
        {
            var errors = results
                .GroupBy(r => r.MemberNames.FirstOrDefault() ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage ?? "Invalid").ToArray());

            return Results.ValidationProblem(errors);
        }

        return await next(ctx);
    }
}
