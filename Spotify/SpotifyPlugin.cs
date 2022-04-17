using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Newtonsoft.Json;
using Spotify.Infrastructure;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace Spotify
{
  public class SpotifyPlugin
  {
    private const string ClientId         = "1625ee502f714906bdbcf84e22a2906e";
    private const string RequestTokenFile = @"Lit Pixi\Spotify\RequestTokenFile.json";

    private static SpotifyClient _spotify;
    private static UserProfile   _userProfile;
    private static string        _deviceId;
    private static int           _lastVolume;
    private static Version       _currentVersion;


    public static VaProxy VaProxy { get; set; }

    public static TelemetryClient Telemetry { get; private set; }

    private static string RequestTokenFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), RequestTokenFile);


    public static string VA_DisplayName()
    {
      return "Spotify Plugin - litpixi.com/va-spotify/";
    }

    public static string VA_DisplayInfo()
    {
      return "Spotify Plugin";
    }

    public static Guid VA_Id()
    {
      return new Guid("{19C595F3-CDA3-42E8-B299-9FC4CC68F1C5}");
    }

    public static void VA_StopCommand()
    {
    }

    public static void VA_Init1(dynamic vaProxy)
    {
      VaProxy = new VaProxy(vaProxy);

      var pluginAssembly = Assembly.GetExecutingAssembly();
      _currentVersion = pluginAssembly.GetName().Version;

      bool disableTelemetry       = File.Exists(Path.Combine(new FileInfo(pluginAssembly.Location).DirectoryName, "NoTelemetry.txt"));
      var  telemetryConfiguration = new TelemetryConfiguration("ef43b393-3b65-47bf-b0b0-cfc837d29747") { DisableTelemetry = disableTelemetry };

      if (disableTelemetry)
      {
        VaProxy.WriteToLog("Telemetry disabled.", ColorEnum.Yellow);
      }

      Telemetry                                = new TelemetryClient(telemetryConfiguration);
      Telemetry.Context.Component.Version      = _currentVersion.ToString();
      Telemetry.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
      Telemetry.Context.Session.Id             = Guid.NewGuid().ToString();

      Telemetry.Context.GlobalProperties.Add("VoiceAttackVersion", VaProxy.Version().ToString());

      try
      {
        var upToDatePluginVersion = WebUtil.GetPluginVersion();

        if (upToDatePluginVersion != _currentVersion)
        {
          VaProxy.WriteToLog($"[Spotify] There is a new version of the Spotify plugin. Current is {_currentVersion} and latest is {upToDatePluginVersion}.", ColorEnum.Purple);
        }

        if (File.Exists(RequestTokenFilePath))
        {
          VaProxy.WriteToLog("[Spotify] Already authorized, no web page should have opened.", ColorEnum.Green);
          Initialize();
        }
        else
        {
          Directory.CreateDirectory(Path.GetDirectoryName(RequestTokenFilePath));

          VaProxy.WriteToLog("[Spotify] This plugin needs authorization to your Spotify account. A Web page should have opened in your browser and you have to authorize it.", ColorEnum.Yellow);
          StartAuthentication();
        }
      }
      catch (Exception ex)
      {
        Telemetry.TrackException(ex);
        VaProxy.WriteToLog($"[Spotify] Error while initiating the connection to Spotify ({ex.Message}{ex.InnerException?.Message}).", ColorEnum.Red);
      }

      Telemetry.Flush();


      void Initialize()
      {
        string json          = File.ReadAllText(RequestTokenFilePath);
        var    tokenResponse = JsonConvert.DeserializeObject<PKCETokenResponse>(json);

        var authenticator = new PKCEAuthenticator(ClientId, tokenResponse);

        authenticator.TokenRefreshed += (sender, token) => File.WriteAllText(RequestTokenFilePath, JsonConvert.SerializeObject(token));

        var config = SpotifyClientConfig.CreateDefault()
                                        .WithAuthenticator(authenticator)
                                        .WithRetryHandler(new SimpleRetryHandler());

        _spotify = new SpotifyClient(config);

        UpdateSpotifyStateVariables();
        SetThisDevice();

        Telemetry.Context.User.Id                  = _userProfile?.Id;
        Telemetry.Context.User.AuthenticatedUserId = _userProfile?.Id;

        Telemetry.Context.GlobalProperties.Add(nameof(_userProfile.Country), _userProfile?.Country);
        Telemetry.Context.GlobalProperties.Add(nameof(_userProfile.Product), _userProfile?.Product.ToString());
        Telemetry.Context.GlobalProperties.Add("DeviceId", _deviceId);
        Telemetry.Context.GlobalProperties.Add("DeviceName", VaProxy.GetText("Spotify_DeviceName"));
        Telemetry.Context.GlobalProperties.Add("DeviceType", VaProxy.GetText("Spotify_DeviceType"));

        VaProxy.WriteToLog("[Spotify] All green!", ColorEnum.Green);

        Telemetry.Flush();
      }

      void StartAuthentication()
      {
        var (verifier, challenge) = PKCEUtil.GenerateCodes();
        var server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000, Assembly.GetExecutingAssembly(), "Spotify.ReadMe");

        server.Start().Wait();

        server.AuthorizationCodeReceived += async (sender, response) =>
                                              {
                                                try
                                                {
                                                  await server.Stop();
                                                  server.Dispose();

                                                  PKCETokenResponse token = await new OAuthClient().RequestToken(new PKCETokenRequest(ClientId, response.Code, server.BaseUri, verifier));

                                                  File.WriteAllText(RequestTokenFilePath, JsonConvert.SerializeObject(token));

                                                  Telemetry.TrackEvent(nameof(server.AuthorizationCodeReceived));
                                                  VaProxy.WriteToLog("[Spotify] Successfully authenticated with Spotify.", ColorEnum.Green);

                                                  Initialize();
                                                }
                                                catch (Exception ex)
                                                {
                                                  Telemetry.TrackException(ex);
                                                  VaProxy.WriteToLog($"[Spotify] Error while receiving the authorization ({ex.Message}{ex.InnerException?.Message}).", ColorEnum.Red);
                                                }
                                              };

        var request = new LoginRequest(server.BaseUri, ClientId, LoginRequest.ResponseType.Code)
                        {
                          CodeChallenge       = challenge,
                          CodeChallengeMethod = "S256",
                          Scope = new List<string>
                                    {
                                      Scopes.PlaylistReadPrivate,
                                      Scopes.UserReadPrivate,
                                      Scopes.UserLibraryRead,
                                      Scopes.UserTopRead,
                                      Scopes.PlaylistReadCollaborative,
                                      Scopes.UserReadRecentlyPlayed,
                                      Scopes.UserReadPlaybackState,
                                      Scopes.UserModifyPlaybackState,
                                      Scopes.UserReadCurrentlyPlaying,
                                      Scopes.UserFollowModify,
                                      Scopes.UserFollowRead,
                                      Scopes.PlaylistModifyPublic,
                                      Scopes.PlaylistModifyPrivate,
                                      Scopes.UserLibraryModify
                                    }
                        };

        var uri = request.ToUri();
        Process.Start(uri.ToString());
      }
    }

    public static void VA_Exit1(dynamic vaProxy)
    {
      VaProxy = new VaProxy(vaProxy);

      Telemetry?.Flush();
    }

    public static void VA_Invoke1(dynamic vaProxy)
    {
      VaProxy = new VaProxy(vaProxy);

      try
      {
        string context = VaProxy.Context.ToLower();

        UpdateSpotifyStateVariables();

        if (string.IsNullOrWhiteSpace(context) || context == "updatestate")
        {
          return;
        }

        VaProxy.WriteToLog($"[Spotify] {VaProxy.Context}.", ColorEnum.Green);

        switch (context)
        {
          case "play":
            Play();

            break;

          case "pause":
            Pause();

            break;

          case "skip":
            Skip();

            break;

          case "previous":
            Previous();

            break;

          case "tobeginning":
            ToBeginning();

            break;

          case "mute":
            Mute();

            break;

          case "unmute":
            Unmute();

            break;

          case "setvolume":
            SetVolume();

            break;

          case "playurl":
            PlayUrl();

            break;

          case "playfeaturedplaylist":
            PlayFeaturedPlaylist();

            break;

          case "playnewrelease":
            PlayNewRelease();

            break;

          case "isfollowingartist":
            IsFollowingArtist();

            break;

          case "followartist":
            FollowArtist();

            break;

          case "unfollowartist":
            UnfollowArtist();

            break;

          case "isfollowingplaylist":
            IsFollowingPlaylist();

            break;

          case "followplaylist":
            FollowPlaylist();

            break;

          case "unfollowplaylist":
            UnfollowPlaylist();

            break;

          case "isalbumsaved":
            IsAlbumSaved();

            break;

          case "istracksaved":
            IsTrackSaved();

            break;

          case "savealbum":
            SaveAlbum();

            break;

          case "savetrack":
            SaveTrack();

            break;

          case "removesavedalbum":
            RemoveSavedAlbum();

            break;

          case "removesavedtrack":
            RemoveSavedTrack();

            break;

          case "playmytopartist":
            PlayMyTopArtist();

            break;

          case "playmytoptracks":
            PlayMyTopTracks();

            break;

          case "changerepeatmode":
            ChangeRepeatMode();

            break;

          case "toggleshuffle":
            ToggleShuffle();

            break;

          case "downloadnewversion":
            DownloadNewVersion();

            break;

          default:
            Telemetry.TrackTrace($"Context not recognized : {VaProxy.Context}", SeverityLevel.Warning);
            VaProxy.WriteToLog("[Spotify] Context not recognized.", ColorEnum.Red);

            break;
        }

        Task.Delay(1000).Wait();
        UpdateSpotifyStateVariables();
        Telemetry.Flush();
      }
      catch (Exception ex)
      {
        Telemetry.TrackException(ex);
        VaProxy.WriteToLog($"[Spotify] Error while executing an action to Spotify ({ex.Message}{ex.InnerException?.Message}).", ColorEnum.Red);
      }


      void Play()
      {
        var request = new Request();

        bool isSuccess = _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest { DeviceId = _deviceId }).Result;

        request.Track(isSuccess);
      }

      void Pause()
      {
        var request = new Request();

        bool isSuccess = _spotify.Player.PausePlayback(new PlayerPausePlaybackRequest { DeviceId = _deviceId }).Result;

        request.Track(isSuccess);
      }

      void Skip()
      {
        var request = new Request();

        bool isSuccess = _spotify.Player.SkipNext(new PlayerSkipNextRequest { DeviceId = _deviceId }).Result;

        request.Track(isSuccess);
      }

      void Previous()
      {
        var request = new Request();

        bool isSuccess = _spotify.Player.SkipPrevious(new PlayerSkipPreviousRequest { DeviceId = _deviceId }).Result;

        request.Track(isSuccess);
      }

      void ToBeginning()
      {
        var request = new Request();

        bool isSuccess = _spotify.Player.SeekTo(new PlayerSeekToRequest(0) { DeviceId = _deviceId }).Result;

        request.Track(isSuccess);
      }

      void Mute()
      {
        var request = new Request();

        int currentVolume = VaProxy.GetInt("Spotify_Volume") ?? -1;

        if (currentVolume != 0 && currentVolume != -1)
        {
          _lastVolume = currentVolume;

          bool isSuccess = _spotify.Player.SetVolume(new PlayerVolumeRequest(0) { DeviceId = _deviceId }).Result;

          request.Track(isSuccess);
        }
      }

      void Unmute()
      {
        var request = new Request();

        bool isSuccess = _spotify.Player.SetVolume(new PlayerVolumeRequest(_lastVolume) { DeviceId = _deviceId }).Result;

        request.Track(isSuccess);
      }

      void SetVolume()
      {
        var request = new Request();

        int volume = VaProxy.GetInt("Spotify_SetThatVolume") ?? -1;

        if (volume != -1)
        {
          bool isSuccess = _spotify.Player.SetVolume(new PlayerVolumeRequest(Math.Min(Math.Max(volume, 0), 100)) { DeviceId = _deviceId }).Result;

          request.Track(isSuccess);
        }
      }

      void PlayUrl()
      {
        var request = new Request();

        string uri = VaProxy.GetText("Spotify_PlayThatUrl");

        bool isSuccess = _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest
                                                          {
                                                            DeviceId   = _deviceId,
                                                            ContextUri = uri
                                                          }).Result;

        request.Track(isSuccess);
      }

      void PlayFeaturedPlaylist()
      {
        var request = new Request();

        string    country   = VaProxy.GetText("Spotify_Country");
        DateTime? timestamp = VaProxy.GetDate("Spotify_Timestamp");
        int?      offset    = VaProxy.GetInt("Spotify_Offset");

        var featuredPlaylistsResponse = _spotify.Browse.GetFeaturedPlaylists(new FeaturedPlaylistsRequest
                                                                               {
                                                                                 Country   = country,
                                                                                 Timestamp = timestamp,
                                                                                 Offset    = offset,
                                                                                 Limit     = 1
                                                                               }).Result;

        var playlist = featuredPlaylistsResponse.Playlists.Items?.FirstOrDefault();

        if (playlist != null)
        {
          VaProxy.SetText("Spotify_PlaylistName", playlist.Name);
          VaProxy.SetText("Spotify_PlaylistId", playlist.Id);
          VaProxy.SetText("Spotify_PlaylistUri", playlist.Uri);

          bool isSuccess = _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest
                                                            {
                                                              DeviceId   = _deviceId,
                                                              ContextUri = playlist.Uri
                                                            }).Result;

          request.Track(isSuccess);
        }
      }

      void PlayNewRelease()
      {
        var request = new Request();

        string country = VaProxy.GetText("Spotify_Country");
        int?   offset  = VaProxy.GetInt("Spotify_Offset");

        var newAlbumReleasesResponse = _spotify.Browse.GetNewReleases(new NewReleasesRequest
                                                                        {
                                                                          Country = country,
                                                                          Offset  = offset,
                                                                          Limit   = 1
                                                                        }).Result;

        var album = newAlbumReleasesResponse.Albums.Items?.FirstOrDefault();

        if (album != null)
        {
          bool isSuccess = _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest
                                                            {
                                                              DeviceId   = _deviceId,
                                                              ContextUri = album.Uri
                                                            }).Result;

          request.Track(isSuccess);
        }
      }

      void IsFollowingArtist()
      {
        var request = new Request();

        string artistId  = VaProxy.GetText("Spotify_ArtistId");
        bool   isSuccess = _spotify.Follow.CheckCurrentUser(new FollowCheckCurrentUserRequest(FollowCheckCurrentUserRequest.Type.Artist, new[] { artistId })).Result.FirstOrDefault();

        VaProxy.SetBoolean("Spotify_IsFollowingArtist", isSuccess);

        request.Track(isSuccess);
      }

      void FollowArtist()
      {
        var request = new Request();

        string artistId = VaProxy.GetText("Spotify_ArtistId");

        bool isSuccess = _spotify.Follow.Follow(new FollowRequest(FollowRequest.Type.Artist, new[] { artistId })).Result;

        request.Track(isSuccess);
      }

      void UnfollowArtist()
      {
        var request = new Request();

        string artistId = VaProxy.GetText("Spotify_ArtistId");

        bool isSuccess = _spotify.Follow.Unfollow(new UnfollowRequest(UnfollowRequest.Type.Artist, new[] { artistId })).Result;

        request.Track(isSuccess);
      }

      void IsFollowingPlaylist()
      {
        var request = new Request();

        string playlistId = VaProxy.GetText("Spotify_PlaylistId");
        string userId     = _userProfile.Id;

        bool isSuccess = _spotify.Follow.CheckPlaylist(playlistId, new FollowCheckPlaylistRequest(new[] { userId })).Result.FirstOrDefault();

        VaProxy.SetBoolean("Spotify_IsFollowingPlaylist", isSuccess);

        request.Track(isSuccess);
      }

      void FollowPlaylist()
      {
        var request = new Request();

        string playlistId = VaProxy.GetText("Spotify_PlaylistId");

        bool isSuccess = _spotify.Follow.FollowPlaylist(playlistId).Result;

        request.Track(isSuccess);
      }

      void UnfollowPlaylist()
      {
        var request = new Request();

        string playlistId = VaProxy.GetText("Spotify_PlaylistId");

        bool isSuccess = _spotify.Follow.UnfollowPlaylist(playlistId).Result;

        request.Track(isSuccess);
      }

      void IsAlbumSaved()
      {
        var request = new Request();

        string albumId = VaProxy.GetText("Spotify_AlbumId");

        bool isSuccess = _spotify.Library.CheckAlbums(new LibraryCheckAlbumsRequest(new[] { albumId })).Result.FirstOrDefault();

        VaProxy.SetBoolean("Spotify_IsAlbumSaved", isSuccess);

        request.Track(isSuccess);
      }

      void IsTrackSaved()
      {
        var request = new Request();

        string trackId = VaProxy.GetText("Spotify_TrackId");

        bool isSuccess = _spotify.Library.CheckTracks(new LibraryCheckTracksRequest(new[] { trackId })).Result.FirstOrDefault();

        VaProxy.SetBoolean("Spotify_IsTrackSaved", isSuccess);

        request.Track(isSuccess);
      }

      void SaveAlbum()
      {
        var request = new Request();

        string albumId = VaProxy.GetText("Spotify_AlbumId");

        bool isSuccess = _spotify.Library.SaveAlbums(new LibrarySaveAlbumsRequest(new[] { albumId })).Result;

        request.Track(isSuccess);
      }

      void SaveTrack()
      {
        var request = new Request();

        string trackId = VaProxy.GetText("Spotify_TrackId");

        bool isSuccess = _spotify.Library.SaveTracks(new LibrarySaveTracksRequest(new[] { trackId })).Result;

        request.Track(isSuccess);
      }

      void RemoveSavedAlbum()
      {
        var request = new Request();

        string albumId = VaProxy.GetText("Spotify_AlbumId");

        bool isSuccess = _spotify.Library.RemoveAlbums(new LibraryRemoveAlbumsRequest(new[] { albumId })).Result;

        request.Track(isSuccess);
      }

      void RemoveSavedTrack()
      {
        var request = new Request();

        string trackId = VaProxy.GetText("Spotify_TrackId");

        bool isSuccess = _spotify.Library.RemoveTracks(new LibraryRemoveTracksRequest(new[] { trackId })).Result;

        request.Track(isSuccess);
      }

      void PlayMyTopArtist()
      {
        var request = new Request();

        int timeRange = VaProxy.GetInt("Spotify_TimeRange") ?? 2;
        int offset    = VaProxy.GetInt("Spotify_Offset") ?? 0;

        PersonalizationTopRequest.TimeRange timeRangeType = GetTimeRangeType(timeRange);

        var topArtists = _spotify.Personalization.GetTopArtists(new PersonalizationTopRequest
                                                                  {
                                                                    TimeRangeParam = timeRangeType,
                                                                    Limit          = 1,
                                                                    Offset         = offset
                                                                  }).Result;

        if (topArtists.Items?.Any() == true)
        {
          var artist = topArtists.Items?.FirstOrDefault();

          if (artist != null)
          {
            bool isSuccess = _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest
                                                              {
                                                                DeviceId   = _deviceId,
                                                                ContextUri = artist.Uri
                                                              }).Result;

            request.Track(isSuccess);
          }
        }
        else
        {
          request.Track(false);
        }
      }

      void PlayMyTopTracks()
      {
        var request = new Request();

        int timeRange = VaProxy.GetInt("Spotify_TimeRange") ?? 2;
        int limit     = VaProxy.GetInt("Spotify_Limit") ?? 50;

        PersonalizationTopRequest.TimeRange timeRangeType = GetTimeRangeType(timeRange);

        var fullTracks = _spotify.Personalization.GetTopTracks(new PersonalizationTopRequest
                                                                 {
                                                                   TimeRangeParam = timeRangeType,
                                                                   Limit          = limit
                                                                 }).Result;

        if (fullTracks.Items?.Any() == true)
        {
          var trackUris = fullTracks.Items.Select(x => x.Uri).ToList();

          bool isSuccess = _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest
                                                            {
                                                              DeviceId = _deviceId,
                                                              Uris     = trackUris
                                                            }).Result;

          request.Track(isSuccess);
        }
        else
        {
          request.Track(false);
        }
      }

      void ChangeRepeatMode()
      {
        var request = new Request();

        string mode = VaProxy.GetText("Spotify_RepeatMode");
        Enum.TryParse(mode, true, out PlayerSetRepeatRequest.State state);
        PlayerSetRepeatRequest.State nextState;

        switch (state)
        {
          case PlayerSetRepeatRequest.State.Off:
            nextState = PlayerSetRepeatRequest.State.Context;

            break;

          case PlayerSetRepeatRequest.State.Context:
            nextState = PlayerSetRepeatRequest.State.Track;

            break;

          case PlayerSetRepeatRequest.State.Track:
          default:
            nextState = PlayerSetRepeatRequest.State.Off;

            break;
        }

        bool isSuccess = _spotify.Player.SetRepeat(new PlayerSetRepeatRequest(nextState) { DeviceId = _deviceId }).Result;

        request.Track(isSuccess);
      }

      void ToggleShuffle()
      {
        var request = new Request();

        bool? isShuffleEnabled = VaProxy.GetBoolean("Spotify_IsShuffleEnabled");

        if (isShuffleEnabled.HasValue)
        {
          bool isSuccess = _spotify.Player.SetShuffle(new PlayerShuffleRequest(!isShuffleEnabled.Value) { DeviceId = _deviceId }).Result;

          request.Track(isSuccess);
        }
      }

      void DownloadNewVersion()
      {
        var request = new Request();

        var upToDatePluginVersion = WebUtil.GetPluginVersion();

        if (upToDatePluginVersion == _currentVersion)
        {
          VaProxy.WriteToLog("You already have the latest version.", ColorEnum.Yellow);
        }
        else
        {
          Process.Start("https://litpixistorage.blob.core.windows.net/publicfiles/Spotify.zip");
        }

        request.Track();
      }
    }

    private static void UpdateSpotifyStateVariables()
    {
      var request = new Request();

      try
      {
        SetThisDevice();
        
        var status = _spotify?.Player.GetCurrentPlayback().Result;

        if (status != null)
        {
          VaProxy.SetBoolean("Spotify_IsRunning", status?.Device != null || Process.GetProcessesByName("Spotify").Any());
          VaProxy.SetBoolean("Spotify_IsPlaying", status?.IsPlaying == true);
          VaProxy.SetBoolean("Spotify_IsShuffleEnabled", status?.ShuffleState == true);
          VaProxy.SetText("Spotify_RepeatMode", status?.RepeatState);
          VaProxy.SetBoolean("Spotify_IsAd", status?.CurrentlyPlayingType.Equals("ad", StringComparison.InvariantCultureIgnoreCase));
          VaProxy.SetInt("Spotify_Volume", status?.Device?.VolumePercent);
          VaProxy.SetBoolean("Spotify_IsMuted", status?.Device?.VolumePercent == 0);

          VaProxy.SetText("Spotify_TrackType", status?.Item?.Type.ToString());

          if (status?.Item?.Type == ItemType.Track)
          {
            var track = (FullTrack) status.Item;

            VaProxy.SetText("Spotify_TrackId", track.Id);
            VaProxy.SetText("Spotify_TrackName", track.Name);
            VaProxy.SetInt("Spotify_TrackLength", track.DurationMs);

            VaProxy.SetText("Spotify_AlbumId", track.Album?.Id);
            VaProxy.SetText("Spotify_AlbumName", track.Album?.Name);
            VaProxy.SetText("Spotify_AlbumReleaseDate", track.Album?.ReleaseDate);
            VaProxy.SetText("Spotify_AlbumType", track.Album?.Type);
            VaProxy.SetText("Spotify_AlbumUri", track.Album?.Uri);

            VaProxy.SetText("Spotify_ArtistId", track.Artists.FirstOrDefault()?.Id);
            VaProxy.SetText("Spotify_ArtistUri", track.Artists.FirstOrDefault()?.Uri);
            VaProxy.SetText("Spotify_ArtistName", track.Artists.Any() ? string.Join(" and ", track.Artists.Select(x => x.Name)) : string.Empty);
          }

          VaProxy.SetText("Spotify_DeviceId", status?.Device?.Id);
          VaProxy.SetText("Spotify_DeviceName", status?.Device?.Name);
          VaProxy.SetText("Spotify_DeviceType", status?.Device?.Type);
          VaProxy.SetBoolean("Spotify_IsDeviceActive", status?.Device?.IsActive);

          if (status?.Context?.Type == "playlist")
          {
            string playlistUri = VaProxy.GetText("Spotify_PlaylistUri");

            if (status.Context.Uri != playlistUri)
            {
              SetPlaylist(status.Context.Uri.Right(":playlist:"));
            }
          }
        }

        if (_userProfile == null && _spotify != null)
        {
          SetUserProfile();
        }

        request.Track(status != null);
      }
      catch (Exception ex)
      {
        request.Track(false);
        Telemetry.TrackException(ex);
      }
    }

    private static void SetUserProfile()
    {
      var request = new Request();

      var privateUser = _spotify.UserProfile.Current().Result;
      _userProfile = new UserProfile(privateUser);

      request.Track();
    }

    private static void SetPlaylist(string playlistId)
    {
      var request = new Request();

      var fullPlaylist = _spotify.Playlists.Get(playlistId).Result;

      VaProxy.SetText("Spotify_PlaylistId", fullPlaylist.Id);
      VaProxy.SetText("Spotify_PlaylistUri", fullPlaylist.Uri);
      VaProxy.SetText("Spotify_PlaylistName", fullPlaylist.Name);
      VaProxy.SetText("Spotify_PlaylistDescription", fullPlaylist.Description);

      request.Track();
    }

    private static void SetThisDevice()
    {
      if (string.IsNullOrEmpty(_deviceId))
      {
        var result = _spotify.Player.GetAvailableDevices().Result;

        if (result?.Devices.Any() == true)
        {
          var thisComputer = result.Devices.FirstOrDefault(x => string.Equals(x.Name, Environment.MachineName, StringComparison.InvariantCultureIgnoreCase));

          _deviceId = thisComputer?.Id ?? string.Empty;
        }
      }
    }

    private static string GetThisDeviceIfNoOneActive()
    {
      string deviceId = VaProxy.GetText("Spotify_DeviceId");

      if (string.IsNullOrEmpty(deviceId))
      {
        var result = _spotify.Player.GetAvailableDevices().Result;

        if (result?.Devices.Any() == true)
        {
          var thisComputer = result.Devices.FirstOrDefault(x => string.Equals(x.Name, Environment.MachineName, StringComparison.InvariantCultureIgnoreCase));

          return thisComputer?.Id ?? string.Empty;
        }
      }

      return string.Empty;
    }

    private static PersonalizationTopRequest.TimeRange GetTimeRangeType(int timeRange)
    {
      PersonalizationTopRequest.TimeRange timeRangeType;

      switch (timeRange)
      {
        case 1:
          timeRangeType = PersonalizationTopRequest.TimeRange.LongTerm;

          break;

        case 3:
          timeRangeType = PersonalizationTopRequest.TimeRange.ShortTerm;

          break;

        case 2:
        default:
          timeRangeType = PersonalizationTopRequest.TimeRange.MediumTerm;

          break;
      }

      return timeRangeType;
    }
  }
}