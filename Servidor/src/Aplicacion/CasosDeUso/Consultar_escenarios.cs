using Dominio.Juego;

namespace Aplicacion.CasosDeUso;

public sealed record PosicionVista(int X, int Y);

/// <summary>
/// Un escenario del catálogo, para que el cliente lo muestre antes de crear la partida (US-23).
/// Filas va en el formato que Unity ya entiende (W . S R T): las puertas salen como su estado inicial
/// (W cerrada, . abierta) y los inicios de guardián como piso. Las puertas y los inicios de guardián
/// también se listan aparte, por si el cliente quiere dibujarlos.
/// </summary>
public sealed record EscenarioVista(
    string Id,
    string Nombre,
    string Descripcion,
    int Ancho,
    int Alto,
    IReadOnlyList<string> Filas,
    IReadOnlyList<PosicionVista> Puertas,
    IReadOnlyList<PosicionVista> InicioGuardianes,
    int MaxJugadores,
    int MinGuardianes,
    int MaxGuardianes,
    int GuardianesPorDefecto);

/// <summary>Consulta: los escenarios que se pueden elegir al crear una partida.</summary>
public sealed class ListarEscenarios
{
    public IReadOnlyList<EscenarioVista> Ejecutar() =>
        Escenarios.Catalogo.Select(Desde).ToList();

    private static EscenarioVista Desde(EscenarioDef definicion)
    {
        var arena = Arena.DesdeFilas(definicion.Filas);

        return new EscenarioVista(
            definicion.Id,
            definicion.Nombre,
            definicion.Descripcion,
            arena.Ancho,
            arena.Alto,
            arena.ComoFilas(),
            arena.Puertas
                .OrderBy(p => p.Posicion.Y).ThenBy(p => p.Posicion.X)
                .Select(p => new PosicionVista(p.Posicion.X, p.Posicion.Y))
                .ToList(),
            arena.InicioGuardianes.Select(g => new PosicionVista(g.X, g.Y)).ToList(),
            arena.CantidadPuntosDeAparicion,
            ConfigPartida.MinimoGuardianes,
            ConfigPartida.MaximoGuardianes,
            new ConfigPartida().CantidadGuardianes);
    }
}
