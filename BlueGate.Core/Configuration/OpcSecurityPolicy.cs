using Opc.Ua;

namespace BlueGate.Core.Configuration
{
    public class OpcSecurityPolicy
    {
        public string SecurityPolicy { get; set; }
        public MessageSecurityMode SecurityMode { get; set; }
    }
}
