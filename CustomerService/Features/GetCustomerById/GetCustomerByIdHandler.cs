using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using MapsterMapper;
using MediatR;

namespace CustomerService.Features.GetCustomerById;

public class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, GetCustomerByIdResponse>
{
    private readonly CustomerDbContext _db;
    private readonly IMapper _mapper;

    public GetCustomerByIdHandler(CustomerDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<GetCustomerByIdResponse> Handle(
        GetCustomerByIdQuery query, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { query.CustomerId }, ct)
            ?? throw new NotFoundException("Customer", query.CustomerId);

        return _mapper.Map<GetCustomerByIdResponse>(customer);
    }
}
