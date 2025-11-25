using Opc.Ua;

namespace BlueGate.Core.Configuration
{
    public class OpcUserTokenPolicy
    {
        public string PolicyId { get; set; }
        public UserTokenType TokenType { get; set; }
        public string SecurityPolicy { get; set; }
    }
}
