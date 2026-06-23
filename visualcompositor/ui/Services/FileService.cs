using VisualCompositor.Core.Model;
using VisualCompositor.Core.Serialization;

namespace VisualCompositor.UI.Services;

/// <summary>Handles file IO for .osb and .storybrewcomp files.</summary>
public sealed class FileService
{
    public string ReadOsb(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    public CompositionDocument LoadStorybrewComp(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var result = StorybrewCompSerializer.Deserialize(json);
        return result.Document;
    }

    public void SaveStorybrewComp(CompositionDocument document, string filePath)
    {
        var json = StorybrewCompSerializer.Serialize(document);
        File.WriteAllText(filePath, json);
    }

    public void ExportOsb(string osbText, string filePath)
    {
        File.WriteAllText(filePath, osbText);
    }
}
