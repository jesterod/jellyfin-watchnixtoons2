using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Http;
using Jellyfin.Plugin.WatchNixtoons2Direct.Services;

namespace Jellyfin.Plugin.WatchNixtoons2Direct
{
    public class WatchNixtoons2Channel : IChannel, IRequiresMediaInfoCallback
    {
        private readonly WatchNixtoons2Service _service;
        private readonly IServerApplicationHost _appHost;
        private readonly ILibraryManager _libraryManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public WatchNixtoons2Channel(WatchNixtoons2Service service, IServerApplicationHost appHost, ILibraryManager libraryManager, IHttpContextAccessor httpContextAccessor)
        {
            _service = service;
            _appHost = appHost;
            _libraryManager = libraryManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public string Name => "WatchNixtoons2 Direct Play";
        public string Description => "Watch cartoons and anime from wcofun.net and related sites";
        public string DataVersion => "1";
        public string HomePageUrl => null;
        public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;
        public bool IsEnabledFor(string userId) => true;

        public InternalChannelFeatures GetChannelFeatures()
        {
            return new InternalChannelFeatures
            {
                ContentTypes = new List<ChannelMediaContentType>
                {
                    ChannelMediaContentType.Movie,
                    ChannelMediaContentType.Episode,
                    ChannelMediaContentType.Clip
                },
                MediaTypes = new List<ChannelMediaType>
                {
                    ChannelMediaType.Video
                },
                SupportsSortOrderToggle = false
            };
        }

        public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
        {
            var result = new ChannelItemResult();
            var items = new List<ChannelItemInfo>();

            // Root level
            if (string.IsNullOrEmpty(query.FolderId))
            {
                var menu = await _service.GetMainMenuAsync();
                foreach (var section in menu.Take(8))
                {
                    items.Add(new ChannelItemInfo
                    {
                        Id = "menu:" + section.Name,
                        Name = section.Name,
                        Type = ChannelItemType.Folder,
                        ImageUrl = null
                    });
                }
            }
            else if (query.FolderId.StartsWith("menu:"))
            {
                var sectionName = query.FolderId.Substring(5);
                // map back to path
                var path = sectionName switch
                {
                    "Latest Releases" => "latest",
                    "Latest Movies" => "latestmovies",
                    "Popular & Ongoing" => "popular",
                    "Dubbed Anime" => "dubbed",
                    "Cartoons" => "cartoons",
                    "Subbed Anime" => "subbed",
                    "Movies" => "movies",
                    "OVAs" => "ova",
                    _ => "latest"
                };
                var cat = await _service.GetCatalogAsync(path);
                foreach (var c in cat)
                {
                    items.Add(new ChannelItemInfo
                    {
                        Id = "series:" + c.Url,
                        Name = c.Title,
                        Type = ChannelItemType.Folder,
                        IsFavoriteable = true,
                        MediaType = ChannelMediaType.Video,
                        ImageUrl = c.ThumbnailUrl,
                        Overview = c.Plot
                    });
                }
            }
            else if (query.FolderId.StartsWith("series:"))
            {
                var url = query.FolderId.Substring(7);
                var eps = await _service.GetEpisodesAsync(url);
                foreach (var e in eps.Episodes)
                {
                    items.Add(new ChannelItemInfo
                    {
                        Id = "ep:" + e.Url,
                        Name = e.Title,
                        Type = ChannelItemType.Media,
                        ImageUrl = eps.ThumbnailUrl,
                        MediaType = ChannelMediaType.Video,
                        IsLiveStream = false,
                        MediaSources = new List<MediaSourceInfo>()
                    });
                }
            }

            result.Items = items;
            result.TotalRecordCount = items.Count;
            return result;
        }

        public Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken cancellationToken)
        {
            if (!id.StartsWith("ep:", StringComparison.Ordinal))
            {
                return Task.FromResult<IEnumerable<MediaSourceInfo>>(Enumerable.Empty<MediaSourceInfo>());
            }

            var pageUrl = id.Substring(3);

            // The upstream CDN only serves the video when the request carries a browser
            // User-Agent, which no player sends. Playback is routed back through this
            // plugin's /WatchNixtoons2Direct/Stream endpoint, which adds the headers.
            // Because this plugin offers direct play, that URL is fetched by the *client*,
            // so it must be an address the client can reach. Build it from the incoming
            // PlaybackInfo request (GetSmartApiUrl picks the interface on the client's
            // subnet and honours a published server URL), falling back to the server's
            // local address when no request is available. A 127.0.0.1 URL is reachable
            // only by the server itself and makes clients fail with DirectPlayError.
            var request = _httpContextAccessor.HttpContext?.Request;
            var baseUrl = request is not null
                ? _appHost.GetSmartApiUrl(request)
                : _appHost.GetApiUrlForLocalAccess();

            var proxyUrl = $"{baseUrl.TrimEnd('/')}/WatchNixtoons2Direct/Stream?url={Uri.EscapeDataString(pageUrl)}";

            // The media source id must match the id Jellyfin advertises for this channel
            // item while browsing: BaseItem.GetVersionInfo builds it as item.Id.ToString("N"),
            // and the player sends that id back as MediaSourceId on PlaybackInfo. If it does
            // not match, MediaInfoHelper filters out every source and returns NoCompatibleStream
            // ("This client isn't compatible with the media..."), which never reaches transcoding.
            // ChannelManager derives item.Id from GetIdToHash(externalId, channelName), so
            // reproduce it here. The result is 32 hex characters, keeping it filesystem-safe
            // for the FFmpeg log filename that TranscodeManager builds from this id.
            var safeId = _libraryManager
                .GetNewItemId(id + Name + "16", typeof(Video))
                .ToString("N", CultureInfo.InvariantCulture);

            var source = new MediaSourceInfo
            {
                Id = safeId,
                Name = "WatchNixtoons2 Direct Play",
                Path = proxyUrl,
                Container = "mp4",
                Protocol = MediaBrowser.Model.MediaInfo.MediaProtocol.Http,
                IsRemote = true,
                RequiresOpening = false,
                SupportsProbing = true,
                // Direct play hands the proxy URL above to the client, which fetches and
                // plays it as-is, avoiding transcoding entirely. DirectStream is not an
                // option on Jellyfin 10.10 (its EnableDirectStream path is disabled), so
                // direct play is the only non-transcode route. Transcoding stays enabled
                // as a fallback for clients whose profile rejects mp4/h264/aac.
                SupportsDirectPlay = true,
                SupportsDirectStream = true,
                SupportsTranscoding = true,
                MediaStreams = new List<MediaStream>
                {
                    // Declaring the real source bitrate lets Jellyfin's
                    // "don't allow bitrate increases" cap (GetVideoBitrateParamValue)
                    // clamp the transcode target to the source. Without it the client's
                    // requested maximum (hundreds of Mbps on a fast LAN) is handed to the
                    // encoder; libsvtav1 rejects anything over 100 Mbps and the HLS
                    // transcode fails with HTTP 500, so mpv never opens the stream.
                    // Width/Height are deliberately left unset so an explicit upscale
                    // request cannot bypass the cap.
                    new MediaStream { Type = MediaStreamType.Video, Index = 0, Codec = "h264", IsDefault = true, BitRate = 1500000 },
                    new MediaStream { Type = MediaStreamType.Audio, Index = 1, Codec = "aac", IsDefault = true }
                }
            };

            return Task.FromResult<IEnumerable<MediaSourceInfo>>(new[] { source });
        }

        public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
            => Task.FromResult(new DynamicImageResponse { Format = ImageFormat.Png, HasImage = false });

        public IEnumerable<ImageType> GetSupportedChannelImages() => new List<ImageType> { ImageType.Thumb };

        public bool SupportsContentType(ChannelMediaContentType contentType) => true;
        public bool SupportsMediaType(ChannelMediaType mediaType) => mediaType == ChannelMediaType.Video;
        public string GetCacheKey(string userId) => "watchnixtoons2";
    }
}
