// ============================================================
// NsfwModels.cs - NSFWJS 응답 모델
// ============================================================

using System.Text.Json.Serialization;

namespace Marvin.Tmthfh91.Crawling.Model
{
    public class NsfwResponse
    {
        [JsonPropertyName("prediction")]
        public List<NsfwPrediction> Prediction { get; set; } = new();
    }

    public class NsfwPrediction
    {
        [JsonPropertyName("className")]
        public string ClassName { get; set; } = string.Empty;

        [JsonPropertyName("probability")]
        public double Probability { get; set; }
    }

    public class NsfwResult
    {
        public bool IsAdult { get; set; }
        public double PornScore { get; set; }
        public double HentaiScore { get; set; }
        public double SexyScore { get; set; }
        public double NeutralScore { get; set; }
        public double DrawingScore { get; set; }
        public string Category { get; set; } = "neutral"; // 가장 높은 점수의 카테고리
        public string? Error { get; set; }
    }

    // ============================================================
    // NsfwThresholds.cs - 임계값 설정 (appsettings.json에서 관리)
    // ============================================================

    public class NsfwThresholds
    {
        public double Porn { get; set; } = 0.3;
        public double Hentai { get; set; } = 0.3;
        public double Sexy { get; set; } = 0.5;
    }
}