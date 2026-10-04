using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;
using ArtistShop.Web.Components.Pages.Admin.Catalog.Artworks;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworksFromImages;

public enum RowImageState : byte
{
    Waiting = 1,
    Added = 2,
    Failed = 3,
    // a later drop replaced the files the script held before this one was sent
    NotSent = 4,
}

public class RowImage(BulkImageCandidate file)
{
    public string FileId { get; } = file.Id;
    public string Path { get; } = file.Path;
    public RowImageState State { get; set; } = RowImageState.Waiting;
    public ArtworkImage? Image { get; set; }
    public string? Error { get; set; }
}

// One artwork already created from a drop, as the table edits it. Its fields are an ArtworkForm, so
// a row is checked by the same rules as the add and edit pages
public class ArtworkFromImagesRow(ArtworkId id, PlannedArtworkFromImages planned, IReadOnlyList<SeriesId> seriesIds)
{
    public ArtworkId Id { get; } = id;

    public ArtworkForm Form { get; } =
        new()
        {
            Name = planned.Name.Value,
            SeriesIds = [.. seriesIds.Select(seriesId => seriesId.Value)],
        };

    public IReadOnlyList<RowImage> Images { get; } = [.. planned.Images.Select(image => new RowImage(image))];

    // What was typed in the number fields. Text boxes rather than type="number", where Firefox takes
    // letters and reports them as an empty value, so a typo would vanish without a word
    public string? YearText
    {
        get;
        set
        {
            field = value;
            Form.YearCreated = ReadNumber<int>(value, nameof(ArtworkForm.YearCreated), NumberStyles.Integer, "Enter the year as a number.");
        }
    }

    public string? HeightText
    {
        get;
        set
        {
            field = value;
            Form.HeightCm = ReadNumber<decimal>(value, nameof(ArtworkForm.HeightCm), NumberStyles.Float, DecimalMessage);
        }
    }

    public string? WidthText
    {
        get;
        set
        {
            field = value;
            Form.WidthCm = ReadNumber<decimal>(value, nameof(ArtworkForm.WidthCm), NumberStyles.Float, DecimalMessage);
        }
    }

    public string? DepthText
    {
        get;
        set
        {
            field = value;
            Form.DepthCm = ReadNumber<decimal>(value, nameof(ArtworkForm.DepthCm), NumberStyles.Float, DecimalMessage);
        }
    }

    private const string DecimalMessage = "Enter a number, like 12.5.";

    // by form property name, text in a number field that isn't a number
    private readonly Dictionary<string, string> _unreadable = [];

    // by form property name, the first message for each
    public IReadOnlyDictionary<string, string> Errors { get; private set; } = new Dictionary<string, string>();

    // what the database said on the last save, such as the artwork having been deleted
    public string? SaveError { get; set; }

    // counts up with each save, so the thumbnail's saved mark plays again
    public int SaveCount { get; set; }

    public bool IsSaving { get; set; }

    // a field changed while a save was running, so another save follows it
    public bool SaveAgain { get; set; }

    public ArtworkImage? Thumbnail =>
        Images.Where(image => image.State is RowImageState.Added).Select(image => image.Image).FirstOrDefault();

    public string? ErrorFor(string propertyName) => Errors.GetValueOrDefault(propertyName);

    public bool Validate()
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(Form, new ValidationContext(Form), results, validateAllProperties: true);

        var errors = results
            .SelectMany(result => result.MemberNames.Select(member => (Member: member, Message: Unwrap.Value(result.ErrorMessage))))
            .GroupBy(error => error.Member)
            .ToDictionary(group => group.Key, group => group.First().Message);

        // the form sees an unreadable number as blank, so its own message for that field doesn't apply
        foreach (var (property, message) in _unreadable)
        {
            errors[property] = message;
        }

        Errors = errors;
        return Errors.Count is 0;
    }

    private T? ReadNumber<T>(string? text, string propertyName, NumberStyles styles, string message)
        where T : struct, INumberBase<T>
    {
        _unreadable.Remove(propertyName);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (T.TryParse(text, styles, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        _unreadable[propertyName] = message;
        return null;
    }
}
