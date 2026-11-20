

using Ecom.Core.Interfaces;
using Ecom.Infrastructure.Services;

namespace Ecom.Infrastructure;

public static class InfrastructureRegisteration
{
    public static IServiceCollection InfrastructureConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext
        services.AddDbContext<AppDbContext>(options => {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"));
        });

        // Register Repositories
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Register UnitOfWork
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register Token
        services.AddScoped<IGenerateToken, GenerateToken>();

        // Register Order
        services.AddScoped<IOrderService, OrderService>();

        // Register Order
        services.AddScoped<IPaymentService, PaymentService>();

        // Register Basket
        services.AddScoped<IBasketService, BasketService>();

        // Register Checkout
        services.AddScoped<ICheckoutService, CheckoutService>();

        // Register ImageManagementService
        services.AddSingleton<IImageManagementService, ImageManagementService>();

        // Register EmailService
        services.AddSingleton<IEmailService, EmailService>();

        // Register IFileProvider to access wwwroot
        services.AddSingleton<IFileProvider>(
            new PhysicalFileProvider(
                Path.Combine(Directory.GetCurrentDirectory(),"wwwroot")));

        //Apply redis Connection
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = ConfigurationOptions.Parse(
                //6379 : port of redis 
                configuration.GetConnectionString("Redis")?? "localhost:6379", true);

            config.AbortOnConnectFail = false; // ✅ عشان ميقفش البرنامج لو فشل الاتصال


            return ConnectionMultiplexer.Connect(config);
        });

      
        services.AddIdentity<User, IdentityRole>(o =>
        {
            // Backend-authoritative password policy (aligned with frontend validation; stronger than previous config)
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = false;
            o.Password.RequireUppercase = false;
            o.Password.RequireNonAlphanumeric = true;
            o.Password.RequiredLength = 8;

            //  o.User.RequireUniqueEmail = true;

            //  o.SignIn.RequireConfirmedEmail = true;

            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            o.Lockout.MaxFailedAccessAttempts = 3;
            o.Lockout.AllowedForNewUsers = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders()
        .AddTokenProvider<DataProtectorTokenProvider<User>>(TokenOptions.DefaultProvider);

        var jwtSecret = configuration["Token:Secret"];
        var jwtIssuer = configuration["Token:Issuer"];

        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            throw new InvalidOperationException(
                "JWT configuration is missing. Configure 'Token:Secret' via .NET User Secrets (recommended for Development) or an environment variable (e.g. Token__Secret). Do not store secrets in appsettings.json or source control.");
        }

        var jwtSigningKeyBytes = Encoding.UTF8.GetBytes(jwtSecret);
        if (jwtSigningKeyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT configuration is invalid. 'Token:Secret' must be at least 32 bytes (256 bits) for HMAC-SHA256 signing.");
        }

        if (string.IsNullOrWhiteSpace(jwtIssuer))
        {
            throw new InvalidOperationException(
                "JWT configuration is missing. Configure 'Token:Issuer' in application configuration.");
        }

        var jwtSigningKey = new SymmetricSecurityKey(jwtSigningKeyBytes);

        services.AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(x =>
        {
            x.Cookie.Name = "token";
            x.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        })
        .AddJwtBearer(x =>
        {
            x.RequireHttpsMetadata = false;
            x.SaveToken = true;
            x.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey = jwtSigningKey,

                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
            x.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (string.IsNullOrEmpty(context.Token))
                    {
                        var token = context.Request.Cookies["token"];
                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            context.Token = token;
                        }
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                    if (string.IsNullOrWhiteSpace(jti))
                    {
                        return;
                    }

                    try
                    {
                        var redis = context.HttpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                        var db = redis.GetDatabase();
                        var revoked = await db.KeyExistsAsync($"auth:revoked:jti:{jti}");
                        if (revoked)
                        {
                            context.Fail("Token has been revoked.");
                        }
                    }
                    catch
                    {
                        // If revocation store is unavailable, do not block token validation to preserve availability.
                    }
                }
            };
        });




        return services;
    }
}
