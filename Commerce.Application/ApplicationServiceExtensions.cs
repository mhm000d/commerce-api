using Commerce.Application.Services.Carts;
using Commerce.Application.Services.Ratings;
using System.Text;
using Amazon.S3;
using Amazon.SimpleEmailV2;
using Commerce.Application.Database;
using Commerce.Application.Jobs;
using Commerce.Application.Services.Account;
using Commerce.Application.Services.Addresses;
using Commerce.Application.Services.Admin;
using Commerce.Application.Services.Auth;
using Commerce.Application.Services.Email;
using Commerce.Application.Services.Email.Templates;
using Commerce.Application.Services.Orders;
using Commerce.Application.Services.Payments;
using Commerce.Application.Services.ProductImages;
using Commerce.Application.Services.Products;
using Commerce.Application.Services.Storages;
using Commerce.Application.Settings;
using Commerce.Application.Validators;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using AccountService = Commerce.Application.Services.Account.AccountService;
using ProductService = Commerce.Application.Services.Products.ProductService;
using TokenService = Commerce.Application.Services.Auth.TokenService;

namespace Commerce.Application;

public static class ApplicationServiceExtensions
{
    extension(IServiceCollection services)
    {
        public void AddApplication(IConfiguration configuration, IWebHostEnvironment environment)
        {
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IProductImageService, ProductImageService>();
            services.AddScoped<IRatingService, RatingService>();
            services.AddScoped<IAddressService, AddressService>();
            services.AddScoped<ICartService, CartService>();
            
            StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"]
                                         ?? throw new InvalidOperationException("Stripe:SecretKey is not configured.");

            services.AddScoped<IStripeService, StripeService>();
            services.AddScoped<IOrderService, OrderService>();
            
            // Email
            if (environment.IsDevelopment())
                services.AddScoped<IEmailService, SmtpEmailService>();
            else
                services.AddScoped<IEmailService, SesEmailService>();
            // services.AddScoped<IEmailService, SesEmailService>();
            services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
            services.AddScoped<IEmailNotificationService, EmailNotificationService>();
            services.AddScoped<EmailTemplateRenderer>();
            
            // Background jobs
            services.AddScoped<EmailSenderJob>();
            services.AddScoped<PaymentTimeoutJob>();
            services.AddScoped<CleanupJob>();
            
            // Hangfire
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options =>
                    options.UseNpgsqlConnection(
                        configuration.GetConnectionString("DefaultConnection"))));

            if (!environment.IsEnvironment("Testing"))
                services.AddHangfireServer();
            
            // Disable buffering so WebhookController can read the raw body:
            services.Configure<KestrelServerOptions>(o => o.AllowSynchronousIO = true);
            // OR (preferred — doesn't affect all of Kestrel):
            // Add [DisableRequestSizeLimit] and [RequestSizeLimit] per-action if needed.

            services.AddValidatorsFromAssemblyContaining<ProductValidator>();
            // AWS S3
            services.AddDefaultAWSOptions(configuration.GetAWSOptions());
            services.AddAWSService<IAmazonS3>();
            services.AddScoped<IStorageService, StorageService>();
            services.AddAWSService<IAmazonSimpleEmailServiceV2>();
        }

        public void AddDatabase(IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"))
            );
        }

        public void AddAuthServices(IConfiguration configuration)
        {
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, AuthService>();

            services
                .AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(o =>
                {
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = configuration["Jwt:Audience"],
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });
        }
    }
}
