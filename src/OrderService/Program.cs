using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using OrderService.Data;
using OrderService.Endpoints;
using OrderService.Handlers;
using Shared.Infrastructure;

var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

try
{
    logger.Info("Iniciando OrderService");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseNLog();

    var configuration = builder.Configuration;
    var services = builder.Services;

    // Database
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    services.AddDbContext<OrderDbContext>(options =>
        options.UseSqlServer(connectionString));

    // RabbitMQ
    var rabbitMqSettings = configuration.GetSection("RabbitMQ").Get<RabbitMQSettings>()!;
    services.AddSingleton(rabbitMqSettings);
    services.AddSingleton<MessagePublisher>();

    // Event Handlers
    services.AddTransient<OrderEventHandler>();
    services.AddHostedService<OrderEventConsumer>();

    // Controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Apply migrations
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Database.Migrate();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    // Map Order Endpoints
    app.MapOrderEndpoints();

    logger.Info("Order Service iniciado na porta 5000");
    app.Run("http://0.0.0.0:5000");
}
catch (Exception ex)
{
    logger.Error(ex, "Aplicação encerrada inesperadamente");
    throw;
}
finally
{
    LogManager.Shutdown();
}
