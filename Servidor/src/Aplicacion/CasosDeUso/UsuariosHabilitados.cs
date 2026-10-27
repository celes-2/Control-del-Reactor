using Aplicacion.Puertos;
using Dominio.Comun;

namespace Aplicacion.CasosDeUso;

internal sealed record JugadorHabilitado(Guid Id, string Nombre, Guid AvatarId);

internal static class UsuariosHabilitados
{
    /// <summary>
    /// Regla común para crear o unirse a una partida: la cuenta debe existir,
    /// tener el correo verificado y haber elegido un avatar.
    /// </summary>
    public static async Task<JugadorHabilitado> ObtenerJugadorHabilitadoAsync(
        this IUsuarioRepo usuarios, Guid usuarioId, CancellationToken ct)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId, ct)
            ?? throw new NoEncontradoException("El usuario no existe.");

        if (!usuario.Verificado)
            throw new DominioException("Debe verificar su correo antes de jugar.");

        if (usuario.AvatarId is not { } avatarId)
            throw new DominioException("Debe elegir un avatar antes de jugar.");

        return new JugadorHabilitado(usuario.Id, usuario.Nombre, avatarId);
    }
}
