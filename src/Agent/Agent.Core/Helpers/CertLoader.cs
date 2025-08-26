// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using Agent.Core.Interfaces;
using Agent.Logging;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Helpers
{
    public static class CertLoader
    {
        private static ConcurrentDictionary<string, SecretClient> kvSecretClientCache = new ConcurrentDictionary<string, SecretClient>(StringComparer.OrdinalIgnoreCase);
        private static ConcurrentDictionary<string, CertificateClient> kvCertClientCache = new ConcurrentDictionary<string, CertificateClient>(StringComparer.OrdinalIgnoreCase);

        public static X509Certificate2 LoadCertFromAppService(string SubjectName, string Thumbprint, ILogger? log = null)
        {
            //StoreLocation location = Utilities.IsLocalDevelopment()? StoreLocation.LocalMachine: StoreLocation.CurrentUser;
            StoreLocation location = StoreLocation.CurrentUser;
            X509Store certStore = new X509Store(StoreName.My, location);
            certStore.Open(OpenFlags.ReadOnly);
            X509Certificate2 Cert;


            try
            {
                if (!string.IsNullOrWhiteSpace(SubjectName) && SubjectName.StartsWith("CN=", StringComparison.CurrentCultureIgnoreCase))
                {
                    SubjectName = SubjectName.Substring(3);
                }

                X509Certificate2Collection certCollection = string.IsNullOrWhiteSpace(SubjectName) ? certStore.Certificates.Find(
                                                            X509FindType.FindByThumbprint,
                                                            Thumbprint,
                                                            true) : certStore.Certificates.Find(
                                                            X509FindType.FindBySubjectName,
                                                            SubjectName,
                                                            true);

                // Get the first cert with the thumbprint
                if (certCollection.Count > 0)
                {
                    Cert = certCollection[0];
                    if (log != null) log.LogInternalInformation($"Successfully loaded Cert with thumbprint {Thumbprint}");
                    return Cert;
                }
                else
                {
                    throw new Exception($"Certificate with the subject name '{SubjectName}' was not found in the store");
                }
            }
            catch (Exception ex)
            {
                if (log != null) log.LogInternalInformation($"Error: {ex.Message} occurred while trying to load cert {Thumbprint}, Stack Trace: {ex.StackTrace} ");
                throw;
            }
            finally
            {
                certStore.Close();
            }
        }

        // Load cert from the file path
        public static X509Certificate2 LoadCertFromFile(string certFilePath, string? certPassword = null, ILogger? log = null)
        {
            try
            {
                if (certFilePath.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
                {
                    certFilePath = certFilePath.Substring(7);
                    // Read the file content from the path
                    var content = File.ReadAllText(certFilePath);
                    var certBytes = Convert.FromBase64String(content);
                    var cert = X509CertificateLoader.LoadPkcs12(certBytes, certPassword);

                    log?.LogInternalInformation("Successfully loaded Cert from base64 string");
                    return cert;
                }
                else
                {
                    var cert = X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(certFilePath), certPassword);
                    log?.LogInternalInformation($"Successfully loaded Cert from file {certFilePath}");
                    return cert;
                }
            }
            catch (Exception ex)
            {
                log?.LogInternalError(ex, $"Error: {ex.Message} occurred while trying to load cert from file {certFilePath}");
                throw;
            }
        }

        public static X509Certificate2 LoadCertFromKeyVault(IAuthenticationService authService, string keyVaultUrl, string certificateName, string managedIdentityId, string? certPassword = null, ILogger? log = null)
        {
            try
            {
                log?.LogInternalInformation($"Loading cert from Key Vault: {keyVaultUrl}, Certificate Name: {certificateName}, Managed Identity Client Id: {managedIdentityId}");

                var cred = authService.Get1PAgentKeyVaultCredential(managedIdentityId);
                KeyVaultCertificateWithPolicy certificateWithPolicy = GetKVCertificateClient(keyVaultUrl, cred, log).GetCertificate(certificateName);
                KeyVaultSecret secret = GetKVSecretClient(keyVaultUrl, cred, log).GetSecret(certificateWithPolicy.Name);

                byte[] privateKeyBytes = Convert.FromBase64String(secret.Value);

                // Create an X509Certificate2 object from the byte array
                X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12(privateKeyBytes, certPassword);

                log?.LogInternalInformation($"Successfully loaded Cert from Key Vault, Certificate Subject: {certificate.Subject}");
                return certificate;
            }
            catch (Exception ex)
            {
                log?.LogInternalError(ex, "Error: {} occurred while trying to load cert from Key Vault {}", ex.Message, keyVaultUrl);
                throw;
            }
        }

        private static CertificateClient GetKVCertificateClient(string keyVaultUrl, TokenCredential cred, ILogger? log = null)
        {
            if (kvCertClientCache.TryGetValue(keyVaultUrl, out var certClient))
            {
                return certClient;
            }
            try
            {
                var client = new CertificateClient(new Uri(keyVaultUrl), cred);
                kvCertClientCache[keyVaultUrl] = client;
                log?.LogInternalInformation($"Successfully created CertificateClient for Key Vault: {keyVaultUrl}");
                return client;
            }
            catch (Exception ex)
            {
                log?.LogInternalError(ex, $"Error: {ex.Message} occurred while trying to create CertificateClient for Key Vault {keyVaultUrl}");
                throw;
            }
        }

        private static SecretClient GetKVSecretClient(string keyVaultUrl, TokenCredential cred, ILogger? log = null)
        {
            if (kvSecretClientCache.TryGetValue(keyVaultUrl, out var secretClient))
            {
                return secretClient;
            }
            try
            {
                var client = new SecretClient(new Uri(keyVaultUrl), cred);
                kvSecretClientCache[keyVaultUrl] = client;
                log?.LogInternalInformation($"Successfully created SecretClient for Key Vault: {keyVaultUrl}");
                return client;
            }
            catch (Exception ex)
            {
                log?.LogInternalError(ex, $"Error: {ex.Message} occurred while trying to create SecretClient for Key Vault {keyVaultUrl}");
                throw;
            }
        }
    }
}

