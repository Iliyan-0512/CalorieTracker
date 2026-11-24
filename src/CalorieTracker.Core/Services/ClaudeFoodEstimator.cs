using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using CalorieTracker.Core.Models;

namespace CalorieTracker.Core.Services;

/// <summary>Една храна, разпозната от AI. Нутриентите са на 100 g, а Grams е изчислената порция.</summary>
public record AiFoodEstimate(
    string Name,
    double Grams,
    double Calories,
    double Protein,
    double Carbs,
    double Fat,
    double Fiber,
    string Confidence,
    string? Note)
{
    public Food ToFood() => new()
    {
        Name = Name,
        Calories = Calories,
        Protein = Protein,
        Carbs = Carbs,
        Fat = Fat,
        Fiber = Fiber,
        DefaultServingGrams = Grams > 0 ? Grams : 100,
        Source = FoodSource.AiEstimate
    };
}

public class AiNotConfiguredException() : Exception("Не е зададен Anthropic API ключ.");

public class AiEstimateException(string message, Exception? inner = null) : Exception(message, inner);

public interface IFoodAiEstimator
{
    Task<bool> IsConfiguredAsync();
    Task<IReadOnlyList<AiFoodEstimate>> EstimateFromTextAsync(string description, CancellationToken ct = default);
    Task<IReadOnlyList<AiFoodEstimate>> EstimateFromPhotoAsync(byte[] image, string mediaType, string? hint, CancellationToken ct = default);
}

/// <summary>Откъде да вземем API ключа: в API-то от конфигурацията, в мобилното приложение от SecureStorage.</summary>
public interface IAiKeyProvider
{
    Task<string?> GetApiKeyAsync();

    /// <summary>Кой модел на Claude да се ползва (вижте ClaudeFoodEstimator.Models).</summary>
    Task<string> GetModelAsync() => Task.FromResult(ClaudeFoodEstimator.DefaultModel);
}

public record AiModelOption(string Id, string Label);

/// <summary>Оценява калории и макроси с Claude – за храни, които ги няма в базата, и по снимка на чинията.</summary>
public class ClaudeFoodEstimator(IAiKeyProvider keys) : IFoodAiEstimator
{
    /// <summary>Haiku е десетки пъти по-евтин и напълно достатъчен за оценка на храна.</summary>
    public const string DefaultModel = "claude-haiku-5-5";

    public static IReadOnlyList<AiModelOption> Models { get; } =
    [
        new("claude-haiku-5-5", "Haiku 5.5 – най-евтин (~0.1 ¢ на заявка)"),
        new("claude-sonnet-5-5", "Sonnet 5.5 – по-точен (~1–1.5 ¢)"),
        new("claude-opus-5-5", "Opus 5.5 – най-точен (~2–3 ¢)")
    ];

    private const string SystemPrompt = """
        Ти си диетолог, който оценява хранителни стойности за приложение за броене на калории.
        Потребителят описва храна с текст или изпраща снимка на храна. Раздели я на отделни храни
        (напр. „мусака със салата“ са две) и за всяка дай:
        - name: кратко име на български
        - grams: реалистично тегло на порцията в грамове – от описанието, ако е посочено, иначе по снимката или типична порция
        - calories, protein, carbs, fat, fiber: стойности на 100 g от готовата храна (както се яде)
        - confidence: "high", "medium" или "low" според това колко сигурна е оценката
        - note: едно кратко изречение на български с допускането, което си направил (или празен низ)
        Ползвай стойности, близки до USDA и българските етикети. За български ястия (баница, мусака,
        таратор, шопска салата, кебапче…) ползвай типичната българска рецепта.
        Ако на снимката или в текста няма храна, върни празен списък items.
        """;

