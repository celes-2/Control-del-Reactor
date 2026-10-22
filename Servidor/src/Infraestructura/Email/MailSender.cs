using Aplicacion.Puertos;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Infraestructura.Email;

public sealed record SmtpOpciones(string Host, int Port, string From, string? Username, string? Password);

/// <summary>SMTP real. En desarrollo con Docker: MailHog (host=mailhog, port=1025). Las credenciales son secretos.</summary>
public sealed class MailKitMailSender(SmtpOpciones opciones) : IMailSender
{
    public async Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct = default)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(MailboxAddress.Parse(opciones.From));
        mensaje.To.Add(MailboxAddress.Parse(destinatario));
        mensaje.Subject = asunto;
        mensaje.Body = new TextPart("plain") { Text = cuerpo };

        using var cliente = new SmtpClient();
        await cliente.ConnectAsync(opciones.Host, opciones.Port, SecureSocketOptions.Auto, ct);
        if (!string.IsNullOrWhiteSpace(opciones.Username))
            await cliente.AuthenticateAsync(opciones.Username, opciones.Password ?? "", ct);
        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(true, ct);
    }
}

/// <summary>
/// Sin SMTP configurado (por ejemplo "dotnet run" sin Docker) el correo se deja como archivo de texto
/// en la carpeta outbox, para poder copiar el código de verificación y probar el flujo completo.
/// </summary>
public sealed class ArchivoMailSender(string carpetaOutbox) : IMailSender
{
    public async Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct = default)
    {
        Directory.CreateDirectory(carpetaOutbox);
        var archivo = Path.Combine(carpetaOutbox, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(archivo, $"Para: {destinatario}\nAsunto: {asunto}\n\n{cuerpo}", ct);
    }
}
