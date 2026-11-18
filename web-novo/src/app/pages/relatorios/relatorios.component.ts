import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { dataIso } from '../../core/filtro-contas';
import { formatarCnpj } from '../../core/implantacao';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { Dossie, IndicadorConformidade, SemanaProjecao } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { ExportarComponent } from '../../shared/exportar/exportar.component';
import { SimboloComponent } from '../../shared/simbolo/simbolo.component';
import { ErpService } from '../../core/erp.service';

/** Uma semana desenhada no gráfico da projeção (coordenadas no viewBox). */
interface BarraSemana {
  x: number;
  largura: number;
  yEntradas: number;
  alturaEntradas: number;
  ySaidas: number;
  alturaSaidas: number;
  critica: boolean;
  rotulo: string;
  dica: string;
}

const LARGURA_GRAFICO = 720;
const ALTURA_GRAFICO = 200;
const MARGEM_ESQUERDA = 64;
const MARGEM_TOPO = 12;
const MARGEM_BAIXO = 26;

/**
 * Relatórios executivos: o Dossiê financeiro da rede, feito para levar a sócios ou ao banco.
 * A tela é o próprio documento: "Exportar PDF" abre a impressão do navegador (A4, até 2 páginas,
 * sem menu nem filtros) e "Exportar Excel" baixa a planilha com uma aba por bloco.
 */
@Component({
  selector: 'app-relatorios',
  imports: [CurrencyPipe, DatePipe, DecimalPipe, RouterLink, ExportarComponent, SimboloComponent],
  templateUrl: './relatorios.component.html'
})
export class RelatoriosComponent {
  /** Nome do ERP da rede nos textos (vem da API). */
  protected readonly erp = inject(ErpService).nome;

  private readonly api = inject(PrevisioService);

  readonly horizontes = [30, 60, 90];
  readonly dias = signal(30);
  readonly de = signal(dataIso(primeiroDiaDoMes(new Date())));
  readonly ate = signal(dataIso(ultimoDiaDoMes(new Date())));

  readonly dossie = signal<Dossie | null>(null);
  readonly carregando = signal(false);
  readonly erro = signal('');

  readonly exportar = () => this.api.exportarDossie(this.dias(), this.de(), this.ate());

  readonly larguraGrafico = LARGURA_GRAFICO;
  readonly alturaGrafico = ALTURA_GRAFICO;
  readonly margemEsquerda = MARGEM_ESQUERDA;

  /** Escala do gráfico: de min(0, menor saldo acumulado) até o maior valor da projeção. */
  private readonly escala = computed(() => {
    const semanas = this.dossie()?.projecao.semanas ?? [];
    let maximo = 0;
    let minimo = 0;
    for (const semana of semanas) {
      maximo = Math.max(maximo, semana.entradas, semana.saidas, semana.saldoAcumulado);
      minimo = Math.min(minimo, semana.saldoAcumulado);
    }
    if (maximo === minimo) {
      maximo = minimo + 1;
    }
    return { maximo, minimo };
  });

  readonly yZero = computed(() => this.y(0));

  readonly barras = computed<BarraSemana[]>(() => {
    const dossie = this.dossie();
    if (!dossie) {
      return [];
    }
    const semanas = dossie.projecao.semanas;
    const passo = (LARGURA_GRAFICO - MARGEM_ESQUERDA) / Math.max(1, semanas.length);
    const largura = Math.max(4, Math.min(22, passo * 0.32));
    const zero = this.yZero();
    return semanas.map((semana, i) => {
      const centro = MARGEM_ESQUERDA + passo * i + passo / 2;
      const yEntradas = this.y(semana.entradas);
      const ySaidas = this.y(semana.saidas);
      return {
        x: centro - largura - 1,
        largura,
        yEntradas,
        alturaEntradas: Math.max(0, zero - yEntradas),
        ySaidas,
        alturaSaidas: Math.max(0, zero - ySaidas),
        critica: semana.critica,
        rotulo: rotuloSemana(semana),
        dica: rotuloSemana(semana) + ': entradas ' + reais(semana.entradas) + ', saídas ' + reais(semana.saidas)
          + ', saldo acumulado ' + reais(semana.saldoAcumulado)
      };
    });
  });

  /** Linha do saldo acumulado, passando pelo centro de cada semana. */
  readonly linhaSaldo = computed(() => {
    const semanas = this.dossie()?.projecao.semanas ?? [];
    const passo = (LARGURA_GRAFICO - MARGEM_ESQUERDA) / Math.max(1, semanas.length);
    return semanas
      .map((semana, i) => (MARGEM_ESQUERDA + passo * i + passo / 2).toFixed(1) + ',' + this.y(semana.saldoAcumulado).toFixed(1))
      .join(' ');
  });

