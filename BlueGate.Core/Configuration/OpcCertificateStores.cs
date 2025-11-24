namespace BlueGate.Core.Configuration
{
    public class OpcCertificateStores
    {
        public OpcCertificateStore ApplicationStore { get; set; }
        public OpcCertificateStore TrustedPeerStore { get; set; }
        public OpcCertificateStore TrustedIssuerStore { get; set; }
        public OpcCertificateStore RejectedStore { get; set; }
    }
}
