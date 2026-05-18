using System.ComponentModel.DataAnnotations;

namespace OrderService.Domain
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = OrderStatus.Pending;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public void Cancel(string reason)
        {
            Status = OrderStatus.Cancelled;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkAsRefunded()
        {
            Status = OrderStatus.Refunded;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
