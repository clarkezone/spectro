using System.Threading.Tasks;

namespace Spectro.Core.Extensions
{
    public static class TaskExtensions
    {
        public static void DontAwait(this Task task, string reason) { }
    }
}
