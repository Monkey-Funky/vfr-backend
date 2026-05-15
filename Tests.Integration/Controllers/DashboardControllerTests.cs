using System.Net;
using System.Net.Http.Json;
using Application.Features.Dashboard.DTOs;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class DashboardControllerTests : IntegrationTestBase
{
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
    private readonly string _dateRange = "from=2024-01-01&to=2026-12-31";

    public DashboardControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetKpis_ReturnsOk_WithValidDateRange()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/kpis?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<KpiDto>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetKpis_ReturnsForbidden_WhenRetailerIdDoesNotMatch()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{Guid.NewGuid()}/dashboard/kpis?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRevenueChart_ReturnsOk_WithValidParams()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/revenue?{_dateRange}&groupBy=Day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ChartDataPoint>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetProfitChart_ReturnsOk_WithValidParams()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/profit?{_dateRange}&groupBy=Day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ChartDataPoint>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetSessionsChart_ReturnsOk_WithValidParams()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/sessions?{_dateRange}&groupBy=Day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ChartDataPoint>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetRealTimeActivity_ReturnsOk()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/activity");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ActivityEventDto>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetReturnReasons_ReturnsOk_WithValidDateRange()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/return-reasons?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ReturnReasonDto>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetFitAccuracy_ReturnsOk_WithValidDateRange()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/fit-accuracy?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<FitAccuracyDto>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetSizeDistribution_ReturnsOk_WithValidDateRange()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/size-distribution?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SizeDistributionDto>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetReturnRate_ReturnsOk_WithValidDateRange()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/return-rate?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ReturnRateByProductDto>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetConversionRate_ReturnsOk_WithValidDateRange()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/conversion?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ConversionRateDto>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetEngagement_ReturnsOk_WithValidParams()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/engagement?{_dateRange}&groupBy=Day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<TryOnEngagementDto>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExportDashboard_ReturnsCsv_WithValidDateRange()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/export?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }

    [Fact]
    public async Task GenerateReport_ReturnsAccepted_WhenRequestIsValid()
    {
        var request = new { From = "2024-01-01", To = "2026-12-31" };

        var response = await Client.PostAsJsonAsync(
            $"/api/retailers/{_retailerId}/dashboard/reports", request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task GetReportStatus_ReturnsNotFound_WhenReportDoesNotExist()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/reports/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
