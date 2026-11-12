import { Component, computed, input, model } from '@angular/core';

import { totalDePaginas } from '../../core/filtro-falhas';
import { TAMANHOS_PAGINA, TAMANHO_PAGINA_PADRAO, paginaValida } from '../../core/paginacao';

let proximoId = 1;

/**
 * Paginação embaixo das tabelas que recebem a lista inteira da API:
 *
 *   @for (empresa of empresasDaPagina(); track empresa.id) { ... }
 *   <app-paginacao [total]="empresas().length" [(pagina)]="pagina" [(tamanho)]="tamanhoPagina" />
 *
 * Só aparece quando há mais itens do que cabem na menor página.
 * Com [tamanhos]="[]" some o "Por página" (cartões estreitos).
 */
@Component({
  selector: 'app-paginacao',
  host: {
    '[class.pv-paginacao]': 'visivel()'
  },
  template: `
    @if (visivel()) {
      <span class="pv-paginacao__texto" aria-live="polite">Mostrando {{ inicio() }}–{{ fim() }} de {{ total() }}</span>
      <div class="pv-paginacao__controles">
        @if (tamanhos().length > 0) {
          <div class="pv-paginacao__grupo">
            <label class="pv-paginacao__texto" [for]="idTamanho">Por página</label>
            <!-- bmd-form-group: impede o Bootstrap Material Design de embrulhar o select -->
            <span class="pv-campo-grupo bmd-form-group">
              <select class="pv-campo pv-campo--compacto" [id]="idTamanho" (change)="trocarTamanho($event)">
                @for (opcao of tamanhos(); track opcao) {
                  <option [value]="opcao" [selected]="opcao === tamanho()">{{ opcao }}</option>
                }
              </select>
            </span>
          </div>
        }
        <!-- Botões e "Página X de Y" ficam juntos: no celular quebram de linha como um bloco só -->
        <nav class="pv-paginacao__grupo" aria-label="Paginação">
        <button type="button" class="pv-botao-icone" aria-label="Primeira página" title="Primeira página"
                [disabled]="paginaAtual() <= 1" (click)="irPara(1)">
          <i class="material-icons" aria-hidden="true">first_page</i>
        </button>
        <button type="button" class="pv-botao-icone" aria-label="Página anterior" title="Página anterior"
                [disabled]="paginaAtual() <= 1" (click)="irPara(paginaAtual() - 1)">
          <i class="material-icons" aria-hidden="true">chevron_left</i>
        </button>
        <span class="pv-paginacao__texto">Página {{ paginaAtual() }} de {{ totalPaginas() }}</span>
        <button type="button" class="pv-botao-icone" aria-label="Próxima página" title="Próxima página"
                [disabled]="paginaAtual() >= totalPaginas()" (click)="irPara(paginaAtual() + 1)">
          <i class="material-icons" aria-hidden="true">chevron_right</i>
        </button>
        <button type="button" class="pv-botao-icone" aria-label="Última página" title="Última página"
                [disabled]="paginaAtual() >= totalPaginas()" (click)="irPara(totalPaginas())">
          <i class="material-icons" aria-hidden="true">last_page</i>
        </button>
        </nav>
      </div>
    }
  `
})
export class PaginacaoComponent {
  readonly total = input.required<number>();
  readonly pagina = model(1);
  readonly tamanho = model(TAMANHO_PAGINA_PADRAO);
  readonly tamanhos = input<number[]>(TAMANHOS_PAGINA);

  readonly idTamanho = 'pv-por-pagina-' + proximoId++;

  readonly totalPaginas = computed(() => totalDePaginas(this.total(), this.tamanho()));
  readonly paginaAtual = computed(() => paginaValida(this.pagina(), this.total(), this.tamanho()));

  readonly inicio = computed(() => {
    if (this.total() === 0) {
      return 0;
    }
    return (this.paginaAtual() - 1) * this.tamanho() + 1;
  });

  readonly fim = computed(() => Math.min(this.paginaAtual() * this.tamanho(), this.total()));

  // Escondida enquanto tudo cabe na menor página
  readonly visivel = computed(() => {
    const opcoes = this.tamanhos();
    let menor = this.tamanho();
    for (const opcao of opcoes) {
      if (opcao < menor) {
        menor = opcao;
      }
    }
    return this.total() > menor;
  });

  irPara(pagina: number): void {
    this.pagina.set(paginaValida(pagina, this.total(), this.tamanho()));
  }

  trocarTamanho(evento: Event): void {
    const valor = Number((evento.target as HTMLSelectElement).value);
    // Algumas telas disparam "change" em todos os selects (campos-preenchidos.ts) sem o valor mudar
    if (valor === this.tamanho()) {
      return;
    }
    this.tamanho.set(valor);
    // Volta para a primeira página: a posição antiga não corresponde mais aos mesmos itens
    this.pagina.set(1);
  }
}
