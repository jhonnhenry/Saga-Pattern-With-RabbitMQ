# 📋 Plano Detalhado de Testes - RabbitMQ Saga Pattern

**Versão**: 1.0
**Data**: Janeiro 2026
**Status**: Em Planejamento para Implementação Futura

---

## 📑 Índice

1. [Visão Geral](#visão-geral)
2. [Estrutura de Testes](#estrutura-de-testes)
3. [Testes Unitários](#testes-unitários)
4. [Testes de Integração](#testes-de-integração)
5. [Fixtures e Test Data](#fixtures-e-test-data)
6. [Infraestrutura de Testes](#infraestrutura-de-testes)
7. [Cronograma de Implementação](#cronograma-de-implementação)

---

## 🎯 Visão Geral

### Objetivos
- Garantir 80%+ de cobertura de código
- Validar comportamento do Saga Pattern em cenários normais e de falha
- Assegurar idempotência e resiliência
- Testar integração entre microserviços via RabbitMQ
- Validar persistência e consistência de dados

### Escopo
- **Unitários**: Lógica de negócio, validações, comandos/eventos
- **Integração**: Fluxos completos, RabbitMQ, banco de dados, resiliência
- **Excluído**: Testes de UI (apenas API), testes de carga extrema (>1000 concurrent)

### Tecnologias
```
xUnit - Framework de testes
Moq - Mocking
FluentAssertions - Assertions fluentes
TestContainers - RabbitMQ/SQL Server em containers
AutoFixture - Geração de dados de teste
```

---

## 📁 Estrutura de Testes

### Organização de Pastas

```
tests/
├── UnitTests/
│   ├── Domain/
│   │   ├── Models/
│   │   │   ├── OrderTests.cs
│   │   │   ├── OrderItemTests.cs
│   │   │   ├── PaymentTests.cs
│   │   │   ├── InventoryTests.cs
│   │   │   └── DeliveryTests.cs
│   │   └── ValueObjects/
│   │
│   ├── Services/
│   │   ├── OrderSagaTests.cs
│   │   ├── PaymentServiceTests.cs
│   │   ├── InventoryServiceTests.cs
│   │   └── DeliveryServiceTests.cs
│   │
│   ├── Handlers/
│   │   ├── SagaOrchestrationHandlerTests.cs
│   │   ├── PaymentCommandHandlerTests.cs
│   │   ├── InventoryCommandHandlerTests.cs
│   │   └── DeliveryCommandHandlerTests.cs
│   │
│   ├── Infrastructure/
│   │   ├── MessagePublisherTests.cs
│   │   ├── MessageConsumerTests.cs
│   │   └── CorrelationIdGeneratorTests.cs
│   │
│   ├── Endpoints/
│   │   └── OrderEndpointsTests.cs
│   │
│   └── Fixtures/
│       ├── OrderFixture.cs
│       ├── PaymentFixture.cs
│       ├── InventoryFixture.cs
│       └── DeliveryFixture.cs
│
├── IntegrationTests/
│   ├── Sagas/
│   │   ├── SuccessFlowTests.cs
│   │   ├── CompensationFlowTests.cs
│   │   ├── IdempotencyTests.cs
│   │   ├── CorrelationTests.cs
│   │   └── ResilienceTests.cs
│   │
│   ├── RabbitMQ/
│   │   ├── ExchangeConfigurationTests.cs
│   │   ├── QueueConfigurationTests.cs
│   │   ├── MessageDurabilityTests.cs
│   │   └── DeadLetterQueueTests.cs
│   │
│   ├── Database/
│   │   ├── EventSourcingTests.cs
│   │   ├── SagaStateTests.cs
│   │   └── TransactionConsistencyTests.cs
│   │
│   ├── API/
│   │   ├── CreateOrderApiTests.cs
│   │   ├── GetOrderApiTests.cs
│   │   └── OrderValidationApiTests.cs
│   │
│   ├── Performance/
│   │   ├── ConcurrentOrderProcessingTests.cs
│   │   ├── MemoryLeakTests.cs
│   │   └── ThroughputTests.cs
│   │
│   └── Fixtures/
│       ├── RabbitMQFixture.cs
│       ├── SqlServerFixture.cs
│       ├── IntegrationTestBase.cs
│       └── TestDataBuilder.cs

└── README.md (instruções para rodar testes)
```

---

## 🧪 Testes Unitários

### 1. Domain Models - Order

#### 1.1 `OrderTests.cs`

**Test Case 1.1.1**: Order_Create_WithValidData_ShouldBeValid
```
Descrição: Validar criação de Order com dados válidos
Pré-requisitos: Nenhum
Arrange:
  - customerId = 1
  - status = OrderStatus.PENDING
  - shippingAddress = "Rua A, 123"
  - totalAmount = 100.00
Act:
  - Criar novo Order
Assert:
  - Order.Id > 0
  - Order.Status == PENDING
  - Order.CreatedAt != null
  - Order.UpdatedAt != null
Dados de Teste:
  - customerId: 1, 999, -1 (inválido)
  - totalAmount: 0.01, 1000.00, 0 (inválido)
  - shippingAddress: normal, vazia (inválido)
```

**Test Case 1.1.2**: Order_AddItem_WithValidItem_ShouldIncludeInCollection
```
Descrição: Validar adição de itens a um pedido
Pré-requisitos: Order deve estar criada
Arrange:
  - Criar Order
  - Criar OrderItem com productId=1, quantity=2, price=50
Act:
  - order.Items.Add(item)
Assert:
  - order.Items.Count == 1
  - order.Items[0].ProductId == 1
  - order.Items[0].Quantity == 2
Dados de Teste:
  - quantity: 1, 100, 0 (inválido), -1 (inválido)
  - price: 0.01, 999.99, 0 (inválido)
```

**Test Case 1.1.3**: Order_CalculateTotalAmount_WithMultipleItems_ShouldBeCorrect
```
Descrição: Validar cálculo automático do total
Pré-requisitos: Order com múltiplos itens
Arrange:
  - Criar Order
  - Adicionar item: qty=2, price=50 (subtotal=100)
  - Adicionar item: qty=1, price=30 (subtotal=30)
  - Adicionar item: qty=3, price=20 (subtotal=60)
Act:
  - Calcular totalAmount
Assert:
  - totalAmount == 190.00
  - Math.Abs(totalAmount - expectedTotal) < 0.01 (floating point safety)
Dados de Teste:
  - Múltiplas combinações com 1, 5, 10 itens
  - Valores fracionários (9.99, 19.99)
```

**Test Case 1.1.4**: Order_ChangeStatus_FromPendingToCompleted_ShouldWork
```
Descrição: Validar transição de estado
Pré-requisitos: Order no estado PENDING
Arrange:
  - Criar Order com status PENDING
Act:
  - order.Status = OrderStatus.COMPLETED
Assert:
  - order.Status == COMPLETED
  - order.UpdatedAt foi atualizado
Dados de Teste:
  - Estados: PENDING → PROCESSING_PAYMENT → COMPLETED
  - Estados: PENDING → FAILED
```

**Test Case 1.1.5**: Order_InvalidTransition_ShouldThrowException
```
Descrição: Validar que transições inválidas geram exceção
Pré-requisitos: Order com status COMPLETED
Arrange:
  - Criar Order com status COMPLETED
Act:
  - Tentar mudar para PENDING (transição inválida)
Assert:
  - Deve lançar InvalidOperationException
  - Mensagem deve indicar transição inválida
Dados de Teste:
  - COMPLETED → PENDING (inválida)
  - FAILED → PROCESSING_PAYMENT (inválida)
```

---

### 2. Domain Models - Payment

#### 2.1 `PaymentTests.cs`

**Test Case 2.1.1**: Payment_Create_WithValidData_ShouldBeValid
```
Descrição: Criar Payment válido
Pré-requisitos: Nenhum
Arrange:
  - orderId = 1
  - amount = 150.50
  - paymentMethod = "CreditCard"
  - status = PaymentStatus.PENDING
Act:
  - Criar novo Payment
Assert:
  - Payment.Id > 0
  - Payment.Status == PENDING
  - Payment.CreatedAt não é null
Dados de Teste:
  - amount: 0.01, 99999.99
  - paymentMethod: "CreditCard", "Debit", "PayPal"
  - status: PENDING, COMPLETED, FAILED
```

**Test Case 2.1.2**: Payment_ProcessPayment_ShouldChangeStatusToCompleted
```
Descrição: Validar mudança de status ao processar pagamento
Pré-requisitos: Payment com status PENDING
Arrange:
  - Criar Payment com status PENDING
  - transactionId = "TXN-12345"
Act:
  - payment.Complete(transactionId)
Assert:
  - payment.Status == COMPLETED
  - payment.TransactionId == "TXN-12345"
  - payment.CompletedAt != null
```

**Test Case 2.1.3**: Payment_RefundPayment_ShouldChangeStatusToRefunded
```
Descrição: Validar refund
Pré-requisitos: Payment com status COMPLETED
Arrange:
  - Criar Payment COMPLETED
Act:
  - payment.Refund("Saga compensation")
Assert:
  - payment.Status == REFUNDED
  - payment.RefundReason == "Saga compensation"
  - payment.RefundedAt != null
```

**Test Case 2.1.4**: Payment_FailPayment_ShouldChangeStatusToFailed
```
Descrição: Validar mudança para FAILED
Pré-requisitos: Payment com status PENDING
Arrange:
  - Criar Payment PENDING
Act:
  - payment.Fail("Insufficient funds")
Assert:
  - payment.Status == FAILED
  - payment.FailureReason == "Insufficient funds"
```

---

### 3. Domain Models - Inventory

#### 3.1 `InventoryTests.cs`

**Test Case 3.1.1**: Inventory_ReserveItems_WithSufficientStock_ShouldReduce
```
Descrição: Validar reserva quando há estoque suficiente
Pré-requisitos: Inventory com 100 unidades
Arrange:
  - Criar Inventory: productId=1, quantity=100
Act:
  - inventory.Reserve(50)
Assert:
  - inventory.AvailableQuantity == 50
  - inventory.ReservedQuantity == 50
  - inventory.Status == RESERVED
Dados de Teste:
  - quantity: 1, 50, 99, 100 (total)
```

**Test Case 3.1.2**: Inventory_ReserveItems_WithInsufficientStock_ShouldFail
```
Descrição: Validar falha quando estoque insuficiente
Pré-requisitos: Inventory com 30 unidades
Arrange:
  - Criar Inventory: quantity=30
Act:
  - Tentar inventory.Reserve(50)
Assert:
  - Deve lançar InsufficientStockException
  - inventory.AvailableQuantity == 30 (sem mudança)
Dados de Teste:
  - quantity: 31, 50, 100
```

**Test Case 3.1.3**: Inventory_ReleaseReservedItems_ShouldRestoreStock
```
Descrição: Validar liberação de itens reservados
Pré-requisitos: Inventory com 50 reservados
Arrange:
  - Criar Inventory: reserved=50, available=50
Act:
  - inventory.Release(50)
Assert:
  - inventory.AvailableQuantity == 100
  - inventory.ReservedQuantity == 0
  - inventory.Status == AVAILABLE
```

---

### 4. OrderSaga - State Transitions

#### 4.1 `OrderSagaTests.cs`

**Test Case 4.1.1**: OrderSaga_GetNextState_FromCreated_ShouldReturnAwaitingPayment
```
Descrição: Validar transição de estado CREATED → AWAITING_PAYMENT
Pré-requisitos: OrderSaga instanciada
Arrange:
  - saga = new OrderSaga(logger)
  - currentStatus = SagaStatus.CREATED
Act:
  - nextStatus = saga.GetNextState(currentStatus)
Assert:
  - nextStatus == SagaStatus.AWAITING_PAYMENT
```

**Test Case 4.1.2**: OrderSaga_GetNextState_FromAwaitingPayment_ShouldReturnAwaitingInventory
```
Descrição: Validar transição AWAITING_PAYMENT → AWAITING_INVENTORY
Pré-requisitos: OrderSaga instanciada
Arrange:
  - saga = new OrderSaga(logger)
  - currentStatus = SagaStatus.AWAITING_PAYMENT
Act:
  - nextStatus = saga.GetNextState(currentStatus)
Assert:
  - nextStatus == SagaStatus.AWAITING_INVENTORY
```

**Test Case 4.1.3**: OrderSaga_GetNextState_AllTransitions_ShouldFollowSequence
```
Descrição: Validar sequência completa de transições
Pré-requisitos: OrderSaga instanciada
Arrange:
  - saga = new OrderSaga(logger)
  - expectedSequence = [CREATED, AWAITING_PAYMENT, AWAITING_INVENTORY, AWAITING_DELIVERY, COMPLETED]
Act:
  - Para cada status, calcular próximo
Assert:
  - Sequência == expectedSequence
```

**Test Case 4.1.4**: OrderSaga_GetNextState_UnknownStatus_ShouldReturnFailed
```
Descrição: Validar que status desconhecido retorna FAILED
Pré-requisitos: OrderSaga instanciada
Arrange:
  - saga = new OrderSaga(logger)
  - unknownStatus = (SagaStatus)999
Act:
  - nextStatus = saga.GetNextState(unknownStatus)
Assert:
  - nextStatus == SagaStatus.FAILED
```

---

### 5. OrderSaga - Command Creation

#### 4.2 `OrderSagaCommandTests.cs`

**Test Case 4.2.1**: OrderSaga_CreatePaymentCommand_ShouldMapOrderDataCorrectly
```
Descrição: Validar criação de ProcessPaymentCommand
Pré-requisitos: OrderSaga, SagaState, OrderCreated event
Arrange:
  - saga = new OrderSaga(logger)
  - sagaState = new SagaState { Id=1, OrderId=100, Data="{...}" }
  - orderEvent = new OrderCreated
    {
      OrderId=100,
      TotalAmount=500.00,
      CorrelationId="corr-123"
    }
Act:
  - command = saga.CreatePaymentCommand(sagaState, orderEvent)
Assert:
  - command.OrderId == 100
  - command.Amount == 500.00
  - command.PaymentMethod == "CreditCard"
  - command.CorrelationId == "corr-123"
Dados de Teste:
  - amount: 0.01, 99999.99
  - paymentMethod: sempre "CreditCard"
  - correlationId: GUID válido
```

**Test Case 4.2.2**: OrderSaga_CreateInventoryCommand_ShouldIncludeAllItems
```
Descrição: Validar criação de ReserveInventoryCommand com todos os itens
Pré-requisitos: OrderSaga, SagaState, OrderCreated com múltiplos itens
Arrange:
  - saga = new OrderSaga(logger)
  - sagaState = new SagaState { OrderId=100 }
  - orderEvent com 3 items:
    - item1: productId=1, qty=2
    - item2: productId=2, qty=5
    - item3: productId=3, qty=1
Act:
  - command = saga.CreateInventoryCommand(sagaState, orderEvent)
Assert:
  - command.OrderId == 100
  - command.Items.Count == 3
  - command.Items[0].ProductId == 1
  - command.Items[0].Quantity == 2
  - command.Items[1].ProductId == 2
  - command.Items[1].Quantity == 5
  - command.CorrelationId preservado
Dados de Teste:
  - 1 item, 5 items, 10 items
```

**Test Case 4.2.3**: OrderSaga_CreateDeliveryCommand_ShouldHaveValidDeliveryDate
```
Descrição: Validar criação de ScheduleDeliveryCommand com data correta
Pré-requisitos: OrderSaga, SagaState, OrderCreated
Arrange:
  - saga = new OrderSaga(logger)
  - sagaState = new SagaState { OrderId=100 }
  - orderEvent = new OrderCreated { ShippingAddress="..." }
  - beforeTime = DateTime.UtcNow
Act:
  - command = saga.CreateDeliveryCommand(sagaState, orderEvent)
  - afterTime = DateTime.UtcNow
Assert:
  - command.OrderId == 100
  - command.ShippingAddress == orderEvent.ShippingAddress
  - command.PreferredDeliveryDate > beforeTime.AddDays(4)
  - command.PreferredDeliveryDate < afterTime.AddDays(6)
  - command.DeliveryNotes.Contains("Order 100")
```

**Test Case 4.2.4**: OrderSaga_CreateReleasePaymentCommand_ShouldParseSagaData
```
Descrição: Validar criação de ReleasePaymentCommand com dados do saga state
Pré-requisitos: OrderSaga, SagaState com dados JSON
Arrange:
  - saga = new OrderSaga(logger)
  - sagaState = new SagaState
    {
      OrderId=100,
      Data = JsonSerializer.Serialize(new { amount = 500.00 })
    }
  - transactionId = "TXN-123"
Act:
  - command = saga.CreateReleasePaymentCommand(sagaState, transactionId)
Assert:
  - command.OrderId == 100
  - command.Amount == 500.00
  - command.OriginalTransactionId == "TXN-123"
  - command.Reason == "Saga compensation"
```

**Test Case 4.2.5**: OrderSaga_CreateReleaseInventoryCommand_ShouldPreserveItems
```
Descrição: Validar criação de ReleaseInventoryCommand
Pré-requisitos: OrderSaga, SagaState, OrderCreated
Arrange:
  - saga = new OrderSaga(logger)
  - sagaState = new SagaState { OrderId=100 }
  - orderEvent com items
Act:
  - command = saga.CreateReleaseInventoryCommand(sagaState, orderEvent)
Assert:
  - command.OrderId == 100
  - command.Items.Count == orderEvent.Items.Count
  - Todos os items mapeados corretamente
  - command.Reason == "Saga compensation"
```

---

### 6. Order Endpoints

#### 6.1 `OrderEndpointsTests.cs`

**Test Case 6.1.1**: OrderEndpoint_CreateOrder_WithValidRequest_ShouldReturn201
```
Descrição: Validar POST /api/orders com dados válidos retorna 201
Pré-requisitos: OrderDbContext mockado, MessagePublisher mockado
Arrange:
  - db = Mock<OrderDbContext>()
  - publisher = Mock<MessagePublisher>()
  - request = new CreateOrderRequest
    {
      CustomerId = 1,
      ShippingAddress = "Rua A, 123",
      Items = [new { ProductId=1, Quantity=2, Price=50 }]
    }
Act:
  - result = await OrderEndpoints.CreateOrder(request, db, publisher, loggerFactory)
Assert:
  - result é CreatedResult (201)
  - ((OrderResponse)result.Value).OrderId > 0
  - db.Orders.Add foi chamado
  - db.SaveChangesAsync foi chamado
  - publisher.PublishEvent foi chamado
Dados de Teste:
  - customerId: 1, 999
  - shippingAddress: endereço normal, 100 chars, 500 chars
  - items: 1 item, 5 items, 10 items
```

**Test Case 6.1.2**: OrderEndpoint_CreateOrder_ShouldPersistToDatabase
```
Descrição: Validar que Order foi persistida no BD
Pré-requisitos: OrderDbContext real (in-memory)
Arrange:
  - db = new OrderDbContext(inMemoryDb)
  - request com dados válidos
Act:
  - result = await OrderEndpoints.CreateOrder(request, db, publisher, loggerFactory)
  - savedOrder = db.Orders.FirstOrDefault()
Assert:
  - savedOrder != null
  - savedOrder.CustomerId == request.CustomerId
  - savedOrder.ShippingAddress == request.ShippingAddress
  - savedOrder.Items.Count == request.Items.Count
  - savedOrder.Status == OrderStatus.PENDING
  - savedOrder.CreatedAt != null
```

**Test Case 6.1.3**: OrderEndpoint_CreateOrder_ShouldPublishEvent
```
Descrição: Validar que OrderCreated event foi publicado
Pré-requisitos: MessagePublisher mockado
Arrange:
  - publisher = Mock<MessagePublisher>()
  - request com dados válidos
Act:
  - result = await OrderEndpoints.CreateOrder(request, db, publisher, loggerFactory)
Assert:
  - publisher.PublishEvent foi chamado exatamente uma vez
  - Argumento é OrderCreated event
  - OrderCreated.OrderId == order.Id
  - OrderCreated.TotalAmount == sum(items)
  - Routing key == EventRoutingKeys.OrderCreated
Dados de Teste:
  - Validar que event type é OrderCreated
  - Validar que routing key correto
```

**Test Case 6.1.4**: OrderEndpoint_CreateOrder_WithEmptyItems_ShouldFail
```
Descrição: Validar falha ao criar pedido sem items
Pré-requisitos: Request com items vazio
Arrange:
  - request.Items = [] (vazio)
Act:
  - result = await OrderEndpoints.CreateOrder(request, db, publisher, loggerFactory)
Assert:
  - result é BadRequest (400)
  - ((ErrorResponse)result.Value).error contém "items"
```

**Test Case 6.1.5**: OrderEndpoint_CreateOrder_WithNegativePrice_ShouldFail
```
Descrição: Validar falha com preço negativo
Pré-requisitos: Item com preço negativo
Arrange:
  - item.Price = -50
Act:
  - result = await OrderEndpoints.CreateOrder(request, db, publisher, loggerFactory)
Assert:
  - result é BadRequest (400)
  - Mensagem indica "price"
```

**Test Case 6.1.6**: OrderEndpoint_GetOrderById_WithValidId_ShouldReturnOrder
```
Descrição: Validar GET /api/orders/{id} com ID válido
Pré-requisitos: Order existe no BD
Arrange:
  - order = new Order { Id=1, CustomerId=1, Status=PENDING, ... }
  - db.Orders contém order
  - orderId = 1
Act:
  - result = await OrderEndpoints.GetOrderById(1, db, loggerFactory)
Assert:
  - result é OkResult (200)
  - ((OrderDetailResponse)result.Value).OrderId == 1
  - response contém todos os items
  - response.Status == "PENDING"
```

**Test Case 6.1.7**: OrderEndpoint_GetOrderById_WithInvalidId_ShouldReturnNotFound
```
Descrição: Validar GET /api/orders/{id} com ID inválido retorna 404
Pré-requisitos: ID não existe
Arrange:
  - orderId = 999
  - DB vazio ou não contém 999
Act:
  - result = await OrderEndpoints.GetOrderById(999, db, loggerFactory)
Assert:
  - result é NotFoundResult (404)
  - ((ErrorResponse)result.Value).error contém "not found"
```

**Test Case 6.1.8**: OrderEndpoint_GetAllOrders_ShouldReturnAllOrders
```
Descrição: Validar GET /api/orders retorna todas as orders
Pré-requisitos: BD com 3 orders
Arrange:
  - db contém orders [1, 2, 3]
Act:
  - result = await OrderEndpoints.GetAllOrders(db, loggerFactory)
Assert:
  - result é OkResult (200)
  - ((List<OrderDetailResponse>)result.Value).Count == 3
  - Cada order tem seus items
```

**Test Case 6.1.9**: OrderEndpoint_GetAllOrders_WhenEmpty_ShouldReturnEmptyList
```
Descrição: Validar retorno vazio quando sem orders
Pré-requisitos: BD sem orders
Arrange:
  - db.Orders está vazio
Act:
  - result = await OrderEndpoints.GetAllOrders(db, loggerFactory)
Assert:
  - result é OkResult (200)
  - ((List<OrderDetailResponse>)result.Value).Count == 0
```

---

### 7. Message Infrastructure

#### 7.1 `MessagePublisherTests.cs`

**Test Case 7.1.1**: MessagePublisher_PublishEvent_ShouldSerializeCorrectly
```
Descrição: Validar serialização de eventos para JSON
Pré-requisitos: MessagePublisher instanciada
Arrange:
  - publisher = new MessagePublisher(channel)
  - orderEvent = new OrderCreated
    {
      OrderId=1,
      CustomerId=2,
      TotalAmount=100.50,
      CorrelationId="corr-123"
    }
Act:
  - publisher.PublishEvent(orderEvent, EventRoutingKeys.OrderCreated)
  - capturedJson = CapturePublishedMessage()
Assert:
  - capturedJson contém "OrderId": 1
  - capturedJson contém "CustomerId": 2
  - capturedJson contém "TotalAmount": 100.50
  - JSON é válido e pode ser desserializado
```

**Test Case 7.1.2**: MessagePublisher_PublishCommand_ShouldUseCorrectRoutingKey
```
Descrição: Validar que comando usa routing key correto
Pré-requisitos: MessagePublisher mockado
Arrange:
  - channel = Mock<IModel>()
  - command = new ProcessPaymentCommand { OrderId=1, Amount=100 }
Act:
  - publisher.PublishCommand(command, "payment.commands")
Assert:
  - channel.BasicPublish foi chamado
  - Argumento exchange == "sagaCommands"
  - Argumento routingKey == "payment.commands"
```

**Test Case 7.1.3**: MessagePublisher_PublishEvent_ShouldIncludeCorrelationId
```
Descrição: Validar que correlationId é incluído nas propriedades
Pré-requisitos: Evento com CorrelationId
Arrange:
  - orderEvent.CorrelationId = "corr-123"
Act:
  - publisher.PublishEvent(orderEvent, routingKey)
  - properties = CaptureMessageProperties()
Assert:
  - properties.Headers["CorrelationId"] == "corr-123"
  - properties.DeliveryMode == 2 (persistent)
```

---

## 🔗 Testes de Integração

### 1. Saga Flow - Happy Path

#### 1.1 `SuccessFlowTests.cs`

**Test Case 1.1.1**: SagaFlow_CreateOrder_ToCompletion_ShouldSucceed
```
Descrição: Fluxo completo bem-sucedido de um pedido
Escopo: Todos os 5 microserviços
Pré-requisitos:
  - RabbitMQ rodando via TestContainers
  - SQL Server rodando via TestContainers
  - Todos os serviços iniciados
  - Tabelas do BD criadas
Setup:
  1. Iniciar RabbitMQ container
  2. Iniciar SQL Server container
  3. Executar migrations
  4. Limpar filas e estado anterior
  5. Iniciar consumers em threads separadas
Teste:
  Arrange:
    - request = new CreateOrderRequest
      {
        CustomerId = 1,
        ShippingAddress = "Quadra 207 sul - Palmas-TO",
        Items = [
          { ProductId=1, Quantity=2, Price=99.99 },
          { ProductId=2, Quantity=1, Price=150.00 }
        ]
      }
    - product1 com estoque 100
    - product2 com estoque 50

  Act:
    1. POST /api/orders (OrderService)
    2. Aguardar OrderCreated event
    3. SagaOrchestrator processa e publica ProcessPaymentCommand
    4. PaymentService processa e publica PaymentCompleted
    5. SagaOrchestrator publica ReserveInventoryCommand
    6. InventoryService processa e publica InventoryReserved
    7. SagaOrchestrator publica ScheduleDeliveryCommand
    8. DeliveryService processa e publica DeliveryScheduled
    9. SagaOrchestrator publica OrderCompleted
    10. OrderService atualiza Order.Status = COMPLETED

  Assert - Após cada etapa:
    ✓ Order criada com status PENDING
    ✓ Order.TotalAmount == 349.98
    ✓ Order.Items.Count == 2
    ✓ Payment criado com status COMPLETED
    ✓ Payment.Amount == 349.98
    ✓ Estoque reduzido:
      - product1: 100 → 98
      - product2: 50 → 49
    ✓ Delivery criada com status SCHEDULED
    ✓ Order.Status == COMPLETED (final)
    ✓ CorrelationId rastreado em todas as mensagens
    ✓ SagaState.Status == COMPLETED

  Timeout: 30 segundos
  Cleanup:
    - Parar consumers
    - Limpar dados de teste
    - Fechar conexões
```

**Test Case 1.1.2**: SagaFlow_MultipleOrders_ShouldProcessIndependently
```
Descrição: Processar 3 pedidos simultaneamente sem interferência
Pré-requisitos: Sistema limpo
Arrange:
  - 3 requests diferentes com IDs de cliente 1, 2, 3
  - Cada um com items diferentes
Act:
  1. Disparar 3 POSTs simultaneamente
  2. Aguardar conclusão de todas as 3 sagas
Assert:
  - 3 orders criadas com IDs diferentes
  - 3 payments independentes
  - 3 reservas de estoque independentes
  - Nenhum cruzamento de dados
  - SagaState.CorrelationId diferente para cada uma
  - Tempo total < 45 segundos
```

**Test Case 1.1.3**: SagaFlow_ShouldPersistAllEvents
```
Descrição: Validar que todos os eventos foram salvos no BD
Pré-requisitos: Saga completa
Act:
  1. Executar fluxo completo
  2. Consultar tabela de eventos
Assert:
  - Evento OrderCreated salvo
  - Evento PaymentProcessed salvo
  - Evento InventoryReserved salvo
  - Evento DeliveryScheduled salvo
  - Evento OrderCompleted salvo
  - Cada evento com timestamp e CorrelationId
```

**Test Case 1.1.4**: SagaFlow_OrderStatusProgression_ShouldFollowSequence
```
Descrição: Validar que Order.Status segue a sequência correta
Pré-requisitos: Saga em andamento
Act:
  1. Criar order
  2. Coletar snapshots do status em cada etapa
Assert:
  - snapshot1: PENDING
  - snapshot2: PROCESSING_PAYMENT
  - snapshot3: RESERVED_INVENTORY
  - snapshot4: DELIVERY_SCHEDULED
  - snapshot5: COMPLETED
  - Cada status com timestamp > anterior
```

---

### 2. Saga Flow - Compensation (Failure Scenarios)

#### 2.1 `CompensationFlowTests.cs`

**Test Case 2.1.1**: SagaFlow_InventoryReservationFails_ShouldCompensatePayment
```
Descrição: Falha na reserva de estoque deve reembolsar pagamento
Pré-requisitos:
  - RabbitMQ e SQL Server rodando
  - Product sem estoque suficiente
Setup:
  1. Criar product1 com apenas 1 unidade
Teste:
  Arrange:
    - request com ProductId=1, Quantity=5 (insuficiente)
    - $500 de crédito disponível

  Act:
    1. POST /api/orders
    2. OrderCreated publicado
    3. PaymentService processa e completa ($500)
    4. InventoryService tenta reservar 5 unidades
    5. FALHA: InventoryFailed publicado
    6. SagaOrchestrator entra em COMPENSATING
    7. Publica ReleasePaymentCommand
    8. PaymentService reembolsa
    9. Order marcada como FAILED

  Assert:
    ✓ Order.Status == FAILED
    ✓ Payment.Status == REFUNDED
    ✓ Payment.RefundReason == "Saga compensation"
    ✓ Estoque não foi alterado (1 unidade permanece)
    ✓ SagaState.Status == FAILED
    ✓ Evento InventoryReservationFailed registrado
    ✓ Evento PaymentReleased registrado
    ✓ CorrelationId consistente em todas as mensagens

  Timeout: 30 segundos
```

**Test Case 2.1.2**: SagaFlow_PaymentFails_ShouldCancelOrder
```
Descrição: Falha no pagamento deve cancelar order
Pré-requisitos:
  - PaymentService configurado para falhar
Setup:
  1. Mock PaymentService para lançar exceção
Teste:
  Arrange:
    - OrderService pronto
    - PaymentService vai falhar
    - InventoryService pronto

  Act:
    1. POST /api/orders
    2. OrderCreated publicado
    3. SagaOrchestrator publica ProcessPaymentCommand
    4. PaymentService falha (PaymentFailed)
    5. SagaOrchestrator não continua para estoque
    6. Order é cancelada

  Assert:
    ✓ Order.Status == FAILED
    ✓ Payment.Status == FAILED
    ✓ Nenhuma tentativa de reserva de estoque
    ✓ SagaState.Status == FAILED
    ✓ Evento PaymentFailed registrado
    ✓ Tempo total < 15 segundos

  Timeout: 30 segundos
```

**Test Case 2.1.3**: SagaFlow_DeliveryFails_ShouldCompensateAllSteps
```
Descrição: Falha na entrega deve compensar tudo (payment + inventory)
Pré-requisitos:
  - DeliveryService configurado para falhar
Teste:
  Arrange:
    - request com dados válidos
    - Estoque disponível
    - Payment vai suceder
    - DeliveryService vai falhar

  Act:
    1. Criar order
    2. Payment sucede
    3. Inventory reservado
    4. DeliveryService falha
    5. COMPENSAÇÃO:
       a. ReleaseInventoryCommand
       b. ReleasePaymentCommand
       c. OrderCancellationCommand

  Assert:
    ✓ Order.Status == FAILED
    ✓ Payment.Status == REFUNDED
    ✓ Estoque restaurado ao estado inicial
    ✓ SagaState.Status == FAILED
    ✓ Evento DeliverySchedulingFailed registrado
    ✓ Eventos de compensação em sequência correta
    ✓ Todos os eventos rastreados com CorrelationId

  Timeout: 45 segundos
```

**Test Case 2.1.4**: SagaFlow_CompensationOrder_ShouldBeReverse
```
Descrição: Compensações devem ocorrer em ordem reversa
Pré-requisitos: Saga com falha na última etapa (Delivery)
Act:
  1. Executar fluxo até Delivery fail
  2. Coletar ordem de compensações
Assert:
  - Compensação 1: Inventory released primeiro
  - Compensação 2: Payment released segundo
  - Compensação 3: Order cancelled terceiro
  - Ordem reversa da execução original
```

---

### 3. Idempotency Tests

#### 3.1 `IdempotencyTests.cs`

**Test Case 3.1.1**: SagaFlow_DuplicateOrderCreated_ShouldNotCreateDuplicate
```
Descrição: Publicar OrderCreated 2x com mesmo CorrelationId
Pré-requisitos: Sistema limpo
Arrange:
  - correlationId = "dup-123"
  - orderCreatedEvent com correlationId="dup-123"
Act:
  1. Publicar OrderCreated (event1) com correlationId
  2. Aguardar processamento
  3. Publicar OrderCreated (event2) duplicado com mesmo correlationId
  4. Aguardar processamento
Assert:
  - Apenas 1 order criada no BD
  - Order.Id existe e é único
  - 2 SagaState podem existir, mas apenas 1 é processada
  - Ou: SagaState rejeita duplicata baseada em CorrelationId
  - Nenhuma duplicação de Payment
Dados de Teste:
  - Intervalo entre duplicatas: 100ms, 1s, 5s
```

**Test Case 3.1.2**: SagaFlow_DuplicatePaymentCommand_ShouldChargeOnlyOnce
```
Descrição: Processar ProcessPaymentCommand 2x não deve cobrar 2x
Pré-requisitos: Order criada, Payment em PENDING
Arrange:
  - paymentCommand com orderId=1, amount=100
  - IdempotencyKey ou correlationId único
Act:
  1. PublishCommand(paymentCommand)
  2. Aguardar Payment COMPLETED
  3. PublishCommand(paymentCommand) duplicado
  4. Aguardar processamento
Assert:
  - Apenas 1 Payment.Status == COMPLETED
  - Ou: 2 Payments mas ambos com transactionId idêntico
  - Métrica de pagamento mostra $100 cobrado 1x
  - Não há 2 transações separadas
```

**Test Case 3.1.3**: SagaFlow_DuplicateInventoryReservation_ShouldReserveOnlyOnce
```
Descrição: Publicar ReserveInventoryCommand 2x não diminui 2x
Pré-requisitos: Inventory com 100 unidades
Arrange:
  - product.AvailableQuantity = 100
  - reserveCommand: qty=5, correlationId="inv-123"
Act:
  1. PublishCommand(reserveCommand)
  2. Aguardar Inventory reservada (95 disponível)
  3. PublishCommand(reserveCommand) duplicado
  4. Aguardar processamento
Assert:
  - product.AvailableQuantity == 95 (não 90)
  - product.ReservedQuantity == 5 (não 10)
  - Apenas 1 evento InventoryReserved registrado
```

---

### 4. Correlation Tests

#### 4.1 `CorrelationTests.cs`

**Test Case 4.1.1**: SagaFlow_ShouldMaintainCorrelationIdThroughout
```
Descrição: Rastrear CorrelationId em toda a cadeia
Pré-requisitos: Saga completa
Act:
  1. Criar order com OrderCreated (gera correlationId)
  2. Coletar correlationId de cada etapa:
     - OrderCreated: corr-abc-123
     - ProcessPaymentCommand: ?
     - PaymentCompleted: ?
     - ReserveInventoryCommand: ?
     - InventoryReserved: ?
     - ScheduleDeliveryCommand: ?
     - DeliveryScheduled: ?
     - OrderCompleted: ?
Assert:
  - Todos têm correlationId == "corr-abc-123"
  - CorrelationId nunca muda durante fluxo
```

**Test Case 4.1.2**: SagaFlow_MultipleConcurrentOrders_ShouldNotMixCorrelationIds
```
Descrição: 5 pedidos não devem ter correlationIds misturados
Pré-requisitos: Sistema limpo
Arrange:
  - requests = [req1, req2, req3, req4, req5]
Act:
  1. Disparar 5 POSTs simultaneamente
  2. Para cada order, rastrear correlationId:
     - order1: corr-1
     - order2: corr-2
     - order3: corr-3
     - order4: corr-4
     - order5: corr-5
  3. Coletar todos os eventos de cada saga
Assert:
  - Eventos de order1 têm APENAS corr-1
  - Eventos de order2 têm APENAS corr-2
  - Etc. (sem cruzamento)
  - Cada order completou CORRETAMENTE
```

---

### 5. Resilience Tests

#### 5.1 `ResilienceTests.cs`

**Test Case 5.1.1**: SagaFlow_RabbitMQRestart_ShouldResume
```
Descrição: Reiniciar RabbitMQ durante saga
Pré-requisitos: RabbitMQ em container
Arrange:
  - Saga em andamento (na etapa de Payment)
  - RabbitMQ ready
Act:
  1. Criar order
  2. OrderCreated publicado
  3. SagaOrchestrator recebendo
  4. [RESTART RabbitMQ container]
  5. Aguardar RabbitMQ voltar
  6. Conexões reconectam
  7. Saga continua
Assert:
  - Saga completa com sucesso
  - Nenhuma perda de mensagens
  - Order.Status == COMPLETED
  - Logs indicam reconexão
```

**Test Case 5.1.2**: SagaFlow_DatabaseTemporaryFailure_ShouldRetryAndSucceed
```
Descrição: Falha temporária no BD deve retentear
Pré-requisitos: Database em container
Arrange:
  - SQL Server pronto
  - Order ready to save
  - Simular timeout na 1ª tentativa
Act:
  1. Tentar INSERT Order
  2. [SIMULATE DB TIMEOUT]
  3. 1ª tentativa falha
  4. Retry logic dispara
  5. 2ª tentativa sucede
Assert:
  - Order salva no BD após retry
  - Order.Id > 0
  - Não há duplicatas
  - Logs mostram retries
```

**Test Case 5.1.3**: SagaFlow_DeadLetterQueue_ShouldHandleUnprocessable
```
Descrição: Mensagem com erro deve ir para DLQ
Pré-requisitos: RabbitMQ com DLQ configurada
Arrange:
  - Invalid message (JSON malformado)
  - DLQ queue vazia
Act:
  1. PublishEvent(malformed_event)
  2. Consumer tenta processar
  3. Falha ao desserializar
  4. Max retries atingido
  5. Mensagem vai para DLQ
Assert:
  - Mensagem em sagaDlqQueue
  - Mensagem não foi descartada
  - Logs indicam erro de processamento
  - Order não foi criada
```

**Test Case 5.1.4**: SagaFlow_ExponentialBackoffRetry_ShouldWait
```
Descrição: Validar que retry segue backoff exponencial
Pré-requisitos: Consumer com retry logic
Arrange:
  - Forçar falha transitória 3x
  - Coletar timestamps de tentativas
Act:
  1. PublishCommand que vai falhar 3x
  2. Anotar timestamp de cada tentativa
  3. Aguardar sucesso na 4ª
Assert:
  - time(retry2) - time(retry1) >= 100ms
  - time(retry3) - time(retry2) >= 200ms
  - time(retry4) - time(retry3) >= 400ms
  - Pattern exponencial 2x
```

**Test Case 5.1.5**: SagaFlow_MaxRetriesExceeded_GoesToDLQ
```
Descrição: Após N retries, mensagem vai para DLQ
Pré-requisitos: Consumer com retry limit
Arrange:
  - Max retries = 3
  - Comando que sempre falha
Act:
  1. PublishCommand
  2. Retry 1 (falha)
  3. Retry 2 (falha)
  4. Retry 3 (falha)
  5. Vai para DLQ
Assert:
  - Mensagem em DLQ após 3 tentativas
  - Logs mostram tentativas
  - Nenhuma processamento bem-sucedido
```

---

### 6. Performance Tests

#### 6.1 `ConcurrentOrderProcessingTests.cs`

**Test Case 6.1.1**: SagaFlow_1000Orders_ConcurrentlyProcessed
```
Descrição: Processar 1000 pedidos simultaneamente
Pré-requisitos: Sistema com recursos suficientes
Arrange:
  - 1000 requests diferentes
  - Produtos com estoque ilimitado
  - Crédito ilimitado
Act:
  1. Disparar 1000 POSTs em paralelo
  2. Limitar a ~10 concurrent
  3. Aguardar todas completarem
Assert:
  - 1000 orders criadas
  - 1000 orders com status COMPLETED
  - 0 duplicatas
  - Tempo total < 5 minutos
  - Nenhuma loss de mensagens
  - BD consistente
Métricas:
  - Throughput: X orders/segundo
  - P50 latência: Xms
  - P99 latência: Xms
```

**Test Case 6.1.2**: SagaFlow_MemoryLeakDetection
```
Descrição: Executar 100 sagas completas e monitorar memória
Pré-requisitos: Profiler disponível
Arrange:
  - Memory profiler configurado
  - Baseline memória medido
Act:
  1. Executar 100 sagas completas sequencialmente
  2. Medir memória a cada 10 sagas:
     - Memory[0]: baseline
     - Memory[10]: after 10 sagas
     - Memory[20]: after 20 sagas
     - ...
     - Memory[100]: after 100 sagas
  3. Executar GC agressivo entre sagas
Assert:
  - Memory[100] - Memory[0] < 50MB
  - Não há crescimento linear
  - Padrão está estável
```

---

### 7. RabbitMQ Configuration Tests

#### 7.1 `ExchangeConfigurationTests.cs`

**Test Case 7.1.1**: RabbitMQ_ShouldHaveRequiredExchanges
```
Descrição: Validar que exchanges existem e estão configuradas
Pré-requisitos: RabbitMQ iniciado
Act:
  1. Conectar ao RabbitMQ
  2. Listar exchanges
Assert:
  - Exchange "sagaCommands" existe (type: Direct)
  - Exchange "sagaEvents" existe (type: Fanout)
  - Exchange "sagaDlq" existe (type: Direct)
  - Todas durável (durable=true)
```

**Test Case 7.1.2**: RabbitMQ_BindingsToQueuesCorrect
```
Descrição: Validar que filas estão corretamente ligadas
Pré-requisitos: RabbitMQ iniciado
Act:
  1. Listar bindings
Assert:
  - Queue "orderCommands" ligada a "sagaCommands"
  - Queue "paymentCommands" ligada a "sagaCommands"
  - Queue "inventoryCommands" ligada a "sagaCommands"
  - Queue "deliveryCommands" ligada a "sagaCommands"
  - Queue "sagaDlqQueue" ligada a "sagaDlq"
  - Routing keys corretos
```

---

### 8. Database Event Sourcing Tests

#### 8.1 `EventSourcingTests.cs`

**Test Case 8.1.1**: EventSourcing_AllEventsSavedToDatabase
```
Descrição: Validar persistência de evento sourcing
Pré-requisitos: Saga completa
Act:
  1. Criar order e processar toda saga
  2. Consultar tabela de eventos
Assert:
  - Evento OrderCreated persisted
  - Evento PaymentStarted persisted
  - Evento PaymentCompleted persisted
  - Evento InventoryReservationStarted persisted
  - Evento InventoryReserved persisted
  - Evento DeliverySchedulingStarted persisted
  - Evento DeliveryScheduled persisted
  - Evento SagaCompleted persisted
  - Total: 8 eventos
Validações:
  - Cada evento com AggregateId (OrderId)
  - Cada evento com CorrelationId
  - Cada evento com Timestamp
  - Cada evento com dados JSON
```

**Test Case 8.1.2**: EventSourcing_CanReplayAndReconstruct
```
Descrição: Reconstruir estado da order a partir de eventos
Pré-requisitos: Saga completa com eventos salvos
Act:
  1. Carregar todos os eventos de um Order
  2. Aplicar sequencialmente para reconstruir estado
  3. Comparar com estado atual no BD
Assert:
  - Estado reconstruído == estado atual
  - Order.Status == COMPLETED
  - Order.Items íntegros
  - Order.TotalAmount correto
```

---

### 9. API Validation Tests

#### 9.1 `CreateOrderApiTests.cs`

**Test Case 9.1.1**: API_CreateOrder_InputValidation_EmptyItems
```
Descrição: Rejeitar request com items vazio
Pré-requisitos: OrderService rodando
Act:
  - POST /api/orders com Items = []
Assert:
  - Response 400 Bad Request
  - Message: "Items cannot be empty"
```

**Test Case 9.1.2**: API_CreateOrder_InputValidation_NegativeQuantity
```
Descrição: Rejeitar quantidade negativa
Pré-requisitos: OrderService rodando
Act:
  - POST /api/orders com Quantity = -5
Assert:
  - Response 400 Bad Request
  - Message contém "Quantity"
```

**Test Case 9.1.3**: API_CreateOrder_InputValidation_ZeroPrice
```
Descrição: Rejeitar preço zero
Pré-requisitos: OrderService rodando
Act:
  - POST /api/orders com Price = 0
Assert:
  - Response 400 Bad Request
  - Message contém "Price"
```

**Test Case 9.1.4**: API_GetOrder_PathValidation_InvalidId
```
Descrição: Rejeitar ID inválido no path
Pré-requisitos: OrderService rodando
Act:
  - GET /api/orders/abc (não é número)
Assert:
  - Response 400 Bad Request
  - Ou 404 Not Found
```

**Test Case 9.1.5**: API_CreateOrder_SizeValidation_LongAddress
```
Descrição: Rejeitar endereço muito longo
Pré-requisitos: OrderService rodando
Arrange:
  - ShippingAddress = "a" * 2000 (2000 caracteres)
Act:
  - POST /api/orders
Assert:
  - Response 400 Bad Request
  - Message contém "ShippingAddress"
```

---

## 📊 Fixtures e Test Data

### 1. Order Fixture

**Arquivo**: `tests/UnitTests/Fixtures/OrderFixture.cs`

```csharp
public class OrderFixture : IDisposable
{
    public Order CreateValidOrder(long customerId = 1)
    {
        return new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.PENDING,
            ShippingAddress = "Rua Teste, 123",
            TotalAmount = 100.00m,
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

    public void Dispose() { }
}
```

### 2. Test Data Builder

**Arquivo**: `tests/IntegrationTests/Fixtures/TestDataBuilder.cs`

```csharp
public class TestDataBuilder
{
    private readonly IServiceProvider _serviceProvider;

    public TestDataBuilder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Order> CreateOrderWithPaymentAsync(
        decimal amount, OrderStatus status = OrderStatus.PENDING)
    {
        // Create order
        var order = new Order { ... };

        // Create payment
        var payment = new Payment { ... };

        // Save
        var db = _serviceProvider.GetRequiredService<OrderDbContext>();
        db.Orders.Add(order);
        db.SaveChangesAsync();

        return order;
    }

    public async Task<Product> CreateProductWithStockAsync(
        int productId, int quantity)
    {
        var inventory = new Inventory { ... };
        var db = _serviceProvider.GetRequiredService<InventoryDbContext>();
        db.Inventories.Add(inventory);
        await db.SaveChangesAsync();

        return /* ... */;
    }
}
```

---

## 🏗️ Infraestrutura de Testes

### 1. Test Containers Setup

**Arquivo**: `tests/IntegrationTests/Fixtures/IntegrationTestBase.cs`

```csharp
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private RabbitMQContainer _rabbitMq;
    private MsSqlContainer _sqlServer;
    protected IServiceProvider ServiceProvider { get; private set; }

    public async Task InitializeAsync()
    {
        // Start containers
        _rabbitMq = new RabbitMQBuilder()
            .WithCleanup(true)
            .Build();
        await _rabbitMq.StartAsync();

        _sqlServer = new MsSqlBuilder()
            .WithPassword("TestPassword123!")
            .Build();
        await _sqlServer.StartAsync();

        // Configure services
        var services = new ServiceCollection();
        // ... configure
        ServiceProvider = services.BuildServiceProvider();

        // Run migrations
        await RunMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbitMq.StopAsync();
        await _sqlServer.StopAsync();
    }

    private async Task RunMigrationsAsync()
    {
        // Apply EF migrations
    }
}
```

### 2. Mock RabbitMQ Consumer

**Arquivo**: `tests/IntegrationTests/Fixtures/MockConsumerHelper.cs`

```csharp
public class MockConsumerHelper
{
    public async Task<T> WaitForEventAsync<T>(
        string queueName,
        TimeSpan timeout) where T : class
    {
        using var tokenSource = new CancellationTokenSource(timeout);

        while (!tokenSource.Token.IsCancellationRequested)
        {
            var message = GetNextMessage(queueName);
            if (message != null)
            {
                return JsonSerializer.Deserialize<T>(message);
            }
            await Task.Delay(100);
        }

        throw new TimeoutException($"Event not received in {timeout}");
    }
}
```

---

## 📅 Cronograma de Implementação

### Fase 1: Fundação (Semana 1-2)
- [ ] Configurar projetos de teste (UnitTests.csproj, IntegrationTests.csproj)
- [ ] Instalar dependências (xUnit, Moq, TestContainers)
- [ ] Criar fixtures e test data builders
- [ ] Criar base classes (IntegrationTestBase, etc)

### Fase 2: Testes Unitários (Semana 2-3)
- [ ] Domain Models (Order, Payment, Inventory, Delivery) - ~15 testes
- [ ] OrderSaga (State transitions, Command creation) - ~10 testes
- [ ] OrderEndpoints - ~9 testes
- [ ] Message Infrastructure - ~3 testes
- **Total**: ~40 testes unitários

### Fase 3: Testes de Integração - Happy Path (Semana 3-4)
- [ ] Success flow tests - ~4 testes
- [ ] Event sourcing tests - ~2 testes
- [ ] API tests - ~5 testes
- **Total**: ~11 testes

### Fase 4: Testes de Integração - Failure & Advanced (Semana 4-5)
- [ ] Compensation flow tests - ~4 testes
- [ ] Idempotency tests - ~3 testes
- [ ] Correlation tests - ~2 testes
- [ ] Resilience tests - ~5 testes
- [ ] RabbitMQ configuration tests - ~2 testes
- **Total**: ~16 testes

### Fase 5: Testes de Performance (Semana 5)
- [ ] Concurrent processing - ~2 testes
- [ ] Memory leak detection - ~1 teste
- **Total**: ~3 testes

### Fase 6: CI/CD & Documentação (Semana 5-6)
- [ ] Configurar GitHub Actions/Azure Pipelines
- [ ] Adicionar badges de cobertura
- [ ] Documentar como rodar testes
- [ ] Documentar troubleshooting

---

## 🚀 Como Executar Testes

### Unitários
```bash
cd tests/UnitTests
dotnet test -v minimal
```

### Integração
```bash
# Requer Docker
cd tests/IntegrationTests
dotnet test --configuration Release -v normal
```

### Todos os Testes
```bash
dotnet test --configuration Release
```

### Com Cobertura
```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=cobertura
```

---

## 📊 Métricas de Sucesso

| Métrica | Target |
|---------|--------|
| Cobertura de Código | ≥ 80% |
| Testes Unitários | 40+ |
| Testes Integração | 30+ |
| Taxa de Sucesso | 100% |
| Tempo (todos os testes) | < 3 min |
| Flakiness | 0% |

---

## 🔗 Recursos e Referências

- xUnit Documentation: https://xunit.net/
- Moq Documentation: https://github.com/moq/moq4
- TestContainers: https://testcontainers.com/
- FluentAssertions: https://fluentassertions.com/
- RabbitMQ Testing: https://www.rabbitmq.com/testing.html

---

**Versão**: 1.0
**Última Atualização**: Janeiro 2026
**Status**: Pronto para Implementação
