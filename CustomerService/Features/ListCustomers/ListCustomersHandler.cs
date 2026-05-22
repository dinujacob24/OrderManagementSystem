using CustomerService.Common.Persistence;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerService.Features.ListCustomers;

public class ListCustomersHandler : IRequestHandler<ListCustomersQuery, List<ListCustomersResponse>>
{
    private readonly CustomerDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILogger<ListCustomersHandler> _logger;

    public ListCustomersHandler(CustomerDbContext db, IMapper mapper, ILogger<ListCustomersHandler> logger)
    {
        _db = db;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<ListCustomersResponse>> Handle(
        ListCustomersQuery query, CancellationToken ct)
    {
        var customers = await _db.Customers
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        _logger.LogInformation("Listed {Count} customers", customers.Count);

        return customers.Select(c => _mapper.Map<ListCustomersResponse>(c)).ToList();
    }
}
