namespace infrastructure.QueryModels;

public class QuestionWithAnswers {
    public Question Question { get; set; }
    public List<Answer> Answers { get; set; }
}