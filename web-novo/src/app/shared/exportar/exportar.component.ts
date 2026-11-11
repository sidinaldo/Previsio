import { Component, input, signal } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

import { mensagemDoDownload, salvarArquivo } from '../../core/download';
import { CarregandoDirective } from '../carregando.directive';

/**
 * Botão "Exportar Excel": chama a API, salva a planilha e mostra o motivo se a API recusar.
 *
 *   <app-exportar [gerar]="exportar" nomePadrao="previsio-notas.xlsx" />
 */
@Component({
  selector: 'app-exportar',
  imports: [CarregandoDirective],
  host: { class: 'pv-exportar' },
  template: `
    <button type="button" class="btn btn-secundario" [pvCarregando]="exportando()" [disabled]="exportando() || desabilitado()"
            [title]="titulo()" (click)="exportar()">
      <i class="material-icons" aria-hidden="true">download</i> Exportar Excel
    </button>
    @if (erro()) {
      <span class="pv-exportar__erro" role="alert">{{ erro() }}</span>
    }
  `
})
export class ExportarComponent {
  /** Função que chama a API e devolve a planilha (com os filtros de quem usa). */
  readonly gerar = input.required<() => Observable<HttpResponse<Blob>>>();
  readonly nomePadrao = input('previsio.xlsx');
  readonly titulo = input('Baixar uma planilha do Excel com o que está na lista');
  readonly desabilitado = input(false);

  readonly exportando = signal(false);
  readonly erro = signal('');

  exportar(): void {
    this.exportando.set(true);
    this.erro.set('');
    this.gerar()().subscribe({
      next: resposta => {
        salvarArquivo(resposta, this.nomePadrao());
        this.exportando.set(false);
      },
      error: async erro => {
        this.erro.set(await mensagemDoDownload(erro, 'Não consegui gerar a planilha. Tente de novo.'));
        this.exportando.set(false);
      }
    });
  }
}
