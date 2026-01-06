namespace Shared.Models;

public enum OrderStatus
{
    PENDING = 0,
    PROCESSING_PAYMENT = 1,
    RESERVED_INVENTORY = 2,
    DELIVERY_SCHEDULED = 3,
    COMPLETED = 4,
    FAILED = 5,
    COMPENSATING = 6,
    COMPENSATED = 7
}
