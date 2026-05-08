using System.ComponentModel.DataAnnotations;

namespace OrderService.Domain
{
    public class SagaState
    {
        [Key]
        public Guid SagaId { get; set; }
        public int OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string CurrentStep { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsOrderDetailsCompleted { get; set; }
        public bool IsPaymentCompleted { get; set; }
        public bool IsNotificationCompleted { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
    }
}
