using Aplicacion.Puertos;

namespace Infraestructura.Persistencia;

/// <summary>Forma de un vínculo en cuentas-externas.json.</summary>
public sealed class CuentaExternaRegistro
{
    public string Proveedor { get; set; } = "";
    public string IdExterno { get; set; } = "";
    public Guid UsuarioId { get; set; }
}

/// <summary>
/// Adaptador de <see cref="ICuentaExternaRepo"/> sobre cuentas-externas.json.
/// Una cuenta externa apunta a un solo usuario interno (CA-29.6).
/// </summary>
public sealed class JsonCuentaExternaRepositorio(JsonFileStore<CuentaExternaRegistro> almacen) : ICuentaExternaRepo
{
    public async Task<Guid?> BuscarUsuarioAsync(string proveedor, string idExterno, CancellationToken ct = default) =>
        (await almacen.LeerAsync(ct)).FirstOrDefault(c => Mismo(c, proveedor, idExterno))?.UsuarioId;

    public Task VincularAsync(string proveedor, string idExterno, Guid usuarioId, CancellationToken ct = default) =>
        almacen.ModificarAsync(lista =>
        {
            lista.RemoveAll(c => Mismo(c, proveedor, idExterno));
            lista.Add(new CuentaExternaRegistro { Proveedor = proveedor, IdExterno = idExterno, UsuarioId = usuarioId });
        }, ct);

    private static bool Mismo(CuentaExternaRegistro c, string proveedor, string idExterno) =>
        string.Equals(c.Proveedor, proveedor, StringComparison.OrdinalIgnoreCase) && c.IdExterno == idExterno;
}
