import { Component, Injector, afterNextRender, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe, SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { PrevisioService } from '../../core/previsio.service';
import { NotaParaResolver, ResumoPainel, TotalCategoria, TotalSemana } from '../../core/previsio.models';
import { descricaoDaSituacao } from '../../core/filtro-notas';
import { UrgenciaNota, urgenciaDaNota } from './notas-para-resolver';

// Chartist 0.11 é carregado como script global (angular.json), como no template
declare const Chartist: any;
import { CarregandoDirective } from '../../shared/carregando.directive';
import { TAMANHO_PAGINA_PADRAO, paginar } from '../../core/paginacao';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';
import { ExportarComponent } from '../../shared/exportar/exportar.component';

// Acima desta parte das saídas sem categoria (%), o painel mostra o atalho para configurar
const LIMITE_SEM_CATEGORIA = 20;

@Component({
  selector: 'app-painel',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, DecimalPipe, SlicePipe, CarregandoDirective, PaginacaoComponent, ExportarComponent],
  templateUrl: './painel.component.html'
})
export class PainelComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly injector = inject(Injector);

  readonly resumo = signal<ResumoPainel | null>(null);

  // Paginação da tabela "Por empresa" (a linha de total soma todas as empresas)
  readonly pagina = signal(1);
  readonly tamanhoPagina = signal(TAMANHO_PAGINA_PADRAO);
  readonly empresasDaPagina = computed(() => {
    const resumo = this.resumo();
    if (!resumo) {
      return [];
    }
    return paginar(resumo.empresas, this.pagina(), this.tamanhoPagina());
  });
  readonly dias = signal(30);
  readonly exportar = () => this.previsio.exportarContasPagar(this.dias());
  readonly carregando = signal(false);
  readonly erro = signal('');

  readonly fluxoCaixa = computed(() => this.resumo()?.fluxoCaixaAtivo === true);
  readonly abs = Math.abs;

  /** Card "Saídas por categoria": só aparece quando há contas no período. */
  readonly categorias = computed<TotalCategoria[]>(() => this.resumo()?.categorias ?? []);
  private readonly maiorCategoria = computed(() => Math.max(0, ...this.categorias().map(c => c.valor)));
  private readonly totalCategorias = computed(() => this.categorias().reduce((soma, c) => soma + c.valor, 0));

  /** Parte das saídas sem categoria no Bling (0 a 100). */
  readonly percentualSemCategoria = computed(() => {
    const sem = this.categorias().find(c => c.tipo === 'sem');
    if (!sem) {
      return 0;
    }
    return this.percentualCategoria(sem);
  });

  /** Acima do limite, o card de categorias mostra o atalho "Configurar categorias". */
  readonly mostrarConfigurarCategorias = computed(() => this.percentualSemCategoria() > LIMITE_SEM_CATEGORIA);

  /** Semana sob o mouse (ou o foco) no gráfico, com a posição do tooltip dentro da área do gráfico. */
  readonly dica = signal<{ semana: TotalSemana; esquerda: number; topo: number } | null>(null);

  // Semana com mais saídas; nenhuma quando todas estão zeradas
  readonly maiorSemana = computed<TotalSemana | null>(() => {
    const resumo = this.resumo();
    if (!resumo || resumo.semanas.length === 0) {
      return null;
    }
    let maior = resumo.semanas[0];
    for (const semana of resumo.semanas) {
      if (semana.valor > maior.valor) {
        maior = semana;
      }
    }
    if (maior.valor <= 0) {
      return null;
    }
    return maior;
  });

  // Nada previsto e nenhuma nota pendente: a tela mostra o estado vazio no lugar dos zeros
  readonly semDados = computed(() => {
    const resumo = this.resumo();
    if (!resumo) {
      return false;
    }
    return resumo.totalPeriodo === 0 && resumo.quantidadeContas === 0 && resumo.notasPendentes === 0
      && (resumo.quantidadeVencidas ?? 0) === 0
      && (resumo.quantidadeContasReceber ?? 0) === 0 && (resumo.quantidadeReceberVencido ?? 0) === 0;
  });

  // Todas as semanas zeradas (saídas e entradas): no lugar do gráfico vai o aviso
  readonly graficoVazio = computed(() => {
    if (this.maiorSemana() !== null) {
      return false;
    }
    const resumo = this.resumo();
    if (!resumo || !this.fluxoCaixa()) {
      return true;
    }
    return resumo.semanas.every(semana => (semana.entradas ?? 0) <= 0);
  });
  readonly notasParaResolver = computed<NotaParaResolver[]>(() => this.resumo()?.notasParaResolver ?? []);
  readonly descricaoSituacao = descricaoDaSituacao;

  /** Largura da barra, proporcional à maior categoria. */
  larguraCategoria(categoria: TotalCategoria): number {
    const maior = this.maiorCategoria();
    if (maior <= 0) {
      return 0;
    }
    return Math.max(2, Math.round((categoria.valor / maior) * 100));
  }

  percentualCategoria(categoria: TotalCategoria): number {
    const total = this.totalCategorias();
    if (total <= 0) {
      return 0;
    }
    return Math.round((categoria.valor / total) * 100);
  }

  /** Filtro de Contas a pagar da categoria; Outras e A conferir não têm. */
  linkCategoria(categoria: TotalCategoria, resumo: ResumoPainel): Record<string, string> | null {
    let nome = '';
    if (categoria.tipo === 'categoria') {
      nome = categoria.nome;
    } else if (categoria.tipo === 'sem') {
      nome = 'Sem categoria';
    } else {
      return null;
    }
    return { categoria: nome, de: resumo.inicio.substring(0, 10), ate: resumo.fim.substring(0, 10) };
  }

  urgencia(nota: NotaParaResolver): UrgenciaNota {
    const resumo = this.resumo();
    if (resumo === null) {
      return 'depois';
    }
    return urgenciaDaNota(nota.primeiroVencimento, resumo.inicio, resumo.fim);
  }

  constructor() {
    this.carregar();
  }

  carregar(): void {
    this.carregando.set(true);
    this.erro.set('');

    this.previsio.obterPainel(this.dias()).subscribe({
      next: resumo => {
        this.resumo.set(resumo);
        this.carregando.set(false);
        // O gráfico só existe na tela depois que o Angular desenhar o card
        afterNextRender(() => this.desenharGrafico(), { injector: this.injector });
      },
      error: () => {
        this.erro.set('Não foi possível carregar o painel. Verifique se a API está no ar.');
        this.carregando.set(false);
      }
    });
  }

  alterarPeriodo(dias: number): void {
    this.dias.set(Number(dias));
    this.carregar();
  }

  formatarCnpj(cnpj: string): string {
    if (!cnpj || cnpj.length !== 14) {
      return cnpj;
    }
    return cnpj.substring(0, 2) + '.' + cnpj.substring(2, 5) + '.' + cnpj.substring(5, 8) + '/'
      + cnpj.substring(8, 12) + '-' + cnpj.substring(12, 14);
  }

  /** Tooltip do gráfico: a barra traz o índice da semana (data-semana), gravado ao desenhar. */
  mostrarDica(evento: Event): void {
    const alvo = evento.target as Element | null;
    const barra = alvo && typeof alvo.closest === 'function' ? alvo.closest('[data-semana]') : null;
    const area = evento.currentTarget as HTMLElement | null;
    const resumo = this.resumo();
    if (!barra || !area || !resumo) {
      return;
    }
    const indice = Number(barra.getAttribute('data-semana'));
    const semana = resumo.semanas[indice];
    if (!semana) {
      return;
    }
    const caixaArea = area.getBoundingClientRect();
    const caixaBarra = barra.getBoundingClientRect();
    this.dica.set({
      semana,
      esquerda: caixaBarra.left - caixaArea.left + caixaBarra.width / 2,
      topo: caixaBarra.top - caixaArea.top
    });
  }

  esconderDica(): void {
    this.dica.set(null);
  }

  private desenharGrafico(): void {
    const resumo = this.resumo();
    if (!resumo || this.semDados() || this.graficoVazio()) {
      return;
    }

    const rotulos: string[] = [];
    const valores: number[] = [];
    const entradas: number[] = [];
    let maiorValor = 0;

    for (const semana of resumo.semanas) {
      rotulos.push(this.formatarDiaMes(semana.inicio));
      valores.push(semana.valor);
      entradas.push(semana.entradas ?? 0);
      if (semana.valor > maiorValor) {
        maiorValor = semana.valor;
      }
      if ((semana.entradas ?? 0) > maiorValor) {
        maiorValor = semana.entradas ?? 0;
      }
    }

    let topo = maiorValor * 1.15;
    if (topo === 0) {
      topo = 10;
    }

    const opcoes: any = {
      axisX: { showGrid: false },
      axisY: {
        // Espaço para "R$ 12 mil"
        offset: 64,
        labelInterpolationFnc: (valor: number) => this.abreviarValor(valor)
      },
      low: 0,
      high: topo,
      chartPadding: { top: 12, right: 8, bottom: 0, left: 0 }
    };

    // Fluxo de caixa: saídas e entradas lado a lado (série a e série b)
    let series: number[][] = [valores];
    if (this.fluxoCaixa()) {
      series = [valores, entradas];
      opcoes.seriesBarDistance = 24;
    }
    // No celular as barras do fluxo de caixa ficam mais finas e mais juntas
    const responsivo = [['screen and (max-width: 575.98px)', { seriesBarDistance: 12 }]];
    const grafico = new Chartist.Bar('#previsioSemanasChart', { labels: rotulos, series: series }, opcoes, responsivo);
    this.prepararDica(grafico, resumo.semanas);
    this.animarBarras(grafico);
  }

  // Cada barra recebe o índice da semana e entra no Tab, para o tooltip abrir no foco do teclado
  private prepararDica(grafico: any, semanas: TotalSemana[]): void {
    grafico.on('draw', (dado: any) => {
      if (dado.type !== 'bar') {
        return;
      }
      const semana = semanas[dado.index];
      if (!semana) {
        return;
      }
      dado.element.attr({
        'data-semana': String(dado.index),
        tabindex: '0',
        'aria-describedby': 'pv-grafico-dica',
        'aria-label': 'Semana de ' + this.formatarDiaMes(semana.inicio) + ' a ' + this.formatarDiaMes(semana.fim)
      });
    });
  }

  // Mesma animação usada no dashboard de exemplo do template, só no primeiro desenho:
  // o Chartist redesenha a cada resize (ex.: ao recolher o menu) e, sem isso,
  // as barras sumiam e reapareciam com atraso cada vez maior
  private animarBarras(grafico: any): void {
    let sequencia = 0;
    let primeiroDesenho = true;
    grafico.on('created', () => {
      primeiroDesenho = false;
    });
    grafico.on('draw', (dado: any) => {
      if (dado.type === 'bar' && primeiroDesenho) {
        sequencia++;
        dado.element.animate({
          opacity: {
            begin: sequencia * 80,
            dur: 500,
            from: 0,
            to: 1,
            easing: 'ease'
          }
        });
      }
    });
  }

  // Eixo Y em reais, abreviado: "R$ 800", "R$ 12 mil", "R$ 1,5 mi"
  private abreviarValor(valor: number): string {
    if (valor >= 1000000) {
      return 'R$ ' + (valor / 1000000).toFixed(1).replace('.', ',').replace(',0', '') + ' mi';
    }
    if (valor >= 1000) {
      return 'R$ ' + Math.round(valor / 1000) + ' mil';
    }
    return 'R$ ' + Math.round(valor);
  }

  private formatarDiaMes(dataIso: string): string {
    // "2026-11-10T00:00:00" -> "10/11"
    const partes = dataIso.substring(0, 10).split('-');
    return partes[2] + '/' + partes[1];
  }
}
