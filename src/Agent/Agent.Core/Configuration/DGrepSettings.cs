// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration;

public enum AuthMode
{
    ManagedIdentity,
    Certificate
}

public enum CertificateLocation
{
    KeyVault,
    FileSystem,
    CertStore
}

public class DGrepSettings
{
    public string DGrepEndpoint { get; set; } = string.Empty;
    public string MdsEndpoint { get; set; } = string.Empty;
    public string ApplicationCertificate { get; set; } = string.Empty;
    public string KeyVaultUri { get; set; } = string.Empty;
    public string KeyVaultCertificateName { get; set; } = string.Empty;
    public string CertificateSubjectName { get; set; } = string.Empty;
    public string CertificateFilePath { get; set; } = string.Empty;
    public string CertificatePassword { get; set; } = string.Empty;
    public string ManagedIdentityClientId { get; set; } = string.Empty;
    public string AADResource { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
    public AuthMode AuthenticationMode { get; set; } = AuthMode.ManagedIdentity;
    public CertificateLocation CertificateLocation { get; set; } = CertificateLocation.KeyVault;
    public int QueryTimeoutMinutes { get; set; } = 3;
}