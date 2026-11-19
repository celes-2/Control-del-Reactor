namespace Aplicacion.Puertos;

/// <summary>Codigo que GitHub entrega al iniciar el device flow.</summary>
public sealed record CodigoDispositivo(
    string DeviceCode, string UserCode, string VerificationUri, int ExpiraEnSegundos, int IntervaloSegundos);

/// <summary>Cuenta del jugador en el proveedor externo: solo lo necesario para vincularla.</summary>
public sealed record IdentidadExterna(string IdExterno, string Login, string? CorreoVerificado);

/// <summary>Respuesta de GitHub al preguntar si el jugador ya autorizo.</summary>
public enum EstadoAutorizacion
{
    Pendiente,
    MasLento,
    Autorizado,
    Denegado,
    Expirado,
    Error
}

public sealed record ResultadoAutorizacion(EstadoAutorizacion Estado, IdentidadExterna? Identidad = null);

/// <summary>Puerto de salida hacia GitHub (OAuth device flow). La implementacion vive en Infraestructura.</summary>
public interface IProveedorGitHub
{
    /// <summary>False si el servidor no tiene el ClientId de la OAuth App.</summary>
    bool EstaConfigurado { get; }

    /// <summary>Pide a GitHub un codigo para el jugador. Devuelve null si GitHub no responde bien.</summary>
    Task<CodigoDispositivo?> IniciarAsync(CancellationToken ct = default);

    /// <summary>
    /// Pregunta a GitHub si el jugador ya autorizo. Si autorizo, lee su id, nombre y correo verificado.
    /// El token de GitHub solo se usa para esa lectura y se descarta: no se guarda.
    /// </summary>
    Task<ResultadoAutorizacion> ConsultarAsync(string deviceCode, CancellationToken ct = default);
}

/// <summary>Vinculo entre una cuenta externa (por ejemplo GitHub) y una cuenta interna. Evita duplicados (CA-29.6).</summary>
public interface ICuentaExternaRepo
{
    Task<Guid?> BuscarUsuarioAsync(string proveedor, string idExterno, CancellationToken ct = default);

    /// <summary>Asocia la cuenta externa al usuario. Si ya estaba asociada, la reemplaza (nunca quedan dos).</summary>
    Task VincularAsync(string proveedor, string idExterno, Guid usuarioId, CancellationToken ct = default);
}
