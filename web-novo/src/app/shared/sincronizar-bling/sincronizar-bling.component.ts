import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { aguardarFimDoJob } from '../../core/aguardar-job';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { PrevisioService } from '../../core/previsio.service';

/**
 * Botão "Sincronizar com o Bling" das telas Contas a pagar e Contas a receber: traz as contas
 * só das empresas da rede, espera terminar e avisa a tela para recarregar a lista.
 * A API recusa se a rede sincronizou há menos de 2 minutos; a mensagem dela aparece embaixo.
 */
@Component({
  selector: 'app-sincronizar-bling',
  template: `
    <button type="button" class="btn btn-secundario" (click)="sincronizar()" [disabled]="sincronizando()">
      @if (sincronizando()) {
        <span class="pv-spinner" aria-hidden="true"></span> Sincronizando...
      } @else {
        <i class="material-icons" aria-hidden="true">sync</i> Sincronizar com o Bling
      }
    </button>
  `
})
export class SincronizarBlingComponent {
  private readonly api = inject(PrevisioService);
  private readonly destroyRef = inject(DestroyRef);

  /** Terminou: a tela recarrega a lista. */
  readonly sincronizado = output<void>();
  /** Texto para a tela mostrar (andamento, fim ou motivo da recusa). */
  readonly aviso = output<{ tipo: 'info' | 'sucesso' | 'erro'; texto: string }>();

  readonly sincronizando = signal(false);

  sincronizar(): void {
    this.sincronizando.set(true);
    this.aviso.emit({ tipo: 'info', texto: 'Buscando as contas no Bling... a lista atualiza sozinha no fim.' });
    this.api.sincronizarRede().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.esperar(),
      error: erro => {
        this.sincronizando.set(false);
        // 409: sincronizou há pouco. Não é erro, só precisa esperar
        let tipo: 'info' | 'erro' = 'erro';
        if (erro instanceof HttpErrorResponse && erro.status === 409) {
          tipo = 'info';
        }
        this.aviso.emit({ tipo: tipo, texto: mensagemDaApi(erro, 'Não foi possível iniciar a sincronização.') });
      }
    });
  }

  private esperar(): void {
    aguardarFimDoJob(() => this.api.situacaoSincronizacao())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.sincronizando.set(false);
          this.aviso.emit({ tipo: 'sucesso', texto: 'Contas atualizadas com o Bling.' });
          this.sincronizado.emit();
        },
        error: () => {
          this.sincronizando.set(false);
          this.aviso.emit({ tipo: 'info', texto: 'A busca no Bling ainda não terminou. Clique em Atualizar em alguns minutos.' });
          this.sincronizado.emit();
        }
      });
  }
}
