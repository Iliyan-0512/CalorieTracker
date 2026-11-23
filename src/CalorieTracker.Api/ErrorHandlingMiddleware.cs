using CalorieTracker.Core.Services;

namespace CalorieTracker.Api;

/// <summary>Превръща грешките от валидацията в услугите в 400 / 404 отговори.</summary>
public class ErrorHandlingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ArgumentException ex)
        {
            await Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(context);
        }
        catch (KeyNotFoundException ex)
        {
            await Results.Problem(ex.Message, statusCode: StatusCodes.Status404NotFound).ExecuteAsync(context);
        }
        catch (AiNotConfiguredException ex)
        {
            await Results.Problem(ex.Message + " Задайте Anthropic:ApiKey (dotnet user-secrets) или ANTHROPIC_API_KEY.",
                statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context);
        }
        catch (AiEstimateException ex)
        {
            await Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway).ExecuteAsync(context);
        }
    }
}
