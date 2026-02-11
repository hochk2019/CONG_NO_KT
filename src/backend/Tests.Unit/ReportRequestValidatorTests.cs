using CongNoGolden.Application.Reports;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class ReportRequestValidatorTests
{
    [Fact]
    public void ValidateDateRange_ReturnsError_WhenMissingDates()
    {
        var missingFrom = ReportRequestValidator.ValidateDateRange(null, DateOnly.FromDateTime(DateTime.Today));
        var missingTo = ReportRequestValidator.ValidateDateRange(DateOnly.FromDateTime(DateTime.Today), null);

        Assert.Equal("Vui lòng chọn khoảng thời gian.", missingFrom);
        Assert.Equal("Vui lòng chọn khoảng thời gian.", missingTo);
    }

    [Fact]
    public void ValidateDateRange_ReturnsError_WhenFromAfterTo()
    {
        var from = new DateOnly(2025, 2, 1);
        var to = new DateOnly(2025, 1, 1);

        var result = ReportRequestValidator.ValidateDateRange(from, to);

        Assert.Equal("Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", result);
    }

    [Fact]
    public void ValidateDateRange_ReturnsNull_WhenValid()
    {
        var from = new DateOnly(2025, 1, 1);
        var to = new DateOnly(2025, 1, 31);

        var result = ReportRequestValidator.ValidateDateRange(from, to);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateAsOfDate_ReturnsError_WhenMissing()
    {
        var result = ReportRequestValidator.ValidateAsOfDate(null);

        Assert.Equal("Vui lòng chọn \"Tính đến ngày\".", result);
    }

    [Fact]
    public void ValidateAsOfDate_ReturnsNull_WhenProvided()
    {
        var result = ReportRequestValidator.ValidateAsOfDate(new DateOnly(2025, 1, 1));

        Assert.Null(result);
    }
}
