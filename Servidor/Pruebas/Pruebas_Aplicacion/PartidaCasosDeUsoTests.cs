using Aplicacion;
using Aplicacion.CasosDeUso;
using Aplicacion.Puertos;
using Dominio.Comun;
using Dominio.Juego;
using Dominio.Usuarios;
using Infraestructura.Partidas;
using Xunit;

namespace PruebasAplicacion;

public class PartidaCasosDeUsoTests
{
    /// <summary>Repositorio de usuarios falso: vive en memoria y no toca archivos.</summary>
    private sealed class UsuariosEnMemoria : IUsuarioRepo
    {
        private readonly Dictionary<Guid, Usuario> _usuarios = new();

        public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_usuarios.GetValueOrDefault(id));

        public Task<Usuario?> ObtenerPorUsernameAsync(string username, CancellationToken ct = default) =>
            Task.FromResult(_usuarios.Values.FirstOrDefault(u => string.Equals(u.Nombre, username?.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken ct = default) =>
            Task.FromResult(_usuarios.Values.FirstOrDefault(u => u.Correo == correo.ToLowerInvariant()));

        public Task GuardarAsync(Usuario usuario, CancellationToken ct = default)
        {
            _usuarios[usuario.Id] = usuario;
            return Task.CompletedTask;
        }
    }

    /// <summary>Arma los casos de uso con repositorios en memoria.</summary>
    private sealed class Contexto
    {
        private readonly UsuariosEnMemoria _usuarios = new();

        public ConfigurarPartida Configurar { get; }
        public UnirsePartida Unirse { get; }
        public IniciarPartida Iniciar { get; }
        public ObtenerPartida Obtener { get; }
        public ListarPartidasEnEspera Listar { get; }
        public EjecutarAccion Ejecutar { get; }

        public Contexto()
        {
            IPartidaRepo partidas = new PartidaEnMemoriaRepositorio();
            Configurar = new ConfigurarPartida(_usuarios, partidas);
            Unirse = new UnirsePartida(_usuarios, partidas);
            Iniciar = new IniciarPartida(partidas);
            Obtener = new ObtenerPartida(partidas);
            Listar = new ListarPartidasEnEspera(partidas);
            Ejecutar = new EjecutarAccion(partidas);
        }

        public async Task<Usuario> CrearUsuarioAsync(
            string nombre, bool verificado = true, bool conAvatar = true)
        {
            var usuario = new Usuario(Guid.NewGuid(), nombre, $"{nombre.ToLowerInvariant()}@correo.com", "hash");
            if (verificado)
                usuario.MarcarVerificado();
            if (conAvatar)
                usuario.ElegirAvatar(Guid.NewGuid());
            await _usuarios.GuardarAsync(usuario);
            return usuario;
        }

        public async Task<PartidaVista> CrearPartidaAsync(Usuario anfitrion, ConfigPartida? config = null) =>
            await Configurar.EjecutarAsync(new CrearPartidaComando(anfitrion.Id, config ?? new ConfigPartida()));
    }

    // ---------- Crear / configurar ----------

    [Fact]
    public async Task Configurar_CreaLaPartidaYUneAlAnfitrion()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");

        var vista = await ctx.CrearPartidaAsync(ana);

        Assert.Equal(EstadoPartida.Espera, vista.Estado);
        var jugador = Assert.Single(vista.Jugadores);
        Assert.Equal(ana.Id, jugador.Id);
        Assert.Equal(ana.AvatarId, jugador.AvatarId);
    }

    [Fact]
    public async Task Configurar_AplicaLaConfiguracionYLosObstaculos()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        var config = new ConfigPartida { Ancho = 15, Alto = 9, EnergiaObjetivo = 50 };
        var muro = new Obstaculo(new Posicion(3, 3), TipoObstaculo.Muro);

        var vista = await ctx.Configurar.EjecutarAsync(new CrearPartidaComando(ana.Id, config, [muro]));

