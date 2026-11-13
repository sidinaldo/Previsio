import { Component, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { classeDaConta, filtroPadraoContas, textoDaConta } from '../../core/filtro-contas';
import { totalDePaginas } from '../../core/filtro-falhas';
import { Ordenacao, alternarOrdenacao, parametroOrdem } from '../../core/ordenacao';
import { PrevisioService } from '../../core/previsio.service';
import { ContaReceber, Empresa, FiltroContas } from '../../core/previsio.models';
import { BuscaComponent } from '../../shared/busca/busca.component';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { ColunaOrdenavelComponent } from '../../shared/coluna-ordenavel/coluna-ordenavel.component';
import { ExportarComponent } from '../../shared/exportar/exportar.component';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';
import { SincronizarBlingComponent } from '../../shared/sincronizar-bling/sincronizar-bling.component';

/** Opção de um select de filtro. */
interface Opcao {
  valor: string;
  texto: string;
}

/** Botão que disparou o carregamento da lista, para o spinner aparecer só nele. */
type AcaoLista = 'filtrar' | 'limpar' | 'atualizar' | 'pagina' | null;

/** Situações aceitas pelo link do painel (?situacao=Vencidas). */
const SITUACOES_VALIDAS = ['Abertas', 'Vencidas', 'Recebidas', 'Canceladas', 'Todas'];

/**
 * Contas a receber que a sincronização trouxe do Bling (fluxo de caixa): cliente, empresa,
 * vencimento e situação. Abre com as em aberto que vencem até daqui a 30 dias.
 */
@Component({
  selector: 'app-contas-receber',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, CarregandoDirective, PaginacaoComponent, BuscaComponent,
    ColunaOrdenavelComponent, ExportarComponent, SincronizarBlingComponent],
  templateUrl: './contas-receber.component.html'
})
export class ContasReceberComponent {
  private readonly previsio = inject(PrevisioService);

  readonly situacoes: Opcao[] = [
    { valor: 'Abertas', texto: 'Em aberto (inclui vencidas)' },
    { valor: 'Vencidas', texto: 'Só as vencidas' },
    { valor: 'Recebidas', texto: 'Recebidas' },
    { valor: 'Canceladas', texto: 'Canceladas ou devolvidas' },
    { valor: 'Todas', texto: 'Todas' }
  ];

  readonly empresas = signal<Empresa[]>([]);

  // Filtros
  readonly empresaId = signal<number | null>(null);
  readonly situacao = signal('Abertas');
  readonly de = signal('');
  readonly ate = signal('');
  readonly busca = signal('');
  readonly pagina = signal(1);
  readonly ordenacao = signal<Ordenacao | null>({ coluna: 'vencimento', decrescente: false });

  readonly contas = signal<ContaReceber[]>([]);
  readonly total = signal(0);
  readonly valorTotal = signal(0);
  readonly tamanhoPagina = signal(50);
  readonly sincronizadaEm = signal<string | null>(null);
  readonly diasPassados = signal(30);
  readonly diasFuturos = signal(90);
  readonly fluxoCaixaAtivo = signal(true);
  readonly carregando = signal(false);
  readonly acaoLista = signal<AcaoLista>(null);
  readonly carregada = signal(false);
  readonly erro = signal('');
  // Andamento, fim ou recusa do "Sincronizar com o Bling"
  readonly avisoSincronizacao = signal<{ tipo: 'info' | 'sucesso' | 'erro'; texto: string } | null>(null);

  readonly totalPaginas = computed(() => totalDePaginas(this.total(), this.tamanhoPagina()));

  // Filtros da lista mostrada: a exportação usa estes, não o que ainda está sendo digitado
  private filtroAplicado: FiltroContas = {};
  readonly exportar = () => this.previsio.exportarContasReceber(this.filtroAplicado);

  constructor() {
    this.aplicarPadrao();
    // Do painel: "A receber vencido" abre direto nas vencidas, sem limite de data
    const situacao = inject(ActivatedRoute).snapshot.queryParamMap.get('situacao');
    if (situacao && SITUACOES_VALIDAS.includes(situacao)) {
      this.situacao.set(situacao);
      if (situacao === 'Vencidas') {
        this.ate.set('');
      }
    }
    this.previsio.listarEmpresas().subscribe({
      next: empresas => this.empresas.set(empresas),
      error: () => this.empresas.set([])
    });
    this.carregar();
  }

  classe(conta: ContaReceber): string {
    return classeDaConta(conta);
  }

  texto(conta: ContaReceber): string {
    return textoDaConta(conta);
  }

  filtrar(): void {
    this.acaoLista.set('filtrar');
    this.pagina.set(1);
    this.carregar();
  }

  /** Volta para o filtro padrão: em aberto, vencendo até daqui a 30 dias. */
  limpar(): void {
    this.acaoLista.set('limpar');
    this.aplicarPadrao();
    this.busca.set('');
    this.pagina.set(1);
    this.carregar();
  }

  ordenarPor(coluna: string): void {
    this.ordenacao.set(alternarOrdenacao(this.ordenacao(), coluna));
    this.pagina.set(1);
    this.carregar();
  }

  irParaPagina(pagina: number): void {
    if (pagina < 1 || pagina > this.totalPaginas()) {
      return;
    }
    this.acaoLista.set('pagina');
    this.pagina.set(pagina);
    this.carregar();
  }

  carregar(): void {
    if (this.de() && this.ate() && this.ate() < this.de()) {
      this.erro.set('A data final deve ser igual ou posterior à inicial.');
      return;
    }

    const filtro: FiltroContas = {
      empresaId: this.empresaId(),
      situacao: this.situacao(),
      de: this.de(),
      ate: this.ate(),
      busca: this.busca(),
      ordem: parametroOrdem(this.ordenacao()),
      pagina: this.pagina()
    };
    this.filtroAplicado = filtro;

    this.carregando.set(true);
    this.erro.set('');
    this.previsio.obterContasReceber(filtro).subscribe({
      next: resposta => {
        this.contas.set(resposta.itens);
        this.total.set(resposta.total);
        this.valorTotal.set(resposta.valorTotal);
        this.tamanhoPagina.set(resposta.tamanhoPagina);
        this.sincronizadaEm.set(resposta.sincronizadaEm);
        this.diasPassados.set(resposta.diasPassados);
        this.diasFuturos.set(resposta.diasFuturos);
        this.fluxoCaixaAtivo.set(resposta.fluxoCaixaAtivo);
        this.carregando.set(false);
        this.carregada.set(true);
      },
      error: () => {
        this.erro.set('Não foi possível carregar as contas a receber.');
        this.carregando.set(false);
      }
    });
  }

  private aplicarPadrao(): void {
    const padrao = filtroPadraoContas(new Date());
    this.empresaId.set(null);
    this.situacao.set('Abertas');
    this.de.set(padrao.de ?? '');
    this.ate.set(padrao.ate ?? '');
  }
}
