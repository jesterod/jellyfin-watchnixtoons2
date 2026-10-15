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
using Jellyfin.Plugin.WatchNixtoons2.Services;

namespace Jellyfin.Plugin.WatchNixtoons2
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

        public string Name => "WatchNixtoons2";
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
        public Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken cancellationToken)
        {
            if (!id.StartsWith("ep:", StringComparison.Ordinal))
            {
                return Task.FromResult<IEnumerable<MediaSourceInfo>>(Enumerable.Empty<MediaSourceInfo>());
            }

            var pageUrl = id.Substring(3);

            var request = _httpContextAccessor.HttpContext?.Request;
            string baseUrl = string.Empty;
            if (request is not null)
            {
                var remoteIp = request.HttpContext?.Connection?.RemoteIpAddress;
                bool isRemote = remoteIp != null && !System.Net.IPAddress.IsLoopback(remoteIp) &&
                               !remoteIp.IsIPv6LinkLocal && !remoteIp.IsIPv6SiteLocal &&
                               !(remoteIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                 (remoteIp.ToString().StartsWith("192.168.") || remoteIp.ToString().StartsWith("10.") ||
                                  (remoteIp.ToString().StartsWith("172.") &&
                                   int.Parse(remoteIp.ToString().Split('.')[1]) >= 16 &&
                                   int.Parse(remoteIp.ToString().Split('.')[1]) <= 31)));
                if (isRemote && request.Host.HasValue)
                {
                    baseUrl = $"{request.Scheme}://{request.Host.Value}";
                }
                else
                {
                    try
                    {
                        baseUrl = _appHost.GetSmartApiUrl(request);
                    }
                    catch
                    {
                        baseUrl = string.Empty;
                    }
                    if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                    {
                        if (request.Host.HasValue)
                        {
                            baseUrl = $"{request.Scheme}://{request.Host.Value}";
                        }
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = _appHost.GetApiUrlForLocalAccess();
            }

            var proxyUrl = $"{baseUrl.TrimEnd('/')}/WatchNixtoons2/Stream?url={Uri.EscapeDataString(pageUrl)}";

            var safeId = _libraryManager
                .GetNewItemId(id + Name + "16", typeof(Video))
                .ToString("N", CultureInfo.InvariantCulture);

            var source = new MediaSourceInfo
            {
                Id = safeId,
                Name = "WatchNixtoons2",
                Path = proxyUrl,
                Container = "mp4",
                Protocol = MediaBrowser.Model.MediaInfo.MediaProtocol.Http,
                IsRemote = true,
                RequiresOpening = false,
                SupportsProbing = true,
                SupportsDirectPlay = true,
                SupportsDirectStream = true,
                SupportsTranscoding = true,
                MediaStreams = new List<MediaStream>
                {
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
