using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Marvin.Tmthfh91.Crawling.Model;

namespace Marvin.Tmthfh91.Crawling
{
    public class CloudflareR2Uploader
    {
        private readonly string AccountId;
        private readonly string AccessKeyId;
        private readonly string SecretAccessKey;
        private readonly string BucketName;
        private readonly string PublicUrl;

        private readonly AmazonS3Client _s3Client;
        private readonly HttpClient _httpClient;

        public CloudflareR2Uploader()
        {
            EnvLoader.Load();

            AccountId = EnvLoader.Get("R2_ACCOUNT_ID");
            AccessKeyId = EnvLoader.Get("R2_ACCESS_KEY_ID");
            SecretAccessKey = EnvLoader.Get("R2_SECRET_ACCESS_KEY");
            BucketName = EnvLoader.Get("R2_BUCKET_NAME");
            PublicUrl = EnvLoader.Get("R2_PUBLIC_URL");

            var config = new AmazonS3Config
            {
                ServiceURL = $"https://{AccountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                SignatureVersion = "4",
                AuthenticationRegion = "auto",
                Timeout = TimeSpan.FromMinutes(5),
                MaxErrorRetry = 3,
                //DisablePayloadSigning = true  // ✨ 핵심 설정!
            };

            _s3Client = new AmazonS3Client(AccessKeyId, SecretAccessKey, config);
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        /// <summary>
        /// 단일 이미지 업로드 (R2 URL과 원본 바이트 반환)
        /// </summary>
        public async Task<(string Url, byte[] Bytes)> UploadFromUrl(string imageUrl, string? fileName = null)
        {
            try
            {
                var generatedFileName = GenerateFileName(imageUrl, fileName);

                // 1. URL에서 이미지 다운로드
                using var response = await _httpClient.GetAsync(imageUrl);
                response.EnsureSuccessStatusCode();

                // 2. 바이트 배열로 읽기 (스트리밍 문제 해결)
                byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

                using var memoryStream = new MemoryStream(imageBytes);
                var key = $"shooq/{generatedFileName}";

                // 3. R2에 업로드
                var putRequest = new PutObjectRequest
                {
                    BucketName = BucketName,
                    Key = key,
                    InputStream = memoryStream,
                    ContentType = GetContentType(generatedFileName),
                    UseChunkEncoding = false  // ✨ 청크 인코딩 비활성화
                };

                var putResponse = await _s3Client.PutObjectAsync(putRequest);

                // 4. URL + 원본 바이트 리턴
                return ($"{PublicUrl}/{key}", imageBytes);
            }
            catch (Exception ex)
            {
                throw new Exception($"R2 업로드 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 연결 테스트
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = BucketName,
                    MaxKeys = 1
                };

                await _s3Client.ListObjectsV2Async(request);
                Console.WriteLine($"✅ R2 연결 성공!");
                return true;
            }
            catch (AmazonS3Exception s3Ex)
            {
                Console.WriteLine($"❌ R2 연결 실패: {s3Ex.ErrorCode} - {s3Ex.Message}");
                return false;
            }
        }

        private string GetFileExtensionFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var pathWithoutQuery = uri.GetLeftPart(UriPartial.Path);
                var extension = Path.GetExtension(pathWithoutQuery)?.ToLower();

                var validExtensions = new[] {
                    ".jpg", ".jpeg", ".png", ".gif", ".webp",
                    ".bmp", ".svg", ".ico", ".tiff", ".tif",
                    ".mp4", ".webm", ".mov", ".avi", ".mkv", ".flv", ".wmv", ".m4v"
                };

                if (!string.IsNullOrEmpty(extension) && validExtensions.Contains(extension))
                {
                    return extension;
                }

                return ".jpg";
            }
            catch
            {
                return ".jpg";
            }
        }

        private string GenerateFileName(string originalUrl, string? customFileName = null, int? index = null)
        {
            if (!string.IsNullOrEmpty(customFileName))
            {
                return customFileName;
            }

            var extension = GetFileExtensionFromUrl(originalUrl);
            var timestamp = DateTime.Now.Ticks;

            if (index.HasValue)
            {
                return $"image_{index}_{timestamp}{extension}";
            }

            return $"image_{timestamp}{extension}";
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".tiff" or ".tif" => "image/tiff",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                ".mkv" => "video/x-matroska",
                ".flv" => "video/x-flv",
                ".wmv" => "video/x-ms-wmv",
                ".m4v" => "video/x-m4v",
                _ => "application/octet-stream"
            };
        }

        public async Task<List<ImageUploadResult>> UploadMultipleSequential(List<string> imageUrls)
        {
            var results = new List<ImageUploadResult>();

            for (int i = 0; i < imageUrls.Count; i++)
            {
                var imageUrl = imageUrls[i];

                try
                {
                    var fileName = GenerateFileName(imageUrl, index: i);
                    var uploadedUrl = string.Empty;

                    byte[]? imageBytes = null;

                    // youtube 정보가 포함 되어 있으면 그대로 저장
                    if (imageUrl.Contains("youtube", StringComparison.CurrentCultureIgnoreCase))
                    {
                        uploadedUrl = imageUrl;
                    }
                    // Claudflare R2 업로드
                    else
                    {
                        var (url, bytes) = await UploadFromUrl(imageUrl, fileName);
                        uploadedUrl = url;
                        imageBytes = bytes;
                    }

                    results.Add(new ImageUploadResult
                    {
                        OriginalUrl = imageUrl,
                        UploadedUrl = uploadedUrl,
                        Success = true,
                        Index = i,
                        ImageBytes = imageBytes
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new ImageUploadResult
                    {
                        OriginalUrl = imageUrl,
                        Success = false,
                        ErrorMessage = ex.Message,
                        Index = i
                    });
                }

                await Task.Delay(100);
            }

            return results;
        }

        public void Dispose()
        {
            _s3Client?.Dispose();
            _httpClient?.Dispose();
        }
    }
}