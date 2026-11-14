import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';

import { formatarCnpj } from '../../core/implantacao';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { ConfiguracaoFormasPagamento, FornecedorDaRede, OpcaoFormaPagamento, RegraFormaPagamento } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../carregando.directive';

type AcaoFormas = 'padrao' | 'regra' | 'remover';

/**
 * Seção "Formas de pagamento" da rede: forma padrão e regras "fornecedor → forma" usadas ao lançar a nota
 * no Bling. Sem regra, o Previsio repete a forma da última conta do fornecedor no Bling; sem histórico,
 * usa o meio de pagamento do XML (boleto, Pix...) e, por último, a forma padrão.
 */
@Component({
  selector: 'app-formas-pagamento',
  imports: [CarregandoDirective],
  template: `
    <section class="pv-secao">
      <div class="pv-secao__cabecalho">
        <h2 class="pv-card__titulo">Formas de pagamento</h2>
        <p class="pv-card__descricao">
          A forma do Bling que vai na conta a pagar de cada nota. Sem regra para o fornecedor, o Previsio repete a
          forma da última conta dele no Bling; se ele nunca teve, usa a da nota (boleto, Pix...) e, por último, a padrão.
        </p>
      </div>
      <div class="card pv-secao__card">
      @if (dados(); as atual) {
        <div class="card-body pv-resumo">
          @if (mensagem()) {
            <div class="alert alert-success" role="status"><span>{{ mensagem() }}</span></div>
          }
          @if (erro()) {
            <div class="alert alert-danger" role="alert"><span>{{ erro() }}</span></div>
          }
          @if (atual.empresasSemFormas.length > 0) {
            <div class="alert alert-warning" role="status">
              <span>
                Ainda sem as formas de pagamento do Bling: {{ atual.empresasSemFormas.join(', ') }}.
                Elas chegam na próxima sincronização; se não chegarem, reconecte a empresa ao Bling para liberar o acesso às formas de pagamento.
              </span>
            </div>
          }
          @if (atual.opcoes.length === 0) {
            <p class="pv-formulario__ajuda">Nenhuma forma de pagamento ativa no Bling por enquanto. Cadastre as formas no Bling e sincronize.</p>
          } @else {
            <div class="pv-resumo__linha">
              <div class="pv-resumo__campo">
                <label class="pv-rotulo" for="forma-padrao">Forma padrão</label>
                <span class="pv-campo-grupo bmd-form-group">
                  <select id="forma-padrao" class="pv-campo pv-categorias__select" [value]="padrao()" (change)="padrao.set(valor($event))">
                    <option value="">{{ rotuloSemPadrao() }}</option>
                    @for (opcao of atual.opcoes; track opcao.nome) {
                      <option [value]="opcao.nome" [selected]="opcao.nome === padrao()">{{ rotuloOpcao(opcao) }}</option>
                    }
                  </select>
                </span>
              </div>
            </div>
            @if (atual.padraoAusenteEm.length > 0) {
              <p class="pv-formulario__ajuda pv-categorias__aviso">
                Não existe no Bling de {{ atual.padraoAusenteEm.join(', ') }}: as notas dessas empresas sem regra, histórico
                ou forma na nota vão sem forma de pagamento.
              </p>
            }
            <div class="pv-config__acoes">
              <button type="button" class="btn btn-info" [pvCarregando]="acao() === 'padrao'" [disabled]="acao() !== null"
                      (click)="salvarPadrao()">Salvar forma padrão</button>
            </div>

            <h3 class="pv-categorias__subtitulo">Regras por fornecedor</h3>
            @if (atual.regras.length === 0) {
              <p class="pv-formulario__ajuda">Nenhuma regra. Crie uma quando quiser fixar a forma de um fornecedor (por exemplo, o que debita na conta).</p>
            } @else {
              <ul class="pv-categorias__regras">
                @for (regra of atual.regras; track regra.id) {
                  <li>
                    <div>
                      <strong>{{ regra.fornecedorNome || cnpj(regra.cnpjFornecedor) }}</strong>
                      <span class="pv-categorias__seta" aria-hidden="true">→</span>
                      {{ regra.forma }}
                      @if (saiSozinho(regra.forma)) {
                        <span class="badge badge-info pv-forma__selo">sai sozinho</span>
                      }
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
                  <label class="pv-rotulo" for="regra-forma-fornecedor">Fornecedor</label>
                  <span class="pv-campo-grupo bmd-form-group">
                    <select id="regra-forma-fornecedor" class="pv-campo pv-categorias__select" [value]="fornecedor()" (change)="fornecedor.set(valor($event))">
                      <option value="">Escolha</option>
                      @for (item of atual.fornecedores; track item.cnpj) {
                        <option [value]="item.cnpj" [selected]="item.cnpj === fornecedor()">{{ rotuloFornecedor(item) }}</option>
                      }
                    </select>
                  </span>
                </div>
                <div class="pv-resumo__campo">
                  <label class="pv-rotulo" for="regra-forma">Forma de pagamento</label>
                  <span class="pv-campo-grupo bmd-form-group">
                    <select id="regra-forma" class="pv-campo pv-categorias__select" [value]="formaRegra()" (change)="formaRegra.set(valor($event))">
                      <option value="">Escolha</option>
                      @for (opcao of atual.opcoes; track opcao.nome) {
                        <option [value]="opcao.nome" [selected]="opcao.nome === formaRegra()">{{ rotuloOpcao(opcao) }}</option>
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
                As formas são escolhidas pelo nome: cada empresa usa a forma com esse nome no Bling dela.
                @if (atual.gruposSaiSozinho.length > 0) {
                  {{ textoSaiSozinho() }}
                }
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
    </section>
  `
})
export class FormasPagamentoComponent {
  private readonly api = inject(PrevisioService);

