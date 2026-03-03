using Marvin.Tmthfh91.Crawling.Crawler;
using Marvin.Tmthfh91.Crawling.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

//using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
//using Google.Apis.Util.Store;
//using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
//using Google.Apis.YouTube.v3.Data;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public class YouTubeCrawler : BaseCrawler
    {
        private readonly YouTubeService _youtubeService;

        public YouTubeCrawler()
        {
            EnvLoader.Load();
            _youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = EnvLoader.Get("YOUTUBE_API_KEY"),
                ApplicationName = "ShooqLive-YouTubeShorts"
            });
        }

        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 유튜브 인기 동영상 수집 시작: {Site.YouTube.url}");

            try
            {
                var shorts = await GetTodayShortsAsync(50);
                foreach (var (shortVideo, index) in shorts.Select((s, i) => (s, i + 1)))
                {
                    Console.WriteLine($"\n[{index}] {shortVideo.Title}");
                    Console.WriteLine($"    채널: {shortVideo.ChannelTitle}");
                    Console.WriteLine($"    조회수: {shortVideo.ViewCount:N0}회");
                    Console.WriteLine($"    좋아요: {shortVideo.LikeCount:N0}");
                    Console.WriteLine($"    길이: {shortVideo.DurationSeconds}초");
                    Console.WriteLine($"    URL: https://www.youtube.com/watch?v={shortVideo.VideoId}");

                    var optimizedImage = new OptimizedImageData()
                    {
                        CloudinaryUrl = shortVideo.ThumbnailUrl,
                        CloudinaryPublicId = "YouTube"
                    };

                    var resultId = await new DatabaseManager().InsertOptimizedImagesAndReturnIdAsync(optimizedImage);

                    var post = new PostInfo
                    {
                        Title = shortVideo.Title,
                        Url = $"https://www.youtube.com/watch?v={shortVideo.VideoId}",
                        Views = $"{shortVideo.ViewCount}",
                        Likes = $"{shortVideo.LikeCount}",
                        Date = $"{shortVideo.PublishedAt:yyyy-MM-dd HH:mm:ss}",
                        img1 = resultId,
                    };

                    posts.Add(post);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"유튜브 수집 오류: {ex.Message}");
            }

            return posts;
        }

        private static readonly TimeZoneInfo KoreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");

        /// <summary>
        /// 오늘 업로드된 인기 Shorts (한국 시간 기준)
        /// mostPopular를 가져온 후 날짜 필터링
        /// </summary>
        public async Task<List<YouTubeShort>> GetTodayShortsAsync(int maxResults = 200)
        {
            // 한국 시간 기준 오늘
            var koreaTimeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KoreaTimeZone);
            var todayStartKst = koreaTimeNow.Date;
            var todayEndKst = todayStartKst.AddDays(1);

            Console.WriteLine($"[오늘 날짜] KST: {todayStartKst:yyyy-MM-dd HH:mm:ss} ~ {todayEndKst:yyyy-MM-dd HH:mm:ss}");

            // mostPopular 인기 동영상 가져오기 (더 많이)
            var videoRequest = _youtubeService.Videos.List("snippet,statistics,contentDetails");
            videoRequest.Chart = VideosResource.ListRequest.ChartEnum.MostPopular;
            videoRequest.RegionCode = "KR";
            videoRequest.MaxResults = maxResults; // 최대한 많이 가져오기

            var videoResponse = await videoRequest.ExecuteAsync();

            if (videoResponse.Items == null || !videoResponse.Items.Any())
            {
                Console.WriteLine("✗ API 응답에 동영상이 없습니다.");
                return new List<YouTubeShort>();
            }

            Console.WriteLine($"✓ 총 {videoResponse.Items.Count}개 동영상 가져옴");

            var shorts = new List<YouTubeShort>();
            int shortsCount = 0;
            int todayCount = 0;

            foreach (var video in videoResponse.Items)
            {
                var durationSeconds = ParseIsoDuration(video.ContentDetails.Duration);
                var publishedAt = video.Snippet.PublishedAtDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow;
                var publishedKst = TimeZoneInfo.ConvertTimeFromUtc(publishedAt, KoreaTimeZone);

                // Shorts 필터링 (60초 이하)
                // if (durationSeconds <= 60)
                // {
                shortsCount++;

                // 오늘 날짜 필터링
                if (publishedKst >= todayStartKst && publishedKst < todayEndKst)
                {
                    todayCount++;
                    shorts.Add(new YouTubeShort
                    {
                        VideoId = video.Id,
                        Title = video.Snippet.Title,
                        ChannelTitle = video.Snippet.ChannelTitle,
                        ThumbnailUrl = video.Snippet.Thumbnails.High?.Url
                                       ?? video.Snippet.Thumbnails.Medium?.Url,
                        ViewCount = (long)(video.Statistics.ViewCount ?? 0),
                        LikeCount = (long)(video.Statistics.LikeCount ?? 0),
                        DurationSeconds = durationSeconds,
                        PublishedAt = publishedAt
                    });

                    //Console.WriteLine($"  → Shorts 발견: {video.Snippet.Title.Substring(0, Math.Min(30, video.Snippet.Title.Length))}... ({publishedKst:MM-dd HH:mm})");
                    // }
                }
            }

            Console.WriteLine($"✓ Shorts: {shortsCount}개, 오늘 업로드: {todayCount}개");

            return shorts.OrderByDescending(s => s.ViewCount).ToList();
        }

        /// <summary>
        /// 한국 인기 Shorts 가져오기 (60초 이하 필터링)
        /// </summary>
        public async Task<List<YouTubeShort>> GetTrendingShortsAsync(int maxResults = 50)
        {
            // 오늘 날짜 범위 설정 (UTC 기준)
            var koreaTimeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KoreaTimeZone);
            var todayStartKst = koreaTimeNow.Date.AddDays(-7); // 오늘 00:00:00
            var todayEndKst = koreaTimeNow.Date.AddDays(1);   // 내일 00:00:00

            // UTC로 변환
            var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayStartKst, KoreaTimeZone);
            var todayEndUtc = TimeZoneInfo.ConvertTimeToUtc(todayEndKst, KoreaTimeZone);

            Console.WriteLine($"검색 범위 (KST): {todayStartKst:yyyy-MM-dd HH:mm:ss} ~ {todayEndKst:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"검색 범위 (UTC): {todayStartUtc:yyyy-MM-dd HH:mm:ss} ~ {todayEndUtc:yyyy-MM-dd HH:mm:ss}");

            // 1단계: Search API로 오늘 날짜 동영상 검색
            var searchRequest = _youtubeService.Search.List("snippet");
            searchRequest.Type = "video";
            searchRequest.RegionCode = "KR";
            searchRequest.Order = SearchResource.ListRequest.OrderEnum.Date; // 최신순으로 변경!
            searchRequest.PublishedAfterDateTimeOffset = new DateTimeOffset(todayStartUtc);
            searchRequest.PublishedBeforeDateTimeOffset = new DateTimeOffset(todayEndUtc);
            searchRequest.MaxResults = maxResults;

            var searchResponse = await searchRequest.ExecuteAsync();

            if (searchResponse.Items == null || !searchResponse.Items.Any())
            {
                return new List<YouTubeShort>();
            }

            // 2단계: 비디오 ID 목록 추출
            var videoIds = string.Join(",", searchResponse.Items.Select(item => item.Id.VideoId));

            // 3단계: Videos API로 상세 정보 가져오기
            var videoRequest = _youtubeService.Videos.List("snippet,statistics,contentDetails");
            videoRequest.Id = videoIds;

            var videoResponse = await videoRequest.ExecuteAsync();

            // 2단계: Shorts만 필터링 (60초 이하)
            var shorts = new List<YouTubeShort>();

            foreach (var video in videoResponse.Items)
            {
                var durationSeconds = ParseIsoDuration(video.ContentDetails.Duration);

                // Shorts는 60초 이하
                // if (durationSeconds <= 60)
                // {
                shorts.Add(new YouTubeShort
                {
                    VideoId = video.Id,
                    Title = video.Snippet.Title,
                    ChannelTitle = video.Snippet.ChannelTitle,
                    ThumbnailUrl = video.Snippet.Thumbnails.High?.Url
                                   ?? video.Snippet.Thumbnails.Medium?.Url,
                    ViewCount = (long)(video.Statistics.ViewCount ?? 0),
                    LikeCount = (long)(video.Statistics.LikeCount ?? 0),
                    DurationSeconds = durationSeconds,
                    PublishedAt = video.Snippet.PublishedAtDateTimeOffset?.DateTime ?? DateTime.UtcNow
                });
                // }
            }

            return shorts;
        }

        /// <summary>
        /// ISO 8601 duration을 초 단위로 변환 (PT1M30S -> 90초)
        /// </summary>
        private int ParseIsoDuration(string isoDuration)
        {
            try
            {
                var timeSpan = XmlConvert.ToTimeSpan(isoDuration);
                return (int)timeSpan.TotalSeconds;
            }
            catch
            {
                return 0;
            }
        }
    }
}
