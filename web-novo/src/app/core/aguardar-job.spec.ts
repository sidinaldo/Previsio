import { Observable, of, throwError } from 'rxjs';

import { aguardarFimDoJob } from './aguardar-job';
import { SituacaoJob } from './previsio.models';

function consultaComRespostas(respostas: boolean[]): { consultar: () => Observable<SituacaoJob>; vezes: () => number } {
  let vezes = 0;
  return {
    consultar: () => {
      const emExecucao = respostas[Math.min(vezes, respostas.length - 1)];
      vezes++;
      return of({ nome: 'leitura-email', emExecucao: emExecucao });
    },
    vezes: () => vezes
  };
}

describe('aguardarFimDoJob', () => {
  it('emite quando o job sai de execução, sem perguntar de novo depois', async () => {
    const consulta = consultaComRespostas([true, true, false]);
    await new Promise<void>((resolver, rejeitar) => {
      aguardarFimDoJob(consulta.consultar, 5, 1000).subscribe({ next: () => resolver(), error: rejeitar });
    });
    expect(consulta.vezes()).toBe(3);
  });

  it('dá erro quando passa do limite de espera', async () => {
    const consulta = consultaComRespostas([true]);
    const resultado = await new Promise<string>(resolver => {
      aguardarFimDoJob(consulta.consultar, 5, 40).subscribe({ next: () => resolver('terminou'), error: () => resolver('desistiu') });
    });
    expect(resultado).toBe('desistiu');
  });

  it('dá erro quando a consulta falha (ex.: API antiga sem a rota)', async () => {
    const resultado = await new Promise<string>(resolver => {
      aguardarFimDoJob(() => throwError(() => new Error('404')), 5, 1000)
        .subscribe({ next: () => resolver('terminou'), error: () => resolver('falhou') });
    });
    expect(resultado).toBe('falhou');
  });
});
