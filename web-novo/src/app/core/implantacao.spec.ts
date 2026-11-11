import {
  SituacaoImplantacao,
  etapaConcluida,
  formatarCnpj,
  podeAbrirEtapa,
  primeiraEtapaPendente,
  proximaEtapa,
  usuariosDoCliente
} from './implantacao';
import { Cliente, Empresa, Usuario } from './previsio.models';

function cliente(): Cliente {
  return {
    id: 7, nome: 'Rede Alfa', imapHost: 'imap.gmail.com', imapPorta: 993, imapSsl: true,
    imapUsuario: 'notas@alfa.com.br', blingClientId: null, blingClientSecretCadastrado: false, ativo: true
  };
}

function empresa(id: number, conectada: boolean, ativa: boolean = true): Empresa {
  return {
    id: id, nome: 'Loja ' + id, cnpj: '1122233300018' + id, blingConectado: conectada, blingClientId: null,
    blingClientSecretCadastrado: false, origemAplicativoBling: 'Padrao', emailProprio: false, imapHost: '',
    imapPorta: 993, imapSsl: true, imapUsuario: '', imapSenhaCadastrada: false, ultimaSincronizacaoEm: null, ativo: ativa
  };
}

function usuario(clienteId: number | null, perfil: string): Usuario {
  return { id: 1, clienteId: clienteId, nome: 'Ana', email: 'ana@alfa.com.br', perfil: perfil, ativo: true, ultimoAcessoEm: null };
}

function situacao(parcial: Partial<SituacaoImplantacao>): SituacaoImplantacao {
  return { cliente: cliente(), empresas: [], usuarios: [], notasLidas: 0, ...parcial };
}

describe('implantacao', () => {
  it('sem rede: começa pela rede e as outras etapas ficam fechadas', () => {
    const s = situacao({ cliente: null });
    expect(primeiraEtapaPendente(s)).toBe('rede');
    expect(podeAbrirEtapa(s, 'rede')).toBe(true);
    expect(podeAbrirEtapa(s, 'empresas')).toBe(false);
  });

  it('rede sem empresas: continua em Empresas', () => {
    expect(primeiraEtapaPendente(situacao({}))).toBe('empresas');
  });

  it('empresa inativa não conta como cadastrada', () => {
    expect(etapaConcluida(situacao({ empresas: [empresa(1, true, false)] }), 'empresas')).toBe(false);
  });

  it('Bling só conclui com todas as empresas ativas conectadas', () => {
    expect(primeiraEtapaPendente(situacao({ empresas: [empresa(1, true), empresa(2, false)] }))).toBe('bling');
    expect(etapaConcluida(situacao({ empresas: [empresa(1, true), empresa(2, false, false)] }), 'bling')).toBe(true);
  });

  it('usuário: só conta perfil Cliente desta rede', () => {
    const s = situacao({ empresas: [empresa(1, true)], usuarios: [usuario(null, 'Admin'), usuario(8, 'Cliente')] });
    expect(usuariosDoCliente(s).length).toBe(0);
    expect(primeiraEtapaPendente(s)).toBe('usuario');
  });

  it('tudo feito menos notas: primeira leitura; com notas, continua na leitura para ver o resultado', () => {
    const base = { empresas: [empresa(1, true)], usuarios: [usuario(7, 'Cliente')] };
    expect(primeiraEtapaPendente(situacao(base))).toBe('leitura');
    expect(etapaConcluida(situacao({ ...base, notasLidas: 3 }), 'leitura')).toBe(true);
    expect(primeiraEtapaPendente(situacao({ ...base, notasLidas: 3 }))).toBe('leitura');
  });

  it('próxima etapa segue a ordem e para na última', () => {
    expect(proximaEtapa('rede')).toBe('empresas');
    expect(proximaEtapa('usuario')).toBe('leitura');
    expect(proximaEtapa('leitura')).toBe('leitura');
  });

  it('formata o CNPJ', () => {
    expect(formatarCnpj('11222333000181')).toBe('11.222.333/0001-81');
    expect(formatarCnpj('123')).toBe('123');
  });
});
