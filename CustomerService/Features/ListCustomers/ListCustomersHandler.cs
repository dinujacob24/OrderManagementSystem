using CustomerService.Common.Persistence;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Features.ListCustomers;

public class ListCustomersHandler : IRequestHandler<ListCustomersQuery, List<ListCustomersResponse>>
{
    private readonly CustomerDbContext _db;
    private readonly IMapper _mapper;

    public ListCustomersHandler(CustomerDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<List<ListCustomersResponse>> Handle(
        ListCustomersQuery query, CancellationToken ct)
    {
        var customers = await _db.Customers
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return customers.Select(c => _mapper.Map<ListCustomersResponse>(c)).ToList();
    }
}
