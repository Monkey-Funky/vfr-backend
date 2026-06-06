using Application.Features.Customer.Catalog.DTOs;

namespace Application.Features.Customer.Catalog.Queries.GetProductsByModelIds;

/// <summary>
/// Returns a list of products whose <c>model_id</c> matches one of the supplied
/// identifiers.  The identifiers come directly from the AI style-recommendation
/// model response, e.g.:
/// <code>
/// { "matches": ["78_y3ppkj", "66_d3xdez", "39_kuchvf"] }
/// </code>
///
/// CONTRACT:
///   • Only Active, non-deleted products are returned.
///   • The result list preserves the same ordering as the input model IDs
///     (model rank order is meaningful — most-relevant first).
///   • Unknown / not-found model IDs are silently skipped — the caller receives
///     whatever subset actually exists in the catalogue.
///   • The response shape reuses <see cref="ProductCardDto"/> (same as Browse)
///     so the front-end can render results with zero extra mapping.
/// </summary>
public sealed record GetProductsByModelIdsQuery(
    /// <summary>
    /// Model IDs as returned by the AI recommendation model.
    /// Example: ["78_y3ppkj", "66_d3xdez", "39_kuchvf"]
    /// Max 50 items per call — validated by <see cref="GetProductsByModelIdsQueryValidator"/>.
    /// </summary>
    IReadOnlyList<string> ModelIds
) : IRequest<List<ProductCardDto>>;