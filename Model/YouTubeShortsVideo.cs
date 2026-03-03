namespace Marvin.Tmthfh91.Crawling.Model
{
    public class YouTubeShort
    {
        public string? VideoId { get; set; }
        public string? Title { get; set; }
        public string? ChannelTitle { get; set; }
        public string? ThumbnailUrl { get; set; }
        public long ViewCount { get; set; }
        public long LikeCount { get; set; }
        public int DurationSeconds { get; set; }
        public DateTime PublishedAt { get; set; }
    }
}