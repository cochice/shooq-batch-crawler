// ============================================================
// NsfwService.cs - 핵심 서비스
// ============================================================

using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Marvin.Tmthfh91.Crawling.Model;
using NLog;

namespace Marvin.Tmthfh91.Crawling
{

    public class NsfwService
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly HttpClient _downloadClient; // 이미지 다운로드 전용 (자동 압축 해제)
        private readonly HttpClient _apiClient;      // NSFW API 호출 전용
        private readonly NsfwThresholds _thresholds;
        private readonly string _nsfwApiUrl;

        public NsfwService(HttpClient? httpClient = null)
        {
            // 이미지 다운로드용: gzip/deflate 자동 해제
            var downloadHandler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                       | System.Net.DecompressionMethods.Deflate
            };
            _downloadClient = new HttpClient(downloadHandler) { Timeout = TimeSpan.FromSeconds(30) };
            _apiClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            _nsfwApiUrl = Environment.GetEnvironmentVariable("NSFW_API_URL") ?? "http://127.0.0.1:3333";
            _thresholds = new NsfwThresholds
            {
                Porn = double.TryParse(Environment.GetEnvironmentVariable("NSFW_THRESHOLD_PORN"), out var p) ? p : 0.3,
                Hentai = double.TryParse(Environment.GetEnvironmentVariable("NSFW_THRESHOLD_HENTAI"), out var h) ? h : 0.3,
                Sexy = double.TryParse(Environment.GetEnvironmentVariable("NSFW_THRESHOLD_SEXY"), out var s) ? s : 0.5,
            };
        }

        /// <summary>
        /// URL에서 파일명 추출 (확장자 포함)
        /// </summary>
        private string GetFileName(string imageUrl)
        {
            try
            {
                var uri = new Uri(imageUrl);
                var path = uri.GetLeftPart(UriPartial.Path);
                var ext = Path.GetExtension(path)?.ToLower();
                var validExts = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif" };
                ext = validExts.Contains(ext ?? "") ? ext : ".jpg";
                return $"image{ext}";
            }
            catch { return "image.jpg"; }
        }

        /// <summary>
        /// Content-Type 결정
        /// </summary>
        private string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };
        }

        /// <summary>
        /// curl 호환 multipart/form-data 바디 생성 (따옴표 없는 boundary)
        /// </summary>
        private (byte[] Body, string Boundary) BuildMultipartBody(byte[] fileBytes, string fileName, string contentType)
        {
            var boundary = "----FormBoundary" + Guid.NewGuid().ToString("N");
            using var ms = new MemoryStream();
            var header = $"--{boundary}\r\nContent-Disposition: form-data; name=\"content\"; filename=\"{fileName}\"\r\nContent-Type: {contentType}\r\n\r\n";
            ms.Write(Encoding.UTF8.GetBytes(header));
            ms.Write(fileBytes);
            ms.Write(Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n"));
            return (ms.ToArray(), boundary);
        }

        /// <summary>
        /// 이미지 바이트 배열로 성인콘텐츠 여부 판별 (다운로드 불필요)
        /// </summary>
        public async Task<NsfwResult> CheckImageBytesAsync(byte[] imageBytes, string fileName = "image.jpg")
        {
            try
            {
                // MultipartFormDataContent는 boundary에 따옴표를 넣어 NSFWJS 서버와 비호환
                // → 수동으로 multipart body를 구성하여 curl과 동일하게 전송
                var (body, boundary) = BuildMultipartBody(imageBytes, fileName, GetContentType(fileName));
                using var rawContent = new ByteArrayContent(body);
                rawContent.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");

                var response = await _apiClient.PostAsync(
                    $"{_nsfwApiUrl}/single/multipart-form", rawContent);

                if (!response.IsSuccessStatusCode)
                {
                    logger.Error("NSFWJS API 오류: {StatusCode}", response.StatusCode);
                    return new NsfwResult { Error = $"API 오류: {response.StatusCode}" };
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var nsfwResponse = JsonSerializer.Deserialize<NsfwResponse>(responseBody, jsonOptions);
                if (nsfwResponse?.Prediction == null || !nsfwResponse.Prediction.Any())
                {
                    logger.Error("NSFWJS API 빈 응답 (body={Body})", responseBody);
                    return new NsfwResult { Error = "Empty prediction response" };
                }

                return Evaluate(nsfwResponse.Prediction);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "NSFW 바이트 검사 실패");
                return new NsfwResult { Error = ex.Message };
            }
        }

        /// <summary>
        /// 이미지 URL로 성인콘텐츠 여부 판별 (URL에서 다운로드 후 체크)
        /// </summary>
        public async Task<NsfwResult> CheckImageAsync(string imageUrl)
        {
            try
            {
                var imageBytes = await _downloadClient.GetByteArrayAsync(imageUrl);
                var fileName = GetFileName(imageUrl);
                return await CheckImageBytesAsync(imageBytes, fileName);
            }
            catch (HttpRequestException ex)
            {
                logger.Error(ex, "이미지 다운로드 실패: {Url}", imageUrl);
                return new NsfwResult { Error = $"다운로드 실패: {ex.Message}" };
            }
            catch (Exception ex)
            {
                logger.Error(ex, "NSFW 검사 실패: {Url}", imageUrl);
                return new NsfwResult { Error = ex.Message };
            }
        }

        /// <summary>
        /// 여러 이미지 중 하나라도 성인이면 true
        /// </summary>
        public async Task<NsfwResult> CheckImagesAsync(IEnumerable<string> imageUrls)
        {
            NsfwResult worstResult = new() { IsAdult = false, Category = "neutral" };

            foreach (var url in imageUrls)
            {
                var result = await CheckImageAsync(url);

                if (result.Error != null)
                {
                    logger.Error("이미지 검사 스킵: {Url} - {Error}", url, result.Error);
                    continue;
                }

                if (result.IsAdult)
                {
                    return result; // 하나라도 성인이면 즉시 반환
                }

                // 가장 높은 점수 기록
                if (result.PornScore > worstResult.PornScore)
                    worstResult = result;
            }

            return worstResult;
        }

        /// <summary>
        /// NSFWJS 결과를 임계값과 비교하여 성인 여부 판정
        /// </summary>
        private NsfwResult Evaluate(List<NsfwPrediction> predictions)
        {
            var scores = predictions.ToDictionary(p => p.ClassName, p => p.Probability);

            var result = new NsfwResult
            {
                PornScore = scores.GetValueOrDefault("Porn"),
                HentaiScore = scores.GetValueOrDefault("Hentai"),
                SexyScore = scores.GetValueOrDefault("Sexy"),
                NeutralScore = scores.GetValueOrDefault("Neutral"),
                DrawingScore = scores.GetValueOrDefault("Drawing"),
            };

            // 성인 판정
            result.IsAdult = result.PornScore >= _thresholds.Porn
                          || result.HentaiScore >= _thresholds.Hentai
                          || result.SexyScore >= _thresholds.Sexy;

            // 가장 높은 카테고리
            result.Category = predictions
                .OrderByDescending(p => p.Probability)
                .First().ClassName.ToLower();

            return result;
        }
    }
}