# 🛠️ Guia de Implementação dos Testes

**Versão**: 1.0
**Status**: Pronto para Implementação

---

## 📋 Índice

1. [Setup Inicial](#setup-inicial)
2. [Estrutura de Arquivos](#estrutura-de-arquivos)
3. [Conventions e Best Practices](#conventions-e-best-practices)
4. [Template de Testes Unitários](#template-de-testes-unitários)
5. [Template de Testes de Integração](#template-de-testes-de-integração)
6. [Fixtures e Helpers](#fixtures-e-helpers)
7. [CI/CD Integration](#cicd-integration)

---

## 🚀 Setup Inicial

### 1. Criar Projetos de Teste

```bash
cd tests/

# Criar projeto de testes unitários
dotnet new xunit -n UnitTests
cd UnitTests
dotnet add package Moq --version 4.20.70
dotnet add package FluentAssertions --version 6.12.0
dotnet add package AutoFixture --version 4.18.1
dotnet add package AutoFixture.Xunit2 --version 4.18.1
cd ..

# Criar projeto de testes de integração
dotnet new xunit -n IntegrationTests
cd IntegrationTests
dotnet add package Moq --version 4.20.70
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Testcontainers --version 3.7.0
dotnet add package Testcontainers.RabbitMQ --version 3.7.0
dotnet add package Testcontainers.MsSql --version 3.7.0
dotnet add package AutoFixture --version 4.18.1
cd ..

# Adicionar referências aos projetos de teste
cd UnitTests
dotnet add reference ../../src/Shared
dotnet add reference ../../src/OrderService
dotnet add reference ../../src/PaymentService
dotnet add reference ../../src/InventoryService
dotnet add reference ../../src/DeliveryService
dotnet add reference ../../src/SagaOrchestrator
cd ..

cd IntegrationTests
dotnet add reference ../../src/Shared
dotnet add reference ../../src/OrderService
dotnet add reference ../../src/PaymentService
dotnet add reference ../../src/InventoryService
dotnet add reference ../../src/DeliveryService
dotnet add reference ../../src/SagaOrchestrator
cd ..
```

### 2. Arquivo .csproj Base para UnitTests

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
    <CollectCoverage>true</CollectCoverage>
    <CoverageThreshold>80</CoverageThreshold>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.2" />
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
    <PackageReference Include="Moq" Version="4.20.70" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="AutoFixture" Version="4.18.1" />
    <PackageReference Include="AutoFixture.Xunit2" Version="4.18.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Shared\Shared.csproj" />
    <ProjectReference Include="..\..\src\OrderService\OrderService.csproj" />
    <ProjectReference Include="..\..\src\PaymentService\PaymentService.csproj" />
    <ProjectReference Include="..\..\src\InventoryService\InventoryService.csproj" />
    <ProjectReference Include="..\..\src\DeliveryService\DeliveryService.csproj" />
    <ProjectReference Include="..\..\src\SagaOrchestrator\SagaOrchestrator.csproj" />
  </ItemGroup>

</Project>
```

---

## 📁 Estrutura de Arquivos Detalhada

### UnitTests/

```
UnitTests/
├── Global.cs                          # Imports globais
├── UnitTests.csproj
│
├── Domain/
│   └── Models/
│       ├── OrderTests.cs              # 5 testes
│       ├── OrderItemTests.cs          # 3 testes
│       ├── PaymentTests.cs            # 4 testes
│       ├── InventoryTests.cs          # 3 testes
│       └── DeliveryTests.cs           # 2 testes
│
├── Sagas/
│   ├── OrderSagaStateTests.cs         # 5 testes (state transitions)
│   └── OrderSagaCommandTests.cs       # 5 testes (command creation)
│
├── Services/
│   └── PaymentServiceTests.cs         # 2 testes
│
├── Endpoints/
│   └── OrderEndpointsTests.cs         # 9 testes
│
├── Infrastructure/
│   ├── MessagePublisherTests.cs       # 3 testes
│   └── CorrelationIdTests.cs          # 2 testes
│
└── Fixtures/
    ├── OrderFixture.cs
    ├── PaymentFixture.cs
    ├── InventoryFixture.cs
    └── DeliveryFixture.cs

Total: ~43 testes unitários
```

### IntegrationTests/

```
IntegrationTests/
├── Global.cs
├── IntegrationTests.csproj
│
├── Sagas/
│   ├── SuccessFlowTests.cs
│   │   ├── CreateOrder_ToCompletion_ShouldSucceed
│   │   ├── MultipleOrders_ShouldProcessIndependently
│   │   ├── ShouldPersistAllEvents
│   │   └── OrderStatusProgression_ShouldFollowSequence
│   │
│   ├── CompensationFlowTests.cs
│   │   ├── InventoryReservationFails_ShouldCompensatePayment
│   │   ├── PaymentFails_ShouldCancelOrder
│   │   ├── DeliveryFails_ShouldCompensateAllSteps
│   │   └── CompensationOrder_ShouldBeReverse
│   │
│   ├── IdempotencyTests.cs
│   │   ├── DuplicateOrderCreated_ShouldNotCreateDuplicate
│   │   ├── DuplicatePaymentCommand_ShouldChargeOnlyOnce
│   │   └── DuplicateInventoryReservation_ShouldReserveOnlyOnce
│   │
│   ├── CorrelationTests.cs
│   │   ├── ShouldMaintainCorrelationIdThroughout
│   │   └── MultipleConcurrentOrders_ShouldNotMixCorrelationIds
│   │
│   └── ResilienceTests.cs
│       ├── RabbitMQRestart_ShouldResume
│       ├── DatabaseTemporaryFailure_ShouldRetryAndSucceed
│       ├── DeadLetterQueue_ShouldHandleUnprocessable
│       ├── ExponentialBackoffRetry_ShouldWait
│       └── MaxRetriesExceeded_GoesToDLQ
│
├── RabbitMQ/
│   ├── ExchangeConfigurationTests.cs
│   │   ├── ShouldHaveRequiredExchanges
│   │   └── BindingsToQueuesCorrect
│   │
│   └── MessageDurabilityTests.cs
│       └── ShouldNotLoseMessages
│
├── Database/
│   ├── EventSourcingTests.cs
│   │   ├── AllEventsSavedToDatabase
│   │   └── CanReplayAndReconstruct
│   │
│   └── SagaStateTests.cs
│       └── ShouldTrackAllStateTransitions
│
├── API/
│   ├── CreateOrderApiTests.cs
│   │   ├── InputValidation_EmptyItems
│   │   ├── InputValidation_NegativeQuantity
│   │   ├── InputValidation_ZeroPrice
│   │   └── SizeValidation_LongAddress
│   │
│   └── GetOrderApiTests.cs
│       ├── PathValidation_InvalidId
│       └── ShouldReturnCompleteOrderDetails
│
├── Performance/
│   ├── ConcurrentOrderProcessingTests.cs
│   │   └── 1000Orders_ConcurrentlyProcessed
│   │
│   └── MemoryLeakTests.cs
│       └── ShouldNotLeakOverTime
│
└── Fixtures/
    ├── IntegrationTestBase.cs         # Base class com containers
    ├── RabbitMQFixture.cs             # RabbitMQ container
    ├── SqlServerFixture.cs            # SQL Server container
    ├── TestDataBuilder.cs             # Criação de dados de teste
    └── MockConsumerHelper.cs          # Helper para capture de eventos

Total: ~31 testes de integração
```

---

## ✅ Conventions e Best Practices

### 1. Nomenclatura de Testes

```csharp
// ❌ RUIM
public class OrderTests
{
    [Fact]
    public void Test1() { }

    [Fact]
    public void OrderTest() { }

    [Fact]
    public void CreateTest() { }
}

// ✅ BOM
public class OrderTests
{
    [Fact]
    public void Create_WithValidData_ShouldBeValid()
    {
        // Arrange
        // Act
        // Assert
    }

    [Theory]
    [InlineData(1, "valid")]
    [InlineData(-1, "invalid")]
    public void Create_WithVariousCustomerIds_ShouldValidate(int customerId, string expected)
    {
        // Arrange
        // Act
        // Assert
    }
}
```

**Padrão**: `[MethodName]_[Scenario]_[ExpectedResult]`

### 2. Estrutura AAA (Arrange-Act-Assert)

```csharp
[Fact]
public void CreateOrder_WithValidRequest_ShouldReturn201()
{
    // ARRANGE - Preparar dados e mocks
    var request = new CreateOrderRequest
    {
        CustomerId = 1,
        ShippingAddress = "Rua A, 123",
        Items = new List<OrderItemDto>
        {
            new OrderItemDto { ProductId = 1, Quantity = 2, Price = 50 }
        }
    };

    var dbMock = new Mock<OrderDbContext>();
    var publisherMock = new Mock<MessagePublisher>();

    // ACT - Executar ação
    var result = await OrderEndpoints.CreateOrder(request, dbMock.Object, publisherMock.Object, _loggerFactory);

    // ASSERT - Validar resultado
    result.Should().BeOfType<CreatedResult>();
    ((CreatedResult)result).StatusCode.Should().Be(201);
    publisherMock.Verify(x => x.PublishEvent(It.IsAny<OrderCreated>(), It.IsAny<string>()), Times.Once);
}
```

### 3. Usar Fixtures para Dados Repetidos

```csharp
public class OrderTests : IClassFixture<OrderFixture>
{
    private readonly OrderFixture _fixture;

    public OrderTests(OrderFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Create_WithValidOrder_ShouldBeValid()
    {
        // Usar fixture ao invés de criar manualmente
        var order = _fixture.CreateValidOrder(customerId: 5);

        order.CustomerId.Should().Be(5);
        order.Status.Should().Be(OrderStatus.PENDING);
    }
}
```

### 4. AutoFixture para Dados Aleatórios

```csharp
[Theory, AutoData]  // Gera automaticamente tipos primitivos
public void Create_WithValidOrder_ShouldBeValid(
    long customerId,
    string shippingAddress)
{
    var order = new Order
    {
        CustomerId = customerId,
        ShippingAddress = shippingAddress
    };

    order.CustomerId.Should().Be(customerId);
    order.ShippingAddress.Should().Be(shippingAddress);
}

[Theory]
[InlineAutoData(1)]       // Customizado
[InlineAutoData(999)]
public void Create_WithVariousCustomerIds_ShouldBeValid(
    int customerId,
    string shippingAddress)  // Gerado automaticamente
{
    // Test
}
```

### 5. Assertions com FluentAssertions

```csharp
// ❌ RUIM
Assert.Equal(order.Status, OrderStatus.COMPLETED);
Assert.NotNull(order.Id);
Assert.True(order.TotalAmount > 0);

// ✅ BOM
order.Status.Should().Be(OrderStatus.COMPLETED);
order.Id.Should().BeGreaterThan(0);
order.TotalAmount.Should().BeGreaterThan(0);
order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
```

### 6. Validar Múltiplas Condições

```csharp
[Fact]
public void CreateOrder_WithValidRequest_ShouldInitializeProperly()
{
    // Arrange
    var request = new CreateOrderRequest { /* ... */ };

    // Act
    var order = new Order { /* ... */ };

    // Assert - Usar scope para agrupar assertions relacionadas
    using (new AssertionScope())
    {
        order.Id.Should().BeGreaterThan(0);
        order.CustomerId.Should().Be(request.CustomerId);
        order.Status.Should().Be(OrderStatus.PENDING);
        order.ShippingAddress.Should().Be(request.ShippingAddress);
        order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        order.Items.Should().HaveCount(request.Items.Count);
        order.TotalAmount.Should().Be(request.Items.Sum(x => x.Price * x.Quantity));
    }
}
```

### 7. Testar Exceções

```csharp
[Fact]
public void ReserveInventory_WithInsufficientStock_ShouldThrow()
{
    // Arrange
    var inventory = new Inventory { AvailableQuantity = 5 };

    // Act & Assert
    var action = () => inventory.Reserve(10);

    action.Should()
        .Throw<InsufficientStockException>()
        .WithMessage("*insufficient*");
}
```

### 8. Testes Parametrizados

```csharp
[Theory]
[InlineData(0, false)]          // Quantity 0 inválido
[InlineData(1, true)]           // Quantity 1 válido
[InlineData(100, true)]         // Quantity 100 válido
[InlineData(-1, false)]         // Quantity negativa inválido
public void OrderItem_WithVariousQuantities_ShouldValidate(
    int quantity,
    bool shouldBeValid)
{
    var item = new OrderItem { ProductId = 1, Quantity = quantity, Price = 50 };

    if (shouldBeValid)
        item.IsValid().Should().BeTrue();
    else
        item.IsValid().Should().BeFalse();
}
```

### 9. Mockar Dependências Corretamente

```csharp
[Fact]
public void CreateOrder_ShouldPublishEvent()
{
    // Arrange
    var publisherMock = new Mock<MessagePublisher>();

    // Act
    var result = await OrderEndpoints.CreateOrder(request, db, publisherMock.Object, logger);

    // Assert - Verificar chamadas
    publisherMock.Verify(
        x => x.PublishEvent(
            It.Is<OrderCreated>(e => e.OrderId == 1),
            It.Is<string>(rk => rk == EventRoutingKeys.OrderCreated)),
        Times.Once);
}
```

### 10. Limpar Recursos

```csharp
public class IntegrationTests : IAsyncLifetime
{
    private readonly IContainer _container;

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder()
            .WithRabbitMQ()
            .WithSqlServer()
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task SomeTest()
    {
        // _container disponível aqui
    }
}
```

---

## 🎯 Template de Testes Unitários

### Template Completo

```csharp
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Domain.Models;

public class OrderTests
{
    #region Fixtures

    private Order CreateValidOrder(
        long customerId = 1,
        string shippingAddress = "Rua A, 123",
        decimal totalAmount = 100.00m)
    {
        return new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.PENDING,
            ShippingAddress = shippingAddress,
            TotalAmount = totalAmount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region Tests - Valid Cases

    [Fact]
    public void Create_WithValidData_ShouldBeValid()
    {
        // Arrange
        var customerId = 1;
        var shippingAddress = "Rua A, 123";
        var totalAmount = 100.00m;

        // Act
        var order = CreateValidOrder(customerId, shippingAddress, totalAmount);

        // Assert
        using (new AssertionScope())
        {
            order.CustomerId.Should().Be(customerId);
            order.ShippingAddress.Should().Be(shippingAddress);
            order.TotalAmount.Should().Be(totalAmount);
            order.Status.Should().Be(OrderStatus.PENDING);
            order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    [InlineData(long.MaxValue)]
    public void Create_WithVariousCustomerIds_ShouldBeValid(long customerId)
    {
        // Act
        var order = CreateValidOrder(customerId: customerId);

        // Assert
        order.CustomerId.Should().Be(customerId);
    }

    #endregion

    #region Tests - Invalid Cases

    [Fact]
    public void Create_WithZeroCustomerId_ShouldFail()
    {
        // Act
        var action = () => CreateValidOrder(customerId: 0);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyShippingAddress_ShouldFail()
    {
        // Act
        var action = () => CreateValidOrder(shippingAddress: "");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Tests - State Transitions

    [Fact]
    public void ChangeStatus_FromPendingToCompleted_ShouldWork()
    {
        // Arrange
        var order = CreateValidOrder();

        // Act
        order.Status = OrderStatus.COMPLETED;
        var originalUpdatedAt = order.UpdatedAt;
        System.Threading.Thread.Sleep(10);
        order.UpdatedAt = DateTime.UtcNow;

        // Assert
        order.Status.Should().Be(OrderStatus.COMPLETED);
        order.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion
}
```

---

## 🔗 Template de Testes de Integração

### Template Completo

```csharp
using FluentAssertions;
using Testcontainers.RabbitMq;
using Testcontainers.MsSql;
using Xunit;

namespace IntegrationTests.Sagas;

public class SuccessFlowTests : IAsyncLifetime
{
    #region Containers

    private RabbitMqContainer _rabbitMq;
    private MsSqlContainer _sqlServer;
    private IServiceProvider _serviceProvider;

    #endregion

    #region Lifecycle

    public async Task InitializeAsync()
    {
        // Start RabbitMQ
        _rabbitMq = new RabbitMqBuilder()
            .WithCleanup(true)
            .Build();
        await _rabbitMq.StartAsync();

        // Start SQL Server
        _sqlServer = new MsSqlBuilder()
            .WithPassword("TestPassword123!")
            .Build();
        await _sqlServer.StartAsync();

        // Configure Services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Run Migrations
        await RunMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        if (_rabbitMq != null)
            await _rabbitMq.StopAsync();

        if (_sqlServer != null)
            await _sqlServer.StopAsync();

        _serviceProvider?.Dispose();
    }

    #endregion

    #region Tests

    [Fact]
    public async Task SagaFlow_CreateOrder_ToCompletion_ShouldSucceed()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = 1,
            ShippingAddress = "Quadra 207 sul - Palmas-TO",
            Items = new List<OrderItemDto>
            {
                new OrderItemDto { ProductId = 1, Quantity = 2, Price = 99.99m }
            }
        };

        // Act
        var orderService = _serviceProvider.GetRequiredService<IOrderService>();
        var order = await orderService.CreateOrderAsync(request);

        // Assert Order Created
        order.Should().NotBeNull();
        order.Id.Should().BeGreaterThan(0);
        order.Status.Should().Be(OrderStatus.PENDING);
        order.TotalAmount.Should().Be(199.98m);

        // Wait for saga completion
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert Order Completed
        var completedOrder = await orderService.GetOrderAsync(order.Id);
        completedOrder.Status.Should().Be(OrderStatus.COMPLETED);

        // Assert Payment Completed
        var paymentService = _serviceProvider.GetRequiredService<IPaymentService>();
        var payment = await paymentService.GetPaymentByOrderIdAsync(order.Id);
        payment.Should().NotBeNull();
        payment.Status.Should().Be(PaymentStatus.COMPLETED);

        // Assert Inventory Reserved
        var inventoryService = _serviceProvider.GetRequiredService<IInventoryService>();
        var inventory = await inventoryService.GetInventoryAsync(1);
        inventory.ReservedQuantity.Should().Be(2);

        // Assert Delivery Scheduled
        var deliveryService = _serviceProvider.GetRequiredService<IDeliveryService>();
        var delivery = await deliveryService.GetDeliveryByOrderIdAsync(order.Id);
        delivery.Should().NotBeNull();
        delivery.Status.Should().Be(DeliveryStatus.SCHEDULED);
    }

    [Fact]
    public async Task SagaFlow_InventoryReservationFails_ShouldCompensatePayment()
    {
        // Arrange
        // Create product with insufficient stock
        var inventoryService = _serviceProvider.GetRequiredService<IInventoryService>();
        await inventoryService.CreateProductAsync(new Product
        {
            Id = 999,
            AvailableQuantity = 1  // Only 1 unit
        });

        var request = new CreateOrderRequest
        {
            CustomerId = 1,
            ShippingAddress = "Rua B, 456",
            Items = new List<OrderItemDto>
            {
                new OrderItemDto { ProductId = 999, Quantity = 5, Price = 50m }  // Request 5
            }
        };

        // Act
        var orderService = _serviceProvider.GetRequiredService<IOrderService>();
        var order = await orderService.CreateOrderAsync(request);

        // Wait for saga processing
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        var failedOrder = await orderService.GetOrderAsync(order.Id);
        failedOrder.Status.Should().Be(OrderStatus.FAILED);

        // Assert Payment was refunded
        var paymentService = _serviceProvider.GetRequiredService<IPaymentService>();
        var payment = await paymentService.GetPaymentByOrderIdAsync(order.Id);
        payment.Status.Should().Be(PaymentStatus.REFUNDED);

        // Assert Inventory not reserved
        var inventory = await inventoryService.GetInventoryAsync(999);
        inventory.AvailableQuantity.Should().Be(1);  // Not changed
    }

    #endregion

    #region Helpers

    private void ConfigureServices(IServiceCollection services)
    {
        var rabbitMqUri = _rabbitMq.GetConnectionString();
        var sqlServerConnection = _sqlServer.GetConnectionString();

        services.AddSingleton(new RabbitMQSettings { ConnectionString = rabbitMqUri });
        services.AddDbContext<OrderDbContext>(opt => opt.UseSqlServer(sqlServerConnection));
        services.AddDbContext<PaymentDbContext>(opt => opt.UseSqlServer(sqlServerConnection));
        // ... Add other services
    }

    private async Task RunMigrationsAsync()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var orderDbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            await orderDbContext.Database.MigrateAsync();

            var paymentDbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            await paymentDbContext.Database.MigrateAsync();

            // ... Apply other migrations
        }
    }

    #endregion
}
```

---

## 📦 Fixtures e Helpers

### 1. OrderFixture Completo

```csharp
using Xunit;

namespace UnitTests.Fixtures;

public class OrderFixture : IDisposable
{
    public Order CreateValidOrder(
        long customerId = 1,
        string shippingAddress = "Rua A, 123",
        decimal totalAmount = 100.00m)
    {
        return new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.PENDING,
            ShippingAddress = shippingAddress,
            TotalAmount = totalAmount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Order CreateOrderWithItems(int itemCount = 3)
    {
        var order = CreateValidOrder();

        for (int i = 0; i < itemCount; i++)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = i + 1,
                Quantity = i + 1,
                Price = (i + 1) * 50m
            });
        }

        order.TotalAmount = order.Items.Sum(x => x.Price * x.Quantity);
        return order;
    }

    public CreateOrderRequest CreateValidOrderRequest()
    {
        return new CreateOrderRequest
        {
            CustomerId = 1,
            ShippingAddress = "Rua A, 123",
            Items = new List<OrderItemDto>
            {
                new OrderItemDto { ProductId = 1, Quantity = 2, Price = 50 }
            }
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
```

### 2. IntegrationTestBase

```csharp
using Testcontainers.RabbitMq;
using Testcontainers.MsSql;
using Xunit;

namespace IntegrationTests.Fixtures;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected RabbitMqContainer RabbitMqContainer { get; private set; }
    protected MsSqlContainer SqlServerContainer { get; private set; }
    protected IServiceProvider ServiceProvider { get; private set; }

    public virtual async Task InitializeAsync()
    {
        RabbitMqContainer = new RabbitMqBuilder()
            .WithCleanup(true)
            .Build();
        await RabbitMqContainer.StartAsync();

        SqlServerContainer = new MsSqlBuilder()
            .WithPassword("TestPassword123!")
            .Build();
        await SqlServerContainer.StartAsync();

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        await RunMigrationsAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (RabbitMqContainer != null)
            await RabbitMqContainer.StopAsync();

        if (SqlServerContainer != null)
            await SqlServerContainer.StopAsync();

        ServiceProvider?.Dispose();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Override in derived classes
    }

    protected virtual async Task RunMigrationsAsync()
    {
        // Override in derived classes
    }
}
```

### 3. TestDataBuilder

```csharp
namespace IntegrationTests.Fixtures;

public class TestDataBuilder
{
    private readonly IServiceProvider _serviceProvider;

    public TestDataBuilder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Order> CreateOrderAsync(
        long customerId = 1,
        int itemCount = 1,
        OrderStatus status = OrderStatus.PENDING)
    {
        var orderService = _serviceProvider.GetRequiredService<IOrderService>();

        var request = new CreateOrderRequest
        {
            CustomerId = customerId,
            ShippingAddress = "Rua Teste",
            Items = Enumerable.Range(1, itemCount)
                .Select(i => new OrderItemDto
                {
                    ProductId = i,
                    Quantity = 1,
                    Price = 100m
                })
                .ToList()
        };

        return await orderService.CreateOrderAsync(request);
    }

    public async Task<Product> CreateProductWithStockAsync(
        long productId,
        int availableQuantity = 100)
    {
        var inventoryService = _serviceProvider.GetRequiredService<IInventoryService>();

        return await inventoryService.CreateProductAsync(new Product
        {
            Id = productId,
            Name = $"Product {productId}",
            AvailableQuantity = availableQuantity
        });
    }

    public async Task PopulateTestDataAsync()
    {
        // Create test products
        for (int i = 1; i <= 5; i++)
        {
            await CreateProductWithStockAsync(i, 100);
        }
    }
}
```

---

## 🔄 CI/CD Integration

### GitHub Actions Workflow

```yaml
# .github/workflows/tests.yml
name: Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      docker:
        image: docker:latest
        options: --privileged

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Unit Tests
      run: dotnet test tests/UnitTests --no-restore --verbosity normal --logger "trx;LogFileName=unit-tests.trx"

    - name: Integration Tests
      run: dotnet test tests/IntegrationTests --no-restore --verbosity normal --logger "trx;LogFileName=integration-tests.trx"

    - name: Code Coverage
      run: |
        dotnet test /p:CollectCoverage=true \
                    /p:CoverageFormat=cobertura \
                    /p:CoverageFilename=coverage.xml

    - name: Upload Coverage to Codecov
      uses: codecov/codecov-action@v3
      with:
        files: ./coverage.xml
        flags: unittests
        name: codecov-umbrella

    - name: Test Results
      uses: dorny/test-reporter@v1
      if: always()
      with:
        name: Test Results
        path: '**/unit-tests.trx'
        reporter: 'dotnet trx'

    - name: Comment PR with Results
      if: github.event_name == 'pull_request'
      uses: daun/action-publish-result@v1
      with:
        token: ${{ secrets.GITHUB_TOKEN }}
```

### Arquivo de Configuração Local

```bash
# run-all-tests.sh
#!/bin/bash

echo "🔍 Running Unit Tests..."
dotnet test tests/UnitTests --configuration Release --verbosity normal

if [ $? -ne 0 ]; then
    echo "❌ Unit tests failed"
    exit 1
fi

echo ""
echo "🔗 Running Integration Tests..."
dotnet test tests/IntegrationTests --configuration Release --verbosity normal

if [ $? -ne 0 ]; then
    echo "❌ Integration tests failed"
    exit 1
fi

echo ""
echo "📊 Calculating Code Coverage..."
dotnet test /p:CollectCoverage=true /p:CoverageFormat=cobertura

echo ""
echo "✅ All tests passed!"
```

---

## 📊 Checklist de Implementação

### Phase 1: Setup
- [ ] Criar projetos UnitTests.csproj e IntegrationTests.csproj
- [ ] Instalar todas as dependências
- [ ] Criar estrutura de pastas
- [ ] Criar Global.cs com imports globais

### Phase 2: Fixtures
- [ ] Criar OrderFixture
- [ ] Criar PaymentFixture
- [ ] Criar InventoryFixture
- [ ] Criar DeliveryFixture
- [ ] Criar IntegrationTestBase
- [ ] Criar TestDataBuilder

### Phase 3: Unit Tests - Models (15 testes)
- [ ] OrderTests.cs (5)
- [ ] OrderItemTests.cs (3)
- [ ] PaymentTests.cs (4)
- [ ] InventoryTests.cs (3)

### Phase 4: Unit Tests - Services (15 testes)
- [ ] OrderSagaStateTests.cs (5)
- [ ] OrderSagaCommandTests.cs (5)
- [ ] MessagePublisherTests.cs (3)
- [ ] PaymentServiceTests.cs (2)

### Phase 5: Unit Tests - API (9 testes)
- [ ] OrderEndpointsTests.cs (9)

### Phase 6: Integration - Success (4 testes)
- [ ] SuccessFlowTests.cs

### Phase 7: Integration - Failure (4 testes)
- [ ] CompensationFlowTests.cs

### Phase 8: Integration - Advanced (10 testes)
- [ ] IdempotencyTests.cs
- [ ] CorrelationTests.cs
- [ ] ResilienceTests.cs

### Phase 9: Integration - Infrastructure (5 testes)
- [ ] ExchangeConfigurationTests.cs
- [ ] EventSourcingTests.cs
- [ ] ApiValidationTests.cs

### Phase 10: Performance (3 testes)
- [ ] ConcurrentOrderProcessingTests.cs
- [ ] MemoryLeakTests.cs

### Phase 11: CI/CD
- [ ] Configurar GitHub Actions
- [ ] Criar run-all-tests.sh

---

## 🎓 Recursos Adicionais

### xUnit
- Docs: https://xunit.net/
- Getting Started: https://xunit.net/docs/getting-started/netcore

### Moq
- GitHub: https://github.com/moq/moq4
- Samples: https://github.com/moq/moq4/wiki/Quickstart

### FluentAssertions
- Docs: https://fluentassertions.com/
- API: https://fluentassertions.com/api/

### TestContainers
- Docs: https://testcontainers.com/
- RabbitMQ: https://testcontainers.com/modules/rabbitmq/
- MSSql: https://testcontainers.com/modules/mssql/

### AutoFixture
- Docs: https://autofixture.github.io/
- Examples: https://github.com/AutoFixture/AutoFixture/wiki

---

**Versão**: 1.0
**Última Atualização**: Janeiro 2026
**Status**: Pronto para Uso
