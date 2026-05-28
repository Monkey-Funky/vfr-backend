namespace Tests.Unit.DomainTests.Entities;

public sealed class AvatarTests
{
    private static readonly Guid ValidCustomerId = Guid.NewGuid();
    private const decimal ValidHeight = 175m;
    private const decimal ValidWeight = 70m;

    private static Avatar CreateAvatar(
        decimal height = ValidHeight,
        decimal weight = ValidWeight,
        decimal? chest = null,
        decimal? waist = null,
        decimal? hips = null)
        => Avatar.Create(ValidCustomerId, height, weight, chest, waist, hips);

    private static BodyMeasurements CreateMeasurements(
        decimal height = 180m,
        decimal weight = 75m,
        decimal? chest = 95m,
        decimal? waist = 80m,
        decimal? hips = 98m,
        decimal? shoulder = 44m,
        decimal? inseam = 82m,
        decimal? neck = 38m,
        decimal? arm = 62m,
        decimal? shoe = 42m,
        string? bodyShape = "Athletic")
        => new(height, weight, chest, waist, hips, shoulder, inseam, neck, arm, shoe, bodyShape);

    [Fact]
    public void Create_ValidParameters_SetsPropertiesCorrectly()
    {
        var customerId = Guid.NewGuid();
        const decimal height = 178m;
        const decimal weight = 72m;
        const decimal chest = 94m;
        const decimal waist = 82m;

        var avatar = Avatar.Create(customerId, height, weight, chest, waist, bodyShape: "Rectangular");

        avatar.Id.Should().NotBeEmpty();
        avatar.CustomerId.Should().Be(customerId);
        avatar.HeightCm.Should().Be(height);
        avatar.WeightKg.Should().Be(weight);
        avatar.ChestCm.Should().Be(chest);
        avatar.WaistCm.Should().Be(waist);
        avatar.BodyShape.Should().Be("Rectangular");
        avatar.MeasurementHistories.Should().BeEmpty();
        avatar.IsDeleted.Should().BeFalse();
        avatar.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateMeasurements_UpdatesAllFields()
    {
        var avatar = CreateAvatar();
        var measurements = CreateMeasurements();

        avatar.UpdateMeasurements(measurements);

        avatar.HeightCm.Should().Be(measurements.HeightCm);
        avatar.WeightKg.Should().Be(measurements.WeightKg);
        avatar.ChestCm.Should().Be(measurements.ChestCm);
        avatar.WaistCm.Should().Be(measurements.WaistCm);
        avatar.HipsCm.Should().Be(measurements.HipsCm);
        avatar.ShoulderWidthCm.Should().Be(measurements.ShoulderWidthCm);
        avatar.InseamCm.Should().Be(measurements.InseamCm);
        avatar.NeckCm.Should().Be(measurements.NeckCm);
        avatar.ArmLengthCm.Should().Be(measurements.ArmLengthCm);
        avatar.ShoeSizeEu.Should().Be(measurements.ShoeSizeEu);
        avatar.BodyShape.Should().Be(measurements.BodyShape);
        avatar.LastMeasuredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        avatar.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateMeasurements_SavesMeasurementHistory()
    {
        var avatar = CreateAvatar();
        avatar.MeasurementHistories.Should().BeEmpty();
        var firstMeasurements = CreateMeasurements(height: 175m, weight: 70m);
        var secondMeasurements = CreateMeasurements(height: 176m, weight: 72m);

        avatar.UpdateMeasurements(firstMeasurements);
        avatar.UpdateMeasurements(secondMeasurements);

        avatar.MeasurementHistories.Should().HaveCount(2);
        avatar.MeasurementHistories.Should().AllSatisfy(h =>
        {
            h.AvatarId.Should().Be(avatar.Id);
            h.MeasurementData.Should().NotBeNullOrWhiteSpace();
            h.Source.Should().Be("Manual");
            h.RecordedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        });
    }

    [Fact]
    public void Delete_SetsIsDeletedFlag()
    {
        var avatar = CreateAvatar();
        avatar.IsDeleted.Should().BeFalse();

        avatar.MarkAsDeleted();

        avatar.IsDeleted.Should().BeTrue();
    }
}