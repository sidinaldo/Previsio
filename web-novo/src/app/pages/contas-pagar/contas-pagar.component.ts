import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, ParamMap } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { classeDaConta, filtroPadraoContas, lerFiltroContas, salvarFiltroContas, textoDaConta } from '../../core/filtro-contas';
import { totalDePaginas } from '../../core/filtro-falhas';
import { Ordenacao, alternarOrdenacao, parametroOrdem } from '../../core/ordenacao';
import { TAMANHOS_PAGINA_API, lerTamanhoPagina, salvarTamanhoPagina } from '../../core/paginacao';
import { PrevisioService } from '../../core/previsio.service';
import { ContaPagar, Empresa, FiltroContas } from '../../core/previsio.models';
import { SessaoService } from '../../core/sessao.service';
import { BuscaComponent } from '../../shared/busca/busca.component';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { ColunaOrdenavelComponent } from '../../shared/coluna-ordenavel/coluna-ordenavel.component';
import { ExportarComponent } from '../../shared/exportar/exportar.component';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';
import { SincronizarBlingComponent } from '../../shared/sincronizar-bling/sincronizar-bling.component';

const DATA_ISO = /^\d{4}-\d{2}-\d{2}$/;

/** Opção de um select de filtro. */
interface Opcao {
  valor: string;
  texto: string;
}

/** Botão que disparou o carregamento da lista, para o spinner aparecer só nele. */
type AcaoLista = 'filtrar' | 'limpar' | 'atualizar' | 'pagina' | null;

/**
 * Tudo o que a sincronização trouxe do Bling: as contas lançadas pelo Previsio e as lançadas à mão,
 * com fornecedor, situação e origem. O último filtro usado fica guardado neste navegador.
 */
