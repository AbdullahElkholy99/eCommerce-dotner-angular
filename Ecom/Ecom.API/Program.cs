using Ecom.API.Middleware;
using Ecom.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// ✅ Register Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.InfrastructureConfiguration(builder.Configuration);

// ✅ Register AutoMapper
builder.Services.AddAutoMapper(cfg => { }, typeof(Program));

// Register MemoryCache
builder.Services.AddMemoryCache();

// Antiforgery (CSRF) for cookie-authenticated SPA flows (guest + authenticated).
// Token is issued via GET /api/Antiforgery/token and validated for unsafe API requests where applicable.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false; // SPA/client reads token flow; request token sent via header
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.Path = "/";
    options.Cookie.Domain = "localhost";
    options.Cookie.IsEssential = true;
});

// Register CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CORSPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:52945")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddAuthentication();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable CORS
app.UseCors("CORSPolicy");

// Redirect HTTP to HTTPS (before issuing/validating secure cookies/tokens)
app.UseHttpsRedirection();

// Use custom exception handling middleware
app.UseMiddleware<ExceptionsMiddleware>();

// Handle status code pages
app.UseStatusCodePagesWithReExecute("/Errors/{0}");

// Enable authentication and authorization middleware
app.UseAuthentication();

// CSRF protection for unsafe cookie-authenticated API requests (defense-in-depth)
app.UseMiddleware<CsrfProtectionMiddleware>();

app.UseAuthorization();

// Serve static files
app.UseStaticFiles();

// Map controller routes
app.MapControllers();

// Initialize the database
app.Run();
