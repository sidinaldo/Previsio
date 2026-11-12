import { PROVEDOR_OUTRO, provedorDoHost, provedorPorId } from './provedores-email';

describe('provedores de e-mail', () => {
  it('reconhece o provedor pelo servidor, sem diferença de maiúsculas e espaços', () => {
    expect(provedorDoHost('imap.gmail.com')).toBe('gmail');
    expect(provedorDoHost(' IMAP.Mail.Yahoo.com ')).toBe('yahoo');
    expect(provedorDoHost('email-ssl.com.br')).toBe('locaweb');
    expect(provedorDoHost('outlook.office365.com')).toBe('microsoft');
    expect(provedorDoHost('imap-mail.outlook.com')).toBe('microsoft');
  });

  it('servidor vazio ou fora da lista é "outro"', () => {
    expect(provedorDoHost('')).toBe(PROVEDOR_OUTRO);
    expect(provedorDoHost(null)).toBe(PROVEDOR_OUTRO);
    expect(provedorDoHost('mail.autocenter.com.br')).toBe(PROVEDOR_OUTRO);
  });

  it('só a Microsoft fica marcada como não suportada', () => {
    expect(provedorPorId('microsoft')?.suportado).toBe(false);
    expect(provedorPorId('gmail')?.suportado).toBe(true);
    expect(provedorPorId(PROVEDOR_OUTRO)).toBeUndefined();
  });
});
