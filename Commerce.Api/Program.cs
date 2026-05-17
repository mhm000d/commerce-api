using Commerce.Api;
using Commerce.Api.Startup;
using Commerce.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddApiDocumentation();
builder.Services.AddConfiguredCors(builder.Configuration);
builder.Services.AddApiHealthChecks();
builder.Services.AddConfiguredRateLimiting(builder.Configuration, builder.Environment);

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddApplication(builder.Configuration, builder.Environment);
builder.Services.AddAuthServices(builder.Configuration);

var app = builder.Build();

app.UseApiDocumentation();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseApiSecurity();
app.UseRequestBodyBuffering();
app.UseHangfireDashboardAndJobs();

app.MapControllers();
app.MapApiHealthChecks();

await app.MigrateAndSeedDatabaseAsync();

app.Run();
