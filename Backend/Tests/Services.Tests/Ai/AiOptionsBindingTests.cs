using Common.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Services.Extensions;

namespace Services.Tests.Ai;

public class AiOptionsBindingTests
{
    // Goes through the real registration (DependencyInjection.AddAi), including the empty-value handling.
    private static AiOptions Bind(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAi(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<AiOptions>>().Value;
    }

    [Fact]
    public void Temperature_NotConfigured_DefaultsTo02()
    {
        Assert.Equal(0.2f, Bind([]).Temperature);
    }

    [Fact]
    public void Temperature_Empty_IsNull()
    {
        Assert.Null(Bind(new() { ["Ai:Temperature"] = "" }).Temperature);
    }

    [Fact]
    public void Temperature_JsonNull_IsNull()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream("""{"Ai":{"Temperature":null}}"""u8.ToArray()))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAi(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetRequiredService<IOptions<AiOptions>>().Value.Temperature);
    }

    [Fact]
    public void Temperature_Value_IsBound()
    {
        Assert.Equal(0.7f, Bind(new() { ["Ai:Temperature"] = "0.7" }).Temperature);
    }
}