        Assert.Equal(15, vista.Ancho);
        Assert.Equal(9, vista.Alto);
        Assert.Equal(50, vista.EnergiaObjetivo);
        Assert.Equal(new ObstaculoVista(3, 3, TipoObstaculo.Muro), Assert.Single(vista.Obstaculos));
    }

    [Fact]
    public async Task Configurar_SinObstaculos_UsaElEscenarioPredeterminado()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");

        var vista = await ctx.CrearPartidaAsync(ana);

        Assert.Equal(15, vista.Ancho);
        Assert.Equal(11, vista.Alto);
        Assert.Equal((7, 5), (vista.ReactorX, vista.ReactorY));
        Assert.Equal(Escenarios.Predeterminado.ToArray(), vista.Escenario.ToArray());
        var jugador = Assert.Single(vista.Jugadores);
        Assert.Equal((1, 1), (jugador.X, jugador.Y)); // aparece en la primera S, no en la esquina
    }

    [Fact]
    public async Task Configurar_ConMasJugadoresDeLosPermitidos_Falla()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");

        await Assert.ThrowsAsync<DominioException>(() =>
            ctx.CrearPartidaAsync(ana, new ConfigPartida { MaxJugadores = 5 }));
    }

    [Fact]
    public async Task Configurar_ConUsuarioInexistente_LanzaNoEncontrado()
    {
        var ctx = new Contexto();

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            ctx.Configurar.EjecutarAsync(new CrearPartidaComando(Guid.NewGuid(), new ConfigPartida())));
    }

    [Fact]
    public async Task Configurar_ConUsuarioSinVerificar_Falla()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana", verificado: false);

        await Assert.ThrowsAsync<DominioException>(() => ctx.CrearPartidaAsync(ana));
    }

    [Fact]
    public async Task Configurar_ConUsuarioSinAvatar_Falla()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana", conAvatar: false);

        await Assert.ThrowsAsync<DominioException>(() => ctx.CrearPartidaAsync(ana));
    }

    [Fact]
    public async Task Configurar_ConConfiguracionInvalida_Falla()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");

        await Assert.ThrowsAsync<DominioException>(() =>
            ctx.CrearPartidaAsync(ana, new ConfigPartida { MaxJugadores = 1 }));
    }

    // ---------- Unirse ----------

    [Fact]
    public async Task Unirse_AgregaAlJugadorALaPartida()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        var beto = await ctx.CrearUsuarioAsync("Beto");
        var partida = await ctx.CrearPartidaAsync(ana);

        var vista = await ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(partida.Id, beto.Id));

        Assert.Equal(2, vista.Jugadores.Count);
        Assert.Contains(vista.Jugadores, j => j.Id == beto.Id);
    }

    [Fact]
    public async Task Unirse_AUnaPartidaInexistente_LanzaNoEncontrado()
    {
        var ctx = new Contexto();
        var beto = await ctx.CrearUsuarioAsync("Beto");

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(Guid.NewGuid(), beto.Id)));
    }

    [Fact]
    public async Task Unirse_DosVeces_Falla()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        var partida = await ctx.CrearPartidaAsync(ana);

        await Assert.ThrowsAsync<DominioException>(() =>
            ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(partida.Id, ana.Id)));
    }

    [Fact]
    public async Task Unirse_ConUsuarioSinVerificar_Falla()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        var beto = await ctx.CrearUsuarioAsync("Beto", verificado: false);
        var partida = await ctx.CrearPartidaAsync(ana);

        await Assert.ThrowsAsync<DominioException>(() =>
            ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(partida.Id, beto.Id)));
    }

    [Fact]
    public async Task Unirse_EnParalelo_NoSuperaElMaximoDeJugadores()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        var partida = await ctx.CrearPartidaAsync(ana, new ConfigPartida { MaxJugadores = 2 });
        var otros = new List<Usuario>();
        for (var i = 0; i < 5; i++)
            otros.Add(await ctx.CrearUsuarioAsync($"Jugador{i}"));

        var intentos = otros.Select(async usuario =>
        {
            try
            {
                await ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(partida.Id, usuario.Id));
                return true;
            }
            catch (DominioException)
            {
                return false;
            }
        });
        var resultados = await Task.WhenAll(intentos);

        Assert.Single(resultados, entro => entro); // hay un solo lugar libre
        var final = await ctx.Obtener.EjecutarAsync(partida.Id);
        Assert.Equal(2, final.Jugadores.Count);
    }

    // ---------- Iniciar ----------

    [Fact]
    public async Task Iniciar_ElAnfitrion_PoneLaPartidaEnCurso()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        var beto = await ctx.CrearUsuarioAsync("Beto");
        var partida = await ctx.CrearPartidaAsync(ana);
        await ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(partida.Id, beto.Id));

        var vista = await ctx.Iniciar.EjecutarAsync(new IniciarPartidaComando(partida.Id, ana.Id));

        Assert.Equal(EstadoPartida.EnCurso, vista.Estado);
    }

    [Fact]
    public async Task Iniciar_ConUnJugadorQueNoEsElAnfitrion_Falla()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        var beto = await ctx.CrearUsuarioAsync("Beto");
        var partida = await ctx.CrearPartidaAsync(ana);
        await ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(partida.Id, beto.Id));

        await Assert.ThrowsAsync<DominioException>(() =>
            ctx.Iniciar.EjecutarAsync(new IniciarPartidaComando(partida.Id, beto.Id)));
    }

    [Fact]
    public async Task Iniciar_SinSuficientesJugadores_Falla()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        var partida = await ctx.CrearPartidaAsync(ana);

        await Assert.ThrowsAsync<DominioException>(() =>
            ctx.Iniciar.EjecutarAsync(new IniciarPartidaComando(partida.Id, ana.Id)));
    }

    [Fact]
    public async Task Iniciar_UnaPartidaInexistente_LanzaNoEncontrado()
    {
        var ctx = new Contexto();

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            ctx.Iniciar.EjecutarAsync(new IniciarPartidaComando(Guid.NewGuid(), Guid.NewGuid())));
    }

    // ---------- Ejecutar acción (botones del control) ----------

    private static async Task<(Contexto ctx, Usuario ana, Usuario beto, PartidaVista partida)> PartidaEnCursoAsync()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        var beto = await ctx.CrearUsuarioAsync("Beto");
        var partida = await ctx.CrearPartidaAsync(ana);
        await ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(partida.Id, beto.Id));
        await ctx.Iniciar.EjecutarAsync(new IniciarPartidaComando(partida.Id, ana.Id));
        return (ctx, ana, beto, partida);
    }

    [Fact]
    public async Task Ejecutar_UnMovimientoValido_DevuelvePosicionYPuntaje()
    {
        var (ctx, ana, _, partida) = await PartidaEnCursoAsync();

        // Ana aparece en (1,1) del escenario predeterminado; abajo, en (1,2), hay piso.
        var resultado = await ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(ana.Id, Accion.Abajo));

        Assert.Equal(partida.Id, resultado.PartidaId);
        Assert.Equal(ResultadoAccion.Movido, resultado.Resultado);
        Assert.Equal((1, 2), (resultado.X, resultado.Y));
        Assert.Equal(1, resultado.Puntaje);
        Assert.Equal(EstadoPartida.EnCurso, resultado.Estado);
        Assert.Null(resultado.GanadorId);
    }

    [Fact]
    public async Task Ejecutar_HaciaUnMuro_EsBloqueadoYNoSumaPuntos()
    {
        var (ctx, ana, _, _) = await PartidaEnCursoAsync();

        // Arriba de (1,1) está el muro del borde.
        var resultado = await ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(ana.Id, Accion.Arriba));

        Assert.Equal(ResultadoAccion.Bloqueado, resultado.Resultado);
        Assert.Equal((1, 1), (resultado.X, resultado.Y));
        Assert.Equal(0, resultado.Puntaje);
    }

    [Fact]
    public async Task Ejecutar_CadaJugadorMueveSoloSuAvatar()
    {
        var (ctx, ana, beto, _) = await PartidaEnCursoAsync();

        var deAna = await ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(ana.Id, Accion.Abajo));
        var deBeto = await ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(beto.Id, Accion.Abajo));

        Assert.Equal(ana.Id, deAna.UsuarioId);
        Assert.Equal(beto.Id, deBeto.UsuarioId);
        Assert.Equal((1, 2), (deAna.X, deAna.Y));
        Assert.Equal((13, 2), (deBeto.X, deBeto.Y)); // Beto nace en (13,1)
    }

    [Fact]
    public async Task Ejecutar_UnJugadorQueNoEstaEnNingunaPartida_LanzaNoEncontrado()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(ana.Id, Accion.Abajo)));
    }

    [Fact]
    public async Task Ejecutar_ConLaPartidaEnEspera_Falla()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        await ctx.CrearPartidaAsync(ana);

        await Assert.ThrowsAsync<DominioException>(() =>
            ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(ana.Id, Accion.Abajo)));
    }

    [Fact]
    public async Task Ejecutar_BuscaLaPartidaDelJugadorEntreVarias()
    {
        var (ctx, _, _, primera) = await PartidaEnCursoAsync();
        var carla = await ctx.CrearUsuarioAsync("Carla");
        var dani = await ctx.CrearUsuarioAsync("Dani");
        var segunda = await ctx.CrearPartidaAsync(carla);
        await ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(segunda.Id, dani.Id));
        await ctx.Iniciar.EjecutarAsync(new IniciarPartidaComando(segunda.Id, carla.Id));

        var resultado = await ctx.Ejecutar.EjecutarAsync(new EjecutarAccionComando(dani.Id, Accion.Abajo));

        Assert.Equal(segunda.Id, resultado.PartidaId);
        Assert.NotEqual(primera.Id, resultado.PartidaId);
    }

    // ---------- Consultas ----------

    [Fact]
    public async Task Listar_SoloMuestraLasPartidasQueNoHanComenzado()
    {
        var ctx = new Contexto();
        var ana = await ctx.CrearUsuarioAsync("Ana");
        var beto = await ctx.CrearUsuarioAsync("Beto");
        var carla = await ctx.CrearUsuarioAsync("Carla");

        var enJuego = await ctx.CrearPartidaAsync(ana);
        await ctx.Unirse.EjecutarAsync(new UnirsePartidaComando(enJuego.Id, beto.Id));
        await ctx.Iniciar.EjecutarAsync(new IniciarPartidaComando(enJuego.Id, ana.Id));
        var abierta = await ctx.CrearPartidaAsync(carla);

        var lista = await ctx.Listar.EjecutarAsync();

        var resumen = Assert.Single(lista);
        Assert.Equal(abierta.Id, resumen.Id);
        Assert.Equal("Carla", resumen.Anfitrion);
        Assert.Equal(1, resumen.Jugadores);
    }

    [Fact]
    public async Task Obtener_UnaPartidaInexistente_LanzaNoEncontrado()
    {
        var ctx = new Contexto();

        await Assert.ThrowsAsync<NoEncontradoException>(() => ctx.Obtener.EjecutarAsync(Guid.NewGuid()));
    }
}
