using ProxyKit;
using WebApplication.Core;

namespace MainHost
{
    public static class ForwardContextExtensions
    {
        public static ForwardContext AddPreSharedKeyHeader(this ForwardContext forwardContext, string preSharedKey)
        {
            forwardContext
                .UpstreamRequest
                .Headers
                .Remove(SharedSettings.PreSharedKeyHeader);

            forwardContext
                .UpstreamRequest
                .Headers
                .TryAddWithoutValidation(SharedSettings.PreSharedKeyHeader, preSharedKey);

            return forwardContext;
        }
    }
}
