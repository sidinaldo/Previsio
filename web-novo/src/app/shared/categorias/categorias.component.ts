import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';

import { formatarCnpj } from '../../core/implantacao';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { ConfiguracaoCategorias, FornecedorDaRede, RegraCategoria } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../carregando.directive';

/**
 * Card "Categorias" da rede: categoria padrão e regras "fornecedor → categoria" usadas ao lançar
 * a nota no Bling. Sem regra, o Previsio repete a categoria da última conta do fornecedor no Bling.
 * Usado em Empresas (gestor e admin) e no detalhe do cliente (admin).
 */
@Component({
  selector: 'app-categorias',
  imports: [CarregandoDirective],
  template: `
    <div class="card">
      <div class="pv-card__cabecalho">
        <div>
          <h2 class="pv-card__titulo">Categorias</h2>
          <p class="pv-card__descricao">
            A categoria do Bling que vai na conta a pagar de cada nota. Sem regra para o fornecedor, o Previsio
            repete a categoria da última conta dele no Bling; se ele nunca teve, usa a categoria padrão.
          </p>
        </div>
      </div>
      @if (dados(); as atual) {
        <div class="card-body pv-resumo">
          @if (mensagem()) {
            <div class="alert alert-success" role="status"><span>{{ mensagem() }}</span></div>
          }
          @if (erro()) {
            <div class="alert alert-danger" role="alert"><span>{{ erro() }}</span></div>
          }
          @if (atual.empresasSemCategorias.length > 0) {
            <div class="alert alert-warning" role="status">
              <span>
                Ainda sem as categorias do Bling: {{ atual.empresasSemCategorias.join(', ') }}.
                Elas chegam na próxima sincronização; se não chegarem, reconecte a empresa ao Bling para liberar o acesso às categorias.
              </span>
            </div>
          }
          @if (atual.opcoes.length === 0) {
            <p class="pv-formulario__ajuda">Nenhuma categoria de despesa do Bling por enquanto. Cadastre as categorias no Bling e sincronize.</p>
          } @else {
            <div class="pv-resumo__linha">
              <div class="pv-resumo__campo">
                <label class="pv-rotulo" for="categoria-padrao">Categoria padrão</label>
                <span class="pv-campo-grupo bmd-form-group">
                  <select id="categoria-padrao" class="pv-campo pv-categorias__select" [value]="padrao()" (change)="padrao.set(valor($event))">
                    <option value="">{{ rotuloSemPadrao() }}</option>
                    @for (nome of atual.opcoes; track nome) {
                      <option [value]="nome" [selected]="nome === padrao()">{{ nome }}</option>
                    }
                  </select>
                </span>
              </div>
              <button type="button" class="btn btn-info" [pvCarregando]="acao() === 'padrao'" [disabled]="acao() !== null"
                      (click)="salvarPadrao()">Salvar</button>
            </div>
            @if (atual.padraoAusenteEm.length > 0) {
              <p class="pv-formulario__ajuda pv-categorias__aviso">
                Não existe no Bling de {{ atual.padraoAusenteEm.join(', ') }}: as notas dessas empresas vão sem categoria.
              </p>
            }

            <h3 class="pv-categorias__subtitulo">Regras por fornecedor</h3>
            @if (atual.regras.length === 0) {
              <p class="pv-formulario__ajuda">Nenhuma regra. Crie uma quando quiser fixar a categoria de um fornecedor.</p>
            } @else {
              <ul class="pv-categorias__regras">
                @for (regra of atual.regras; track regra.id) {
                  <li>
                    <div>
                      <strong>{{ regra.fornecedorNome || cnpj(regra.cnpjFornecedor) }}</strong>
                      <span class="pv-categorias__seta" aria-hidden="true">→</span>
                      {{ regra.categoria }}
                      @if (regra.fornecedorNome) {
                        <small class="pv-categorias__cnpj">{{ cnpj(regra.cnpjFornecedor) }}</small>
                      }
                      @if (regra.ausenteEm.length > 0) {
                        <small class="pv-categorias__aviso">Não existe no Bling de {{ regra.ausenteEm.join(', ') }}.</small>
                      }
                    </div>
                    <button type="button" class="btn btn-link btn-sm" [disabled]="acao() !== null"
                            [attr.aria-label]="'Remover a regra de ' + (regra.fornecedorNome || regra.cnpjFornecedor)"
                            (click)="remover(regra)">
                      <i class="material-icons" aria-hidden="true">delete</i>
                    </button>
                  </li>
                }
              </ul>
            }
            @if (atual.fornecedores.length > 0) {
              <div class="pv-resumo__linha">
                <div class="pv-resumo__campo">
                  <label class="pv-rotulo" for="regra-fornecedor">Fornecedor</label>
                  <span class="pv-campo-grupo bmd-form-group">
                    <select id="regra-fornecedor" class="pv-campo pv-categorias__select" [value]="fornecedor()" (change)="fornecedor.set(valor($event))">
                      <option value="">Escolha</option>
                      @for (item of atual.fornecedores; track item.cnpj) {
                        <option [value]="item.cnpj" [selected]="item.cnpj === fornecedor()">{{ rotuloFornecedor(item) }}</option>
                      }
                    </select>
                  </span>
                </div>
                <div class="pv-resumo__campo">
                  <label class="pv-rotulo" for="regra-categoria">Categoria</label>
                  <span class="pv-campo-grupo bmd-form-group">
                    <select id="regra-categoria" class="pv-campo pv-categorias__select" [value]="categoriaRegra()" (change)="categoriaRegra.set(valor($event))">
                      <option value="">Escolha</option>
                      @for (nome of atual.opcoes; track nome) {
                        <option [value]="nome" [selected]="nome === categoriaRegra()">{{ nome }}</option>
                      }
                    </select>
                  </span>
                </div>
                <button type="button" class="btn btn-secundario" [pvCarregando]="acao() === 'regra'"
                        [disabled]="acao() !== null || !podeAdicionar()" (click)="adicionar()">
                  <i class="material-icons" aria-hidden="true">add</i> Adicionar regra
                </button>
              </div>
              <p class="pv-formulario__ajuda">
                A lista mostra os fornecedores das notas já recebidas. Se o fornecedor já tem regra, a categoria é trocada.
                As categorias são escolhidas pelo nome: cada empresa usa a categoria com esse nome no Bling dela.
              </p>
            }
          }
        </div>
      } @else if (erro()) {
        <div class="card-body"><div class="alert alert-danger" role="alert"><span>{{ erro() }}</span></div></div>
      } @else {
        <div class="card-body"><p class="pv-formulario__ajuda">Carregando...</p></div>
      }
    </div>
  `
})
export class CategoriasComponent {
  private readonly api = inject(PrevisioService);

