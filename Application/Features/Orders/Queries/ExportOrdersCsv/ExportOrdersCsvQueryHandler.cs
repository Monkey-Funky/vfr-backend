using Application.Features.Orders.DTOs;
using Application.Features.Orders.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Application.Features.Orders.Queries.ExportOrdersCsv;

/// <summary>
/// Handles ExportOrdersCsvQuery — streams all retailer orders into a CSV byte array.
///
/// FIXES APPLIED:
///   • AsSplitQuery() removed — not available in Application layer.
///   • CancellationToken passed through AsAsyncEnumerable().WithCancellation(ct):
///     ensures the EF Core DataReader is disposed when the client disconnects.
///     Without WithCancellation(ct), disconnecting mid-stream leaks the DB connection.
///   • Scopes the stream to retailerId from JWT (IDOR protection).
/// </summary>
public sealed class ExportOrdersCsvQueryHandler
    : IRequestHandler<ExportOrdersCsvQuery, byte[]>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICurrentUserService _currentUserService;

    public ExportOrdersCsvQueryHandler(
        IOrderRepository orderRepository,
        ICurrentUserService currentUserService)
    {
        _orderRepository = orderRepository;
        _currentUserService = currentUserService;
    }

    public async Task<byte[]> Handle(
        ExportOrdersCsvQuery request,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        var sb = new StringBuilder();

        // CSV header row
        sb.AppendLine("OrderId,CustomerName,OrderDate,TotalAmount,Currency,Status,ItemCount,CreatedAt");

        // ✅ AsAsyncEnumerable() + WithCancellation(ct) guarantees that when
        //    ct fires (on client disconnect), EF Core disposes the DataReader
        //    and returns the connection to the pool immediately.
        await foreach (var order in _orderRepository
            .StreamOrdersAsync(retailerId, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            // Defence-in-depth: explicitly check before each write
            cancellationToken.ThrowIfCancellationRequested();

            sb.AppendLine(order.ToCsvRow().ToCsvLine());
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}