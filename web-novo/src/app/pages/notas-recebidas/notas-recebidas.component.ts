import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { aguardarFimDoJob } from '../../core/aguardar-job';
import { classeDaSituacao, descricaoDaSituacao } from '../../core/filtro-notas';
import { totalDePaginas } from '../../core/filtro-falhas';
import { PrevisioService } from '../../core/previsio.service';
import { Empresa, FiltroNotas, NotaRecebida, SITUACAO_PENDENTES, SITUACOES_COM_PROBLEMA } from '../../core/previsio.models';
import { SessaoService } from '../../core/sessao.service';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { MenuAcoesComponent } from '../../shared/menu-acoes/menu-acoes.component';
import { ResolucaoManualComponent } from '../../shared/resolucao-manual/resolucao-manual.component';

/** Opção do filtro de situação. */
interface OpcaoSituacao {
  valor: string;
  texto: string;
}

/** Botão que disparou o carregamento da lista, para o spinner aparecer só nele. */
type AcaoLista = 'filtrar' | 'limpar' | 'anterior' | 'proxima' | 'atualizar' | null;

/**
 * Todas as notas lidas do e-mail, lançadas no Bling ou não, da mais recente para a mais antiga.
 * A linha abre o detalhe com as parcelas e a conta a pagar criada no Bling para cada uma.
 */
