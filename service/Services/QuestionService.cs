﻿using infrastructure.QueryModels;
using infrastructure.Repositories;

namespace service;

public class QuestionService
{
    private readonly QuestionRepository _questionRepository;
    private readonly AnswerService _answerService;

    public QuestionService(QuestionRepository questionRepository, AnswerService answerService)
    {
        _questionRepository = questionRepository;
        _answerService = answerService;
        
    }

    public Question CreateQuestion(Question question)
    {
        return _questionRepository.CreateQuestion(question);
    }

    public bool DeleteQuestionsByQuizId(string quizId)
    {
        _answerService.DeleteAnswersByQuizId(quizId);
        return _questionRepository.DeleteQuestionsByQuizId(quizId);
    }
    public List<Question> GetQuestionsByQuizId(string id)
    {
        return _questionRepository.GetQuestionsByQuizId(id);
    }
}