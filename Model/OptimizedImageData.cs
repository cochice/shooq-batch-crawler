namespace Marvin.Tmthfh91.Crawling.Model
{
    public class OptimizedImageData
    {
        public int Id { get; set; }
        public string? CloudinaryUrl { get; set; }        // 최적화된 이미지 URL (필수)
        public string? CloudinaryPublicId { get; set; }   // Cloudinary 식별자 (삭제/수정용)
        public string? Title { get; set; }                // 제목
        public DateTime UploadedAt { get; set; }         // 업로드 시간
        public bool IsActive { get; set; } = true;       // 활성 상태
        public int No { get; set; }                      // 원글 글번호
    }

    // ========== 결과 모델 ==========
    public class ImageUploadResult
    {
        public string? OriginalUrl { get; set; }
        public string? UploadedUrl { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int Index { get; set; }
        public byte[]? ImageBytes { get; set; }  // NSFW 체크용 원본 바이트
    }

    public class UploadProgress
    {
        public int Completed { get; set; }
        public int Total { get; set; }
        public string? CurrentUrl { get; set; }
        public int Percentage { get; set; }
        public bool HasError { get; set; }
    }
}