@Component({
  selector: 'app-contas-pagar',
  imports: [FormsModule, CurrencyPipe, DatePipe, CarregandoDirective, PaginacaoComponent, BuscaComponent, ColunaOrdenavelComponent, ExportarComponent, SincronizarBlingComponent],
  templateUrl: './contas-pagar.component.html'
})
export class ContasPagarComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly sessao = inject(SessaoService);

  readonly situacoes: Opcao[] = [
    { valor: 'Abertas', texto: 'Em aberto (inclui vencidas)' },
    { valor: 'Vencidas', texto: 'Só as vencidas' },
    { valor: 'Pagas', texto: 'Pagas' },
    { valor: 'Canceladas', texto: 'Canceladas ou devolvidas' },
    { valor: 'Todas', texto: 'Todas' }
  ];

  readonly origens: Opcao[] = [
    { valor: '', texto: 'Todas' },
    { valor: 'Previsio', texto: 'Notas lidas pelo Previsio' },
    { valor: 'Bling', texto: 'Lançadas direto no Bling' }
  ];

  readonly empresas = signal<Empresa[]>([]);

  // Filtros
  readonly empresaId = signal<number | null>(null);
  readonly situacao = signal('Abertas');
  readonly origem = signal('');
  readonly de = signal('');
  readonly ate = signal('');
  readonly busca = signal('');
  // Vem do card "Saídas por categoria" do painel; não fica guardado no navegador
  readonly categoria = signal('');
  readonly pagina = signal(1);
  readonly ordenacao = signal<Ordenacao | null>({ coluna: 'vencimento', decrescente: false });

  readonly contas = signal<ContaPagar[]>([]);
  readonly total = signal(0);
  readonly valorTotal = signal(0);
  readonly tamanhoPagina = signal(lerTamanhoPagina('contas-pagar'));
  readonly tamanhosPagina = TAMANHOS_PAGINA_API;
  readonly sincronizadaEm = signal<string | null>(null);
  readonly diasPassados = signal(30);
  readonly diasFuturos = signal(90);
  readonly carregando = signal(false);
  readonly acaoLista = signal<AcaoLista>(null);
  readonly carregada = signal(false);
  readonly erro = signal('');
  // Andamento, fim ou recusa do "Sincronizar com o Bling"
  readonly avisoSincronizacao = signal<{ tipo: 'info' | 'sucesso' | 'erro'; texto: string } | null>(null);

  readonly totalPaginas = computed(() => totalDePaginas(this.total(), this.tamanhoPagina()));

  // Filtros da lista mostrada: a exportação usa estes, não o que ainda está sendo digitado
  private filtroAplicado: FiltroContas = {};
  readonly exportar = () => this.previsio.exportarContasFiltradas(this.filtroAplicado);

  constructor() {
    this.aplicar(lerFiltroContas(this.sessao.clienteId(), new Date()));
    this.aplicarDoPainel(inject(ActivatedRoute).snapshot.queryParamMap);
    this.previsio.listarEmpresas().subscribe({
      next: empresas => this.empresas.set(empresas),
      error: () => this.empresas.set([])
    });
    this.carregar();
  }

  classe(conta: ContaPagar): string {
    return classeDaConta(conta);
  }

  texto(conta: ContaPagar): string {
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
    this.aplicar(filtroPadraoContas(new Date()));
    this.busca.set('');
    this.categoria.set('');
    this.pagina.set(1);
    this.carregar();
  }

  tirarCategoria(): void {
    this.categoria.set('');
    this.acaoLista.set('filtrar');
    this.pagina.set(1);
    this.carregar();
  }

  ordenarPor(coluna: string): void {
    this.ordenacao.set(alternarOrdenacao(this.ordenacao(), coluna));
    this.pagina.set(1);
    this.carregar();
  }

  /** "Por página": guarda a escolha neste navegador e volta para a primeira página. */
  trocarTamanho(tamanho: number): void {
    this.tamanhoPagina.set(tamanho);
    salvarTamanhoPagina('contas-pagar', tamanho);
    this.acaoLista.set('pagina');
    this.pagina.set(1);
    this.carregar();
  }

  irParaPagina(pagina: number): void {
    // Mesma página: acontece logo depois de trocar o "Por página" (que já recarregou)
    if (pagina < 1 || pagina > this.totalPaginas() || pagina === this.pagina()) {
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
      origem: this.origem(),
      de: this.de(),
      ate: this.ate(),
      busca: this.busca(),
      categoria: this.categoria(),
      ordem: parametroOrdem(this.ordenacao()),
      pagina: this.pagina(),
      tamanho: this.tamanhoPagina()
    };
    this.filtroAplicado = filtro;
    salvarFiltroContas(this.sessao.clienteId(), filtro, new Date());

    this.carregando.set(true);
    this.erro.set('');
    this.previsio.obterContasPagar(filtro).subscribe({
      next: resposta => {
        this.contas.set(resposta.itens);
        this.total.set(resposta.total);
        this.valorTotal.set(resposta.valorTotal);
        this.tamanhoPagina.set(resposta.tamanhoPagina);
        this.sincronizadaEm.set(resposta.sincronizadaEm);
        this.diasPassados.set(resposta.diasPassados);
        this.diasFuturos.set(resposta.diasFuturos);
        this.carregando.set(false);
        this.carregada.set(true);
      },
      error: () => {
        this.erro.set('Não foi possível carregar as contas a pagar.');
        this.carregando.set(false);
      }
    });
  }

  /** Do painel: as contas em aberto da categoria, no período do painel. */
  private aplicarDoPainel(parametros: ParamMap): void {
    const categoria = (parametros.get('categoria') ?? '').trim();
    if (categoria === '') {
      return;
    }
    this.categoria.set(categoria);
    this.situacao.set('Abertas');
    this.empresaId.set(null);
    this.origem.set('');
    const de = parametros.get('de') ?? '';
    const ate = parametros.get('ate') ?? '';
    if (DATA_ISO.test(de) && DATA_ISO.test(ate)) {
      this.de.set(de);
      this.ate.set(ate);
    }
  }

  private aplicar(filtro: FiltroContas): void {
    this.empresaId.set(filtro.empresaId ?? null);
    this.situacao.set(filtro.situacao || 'Abertas');
    this.origem.set(filtro.origem ?? '');
    this.de.set(filtro.de ?? '');
    this.ate.set(filtro.ate ?? '');
  }
}
