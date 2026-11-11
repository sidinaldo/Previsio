namespace Previsio.Core.Entidades;

/// <summary>
/// Onde está a nota que chegou por e-mail só em PDF (sem o XML).
/// </summary>
public enum SituacaoNotaSoPdf
{
    // O Previsio está buscando o XML na SEFAZ pela chave de acesso
    BuscandoNaSefaz = 0,

    // A chave de acesso não foi achada no PDF, no nome do arquivo nem no e-mail
    SemChave = 1,

    // Nenhuma loja da rede tem certificado digital A1 para consultar a SEFAZ
    SemCertificado = 2,

    // A SEFAZ não devolveu o XML depois das tentativas (ou recusou a consulta)
    NaoResolvida = 3,

    // O XML foi obtido (SEFAZ ou anexado à mão) e virou nota normal
    Resolvida = 4,

    // Alguém lançou a conta direto no Bling
    LancadaManualmente = 5
}
