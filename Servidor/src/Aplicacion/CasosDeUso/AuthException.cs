namespace Aplicacion.CasosDeUso;

/// <summary>Error de autenticación/registro con un código estable que Unity puede leer.</summary>
public sealed class AuthException(string codigo, string mensaje) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
}
