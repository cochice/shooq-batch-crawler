// 클라우디너리
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Marvin.Tmthfh91.Crawling.Model;
using Npgsql;
using Dapper;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    // 크롤링된 이미지 기본 정보 (이미 있는 데이터)
    public class CrawledImage
    {
        public string? ImageUrl { get; set; }
        public string? Title { get; set; }
        public string? SourceSite { get; set; }
    }

    // Cloudinary 업로드 서비스 (간소화)
    public class SimpleCloudinaryUploader
    {
        private readonly Cloudinary _cloudinary;

        public SimpleCloudinaryUploader()
        {
            var account = new Account(
                Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME"),
                Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY"),
                Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET")
            );
            _cloudinary = new Cloudinary(account);
        }

        // 단일 이미지 업로드 및 최적화
        public async Task<OptimizedImageData> UploadAndOptimize(CrawledImage crawledImage)
        {
            try
            {
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription($"https:{crawledImage.ImageUrl}"), // URL에서 직접 업로드
                    Folder = "shooq",                       // 폴더명
                    UseFilename = false,                    // 고유 파일명 생성
                    UniqueFilename = true,
                    Overwrite = false
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error != null)
                {
                    throw new Exception($"업로드 실패: {result.Error.Message}");
                }

                // 필수 정보만 반환
                return new OptimizedImageData
                {
                    CloudinaryUrl = result.SecureUrl.ToString(),
                    CloudinaryPublicId = result.PublicId,
                    Title = crawledImage.Title,
                    UploadedAt = DateTime.UtcNow,
                    IsActive = true
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"업로드 실패 - {crawledImage.ImageUrl}: {ex.Message}");
                return new OptimizedImageData();
            }
        }

        // 배치 업로드 (병렬 처리)
        public async Task<List<OptimizedImageData>> UploadBatch(List<CrawledImage> crawledImages)
        {
            var results = new List<OptimizedImageData>();

            // 10개씩 배치 처리
            const int batchSize = 10;
            for (int i = 0; i < crawledImages.Count; i += batchSize)
            {
                var batch = crawledImages.Skip(i).Take(batchSize);
                var batchTasks = batch.Select(UploadAndOptimize);
                var batchResults = await Task.WhenAll(batchTasks);

                // null 제거 (실패한 업로드)
                var successResults = batchResults.Where(r => r != null).ToList();
                results.AddRange(successResults);

                Console.WriteLine($"배치 {i / batchSize + 1} 완료: {successResults.Count}개 성공");

                // API 제한 고려 1초 대기
                if (i + batchSize < crawledImages.Count)
                {
                    await Task.Delay(1000);
                }
            }

            return results;
        }
    }

    // 간소화된 DB 저장 서비스
    public class ImageRepository
    {
        // Entity Framework 또는 Dapper 등을 사용한다고 가정

        // 단일 이미지 저장
        public static Task<int> SaveOptimizedImage(OptimizedImageData imageData)
        {
            // INSERT INTO OptimizedImages (CloudinaryUrl, CloudinaryPublicId, Title, UploadedAt, IsActive)
            // VALUES (@CloudinaryUrl, @CloudinaryPublicId, @Title, @UploadedAt, @IsActive)

            // 실제 DB 저장 로직
            // var savedId = await _dbContext.OptimizedImages.Add(imageData).SaveChanges();
            // return savedId;

            Console.WriteLine($"DB 저장: {imageData.Title} - {imageData.CloudinaryUrl}");
            return Task.FromResult(new Random().Next(1, 1000)); // 임시 ID
        }

        // 배치 저장 (성능 최적화)
        public async Task<List<int>> SaveBatch(List<OptimizedImageData> imageDataList)
        {
            var savedIds = new List<int>();

            // 배치 INSERT 사용 (더 빠름)
            foreach (var imageData in imageDataList)
            {
                try
                {
                    var savedId = await SaveOptimizedImage(imageData);
                    savedIds.Add(savedId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DB 저장 실패: {imageData.Title} - {ex.Message}");
                }
            }

            return savedIds;
        }

        // 이미지 불러오기 (페이징)
        public Task<List<OptimizedImageData>> GetImages(int page = 1, int pageSize = 20)
        {
            // SELECT TOP(@pageSize) * FROM OptimizedImages
            // WHERE IsActive = 1
            // ORDER BY UploadedAt DESC
            // OFFSET (@page - 1) * @pageSize ROWS

            return Task.FromResult(new List<OptimizedImageData>()); // 임시 반환
        }

        // 특정 이미지 삭제 (Cloudinary + DB)
        public Task<bool> DeleteImage(int imageId)
        {
            // 1. DB에서 PublicId 조회
            // 2. Cloudinary에서 삭제
            // 3. DB에서 IsActive = false 처리

            return Task.FromResult(true);
        }
    }

    // 메인 처리 서비스
    public class ImageProcessingService
    {
        private readonly SimpleCloudinaryUploader _uploader;
        private readonly ImageRepository _repository;

        public ImageProcessingService(SimpleCloudinaryUploader uploader, ImageRepository repository)
        {
            _uploader = uploader;
            _repository = repository;
        }

        // 전체 처리 워크플로우
        public async Task<int> ProcessCrawledImages(List<CrawledImage> crawledImages)
        {
            Console.WriteLine($"이미지 {crawledImages.Count}개 Cloudinary 업로드 시작...");

            // 1. Cloudinary 업로드
            var optimizedImages = await _uploader.UploadBatch(crawledImages);

            Console.WriteLine($"업로드 완료: {optimizedImages.Count}개 성공");

            // 2. DB 저장
            var resultId = await new DatabaseManager().InsertOptimizedImagesAndReturnIdAsync(optimizedImages[0]);

            Console.WriteLine($"DB 저장 완료 ID: [{resultId}]");

            return resultId;
        }
    }
}