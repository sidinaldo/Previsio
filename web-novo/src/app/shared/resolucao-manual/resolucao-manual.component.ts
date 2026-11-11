import { Component, ElementRef, afterNextRender, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { mensagemDaApi } from '../../core/mensagem-erro';
import { PrevisioService } from '../../core/previsio.service';

/** Tamanho máximo da observação (o mesmo da API). */
export const TAMANHO_MAXIMO_OBSERVACAO = 500;

/**
 * Confirmação de "já resolvi e lancei esta nota manualmente no Bling", com observação opcional.
 * Fica numa linha aberta embaixo da nota, nas telas de notas pendentes e recebidas.
 */
@Component({
  selector: 'app-resolucao-manual',
  imports: [FormsModule],
  template: `
    <form class="pv-resolucao" (ngSubmit)="confirmar()">
      <p class="pv-resolucao__titulo">
        <i class="material-icons" aria-hidden="true">task_alt</i>
        Marcar a NF {{ numero() }} como lançada manualmente no Bling?
      </p>
      <p class="pv-formulario__ajuda">
        A nota sai das pendentes e do painel, e o Previsio não tenta mais lançá-la.
        Use quando a conta a pagar já foi criada direto no Bling.
      </p>
      <div class="pv-resolucao__linha">
        <div class="pv-resolucao__campo">
          <label [for]="'observacao-' + notaId()" class="sr-only">Observação (opcional)</label>
          <input #campo [id]="'observacao-' + notaId()" type="text" class="form-control" name="observacao"
                 placeholder="Observação (opcional), ex.: lançada como conta 1234"
                 [maxlength]="tamanhoMaximo" [(ngModel)]="observacao">
        </div>
        <button type="submit" class="btn btn-info btn-sm" [disabled]="enviando()">
          @if (enviando()) {
            <span class="pv-spinner" aria-hidden="true"></span> Salvando...
          } @else {
            Confirmar
          }
        </button>
        <button type="button" class="btn btn-secundario btn-sm" [disabled]="enviando()" (click)="cancelado.emit()">Cancelar</button>
      </div>
      @if (erro()) {
        <p class="text-danger mb-0" role="alert">{{ erro() }}</p>
      }
    </form>
  `
})
export class ResolucaoManualComponent {
  private readonly previsio = inject(PrevisioService);

  readonly notaId = input.required<number>();
  readonly numero = input.required<string>();

  /** Avisa quem usa que a nota foi marcada (para recarregar a lista). */
  readonly concluido = output<void>();
  readonly cancelado = output<void>();

  readonly tamanhoMaximo = TAMANHO_MAXIMO_OBSERVACAO;
  readonly observacao = signal('');
  readonly enviando = signal(false);
  readonly erro = signal('');

  private readonly campo = viewChild<ElementRef<HTMLInputElement>>('campo');

  constructor() {
    afterNextRender(() => {
      const campo = this.campo();
      if (campo) {
        campo.nativeElement.focus();
      }
    });
  }

  confirmar(): void {
    this.erro.set('');
    this.enviando.set(true);
    this.previsio.marcarLancadaManualmente(this.notaId(), this.observacao().trim()).subscribe({
      next: () => {
        this.enviando.set(false);
        this.concluido.emit();
      },
      error: resposta => {
        this.enviando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível marcar a nota.'));
      }
    });
  }
}