@Component({
  selector: 'app-notas-recebidas',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, MenuAcoesComponent, ResolucaoManualComponent, CarregandoDirective],
  templateUrl: './notas-recebidas.component.html'
})
export class NotasRecebidasComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly destroyRef = inject(DestroyRef);

  // Rodar a leitura de e-mails na hora é só do admin (o job passa por todos os clientes)
  readonly ehAdmin = inject(SessaoService).ehAdmin;

  readonly situacoes: OpcaoSituacao[] = [
    { valor: '', texto: 'Todas' },
    { valor: 'Lancada', texto: 'Lançadas no Bling' },
    { valor: 'LancadaManualmente', texto: 'Lançadas manualmente no Bling' },
    { valor: SITUACAO_PENDENTES, texto: 'Pendentes (todas as não lançadas)' },
    { valor: 'Recebida', texto: descricaoDaSituacao('Recebida') },
    { valor: 'Erro', texto: descricaoDaSituacao('Erro') },
    { valor: 'EmpresaNaoEncontrada', texto: descricaoDaSituacao('EmpresaNaoEncontrada') },
    { valor: 'BlingDesconectado', texto: descricaoDaSituacao('BlingDesconectado') },
    { valor: 'SemParcelas', texto: descricaoDaSituacao('SemParcelas') }
  ];

  readonly empresas = signal<Empresa[]>([]);

  // Filtros
  readonly empresaId = signal<number | null>(null);
  readonly situacao = signal('');
  readonly de = signal('');
  readonly ate = signal('');
  readonly pagina = signal(1);

  readonly notas = signal<NotaRecebida[]>([]);
  readonly total = signal(0);
  readonly tamanhoPagina = signal(50);
  readonly carregando = signal(false);
  readonly acaoLista = signal<AcaoLista>(null);
  readonly carregada = signal(false);
  readonly erro = signal('');
  readonly mensagem = signal('');
  // Leitura dos e-mails rodando: o botão fica desabilitado até ela terminar
  readonly buscandoEmails = signal(false);

  // Linha com o detalhe aberto
  readonly abertaId = signal<number | null>(null);

  // Nota com a confirmação de "lançada manualmente no Bling" aberta
  readonly resolvendoId = signal<number | null>(null);

  // Nota sendo enviada para um novo lançamento
  readonly reprocessandoId = signal<number | null>(null);

  readonly totalPaginas = computed(() => totalDePaginas(this.total(), this.tamanhoPagina()));

  constructor() {
    this.previsio.listarEmpresas().subscribe({
      next: empresas => this.empresas.set(empresas),
      error: () => this.empresas.set([])
    });
    this.carregar();
  }

  descricao(status: string): string {
    return descricaoDaSituacao(status);
  }

  classe(status: string): string {
    return classeDaSituacao(status);
  }

  estaLancada(nota: NotaRecebida): boolean {
    return nota.status === 'Lancada';
  }

  // Mesmas regras da tela de notas pendentes
  temFalhas(nota: NotaRecebida): boolean {
    return nota.status === 'Erro' || nota.status === 'BlingDesconectado';
  }

  /** Notas com problema podem ser marcadas como lançadas manualmente no Bling. */
  podeMarcarManual(nota: NotaRecebida): boolean {
    return SITUACOES_COM_PROBLEMA.includes(nota.status);
  }

  abrirResolucao(nota: NotaRecebida): void {
    this.resolvendoId.set(nota.id);
  }

  fecharResolucao(): void {
    this.resolvendoId.set(null);
  }

  aoResolver(nota: NotaRecebida): void {
    this.resolvendoId.set(null);
    this.erro.set('');
    this.mensagem.set(`NF ${nota.numero} marcada como lançada manualmente no Bling.`);
    this.carregar();
  }

  podeReprocessar(nota: NotaRecebida): boolean {
    return nota.status === 'Erro' || nota.status === 'BlingDesconectado' || nota.status === 'Recebida';
  }

  formatarCnpj(cnpj: string): string {
    if (!cnpj || cnpj.length !== 14) {
      return cnpj;
    }
    return cnpj.substring(0, 2) + '.' + cnpj.substring(2, 5) + '.' + cnpj.substring(5, 8) + '/'
      + cnpj.substring(8, 12) + '-' + cnpj.substring(12, 14);
  }

  filtrar(): void {
    this.acaoLista.set('filtrar');
    this.pagina.set(1);
    this.carregar();
  }

  limpar(): void {
    this.acaoLista.set('limpar');
    this.empresaId.set(null);
    this.situacao.set('');
    this.de.set('');
    this.ate.set('');
    this.pagina.set(1);
    this.carregar();
  }

  irParaPagina(pagina: number): void {
    if (pagina < 1 || pagina > this.totalPaginas()) {
      return;
    }
    if (pagina > this.pagina()) {
      this.acaoLista.set('proxima');
    } else {
      this.acaoLista.set('anterior');
    }
    this.pagina.set(pagina);
    this.carregar();
  }

  alternarDetalhes(nota: NotaRecebida): void {
    if (this.abertaId() === nota.id) {
      this.abertaId.set(null);
    } else {
      this.abertaId.set(nota.id);
    }
  }

  reprocessar(nota: NotaRecebida): void {
    this.erro.set('');
    this.reprocessandoId.set(nota.id);
    this.previsio.reprocessarNota(nota.id).subscribe({
      next: () => {
        this.reprocessandoId.set(null);
        this.mensagem.set(`NF ${nota.numero} enviada para lançamento.`);
        setTimeout(() => this.carregar(), 3000);
      },
      error: () => {
        this.reprocessandoId.set(null);
        this.erro.set(`Não foi possível reprocessar a NF ${nota.numero}.`);
      }
    });
  }


  /** Roda a leitura dos e-mails e atualiza a lista sozinha quando ela termina. */
  buscarEmailsAgora(): void {
    this.erro.set('');
    this.buscandoEmails.set(true);
    this.mensagem.set('Buscando os e-mails... a lista atualiza sozinha quando terminar.');
    this.previsio.executarJob('leitura-email').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.esperarLeitura(),
      error: () => {
        this.buscandoEmails.set(false);
        this.mensagem.set('');
        this.erro.set('Não foi possível iniciar a leitura dos e-mails.');
      }
    });
  }

  private esperarLeitura(): void {
    aguardarFimDoJob(() => this.previsio.situacaoJob('leitura-email'))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.buscandoEmails.set(false);
          this.mensagem.set('Leitura dos e-mails concluída e lista atualizada. As notas novas são lançadas no Bling em até 10 minutos.');
          this.carregar();
        },
        error: () => {
          // Demorou demais (ou a API não respondeu): mostra o que já chegou
          this.buscandoEmails.set(false);
          this.mensagem.set('A leitura dos e-mails ainda não terminou. Clique em Atualizar em alguns instantes.');
          this.carregar();
        }
      });
  }

  carregar(): void {
    if (this.de() && this.ate() && this.ate() < this.de()) {
      this.erro.set('A data final deve ser igual ou posterior à inicial.');
      return;
    }

    const filtro: FiltroNotas = {
      empresaId: this.empresaId(),
      situacao: this.situacao(),
      de: this.de(),
      ate: this.ate(),
      pagina: this.pagina()
    };

    this.carregando.set(true);
    this.erro.set('');
    this.abertaId.set(null);

    this.previsio.obterNotas(filtro).subscribe({
      next: resposta => {
        this.notas.set(resposta.itens);
        this.total.set(resposta.total);
        this.tamanhoPagina.set(resposta.tamanhoPagina);
        this.carregando.set(false);
        this.carregada.set(true);
      },
      error: () => {
        this.erro.set('Não foi possível carregar as notas.');
        this.carregando.set(false);
      }
    });
  }
}