  readonly clienteId = input.required<number>();

  readonly dados = signal<ConfiguracaoCategorias | null>(null);
  readonly padrao = signal('');
  readonly fornecedor = signal('');
  readonly categoriaRegra = signal('');
  readonly acao = signal<'padrao' | 'regra' | 'remover' | null>(null);
  readonly mensagem = signal('');
  readonly erro = signal('');

  readonly podeAdicionar = computed(() => this.fornecedor() !== '' && this.categoriaRegra() !== '');

  /** Primeira opção da categoria padrão: a automática (quando ligada) ou sem categoria. */
  readonly rotuloSemPadrao = computed(() => {
    const automatica = this.dados()?.padraoAutomatico;
    if (automatica) {
      return 'Automática (' + automatica + ')';
    }
    return 'Sem categoria';
  });

  constructor() {
    effect(() => {
      const id = this.clienteId();
      untracked(() => this.carregar(id));
    });
  }

  valor(evento: Event): string {
    return (evento.target as HTMLSelectElement).value;
  }

  cnpj(valor: string): string {
    return formatarCnpj(valor);
  }

  rotuloFornecedor(item: FornecedorDaRede): string {
    return item.nome + ' (' + formatarCnpj(item.cnpj) + ')';
  }

  salvarPadrao(): void {
    this.executar('padrao', this.api.alterarCategoriaPadrao(this.clienteId(), this.padrao()), () => {
      if (this.padrao() === '') {
        const automatica = this.dados()?.padraoAutomatico;
        if (automatica) {
          return 'Padrão automática: notas de fornecedor sem regra e sem histórico vão para ' + automatica + '.';
        }
        return 'Sem categoria padrão: notas de fornecedor sem regra e sem histórico vão sem categoria.';
      }
      return 'Categoria padrão salva.';
    });
  }

  adicionar(): void {
    const regra = { cnpjFornecedor: this.fornecedor(), categoria: this.categoriaRegra() };
    this.executar('regra', this.api.salvarRegraCategoria(this.clienteId(), regra), () => {
      this.fornecedor.set('');
      this.categoriaRegra.set('');
      return 'Regra salva. Vale para as próximas notas lançadas.';
    });
  }

  remover(regra: RegraCategoria): void {
    this.executar('remover', this.api.removerRegraCategoria(this.clienteId(), regra.id), () => 'Regra removida.');
  }

  private executar(
    acao: 'padrao' | 'regra' | 'remover',
    chamada: ReturnType<PrevisioService['obterCategorias']>,
    sucesso: () => string
  ): void {
    this.mensagem.set('');
    this.erro.set('');
    this.acao.set(acao);
    chamada.subscribe({
      next: dados => {
        this.acao.set(null);
        this.aplicar(dados);
        this.mensagem.set(sucesso());
      },
      error: erro => {
        this.acao.set(null);
        this.erro.set(mensagemDaApi(erro, 'Não consegui salvar as categorias.'));
      }
    });
  }

  private aplicar(dados: ConfiguracaoCategorias): void {
    this.dados.set(dados);
    this.padrao.set(dados.categoriaPadrao ?? '');
  }

  private carregar(clienteId: number): void {
    this.dados.set(null);
    this.mensagem.set('');
    this.erro.set('');
    this.api.obterCategorias(clienteId).subscribe({
      next: dados => this.aplicar(dados),
      error: erro => this.erro.set(mensagemDaApi(erro, 'Não consegui carregar as categorias.'))
    });
  }
}
