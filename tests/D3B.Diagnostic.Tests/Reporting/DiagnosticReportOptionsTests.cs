using D3B.Diagnostic.Reporting;

namespace D3B.Diagnostic.Tests.Reporting;

public sealed class DiagnosticReportOptionsTests
{
    [Fact]
    public void OptionsRejectValuesOutsideContractLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticReportOptions { MaxTypes = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticReportOptions { MaxFieldsPerType = 4_097 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticReportOptions { MaxFieldNodesPerType = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticReportOptions { MaxNestedDepth = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticReportOptions { MaxVariantsPerType = 1_025 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticReportOptions { MaxShapeValues = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticReportOptions { MaxProblemsPerType = 4_097 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticReportOptions { MaxBytes = 65_535 }.Validate());
        new DiagnosticReportOptions().Validate();
    }
}
