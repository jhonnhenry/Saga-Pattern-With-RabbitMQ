using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using DeliveryService.Data;
using DeliveryService.Handlers;
using Shared.Infrastructure;

var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

try
{
    logger.Info("Iniciando DeliveryService");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseNLog();

    var configuration = builder.Configuration;
    var services = builder.Services;

    // Database
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    services.AddDbContext<DeliveryDbContext>(options =>
        options.UseSqlServer(connectionString));

    // RabbitMQ
    var rabbitMqSettings = configuration.GetSection("RabbitMQ").Get<RabbitMQSettings>()!;
    services.AddSingleton(rabbitMqSettings);
    services.AddSingleton<MessagePublisher>();

    // Handlers
    services.AddTransient<DeliveryCommandHandler>();
    services.AddHostedService<DeliveryCommandConsumer>();

    // Controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Apply migrations
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
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

    logger.Info("Delivery Service iniciado na porta 5003");
    app.Run("http://0.0.0.0:5003");
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
