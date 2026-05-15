using System.ComponentModel.DataAnnotations;

namespace NotificationService.Domain
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }
        public Guid SagaId { get; set; }
        public int OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = "Email"; // Email, SMS, Push
        public string Status { get; set; } = "Pending"; // Pending, Sent, Failed
        public string? Recipient { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
    }
}
