using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Dominio.Comun;
using Dominio.Juego;
using Dominio.Usuarios;
using Infraestructura.Partidas;
using Xunit;

namespace PruebasAplicacion;

/// <summary>
/// Sprint 3, Paquete B (arena viva) en la capa de aplicación: elegir escenario y cantidad de guardianes al crear,
/// activar la arena viva al iniciar (con azar inyectable) y devolver lo que pasó en cada acción.
/// Las pruebas usan 4 guardianes (valor por defecto, CA-21.1) salvo las que revisan el rango 2 a 4.
/// </summary>
public class ArenaVivaCasosDeUsoTests
{
    private sealed class UsuariosEnMemoria : IUsuarioRepo
    {
        private readonly Dictionary<Guid, Usuario> _usuarios = new();

        public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_usuarios.GetValueOrDefault(id));

        public Task<Usuario?> ObtenerPorUsernameAsync(string username, CancellationToken ct = default) =>
            Task.FromResult(_usuarios.Values.FirstOrDefault(u => u.Nombre == username));

        public Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken ct = default) =>
            Task.FromResult(_usuarios.Values.FirstOrDefault(u => u.Correo == correo));

        public Task GuardarAsync(Usuario usuario, CancellationToken ct = default)
        {
            _usuarios[usuario.Id] = usuario;
            return Task.CompletedTask;
        }
    }

    private sealed class Contexto
    {
        private readonly UsuariosEnMemoria _usuarios = new();

        public IPartidaRepo Partidas { get; } = new PartidaEnMemoriaRepositorio();
        public ConfigurarPartida Configurar { get; }
        public UnirsePartida Unirse { get; }
        public IniciarPartida Iniciar { get; }
        public ObtenerPartida Obtener { get; }
        public EjecutarAccion Ejecutar { get; }

        public Contexto(int semilla = 1)
        {
            Configurar = new ConfigurarPartida(_usuarios, Partidas);
            Unirse = new UnirsePartida(_usuarios, Partidas);
            Iniciar = new IniciarPartida(Partidas, new AzarConSemilla(semilla));
            Obtener = new ObtenerPartida(Partidas);
            Ejecutar = new EjecutarAccion(Partidas);
        }

        public async Task<Usuario> UsuarioAsync(string nombre)
        {
            var usuario = new Usuario(Guid.NewGuid(), nombre, $"{nombre.ToLowerInvariant()}@correo.com", "hash");
            usuario.MarcarVerificado();
            usuario.ElegirAvatar(Guid.NewGuid());
            await _usuarios.GuardarAsync(usuario);
            return usuario;
        }

        /// <summary>Crea la partida con Ana de anfitriona, une a Beto y la inicia.</summary>
        public async Task<(PartidaVista vista, Usuario ana, Usuario beto)> EnCursoAsync(
            ConfigPartida? config = null, IReadOnlyList<Obstaculo>? obstaculos = null)
        {
            var ana = await UsuarioAsync("Ana");
            var beto = await UsuarioAsync("Beto");
            var creada = await Configurar.EjecutarAsync(
                new CrearPartidaComando(ana.Id, config ?? new ConfigPartida(), obstaculos));
            await Unirse.EjecutarAsync(new UnirsePartidaComando(creada.Id, beto.Id));
            var vista = await Iniciar.EjecutarAsync(new IniciarPartidaComando(creada.Id, ana.Id));
            return (vista, ana, beto);
        }
    }

    // ---------- Crear: escenario (US-23) ----------

    [Theory]
    [InlineData("laboratorio")]
    [InlineData("estacion")]
    [InlineData("militar")]
    [Trait("CA", "23.3")]
    public async Task Configurar_ConEscenario_CargaEseMapaAntesDeEmpezar(string id)
    {
        var ctx = new Contexto();
        var ana = await ctx.UsuarioAsync("Ana");

        var vista = await ctx.Configurar.EjecutarAsync(
            new CrearPartidaComando(ana.Id, new ConfigPartida { EscenarioId = id }));

        var esperado = Arena.DesdeFilas(Escenarios.Obtener(id).Filas);
        Assert.Equal(EstadoPartida.Espera, vista.Estado);
        Assert.Equal(id, vista.EscenarioId);
        Assert.Equal(esperado.ComoFilas().ToArray(), vista.Escenario.ToArray());
        Assert.Equal((esperado.Ancho, esperado.Alto), (vista.Ancho, vista.Alto));
        Assert.Contains(vista.Obstaculos, o => o.Tipo == TipoObstaculo.Puerta);
        Assert.Null(vista.ArenaViva);   // la arena viva empieza al iniciar
    }

    [Fact]
    [Trait("CA", "23.3")]
    public async Task Configurar_GuardaElIdDelCatalogoAunqueVengaConMayusculas()
    {
        var ctx = new Contexto();
        var ana = await ctx.UsuarioAsync("Ana");

        var vista = await ctx.Configurar.EjecutarAsync(
            new CrearPartidaComando(ana.Id, new ConfigPartida { EscenarioId = " Militar " }));

        Assert.Equal("militar", vista.EscenarioId);
    }

    [Fact]
    [Trait("CA", "23.8")]
    public async Task Configurar_SinEscenario_UsaElMapaClasicoComoAntes()
    {
        var ctx = new Contexto();
        var ana = await ctx.UsuarioAsync("Ana");

        var vista = await ctx.Configurar.EjecutarAsync(new CrearPartidaComando(ana.Id, new ConfigPartida()));

        Assert.Null(vista.EscenarioId);
        Assert.Equal(Escenarios.Predeterminado.ToArray(), vista.Escenario.ToArray());
    }

    [Fact]
    [Trait("CA", "23.2")]
    public async Task Configurar_ConEscenarioDesconocido_FallaYNoCreaLaPartida()
    {
        var ctx = new Contexto();
        var ana = await ctx.UsuarioAsync("Ana");

        await Assert.ThrowsAsync<DominioException>(() => ctx.Configurar.EjecutarAsync(
            new CrearPartidaComando(ana.Id, new ConfigPartida { EscenarioId = "playa" })));

        Assert.Null(await ctx.Partidas.BuscarPartidaDeJugadorAsync(ana.Id));
    }

    [Fact]
    [Trait("CA", "23.8")]
    public async Task Configurar_ConEscenarioYObstaculosALaVez_Falla()
    {
        var ctx = new Contexto();
        var ana = await ctx.UsuarioAsync("Ana");
        var muro = new Obstaculo(new Posicion(3, 3), TipoObstaculo.Muro);

        await Assert.ThrowsAsync<DominioException>(() => ctx.Configurar.EjecutarAsync(
            new CrearPartidaComando(ana.Id, new ConfigPartida { EscenarioId = "laboratorio" }, [muro])));
    }

    // ---------- Crear: cantidad de guardianes (US-21) ----------

    [Fact]
    [Trait("CA", "21.1")]
    public async Task Configurar_SinElegir_DejaCuatroGuardianes()
    {
        var ctx = new Contexto();
        var ana = await ctx.UsuarioAsync("Ana");

        var vista = await ctx.Configurar.EjecutarAsync(new CrearPartidaComando(ana.Id, new ConfigPartida()));

        Assert.Equal(4, vista.CantidadGuardianes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [Trait("CA", "21.1")]
    public async Task Configurar_GuardianesFueraDeDosACuatro_Falla(int cantidad)
    {
        var ctx = new Contexto();
        var ana = await ctx.UsuarioAsync("Ana");

        await Assert.ThrowsAsync<DominioException>(() => ctx.Configurar.EjecutarAsync(
            new CrearPartidaComando(ana.Id, new ConfigPartida { CantidadGuardianes = cantidad })));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [Trait("CA", "21.1")]
    public async Task Iniciar_CreaLosGuardianesElegidos(int cantidad)
    {
        var ctx = new Contexto();

        var (vista, _, _) = await ctx.EnCursoAsync(
            new ConfigPartida { EscenarioId = "laboratorio", CantidadGuardianes = cantidad });

        Assert.NotNull(vista.ArenaViva);
        Assert.Equal(cantidad, vista.ArenaViva.Guardianes.Count);
    }

    // ---------- Iniciar: arena viva ----------

    [Fact]
    [Trait("CA", "21.1")]
    [Trait("CA", "20.9")]
    public async Task Iniciar_ActivaLaArenaVivaConCuatroGuardianesEnLosExtremosYTresTrampasDistintas()
    {
        var ctx = new Contexto();

        var (vista, _, _) = await ctx.EnCursoAsync(new ConfigPartida { EscenarioId = "estacion" });

        Assert.Equal(EstadoPartida.EnCurso, vista.Estado);
        var viva = Assert.IsType<ArenaVivaVista>(vista.ArenaViva);
        var mapa = Arena.DesdeFilas(Escenarios.Obtener("estacion").Filas);
        var extremos = mapa.InicioGuardianes.Select(p => (p.X, p.Y)).ToHashSet();
        Assert.Equal(extremos, viva.Guardianes.Select(g => (g.X, g.Y)).ToHashSet());
        Assert.All(viva.Guardianes, g => Assert.False(g.Temporal));

        // Las trampas fijas del mapa (T) siguen ahí, y se suman 3 al azar: una de cada variante.
        var fijas = mapa.Obstaculos.Where(o => o.Tipo == TipoObstaculo.Trampa).Select(o => (o.Posicion.X, o.Posicion.Y)).ToHashSet();
        var alAzar = viva.Trampas.Where(t => !fijas.Contains((t.X, t.Y))).ToList();
        Assert.Equal(fijas.Count + 3, viva.Trampas.Count);
        Assert.Equal(3, alAzar.Count);
        Assert.Equal(3, alAzar.Select(t => t.Variante).Distinct().Count());
        Assert.NotEmpty(viva.Puertas);
        Assert.Equal(1, viva.MultiplicadorPuntos);
        Assert.Equal(viva.Trampas.Count, vista.Obstaculos.Count(o => o.Tipo == TipoObstaculo.Trampa && o.Variante is not null));
    }

    [Fact]
    [Trait("CA", "20.9")]
    public async Task Iniciar_ConElMismoAzar_ColocaLasTrampasEnElMismoLugar()
    {
        var (a, _, _) = await new Contexto(semilla: 7).EnCursoAsync(new ConfigPartida { EscenarioId = "militar" });
        var (b, _, _) = await new Contexto(semilla: 7).EnCursoAsync(new ConfigPartida { EscenarioId = "militar" });

        Assert.Equal(a.ArenaViva!.Trampas.ToArray(), b.ArenaViva!.Trampas.ToArray());
    }

    [Fact]
    [Trait("CA", "20.9")]
    public async Task Iniciar_SinAzarInyectado_TambienActivaLaArenaViva()
    {
        var ctx = new Contexto();
        var ana = await ctx.UsuarioAsync("Ana");
        var beto = await ctx.UsuarioAsync("Beto");
        var creada = await ctx.Configurar.EjecutarAsync(new CrearPartidaComando(ana.Id, new ConfigPartida()));
        await ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(creada.Id, beto.Id));
        var iniciar = new IniciarPartida(ctx.Partidas);   // como en producción

        var vista = await iniciar.EjecutarAsync(new IniciarPartidaComando(creada.Id, ana.Id));

        Assert.NotNull(vista.ArenaViva);
        Assert.Equal(4, vista.ArenaViva.Guardianes.Count);
    }

    [Fact]
    [Trait("CA", "23.7")]
    public async Task Iniciar_DosVeces_FallaYElEscenarioNoCambia()
    {
        var ctx = new Contexto();
        var (vista, ana, _) = await ctx.EnCursoAsync(new ConfigPartida { EscenarioId = "laboratorio" });

        await Assert.ThrowsAsync<DominioException>(() =>
            ctx.Iniciar.EjecutarAsync(new IniciarPartidaComando(vista.Id, ana.Id)));

        var despues = await ctx.Obtener.EjecutarAsync(vista.Id);
        Assert.Equal("laboratorio", despues.EscenarioId);
        Assert.Equal(vista.ArenaViva!.Trampas.ToArray(), despues.ArenaViva!.Trampas.ToArray());
    }

    // ---------- Ejecutar: novedades de la acción ----------

    [Fact]
    [Trait("CA", "20.5")]
    [Trait("CA", "21.7")]
    public async Task Ejecutar_CaerEnUnaTrampa_RestaCincoCongelaYDevuelveLaNovedad()
    {
        // Arena en blanco 11x11: Ana aparece en (0,0) y la trampa fija está justo abajo.
        var ctx = new Contexto();
        var trampa = new Obstaculo(new Posicion(0, 1), TipoObstaculo.Trampa, TipoTrampa.CeldaEmergente);
        var (_, ana, _) = await ctx.EnCursoAsync(new ConfigPartida(), [trampa]);

        var resultado = await ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(ana.Id, Accion.Abajo));

        Assert.Equal(ResultadoAccion.DanoPorTrampa, resultado.Resultado);
        Assert.Equal(0, resultado.Puntaje);   // tenía 0: no baja de cero
        var novedades = Assert.IsAssignableFrom<IReadOnlyList<Novedad>>(resultado.Novedades);
        var congelado = Assert.Single(novedades.OfType<JugadorCongeladoNovedad>());
        Assert.Equal(ana.Id, congelado.JugadorId);
        Assert.Equal(OrigenCongelamiento.Trampa, congelado.Origen);
        Assert.Equal(TipoTrampa.CeldaEmergente, congelado.Trampa);
        Assert.Equal(5, congelado.Segundos);

        var vista = await ctx.Obtener.EjecutarAsync(resultado.PartidaId);
        Assert.Equal(5, vista.Jugadores.Single(j => j.Id == ana.Id).SegundosCongelado);
    }

    [Fact]
    [Trait("CA", "21.5")]
    public async Task Ejecutar_EstandoCongelado_NoSeMueve()
    {
        var ctx = new Contexto();
        var trampa = new Obstaculo(new Posicion(0, 1), TipoObstaculo.Trampa);
        var (_, ana, _) = await ctx.EnCursoAsync(new ConfigPartida(), [trampa]);
        await ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(ana.Id, Accion.Abajo));

        var resultado = await ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(ana.Id, Accion.Abajo));

        Assert.Equal(ResultadoAccion.Congelado, resultado.Resultado);
        Assert.Equal((0, 1), (resultado.X, resultado.Y));
    }

    [Fact]
    public async Task Ejecutar_UnMovimientoNormal_NoTraeNovedades()
    {
        var ctx = new Contexto();
        var (_, ana, _) = await ctx.EnCursoAsync();

        var resultado = await ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(ana.Id, Accion.Abajo));

        Assert.Equal(ResultadoAccion.Movido, resultado.Resultado);
        Assert.NotNull(resultado.Novedades);
        Assert.Empty(resultado.Novedades);
    }

    // ---------- Consultas ----------

    [Fact]
    [Trait("CA", "23.4")]
    public async Task Obtener_TodosVenLaMismaArenaViva()
    {
        var ctx = new Contexto();
        var (vista, _, _) = await ctx.EnCursoAsync(new ConfigPartida { EscenarioId = "laboratorio" });

        var unaVez = await ctx.Obtener.EjecutarAsync(vista.Id);
        var otraVez = await ctx.Obtener.EjecutarAsync(vista.Id);

        Assert.Equal(unaVez.ArenaViva!.Guardianes.ToArray(), otraVez.ArenaViva!.Guardianes.ToArray());
        Assert.Equal(unaVez.ArenaViva.Puertas.ToArray(), otraVez.ArenaViva.Puertas.ToArray());
        Assert.Equal(unaVez.Escenario.ToArray(), otraVez.Escenario.ToArray());
    }

    [Fact]
    [Trait("CA", "23.1")]
    public void ListarEscenarios_DevuelveLosTresConLoNecesarioParaElegir()
    {
        var escenarios = new ListarEscenarios().Ejecutar();

        Assert.Equal(new[] { "laboratorio", "estacion", "militar" }, escenarios.Select(e => e.Id).ToArray());
        Assert.All(escenarios, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Nombre));
            Assert.Equal(e.Alto, e.Filas.Count);
            Assert.All(e.Filas, f => Assert.Matches("^[W.SRT]+$", f));   // Unity solo conoce W . S R T
            Assert.NotEmpty(e.Puertas);
            Assert.Equal(4, e.InicioGuardianes.Count);
            Assert.Equal(4, e.MaxJugadores);
            Assert.Equal((2, 4, 4), (e.MinGuardianes, e.MaxGuardianes, e.GuardianesPorDefecto));
        });
    }
}
