using CustomerService.Common.Entities;
using CustomerService.Features.CreateCustomer;
using CustomerService.Features.GetCustomerById;
using CustomerService.Features.ListCustomers;
using CustomerService.Features.UpdateCustomer;
using Mapster;

namespace CustomerService.Common.Mapping;

public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CreateCustomerRequest, Customer>()
            .Ignore(dest => dest.Status)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.UpdatedAt);

        config.NewConfig<Customer, CreateCustomerResponse>();
        config.NewConfig<Customer, GetCustomerByIdResponse>();
        config.NewConfig<Customer, ListCustomersResponse>();
        config.NewConfig<Customer, UpdateCustomerResponse>();
    }
}
