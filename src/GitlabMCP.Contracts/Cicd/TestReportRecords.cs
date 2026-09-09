namespace GitlabMCP.Contracts.Cicd;

/// <summary>
///     Test report projection records (domain "cicd"), used by <c>gitlab_get_pipeline_test_report</c>.
///     Test/suite names, failure output and stack traces are GitLab-authored/CI-log-authored text, so the
///     tool wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record TestReportTotalSummary(
    double? Time,
    int? Count,
    int? Success,
    int? Failed,
    int? Skipped,
    int? Error,
    string? SuiteError);

/// <summary>One test suite's roll-up within a pipeline's test report.</summary>
public sealed record TestSuiteSummary(
    string? Name,
    double? TotalTime,
    int? TotalCount,
    int? SuccessCount,
    int? FailedCount,
    int? SkippedCount,
    int? ErrorCount,
    string? SuiteError);

/// <summary>
///     One failed or errored test case, returned only when the caller asks for
///     <c>includeFailedCases</c> — see <see cref="TestReportSummaryResult" />. <see cref="SystemOutput" />
///     and <see cref="StackTrace" /> are each capped per case (see
///     <c>TestReportMapper.MaxCapturedTextPreviewChars</c>) — a captured-output or stack-trace blob is
///     otherwise unbounded, and up to <c>maxFailedCases</c> (default 20, max 100) of these can be returned
///     in one call, so leaving both uncapped could fill the model's context window on their own;
///     <see cref="SystemOutputTruncated" />/<see cref="StackTraceTruncated" /> report whether each was cut.
/// </summary>
public sealed record TestCaseSummary(
    string? Status,
    string? Name,
    string? ClassName,
    string? File,
    double? ExecutionTime,
    string? SystemOutput,
    bool SystemOutputTruncated,
    string? StackTrace,
    bool StackTraceTruncated);

/// <summary>
///     Result of <c>gitlab_get_pipeline_test_report</c>. <see cref="Suites" /> is always the per-suite
///     roll-up; <see cref="FailedCases" /> is populated only when the tool's <c>includeFailedCases</c>
///     parameter is true, bounded by its <c>maxFailedCases</c> parameter, and
///     <see cref="FailedCasesTruncated" /> reports whether more failed/errored cases existed than were
///     returned.
/// </summary>
public sealed record TestReportSummaryResult(
    TestReportTotalSummary? Total,
    IReadOnlyList<TestSuiteSummary> Suites,
    IReadOnlyList<TestCaseSummary> FailedCases,
    bool FailedCasesTruncated);