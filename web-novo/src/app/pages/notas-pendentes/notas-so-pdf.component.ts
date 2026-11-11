import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';

import { mensagemDoDownload, salvarArquivo } from '../../core/download';
import { formatarCnpj } from '../../core/implantacao';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { DESCRICAO_SITUACAO_SO_PDF, NotaSoPdf } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { MenuAcoesComponent } from '../../shared/menu-acoes/menu-acoes.component';

type Acao = 'xml' | 'chave' | 'lancada';

/**
 * Notas que chegaram por e-mail só em PDF, sem o XML. O Previsio tenta buscar o XML na SEFAZ sozinho;
 * aqui o financeiro vê o andamento e resolve as que travaram: anexar o XML, informar a chave,
 * pedir o XML ao fornecedor ou marcar que já lançou no Bling. Some quando não há nenhuma.
 */
@Component({
  selector: 'app-notas-so-pdf',
  imports: [DatePipe, CarregandoDirective, MenuAcoesComponent],
  templateUrl: './notas-so-pdf.component.html'
})
export class NotasSoPdfComponent {
  private readonly api = inject(PrevisioService);

  readonly empresaId = input<number | undefined>(undefined);

  readonly notas = signal<NotaSoPdf[]>([]);
  readonly mensagem = signal('');
  readonly erro = signal('');
  readonly abertaId = signal<number | null>(null);
  readonly acao = signal<Acao | null>(null);
  readonly ocupadoId = signal<number | null>(null);

  // Campos do formulário aberto embaixo da linha
  readonly chave = signal('');
  readonly observacao = signal('');
  readonly arquivoXml = signal<File | null>(null);

  readonly formatarCnpj = formatarCnpj;

  constructor() {
    effect(() => {
      const empresa = this.empresaId();
      untracked(() => this.carregar(empresa));
    });
  }

  atualizar(): void {
    this.carregar(this.empresaId());
  }

  descricao(nota: NotaSoPdf): string {
    return DESCRICAO_SITUACAO_SO_PDF[nota.situacao] || nota.situacao;
  }

  classe(nota: NotaSoPdf): string {
    if (nota.situacao === 'BuscandoNaSefaz') {
      return 'badge badge-info';
    }
    if (nota.situacao === 'NaoResolvida') {
      return 'badge badge-danger';
    }
    return 'badge badge-warning';
  }

  fornecedor(nota: NotaSoPdf): string {
    if (nota.nomeRemetente) {
      return nota.nomeRemetente;
    }
    return nota.remetente;
  }

  abrir(nota: NotaSoPdf, acao: Acao): void {
    this.limpar();
    this.abertaId.set(nota.id);
    this.acao.set(acao);
    this.chave.set('');
    this.observacao.set('');
    this.arquivoXml.set(null);
  }

  fechar(): void {
    this.abertaId.set(null);
    this.acao.set(null);
  }

  escolherArquivo(evento: Event): void {
    const campo = evento.target as HTMLInputElement;
    if (campo.files && campo.files.length > 0) {
      this.arquivoXml.set(campo.files[0]);
    } else {
      this.arquivoXml.set(null);
    }
  }

  mudarChave(evento: Event): void {
    this.chave.set((evento.target as HTMLInputElement).value);
  }

  mudarObservacao(evento: Event): void {
    this.observacao.set((evento.target as HTMLInputElement).value);
  }

  verPdf(nota: NotaSoPdf): void {
    this.limpar();
    this.api.baixarPdfNotaSoPdf(nota.id).subscribe({
      next: resposta => salvarArquivo(resposta, nota.pdfNome || 'nota.pdf'),
      error: async erro => this.erro.set(await mensagemDoDownload(erro, 'Não consegui baixar o PDF.'))
    });
  }

  async anexarXml(nota: NotaSoPdf): Promise<void> {
    const arquivo = this.arquivoXml();
    if (!arquivo) {
      this.erro.set('Escolha o arquivo .xml da nota.');
      return;
    }
    this.limpar();
    this.ocupadoId.set(nota.id);
    const xml = await arquivo.text();
    this.api.anexarXmlNotaSoPdf(nota.id, xml).subscribe({
      next: () => this.concluir('XML anexado. A nota entrou em Notas pendentes e vai para o Bling em até 10 minutos.'),
      error: erro => this.falhar(erro, 'Não consegui anexar o XML.')
    });
  }

  informarChave(nota: NotaSoPdf): void {
    this.limpar();
    this.ocupadoId.set(nota.id);
    this.api.informarChaveNotaSoPdf(nota.id, this.chave()).subscribe({
      next: () => this.concluir('Chave registrada. O Previsio vai buscar o XML na SEFAZ nos próximos minutos.'),
      error: erro => this.falhar(erro, 'Não consegui registrar a chave.')
    });
  }

  pedirXml(nota: NotaSoPdf): void {
    this.limpar();
    this.ocupadoId.set(nota.id);
    this.api.pedirXmlNotaSoPdf(nota.id).subscribe({
      next: () => this.concluir('Pedido enviado para ' + nota.remetente + '. A resposta com o XML chega na caixa das notas.'),
      error: erro => this.falhar(erro, 'Não consegui enviar o pedido ao fornecedor.')
    });
  }

  marcarLancada(nota: NotaSoPdf): void {
    this.limpar();
    this.ocupadoId.set(nota.id);
    this.api.marcarNotaSoPdfLancada(nota.id, this.observacao()).subscribe({
      next: () => this.concluir('Marcada como lançada no Bling.'),
      error: erro => this.falhar(erro, 'Não consegui marcar a nota.')
    });
  }

  private concluir(texto: string): void {
    this.ocupadoId.set(null);
    this.fechar();
    this.mensagem.set(texto);
    this.atualizar();
  }

  private falhar(erro: unknown, padrao: string): void {
    this.ocupadoId.set(null);
    this.erro.set(mensagemDaApi(erro, padrao));
  }

  private limpar(): void {
    this.mensagem.set('');
    this.erro.set('');
  }

  private carregar(empresaId: number | undefined): void {
    this.api.obterNotasSoPdf(empresaId).subscribe({
      next: notas => this.notas.set(notas),
      error: () => this.erro.set('Não consegui carregar as notas que chegaram só em PDF.')
    });
  }
}
