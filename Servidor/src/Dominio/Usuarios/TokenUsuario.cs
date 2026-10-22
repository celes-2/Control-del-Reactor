namespace Dominio.Usuarios;

public enum TipoToken { EMAIL_VERIFY, PASSWORD_RESET }

/// <summary>
/// Código de un solo uso (verificar correo / recuperar contraseña). Solo guarda el hash del código.
/// </summary>
public sealed class TokenUsuario
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public TipoToken Tipo { get; }
    public string TokenHash { get; }
    public DateTime ExpiraEn { get; }
    public DateTime? UsadoEn { get; private set; }

    public TokenUsuario(Guid userId, TipoToken tipo, string tokenHash, DateTime expiraEn)
        : this(Guid.NewGuid(), userId, tipo, tokenHash, expiraEn, null)
    {
    }

    private TokenUsuario(Guid id, Guid userId, TipoToken tipo, string tokenHash, DateTime expiraEn, DateTime? usadoEn)
    {
        Id = id;
        UserId = userId;
        Tipo = tipo;
        TokenHash = tokenHash;
        ExpiraEn = expiraEn;
        UsadoEn = usadoEn;
    }

    public static TokenUsuario Reconstruir(Guid id, Guid userId, TipoToken tipo, string tokenHash,
        DateTime expiraEn, DateTime? usadoEn) => new(id, userId, tipo, tokenHash, expiraEn, usadoEn);

    public bool EstaVigente(DateTime ahoraUtc) => UsadoEn is null && ExpiraEn > ahoraUtc;

    public void Usar(DateTime ahoraUtc) => UsadoEn = ahoraUtc;
}
