using System.Collections.Generic;

namespace Inspection.Domain
{
    public sealed record Course(
        string Name,
        string DisplayName,
        string Introduction,
        IReadOnlyList<Step> Steps);

    public sealed record CourseSummary(string Name, string DisplayName);

    public sealed record Step(
        int Order,
        string MainTitle,
        string SubTitle,
        string Name,
        string Description,
        Media Media,
        string NextStepIndication,
        IReadOnlyList<ExceptionOption> Exceptions);

    public abstract record Media
    {
        public sealed record None() : Media;
        public sealed record Image(string FileName) : Media;
        public sealed record Video(string FileName) : Media;
    }

    public sealed record ExceptionOption(string Label, ExceptionAction Action);

    public abstract record ExceptionAction
    {
        public sealed record GoToStep(int Step) : ExceptionAction;
        public sealed record ShowMessage(string Text) : ExceptionAction;
    }
}
