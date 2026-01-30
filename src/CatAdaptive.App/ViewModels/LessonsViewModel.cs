using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Application.Services;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.App.ViewModels;

public partial class LessonsViewModel : ObservableObject
{
    private readonly LearningFlowService _learningFlowService;
    private readonly ILessonPlanRepository _lessonPlanRepository;

    public ObservableCollection<LessonPlan> Lessons { get; } = new();

    public ObservableCollection<LessonQuizQuestionViewModel> QuizQuestions { get; } = new();

    [ObservableProperty]
    private LessonPlan? _selectedLesson;

    [ObservableProperty]
    private bool _isGeneratingLessons;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSubmittingQuiz;

    [ObservableProperty]
    private string? _statusMessage;

    public LessonsViewModel(LearningFlowService learningFlowService, ILessonPlanRepository lessonPlanRepository)
    {
        _learningFlowService = learningFlowService;
        _lessonPlanRepository = lessonPlanRepository;
    }

    [RelayCommand]
    public async Task LoadLessonsAsync()
    {
        IsLoading = true;
        try
        {
            Lessons.Clear();
            var lessons = await _learningFlowService.GetLessonsAsync();
            foreach (var lesson in lessons)
            {
                Lessons.Add(lesson);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenLesson(LessonPlan lesson)
    {
        SelectedLesson = lesson;
        QuizQuestions.Clear();
        StatusMessage = null;

        foreach (var question in lesson.Quiz.Questions)
        {
            QuizQuestions.Add(new LessonQuizQuestionViewModel(question));
        }
    }

    [RelayCommand]
    private void BackToList()
    {
        SelectedLesson = null;
        QuizQuestions.Clear();
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task SubmitQuizAsync()
    {
        if (SelectedLesson == null)
        {
            return;
        }

        if (QuizQuestions.Any(q => string.IsNullOrWhiteSpace(q.ResponseText)))
        {
            StatusMessage = "Please answer all quiz questions before submitting.";
            return;
        }

        IsSubmittingQuiz = true;
        IsGeneratingLessons = true;
        StatusMessage = null;

        try
        {
            var answers = QuizQuestions
                .Select(q => new LessonQuizAnswer(q.Question.Id, q.ResponseText.Trim()))
                .ToList();

            await _learningFlowService.SubmitQuizAsync(SelectedLesson.Id, answers);
            BackToList();
            await LoadLessonsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Quiz submission failed: {ex.Message}";
        }
        finally
        {
            IsSubmittingQuiz = false;
            IsGeneratingLessons = false;
        }
    }

    public async Task UpdateSectionProgressAsync(int sectionIndex, double readPercent)
    {
        if (SelectedLesson == null || sectionIndex < 0 || sectionIndex >= SelectedLesson.Sections.Count)
            return;

        var section = SelectedLesson.Sections[sectionIndex];
        var sectionProgress = SelectedLesson.SectionProgresses.FirstOrDefault(sp => sp.SectionId == section.Id);
        
        if (sectionProgress == null || readPercent > sectionProgress.ReadPercent)
        {
            var isRead = readPercent >= 90.0; // Consider section read if 90% scrolled
            await _lessonPlanRepository.UpdateSectionProgressAsync(
                SelectedLesson.Id, 
                section.Id, // Use the section's actual ID
                readPercent, 
                isRead);
            
            // Update the local lesson
            var updatedLesson = SelectedLesson.WithSectionProgress(section.Id, readPercent, isRead);
            SelectedLesson = updatedLesson;
        }
    }
}

public sealed partial class LessonQuizQuestionViewModel : ObservableObject
{
    public LessonQuizQuestion Question { get; }

    [ObservableProperty]
    private string _responseText = string.Empty;

    public string TypeDisplay => Question.Type switch
    {
        LessonQuizQuestionType.FillInBlank => "FILL IN THE BLANK",
        LessonQuizQuestionType.OpenResponse => "OPEN RESPONSE",
        _ => "QUESTION"
    };

    public LessonQuizQuestionViewModel(LessonQuizQuestion question)
    {
        Question = question;
    }
}
