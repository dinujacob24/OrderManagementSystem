namespace OrderService.Domain
{
    public static class OrderStatus
    {
        public const string Pending = "Pending";
        public const string OrderDetailsProcessing = "OrderDetailsProcessing";
        public const string PaymentProcessing = "PaymentProcessing";
        public const string NotificationProcessing = "NotificationProcessing";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string Failed = "Failed";
    }
}
