using System.Text;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;

namespace CatAdaptive.Infrastructure.Parsing;

public sealed class PptxParser : IPptxParser
{
    public Task<IReadOnlyList<KnowledgeUnit>> ParseAsync(string filePath, CancellationToken ct = default)
    {
        using var presentationDocument = PresentationDocument.Open(filePath, false);
        var units = ExtractKnowledgeUnits(presentationDocument);
        return Task.FromResult(units);
    }

    public Task<IReadOnlyList<KnowledgeUnit>> ParseAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var presentationDocument = PresentationDocument.Open(stream, false);
        var units = ExtractKnowledgeUnits(presentationDocument);
        return Task.FromResult(units);
    }

    private static IReadOnlyList<KnowledgeUnit> ExtractKnowledgeUnits(PresentationDocument document)
    {
        var units = new List<KnowledgeUnit>();
        var presentationPart = document.PresentationPart;
        if (presentationPart?.Presentation?.SlideIdList == null)
        {
            return units;
        }

        var globalLearningObjectives = new List<string>();
        var slideIndex = 0;
        
        foreach (var slideId in presentationPart.Presentation.SlideIdList.Elements<SlideId>())
        {
            slideIndex++;
            var relationshipId = slideId.RelationshipId;
            if (relationshipId == null) continue;

            var slidePart = (SlidePart)presentationPart.GetPartById(relationshipId!);
            var slideContent = ExtractSlideContent(slidePart);

            // Check if this is a learning objectives slide
            if (IsLearningObjectivesSlide(slideContent.Title))
            {
                globalLearningObjectives.AddRange(slideContent.KeyPoints);
                continue; // Don't create a separate unit for objectives slide
            }

            if (!string.IsNullOrWhiteSpace(slideContent.Title) || slideContent.KeyPoints.Count > 0)
            {
                var unit = KnowledgeUnit.Create(
                    topic: slideContent.Title ?? $"Slide {slideIndex}",
                    subtopic: string.Empty,
                    sourceSlideId: $"slide-{slideIndex}",
                    summary: slideContent.Title ?? string.Empty,
                    keyPoints: slideContent.KeyPoints,
                    learningObjectives: globalLearningObjectives);

                units.Add(unit);
            }
        }

        return units;
    }

    private static bool IsLearningObjectivesSlide(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        
        var lowerTitle = title.ToLowerInvariant();
        return lowerTitle.Contains("learning objective") ||
               lowerTitle.Contains("objectives") ||
               lowerTitle.Contains("learning outcome") ||
               lowerTitle.Contains("goals");
    }

    private static SlideContent ExtractSlideContent(SlidePart slidePart)
    {
        var title = string.Empty;
        var keyPoints = new List<string>();

        if (slidePart.Slide?.CommonSlideData?.ShapeTree == null)
        {
            return new SlideContent(title, keyPoints);
        }

        foreach (var shape in slidePart.Slide.CommonSlideData.ShapeTree.Elements<Shape>())
        {
            var textBody = shape.TextBody;
            if (textBody == null) continue;

            var isTitle = IsShapeTitle(shape);
            var text = ExtractTextFromTextBody(textBody);

            if (isTitle && string.IsNullOrWhiteSpace(title))
            {
                var trimmed = text.Trim();
                // Filter out generic slide titles
                if (!IsNoise(trimmed))
                {
                    title = trimmed;
                }
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed) && !IsNoise(trimmed))
                    {
                        keyPoints.Add(trimmed);
                    }
                }
            }
        }

        return new SlideContent(title, keyPoints);
    }

    private static bool IsNoise(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        
        // Filter common contact/meta noise - Relaxed to avoid over-filtering content
        var noisePatterns = new[]
        {
            @"(\d{3}-\d{3}-\d{4})", // Phone
            @"Room\s+#?\d+",
            @"@uams.edu",
            @"www\.",
            @"http"
        };

        foreach (var pattern in noisePatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
        }

        // Filter extremely short non-content lines
        if (text.Length < 2) return true;

        return false;
    }

    private static bool IsShapeTitle(Shape shape)
    {
        var nvSpPr = shape.NonVisualShapeProperties;
        var nvPr = nvSpPr?.ApplicationNonVisualDrawingProperties;
        if (nvPr?.PlaceholderShape?.Type != null)
        {
            var phType = nvPr.PlaceholderShape.Type.Value;
            return phType == PlaceholderValues.Title || phType == PlaceholderValues.CenteredTitle;
        }
        return false;
    }

    private static string ExtractTextFromTextBody(TextBody textBody)
    {
        var sb = new StringBuilder();
        foreach (var paragraph in textBody.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>())
        {
            foreach (var run in paragraph.Elements<DocumentFormat.OpenXml.Drawing.Run>())
            {
                var text = run.Text?.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    sb.Append(text);
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private sealed record SlideContent(string? Title, List<string> KeyPoints);
}
