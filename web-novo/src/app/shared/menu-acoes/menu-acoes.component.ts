import { Component, DOCUMENT, ElementRef, Injector, OnDestroy, afterNextRender, inject, input, signal, viewChild } from '@angular/core';

let proximoId = 1;

/**
 * Menu "⋯" com as ações secundárias de uma linha de tabela.
 * Os itens vêm de quem usa (botões ou links com a classe pv-acoes__item):
 *
 *   <app-menu-acoes rotulo="Mais ações para Loja Centro">
 *     <button type="button" class="pv-acoes__item" (click)="editar(empresa)">Editar</button>
 *   </app-menu-acoes>
 *
 * O menu abre em posição fixa (não é cortado pela rolagem da tabela) e fecha ao escolher
 * um item, ao clicar fora, com Esc, ao rolar a tela ou ao redimensionar.
 * Teclado: setas, Home e End percorrem os itens; Esc volta o foco para o botão.
 */
@Component({
  selector: 'app-menu-acoes',
  host: {
    class: 'pv-acoes',
    '(document:click)': 'aoClicarNaPagina($event)',
    '(window:resize)': 'fechar()'
  },
  template: `
    <button #gatilho type="button" class="pv-acoes__gatilho" [attr.aria-label]="rotulo()" [title]="rotulo()"
            aria-haspopup="menu" [attr.aria-expanded]="aberto()" [attr.aria-controls]="aberto() ? idMenu : null"
            (click)="aoClicarNoGatilho($event)" (keydown)="teclaNoGatilho($event)">
      <i class="material-icons" aria-hidden="true">more_horiz</i>
    </button>
    @if (aberto()) {
      <div #painel class="pv-acoes__menu" role="menu" [id]="idMenu" [attr.aria-label]="rotulo()"
           [style.top.px]="topo()" [style.right.px]="direita()" [style.visibility]="posicionado() ? 'visible' : 'hidden'"
           (click)="aoEscolher($event)" (keydown)="teclaNoMenu($event)">
        <ng-content />
      </div>
    }
  `
})
export class MenuAcoesComponent implements OnDestroy {
  private readonly documento = inject(DOCUMENT);
  private readonly elemento = inject(ElementRef<HTMLElement>);
  private readonly injector = inject(Injector);

  /** Texto do botão "⋯" para leitor de tela e dica (ex.: "Mais ações para Loja Centro"). */
  readonly rotulo = input('Mais ações');

  readonly idMenu = 'pv-acoes-' + proximoId++;
  readonly aberto = signal(false);
  readonly topo = signal(0);
  readonly direita = signal(0);
  readonly posicionado = signal(false);

  private readonly gatilho = viewChild.required<ElementRef<HTMLButtonElement>>('gatilho');
  private readonly painel = viewChild<ElementRef<HTMLElement>>('painel');

  // Rolagem de qualquer elemento (a tabela, o painel principal) fecha o menu
  private readonly aoRolar = (): void => this.fechar();

  // Enter e espaço no botão também chegam como clique (com detail 0): aí o foco vai para o primeiro item
  aoClicarNoGatilho(evento: MouseEvent): void {
    this.alternar(evento.detail === 0);
  }

  alternar(focarPrimeiro = false): void {
    if (this.aberto()) {
      this.fechar();
    } else {
      this.abrir(focarPrimeiro);
    }
  }

  fechar(devolverFoco = false): void {
    if (!this.aberto()) {
      return;
    }
    this.aberto.set(false);
    this.posicionado.set(false);
    this.documento.removeEventListener('scroll', this.aoRolar, true);
    if (devolverFoco) {
      this.gatilho().nativeElement.focus();
    }
  }

  aoClicarNaPagina(evento: Event): void {
    if (this.aberto() && !this.elemento.nativeElement.contains(evento.target as Node)) {
      this.fechar();
    }
  }

  // O clique no item já executou a ação de quem usa o menu; aqui só fecha
  aoEscolher(evento: Event): void {
    const alvo = evento.target as HTMLElement;
    const item = alvo.closest('.pv-acoes__item') as HTMLButtonElement | null;
    if (item && !item.disabled) {
      this.fechar(true);
    }
  }

  teclaNoGatilho(evento: KeyboardEvent): void {
    if (evento.key === 'Escape' && this.aberto()) {
      evento.preventDefault();
      this.fechar(true);
    } else if (evento.key === 'ArrowDown' || evento.key === 'ArrowUp') {
      evento.preventDefault();
      if (!this.aberto()) {
        this.abrir(true);
      }
    }
  }

  teclaNoMenu(evento: KeyboardEvent): void {
    const itens = this.itens();
    if (itens.length === 0) {
      return;
    }
    const atual = itens.indexOf(this.documento.activeElement as HTMLElement);

    if (evento.key === 'Escape') {
      evento.preventDefault();
      this.fechar(true);
    } else if (evento.key === 'Tab') {
      this.fechar();
    } else if (evento.key === 'ArrowDown') {
      evento.preventDefault();
      itens[(atual + 1) % itens.length].focus();
    } else if (evento.key === 'ArrowUp') {
      evento.preventDefault();
      if (atual <= 0) {
        itens[itens.length - 1].focus();
      } else {
        itens[atual - 1].focus();
      }
    } else if (evento.key === 'Home') {
      evento.preventDefault();
      itens[0].focus();
    } else if (evento.key === 'End') {
      evento.preventDefault();
      itens[itens.length - 1].focus();
    }
  }

  ngOnDestroy(): void {
    this.documento.removeEventListener('scroll', this.aoRolar, true);
  }

  private abrir(focarPrimeiro: boolean): void {
    const caixa = this.gatilho().nativeElement.getBoundingClientRect();
    const janela = this.documento.defaultView;
    const larguraJanela = janela ? janela.innerWidth : 0;
    this.topo.set(caixa.bottom + 4);
    this.direita.set(Math.max(8, larguraJanela - caixa.right));
    this.aberto.set(true);
    this.documento.addEventListener('scroll', this.aoRolar, true);

    // Depois de desenhar: marca os itens para o leitor de tela, abre para cima se não couber e foca
    afterNextRender(() => {
      const painel = this.painel();
      if (!painel) {
        return;
      }
      for (const item of this.itens()) {
        item.setAttribute('role', 'menuitem');
        item.setAttribute('tabindex', '-1');
      }
      const alturaJanela = janela ? janela.innerHeight : 0;
      const altura = painel.nativeElement.getBoundingClientRect().height;
      if (caixa.bottom + 4 + altura > alturaJanela - 8 && caixa.top - 4 - altura >= 8) {
        this.topo.set(caixa.top - 4 - altura);
        painel.nativeElement.style.top = (caixa.top - 4 - altura) + 'px';
      }
      // Já no DOM: um elemento escondido não recebe o foco
      painel.nativeElement.style.visibility = 'visible';
      this.posicionado.set(true);
      if (focarPrimeiro) {
        const itens = this.itens();
        if (itens.length > 0) {
          itens[0].focus();
        }
      }
    }, { injector: this.injector });
  }

  private itens(): HTMLElement[] {
    const painel = this.painel();
    if (!painel) {
      return [];
    }
    const lista = Array.from(painel.nativeElement.querySelectorAll('.pv-acoes__item')) as HTMLElement[];
    return lista.filter(item => !(item as HTMLButtonElement).disabled);
  }
}
