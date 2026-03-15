using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orion.Core.Jobs
{
    public abstract class Job
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string OwnerId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
    }

    public class BuildJob : Job
    {
        public Guid AppId { get; set; }
        public string RepoUrl { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string? BuildCommand { get; set; }
        public string? RunCommand { get; set; }
        public string? BuildFolder { get; set; }
    }

    public interface IJobDispatcher
    {
        ValueTask EnqueueAsync(Job job);
        ValueTask<Job> DequeueAsync(CancellationToken cancellationToken);
    }

    public class JobDispatcher : IJobDispatcher
    {
        private readonly Channel<Job> _queue;

        public JobDispatcher(int capacity = 100)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _queue = Channel.CreateBounded<Job>(options);
        }

        public async ValueTask EnqueueAsync(Job job)
        {
            await _queue.Writer.WriteAsync(job);
        }

        public async ValueTask<Job> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}
