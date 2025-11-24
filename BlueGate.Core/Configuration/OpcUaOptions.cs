using System.Collections.Generic;
using Opc.Ua;

namespace BlueGate.Core.Configuration;

public class OpcUaSecurityPolicyOptions
{
    public string SecurityPolicy { get; set; } = SecurityPolicies.Basic256Sha256;

    public MessageSecurityMode SecurityMode { get; set; } = MessageSecurityMode.SignAndEncrypt;
}

public class UserTokenPolicyOptions
{
    public string? PolicyId { get; set; }

    public UserTokenType TokenType { get; set; } = UserTokenType.Anonymous;

    public string? SecurityPolicy { get; set; }
}

public class CertificateStoreOptions
{
    public string StoreType { get; set; } = CertificateStoreType.Directory;

    public string StorePath { get; set; } = string.Empty;
}

public class CertificateStoresOptions
{
    public CertificateStoreOptions ApplicationStore { get; set; } = new();

    public CertificateStoreOptions TrustedPeerStore { get; set; } = new();

    public CertificateStoreOptions TrustedIssuerStore { get; set; } = new();

    public CertificateStoreOptions RejectedStore { get; set; } = new();
}

public class OpcUaOptions
{
    public IList<string> BaseAddresses { get; set; } = new List<string>();

    public IList<OpcUaSecurityPolicyOptions> SecurityPolicies { get; set; } = new List<OpcUaSecurityPolicyOptions>();

    public IList<UserTokenPolicyOptions> UserTokenPolicies { get; set; } = new List<UserTokenPolicyOptions>();

    public CertificateStoresOptions CertificateStores { get; set; } = new();

    public bool AutoAcceptUntrustedCertificates { get; set; }

    public bool AddApplicationCertificateToTrustedStore { get; set; } = true;

    public string? ApplicationSubjectName { get; set; }

    public int? MinimumCertificateKeySize { get; set; }
}
