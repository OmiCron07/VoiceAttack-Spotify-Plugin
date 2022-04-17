using System;
using System.Net;

namespace Spotify.Infrastructure
{
  public static class WebUtil
  {
    private const string BaseAddress = "http://litpixispotify.azurewebsites.net";


    private static readonly WebClient _client = new WebClient();


    public static Version GetPluginVersion()
    {
      try
      {
        string pluginVersion = _client.DownloadString($"{BaseAddress}/pluginVersion.php");

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
