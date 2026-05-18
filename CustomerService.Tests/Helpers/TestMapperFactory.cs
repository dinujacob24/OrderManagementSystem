using CustomerService.Common.Mapping;
using Mapster;
using MapsterMapper;

namespace CustomerService.Tests.Helpers;

internal static class TestMapperFactory
{
    public static IMapper Create()
    {
        var config = new TypeAdapterConfig();
        new MappingRegister().Register(config);
        return new Mapper(config);
    }
}
