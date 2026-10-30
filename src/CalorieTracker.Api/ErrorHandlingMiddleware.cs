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
    }
}
