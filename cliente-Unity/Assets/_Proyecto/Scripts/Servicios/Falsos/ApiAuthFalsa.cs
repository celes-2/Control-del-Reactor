using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Reactor.Cliente.Servicios.Falsos
{
    /// <summary>
    /// Cuentas en memoria para probar las pantallas sin servidor. Ya existe el usuario "ana" con clave
    /// "Clave#1234". El código de verificación y de recuperación es siempre "123456".
    /// </summary>
    public sealed class ApiAuthFalsa : IApiAuth
    {
        public const string CodigoFalso = "123456";

        sealed class Cuenta
        {
            public string Id;
            public string Nombre, Correo, Clave, AvatarId;
            public bool Verificada;
        }

       static readonly AvatarInfo[] Avatares =
        {
            new AvatarInfo { Id = "cc2ec6b9-97f9-41ca-8985-2fc3280f7e62", Nombre = "Pablo",  Color = "#FFD400" },
            new AvatarInfo { Id = "a26fe6bc-94c8-44ee-a6c7-3ef34f66442b", Nombre = "Tasha",  Color = "#FF5A1F" },
            new AvatarInfo { Id = "96337b54-24be-4f32-bfc9-0bd8a13a92f5", Nombre = "Austin",  Color = "#1FA2FF" },
            new AvatarInfo { Id = "c4ab2c3c-700d-4918-be03-7df0682380f0", Nombre = "Tyrone", Color = "#3DDC84" },
            new AvatarInfo { Id = "5e1d7b2a-3c64-4f0e-9a58-8d2f1c6b7a43", Nombre = "Uniqua",  Color = "#B14DFF" },
        };

        readonly List<Cuenta> cuentas = new List<Cuenta>
        {
            new Cuenta { Nombre = "ana", Correo = "ana@correo.com", Clave = "Clave#1234", Verificada = true },
        };

        readonly string idJugadorLocal;

        /// <param name="idJugadorLocal">Id con el que se juega en la partida falsa (SocketFalso.Jugador1): todas las cuentas lo usan.</param>
        public ApiAuthFalsa(string idJugadorLocal)
        {
            this.idJugadorLocal = idJugadorLocal;
            cuentas[0].Id = idJugadorLocal;
        }

        public async Task RegistrarAsync(string usuario, string correo, string clave, string confirmar)
        {
            await Task.Yield();
            usuario = (usuario ?? "").Trim();
            correo = (correo ?? "").Trim().ToLowerInvariant();
            if (usuario.Length < 3 || usuario.Length > 30) throw Error("USERNAME_INVALIDO", "El nombre de usuario debe tener entre 3 y 30 caracteres.");
            if (!correo.Contains("@")) throw Error("CORREO_INVALIDO", "El correo electrónico no tiene un formato válido.");
            if (clave != confirmar) throw Error("CONTRASENAS_NO_COINCIDEN", "Las contraseñas no coinciden.");
            if (!EsFuerte(clave)) throw Error("CONTRASENA_DEBIL", "La contraseña debe tener 8 caracteres e incluir mayúscula, minúscula, número y símbolo.");
            if (Buscar(usuario) != null) throw Error("USUARIO_EXISTE", "El nombre de usuario ya está registrado.");
            if (cuentas.Any(c => c.Correo == correo)) throw Error("CORREO_EXISTE", "El correo electrónico ya está registrado.");
            cuentas.Add(new Cuenta { Id = idJugadorLocal, Nombre = usuario, Correo = correo, Clave = clave });
        }

        public async Task VerificarCorreoAsync(string codigo)
        {
            await Task.Yield();
            if (codigo?.Trim() != CodigoFalso) throw Error("TOKEN_INVALIDO", "El código de verificación es inválido o expiró.");
            foreach (var c in cuentas) c.Verificada = true; // simple: verifica a todas las pendientes
        }

        public async Task<SesionIniciada> LoginAsync(string usuarioOCorreo, string clave)
        {
            await Task.Yield();
            var cuenta = Buscar(usuarioOCorreo);
            if (cuenta == null || cuenta.Clave != clave) throw Error("CREDENCIALES_INVALIDAS", "Usuario/correo o contraseña incorrectos.");
            if (!cuenta.Verificada) throw Error("CORREO_NO_VERIFICADO", "Tu correo está pendiente de verificación.");
            actual = cuenta;
            return new SesionIniciada { Token = "token-falso-" + cuenta.Id, UsuarioId = cuenta.Id, Nombre = cuenta.Nombre, AvatarId = cuenta.AvatarId };
        }

        public async Task SolicitarRecuperacionAsync(string correo) => await Task.Yield();

        public async Task RestablecerAsync(string codigo, string clave, string confirmar)
        {
            await Task.Yield();
            if (codigo?.Trim() != CodigoFalso) throw Error("TOKEN_INVALIDO", "El código de recuperación es inválido o expiró.");
            if (clave != confirmar) throw Error("CONTRASENAS_NO_COINCIDEN", "Las contraseñas no coinciden.");
            if (!EsFuerte(clave)) throw Error("CONTRASENA_DEBIL", "La contraseña debe tener 8 caracteres e incluir mayúscula, minúscula, número y símbolo.");
            cuentas[0].Clave = clave;
        }

        Cuenta actual;

        string avatarPropio;

        public async Task<IReadOnlyList<AvatarInfo>> ListarAvataresAsync()
        {
            await Task.Yield();
            var lista = new List<AvatarInfo>(Avatares);
            if (avatarPropio != null) lista.Add(new AvatarInfo { Id = avatarPropio, Nombre = "Mi imagen", Color = "#FFFFFF", Propio = true });
            return lista;
        }

        public async Task<string> SubirAvatarAsync(byte[] png)
        {
            await Task.Yield();
            if (png == null || png.Length == 0) throw Error("IMAGEN_VACIA", "No se recibió ninguna imagen.");
            avatarPropio = "propio-" + Guid.NewGuid().ToString("N");
            (actual ?? cuentas[0]).AvatarId = avatarPropio;
            return avatarPropio;
        }

        public async Task<PerfilUsuario> PerfilAsync()
        {
            await Task.Yield();
            var c = actual ?? cuentas[0];
            return new PerfilUsuario { Id = c.Id, Nombre = c.Nombre, Correo = c.Correo, AvatarId = c.AvatarId };
        }

        public async Task ElegirAvatarAsync(string avatarId)
        {
            await Task.Yield();
            if (Avatares.All(a => a.Id != avatarId) && avatarId != avatarPropio) throw Error("AVATAR_INVALIDO", "Debes seleccionar uno de los avatares disponibles.");
            (actual ?? cuentas[0]).AvatarId = avatarId;
        }

        Cuenta Buscar(string valor)
        {
            valor = (valor ?? "").Trim().ToLowerInvariant();
            return cuentas.FirstOrDefault(c => c.Nombre.ToLowerInvariant() == valor || c.Correo == valor);
        }

        static bool EsFuerte(string p) =>
            p != null && p.Length >= 8 && p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit) && p.Any(c => !char.IsLetterOrDigit(c));

        static ApiAuthException Error(string codigo, string mensaje) => new ApiAuthException(codigo, mensaje);
    }
}
