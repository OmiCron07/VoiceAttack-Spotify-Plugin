using System;
using System.Net;

namespace Spotify.Infrastructure
{
  public static class WebUtil
  {
    private const string BaseAddress = "https://raw.githubusercontent.com/OmiCron07/VoiceAttack-Spotify-Plugin/main";


    private static readonly WebClient _client = new WebClient();


    public static Version GetPluginVersion()
    {
      try
      {
        string pluginVersion = _client.DownloadString($"{BaseAddress}/PluginVersion.txt");

        return string.IsNullOrWhiteSpace(pluginVersion) ? null : Version.Parse(pluginVersion);
      }
      catch (Exception ex)
      {
        SpotifyPlugin.Telemetry.TrackException(ex);

        return null;
      }
    }
  }
}