    private static readonly Dictionary<string, JsonElement> Schema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            items = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string" },
                        grams = new { type = "number" },
                        calories = new { type = "number" },
                        protein = new { type = "number" },
                        carbs = new { type = "number" },
                        fat = new { type = "number" },
                        fiber = new { type = "number" },
                        confidence = new { type = "string", @enum = new[] { "high", "medium", "low" } },
                        note = new { type = "string" }
                    },
                    required = new[] { "name", "grams", "calories", "protein", "carbs", "fat", "fiber", "confidence", "note" },
                    additionalProperties = false
                }
            }
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "items" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false)
    };

    public async Task<bool> IsConfiguredAsync() => !string.IsNullOrWhiteSpace(await keys.GetApiKeyAsync());

    public Task<IReadOnlyList<AiFoodEstimate>> EstimateFromTextAsync(string description, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Опишете храната.");
        return SendAsync([new BetaTextBlockParam { Text = description.Trim() }], ct);
    }

    public Task<IReadOnlyList<AiFoodEstimate>> EstimateFromPhotoAsync(byte[] image, string mediaType, string? hint, CancellationToken ct = default)
    {
        if (image.Length == 0) throw new ArgumentException("Празна снимка.");

        var text = string.IsNullOrWhiteSpace(hint)
            ? "Какво има на снимката и колко е?"
            : $"Какво има на снимката и колко е? Допълнение от потребителя: {hint.Trim()}";

        return SendAsync(
        [
            new BetaImageBlockParam
            {
                Source = new BetaBase64ImageSource { Data = Convert.ToBase64String(image), MediaType = mediaType }
            },
            new BetaTextBlockParam { Text = text }
        ], ct);
    }

    private async Task<IReadOnlyList<AiFoodEstimate>> SendAsync(List<BetaContentBlockParam> content, CancellationToken ct)
    {
        var apiKey = await keys.GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey)) throw new AiNotConfiguredException();

        var model = await keys.GetModelAsync();
        if (Models.All(m => m.Id != model)) model = DefaultModel;

        var client = new AnthropicClient { ApiKey = apiKey };

        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = 16000,
            System = SystemPrompt,
            OutputConfig = new BetaOutputConfig
            {
                Effort = Effort.Medium,
                Format = new BetaJsonOutputFormat { Schema = Schema }
            },
            Messages = [new BetaUserMessageParam { Content = content }]
        };

        // При отказ от класификатора за безопасност Opus/Sonnet автоматично се обслужват от резервен модел.
        // Haiku 5.5 няма такъв резервен вариант – там просто показваме съобщение за отказ.
        if (model != "claude-haiku-5-5")
        {
            parameters = parameters with
            {
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                Fallbacks = new Default()
            };
        }

        BetaMessage response;
        try
        {
            response = await client.Beta.Messages.Create(parameters, ct);
        }
        catch (AnthropicApiException ex)
        {
            throw new AiEstimateException($"Грешка от AI услугата: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AiEstimateException("Няма връзка с AI услугата. Проверете интернета.", ex);
        }

        if (response.StopReason == "refusal")
            throw new AiEstimateException("AI отказа да оцени тази заявка.");

        var json = string.Concat(response.Content
            .Select(b => b.TryPickText(out var t) ? t.Text : null)
            .OfType<string>());

        return Parse(json);
    }

    internal static IReadOnlyList<AiFoodEstimate> Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("items").EnumerateArray()
                .Select(i => new AiFoodEstimate(
                    i.GetProperty("name").GetString() ?? "Храна",
                    Clamp(i.GetProperty("grams").GetDouble(), 1, 5000),
                    Clamp(i.GetProperty("calories").GetDouble(), 0, 900),
                    Clamp(i.GetProperty("protein").GetDouble(), 0, 100),
                    Clamp(i.GetProperty("carbs").GetDouble(), 0, 100),
                    Clamp(i.GetProperty("fat").GetDouble(), 0, 100),
                    Clamp(i.GetProperty("fiber").GetDouble(), 0, 100),
                    i.GetProperty("confidence").GetString() ?? "low",
                    i.GetProperty("note").GetString() is { Length: > 0 } note ? note : null))
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiEstimateException("AI върна неочакван отговор. Опитайте отново.", ex);
        }
    }

    private static double Clamp(double value, double min, double max) => Math.Round(Math.Clamp(value, min, max), 1);
}
