using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.NFe.Common;

namespace OpenAC.Net.NFe.Certificates;

public sealed class NFeCertificadoProvider(X509Certificate2 certificado = null) : INFeCertificadoProvider
{
    public async Task<X509Certificate2> ObterAsync(NFeConfigCertificados configuracao, CancellationToken cancellationToken = default)
    {
        if (configuracao == null) throw new ArgumentNullException(nameof(configuracao));
        cancellationToken.ThrowIfCancellationRequested();
        X509Certificate2 certificate;
        if (certificado != null) certificate = new X509Certificate2(certificado);
        else if (configuracao.CertificadoBytes?.Length > 0)
            certificate = CertificadoDigital.SelecionarCertificado(configuracao.CertificadoBytes, configuracao.Senha);
        else if (File.Exists(configuracao.Certificado))
        {
            using var stream = new FileStream(configuracao.Certificado, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, 8192, cancellationToken).ConfigureAwait(false);
            certificate = CertificadoDigital.SelecionarCertificado(buffer.ToArray(), configuracao.Senha);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(configuracao.Certificado)) throw new InvalidOperationException("Certificado digital não configurado.");
            certificate = configuracao.ObterCertificado();
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (certificate == null || !certificate.HasPrivateKey) throw new InvalidOperationException("Certificado com chave privada não informado.");
            return certificate;
        }
        catch { certificate?.Dispose(); throw; }
    }
}
