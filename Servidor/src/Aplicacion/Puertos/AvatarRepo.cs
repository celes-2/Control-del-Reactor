using Dominio.Avatares;

namespace Aplicacion.Puertos;

/// <summary>Puerto de salida para consultar los avatares disponibles.</summary>
public interface IAvatarRepo
{
    Task<IReadOnlyList<Avatar>> ListarAsync(CancellationToken ct = default);

    Task<Avatar?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
}
