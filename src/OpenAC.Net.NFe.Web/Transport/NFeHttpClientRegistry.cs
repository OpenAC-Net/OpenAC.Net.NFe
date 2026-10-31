using System.Net.Http;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OpenAC.Net.NFe.Web;

internal sealed class NFeHttpClientRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, NFeHttpRegistration> registrations = new();
    private readonly object gate = new();

    internal static string Hash(string value)
    {
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", "");
    }

    internal string Register(string tenant, OpenAC.Net.DFe.Core.Http.DFeHttpTransportOptions options, Uri endpoint)
    {
        var name = "OpenAC.NFe:" + Hash(tenant + "|" + options.GetPoolKey(endpoint));
        lock (gate)
        {
            if (!registrations.ContainsKey(name))
                registrations[name] = new NFeHttpRegistration(new X509Certificate2(options.Certificate!), options);
        }

        return name;
    }

    internal bool TryGet(string name, out NFeHttpRegistration registration) =>
        registrations.TryGetValue(name, out registration!);

    public void Dispose()
    {
        foreach (var registration in registrations.Values) registration.Certificate.Dispose();
        registrations.Clear();
    }
}
