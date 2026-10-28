using System.Text.Json.Serialization;
using dotnet.challenge.api.Background;
using dotnet.challenge.api.Services;
using dotnet.challenge.data.Cache;
using dotnet.challenge.data.Entities;
using dotnet.challenge.data.sqlserver;
using Microsoft.OpenApi.Models;

namespace dotnet.challenge.api;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            // Reservation.Status is part of the contract as a string ("Active", "Confirmed", ...).
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddProblemDetails();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory Reservations API", Version = "v1" });
        });

        AddObjectCaches(services);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IInventoryService, InventoryService>();

        services.Configure<ReservationExpiryOptions>(Configuration.GetSection(ReservationExpiryOptions.Section));
        services.AddHostedService<ReservationExpirySweeper>();
    }

    /// <summary>
    /// "Persistence:Provider" = "InMemory" (default; what the challenge requires) or "SqlServer"
    /// (optional EF Core database-first storage, connection string "ConnectionStrings:Unily").
    /// Both implement the same <see cref="ISimpleObjectCache{TKey,TValue}"/>, so the service is unaware of the store.
    /// </summary>
    private void AddObjectCaches(IServiceCollection services)
    {
        var provider = Configuration["Persistence:Provider"] ?? "InMemory";
        if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = Configuration.GetConnectionString("Unily");
            if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains('<'))
            {
                throw new InvalidOperationException(
                    "Persistence:Provider is SqlServer but ConnectionStrings:Unily is not set. " +
                    "Set it with: dotnet user-secrets set \"ConnectionStrings:Unily\" \"<connection string>\"");
            }
            services.AddSqlServerObjectCaches(connectionString);
            return;
        }

        services.AddSingleton<ISimpleObjectCache<Guid, Product>, SimpleObjectCache<Guid, Product>>();
        services.AddSingleton<ISimpleObjectCache<Guid, Reservation>, SimpleObjectCache<Guid, Reservation>>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler();
        }
        app.UseStatusCodePages();

        app.UseRouting();

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory Reservations API V1");
            c.RoutePrefix = "swagger";
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
