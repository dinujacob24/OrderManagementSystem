using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CustomerService.Features.CreateCustomer;

[ApiController]
[Route("api/customers")]
public class CreateCustomerEndpoint : ControllerBase
{
    private readonly IMediator _mediator;
    public CreateCustomerEndpoint(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateCustomerCommand(request), ct);
        return Created($"/api/customers/{response.CustomerId}", response);
    }
}
