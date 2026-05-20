using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerService.Features.GetCustomerById;

public class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, GetCustomerByIdResponse>
{
    private readonly CustomerDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILogger<GetCustomerByIdHandler> _logger;

    public GetCustomerByIdHandler(CustomerDbContext db, IMapper mapper, ILogger<GetCustomerByIdHandler> logger)
    {
        _db = db;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<GetCustomerByIdResponse> Handle(
        GetCustomerByIdQuery query, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { query.CustomerId }, ct);

        if (customer is null)
        {
            _logger.LogWarning("Customer {CustomerId} not found", query.CustomerId);
            throw new NotFoundException("Customer", query.CustomerId);
        }

        _logger.LogInformation("Fetched customer {CustomerId}", query.CustomerId);

        return _mapper.Map<GetCustomerByIdResponse>(customer);
    }
}
