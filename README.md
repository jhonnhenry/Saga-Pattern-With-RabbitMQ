# RabbitMQ Saga Pattern - Exemplo Complexo

Um exemplo completo e profissional de implementação do **Saga Pattern** (transações distribuídas) usando **RabbitMQ** e **.NET Core 9**, demonstrando como coordenar operações entre múltiplos microserviços sem transações ACID tradicionais.

## 📚 O que é Saga Pattern?

O Saga Pattern resolve o problema de transações distribuídas. Em vez de usar transações ACID, a Saga orquestra uma sequência de operações locais onde cada operação é seguida por um evento que dispara a próxima operação. Se algo falhar, compensating transactions fazem o rollback automático.

## 🎯 Cenário Real: E-commerce Order Processing

Este projeto implementa um fluxo de compra online que envolve 4 microserviços independentes:

```
Cliente cria pedido
    ↓
OrderService (cria pedido)
    ↓
PaymentService (processa pagamento)
    ↓
InventoryService (reserva estoque)
    ↓
DeliveryService (agenda entrega)
    ↓
Pedido COMPLETO ✓
ou CANCELADO com reembolso ✗
```

## ✨ Características Implementadas

- ✅ **Saga Orchestration Pattern** - Coordenação centralizada
- ✅ **Saga State Persistence** - Rastreamento completo de estado da saga
- ✅ **Compensating Transactions** - Rollback automático em falhas
- ✅ **Dead Letter Queue** - Tratamento de mensagens que falham
- ✅ **Message Durability** - Zero perda de mensagens
- ✅ **Message Headers** - Rastreamento via CorrelationId e Timestamp
- ✅ **Message Correlation** - Rastreamento completo de transações
- ✅ **Retry Logic** - Backoff exponencial com TTL
- ✅ **RPC Pattern** - Comunicação síncrona quando necessário

## 🛠️ Stack Tecnológico

- **Runtime**: .NET Core 9
- **Message Broker**: RabbitMQ 4.x
- **Database**: SQL Server 2025
- **ORM**: Entity Framework Core 9
- **API**: ASP.NET Core Minimal APIs
- **Logging**: NLog
- **Testing**: xUnit + TestContainers

---

## 🚀 Quick Start

### 📋 Pré-requisitos