  /** Três marcas no eixo: o topo, o zero e o fundo (quando negativo). */
  readonly marcasEixo = computed(() => {
    const { maximo, minimo } = this.escala();
    const marcas = [{ valor: maximo, y: this.y(maximo) }, { valor: 0, y: this.y(0) }];
    if (minimo < 0) {
      marcas.push({ valor: minimo, y: this.y(minimo) });
    }
    return marcas.map(m => ({ ...m, texto: reaisCurtos(m.valor) }));
  });

  /** Contorno do índice de conformidade (anel de 0 a 100). */
  readonly anelIndice = computed(() => {
    const indice = this.dossie()?.auditoria.indice ?? 0;
    const circunferencia = 2 * Math.PI * 42;
    return { circunferencia, preenchido: (circunferencia * indice) / 100 };
  });

  readonly maiorEmpresa = computed(() => Math.max(1, ...(this.dossie()?.comparativo.empresas ?? []).map(e => e.saidas)));

  constructor() {
    this.carregar();
  }

  mudarDias(evento: Event): void {
    this.dias.set(Number((evento.target as HTMLSelectElement).value));
    this.carregar();
  }

  mudarDe(evento: Event): void {
    this.de.set((evento.target as HTMLInputElement).value);
  }

  mudarAte(evento: Event): void {
    this.ate.set((evento.target as HTMLInputElement).value);
  }

  carregar(): void {
    if (!this.de() || !this.ate()) {
      this.erro.set('Informe as duas datas do período.');
      return;
    }
    this.erro.set('');
    this.carregando.set(true);
    this.api.obterDossie(this.dias(), this.de(), this.ate()).subscribe({
      next: dossie => {
        this.dossie.set(dossie);
        this.carregando.set(false);
      },
      error: erro => {
        this.carregando.set(false);
        this.erro.set(mensagemDaApi(erro, 'Não consegui montar o dossiê. Tente de novo.'));
      }
    });
  }

  /** O documento sai pela impressão do navegador ("Salvar como PDF"). */
  imprimir(): void {
    window.print();
  }

  cnpj(valor: string): string {
    return formatarCnpj(valor);
  }

  larguraEmpresa(saidas: number): number {
    return Math.max(1, Math.round((saidas / this.maiorEmpresa()) * 100));
  }

  /** Onde arrumar o que o indicador aponta. */
  linkIndicador(indicador: IndicadorConformidade): { rota: string; parametros: Record<string, string> } {
    if (indicador.chave === 'notas') {
      return { rota: '/pages/notas-pendentes', parametros: {} };
    }
    if (indicador.chave === 'em-dia') {
      return { rota: '/pages/contas-pagar', parametros: { situacao: 'Vencidas' } };
    }
    return { rota: '/pages/contas-pagar', parametros: { categoria: 'Sem categoria', situacao: 'Todas', de: this.de(), ate: this.ate() } };
  }

  textoFaixa(faixa: string): string {
    if (faixa === 'verde') {
      return 'Em ordem';
    }
    if (faixa === 'amarelo') {
      return 'Atenção';
    }
    if (faixa === 'vermelho') {
      return 'Precisa arrumar';
    }
    return 'Sem dados';
  }

  private y(valor: number): number {
    const { maximo, minimo } = this.escala();
    const util = ALTURA_GRAFICO - MARGEM_TOPO - MARGEM_BAIXO;
    return MARGEM_TOPO + ((maximo - valor) / (maximo - minimo)) * util;
  }
}

function primeiroDiaDoMes(hoje: Date): Date {
  return new Date(hoje.getFullYear(), hoje.getMonth(), 1);
}

function ultimoDiaDoMes(hoje: Date): Date {
  return new Date(hoje.getFullYear(), hoje.getMonth() + 1, 0);
}

function rotuloSemana(semana: SemanaProjecao): string {
  return semana.inicio.substring(8, 10) + '/' + semana.inicio.substring(5, 7);
}

function reais(valor: number): string {
  return valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 0 });
}

/** "R$ 12 mil", "R$ 1,2 mi": cabe no eixo do gráfico. */
function reaisCurtos(valor: number): string {
  const absoluto = Math.abs(valor);
  const sinal = valor < 0 ? '−' : '';
  if (absoluto >= 1000000) {
    return sinal + 'R$ ' + (absoluto / 1000000).toLocaleString('pt-BR', { maximumFractionDigits: 1 }) + ' mi';
  }
  if (absoluto >= 1000) {
    return sinal + 'R$ ' + Math.round(absoluto / 1000).toLocaleString('pt-BR') + ' mil';
  }
  return sinal + 'R$ ' + Math.round(absoluto).toLocaleString('pt-BR');
}
