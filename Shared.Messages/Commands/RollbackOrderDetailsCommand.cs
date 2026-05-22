using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.Messages.Commands
{
    public class RollbackOrderDetailsCommand
    {
        public int OrderId { get; set; }
        public Guid SagaId { get; set; }
        public DateTime Timestamp { get; set; }

    }
}
