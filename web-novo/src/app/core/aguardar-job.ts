import { Observable, exhaustMap, first, map, timeout, timer } from 'rxjs';

/** De quanto em quanto tempo a tela pergunta se o job terminou. */
export const INTERVALO_CONSULTA_JOB_MS = 2000;

/** Depois disso a tela desiste de esperar (o job continua rodando no servidor). */
export const LIMITE_ESPERA_JOB_MS = 3 * 60 * 1000;

/**
 * Pergunta de tempos em tempos se o job ainda está na fila ou rodando e emite uma vez quando ele termina.
 * Dá erro se passar do limite ou se a consulta falhar; quem chama decide o que mostrar.
 */
export function aguardarFimDoJob(
  // Situação de um job ou da sincronização da rede: só importa se ainda está rodando
  consultar: () => Observable<{ emExecucao: boolean }>,
  intervaloMs: number = INTERVALO_CONSULTA_JOB_MS,
  limiteMs: number = LIMITE_ESPERA_JOB_MS
): Observable<void> {
  return timer(intervaloMs, intervaloMs).pipe(
    // Uma consulta por vez: se a resposta demorar, a próxima espera
    exhaustMap(() => consultar()),
    first(situacao => !situacao.emExecucao),
    map(() => undefined),
    timeout({ first: limiteMs })
  );
}
