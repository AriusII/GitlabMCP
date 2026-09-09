using GitlabMCP.Contracts.Epics;
using GitlabMCP.GraphQL.Operations.WorkItems;

namespace GitlabMCP.Mapping.Epics;

/// <summary>
///     Projects <see cref="WorkItemNode" /> (GitlabMCP.GraphQL) into the owned <see cref="EpicSummary" />/
///     <see cref="EpicDetail" /> records — never hands back the GraphQL type itself.
/// </summary>
public static class EpicMapper
{
    public static EpicSummary ToSummary(WorkItemNode node)
    {
        var widget = FindDescriptionWidget(node);
        var labelsWidget = FindLabelsWidget(node);
        var assigneesWidget = FindAssigneesWidget(node);
        var dateWidget = FindDateWidget(node);
        var healthWidget = FindHealthWidget(node);

        return new EpicSummary(
            node.Id,
            node.Iid,
            node.Title,
            node.State.ToString(),
            node.WebUrl,
            widget?.Description,
            labelsWidget?.Labels?.Select(static l => l.Title).ToList() ?? [],
            assigneesWidget?.Assignees?.Select(static a => a.Username).ToList() ?? [],
            dateWidget?.StartDate,
            dateWidget?.DueDate,
            healthWidget?.HealthStatus);
    }

    public static EpicDetail ToDetail(WorkItemNode node)
    {
        var summary = ToSummary(node);
        var hierarchy = node.Widgets?.FirstOrDefault(static w => w.Type == "HIERARCHY");

        return new EpicDetail(
            summary.Id, summary.Iid, summary.Title, summary.State, summary.WebUrl, summary.Description,
            summary.Labels, summary.Assignees, summary.StartDate, summary.DueDate, summary.HealthStatus,
            hierarchy?.HasParent ?? false,
            hierarchy?.HasChildren ?? false,
            hierarchy?.Parent?.Title);
    }

    private static WorkItemWidgetEntry? FindDescriptionWidget(WorkItemNode node)
    {
        return node.Widgets?.FirstOrDefault(static w => w.Description is not null);
    }

    private static WorkItemWidgetEntry? FindLabelsWidget(WorkItemNode node)
    {
        return node.Widgets?.FirstOrDefault(static w => w.Labels is not null);
    }

    private static WorkItemWidgetEntry? FindAssigneesWidget(WorkItemNode node)
    {
        return node.Widgets?.FirstOrDefault(static w => w.Assignees is not null);
    }

    private static WorkItemWidgetEntry? FindDateWidget(WorkItemNode node)
    {
        return node.Widgets?.FirstOrDefault(static w => w.StartDate is not null || w.DueDate is not null);
    }

    private static WorkItemWidgetEntry? FindHealthWidget(WorkItemNode node)
    {
        return node.Widgets?.FirstOrDefault(static w => w.HealthStatus is not null);
    }
}