# Arquitetura - Saga Pattern com RabbitMQ

---

## 📐 Visão Geral

### O Problema das Transações Distribuídas

Em arquitetura de microserviços, você não pode usar transações ACID tradicionais porque:

```
❌ Impossível:
┌─────────────┐
│  Database 1 │
│   (BEGIN)   │
└──────┬──────┘
       │
┌──────v──────┐
│  Database 2 │  ← Não há comunicação nativa entre DBs
│   (BEGIN)   │
└──────┬──────┘
       │
┌──────v──────┐
│  Database 3 │
│   (BEGIN)   │
└─────────────┘
```

### A Solução: Saga Pattern

O Saga Pattern divide a transação distribuída em **transações locais** orquestradas por **eventos**:

```
✅ Possível:
┌──────────────┐
│ Order (LOCAL │
│ TRANSACTION) │ ──┐
└──────────────┘   │
                   v
             ┌──────────────┐
             │  EVENT BUS   │ (RabbitMQ)
             │ OrderCreated │
             └──────┬───────┘
                    │
        ┌───────────┼───────────┬──────────────┐
        │           │           │              │
        v           v           v              v
   ┌────────┐  ┌────────┐  ┌────────┐  ┌──────────┐
   │Payment │  │Inventory│  │Delivery│  │  Saga    │
   │(LOCAL) │  │ (LOCAL) │  │ (LOCAL)│  │Orchestr. │
   └────────┘  └────────┘  └────────┘  └──────────┘
```

---

## 🏗️ Componentes

### 1. **OrderService** (Porta 5000)
Responsável pela criação e gerenciamento de pedidos.

**Responsabilidades:**
- Receber requisição POST /api/orders
- Criar pedido em status `PENDING`
- Publicar evento `OrderCreated`
- Atualizar status conforme saga progride

---

### 2. **PaymentService** (Porta 5001)
Responsável pelo processamento de pagamentos.

**Responsabilidades:**
- Receber comando `ProcessPaymentCommand`
- Processar pagamento (simula integração com gateway)
- Publicar `PaymentCompleted` ou `PaymentFailed`
- Em compensação: Publicar `ReleasePaymentCommand`

---

### 3. **InventoryService** (Porta 5002)
Responsável pela reserva de estoque.

**Responsabilidades:**
- Receber comando `ReserveInventoryCommand`
- Verificar disponibilidade de estoque
- Reservar ou falhar
- Publicar `InventoryReserved` ou `InventoryFailed`
- Em compensação: Liberar estoque reservado

---

### 4. **DeliveryService** (Porta 5003)
Responsável pelo agendamento de entrega.

**Responsabilidades:**
- Receber comando `ScheduleDeliveryCommand`
- Agendar entrega
- Publicar `DeliveryScheduled` ou `DeliveryFailed`
- Em compensação: Cancelar entrega

---

### 5. **SagaOrchestrator** (Porta 5004)
Responsável por coordenar toda a saga.

**Responsabilidades:**
- Ouvir evento `OrderCreated`
- Gerenciar máquina de estados da saga
- Publicar comandos para os serviços
- Ouvir eventos dos serviços
- Executar compensações em caso de falha
- Persister estado em banco de dados

---

## 📤 Fluxo de Mensagens

### RabbitMQ Topology

