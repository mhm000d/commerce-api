namespace Commerce.Api.Startup;

public static class ApiOutputCaching
{
    public const string ProductsTag = "products";

    public const string ProductDetailsPolicy = "product-details";
    public const string ProductsListPolicy = "products-list";

    extension(IServiceCollection services)
    {
        public void AddApiOutputCaching()
        {
            services.AddOutputCache(options =>
            {
                options.AddPolicy(ProductDetailsPolicy, policy => policy
                    .Expire(TimeSpan.FromMinutes(2))
                    .Tag(ProductsTag)
                    .SetVaryByRouteValue("identifier"));

                options.AddPolicy(ProductsListPolicy, policy => policy
                    .Expire(TimeSpan.FromMinutes(1))
                    .Tag(ProductsTag)
                    .SetVaryByQuery("*"));
            });
        }
    }
}