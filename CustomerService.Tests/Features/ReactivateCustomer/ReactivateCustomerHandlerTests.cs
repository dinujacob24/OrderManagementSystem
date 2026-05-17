using CustomerService.Common.Enums;
using CustomerService.Common.Exceptions;
using CustomerService.Features.ReactivateCustomer;
using CustomerService.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Tests.Features.ReactivateCustomer;

public class ReactivateCustomerHandlerTests
{
    [Fact]
    public async Task Handle_SetsStatusActive_WhenInactive()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Inactive().Build());
        await db.SaveChangesAsync();
        var sut = new ReactivateCustomerHandler(db);

        var response = await sut.Handle(new ReactivateCustomerCommand("CUST-001"), CancellationToken.None);

        response.Status.Should().Be(CustomerStatus.Active);
        var persisted = await db.Customers.SingleAsync();
        persisted.Status.Should().Be(CustomerStatus.Active);
    }

    [Fact]
    public async Task Handle_SetsUpdatedAt_WhenTransitioning()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Inactive().WithUpdatedAt(null).Build());
        await db.SaveChangesAsync();
        var sut = new ReactivateCustomerHandler(db);
        var before = DateTime.UtcNow;

        await sut.Handle(new ReactivateCustomerCommand("CUST-001"), CancellationToken.None);

        var after = DateTime.UtcNow;
        var persisted = await db.Customers.SingleAsync();
        persisted.UpdatedAt.Should().NotBeNull();
        persisted.UpdatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenAlreadyActive()
    {
        await using var db = TestDbContextFactory.Create();
        var existingUpdatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        db.Customers.Add(new CustomerBuilder()
            .WithId("CUST-001")
            .Active()
            .WithUpdatedAt(existingUpdatedAt)
            .Build());
        await db.SaveChangesAsync();
        var sut = new ReactivateCustomerHandler(db);

        var response = await sut.Handle(new ReactivateCustomerCommand("CUST-001"), CancellationToken.None);

        response.Status.Should().Be(CustomerStatus.Active);
        var persisted = await db.Customers.SingleAsync();
        persisted.UpdatedAt.Should().Be(existingUpdatedAt);
    }

    [Fact]
    public async Task Handle_ThrowsConflict_WhenSuspended()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Suspended().Build());
        await db.SaveChangesAsync();
        var sut = new ReactivateCustomerHandler(db);

        var act = () => sut.Handle(new ReactivateCustomerCommand("CUST-001"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*suspended*");
    }

    [Fact]
    public async Task Handle_LeavesSuspendedCustomerUnchanged()
    {
        await using var db = TestDbContextFactory.Create();
        var existingUpdatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        db.Customers.Add(new CustomerBuilder()
            .WithId("CUST-001")
            .Suspended()
            .WithUpdatedAt(existingUpdatedAt)
            .Build());
        await db.SaveChangesAsync();
        var sut = new ReactivateCustomerHandler(db);

        try { await sut.Handle(new ReactivateCustomerCommand("CUST-001"), CancellationToken.None); }
        catch (ConflictException) { }

        var persisted = await db.Customers.SingleAsync();
        persisted.Status.Should().Be(CustomerStatus.Suspended);
        persisted.UpdatedAt.Should().Be(existingUpdatedAt);
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissing()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new ReactivateCustomerHandler(db);

        var act = () => sut.Handle(new ReactivateCustomerCommand("CUST-MISSING"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CUST-MISSING*");
    }
}
