import { Component, DOCUMENT, DestroyRef, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription, timer } from 'rxjs';

import { Aviso, PainelAvisos, TipoAviso } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';

/** De quanto em quanto tempo o sininho pergunta à API se há avisos novos. */
export const INTERVALO_AVISOS_MS = 60000;

const ICONES: Record<TipoAviso, string> = {
  NotaComProblema: 'receipt_long',
  BlingDesconectado: 'link_off',
  CaixaEmailFalhando: 'mail'
};

/**
 * Sininho do topo: problemas em aberto que precisam de alguém
 * (nota com problema, Bling desconectado, caixa de e-mail falhando).
 * Pergunta à API a cada minuto, sem a barra de carregamento. Ao abrir, marca os avisos como vistos.
 * Clicar num aviso leva à tela onde se resolve; para o admin, troca antes para o cliente do aviso.
 */
@Component({
  selector: 'app-sininho',
  imports: [DatePipe],
  host: {
    class: 'pv-sininho',
    '(document:click)': 'aoClicarNaPagina($event)',
    '(document:keydown.escape)': 'fechar(true)',
    '(window:resize)': 'fechar()'
  },
  template: `
    <button #gatilho type="button" class="pv-sininho__gatilho" [attr.aria-label]="rotulo()" [title]="rotulo()"
            aria-haspopup="dialog" [attr.aria-expanded]="aberto()" (click)="alternar()">
      <i class="material-icons" aria-hidden="true">notifications</i>
      @if (novos() > 0) {
        <span class="pv-sininho__contador" aria-hidden="true">{{ novos() > 9 ? '9+' : novos() }}</span>
      }
    </button>
    @if (aberto()) {
      <div class="pv-sininho__painel" role="dialog" aria-label="Avisos" [style.top.px]="topo()" [style.right.px]="direita()">
        <div class="pv-sininho__cabecalho">
          <span class="pv-sininho__titulo">Avisos</span>
          @if (total() > 0) {
            <span class="pv-sininho__total">{{ total() === 1 ? '1 em aberto' : total() + ' em aberto' }}</span>
          }
        </div>
        @if (itens().length === 0) {
          <div class="pv-sininho__vazio">
            <i class="material-icons" aria-hidden="true">task_alt</i>
            <span>Tudo em dia. Nenhum problema em aberto.</span>
          </div>
        } @else {
          <ul class="pv-sininho__lista">
            @for (aviso of itens(); track aviso.id) {
              <li>
                <button type="button" class="pv-sininho__item" [class.pv-sininho__item--novo]="aviso.novo" (click)="abrirAviso(aviso)">
                  <i class="material-icons pv-sininho__icone" aria-hidden="true">{{ icone(aviso) }}</i>
                  <span class="pv-sininho__texto">
                    <span class="pv-sininho__item-titulo">{{ aviso.titulo }}</span>
                    <span class="pv-sininho__mensagem">{{ aviso.mensagem }}</span>
                    <span class="pv-sininho__rodape">
                      @if (ehAdmin()) {
                        <span>{{ aviso.clienteNome }} · </span>
                      }
                      {{ aviso.criadoEm | date:'dd/MM/yyyy HH:mm' }}
                      @if (aviso.novo) {
                        <span class="pv-sininho__novo"> · Novo</span>
                      }
                    </span>
                  </span>
                </button>
              </li>
            }
          </ul>
          @if (total() > itens().length) {
            <div class="pv-sininho__mais">Mostrando os {{ itens().length }} mais recentes de {{ total() }}.</div>
          }
        }
      </div>
    }
  `
})
export class SininhoComponent implements OnInit {
  private readonly api = inject(PrevisioService);
  private readonly sessao = inject(SessaoService);
  private readonly router = inject(Router);
  private readonly documento = inject(DOCUMENT);
  private readonly elemento = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);

  readonly ehAdmin = this.sessao.ehAdmin;
  readonly aberto = signal(false);
  readonly painel = signal<PainelAvisos>({ novos: 0, total: 0, itens: [] });
  readonly topo = signal(0);
  readonly direita = signal(0);

  readonly novos = computed(() => this.painel().novos);
  readonly total = computed(() => this.painel().total);
  readonly itens = computed(() => this.painel().itens);
  readonly rotulo = computed(() => {
    const novos = this.novos();
    if (novos === 0) {
      return 'Avisos';
    }
    if (novos === 1) {
      return 'Avisos: 1 novo';
    }
    return 'Avisos: ' + novos + ' novos';
  });

  private readonly gatilho = viewChild.required<ElementRef<HTMLButtonElement>>('gatilho');
  private consulta: Subscription | null = null;

  ngOnInit(): void {
    const ciclo = timer(0, INTERVALO_AVISOS_MS).subscribe(() => {
      // Com a aba escondida não pergunta; volta a perguntar no próximo ciclo
      if (!this.documento.hidden) {
        this.atualizar();
      }
    });
    this.destroyRef.onDestroy(() => {
      ciclo.unsubscribe();
      this.cancelarConsulta();
    });
  }

  icone(aviso: Aviso): string {
    return ICONES[aviso.tipo] || 'error';
  }

  alternar(): void {
    if (this.aberto()) {
      this.fechar();
    } else {
      this.abrir();
    }
  }

  fechar(devolverFoco = false): void {
    if (!this.aberto()) {
      return;
    }
    this.aberto.set(false);
    if (devolverFoco) {
      this.gatilho().nativeElement.focus();
    }
  }

  aoClicarNaPagina(evento: Event): void {
    if (this.aberto() && !this.elemento.nativeElement.contains(evento.target as Node)) {
      this.fechar();
    }
  }

  abrirAviso(aviso: Aviso): void {
    this.fechar();
    if (this.ehAdmin() && this.sessao.clienteId() !== aviso.clienteId) {
      this.sessao.escolherCliente(aviso.clienteId);
    }
    this.router.navigateByUrl(aviso.link);
  }

  private abrir(): void {
    const caixa = this.gatilho().nativeElement.getBoundingClientRect();
    const janela = this.documento.defaultView;
    const larguraJanela = janela ? janela.innerWidth : 0;
    this.topo.set(caixa.bottom + 6);
    this.direita.set(Math.max(8, larguraJanela - caixa.right));
    this.aberto.set(true);

    if (this.novos() > 0) {
      // Zera o número na hora; os itens continuam marcados como "Novo" enquanto o painel está aberto
      this.painel.update(atual => ({ novos: 0, total: atual.total, itens: atual.itens }));
      this.api.marcarAvisosVistos().subscribe({ error: () => undefined });
    }
  }

  private atualizar(): void {
    // Com o painel aberto, não troca a lista debaixo do usuário
    if (this.aberto()) {
      return;
    }
    this.cancelarConsulta();
    this.consulta = this.api.obterAvisos().subscribe({
      next: painel => this.painel.set(painel),
      // Sem rede ou API fora: mantém o que tinha e tenta no próximo ciclo
      error: () => undefined
    });
  }

  private cancelarConsulta(): void {
    if (this.consulta) {
      this.consulta.unsubscribe();
      this.consulta = null;
    }
  }
}
