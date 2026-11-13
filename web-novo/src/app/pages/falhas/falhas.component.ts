import { Component, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { mensagemDaApi } from '../../core/mensagem-erro';
import { TAMANHOS_PAGINA_API, lerTamanhoPagina, salvarTamanhoPagina } from '../../core/paginacao';
import { PrevisioService } from '../../core/previsio.service';
import { DESCRICAO_ETAPA, Empresa, Falha, FiltroFalhas } from '../../core/previsio.models';
import { totalDePaginas } from '../../core/filtro-falhas';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';
import { BuscaComponent } from '../../shared/busca/busca.component';
import { ColunaOrdenavelComponent } from '../../shared/coluna-ordenavel/coluna-ordenavel.component';
import { Ordenacao, alternarOrdenacao, parametroOrdem } from '../../core/ordenacao';

/** Botão que disparou o carregamento da lista, para o spinner aparecer só nele. */
type AcaoLista = 'filtrar' | 'limpar' | 'anterior' | 'proxima' | 'atualizar' | null;

/**
 * Histórico das falhas: por que falhou, qual endereço tentou, qual nota, empresa e valor.
 * Abre filtrado por nota ou empresa quando vem de outra tela (?notaId= / ?empresaId=).
 */
@Component({
  selector: 'app-falhas',
  imports: [FormsModule, CurrencyPipe, DatePipe, SlicePipe, CarregandoDirective, PaginacaoComponent, BuscaComponent, ColunaOrdenavelComponent],
  templateUrl: './falhas.component.html'
})
export class FalhasComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly etapas = Object.keys(DESCRICAO_ETAPA);
  readonly empresas = signal<Empresa[]>([]);

  // Filtros
  readonly empresaId = signal<number | null>(null);
  readonly notaId = signal<number | null>(null);
  readonly etapa = signal('');
  readonly de = signal('');
  readonly ate = signal('');
  readonly busca = signal('');
  readonly pagina = signal(1);
  // Começa como a API ordena sem escolha: da falha mais recente para a mais antiga
  readonly ordenacao = signal<Ordenacao | null>({ coluna: 'data', decrescente: true });

  readonly falhas = signal<Falha[]>([]);
  readonly total = signal(0);
  readonly tamanhoPagina = signal(lerTamanhoPagina('falhas'));
  readonly tamanhosPagina = TAMANHOS_PAGINA_API;
  readonly carregando = signal(false);
  readonly acaoLista = signal<AcaoLista>(null);
  readonly erro = signal('');

  // Linha com os detalhes abertos
  readonly abertaId = signal<number | null>(null);

  readonly totalPaginas = computed(() => totalDePaginas(this.total(), this.tamanhoPagina()));

  // Número da nota filtrada, para o aviso no topo
  readonly notaFiltrada = computed(() => {
    const notaId = this.notaId();
    if (notaId === null) {
      return null;
    }
    const comNumero = this.falhas().find(f => f.notaFiscalId === notaId && f.numeroNota);
    if (comNumero) {
      return comNumero.numeroNota;
    }
    return String(notaId);
  });

  constructor() {
    const parametros = this.rota.snapshot.queryParamMap;
    this.notaId.set(lerNumero(parametros.get('notaId')));
    this.empresaId.set(lerNumero(parametros.get('empresaId')));

    this.previsio.listarEmpresas().subscribe({
      next: empresas => this.empresas.set(empresas),
      error: () => this.empresas.set([])
    });

    this.carregar();
  }

  descricaoEtapa(etapa: string): string {
    const texto = DESCRICAO_ETAPA[etapa];
    if (texto) {
      return texto;
    }
    return etapa;
  }

  filtrar(): void {
    this.acaoLista.set('filtrar');
    this.pagina.set(1);
    this.carregar();
  }

  limpar(): void {
    this.acaoLista.set('limpar');
    this.empresaId.set(null);
    this.notaId.set(null);
    this.etapa.set('');
    this.de.set('');
    this.ate.set('');
    this.busca.set('');
    this.pagina.set(1);
    // Tira ?notaId= / ?empresaId= da URL para o filtro não voltar ao atualizar a página
    this.router.navigate([], { relativeTo: this.rota, queryParams: {} });
    this.carregar();
  }

  /** Clique no cabeçalho: ordena pela coluna (a API ordena todas as páginas) e volta para a primeira. */
  ordenarPor(coluna: string): void {
    this.ordenacao.set(alternarOrdenacao(this.ordenacao(), coluna));
    this.pagina.set(1);
    this.carregar();
  }

  /** "Por página": guarda a escolha neste navegador e volta para a primeira página. */
  trocarTamanho(tamanho: number): void {
    this.tamanhoPagina.set(tamanho);
    salvarTamanhoPagina('falhas', tamanho);
    this.pagina.set(1);
    this.carregar();
  }

  irParaPagina(pagina: number): void {
    // Mesma página: acontece logo depois de trocar o "Por página" (que já recarregou)
    if (pagina < 1 || pagina > this.totalPaginas() || pagina === this.pagina()) {
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

  alternarDetalhes(falha: Falha): void {
    if (this.abertaId() === falha.id) {
      this.abertaId.set(null);
    } else {
      this.abertaId.set(falha.id);
    }
  }

  carregar(): void {
    if (this.de() && this.ate() && this.ate() < this.de()) {
      this.erro.set('A data final deve ser igual ou posterior à inicial.');
      return;
    }

    const filtro: FiltroFalhas = {
      empresaId: this.empresaId(),
      notaId: this.notaId(),
      etapa: this.etapa(),
      de: this.de(),
      ate: this.ate(),
      busca: this.busca(),
      ordem: parametroOrdem(this.ordenacao()),
      pagina: this.pagina(),
      tamanho: this.tamanhoPagina()
    };

    this.carregando.set(true);
    this.erro.set('');
    this.abertaId.set(null);

    this.previsio.obterFalhas(filtro).subscribe({
      next: resposta => {
        this.falhas.set(resposta.itens);
        this.total.set(resposta.total);
        this.tamanhoPagina.set(resposta.tamanhoPagina);
        this.carregando.set(false);
      },
      error: erro => {
        this.erro.set(mensagemDaApi(erro, 'Não foi possível carregar as falhas.'));
        this.carregando.set(false);
      }
    });
  }
}

function lerNumero(texto: string | null): number | null {
  if (!texto) {
    return null;
  }
  const numero = Number(texto);
  if (Number.isInteger(numero) && numero > 0) {
    return numero;
  }
  return null;
}
