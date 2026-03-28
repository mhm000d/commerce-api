using Commerce.Application.Models;
using Commerce.Application.Validators;
using Shouldly;

namespace Commerce.Tests.UnitTests.Validators;

public class RatingValidatorTests
{
    private readonly RatingValidator _validator = new();

    // ── Score ─────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Score_WhenOutOfRange_ShouldFail(int invalidScore)
    {
        var rating = Rating.Create(Guid.NewGuid(), Guid.NewGuid(), invalidScore);
        var result = _validator.Validate(rating);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Rating.Score));
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Score_WhenInValidRange_ShouldNotFailOnScore(int validScore)
    {
        var rating = Rating.Create(Guid.NewGuid(), Guid.NewGuid(), validScore);
        var result = _validator.Validate(rating);

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(Rating.Score));
    }
    
    // ── Comment ───────────────────────────────────────────────────────────────
    [Fact]
    public void Comment_WhenNull_ShouldNotFailOnComment()
    {
        var rating = Rating.Create(Guid.NewGuid(), Guid.NewGuid(), 5, comment: null);
        var result = _validator.Validate(rating);

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(Rating.Comment));
    }
    
    [Fact]
    public void Comment_WhenAtMaxLength_ShouldNotFailOnComment()
    {
        var rating = Rating.Create(Guid.NewGuid(), Guid.NewGuid(), 5, new string('x', 200));
        var result = _validator.Validate(rating);

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(Rating.Comment));
    }
    
    [Fact]
    public void Comment_WhenOverMaxLength_ShouldFail()
    {
        var rating = Rating.Create(Guid.NewGuid(), Guid.NewGuid(), 5, new string('x', 201));
        var result = _validator.Validate(rating);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Rating.Comment));
    }
    
    // ── Full happy path ───────────────────────────────────────────────────────
    [Fact]
    public void Rating_WithAllValidFields_ShouldPassValidation()
    {
        var rating = Rating.Create(Guid.NewGuid(), Guid.NewGuid(), 4, "Solid product.");
        var result = _validator.Validate(rating);

        result.IsValid.ShouldBeTrue();
    }
}