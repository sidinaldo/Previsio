using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Previsio.Core.Sefaz;

namespace Previsio.Api.Sefaz;

/// <summary>
/// Extrai, sem biblioteca externa, os textos de um PDF (DANFE) para procurar a chave de acesso.
/// Não monta o layout da página: junta as strings desenhadas em cada conteúdo, das duas formas
/// (coladas e separadas por espaço), e traduz as fontes CID pelo mapa ToUnicode de cada fonte.
/// Basta para achar 44 dígitos; a validação da chave (módulo 11) descarta o que vier errado.
/// </summary>
public static class TextoPdf
{
    private const int TamanhoMaximoPdf = 15 * 1024 * 1024;
    private const int TamanhoMaximoStream = 20 * 1024 * 1024;

    private static readonly Encoding Latin1 = Encoding.Latin1;
    private static readonly Regex InicioStream = new Regex(@"stream\r?\n", RegexOptions.Compiled);
    private static readonly Regex BfChar = new Regex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>", RegexOptions.Compiled);
    private static readonly Regex BfRange = new Regex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*(<[0-9A-Fa-f]+>|\[[^\]]*\])", RegexOptions.Compiled);

    /// <summary>
    /// Chave de acesso encontrada no texto do PDF; nula se não houver ou se o arquivo não for lido.
    /// </summary>
    public static string? ProcurarChave(byte[] pdf)
    {
        foreach (string texto in Textos(pdf))
        {
            string? chave = ChaveAcesso.Procurar(texto);
            if (chave != null)
            {
                return chave;
            }
        }
        return null;
    }

    /// <summary>
    /// Os textos do PDF em várias leituras possíveis (para procurar números, não para exibir).
    /// </summary>
    public static List<string> Textos(byte[] pdf)
    {
        var textos = new List<string>();
        if (pdf == null || pdf.Length < 8 || pdf.Length > TamanhoMaximoPdf)
        {
            return textos;
        }

        List<string> streams = StreamsDecodificados(pdf);
        var mapas = new List<Dictionary<string, string>>();
        foreach (string conteudo in streams)
        {
            if (conteudo.Contains("begincmap"))
            {
                Dictionary<string, string> mapa = LerCMap(conteudo);
                if (mapa.Count > 0)
                {
                    mapas.Add(mapa);
                }
            }
        }

        foreach (string conteudo in streams)
        {
            if (conteudo.Contains("begincmap"))
            {
                continue;
            }
            List<byte[]> strings = StringsDesenhadas(conteudo);
            if (strings.Count == 0)
            {
                // Pode ter a chave em texto puro (metadados, anotações)
                textos.Add(conteudo);
                continue;
            }

            // Fontes simples: os bytes já são os caracteres
            textos.Add(Juntar(strings, null, ""));
            textos.Add(Juntar(strings, null, " "));
            // Fontes CID: cada mapa ToUnicode é uma tentativa
            foreach (Dictionary<string, string> mapa in mapas)
            {
                textos.Add(Juntar(strings, mapa, ""));
                textos.Add(Juntar(strings, mapa, " "));
            }
        }

        // O próprio arquivo, para chaves gravadas fora de streams (ex.: título do documento)
        textos.Add(Latin1.GetString(pdf));
        return textos;
    }

    private static List<string> StreamsDecodificados(byte[] pdf)
    {
        var resultado = new List<string>();
        string bruto = Latin1.GetString(pdf);
        int posicao = 0;
        while (true)
        {
            Match inicio = InicioStream.Match(bruto, posicao);
            if (!inicio.Success)
            {
                break;
            }
            int comecoDados = inicio.Index + inicio.Length;
            int fim = bruto.IndexOf("endstream", comecoDados, StringComparison.Ordinal);
            if (fim < 0)
            {
                break;
            }
            posicao = fim + 9;

            // "stream" precisa vir logo depois do dicionário do objeto (">>"); evita casar "endstream"
            if (inicio.Index >= 3 && bruto.Substring(inicio.Index - 3, 3) == "end")
            {
                continue;
            }
            int comecoDicionario = bruto.LastIndexOf("<<", inicio.Index, StringComparison.Ordinal);
            string dicionario = "";
            if (comecoDicionario >= 0 && inicio.Index - comecoDicionario < 4000)
            {
                dicionario = bruto.Substring(comecoDicionario, inicio.Index - comecoDicionario);
            }
            // Imagens e fontes embutidas não têm texto
            if (dicionario.Contains("/Image") || dicionario.Contains("/DCTDecode") || dicionario.Contains("/FontFile"))
            {
                continue;
            }

            int tamanho = fim - comecoDados;
            if (tamanho <= 0 || tamanho > TamanhoMaximoStream)
            {
                continue;
            }
            byte[] dados = new byte[tamanho];
            Array.Copy(pdf, comecoDados, dados, 0, tamanho);

            if (dicionario.Contains("/FlateDecode"))
            {
                byte[]? descompactado = Descompactar(dados);
                if (descompactado == null)
                {
                    continue;
                }
                dados = descompactado;
            }
            else if (dicionario.Contains("/Filter"))
            {
                // Outros filtros (LZW, ASCII85...) não são usados nos DANFEs comuns
                continue;
            }
            resultado.Add(Latin1.GetString(dados));
        }
        return resultado;
    }

    private static byte[]? Descompactar(byte[] dados)
    {
        try
        {
            using (var entrada = new MemoryStream(dados))
            using (var zlib = new ZLibStream(entrada, CompressionMode.Decompress))
            using (var saida = new MemoryStream())
            {
                CopiarLimitado(zlib, saida);
                return saida.ToArray();
            }
        }
        catch (InvalidDataException)
        {
            // Alguns geradores gravam o deflate sem o cabeçalho zlib
            try
            {
                using (var entrada = new MemoryStream(dados))
                using (var deflate = new DeflateStream(entrada, CompressionMode.Decompress))
                using (var saida = new MemoryStream())
                {
                    CopiarLimitado(deflate, saida);
                    return saida.ToArray();
                }
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }
    }

    private static void CopiarLimitado(Stream origem, MemoryStream destino)
    {
        byte[] buffer = new byte[81920];
        int lidos;
        while ((lidos = origem.Read(buffer, 0, buffer.Length)) > 0)
        {
            destino.Write(buffer, 0, lidos);
            if (destino.Length > TamanhoMaximoStream)
            {
                throw new InvalidDataException("Stream grande demais");
            }
        }
    }

    /// <summary>
    /// Strings literais (...) e hexadecimais &lt;...&gt; do conteúdo, na ordem em que aparecem.
    /// Dicionários (&lt;&lt; ... &gt;&gt;) são pulados.
    /// </summary>
    private static List<byte[]> StringsDesenhadas(string conteudo)
    {
        var strings = new List<byte[]>();
        int i = 0;
        while (i < conteudo.Length)
        {
            char caractere = conteudo[i];
            if (caractere == '%')
            {
                // Comentário até o fim da linha
                while (i < conteudo.Length && conteudo[i] != '\n' && conteudo[i] != '\r')
                {
                    i++;
                }
            }
            else if (caractere == '(')
            {
                i = LerLiteral(conteudo, i, strings);
            }
            else if (caractere == '<')
            {
                if (i + 1 < conteudo.Length && conteudo[i + 1] == '<')
                {
                    i = i + 2;
                }
                else
                {
                    i = LerHexadecimal(conteudo, i, strings);
                }
            }
            else
            {
                i++;
            }
        }
        return strings;
    }

    private static int LerLiteral(string conteudo, int inicio, List<byte[]> strings)
    {
        var bytes = new List<byte>();
        int nivel = 1;
        int i = inicio + 1;
        while (i < conteudo.Length && nivel > 0)
        {
            char caractere = conteudo[i];
            if (caractere == '\\' && i + 1 < conteudo.Length)
            {
                char proximo = conteudo[i + 1];
                if (proximo >= '0' && proximo <= '7')
                {
                    int fimOctal = i + 1;
                    while (fimOctal < conteudo.Length && fimOctal < i + 4 && conteudo[fimOctal] >= '0' && conteudo[fimOctal] <= '7')
                    {
                        fimOctal++;
                    }
                    bytes.Add((byte)Convert.ToInt32(conteudo.Substring(i + 1, fimOctal - i - 1), 8));
                    i = fimOctal;
                    continue;
                }
                if (proximo == 'n')
                {
                    bytes.Add((byte)'\n');
                }
                else if (proximo == 'r')
                {
                    bytes.Add((byte)'\r');
                }
                else if (proximo == 't')
                {
                    bytes.Add((byte)'\t');
                }
                else if (proximo == '\r' || proximo == '\n')
                {
                    // Quebra de linha escapada: continuação
                }
                else
                {
                    bytes.Add((byte)proximo);
                }
                i = i + 2;
                continue;
            }
            if (caractere == '(')
            {
                nivel++;
            }
            else if (caractere == ')')
            {
                nivel--;
                if (nivel == 0)
                {
                    break;
                }
            }
            bytes.Add((byte)caractere);
            i++;
        }
        strings.Add(bytes.ToArray());
        return i + 1;
    }

    private static int LerHexadecimal(string conteudo, int inicio, List<byte[]> strings)
    {
        int fim = conteudo.IndexOf('>', inicio + 1);
        if (fim < 0)
        {
            return conteudo.Length;
        }
        string hex = Regex.Replace(conteudo.Substring(inicio + 1, fim - inicio - 1), @"\s", "");
        if (hex.Length % 2 == 1)
        {
            hex = hex + "0";
        }
        var bytes = new byte[hex.Length / 2];
        for (int b = 0; b < bytes.Length; b++)
        {
            if (!byte.TryParse(hex.Substring(b * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[b]))
            {
                return fim + 1;
            }
        }
        strings.Add(bytes);
        return fim + 1;
    }

    private static string Juntar(List<byte[]> strings, Dictionary<string, string>? mapa, string separador)
    {
        var texto = new StringBuilder();
        foreach (byte[] bytes in strings)
        {
            if (texto.Length > 0)
            {
                texto.Append(separador);
            }
            if (mapa == null)
            {
                texto.Append(Latin1.GetString(bytes));
            }
            else
            {
                texto.Append(Traduzir(bytes, mapa));
            }
        }
        return texto.ToString();
    }

    // Códigos de 2 bytes (fontes CID) ou de 1 byte, o que o mapa conhecer
    private static string Traduzir(byte[] bytes, Dictionary<string, string> mapa)
    {
        var texto = new StringBuilder();
        int i = 0;
        while (i < bytes.Length)
        {
            if (i + 1 < bytes.Length)
            {
                string dois = bytes[i].ToString("X2") + bytes[i + 1].ToString("X2");
                if (mapa.ContainsKey(dois))
                {
                    texto.Append(mapa[dois]);
                    i = i + 2;
                    continue;
                }
            }
            string um = bytes[i].ToString("X2");
            if (mapa.ContainsKey(um))
            {
                texto.Append(mapa[um]);
            }
            i++;
        }
        return texto.ToString();
    }

    private static Dictionary<string, string> LerCMap(string cmap)
    {
        var mapa = new Dictionary<string, string>();

        foreach (string bloco in Blocos(cmap, "beginbfchar", "endbfchar"))
        {
            foreach (Match par in BfChar.Matches(bloco))
            {
                mapa[Normalizar(par.Groups[1].Value)] = Unicode(par.Groups[2].Value);
            }
        }

        foreach (string bloco in Blocos(cmap, "beginbfrange", "endbfrange"))
        {
            foreach (Match faixa in BfRange.Matches(bloco))
            {
                string inicioHex = faixa.Groups[1].Value;
                int inicio = int.Parse(inicioHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int fim = int.Parse(faixa.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (fim < inicio || fim - inicio > 4096)
                {
                    continue;
                }
                string destino = faixa.Groups[3].Value;
                int largura = inicioHex.Length;
                if (destino.StartsWith("<"))
                {
                    string destinoHex = destino.Trim('<', '>');
                    int baseDestino = int.Parse(destinoHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    for (int codigo = inicio; codigo <= fim; codigo++)
                    {
                        string chave = Normalizar(codigo.ToString("X" + largura));
                        mapa[chave] = char.ConvertFromUtf32(baseDestino + (codigo - inicio));
                    }
                }
                else
                {
                    MatchCollection destinos = Regex.Matches(destino, "<([0-9A-Fa-f]+)>");
                    for (int d = 0; d < destinos.Count && inicio + d <= fim; d++)
                    {
                        string chave = Normalizar((inicio + d).ToString("X" + largura));
                        mapa[chave] = Unicode(destinos[d].Groups[1].Value);
                    }
                }
            }
        }
        return mapa;
    }

    private static IEnumerable<string> Blocos(string texto, string abre, string fecha)
    {
        int posicao = 0;
        while (true)
        {
            int inicio = texto.IndexOf(abre, posicao, StringComparison.Ordinal);
            if (inicio < 0)
            {
                yield break;
            }
            int fim = texto.IndexOf(fecha, inicio, StringComparison.Ordinal);
            if (fim < 0)
            {
                yield break;
            }
            yield return texto.Substring(inicio + abre.Length, fim - inicio - abre.Length);
            posicao = fim + fecha.Length;
        }
    }

    private static string Normalizar(string hex)
    {
        return hex.ToUpperInvariant();
    }

    // Destino do mapa: UTF-16 em hexadecimal (4 dígitos por unidade)
    private static string Unicode(string hex)
    {
        var texto = new StringBuilder();
        for (int i = 0; i + 4 <= hex.Length; i = i + 4)
        {
            int unidade = int.Parse(hex.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            texto.Append((char)unidade);
        }
        if (texto.Length == 0 && hex.Length == 2)
        {
            texto.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        return texto.ToString();
    }
}
