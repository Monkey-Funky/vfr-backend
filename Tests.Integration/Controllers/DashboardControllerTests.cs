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

    private readonly string _from;
    private readonly string _to;
    private readonly string _dateRange;

    public DashboardControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
        _from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)).ToString("yyyy-MM-dd");
        _to = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        _dateRange = $"from={_from}&to={_to}";
    }

    // ── Existing tests (kept intact) ──────────────────────────────────────────

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
        var request = new { From = _from, To = _to };

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

    // ── New tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetKpis_WithDateRange_ShouldReturn200()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/kpis?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<KpiDto>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalOrders.Should().BeGreaterThanOrEqualTo(0);
        result.Data.TotalRevenue.Should().BeGreaterThanOrEqualTo(0);
        result.Data.TotalProfit.Should().BeGreaterThanOrEqualTo(0);
        result.Data.TotalTryOns.Should().BeGreaterThanOrEqualTo(0);
        result.Data.ActiveProducts.Should().BeGreaterThanOrEqualTo(0);
        result.Data.LowStockCount.Should().BeGreaterThanOrEqualTo(0);
        result.Data.ConversionRate.Should().BeGreaterThanOrEqualTo(0);
        result.Data.TotalReturns.Should().BeGreaterThanOrEqualTo(0);
        result.Data.NewOrders.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetKpis_WithInvalidDateRange_ShouldReturn422()
    {
        var futureFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)).ToString("yyyy-MM-dd");
        var pastTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd");

        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/kpis?from={futureFrom}&to={pastTo}");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetKpis_ReturnsZeroValues_WhenNoData()
    {
        var isolatedFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)).ToString("yyyy-MM-dd");
        var isolatedTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4)).ToString("yyyy-MM-dd");

        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/kpis?from={isolatedFrom}&to={isolatedTo}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<KpiDto>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalOrders.Should().Be(0);
        result.Data.TotalRevenue.Should().Be(0);
        result.Data.TotalProfit.Should().Be(0);
        result.Data.TotalTryOns.Should().Be(0);
        result.Data.TotalReturns.Should().Be(0);
        result.Data.NewOrders.Should().Be(0);
    }

    [Fact]
    public async Task GetRevenueChart_WithValidDateRange_ShouldReturn200()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/revenue?{_dateRange}&groupBy=Day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ChartDataPoint>>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();

        foreach (var point in result.Data!)
        {
            point.Label.Should().NotBeNullOrWhiteSpace();
            point.Value.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task GetFitAccuracy_ShouldReturn200WithMetrics()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/fit-accuracy?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<FitAccuracyDto>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalPredictions.Should().BeGreaterThanOrEqualTo(0);
        result.Data.AccuratePredictions.Should().BeGreaterThanOrEqualTo(0);
        result.Data.AccuracyPercentage.Should().BeInRange(0, 100);
        result.Data.AccuratePredictions.Should().BeLessThanOrEqualTo(result.Data.TotalPredictions);
    }

    [Fact]
    public async Task GetSizeDistribution_ShouldReturn200()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/size-distribution?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SizeDistributionDto>>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();

        foreach (var entry in result.Data!)
        {
            entry.Size.Should().NotBeNullOrWhiteSpace();
            entry.Count.Should().BeGreaterThanOrEqualTo(0);
            entry.Percentage.Should().BeInRange(0, 100);
        }
    }

    [Fact]
    public async Task GetReturnRate_ShouldReturn200()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/return-rate?{_dateRange}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ReturnRateByProductDto>>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();

        foreach (var entry in result.Data!)
        {
            entry.ProductId.Should().NotBeEmpty();
            entry.ProductName.Should().NotBeNullOrWhiteSpace();
            entry.TotalOrders.Should().BeGreaterThanOrEqualTo(0);
            entry.TotalReturns.Should().BeGreaterThanOrEqualTo(0);
            entry.ReturnRatePercentage.Should().BeInRange(0, 100);
            entry.TotalReturns.Should().BeLessThanOrEqualTo(entry.TotalOrders);
        }
    }

    [Fact]
    public async Task GetRealTimeActivity_ShouldReturn200()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/activity");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ActivityEventDto>>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Count.Should().BeLessThanOrEqualTo(20);

        foreach (var evt in result.Data)
        {
            evt.Id.Should().NotBeEmpty();
            evt.EventType.Should().NotBeNullOrWhiteSpace();
            evt.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
        }
    }

    [Fact]
    public async Task GenerateReport_WithValidRequest_ShouldReturn202()
    {
        var request = new { From = _from, To = _to };

        var response = await Client.PostAsJsonAsync(
            $"/api/retailers/{_retailerId}/dashboard/reports", request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<GenerateReportResponse>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ReportId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetReportStatus_ForNonExistentReport_ShouldReturn404()
    {
        var nonExistentReportId = Guid.NewGuid();

        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/dashboard/reports/{nonExistentReportId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        error.Should().NotBeNull();
        error!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Dashboard_CrossRetailerAccess_ShouldReturn403()
    {
        var foreignRetailerId = Guid.NewGuid();

        var kpisResponse = await Client.GetAsync(
            $"/api/retailers/{foreignRetailerId}/dashboard/kpis?{_dateRange}");
        kpisResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var revenueResponse = await Client.GetAsync(
            $"/api/retailers/{foreignRetailerId}/dashboard/revenue?{_dateRange}");
        revenueResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var activityResponse = await Client.GetAsync(
            $"/api/retailers/{foreignRetailerId}/dashboard/activity");
        activityResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var fitResponse = await Client.GetAsync(
            $"/api/retailers/{foreignRetailerId}/dashboard/fit-accuracy?{_dateRange}");
        fitResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var reportResponse = await Client.PostAsJsonAsync(
            $"/api/retailers/{foreignRetailerId}/dashboard/reports",
            new { From = _from, To = _to });
        reportResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}