using Microsoft.Extensions.Caching.Memory;
using Services.Implementations;
using Services.Interfaces;

namespace Services.Tests.Admin;

public sealed class MemoryReferenceDataCacheTests
{
    private readonly MemoryReferenceDataCache _cache = new(new MemoryCache(new MemoryCacheOptions()));
    private int _loads;

    private Task<string> Get(ReferenceData kind, int organizationId) =>
        _cache.GetOrCreateAsync(kind, organizationId, _ =>
        {
            _loads++;
            return Task.FromResult($"{kind}:{organizationId}:{_loads}");
        }, CancellationToken.None);

    [Fact]
    public async Task The_second_read_comes_from_the_cache()
    {
        var first = await Get(ReferenceData.Categories, 1);
        var second = await Get(ReferenceData.Categories, 1);

        Assert.Equal(first, second);
        Assert.Equal(1, _loads);
    }

    [Fact]
    public async Task Invalidate_removes_only_that_organization_and_kind()
    {
        await Get(ReferenceData.Categories, 1);
        await Get(ReferenceData.Categories, 2);
        await Get(ReferenceData.Departments, 1);

        _cache.Invalidate(ReferenceData.Categories, 1);
        await Get(ReferenceData.Categories, 1);
        await Get(ReferenceData.Categories, 2);
        await Get(ReferenceData.Departments, 1);

        Assert.Equal(4, _loads); // only categories of organization 1 were loaded again
    }
}
