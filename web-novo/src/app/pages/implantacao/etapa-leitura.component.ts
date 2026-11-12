import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { forkJoin, switchMap, tap } from 'rxjs';

import { aguardarFimDoJob } from '../../core/aguardar-job';
import { Cliente, SITUACAO_PENDENTES } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../../shared/carregando.directive';

/** Em que ponto está a primeira leitura. */
export type FaseLeitura = 'parada' | 'lendo' | 'lancando' | 'pronta' | 'erro';

/** O que a primeira leitura trouxe para este cliente. */
export interface ResultadoLeitura {
  notasLidas: number;
  lancadas: number;
  pendentes: number;
  falhasHoje: number;
}

/**
 * Etapa 5: roda agora a leitura do e-mail e o lançamento no Bling (os mesmos jobs do
 * agendamento) e mostra quantas notas chegaram, quantas viraram conta a pagar e os erros.
 */
@Component({
  selector: 'app-etapa-leitura',
  imports: [RouterLink, CarregandoDirective],
  templateUrl: './etapa-leitura.component.html'
})
export class EtapaLeituraComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly destroyRef = inject(DestroyRef);

  readonly cliente = input.required<Cliente>();
  readonly alterou = output<void>();

  readonly fase = signal<FaseLeitura>('parada');
  readonly resultado = signal<ResultadoLeitura | null>(null);
  readonly erro = signal('');

  constructor() {
    // Voltando à implantação: mostra o que já foi lido
    this.contar();
  }

  executar(): void {
    this.erro.set('');
    this.fase.set('lendo');
    this.previsio.executarJob('leitura-email').pipe(
      switchMap(() => aguardarFimDoJob(() => this.previsio.situacaoJob('leitura-email'))),
      tap(() => this.fase.set('lancando')),
      switchMap(() => this.previsio.executarJob('lancamento')),
      switchMap(() => aguardarFimDoJob(() => this.previsio.situacaoJob('lancamento'))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => {
        this.fase.set('pronta');
        this.contar();
        this.alterou.emit();
      },
      error: () => {
        this.fase.set('erro');
        this.erro.set('A leitura não terminou a tempo ou a API não respondeu. Ela pode continuar rodando: confira as notas e as falhas daqui a pouco.');
      }
    });
  }

  rodando(): boolean {
    return this.fase() === 'lendo' || this.fase() === 'lancando';
  }

  private contar(): void {
    forkJoin({
      todas: this.previsio.obterNotas({ pagina: 1 }),
      lancadas: this.previsio.obterNotas({ situacao: 'Lancada', pagina: 1 }),
      pendentes: this.previsio.obterNotas({ situacao: SITUACAO_PENDENTES, pagina: 1 }),
      falhas: this.previsio.obterFalhas({ de: hojeIso(), pagina: 1 })
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: dados => {
        this.resultado.set({
          notasLidas: dados.todas.total,
          lancadas: dados.lancadas.total,
          pendentes: dados.pendentes.total,
          falhasHoje: dados.falhas.total
        });
      },
      error: () => this.resultado.set(null)
    });
  }
}

/** Data de hoje no fuso do navegador, yyyy-MM-dd. */
function hojeIso(): string {
  const hoje = new Date();
  const mes = String(hoje.getMonth() + 1).padStart(2, '0');
  const dia = String(hoje.getDate()).padStart(2, '0');
  return hoje.getFullYear() + '-' + mes + '-' + dia;
}