- **.NET SDK 9.0+** - [Baixar aqui](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- **Docker & Docker Compose** - [Baixar aqui](https://docs.docker.com/get-docker/)
- **Git** - [Baixar aqui](https://git-scm.com/)
- **Visual Studio Code ou Visual Studio 2026** (opcional)
- **SQL Server Management Studio (SSMS)** (opcional, para gerenciar BD)

### 🐳 Iniciando com Docker Compose

#### 1. Clone e entre no diretório

```bash
git clone https://github.com/seu-usuario/rabbitmq-saga-pattern.git
cd rabbitmq-saga-pattern
```

#### 2. Inicie os serviços

```bash
docker-compose up -d
```

Isso vai iniciar:
- **RabbitMQ** em `localhost:5672` (AMQP) e `localhost:15672` (Management UI)
- **SQL Server** em `localhost:1433`


#### 3. Aguarde os serviços ficarem prontos
Verifique os logs:

```bash
docker logs rabbitmq-saga-pattern-rabbitmq
docker logs rabbitmq-saga-pattern-sqlserver
docker logs rabbitmq-saga-pattern-init
```


#### 4. Verifique se os serviços estão rodando

**RabbitMQ Management UI:**
```
http://localhost:15672
Login: guest / guest
```

**SQL Server:**
Conecte-se com:
- **Server**: localhost,1433
- **User**: sa
- **Password**: SaPassword123!

---

## 🔧 Configurando o Projeto

### 1. Configure as variáveis de ambiente

Crie um arquivo `.env` na raiz do projeto:

```bash
# RabbitMQ
RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USERNAME=guest
RABBITMQ_PASSWORD=guest
RABBITMQ_VIRTUAL_HOST=/

# SQL Server
SQLSERVER_HOST=localhost
SQLSERVER_PORT=1433
SQLSERVER_USERNAME=sa
SQLSERVER_PASSWORD=SaPassword123!
SQLSERVER_DATABASE=SagaDb

# NLog
NLOG_MIN_LEVEL=Debug

# app
ASPNETCORE_ENVIRONMENT=Development
```

### 2. Executando migrations

```bash
dotnet ef database update -p src/OrderService
dotnet ef database update -p src/PaymentService
dotnet ef database update -p src/InventoryService
dotnet ef database update -p src/DeliveryService
dotnet ef database update -p src/SagaOrchestrator
```


## 🚀 Iniciando os Serviços

### Opção 1: Iniciar cada um em um terminal diferente

**Terminal 0 - Saga Orchestrator**
```bash
cd src/SagaOrchestrator
dotnet run
# Escutando em http://localhost:5004
```

**Terminal 1 - Order Service**
```bash
cd src/OrderService 
dotnet run
# Escutando em http://localhost:5000
```

**Terminal 2 - Payment Service**
```bash
cd src/PaymentService
dotnet run
# Escutando em http://localhost:5001
```

**Terminal 3 - Inventory Service**
```bash
cd src/InventoryService
dotnet run
# Escutando em http://localhost:5002
```

**Terminal 4 - Delivery Service**
```bash
cd src/DeliveryService
dotnet run
# Escutando em http://localhost:5003
```

## 📝 Testando a Implementação

### 1. Verificar RabbitMQ Management UI

```bash
# Abra no navegador
http://localhost:15672

# Login
Username: guest
Password: guest
```

Você deve ver:
- **Exchanges**: `sagaCommands`, `sagaEvents`, `sagaDlq`
- **Queues**: `orderCommands`, `paymentCommands`, `inventoryCommands`, `deliveryCommands`, `sagaOrchestratorEvents`, `sagaDlqQueue`

### 2. Criar um Pedido e Acompanhar Status do Pedido

Acesse a URL:  http://localhost:5000/swagger/index.html
Crie um pedido pelo swagger


### 5. Verificar Banco de Dados

Os produtos disponíveis são criados via SEED
SELECT * FROM [SagaDb].[dbo].[Products]

## Veja as tabelas no SQL Server

SELECT * FROM [dbo].[Orders]
SELECT * FROM [dbo].[OrderItems]
SELECT * FROM [dbo].[Payments]
SELECT * FROM [dbo].[Reservations]
SELECT * FROM [dbo].[Deliveries]
SELECT * FROM [dbo].[SagaEvents]
SELECT * FROM [dbo].[SagaStates]

Caso queira recomeçar delete

delete FROM [dbo].[OrderItems]
delete FROM [dbo].[Deliveries]
delete FROM [dbo].[Payments]
delete FROM [dbo].[Reservations]
delete FROM [dbo].[Orders]
delete FROM [dbo].[SagaEvents]
delete FROM [dbo].[SagaStates]

---

## 🔍 Logs e Debugging

Os logs são gerenciados pelo NLog.
Você verá uma pasta Logs na raiz do projeto.
Apenas os logs de nível Error serão registrados no arquivo de texto.
O restante será registrado no console.

### Rastreando Mensagens

Cada mensagem tem um `CorrelationId`:

```csharp
// Ao publicar
var message = new OrderCreated
{
    OrderId = order.Id,
    CorrelationId = Guid.NewGuid().ToString()  // Gerado aqui
};

// Log com CorrelationId
_logger.LogInformation(
    "Processing order {OrderId} with correlation {CorrelationId}",
    order.Id,
    message.CorrelationId
);
```

---

## 🐛 Troubleshooting

### RabbitMQ
```bash
  # Entrar no container
  docker exec -it rabbitmq-saga-pattern-rabbitmq bash

  # Dentro do container, listar exchanges
  rabbitmqctl list_exchanges

  # Listar queues
  rabbitmqctl list_queues

  # Listar bindings
  rabbitmqctl list_bindings
```

### Erro: "Could not connect to RabbitMQ"

```bash
# Verificar se RabbitMQ está rodando
docker ps | grep rabbitmq

# Reiniciar RabbitMQ
docker-compose restart rabbitmq

# Verificar logs
docker logs rabbitmq-saga-pattern-rabbitmq
```

### Erro: "Connection to SQL Server failed"

```bash
# Verificar se SQL Server está rodando
docker ps | grep sqlserver

# Testar conexão
docker exec rabbitmq-saga-pattern-sqlserver sqlcmd -S localhost -U sa -P 'SaPassword123!' -C
```

### Erro: "Migration failed"

```bash
# Remover migrations anteriores
dotnet ef migrations remove

# Recriar schema
dotnet ef database drop -f
dotnet ef database update
```

### Mensagens ficando em DLQ

1. Verifique os logs
2. No RabbitMQ Management UI, veja a fila `saga.dlq`
3. Veja a mensagem que falhou
4. Corrija o problema e reprocesse manualmente


## 🔄 Sequências Detalhadas

### Cenário 1: Sucesso Total ✓

```
1️⃣  CLIENTE
    POST /api/orders
    ├─ OrderService cria Order(PENDING)
    └─ Publica OrderCreated
    │
2️⃣  SAGA ORCHESTRATOR
    Ouve OrderCreated
    ├─ Atualiza SagaState = AWAITING_PAYMENT
    └─ Publica ProcessPaymentCommand
    │
3️⃣  PAYMENT SERVICE
    Ouve ProcessPaymentCommand
    ├─ Processa pagamento ✓
    ├─ Registra Payment(COMPLETED)
    └─ Publica PaymentCompleted
    │
4️⃣  SAGA ORCHESTRATOR
    Ouve PaymentCompleted
    ├─ Atualiza SagaState = AWAITING_INVENTORY
    └─ Publica ReserveInventoryCommand
    │
5️⃣  INVENTORY SERVICE
    Ouve ReserveInventoryCommand
    ├─ Verifica estoque ✓
    ├─ Reserva itens
    └─ Publica InventoryReserved
    │
6️⃣  SAGA ORCHESTRATOR
    Ouve InventoryReserved
    ├─ Atualiza SagaState = AWAITING_DELIVERY
    └─ Publica ScheduleDeliveryCommand
    │
7️⃣  DELIVERY SERVICE
    Ouve ScheduleDeliveryCommand
    ├─ Agenda entrega ✓
    ├─ Registra Delivery(SCHEDULED)
    └─ Publica DeliveryScheduled
    │
8️⃣  SAGA ORCHESTRATOR
    Ouve DeliveryScheduled
    ├─ Atualiza SagaState = COMPLETED
    ├─ Publica OrderCompleted
    └─ Saga termina com SUCESSO ✓
    │
9️⃣  ORDER SERVICE
    Ouve OrderCompleted
    └─ Atualiza Order.Status = COMPLETED
```

---

### Cenário 2: Falha no Inventário (com Compensação) ✗

```
(Passos 1-5 idênticos ao Cenário 1)

6️⃣  INVENTORY SERVICE
    Ouve ReserveInventoryCommand
    ├─ Verifica estoque ✗ (SEM ESTOQUE)
    └─ Publica InventoryFailed
    │
7️⃣  SAGA ORCHESTRATOR
    Ouve InventoryFailed
    ├─ Atualiza SagaState = COMPENSATING
    ├─ Salva evento InventoryReservationFailed
    └─ INICIA COMPENSAÇÃO em ORDER REVERSO:
    │
    ├─→ Publica ReleasePaymentCommand
    │   PaymentService
    │   ├─ Reembolsa pagamento
    │   ├─ Atualiza Payment(REFUNDED)
    │   └─ Publica PaymentReleased
    │
    ├─→ Publica OrderCancellationCommand
    │   OrderService
    │   └─ Atualiza Order.Status = FAILED
    │
    └─ Atualiza SagaState = FAILED

8️⃣  FIM DA SAGA (com FALHA)
    └─ Cliente notificado sobre falha
```

## 📊 Arquitetura Detalhada

Veja [Docs/ARCHITECTURE.md](./Docs/ARCHITECTURE.md) para:
- Diagramas de sequência
- Explicação dos padrões RabbitMQ
- Estados da Saga

---

## 📁 Estrutura do Projeto

```
src/
├── Shared/                    # Contratos de eventos e comandos
├── OrderService/              # Microserviço de pedidos
├── PaymentService/            # Microserviço de pagamentos
├── InventoryService/          # Microserviço de estoque
├── DeliveryService/           # Microserviço de entrega
└── SagaOrchestrator/          # Orquestrador da saga
```

## 🔍 Conceitos-Chave Demonstrados

### 1. **Message-Driven**
- Desacoplamento entre serviços
- Comunicação assíncrona via eventos

### 2. **Distributed Transaction Pattern**
- Como coordenar operações sem ACID
- Trade-offs entre consistência eventual

### 3. **Error Handling & Resilience**
- Retry com backoff exponencial
- Dead Letter Queues

### 4. **Saga State Persistence**
- Todos os eventos persistidos
- Possibilidade de replay
- Auditoria completa

### 5. **Recursos RabbitMQ utilizados**
  - Saga Orchestration Pattern - Orquestrador centralizado
  - Compensating Transactions - Lógica de compensação no SagaOrchestrationHandler
  - Dead Letter Queue (DLQ) - Implementado com exchange e fila dedicada
  - Message Durability - _basicProperties.Persistent = true em MessagePublisher
  - Message Correlation - CorrelationId consistentemente usado em todas as mensagens
  - Retry Logic - Presente em DeliveryCommandConsumer com retry count
---

## 💡 Casos de Uso Reais

Este padrão é usado em:
- **E-commerce**: Processamento de pedidos
- **Fintech**: Transferências bancárias distribuídas
- **Viagens**: Reserva de voos + hotéis
- **Logística**: Coordenação de múltiplos fornecedores
- **Healthcare**: Workflows de autorização de seguros

---

## 👤 Autor

**Jhonatas Lima**
- Website: www.jhonataslima.com

---

## 🙏 Agradecimentos

- RabbitMQ Documentation
- Claude Code
- Gemini 3
- Antigravity
- Visual Studio
- Git
- Docker
- SQL Server

---

## 📞 Contato e Suporte
- Visite www.jhonataslima.com

---
**⭐ Achou útil, considere deixar uma estrela!**
