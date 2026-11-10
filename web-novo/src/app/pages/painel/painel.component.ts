import { Component, Injector, afterNextRender, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { PrevisioService } from '../../core/previsio.service';
import { ResumoPainel, TotalSemana } from '../../core/previsio.models';

// Chartist 0.11 é carregado como script global (angular.json), como no template
declare const Chartist: any;

@Component({
  selector: 'app-painel',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, DecimalPipe],
  templateUrl: './painel.component.html'
})
export class PainelComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly injector = inject(Injector);

  readonly resumo = signal<ResumoPainel | null>(null);
  readonly dias = signal(30);
  readonly carregando = signal(false);
  readonly erro = signal('');

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
    return maior;
  });

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

  private desenharGrafico(): void {
    const resumo = this.resumo();
    if (!resumo) {
      return;
    }

    const rotulos: string[] = [];
    const valores: number[] = [];
    let maiorValor = 0;

    for (const semana of resumo.semanas) {
      rotulos.push(this.formatarDiaMes(semana.inicio));
      valores.push(semana.valor);
      if (semana.valor > maiorValor) {
        maiorValor = semana.valor;
      }
    }

    let topo = maiorValor * 1.15;
    if (topo === 0) {
      topo = 10;
    }

    const opcoes: any = {
      axisX: { showGrid: false },
      axisY: {
        labelInterpolationFnc: (valor: number) => this.abreviarValor(valor)
      },
      low: 0,
      high: topo,
      chartPadding: { top: 0, right: 5, bottom: 0, left: 10 }
    };

    const grafico = new Chartist.Bar('#previsioSemanasChart', { labels: rotulos, series: [valores] }, opcoes);
    this.animarBarras(grafico);
  }

  // Mesma animação usada no dashboard de exemplo do template
  private animarBarras(grafico: any): void {
    let sequencia = 0;
    grafico.on('draw', (dado: any) => {
      if (dado.type === 'bar') {
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

  private abreviarValor(valor: number): string {
    if (valor >= 1000000) {
      return (valor / 1000000).toFixed(1).replace('.', ',') + 'M';
    }
    if (valor >= 1000) {
      return Math.round(valor / 1000) + 'k';
    }
    return String(Math.round(valor));
  }

  private formatarDiaMes(dataIso: string): string {
    // "2026-11-10T00:00:00" -> "10/11"
    const partes = dataIso.substring(0, 10).split('-');
    return partes[2] + '/' + partes[1];
  }
}
