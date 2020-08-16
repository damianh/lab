namespace MainHost
{
    /// <summary>
    /// The purpose of this is to allow hosted service to register information that is
    /// not known up front but _after_ it has started up.
    /// </summary>
    public class HostedServiceContext
    {
        public int WebApplication1Port { get; set; }

        public int WebApplication2Port { get; set; }
    }
}