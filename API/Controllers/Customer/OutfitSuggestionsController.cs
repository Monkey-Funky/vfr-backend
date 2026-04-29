using API.Controllers.BaseControllers;
using Application.Features.Customer.Outfits.Commands.CreateOutfit;
using Application.Features.Customer.OutfitSuggestions.DTOs;
using Application.Features.Customer.OutfitSuggestions.Queries.GetOutfitSuggestions;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[SwaggerTag("Wardrobe Suggestions — AI-driven outfit creation.")]
[Route("api/customer/wardrobe/suggestions")]
public sealed class OutfitSuggestionsController : CustomerBaseApiController
{
    [HttpPost]
    [SwaggerOperation(
        "Generate AI Suggestions",
        "Generates outfit combinations based on explicit UI weather and occasion parameters.")]
    [ProducesResponseType(typeof(ApiResponse<List<OutfitSuggestionResultDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GenerateSuggestions(
        [FromBody] GetOutfitSuggestionsQuery query, 
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("save")]
    [SwaggerOperation(
        "Save Suggested Outfit",
        "Saves an AI-suggested outfit directly to the customer's saved outfits.")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveSuggestion(
        [FromBody] CreateOutfitCommand command, // Reusing the rock-solid CP-022A command!
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result, "Suggested outfit saved successfully.");
    }
}