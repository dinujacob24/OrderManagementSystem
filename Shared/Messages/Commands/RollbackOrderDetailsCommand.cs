using System;

namespace Shared.Messages.Commands
{
    public class RollbackOrderDetailsCommand
    {
        public Guid OrderId { get; set; }
        public Guid SagaId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}