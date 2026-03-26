using Marvin.Tmthfh91.Crawling;
using Marvin.Tmthfh91.Crawling.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using HtmlAgilityPack;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public abstract class BaseCrawler
    {
        public readonly HttpClient _httpClient;
        public readonly Random _random;
        public readonly List<string> _userAgents;
        public readonly DatabaseManager _dbManager;
        protected readonly NsfwService _nsfwService;
        private readonly HashSet<int> _adultDetectedNos = new();

        // HTML 정리용 불필요한 문자 목록
        protected List<string> ArrayUnnecessaryChars = ["공유새창"];

        // HTML 엔티티 코드 목록
        protected List<string> ArrayHtmlCodes =
        [
            "&nbsp;",   // 공백
            "&lt;",     // <
            "&gt;",     // >
            "&amp;",    // &
            "&quot;",   // "
            "&apos;",   // '
            "&copy;",   // ©
            "&reg;",    // ®
            "&#x200B;", // 폭 없는 공백
            "&#8203;",  // 폭 없는 공백 (숫자 형태)
        ];

        protected ChromeOptions GetChromeOptions()
        {
            var options = new ChromeOptions();

            // 기본 옵션들
            options.AddArgument("--headless");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            // 타임아웃 관련 옵션
            options.AddArgument("--page-load-strategy=eager");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins");
            options.AddArgument("--disable-images");

            // User-Agent 설정
            var userAgent = _userAgents[_random.Next(_userAgents.Count)];
            options.AddArgument($"--user-agent={userAgent}");

            return options;
        }

        protected void SetupDriverTimeouts(IWebDriver driver)
        {
            // 페이지 로드 타임아웃을 30초로 설정
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
            // 스크립트 실행 타임아웃을 20초로 설정  
            driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(20);
            // 요소 검색 타임아웃을 10초로 설정
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
        }

        public void SetupHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Clear();

            // 랜덤 User-Agent 선택
            var userAgent = _userAgents[_random.Next(_userAgents.Count)];
            _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            // 한국어 헤더 설정
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ko-KR,ko;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");

            // Referer 설정 (사이트 메인 페이지)
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://web.humoruniv.com/");
        }

        public abstract Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "");

        public BaseCrawler()
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                       System.Net.DecompressionMethods.Deflate
            };

            _httpClient = new HttpClient(handler);
            _random = new Random();
            _dbManager = new DatabaseManager();
            _nsfwService = new NsfwService(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

            // 다양한 User-Agent 준비
            _userAgents =
            [
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/121.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            ];

            SetupHttpClient();
        }

        // URL 유효성 체크
        protected bool IsValidUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (url.StartsWith("data:")) return false; // base64 제외
            return url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("//");
        }

        // 유튜브 URL 체크
        protected bool IsYoutubeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.Contains("youtube.com") ||
                   url.Contains("youtu.be") ||
                   url.Contains("youtube-nocookie.com");
        }

        // 유튜브 비디오 ID 추출
        protected string? ExtractYoutubeId(string url)
        {
            try
            {
                // 패턴 1: youtube.com/watch?v=VIDEO_ID
                var match = Regex.Match(url, @"[?&]v=([^&#]+)");
                if (match.Success)
                    return match.Groups[1].Value;

                // 패턴 2: youtube.com/embed/VIDEO_ID
                match = Regex.Match(url, @"youtube\.com/embed/([^?&#]+)");
                if (match.Success)
                    return match.Groups[1].Value;

                // 패턴 3: youtu.be/VIDEO_ID
                match = Regex.Match(url, @"youtu\.be/([^?&#]+)");
                if (match.Success)
                    return match.Groups[1].Value;

                // 패턴 4: youtube-nocookie.com/embed/VIDEO_ID
                match = Regex.Match(url, @"youtube-nocookie\.com/embed/([^?&#]+)");
                if (match.Success)
                    return match.Groups[1].Value;

                return null;
            }
            catch
            {
                return null;
            }
        }

        // HTML 문자열 정리
        protected string CleanHtmlString(string html)
        {
            var tempDoc = new HtmlDocument();
            tempDoc.LoadHtml(html);

            // 모든 태그의 style, class 등 속성 제거
            var allNodes = tempDoc.DocumentNode.SelectNodes("//*[@*]");
            if (allNodes != null)
            {
                foreach (var node in allNodes)
                {
                    // href만 유지 (a 태그용)
                    var href = node.Name == "a" ? node.GetAttributeValue("href", "") : null;

                    node.Attributes.RemoveAll();

                    if (!string.IsNullOrEmpty(href))
                    {
                        node.SetAttributeValue("href", href);
                        node.SetAttributeValue("target", "_blank");
                        node.SetAttributeValue("rel", "noopener noreferrer");
                    }
                }
            }

            // br 태그 제거
            var brs = tempDoc.DocumentNode.SelectNodes("//br");
            brs?.ToList().ForEach(br => br.Remove());

            return tempDoc.DocumentNode.InnerHtml.Trim();
        }

        // 이미지 파일 여부 확인 (NSFW 체크 대상 필터링용)
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif"
        };

        private bool IsImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (url.Contains("youtube", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                var uri = new Uri(url);
                var ext = Path.GetExtension(uri.GetLeftPart(UriPartial.Path))?.ToLower();
                return !string.IsNullOrEmpty(ext) ? ImageExtensions.Contains(ext) : true; // 확장자 없으면 이미지로 간주
            }
            catch
            {
                return false;
            }
        }

        // Cloudflare R2에 이미지/비디오 업로드
        protected async Task<string?> UploadToR2(List<string> imageSources, int orgNo)
        {
            var retUrl = string.Empty;
            if (imageSources != null && imageSources.Count() > 0)
            {
                // var results = await new ImageKitUploader().UploadMultipleSequential(imageSources);
                var results = await new CloudflareR2Uploader().UploadMultipleSequential(imageSources);
                foreach (var result in results)
                {
                    retUrl = result.UploadedUrl;
                    await new DatabaseManager().InsertOptimizedImagesAsync(
                    new OptimizedImageData
                    {
                        CloudinaryUrl = retUrl,
                        CloudinaryPublicId = "shooq_pub",
                        Title = string.Empty,
                        UploadedAt = DateTime.UtcNow,
                        IsActive = true,
                        No = orgNo
                    });

                    // NSFW 이미지 체크 (이미 성인 판정된 글은 스킵, 원본 바이트 직접 전달)
                    if (!_adultDetectedNos.Contains(orgNo) && result.Success && result.ImageBytes != null && IsImageUrl(retUrl))
                    {
                        try
                        {
                            var fileName = Path.GetFileName(new Uri(retUrl).GetLeftPart(UriPartial.Path));
                            var nsfwResult = await _nsfwService.CheckImageBytesAsync(result.ImageBytes, fileName);
                            if (nsfwResult.IsAdult)
                            {
                                _adultDetectedNos.Add(orgNo);
                                await _dbManager.UpdateAdultYn(orgNo, "Y");
                                Console.WriteLine($"  ⚠️ 성인 콘텐츠 감지 [no: {orgNo}, category: {nsfwResult.Category}, porn: {nsfwResult.PornScore:F3}, hentai: {nsfwResult.HentaiScore:F3}, sexy: {nsfwResult.SexyScore:F3}]");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  NSFW 체크 실패 [no: {orgNo}]: {ex.Message}");
                        }
                    }
                }
            }
            return retUrl;
        }

        // HTML 요소들을 최적화된 HTML로 변환 (이미지/비디오 R2 업로드 포함)
        protected async Task<(string, int)> CreateOptimizedHtml(HtmlNodeCollection elements, int postId)
        {
            int cnt = 0;
            var cleanContent = new StringBuilder();

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    // 1. 이미지 체크
                    var img = element.SelectSingleNode(".//img");

                    if (img != null)
                    {
                        var imgSrc = img.GetAttributeValue("src", "");
                        var imgAlt = img.GetAttributeValue("alt", "");

                        if (!string.IsNullOrEmpty(imgSrc))
                        {
                            // 'https:'가 없으면 추가
                            if (imgSrc.StartsWith("//"))
                                imgSrc = "https:" + imgSrc;
                            else if (!imgSrc.StartsWith("http"))
                                imgSrc = "https://" + imgSrc;
                        }

                        if (!string.IsNullOrEmpty(imgSrc) && IsValidUrl(imgSrc))
                        {
                            try
                            {
                                // R2에 이미지 업로드
                                var r2Url = await UploadToR2([imgSrc], postId);
                                cleanContent.AppendLine($"<img src=\"{r2Url}\" alt=\"{imgAlt}\" loading=\"lazy\">");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"이미지 업로드 실패: {imgSrc}, {ex.Message}");
                            }

                            cnt++;
                        }
                        continue;
                    }

                    // 2. 비디오 체크
                    var video = element.SelectSingleNode(".//video");

                    if (video != null)
                    {
                        var source = video.SelectSingleNode(".//source");
                        var videoSrc = source?.GetAttributeValue("src", "")
                                      ?? video.GetAttributeValue("src", "");

                        if (!string.IsNullOrEmpty(videoSrc) && IsValidUrl(videoSrc))
                        {
                            try
                            {
                                // R2에 비디오 업로드
                                var r2Url = await UploadToR2([videoSrc], postId);
                                cleanContent.AppendLine($"<video controls preload=\"metadata\" class=\"shooq-video\">");
                                cleanContent.AppendLine($"  <source src=\"{r2Url}\" type=\"video/mp4\">");
                                cleanContent.AppendLine($"</video>");

                                cnt++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"비디오 업로드 실패: {videoSrc}, {ex.Message}");
                            }
                        }
                        continue;
                    }

                    // 3. iframe (유튜브) 체크
                    var iframe = element.SelectSingleNode(".//iframe");

                    if (iframe != null)
                    {
                        var iframeSrc = iframe.GetAttributeValue("src", "");

                        if (IsYoutubeUrl(iframeSrc))
                        {
                            var videoId = ExtractYoutubeId(iframeSrc);
                            if (!string.IsNullOrEmpty(videoId))
                            {
                                var r2Url = await UploadToR2([$"https://www.youtube.com/embed/{videoId}"], postId); // ImageCarousel 용

                                cleanContent.AppendLine($"<iframe src=\"https://www.youtube.com/embed/{videoId}\" frameborder=\"0\" allowfullscreen class=\"shooq-youtube\"></iframe>");

                                cnt++;
                            }
                        }
                        continue;
                    }

                    // 4. 텍스트만 있는 경우
                    var text = element.InnerText.Trim();

                    // ArrayHtmlCodes의 항목들을 제거
                    foreach (var htmlCode in ArrayHtmlCodes)
                    {
                        text = text.Replace(htmlCode, "");
                    }

                    // ArrayUnnecessaryChars의 항목들을 제거
                    foreach (var unnecessaryChar in ArrayUnnecessaryChars)
                    {
                        text = text.Replace(unnecessaryChar, "");
                    }

                    // 빈 태그나 br만 있는 경우 스킵
                    if (!string.IsNullOrWhiteSpace(text) && text != "&nbsp;")
                    {
                        // a 태그가 있는지 체크
                        var links = element.SelectNodes(".//a");

                        if (links != null && links.Count > 0)
                        {
                            // 링크가 있는 경우 innerHTML 사용
                            var innerHTML = element.InnerHtml;
                            cleanContent.AppendLine($"<p>{CleanHtmlString(innerHTML)}</p>");
                        }
                        else
                        {
                            // 순수 텍스트
                            cleanContent.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>");
                        }
                    }
                }
            }

            return (cleanContent.ToString(), cnt);
        }

        protected async Task<(string, int)> ProcessChildNodes(HtmlNode parentNode, int postId)
        {
            int cnt = 0;
            var cleanContent = new StringBuilder();

            foreach (var node in parentNode.ChildNodes)
            {
                // 1. 텍스트 노드 처리
                if (node.NodeType == HtmlNodeType.Text)
                {
                    var text = node.InnerText;
                    if (!string.IsNullOrWhiteSpace(text) && text.Trim() != "&nbsp;")
                    {
                        // br 태그로 구분된 텍스트를 줄바꿈으로 처리
                        var lines = text.Split(new[] { "<br>", "<br/>", "<br />" }, StringSplitOptions.None);
                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmed))
                            {
                                cleanContent.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(trimmed)}</p>");
                            }
                        }
                    }
                    continue;
                }

                // 요소 노드가 아니면 스킵
                if (node.NodeType != HtmlNodeType.Element)
                    continue;

                // 2. iframe (유튜브) 처리
                if (node.Name == "iframe")
                {
                    var iframeSrc = node.GetAttributeValue("src", "");
                    if (IsYoutubeUrl(iframeSrc))
                    {
                        var videoId = ExtractYoutubeId(iframeSrc);
                        if (!string.IsNullOrEmpty(videoId))
                        {
                            var r2Url = await UploadToR2([$"https://www.youtube.com/embed/{videoId}"], postId); // ImageCarousel 용

                            cleanContent.AppendLine($"<iframe src=\"https://www.youtube.com/embed/{videoId}\" frameborder=\"0\" allowfullscreen class=\"shooq-youtube\"></iframe>");

                            cnt++;
                        }
                    }
                    continue;
                }

                // 3. img 태그 처리 (직계 자식)
                if (node.Name == "img")
                {
                    var imgSrc = node.GetAttributeValue("src", "");
                    var imgAlt = node.GetAttributeValue("alt", "");

                    if (!string.IsNullOrEmpty(imgSrc) && IsValidUrl(imgSrc))
                    {
                        try
                        {
                            var r2Url = await UploadToR2([imgSrc], postId);
                            cleanContent.AppendLine($"<img src=\"{r2Url}\" alt=\"{imgAlt}\" loading=\"lazy\">");
                            cnt++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"이미지 업로드 실패: {imgSrc}, {ex.Message}");
                        }
                    }
                    continue;
                }

                // 4. p 태그 처리
                if (node.Name == "p")
                {
                    // p 태그 내부에 img가 있는지 확인
                    var img = node.SelectSingleNode(".//img");
                    if (img != null)
                    {
                        var imgSrc = img.GetAttributeValue("src", "");
                        var imgAlt = img.GetAttributeValue("alt", "");

                        if (!string.IsNullOrEmpty(imgSrc) && IsValidUrl(imgSrc))
                        {
                            try
                            {
                                var r2Url = await UploadToR2([imgSrc], postId);
                                cleanContent.AppendLine($"<img src=\"{r2Url}\" alt=\"{imgAlt}\" loading=\"lazy\">");
                                cnt++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"이미지 업로드 실패: {imgSrc}, {ex.Message}");
                            }
                        }
                        continue;
                    }

                    // p 태그의 텍스트 내용
                    var text = node.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(text) && text != "&nbsp;")
                    {
                        cleanContent.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>");
                    }
                    continue;
                }

                // 5. div 태그는 스킵 (tool_cont 같은 불필요한 div 제외)
                if (node.Name == "div")
                {
                    continue;
                }
            }

            return (cleanContent.ToString(), cnt);
        }

        protected async Task<(string, int)> CreateOptimizedHtmlForFM(HtmlNodeCollection elements, int postId)
        {
            int cnt = 0;
            var cleanContent = new StringBuilder();

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    // 1. 비디오 체크 (이미지보다 먼저 - video와 썸네일이 같은 div에 있을 수 있음)
                    var video = element.SelectSingleNode(".//video");

                    if (video != null)
                    {
                        var source = video.SelectSingleNode(".//source");
                        var videoSrc = source?.GetAttributeValue("src", "")
                                      ?? video.GetAttributeValue("src", "");

                        if (!string.IsNullOrEmpty(videoSrc))
                        {
                            // 'https:'가 없으면 추가
                            if (videoSrc.StartsWith("//"))
                                videoSrc = "https:" + videoSrc;
                            else if (!videoSrc.StartsWith("http"))
                                videoSrc = "https://" + videoSrc;
                        }

                        if (!string.IsNullOrEmpty(videoSrc) && IsValidUrl(videoSrc))
                        {
                            try
                            {
                                // R2에 비디오 업로드
                                var r2Url = await UploadToR2([videoSrc], postId);
                                cleanContent.AppendLine($"<video controls preload=\"metadata\" class=\"shooq-video\">");
                                cleanContent.AppendLine($"  <source src=\"{r2Url}\" type=\"video/mp4\">");
                                cleanContent.AppendLine($"</video>");

                                cnt++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"비디오 업로드 실패: {videoSrc}, {ex.Message}");
                            }
                        }
                        continue;
                    }

                    // 2. iframe (유튜브) 체크
                    var iframe = element.SelectSingleNode(".//iframe");

                    if (iframe != null)
                    {
                        var iframeSrc = iframe.GetAttributeValue("src", "");

                        if (IsYoutubeUrl(iframeSrc))
                        {
                            var videoId = ExtractYoutubeId(iframeSrc);
                            if (!string.IsNullOrEmpty(videoId))
                            {
                                var r2Url = await UploadToR2([$"https://www.youtube.com/embed/{videoId}"], postId); // ImageCarousel 용

                                cleanContent.AppendLine($"<iframe src=\"https://www.youtube.com/embed/{videoId}\" frameborder=\"0\" allowfullscreen class=\"shooq-youtube\"></iframe>");

                                cnt++;
                            }
                        }

                        continue;
                    }

                    // 3. 이미지 체크
                    var img = element.SelectSingleNode(".//img");

                    if (img != null)
                    {
                        var imgSrc = img.GetAttributeValue("src", "");
                        var imgAlt = img.GetAttributeValue("alt", "");

                        if (!string.IsNullOrEmpty(imgSrc))
                        {
                            // 'https:'가 없으면 추가
                            if (imgSrc.StartsWith("//"))
                                imgSrc = "https:" + imgSrc;
                            else if (!imgSrc.StartsWith("http"))
                                imgSrc = "https://" + imgSrc;
                        }

                        if (!string.IsNullOrEmpty(imgSrc) && IsValidUrl(imgSrc))
                        {
                            try
                            {
                                // R2에 이미지 업로드
                                var r2Url = await UploadToR2([imgSrc], postId);
                                cleanContent.AppendLine($"<img src=\"{r2Url}\" alt=\"{imgAlt}\" loading=\"lazy\">");

                                cnt++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"이미지 업로드 실패: {imgSrc}, {ex.Message}");
                            }
                        }
                        continue;
                    }

                    // 4. 텍스트만 있는 경우
                    var text = element.InnerText.Trim();

                    // ArrayHtmlCodes의 항목들을 제거
                    foreach (var htmlCode in ArrayHtmlCodes)
                    {
                        text = text.Replace(htmlCode, "");
                    }

                    // ArrayUnnecessaryChars의 항목들을 제거
                    foreach (var unnecessaryChar in ArrayUnnecessaryChars)
                    {
                        text = text.Replace(unnecessaryChar, "");
                    }

                    // 빈 태그나 br만 있는 경우 스킵
                    if (!string.IsNullOrWhiteSpace(text) && text != "&nbsp;")
                    {
                        // a 태그가 있는지 체크
                        var links = element.SelectNodes(".//a");

                        if (links != null && links.Count > 0)
                        {
                            // 링크가 있는 경우 innerHTML 사용
                            var innerHTML = element.InnerHtml;
                            cleanContent.AppendLine($"<p>{CleanHtmlString(innerHTML)}</p>");
                        }
                        else
                        {
                            // 순수 텍스트
                            cleanContent.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>");
                        }
                    }
                }
            }

            return (cleanContent.ToString(), cnt);
        }

        // 유머대학 전용: HTML 요소들을 최적화된 HTML로 변환 (style 태그 제거, 다중 이미지 처리)
        protected async Task<(string, int)> CreateOptimizedHtmlForHumorUniv(HtmlNodeCollection elements, int postId)
        {
            int cnt = 0;
            var cleanContent = new StringBuilder();

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    // 0. style 태그 제거 (CSS 코드 제외)
                    var styleNodes = element.SelectNodes(".//style");
                    if (styleNodes != null)
                    {
                        foreach (var styleNode in styleNodes.ToList())
                        {
                            styleNode.Remove();
                        }
                    }

                    // 1. 이미지 체크 (여러 개의 이미지가 있을 수 있음)
                    var imgs = element.SelectNodes(".//img");

                    if (imgs != null && imgs.Count > 0)
                    {
                        foreach (var img in imgs)
                        {
                            var imgSrc = img.GetAttributeValue("src", "");
                            var imgAlt = img.GetAttributeValue("alt", "");

                            if (!string.IsNullOrEmpty(imgSrc))
                            {
                                // 'https:'가 없으면 추가
                                if (imgSrc.StartsWith("//"))
                                    imgSrc = "https:" + imgSrc;
                                else if (!imgSrc.StartsWith("http"))
                                    imgSrc = "https://" + imgSrc;
                            }

                            if (!string.IsNullOrEmpty(imgSrc) && IsValidUrl(imgSrc))
                            {
                                try
                                {
                                    // R2에 이미지 업로드
                                    var r2Url = await UploadToR2([imgSrc], postId);
                                    cleanContent.AppendLine($"<img src=\"{r2Url}\" alt=\"{imgAlt}\" loading=\"lazy\">");
                                    cnt++;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"이미지 업로드 실패: {imgSrc}, {ex.Message}");
                                }
                            }
                        }
                        continue;
                    }

                    // 2. 비디오 체크
                    var video = element.SelectSingleNode(".//video");

                    if (video != null)
                    {
                        var source = video.SelectSingleNode(".//source");
                        var videoSrc = source?.GetAttributeValue("src", "")
                                      ?? video.GetAttributeValue("src", "");

                        if (!string.IsNullOrEmpty(videoSrc))
                        {
                            // 'https:'가 없으면 추가
                            if (videoSrc.StartsWith("//"))
                                videoSrc = "https:" + videoSrc;
                            else if (!videoSrc.StartsWith("http"))
                                videoSrc = "https://" + videoSrc;
                        }

                        if (!string.IsNullOrEmpty(videoSrc) && IsValidUrl(videoSrc))
                        {
                            try
                            {
                                // R2에 비디오 업로드
                                var r2Url = await UploadToR2([videoSrc], postId);
                                cleanContent.AppendLine($"<video controls preload=\"metadata\" class=\"shooq-video\">");
                                cleanContent.AppendLine($"  <source src=\"{r2Url}\" type=\"video/mp4\">");
                                cleanContent.AppendLine($"</video>");
                                cnt++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"비디오 업로드 실패: {videoSrc}, {ex.Message}");
                            }
                        }
                        continue;
                    }

                    // 3. iframe (유튜브) 체크
                    var iframe = element.SelectSingleNode(".//iframe");

                    if (iframe != null)
                    {
                        var iframeSrc = iframe.GetAttributeValue("src", "");

                        if (IsYoutubeUrl(iframeSrc))
                        {
                            var videoId = ExtractYoutubeId(iframeSrc);
                            if (!string.IsNullOrEmpty(videoId))
                            {
                                var r2Url = await UploadToR2([$"https://www.youtube.com/embed/{videoId}"], postId); // ImageCarousel 용

                                cleanContent.AppendLine($"<iframe src=\"https://www.youtube.com/embed/{videoId}\" frameborder=\"0\" allowfullscreen class=\"shooq-youtube\"></iframe>");

                                cnt++;
                            }
                        }
                        continue;
                    }

                    // 4. 텍스트만 있는 경우
                    var text = element.InnerText.Trim();

                    // ArrayHtmlCodes의 항목들을 제거
                    foreach (var htmlCode in ArrayHtmlCodes)
                    {
                        text = text.Replace(htmlCode, "");
                    }

                    // ArrayUnnecessaryChars의 항목들을 제거
                    foreach (var unnecessaryChar in ArrayUnnecessaryChars)
                    {
                        text = text.Replace(unnecessaryChar, "");
                    }

                    // 빈 태그나 br만 있는 경우 스킵
                    if (!string.IsNullOrWhiteSpace(text) && text != "&nbsp;")
                    {
                        // a 태그가 있는지 체크
                        var links = element.SelectNodes(".//a");

                        if (links != null && links.Count > 0)
                        {
                            // 링크가 있는 경우 innerHTML 사용
                            var innerHTML = element.InnerHtml;
                            cleanContent.AppendLine($"<p>{CleanHtmlString(innerHTML)}</p>");
                        }
                        else
                        {
                            // 순수 텍스트
                            cleanContent.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>");
                        }
                    }
                }
            }

            return (cleanContent.ToString(), cnt);
        }

        protected async Task<(string, int)> CreateOptimizedHtmlForTheqoo(HtmlNode? contentDiv, int postId)
        {
            int cnt = 0;
            var cleanContent = new StringBuilder();

            if (contentDiv == null)
            {
                return (cleanContent.ToString(), cnt);
            }

            foreach (var node in contentDiv.ChildNodes)
            {
                // 텍스트 노드는 스킵
                if (node.NodeType != HtmlNodeType.Element)
                    continue;

                // 1. img 태그 처리 (직계 자식으로 있는 경우)
                if (node.Name == "img")
                {
                    var imgSrc = node.GetAttributeValue("src", "");
                    var imgAlt = node.GetAttributeValue("alt", "");

                    if (!string.IsNullOrEmpty(imgSrc))
                    {
                        // 'https:'가 없으면 추가
                        if (imgSrc.StartsWith("//"))
                            imgSrc = "https:" + imgSrc;
                        else if (!imgSrc.StartsWith("http"))
                            imgSrc = "https://" + imgSrc;
                    }

                    if (!string.IsNullOrEmpty(imgSrc) && IsValidUrl(imgSrc))
                    {
                        try
                        {
                            // R2에 이미지 업로드
                            var r2Url = await UploadToR2([imgSrc], postId);
                            cleanContent.AppendLine($"<img src=\"{r2Url}\" alt=\"{imgAlt}\" loading=\"lazy\">");
                            cnt++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"이미지 업로드 실패: {imgSrc}, {ex.Message}");
                        }
                    }
                    continue;
                }

                // 2. p 태그 처리
                if (node.Name == "p")
                {
                    // p 태그 내부에 여러 개의 img가 있을 수 있음
                    var imgs = node.SelectNodes(".//img");
                    if (imgs != null && imgs.Count > 0)
                    {
                        foreach (var img in imgs)
                        {
                            var imgSrc = img.GetAttributeValue("src", "");
                            var imgAlt = img.GetAttributeValue("alt", "");

                            if (!string.IsNullOrEmpty(imgSrc))
                            {
                                // 'https:'가 없으면 추가
                                if (imgSrc.StartsWith("//"))
                                    imgSrc = "https:" + imgSrc;
                                else if (!imgSrc.StartsWith("http"))
                                    imgSrc = "https://" + imgSrc;
                            }

                            if (!string.IsNullOrEmpty(imgSrc) && IsValidUrl(imgSrc))
                            {
                                try
                                {
                                    // R2에 이미지 업로드
                                    var r2Url = await UploadToR2([imgSrc], postId);
                                    cleanContent.AppendLine($"<img src=\"{r2Url}\" alt=\"{imgAlt}\" loading=\"lazy\">");
                                    cnt++;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"이미지 업로드 실패: {imgSrc}, {ex.Message}");
                                }
                            }
                        }
                        continue;
                    }

                    // p 태그 내부에 iframe (유튜브) 체크
                    var iframe = node.SelectSingleNode(".//iframe");
                    if (iframe != null)
                    {
                        var iframeSrc = iframe.GetAttributeValue("src", "");

                        if (IsYoutubeUrl(iframeSrc))
                        {
                            var videoId = ExtractYoutubeId(iframeSrc);
                            if (!string.IsNullOrEmpty(videoId))
                            {
                                var r2Url = await UploadToR2([$"https://www.youtube.com/embed/{videoId}"], postId); // ImageCarousel 용

                                cleanContent.AppendLine($"<iframe src=\"https://www.youtube.com/embed/{videoId}\" frameborder=\"0\" allowfullscreen class=\"shooq-youtube\"></iframe>");
                            }
                        }
                        continue;
                    }

                    // p 태그의 텍스트 내용
                    var text = node.InnerText.Trim();

                    // ArrayHtmlCodes의 항목들을 제거
                    foreach (var htmlCode in ArrayHtmlCodes)
                    {
                        text = text.Replace(htmlCode, "");
                    }

                    // ArrayUnnecessaryChars의 항목들을 제거
                    foreach (var unnecessaryChar in ArrayUnnecessaryChars)
                    {
                        text = text.Replace(unnecessaryChar, "");
                    }

                    // 빈 태그나 br만 있는 경우 스킵
                    if (!string.IsNullOrWhiteSpace(text) && text != "&nbsp;")
                    {
                        // a 태그가 있는지 체크
                        var links = node.SelectNodes(".//a");

                        if (links != null && links.Count > 0)
                        {
                            // 링크가 있는 경우 innerHTML 사용
                            var innerHTML = node.InnerHtml;
                            cleanContent.AppendLine($"<p>{CleanHtmlString(innerHTML)}</p>");
                        }
                        else
                        {
                            // 순수 텍스트
                            cleanContent.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>");
                        }
                    }
                    continue;
                }

                // 3. div 태그 처리 (oembedall-container 같은 유튜브 임베드)
                if (node.Name == "div")
                {
                    var iframe = node.SelectSingleNode(".//iframe");
                    if (iframe != null)
                    {
                        var iframeSrc = iframe.GetAttributeValue("src", "");

                        if (IsYoutubeUrl(iframeSrc))
                        {
                            var videoId = ExtractYoutubeId(iframeSrc);
                            if (!string.IsNullOrEmpty(videoId))
                            {
                                var r2Url = await UploadToR2([$"https://www.youtube.com/embed/{videoId}"], postId); // ImageCarousel 용

                                cleanContent.AppendLine($"<iframe src=\"https://www.youtube.com/embed/{videoId}\" frameborder=\"0\" allowfullscreen class=\"shooq-youtube\"></iframe>");
                            }
                        }
                    }
                    continue;
                }
            }

            return (cleanContent.ToString(), cnt);
        }
    }
}