```
┌───────────────────────────────────────────────────────────┐
│                    RABBITMQ BROKER                        │
├───────────────────────────────────────────────────────────┤
│                                                           │
│  EXCHANGES:                                               │
│  ──────────                                               │
│  ┌─────────────────────┐  ┌─────────────────────────────┐ │
│  │ saga.commands       │  │ saga.events                 │ │
│  │ (Direct Exchange)   │  │ (Topic Exchange)            │ │
│  └────────┬────────────┘  └───────────┬────────────────── │
│           │                           │                   │
│  BINDINGS:                            │                   │
│  ┌────────v─────┐  ┌──────────────────v──────┐            │
│  │              │  │                         │            │
│  │  Routing:    │  │  Routing Keys:          │            │
│  │  order       │  │  saga.events.order.*    │            │
│  │  payment     │  │  saga.events.payment.*  │            │
│  │  inventory   │  │  saga.events.inventory.*│            │
│  │  delivery    │  │  saga.events.delivery.* │            │
│  │              │  │  saga.events.saga.*     │            │
│  └────────┬─────┘  └──────────────┬──────────┘            │
│           │                       │                       │
│  QUEUES:  │                       │                       │
│           v                       v                       │
│  ┌──────────────────────────────────────────────────────┐ │
│  │ order.commands           → OrderService              │ │
│  │ payment.commands         → PaymentService            │ │
│  │ inventory.commands       → InventoryService          │ │
│  │ delivery.commands        → DeliveryService           │ │
│  │ saga.orchestrator        → SagaOrchestrator          │ │
│  │ saga.dlq                 → Dead Letter Queue         │ │
│  └──────────────────────────────────────────────────────┘ │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

---

## 🔀 Padrões RabbitMQ Utilizados

### 1. **Direct Exchange** (Comandos)
```
Client → Order Service → RabbitMQ (Direct) → Payment Service
                                  ↓
                            routing_key="payment"
```

**Quando usar:**
- Roteamento 1:1 (um comando para um serviço específico)
- Garantia de que uma tarefa vai para o destinatário certo

---

### 2. **Topic Exchange** (Eventos)
```
Payment Service → RabbitMQ (Topic) → [Inventory Service]
                        ↓            [Saga Orchestrator]
                   saga.events.payment.completed

Matching:
- saga.events.payment.*  ✓
- saga.events.*          ✓
- saga.events.payment.#  ✓
```

**Quando usar:**
- Broadcast de eventos para múltiplos subscribers
- Múltiplos serviços interessados no mesmo evento

---

### 3. **Dead Letter Exchange**
```
order.commands (TTL: 5min)
     ↓
  FALHA × 3 tentativas
     ↓
saga.dlq (Dead Letter Queue)
     ↓
Requer intervenção manual
```

---

### 4. **RPC Pattern** (Quando Necessário)
```
Client: "Preciso saber o status do pedido"
   ↓
OrderService
   ↓
Responde em reply_to queue
   ↓
Client recebe resposta
```

---

## ❌ Tratamento de Erros

### Estados de Erro

```
PROCESSING → AWAITING_PAYMENT
                     ↓
              ┌──────┴──────┐
              │             │
              ✓             ✗
              │             │
       AWAITING_      COMPENSATING
       INVENTORY            │
              │             v
              └──────────FAILED
```

### Retry Policy

```
Tentativa 1: Imediato
     ↓ (Nack + Requeue)
Tentativa 2: +5 segundos (TTL + Requeue)
     ↓ (Nack + Requeue)
Tentativa 3: +30 segundos (TTL + Requeue)
     ↓ (Nack + DLQ)
→→→ Dead Letter Queue
    (Alerta para ops)
```

---

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

---

## 📊 Diagrama de Estados da Saga

```
                    ┌─────────────┐
                    │   CREATED   │
                    └──────┬──────┘
                           │ [Publica ProcessPaymentCommand]
                           │
                    ┌──────v─────────────┐
                    │ AWAITING_PAYMENT   │
                    └──────┬──────────┬──┘
                           │          │
                    [Sucesso]      [Falha]
                           │          │
                    ┌──────v──┐  ┌──────v──────────────┐
                    │ AWAITING│  │  COMPENSATING      │
                    │INVENTORY│  │  [Libera pagamento]│
                    └──────┬──┘  └──────┬──────────────┘
                           │            │
                    [Sucesso│Falha]     │
                      │      │          │
                    ┌─┴──────v┐    ┌────v─────┐
                    │AWAITING │    │  FAILED  │
                    │DELIVERY │    └──────────┘
                    └──────┬──┘
                           │
                    [Sucesso│Falha]
                      │      │
                    ┌─┴──────v┐
                    │COMPLETED│
                    └──────────┘
```