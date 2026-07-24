using Commerce.Application.Models;
using Shouldly;

namespace Commerce.Tests.UnitTests.Models;

public class ProductModelTests
{
    [Fact]
    public void NormalizeSlug_ShouldNormalizeExpectedCharacters()
    {
        var result = Product.NormalizeSlug("  Samsung Galaxy S24 Ultra!!!  ");

        result.ShouldBe("samsung-galaxy-s24-ultra");
    }

    [Fact]
    public void NormalizeSlug_WithLongInput_ShouldNotThrowAndShouldReturnNormalizedSlug()
    {
        var veryLongName = string.Join('-', Enumerable.Repeat("a", 300_000));

        var action = () => Product.NormalizeSlug(veryLongName);

        action.ShouldNotThrow();
        action().ShouldBe(veryLongName);
    }
}