  readonly clienteId = input.required<number>();

  readonly dados = signal<ConfiguracaoFormasPagamento | null>(null);
  readonly padrao = signal('');
  readonly fornecedor = signal('');
  readonly formaRegra = signal('');
  readonly acao = signal<AcaoFormas | null>(null);
  readonly mensagem = signal('');
  readonly erro = signal('');

  readonly podeAdicionar = computed(() => this.fornecedor() !== '' && this.formaRegra() !== '');

  /** Primeira opção da forma padrão: a automática (quando ligada) ou sem forma. */
  readonly rotuloSemPadrao = computed(() => {
    const automatica = this.dados()?.padraoAutomatico;
    if (automatica) {
      return 'Automática (' + automatica + ')';
    }
    return 'Sem forma';
  });

  readonly textoSaiSozinho = computed(() => {
    const grupos = this.dados()?.gruposSaiSozinho ?? [];
    return grupos.join(', ') + ' sai sozinho da conta: aparece separado no painel, em Contas a pagar e no e-mail de vencimentos.';
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

  /** "Boleto Itaú (Boleto)": o nome no Bling e, quando diferente, o grupo do Previsio. */
  rotuloOpcao(opcao: OpcaoFormaPagamento): string {
    if (opcao.nome.toLowerCase() === opcao.grupoNome.toLowerCase()) {
      return opcao.nome;
    }
    return opcao.nome + ' (' + opcao.grupoNome + ')';
  }

  saiSozinho(nome: string): boolean {
    const opcao = (this.dados()?.opcoes ?? []).find(o => o.nome.toLowerCase() === nome.toLowerCase());
    return opcao !== undefined && opcao.saiSozinho;
  }

  salvarPadrao(): void {
    this.executar('padrao', this.api.alterarFormaPagamentoPadrao(this.clienteId(), this.padrao()), () => {
      if (this.padrao() === '') {
        const automatica = this.dados()?.padraoAutomatico;
        if (automatica) {
          return 'Padrão automática: notas sem regra, histórico ou forma na nota vão com ' + automatica + '.';
        }
        return 'Sem forma padrão: notas sem regra, histórico ou forma na nota vão sem forma de pagamento.';
      }
      return 'Forma padrão salva.';
    });
  }

  adicionar(): void {
    const regra = { cnpjFornecedor: this.fornecedor(), forma: this.formaRegra() };
    this.executar('regra', this.api.salvarRegraFormaPagamento(this.clienteId(), regra), () => {
      this.fornecedor.set('');
      this.formaRegra.set('');
      return 'Regra salva. Vale para as próximas notas lançadas.';
    });
  }

  remover(regra: RegraFormaPagamento): void {
    this.executar('remover', this.api.removerRegraFormaPagamento(this.clienteId(), regra.id), () => 'Regra removida.');
  }

  private executar(
    acao: AcaoFormas,
    chamada: ReturnType<PrevisioService['obterFormasPagamento']>,
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
        this.erro.set(mensagemDaApi(erro, 'Não consegui salvar as formas de pagamento.'));
      }
    });
  }

  private aplicar(dados: ConfiguracaoFormasPagamento): void {
    this.dados.set(dados);
    this.padrao.set(dados.formaPadrao ?? '');
  }

  private carregar(clienteId: number): void {
    this.dados.set(null);
    this.mensagem.set('');
    this.erro.set('');
    this.api.obterFormasPagamento(clienteId).subscribe({
      next: dados => this.aplicar(dados),
      error: erro => this.erro.set(mensagemDaApi(erro, 'Não consegui carregar as formas de pagamento.'))
    });
  }
}
