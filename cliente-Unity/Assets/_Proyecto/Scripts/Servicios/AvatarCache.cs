using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Reactor.Cliente.Servicios
{
    /// <summary>Entrega la imagen de un avatar propio (GET /api/avatares/{id}/imagen) con caché en memoria.</summary>
    public interface IAvatarCache
    {
        /// <summary>El sprite del avatar, o null si ese avatar no tiene imagen (los predeterminados solo tienen color).</summary>
        Task<Sprite> ObtenerAsync(string avatarId);

        void Olvidar(string avatarId);
    }

    public sealed class AvatarCache : IAvatarCache
    {
        readonly string servidor;
        readonly Dictionary<string, Task<Sprite>> cache = new Dictionary<string, Task<Sprite>>();

        public AvatarCache(string servidor) => this.servidor = (servidor ?? "").TrimEnd('/');

        public Task<Sprite> ObtenerAsync(string avatarId)
        {
            if (string.IsNullOrEmpty(avatarId) || string.IsNullOrEmpty(servidor)) return Task.FromResult<Sprite>(null);
            if (!Guid.TryParse(avatarId, out _)) return Task.FromResult<Sprite>(null); // los ids de prueba no son imágenes del servidor

            if (cache.TryGetValue(avatarId, out var existente)) return existente;

            var descarga = Descargar(avatarId);
            cache[avatarId] = descarga;
            return descarga;
        }

        public void Olvidar(string avatarId) => cache.Remove(avatarId ?? "");

        Task<Sprite> Descargar(string avatarId)
        {
            var fuente = new TaskCompletionSource<Sprite>();
            var solicitud = UnityWebRequestTexture.GetTexture($"{servidor}/api/avatares/{avatarId}/imagen");
            solicitud.timeout = 10;

            UnityWebRequestAsyncOperation operacion;
            try
            {
                operacion = solicitud.SendWebRequest();
            }
            catch (Exception)
            {
                solicitud.Dispose();
                cache.Remove(avatarId);
                fuente.SetResult(null);
                return fuente.Task;
            }

            operacion.completed += _ =>
            {
                try
                {
                    if (solicitud.result == UnityWebRequest.Result.Success)
                    {
                        var textura = DownloadHandlerTexture.GetContent(solicitud);
                        fuente.SetResult(Sprite.Create(textura, new Rect(0, 0, textura.width, textura.height),
                            new Vector2(0.5f, 0.5f), 100f));
                    }
                    else
                    {
                        cache.Remove(avatarId); // 404 (avatar sin imagen) o error de red: se reintenta la próxima vez
                        fuente.SetResult(null);
                    }
                }
                catch (Exception)
                {
                    cache.Remove(avatarId);
                    fuente.SetResult(null);
                }
                finally
                {
                    solicitud.Dispose();
                }
            };
            return fuente.Task;
        }
    }

    /// <summary>Caché para el servidor falso: ningún avatar tiene imagen.</summary>
    public sealed class AvatarCacheFalsa : IAvatarCache
    {
        public Task<Sprite> ObtenerAsync(string avatarId) => Task.FromResult<Sprite>(null);
        public void Olvidar(string avatarId) { }
    }
}
