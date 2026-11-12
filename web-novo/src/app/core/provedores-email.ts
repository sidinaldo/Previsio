/**
 * Provedores de e-mail mais comuns, para preencher servidor, porta e SSL da caixa que recebe as notas.
 * "outro" mantém o que foi digitado (hospedagens com domínio próprio, por exemplo).
 */
export interface ProvedorEmail {
  id: string;
  nome: string;
  host: string;
  porta: number;
  ssl: boolean;
  // Instrução de senha mostrada abaixo do select
  ajuda: string;
  // false: o provedor não aceita login por senha no IMAP (só pelo encaminhamento)
  suportado: boolean;
}

export const PROVEDOR_OUTRO = 'outro';

export const PROVEDORES_EMAIL: ProvedorEmail[] = [
  {
    id: 'gmail', nome: 'Gmail ou Google Workspace', host: 'imap.gmail.com', porta: 993, ssl: true, suportado: true,
    ajuda: 'Ligue a verificação em duas etapas na Conta Google e gere uma senha de app (Conta Google › Segurança › Senhas de app). Use essa senha aqui, não a senha normal.'
  },
  {
    id: 'microsoft', nome: 'Outlook, Hotmail ou Microsoft 365', host: 'outlook.office365.com', porta: 993, ssl: true, suportado: false,
    ajuda: ''
  },
  {
    id: 'yahoo', nome: 'Yahoo', host: 'imap.mail.yahoo.com', porta: 993, ssl: true, suportado: true,
    ajuda: 'Gere uma senha de app nas configurações de segurança da conta Yahoo e use essa senha aqui.'
  },
  {
    id: 'locaweb', nome: 'Locaweb', host: 'email-ssl.com.br', porta: 993, ssl: true, suportado: true,
    ajuda: 'Use o e-mail completo e a senha da caixa.'
  },
  {
    id: 'hostinger', nome: 'Hostinger', host: 'imap.hostinger.com', porta: 993, ssl: true, suportado: true,
    ajuda: 'Use o e-mail completo e a senha da caixa. Se não conectar, confira o servidor no hPanel (E-mails › Configurações).'
  },
  {
    id: 'zoho', nome: 'Zoho Mail', host: 'imap.zoho.com', porta: 993, ssl: true, suportado: true,
    ajuda: 'Ative o IMAP nas configurações do Zoho. Com verificação em duas etapas ligada, use uma senha de app.'
  }
];

export const AJUDA_OUTRO =
  'Use o servidor IMAP informado pela hospedagem (HostGator, KingHost, UOL Host e outras costumam usar mail.seudominio.com.br), porta 993 com SSL, o e-mail completo e a senha da caixa.';

/** Provedor que corresponde ao servidor digitado; "outro" quando não é nenhum da lista. */
export function provedorDoHost(host: string | null | undefined): string {
  const texto = (host ?? '').trim().toLowerCase();
  if (texto === '') {
    return PROVEDOR_OUTRO;
  }
  const achado = PROVEDORES_EMAIL.find(p => p.host === texto);
  if (achado) {
    return achado.id;
  }
  if (texto === 'outlook.office.com' || texto === 'imap-mail.outlook.com') {
    return 'microsoft';
  }
  return PROVEDOR_OUTRO;
}

export function provedorPorId(id: string): ProvedorEmail | undefined {
  return PROVEDORES_EMAIL.find(p => p.id === id);
}
