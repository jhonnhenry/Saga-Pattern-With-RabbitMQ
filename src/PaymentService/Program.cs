using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using PaymentService.Data;
using PaymentService.Handlers;
using Shared.Infrastructure;

var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

try
{
    logger.Info("Iniciando PaymentService");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseNLog();

    var configuration = builder.Configuration;
    var services = builder.Services;

    logger.Info("Configurando banco de dados");
    // Database
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    services.AddDbContext<PaymentDbContext>(options =>
        options.UseSqlServer(connectionString));

    logger.Info("Configurando RabbitMQ");
    // RabbitMQ
    var rabbitMqSettings = configuration.GetSection("RabbitMQ").Get<RabbitMQSettings>()!;
    services.AddSingleton(rabbitMqSettings);
    services.AddSingleton<MessagePublisher>();

    logger.Info("Registrando handlers e consumers");
    // Handlers
    services.AddTransient<PaymentCommandHandler>();
    services.AddHostedService<PaymentCommandConsumer>();

    // Controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    logger.Info("Construindo aplicação");
    var app = builder.Build();

    logger.Info("Aplicando migrações do banco de dados");
    // Apply migrations
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
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

    logger.Info("Payment Service iniciado na porta 5001");
    app.Run("http://0.0.0.0:5001");
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
