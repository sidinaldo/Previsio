import { Component, ElementRef, inject } from '@angular/core';
import { afterNextRender } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { SessaoService } from '../../core/sessao.service';
import { AlertasVencimentoComponent } from '../../shared/alertas-vencimento/alertas-vencimento.component';
import { CategoriasComponent } from '../../shared/categorias/categorias.component';
import { ConferenciaBlingComponent } from '../../shared/conferencia-bling/conferencia-bling.component';
import { FluxoCaixaComponent } from '../../shared/fluxo-caixa/fluxo-caixa.component';
import { FormasPagamentoComponent } from '../../shared/formas-pagamento/formas-pagamento.component';
import { ResumoSemanalComponent } from '../../shared/resumo-semanal/resumo-semanal.component';

/**
 * Configurações da rede escolhida no topo: conferência com o Bling, fluxo de caixa, categorias,
 * formas de pagamento, avisos de vencimento e resumo por e-mail. Cada seção salva sozinha.
 */
@Component({
  selector: 'app-configuracoes',
  imports: [
    ConferenciaBlingComponent, FluxoCaixaComponent, CategoriasComponent, FormasPagamentoComponent, AlertasVencimentoComponent,
    ResumoSemanalComponent
  ],
  template: `
    <div class="main-content">
      <div class="container-fluid">
        <div class="pv-config">
          <div class="pv-pagina__cabecalho">
            <div>
              <h1 class="pv-pagina__titulo">Configurações</h1>
              <p class="pv-pagina__subtitulo">Como o Previsio trabalha para esta rede. Cada seção salva sozinha.</p>
            </div>
          </div>
          @if (sessao.clienteId(); as id) {
            <app-conferencia-bling [clienteId]="id" />
            <app-fluxo-caixa [clienteId]="id" />
            <app-categorias id="categorias" [clienteId]="id" />
            <app-formas-pagamento id="formas-pagamento" [clienteId]="id" />
            <app-alertas-vencimento [clienteId]="id" />
            <app-resumo-semanal [clienteId]="id" />
          } @else {
            <div class="card"><div class="card-body"><p class="pv-formulario__ajuda">Escolha um cliente no topo da tela.</p></div></div>
          }
        </div>
      </div>
    </div>
  `
})
export class ConfiguracoesComponent {
  readonly sessao = inject(SessaoService);
  private readonly elemento = inject(ElementRef);

  constructor() {
    // Do painel ("Configurar categorias"): /pages/configuracoes#categorias rola até a seção
    const ancora = inject(ActivatedRoute).snapshot.fragment;
    if (ancora) {
      afterNextRender(() => this.rolarAte(ancora, 0));
    }
  }

  // Espera a seção terminar de carregar (senão ela muda de lugar depois de rolar)
  private rolarAte(ancora: string, tentativa: number): void {
    const raiz = this.elemento.nativeElement as HTMLElement;
    const secao = raiz.querySelector('#' + ancora) as HTMLElement | null;
    let pronta = false;
    if (secao !== null) {
      const carregando = (secao.textContent ?? '').includes('Carregando...');
      pronta = tentativa >= 20 || !carregando;
    }
    if (pronta && secao !== null) {
      secao.scrollIntoView({ block: 'start' });
      return;
    }
    if (tentativa < 20) {
      setTimeout(() => this.rolarAte(ancora, tentativa + 1), 150);
    }
  }
}
