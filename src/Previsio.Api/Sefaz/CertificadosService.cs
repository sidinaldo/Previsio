using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Dtos;
using Previsio.Api.Mapeamentos;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Core.Sefaz;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Api.Sefaz;

/// <summary>
/// Cadastro do certificado digital A1 de cada empresa. O .pfx e a senha ficam criptografados;
/// o certificado precisa ser da mesma raiz de CNPJ da empresa (matriz e filiais compartilham a raiz).
/// </summary>
public class CertificadosService
{
    public const int TamanhoMaximoArquivo = 100 * 1024;
    public const string MensagemEmpresaNaoEncontrada = "Empresa não encontrada.";
    public const string MensagemUfInvalida = "Escolha a UF da empresa.";
    public const string MensagemArquivoVazio = "Escolha o arquivo .pfx do certificado.";
    public const string MensagemArquivoGrande = "O arquivo é grande demais para um certificado A1 (máximo 100 KB).";
    public const string MensagemSemCnpj = "Não achei o CNPJ no certificado. Use um e-CNPJ do tipo A1.";

    private readonly PrevisioDbContext _db;
    private readonly IProtetorSegredos _protetor;
    private readonly TimeProvider _relogio;

    public CertificadosService(PrevisioDbContext db, IProtetorSegredos protetor, TimeProvider relogio)
    {
        _db = db;
        _protetor = protetor;
        _relogio = relogio;
    }

    public async Task<Resultado<EmpresaDto>> SalvarAsync(
        int empresaId, byte[] pfx, string senha, string uf, CancellationToken cancelamento)
    {
        Empresa? empresa = await _db.Empresas.Include(e => e.Cliente).FirstOrDefaultAsync(e => e.Id == empresaId, cancelamento);
        if (empresa == null)
        {
            return Resultado<EmpresaDto>.NaoEncontrado(MensagemEmpresaNaoEncontrada);
        }
        if (!UfIbge.Valida(uf))
        {
            return Resultado<EmpresaDto>.Invalido(MensagemUfInvalida);
        }
        if (pfx.Length == 0)
        {
            return Resultado<EmpresaDto>.Invalido(MensagemArquivoVazio);
        }
        if (pfx.Length > TamanhoMaximoArquivo)
        {
            return Resultado<EmpresaDto>.Invalido(MensagemArquivoGrande);
        }

        X509Certificate2 certificado;
        try
        {
            certificado = CertificadoA1.Abrir(pfx, senha ?? "");
        }
        catch (CryptographicException erro)
        {
            if (erro.Message == CertificadoA1.MensagemSemChavePrivada)
            {
                return Resultado<EmpresaDto>.Invalido(CertificadoA1.MensagemSemChavePrivada);
            }
            return Resultado<EmpresaDto>.Invalido(CertificadoA1.MensagemSenhaInvalida);
        }

        using (certificado)
        {
            string? cnpj = CertificadoA1.Cnpj(certificado);
            if (cnpj == null)
            {
                return Resultado<EmpresaDto>.Invalido(MensagemSemCnpj);
            }
            if (cnpj.Substring(0, 8) != empresa.Cnpj.Substring(0, 8))
            {
                return Resultado<EmpresaDto>.Invalido("O certificado é do CNPJ " + Formatar(cnpj)
                    + ", que não é da mesma empresa (raiz " + empresa.Cnpj.Substring(0, 8) + ").");
            }
            DateTime agora = _relogio.GetUtcNow().UtcDateTime;
            DateTime validoAte = certificado.NotAfter.ToUniversalTime();
            if (validoAte < agora)
            {
                return Resultado<EmpresaDto>.Invalido("O certificado venceu em " + validoAte.ToString("dd/MM/yyyy") + ".");
            }

            empresa.CertificadoPfxProtegido = _protetor.Proteger(Convert.ToBase64String(pfx));
            empresa.CertificadoSenhaProtegida = _protetor.Proteger(senha ?? "");
            empresa.CertificadoTitular = Limitar(CertificadoA1.Titular(certificado) + " (" + Formatar(cnpj) + ")", 300);
            empresa.CertificadoValidoAte = validoAte;
            empresa.Uf = uf.Trim().ToUpperInvariant();
        }

        await _db.SaveChangesAsync(cancelamento);
        await RetomarSemCertificadoAsync(empresa.ClienteId, cancelamento);
        return Resultado<EmpresaDto>.Ok(EmpresaMapeamento.ParaDto(empresa));
    }

    public async Task<Resultado<EmpresaDto>> RemoverAsync(int empresaId, CancellationToken cancelamento)
    {
        Empresa? empresa = await _db.Empresas.Include(e => e.Cliente).FirstOrDefaultAsync(e => e.Id == empresaId, cancelamento);
        if (empresa == null)
        {
            return Resultado<EmpresaDto>.NaoEncontrado(MensagemEmpresaNaoEncontrada);
        }
        empresa.CertificadoPfxProtegido = null;
        empresa.CertificadoSenhaProtegida = null;
        empresa.CertificadoTitular = null;
        empresa.CertificadoValidoAte = null;
        await _db.SaveChangesAsync(cancelamento);
        return Resultado<EmpresaDto>.Ok(EmpresaMapeamento.ParaDto(empresa));
    }

    /// <summary>
    /// Abre o certificado guardado da empresa (o chamador descarta). Nulo se não houver ou se não abrir mais.
    /// </summary>
    public X509Certificate2? Abrir(Empresa empresa)
    {
        if (!empresa.TemCertificado)
        {
            return null;
        }
        try
        {
            byte[] pfx = Convert.FromBase64String(_protetor.Revelar(empresa.CertificadoPfxProtegido!));
            string senha = _protetor.Revelar(empresa.CertificadoSenhaProtegida!);
            return CertificadoA1.Abrir(pfx, senha);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // Notas que pararam por falta de certificado voltam para a fila da SEFAZ
    private async Task RetomarSemCertificadoAsync(int clienteId, CancellationToken cancelamento)
    {
        List<NotaSoPdf> paradas = await _db.NotasSoPdf
            .Where(n => n.ClienteId == clienteId && n.Situacao == SituacaoNotaSoPdf.SemCertificado)
            .ToListAsync(cancelamento);
        foreach (NotaSoPdf nota in paradas)
        {
            nota.Situacao = SituacaoNotaSoPdf.BuscandoNaSefaz;
            nota.Motivo = null;
            nota.ProximaTentativaEm = null;
        }
        if (paradas.Count > 0)
        {
            await _db.SaveChangesAsync(cancelamento);
        }
    }

    private static string Formatar(string cnpj)
    {
        return cnpj.Substring(0, 2) + "." + cnpj.Substring(2, 3) + "." + cnpj.Substring(5, 3) + "/"
            + cnpj.Substring(8, 4) + "-" + cnpj.Substring(12, 2);
    }

    private static string Limitar(string texto, int tamanho)
    {
        if (texto.Length <= tamanho)
        {
            return texto;
        }
        return texto.Substring(0, tamanho);
    }
}
