using Dominio.Comun;

namespace Dominio.Usuarios;

/// <summary>Relación control físico (deviceId) -> jugador. Evita mezclar controles (CA-10.7).</summary>
public sealed class DispositivoAsignado
{
    public string DeviceId { get; }
    public Guid UserId { get; }
    public DateTime VistoEn { get; private set; }

    public DispositivoAsignado(string deviceId, Guid userId, DateTime vistoEn)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new DominioException("El identificador del dispositivo es obligatorio.");

        DeviceId = deviceId.Trim();
        UserId = userId;
        VistoEn = vistoEn;
    }

    public void MarcarVisto(DateTime ahoraUtc) => VistoEn = ahoraUtc;
}
