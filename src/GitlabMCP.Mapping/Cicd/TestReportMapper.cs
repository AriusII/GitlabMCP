using GitLab.Client.Models;
using GitlabMCP.Contracts.Cicd;

namespace GitlabMCP.Mapping.Cicd;

/// <summary>Projects pipeline test-report DTOs into the owned "cicd" records.</summary>
public static class TestReportMapper
{
    /// <summary>
    ///     Per-case cap on <see cref="TestCaseSummary.SystemOutput" /> and <see cref="TestCaseSummary.StackTrace" />.
    ///     Each is a captured-output/stack-trace blob with no size limit of its own, and
    ///     <c>gitlab_get_pipeline_test_report(includeFailedCases: true)</c> can return up to 100 cases in one
    ///     call, each carrying both fields -- unlike <c>CiLintMapper.MaxMergedYamlPreviewChars</c> or
    ///     <c>WikiMapper.MaxWikiContentPreviewChars</c>, which cap a single field returned once (or once per
    ///     list item), this cap guards two fields multiplied across up to <c>maxFailedCases</c> items, so it is
    ///     smaller than either.
    /// </summary>
    private const int MaxCapturedTextPreviewChars = 4_000;

    public static TestReportTotalSummary? ToSummary(GitLabTestReportTotal? total)
    {
        return total is null
            ? null
            : new TestReportTotalSummary(
                total.Time,
                total.Count,
                total.Success,
                total.Failed,
                total.Skipped,
                total.Error,
                total.SuiteError);
    }

    public static TestSuiteSummary ToSummary(GitLabTestSuite suite)
    {
        return new TestSuiteSummary(
            suite.Name,
            suite.TotalTime,
            suite.TotalCount,
            suite.SuccessCount,
            suite.FailedCount,
            suite.SkippedCount,
            suite.ErrorCount,
            suite.SuiteError);
    }

    public static TestCaseSummary ToSummary(GitLabTestCase testCase)
    {
        var (systemOutput, systemOutputTruncated) = Truncate(testCase.SystemOutput);
        var (stackTrace, stackTraceTruncated) = Truncate(testCase.StackTrace);

        return new TestCaseSummary(
            testCase.Status,
            testCase.Name,
            testCase.ClassName,
            testCase.File,
            testCase.ExecutionTime,
            systemOutput,
            systemOutputTruncated,
            stackTrace,
            stackTraceTruncated);
    }

    private static (string? Text, bool Truncated) Truncate(string? text)
    {
        if (text is null) return (null, false);

        return text.Length > MaxCapturedTextPreviewChars
            ? (text[..MaxCapturedTextPreviewChars], true)
            : (text, false);
    }
}