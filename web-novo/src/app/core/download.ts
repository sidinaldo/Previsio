import { HttpErrorResponse, HttpResponse } from '@angular/common/http';

/**
 * Nome do arquivo que a API mandou no Content-Disposition (filename*=UTF-8''... ou filename=...).
 */
export function nomeDoArquivo(resposta: HttpResponse<Blob>, nomePadrao: string): string {
  const cabecalho = resposta.headers.get('Content-Disposition');
  if (!cabecalho) {
    return nomePadrao;
  }
  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(cabecalho);
  if (utf8) {
    try {
      return decodeURIComponent(utf8[1].trim());
    } catch {
      return nomePadrao;
    }
  }
  const simples = /filename="?([^";]+)"?/i.exec(cabecalho);
  if (simples) {
    return simples[1].trim();
  }
  return nomePadrao;
}

/** Entrega o arquivo baixado da API para o navegador salvar. */
export function salvarArquivo(resposta: HttpResponse<Blob>, nomePadrao: string): void {
  if (!resposta.body) {
    return;
  }
  const url = URL.createObjectURL(resposta.body);
  const link = document.createElement('a');
  link.href = url;
  link.download = nomeDoArquivo(resposta, nomePadrao);
  link.style.display = 'none';
  document.body.appendChild(link);
  link.click();
  link.remove();
  // Dá tempo ao navegador de começar o download antes de liberar a memória
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/**
 * Num download, a mensagem de erro da API chega como Blob: lê o texto para mostrar ao usuário.
 */
export async function mensagemDoDownload(erro: unknown, padrao: string): Promise<string> {
  if (erro instanceof HttpErrorResponse && erro.error instanceof Blob) {
    try {
      const texto = (await erro.error.text()).trim();
      if (texto !== '' && texto.length < 500 && !texto.startsWith('{') && !texto.startsWith('<')) {
        return texto;
      }
    } catch {
      return padrao;
    }
  }
  if (erro instanceof HttpErrorResponse && typeof erro.error === 'string' && erro.error.trim() !== '') {
    return erro.error;
  }
  return padrao;
}
