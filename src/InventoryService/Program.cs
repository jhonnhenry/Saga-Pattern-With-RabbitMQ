using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using InventoryService.Data;
using InventoryService.Handlers;
using Shared.Infrastructure;

var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

try
{
    logger.Info("Iniciando InventoryService");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseNLog();

    var configuration = builder.Configuration;
    var services = builder.Services;

    // Database
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    services.AddDbContext<InventoryDbContext>(options =>
        options.UseSqlServer(connectionString));

    // RabbitMQ
    var rabbitMqSettings = configuration.GetSection("RabbitMQ").Get<RabbitMQSettings>()!;
    services.AddSingleton(rabbitMqSettings);
    services.AddSingleton<MessagePublisher>();

    // Handlers
    services.AddTransient<InventoryCommandHandler>();
    services.AddHostedService<InventoryCommandConsumer>();

    // Controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Apply migrations
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
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

    logger.Info("Inventory Service iniciado na porta 5002");
    app.Run("http://0.0.0.0:5002");
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
