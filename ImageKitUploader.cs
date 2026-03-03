using Imagekit;
using Imagekit.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Marvin.Tmthfh91.Crawling.Model;

namespace Marvin.Tmthfh91.Crawling
{
    public class ImageKitUploader
    {
        private readonly string PublicKey = "public_rrguSF75aq4TI9eFt32M2EJtySk=";
        private readonly string Privatekey = "private_6t7OVwbO8ydjlBI/Gf97OzYGzzI=";
        private readonly string UrlEndpoint = "https://ik.imagekit.io/cdco4lc8k/";

        private readonly ImagekitClient _imagekit;

        public ImageKitUploader()
        {
            _imagekit = new ImagekitClient(PublicKey, Privatekey, UrlEndpoint);
        }

        /// <summary>
        /// 단일 이미지 업로드
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public async Task<string> UploadFromUrl(string imageUrl, string? fileName = null)
        {
            try
            {
                var generatedFileName = GenerateFileName(imageUrl, fileName);

                var uploadRequest = new FileCreateRequest
                {
                    file = imageUrl,
                    fileName = generatedFileName,
                    useUniqueFileName = true,
                    folder = "/shooq/"
                };

                var result = await _imagekit.UploadAsync(uploadRequest);

                return result.url;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// ========== 확장자 추출 헬퍼 메서드 ==========
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string GetFileExtensionFromUrl(string url)
        {
            try
            {
                // URL에서 쿼리 스트링 제거
                var uri = new Uri(url);
                var pathWithoutQuery = uri.GetLeftPart(UriPartial.Path);

                // 확장자 추출
                var extension = Path.GetExtension(pathWithoutQuery)?.ToLower();

                // 유효한 이미지 확장자인지 확인
                var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".ico", ".tiff", ".tif" };

                if (!string.IsNullOrEmpty(extension) && validExtensions.Contains(extension))
                {
                    return extension;
                }

                // 확장자가 없거나 유효하지 않으면 기본값
                return ".jpg";
            }
            catch
            {
                return ".jpg"; // 파싱 실패 시 기본값
            }
        }

        /// <summary>
        /// ========== 파일명 생성 헬퍼 메서드 ==========
        /// </summary>
        /// <param name="originalUrl"></param>
        /// <param name="customFileName"></param>
        /// <param name="index"></param>
        /// <returns></returns>
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

        /// <summary>
        /// ========== 여러 이미지 업로드 (순차) ==========
        /// </summary>
        /// <param name="imageUrls"></param>
        /// <returns></returns> <summary>
        /// 이미지 업로드 결과
        /// </summary>
        /// <param name="imageUrls"></param>
        /// <returns></returns>
        public async Task<List<ImageUploadResult>> UploadMultipleSequential(List<string> imageUrls)
        {
            var results = new List<ImageUploadResult>();

            for (int i = 0; i < imageUrls.Count; i++)
            {
                var imageUrl = imageUrls[i];

                try
                {
                    var fileName = GenerateFileName(imageUrl, index: i);
                    var uploadedUrl = await UploadFromUrl(imageUrl, fileName);

                    results.Add(new ImageUploadResult
                    {
                        OriginalUrl = imageUrl,
                        UploadedUrl = uploadedUrl,
                        Success = true,
                        Index = i
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
    }
}