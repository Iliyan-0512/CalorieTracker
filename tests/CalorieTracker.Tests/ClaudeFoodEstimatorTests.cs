using CalorieTracker.Core.Services;

namespace CalorieTracker.Tests;

public class ClaudeFoodEstimatorTests
{
    [Fact]
    public void Parse_ReadsItemsAndClampsOutOfRangeValues()
    {
        const string json = """
            {"items":[
              {"name":"Баница","grams":180,"calories":300,"protein":9,"carbs":27,"fat":17,"fiber":1,"confidence":"high","note":""},
              {"name":"Айрян","grams":-5,"calories":2000,"protein":3,"carbs":4,"fat":1.5,"fiber":0,"confidence":"low","note":"Типична чаша"}
            ]}
            """;

        var items = ClaudeFoodEstimator.Parse(json);

        Assert.Equal(2, items.Count);
        Assert.Equal("Баница", items[0].Name);
        Assert.Null(items[0].Note);
        Assert.Equal(1, items[1].Grams);       // отрицателното тегло се коригира
        Assert.Equal(900, items[1].Calories);  // над 900 kcal/100 g е невъзможно
        Assert.Equal("Типична чаша", items[1].Note);
    }

    [Fact]
    public void Parse_ThrowsFriendlyErrorOnBadJson()
    {
        Assert.Throws<AiEstimateException>(() => ClaudeFoodEstimator.Parse("not json"));
        Assert.Throws<AiEstimateException>(() => ClaudeFoodEstimator.Parse("""{"foo":1}"""));
    }

    [Fact]
    public async Task Estimate_WithoutKeyThrowsNotConfigured()
    {
        var estimator = new ClaudeFoodEstimator(new NoKey());
        Assert.False(await estimator.IsConfiguredAsync());
        await Assert.ThrowsAsync<AiNotConfiguredException>(() => estimator.EstimateFromTextAsync("ябълка"));
    }

    private class NoKey : IAiKeyProvider
    {
        public Task<string?> GetApiKeyAsync() => Task.FromResult<string?>(null);
    }
}
