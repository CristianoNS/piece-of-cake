using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PieceOfCake.Services;

/// <summary>
/// Minimal client for the local Philips Hue Bridge API (v2 over HTTPS).
/// The bridge uses a self-signed certificate, so the certificate is pinned on first use
/// (trust-on-first-use) and certificate errors are only tolerated for the bridge host itself.
public sealed class HueClient : IDisposable
{
    private const string ApplicationKeyHeader = "hue-application-key";

    private readonly HttpClient _http;
    private readonly string _host;
    private readonly string? _pinnedThumbprint;

    public HueClient(string host, string? pinnedThumbprint)
    {
        if (!IsValidBridgeAddress(host))
        {
            throw new ArgumentException("Adres mostka musi być prywatnym adresem IPv4.", nameof(host));
        }

        _host = host;
        _pinnedThumbprint = string.IsNullOrWhiteSpace(pinnedThumbprint) ? null : pinnedThumbprint;

        // Custom validation is intentional: the bridge certificate is self-signed and is pinned instead (see class summary).
#pragma warning disable MA0039
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = ValidateCertificate,
        };
#pragma warning restore MA0039

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://{host}/"),
            Timeout = TimeSpan.FromSeconds(8),
        };
    }

    /// <summary>SHA-256 thumbprint of the certificate the bridge presented most recently.</summary>
    public string? SeenThumbprint { get; private set; }

    public static bool IsValidBridgeAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !IPAddress.TryParse(value, out var ip))
        {
            return false;
        }

        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = ip.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
               || (b[0] == 192 && b[1] == 168)
               || (b[0] == 169 && b[1] == 254);
    }

    /// <summary>Asks the Philips discovery service for the bridge address in the local network.</summary>
    public static async Task<string?> DiscoverBridgeAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        var json = await http.GetStringAsync("https://discovery.meethue.com/", ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("internalipaddress", out var ipElement)
                && ipElement.ValueKind == JsonValueKind.String
                && ipElement.GetString() is { } ip
                && IsValidBridgeAddress(ip))
            {
                return ip;
            }
        }

        return null;
    }

    public void SetAppKey(string key)
    {
        _http.DefaultRequestHeaders.Remove(ApplicationKeyHeader);
        _http.DefaultRequestHeaders.Add(ApplicationKeyHeader, key);
    }

    /// <summary>
    /// Waits (up to 30 s) for the link button on the bridge to be pressed and returns the new application key.
    /// </summary>
    public async Task<string> PairAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var body = JsonSerializer.Serialize(new { devicetype = "pieceofcake#windows", generateclientkey = true });

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync("api", content, ct).ConfigureAwait(false);
                var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(text);

                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var first = doc.RootElement[0];

                    if (first.TryGetProperty("success", out var success)
                        && success.TryGetProperty("username", out var user)
                        && user.GetString() is { Length: > 0 } key)
                    {
                        return key;
                    }

                    if (first.TryGetProperty("error", out var error)
                        && error.TryGetProperty("type", out var type)
                        && type.ValueKind == JsonValueKind.Number
                        && type.GetInt32() != 101)
                    {
                        var description = error.TryGetProperty("description", out var d) ? d.GetString() : null;
                        throw new HueException("Mostek odrzucił parowanie: " + (description ?? "nieznany błąd") + ".");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                throw new HueException("Nie można połączyć z mostkiem pod tym adresem IP.", false, ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new HueException("Mostek nie odpowiada (przekroczono czas oczekiwania).", false, ex);
            }
            catch (JsonException ex)
            {
                throw new HueException("Mostek zwrócił nieoczekiwaną odpowiedź. Sprawdź adres IP.", false, ex);
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new HueException("Nie naciśnięto przycisku na mostku w ciągu 30 sekund.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<HueRoomInfo>> GetRoomsAsync(CancellationToken ct = default)
    {
        var rooms = await SendAsync(HttpMethod.Get, "clip/v2/resource/room", null, ct).ConfigureAwait(false);
        var zones = await SendAsync(HttpMethod.Get, "clip/v2/resource/zone", null, ct).ConfigureAwait(false);
        var scenes = await SendAsync(HttpMethod.Get, "clip/v2/resource/scene", null, ct).ConfigureAwait(false);
        var lights = await SendAsync(HttpMethod.Get, "clip/v2/resource/grouped_light", null, ct).ConfigureAwait(false);

        var scenesByGroup = new Dictionary<string, List<HueSceneInfo>>(StringComparer.Ordinal);
        foreach (var scene in EnumerateData(scenes))
        {
            var id = GetString(scene, "id");
            var name = GetString(scene, "metadata", "name");
            var groupId = GetString(scene, "group", "rid");
            if (id == null || name == null || groupId == null)
            {
                continue;
            }

            if (!scenesByGroup.TryGetValue(groupId, out var list))
            {
                list = new List<HueSceneInfo>();
                scenesByGroup[groupId] = list;
            }

            list.Add(new HueSceneInfo(id, name));
        }

        var stateByLight = new Dictionary<string, (bool On, double Brightness)>(StringComparer.Ordinal);
        foreach (var light in EnumerateData(lights))
        {
            var id = GetString(light, "id");
            if (id == null)
            {
                continue;
            }

            var on = light.TryGetProperty("on", out var onElement)
                     && onElement.ValueKind == JsonValueKind.Object
                     && onElement.TryGetProperty("on", out var onValue)
                     && onValue.ValueKind == JsonValueKind.True;

            var brightness = 100.0;
            if (light.TryGetProperty("dimming", out var dimming)
                && dimming.ValueKind == JsonValueKind.Object
                && dimming.TryGetProperty("brightness", out var b)
                && b.ValueKind == JsonValueKind.Number)
            {
                brightness = b.GetDouble();
            }

            stateByLight[id] = (on, brightness);
        }

        var result = new List<HueRoomInfo>();
        foreach (var group in EnumerateData(rooms).Concat(EnumerateData(zones))
                     .Select(g => (Element: g, Name: GetString(g, "metadata", "name") ?? ""))
                     .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                     .Select(g => g.Element))
        {
            var id = GetString(group, "id");
            var name = GetString(group, "metadata", "name");
            var lightId = FindGroupedLightId(group);
            if (id == null || name == null || lightId == null)
            {
                continue;
            }

            var state = stateByLight.TryGetValue(lightId, out var s) ? s : (On: false, Brightness: 100.0);
            var groupScenes = scenesByGroup.TryGetValue(id, out var list)
                ? list.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
                : new List<HueSceneInfo>();

            result.Add(new HueRoomInfo(id, name, lightId, state.On, state.Brightness, groupScenes));
        }

        return result;
    }

    public Task SetPowerAsync(string groupedLightId, bool on, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Put, $"clip/v2/resource/grouped_light/{groupedLightId}", new { on = new { on } }, ct);

    /// <summary>Sets brightness (1-100) and turns the group on.</summary>
    public Task SetBrightnessAsync(string groupedLightId, double brightness, CancellationToken ct = default)
    {
        var value = Math.Clamp(brightness, 1.0, 100.0);
        return SendAsync(
            HttpMethod.Put,
            $"clip/v2/resource/grouped_light/{groupedLightId}",
            new { on = new { on = true }, dimming = new { brightness = value } },
            ct);
    }

    public Task RecallSceneAsync(string sceneId, CancellationToken ct = default) =>
        SendAsync(
            HttpMethod.Put,
            $"clip/v2/resource/scene/{sceneId}",
            new { recall = new { action = "active" } },
            ct);

    public void Dispose() => _http.Dispose();

    private bool ValidateCertificate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        System.Net.Security.SslPolicyErrors errors)
    {
        if (certificate == null || request.RequestUri == null)
        {
            return false;
        }

        if (!string.Equals(request.RequestUri.Host, _host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var thumbprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        SeenThumbprint = thumbprint;
        return _pinnedThumbprint == null
               || string.Equals(_pinnedThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new HueException("Mostek odrzucił klucz dostępu. Połącz ponownie.", unauthorized: true);
            }

            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HueException($"Mostek zwrócił błąd HTTP {(int)response.StatusCode}.");
            }

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement.Clone();

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0)
            {
                var description = GetString(errors[0], "description") ?? "nieznany błąd";
                throw new HueException("Mostek zgłosił błąd: " + description + ".");
            }

            return root;
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            throw new HueException(
                "Certyfikat mostka nie zgadza się z zapisanym. Połącz ponownie.", unauthorized: true, inner: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new HueException("Nie można połączyć z mostkiem Hue.", false, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new HueException("Mostek Hue nie odpowiada (przekroczono czas oczekiwania).", false, ex);
        }
        catch (JsonException ex)
        {
            throw new HueException("Mostek zwrócił nieoczekiwaną odpowiedź.", false, ex);
        }
    }

    private static IEnumerable<JsonElement> EnumerateData(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    yield return item;
                }
            }
        }
    }

    private static string? FindGroupedLightId(JsonElement group)
    {
        if (!group.TryGetProperty("services", out var services) || services.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var service in services.EnumerateArray())
        {
            if (service.ValueKind == JsonValueKind.Object
                && string.Equals(GetString(service, "rtype"), "grouped_light", StringComparison.Ordinal))
            {
                return GetString(service, "rid");
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}
