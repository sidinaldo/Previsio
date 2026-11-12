// Regras da lista "Notas para resolver" do painel

/** Como o primeiro vencimento da nota aparece: vermelho se já venceu ou vence dentro do período do painel. */
export type UrgenciaNota = 'vencida' | 'no-periodo' | 'depois' | 'sem-vencimento';

/** Datas vêm da API como "2026-11-12T00:00:00": compara só o dia. */
export function urgenciaDaNota(primeiroVencimento: string | null, inicio: string, fim: string): UrgenciaNota {
  if (!primeiroVencimento) {
    return 'sem-vencimento';
  }
  const dia = primeiroVencimento.slice(0, 10);
  if (dia < inicio.slice(0, 10)) {
    return 'vencida';
  }
  if (dia <= fim.slice(0, 10)) {
    return 'no-periodo';
  }
  return 'depois';
}
