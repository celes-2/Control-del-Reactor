using System.Reflection;
using System.Text.Json;
using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Un escenario seleccionable (US-23). Las reglas son las mismas en todos; cambian los muros, las puertas,
/// los puntos de energía y cuántas trampas se reparten al azar.
/// </summary>
/// <param name="Filas">El mapa: W muro, . piso, S inicio de jugador, R reactor, D puerta automática,
/// G inicio de guardián, E punto de energía, t celda preferida para trampas, T trampa fija.</param>
/// <param name="TrampasAleatorias">Cuántas trampas se reparten al azar al iniciar (mínimo 3).</param>
/// <param name="DistanciaMinimaEntreTrampas">Pasos mínimos entre dos trampas aleatorias.</param>
public sealed record EscenarioDef(
    string Id,
    string Nombre,
    string Descripcion,
    IReadOnlyList<string> Filas,
    int TrampasAleatorias = ReglasArenaViva.MinimoTrampas,
    int DistanciaMinimaEntreTrampas = 2)
{
    public int Ancho => Filas[0].Length;
    public int Alto => Filas.Count;

    /// <summary>La arena de este escenario, lista para crear la partida.</summary>
    public Arena CrearArena() => Arena.DesdeFilas(Filas);

    /// <summary>
    /// La configuración con los datos de este escenario: su id (null si es el clásico) y sus trampas aleatorias.
    /// El resto de la configuración (tiempo, guardianes, etc.) queda como venía.
    /// </summary>
    public ConfigPartida Aplicar(ConfigPartida config) => config with
    {
        EscenarioId = Id == Escenarios.IdClasico ? null : Id,
        ArenaViva = config.ArenaViva with
        {
            TrampasAleatorias = TrampasAleatorias,
            DistanciaMinimaEntreTrampas = DistanciaMinimaEntreTrampas
        }
    };
}

/// <summary>
/// Catálogo de escenarios (US-23). Cada escenario es un archivo JSON en Dominio/Juego/Escenarios/, que viaja
/// dentro de Dominio.dll: para agregar o cambiar uno basta con editar su JSON y volver a compilar.
/// Al cargarlos se validan (ver <see cref="ValidadorEscenario"/>): si alguno está mal, el servidor no arranca.
/// Sin elegir escenario se usa el mapa clásico de 15x11, que sigue en el código.
/// </summary>
public static class Escenarios
{
    public const string IdClasico = "clasico";

    /// <summary>Mapa de 15x11, el mismo que usa el servidor falso de Unity. Se usa cuando no se elige escenario.</summary>
    public static IReadOnlyList<string> Predeterminado { get; } =
    [
        "WWWWWWWWWWWWWWW",
        "WS...........SW",
        "W..WW.....WW..W",
        "W..W.......W..W",
        "W.............W",
        "W......R......W",
        "W.............W",
        "W..W.......W..W",
        "W..WW.....WW..W",
        "WS...........SW",
        "WWWWWWWWWWWWWWW"
    ];

    private static readonly EscenarioDef Clasico = new(
        IdClasico, "Arena clásica", "Arena abierta con muros en las esquinas interiores. Sin puertas.", Predeterminado);

    private static readonly Lazy<IReadOnlyList<EscenarioDef>> _catalogo = new(CargarCatalogo);

    /// <summary>Los escenarios que el jugador puede elegir al crear la partida, en el orden de su JSON.</summary>
    public static IReadOnlyList<EscenarioDef> Catalogo => _catalogo.Value;

    /// <summary>
    /// Busca un escenario por su id (sin distinguir mayúsculas). Sin id devuelve el mapa clásico.
    /// Lanza <see cref="DominioException"/> si el id no existe.
    /// </summary>
    public static EscenarioDef Obtener(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Clasico;

        var definicion = Catalogo.FirstOrDefault(e => string.Equals(e.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

        return definicion ?? throw new DominioException(
            $"Escenario desconocido: '{id}'. Disponibles: {string.Join(", ", Catalogo.Select(e => e.Id))}.");
    }

    // ------------------------------------------------------------------ carga desde JSON

    private sealed record EscenarioJson(
        string? Id,
        int Orden,
        string? Nombre,
        string? Descripcion,
        int? TrampasAleatorias,
        int? DistanciaMinimaEntreTrampas,
        string[]? Filas);

    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static IReadOnlyList<EscenarioDef> CargarCatalogo()
    {
        var ensamblado = typeof(Escenarios).Assembly;
        var recursos = ensamblado.GetManifestResourceNames()
            .Where(n => n.StartsWith("escenarios/", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .ToList();

        if (recursos.Count == 0)
            throw new DominioException("No se encontró ningún escenario (Dominio/Juego/Escenarios/*.json).");

        var leidos = recursos.Select(r => Leer(ensamblado, r)).OrderBy(e => e.Orden).ThenBy(e => e.Def.Id).ToList();

        var repetido = leidos.GroupBy(e => e.Def.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new DominioException($"Hay dos escenarios con el id '{repetido.Key}'.");

        return leidos.Select(e => e.Def).ToList();
    }

    /// <summary>Lee y valida un escenario. Público para poder probar un JSON nuevo antes de agregarlo.</summary>
    public static EscenarioDef DesdeJson(string json, string origen = "escenario")
    {
        EscenarioJson? datos;
        try
        {
            datos = JsonSerializer.Deserialize<EscenarioJson>(json, Opciones);
        }
        catch (JsonException e)
        {
            throw new DominioException($"{origen}: el JSON no es válido ({e.Message}).");
        }

        return Convertir(datos, origen).Def;
    }

    private static (int Orden, EscenarioDef Def) Leer(Assembly ensamblado, string recurso)
    {
        using var flujo = ensamblado.GetManifestResourceStream(recurso)
            ?? throw new DominioException($"No se pudo abrir {recurso}.");
        using var lector = new StreamReader(flujo);
        var datos = JsonSerializer.Deserialize<EscenarioJson>(lector.ReadToEnd(), Opciones);
        return Convertir(datos, recurso);
    }

    private static (int Orden, EscenarioDef Def) Convertir(EscenarioJson? datos, string origen)
    {
        if (datos is null || string.IsNullOrWhiteSpace(datos.Id) || string.IsNullOrWhiteSpace(datos.Nombre) ||
            datos.Filas is null || datos.Filas.Length == 0)
            throw new DominioException($"{origen}: faltan id, nombre o filas.");

        if (string.Equals(datos.Id.Trim(), IdClasico, StringComparison.OrdinalIgnoreCase))
            throw new DominioException($"{origen}: el id '{IdClasico}' está reservado para el mapa por defecto.");

        var definicion = new EscenarioDef(
            datos.Id.Trim(),
            datos.Nombre.Trim(),
            datos.Descripcion?.Trim() ?? "",
            datos.Filas,
            datos.TrampasAleatorias ?? ReglasArenaViva.MinimoTrampas,
            datos.DistanciaMinimaEntreTrampas ?? 2);

        var errores = ValidadorEscenario.Revisar(definicion);
        if (errores.Count > 0)
            throw new DominioException($"{origen}: {string.Join(" ", errores)}");

        return (datos.Orden, definicion);
    }
}
