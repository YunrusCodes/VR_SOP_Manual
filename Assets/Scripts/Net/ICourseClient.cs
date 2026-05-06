using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inspection.Domain;

namespace Inspection.Net
{
    public interface ICourseClient
    {
        Task<IReadOnlyList<CourseSummary>> ListCoursesAsync(CancellationToken ct);
        Task<Course> GetCourseAsync(string courseName, CancellationToken ct);

        string GetImageUrl(string courseName, string fileName);
        string GetVideoUrl(string courseName, string fileName);
    }
}
