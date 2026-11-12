import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { ClientesService } from '../../core/clientes.service';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { Cliente, NovoCliente, ResultadoTesteEmail, TesteEmail } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../../shared/carregando.directive';

/**
 * Etapa 1: nome da rede e a caixa de e-mail dedicada onde chegam as notas.
 * A rede só é cadastrada depois que o teste da caixa passar com os dados digitados.
 * Com a rede já cadastrada, mostra o resumo e permite testar a caixa com a senha gravada.
 */
@Component({
  selector: 'app-etapa-rede',
  imports: [FormsModule, RouterLink, CarregandoDirective],
  templateUrl: './etapa-rede.component.html'
})
export class EtapaRedeComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly clientes = inject(ClientesService);

  readonly cliente = input<Cliente | null>(null);
  readonly cadastrada = output<Cliente>();
  readonly continuar = output<void>();

  readonly nome = signal('');
  readonly imapHost = signal('imap.gmail.com');
  readonly imapPorta = signal<number | null>(993);
  readonly imapSsl = signal(true);
  // Só no cadastro: a rede nova começa no teste grátis
  readonly emTeste = signal(true);
  readonly imapUsuario = signal('');
  readonly imapSenha = signal('');
  readonly clientId = signal('');
  readonly clientSecret = signal('');

  readonly testando = signal(false);
  readonly salvando = signal(false);
  readonly erro = signal('');
  readonly teste = signal<ResultadoTesteEmail | null>(null);

  // Dados da caixa no momento do último teste que passou; mudar qualquer campo exige testar de novo
  private readonly caixaTestada = signal('');
  private readonly caixaAtual = computed(() => [
    this.imapHost().trim().toLowerCase(),
    String(this.imapPorta()),
    String(this.imapSsl()),
    this.imapUsuario().trim().toLowerCase(),
    this.imapSenha()
  ].join('|'));

  readonly testeValido = computed(() => {
    const teste = this.teste();
    return teste !== null && teste.conectou && this.caixaTestada() === this.caixaAtual();
  });

  testar(): void {
    const cliente = this.cliente();
    let dados: TesteEmail;
    if (cliente !== null) {
      // Rede já cadastrada: testa a caixa gravada, com a senha que está no Previsio
      dados = {
        imapHost: cliente.imapHost,
        imapPorta: cliente.imapPorta,
        imapSsl: cliente.imapSsl,
        imapUsuario: cliente.imapUsuario,
        clienteId: cliente.id
      };
    } else {
      if (!this.caixaPreenchida()) {
        return;
      }
      dados = {
        imapHost: this.imapHost().trim(),
        imapPorta: Number(this.imapPorta()),
        imapSsl: this.imapSsl(),
        imapUsuario: this.imapUsuario().trim(),
        imapSenha: this.imapSenha()
      };
    }

    const chave = this.caixaAtual();
    this.erro.set('');
    this.teste.set(null);
    this.testando.set(true);
    this.previsio.testarEmail(dados).subscribe({
      next: resultado => {
        this.testando.set(false);
        this.teste.set(resultado);
        if (resultado.conectou) {
          this.caixaTestada.set(chave);
        }
      },
      error: resposta => {
        this.testando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível testar a caixa de e-mail.'));
      }
    });
  }

  cadastrar(): void {
    const nome = this.nome().trim();
    if (nome === '') {
      this.erro.set('Informe o nome da rede.');
      return;
    }
    if (!this.caixaPreenchida()) {
      return;
    }
    if (!this.testeValido()) {
      this.erro.set('Teste a caixa de e-mail antes de continuar.');
      return;
    }

    const clientId = this.clientId().trim();
    const clientSecret = this.clientSecret().trim();
    if ((clientId === '') !== (clientSecret === '')) {
      this.erro.set('Informe o Client ID e o Client Secret do aplicativo do Bling da rede, ou deixe os dois vazios.');
      return;
    }

    const dados: NovoCliente = {
      nome: nome,
      imapHost: this.imapHost().trim(),
      imapPorta: Number(this.imapPorta()),
      imapSsl: this.imapSsl(),
      imapUsuario: this.imapUsuario().trim(),
      imapSenha: this.imapSenha(),
      emTeste: this.emTeste()
    };
    if (clientId !== '') {
      dados.blingClientId = clientId;
      dados.blingClientSecret = clientSecret;
    }

    this.erro.set('');
    this.salvando.set(true);
    this.clientes.criar(dados).subscribe({
      next: cliente => this.cadastrada.emit(cliente),
      error: resposta => {
        this.salvando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível cadastrar a rede.'));
      }
    });
  }

  private caixaPreenchida(): boolean {
    const porta = Number(this.imapPorta());
    if (this.imapHost().trim() === '' || this.imapUsuario().trim() === '' || this.imapSenha().trim() === '') {
      this.erro.set('Informe o servidor, o e-mail e a senha de app da caixa dedicada.');
      return false;
    }
    if (!Number.isInteger(porta) || porta < 1 || porta > 65535) {
      this.erro.set('Informe uma porta entre 1 e 65535 (no Gmail, 993).');
      return false;
    }
    return true;
  }
}
