using System;
using System.IO;
using OpenAC.Net.DFe.Core.Storage;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenAC.Net.NFe.Storage;

/// <summary>Gravação assíncrona com substituição atômica no mesmo diretório.</summary>
public sealed class LocalDocumentStore : IDFeDocumentStore
{
    public async Task SaveAsync(IDFeDocument document, CancellationToken cancellationToken = default)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        var arquivo = new DFeDocument(document.Caminho, document.Conteudo, document.ArquivoExiste);
        if (arquivo.ArquivoExiste != DFeArquivoExiste.Sobrescrever)
        {
            await DFeFileSystemDocumentStore.Instance.SaveAsync(arquivo, cancellationToken).ConfigureAwait(false);
            return;
        }

        var path = arquivo.Caminho;
        var content = arquivo.Conteudo;
        cancellationToken.ThrowIfCancellationRequested();

        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");

        var temporary = path + "." + System.Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = new UTF8Encoding(false).GetBytes(content);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192,
                       true))
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else
            {
                try
                {
                    File.Move(temporary, path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
            }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
