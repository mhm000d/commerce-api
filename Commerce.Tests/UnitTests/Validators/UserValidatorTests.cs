using Bogus;
using Commerce.Application.Models;
using Commerce.Application.Validators;
using Shouldly;

namespace Commerce.Tests.UnitTests.Validators;

public class UserValidatorTests
{
    private readonly UserValidator _validator = new();
    private readonly Faker _faker = new();

    [Fact]
    public void Email_WhenInvalidFormat_ShouldFail()
    {
        // Arrange
        var user = User.Create(_faker.Name.FullName(), "not-an-email", _faker.Internet.Password());

        // Act
        var result = _validator.Validate(user);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(User.Email));
    }

    [Fact]
    public void Phone_WhenNull_ShouldPass()
    {
        // Arrange
        var user = User.Create(_faker.Name.FullName(), _faker.Internet.Email(), _faker.Internet.Password(), phone: null);

        // Act & Assert
        _validator.Validate(user).IsValid.ShouldBeTrue();
    }
}