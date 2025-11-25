
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Opc.Ua;

namespace BlueGate.Core.Configuration
{
    public class OpcUaOptions
    {
        public List<string> BaseAddresses { get; set; } = new();
        public List<OpcSecurityPolicy> SecurityPolicies { get; set; } = new();
        public List<OpcUserTokenPolicy> UserTokenPolicies { get; set; } = new();
        public OpcCertificateStores CertificateStores { get; set; } = new();
        public bool AutoAcceptUntrustedCertificates { get; set; } = true;
        public string NamespaceUri { get; set; } = "http://bluegate.com/ua";
    }
}
