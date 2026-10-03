namespace CommentService.Models;

public class Comment
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public record CreateCommentRequest(int ArticleId, string Author, string Text);
