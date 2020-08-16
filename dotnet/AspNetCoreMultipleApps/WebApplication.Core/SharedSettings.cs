using System;

namespace WebApplication.Core
{
    public class SharedSettings
    {
        public const string PreSharedKeyHeader = "X-Proxy-PreSharedKey";

        /// <summary>
        /// Set the path to override the web root.
        /// </summary>
        public string WebRootPathOverride { get; set; }

        /// <summary>
        /// A key that will appear on each proxied HTTP Request that
        /// will be checked to ensure that the request is only coming
        /// from MainHost.
        /// </summary>
        public string PreShardKey { get; set; }
    }
}
