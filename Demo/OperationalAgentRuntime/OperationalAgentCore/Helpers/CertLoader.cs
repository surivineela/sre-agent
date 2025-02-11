using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace OperationalAgentCore
{
    public static class CertLoader
    {
        public static X509Certificate2 LoadCertFromAppService(string SubjectName, string Thumbprint = null, ILogger log = null)
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
                    if (log != null) log.LogInformation($"Successfully loaded Cert with thumbprint {Thumbprint}");
                    return Cert;
                }
                else
                {
                    throw new Exception($"Certificate with the subject name '{SubjectName}' was not found in the store");
                }
            }
            catch (Exception ex)
            {
                if (log != null) log.LogInformation($"Error: {ex.Message} occurred while trying to load cert {Thumbprint}, Stack Trace: {ex.StackTrace} ");
                throw;
            }
            finally
            {
                certStore.Close();
            }
        }
    }
}
