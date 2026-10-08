using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;
using ArtistShop.Web.Components.Pages.Admin.Catalog.Works;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.WorkTable;

public enum RowImageState : byte
{
    Waiting = 1,
    Added = 2,
    Failed = 3,
    // a later drop replaced the files the script held before this one was sent
    NotSent = 4,
}

// an image from a drop on its way to the work, or one it already has
public class RowImage
{
    public RowImage(BulkImageCandidate file)
    {
        FileId = file.Id;
        Path = file.Path;
    }

    // the storage key stands in for the browser's file id, as in SelectedImage.ForSavedImage
    private RowImage(WorkImage image)
    {
        FileId = image.StorageKey;
        Path = image.OriginalFileName ?? "Image";
        State = RowImageState.Added;
        Image = image;
    }

    public static RowImage Saved(WorkImage image) => new(image);

    public string FileId { get; }
    public string Path { get; }
    public RowImageState State { get; set; } = RowImageState.Waiting;
    public WorkImage? Image { get; set; }
    public string? Error { get; set; }
}

// One work as the table edits it. Its fields are a WorkForm, so a row is checked by the same
// rules as the add and edit pages
public class WorkTableRow
{
    private WorkTableRow(WorkId id, WorkForm form, List<RowImage> images)
    {
        Id = id;
        Form = form;
        Images = images;
        YearText = form.YearCreated?.ToString(CultureInfo.InvariantCulture);
        HeightText = CentimetresText(form.HeightCm);
        WidthText = CentimetresText(form.WidthCm);
        DepthText = CentimetresText(form.DepthCm);
    }

    // just created from a drop, with its images still to upload
    public static WorkTableRow FromDrop(WorkId id, PlannedWorkFromImages planned, IReadOnlyList<CollectionId> collectionIds) =>
        new(
            id,
            new WorkForm { Name = planned.Name.Value, CollectionIds = [.. collectionIds.Select(collectionId => collectionId.Value)] },
            [.. planned.Images.Select(image => new RowImage(image))]
        );

    public static WorkTableRow FromWork(Work work) =>
        new(work.Id, WorkForm.FromWork(work), [.. work.Images.Select(RowImage.Saved)])
        {
            MainImageKey = work.Images.Count > 0 ? work.Images[work.MainImageIndex].StorageKey : null,
        };

    // as the database keeps it, without the trailing zeros it reads back: 30 rather than 30.0000
    private static string? CentimetresText(decimal? centimetres) =>
        centimetres?.ToString("0.####", CultureInfo.InvariantCulture);

    public WorkId Id { get; }

    public WorkForm Form { get; }

    public IReadOnlyList<RowImage> Images { get; private set; }

    // the starred image, null for the first; uploads star the first image they add
    public string? MainImageKey { get; private set; }

    public bool HasImagesUploading => Images.Any(image => image.State is RowImageState.Waiting);

    public IReadOnlyList<WorkImage> SavedImages => [.. Images.Select(image => image.Image).OfType<WorkImage>()];

    // after the images dialog saved the whole list, so the ones that failed or weren't sent go: the
    // artist has re-added or left them out there
    public void ReplaceSavedImages(IReadOnlyList<WorkImage> images, string? mainImageKey)
    {
        Images = [.. images.Select(RowImage.Saved)];
        MainImageKey = mainImageKey;
    }

    // The number fields' text: a number as it will be saved, or what was typed when it isn't one. Text
    // boxes rather than type="number", where Firefox takes letters and reports them as an empty value,
    // so a typo would vanish without a word
    public string? YearText
    {
        get;
        set
        {
            Form.YearCreated = ReadNumber<int>(value, nameof(WorkForm.YearCreated), NumberStyles.Integer, "Enter the year as a number.");
            field = Form.YearCreated?.ToString(CultureInfo.InvariantCulture) ?? value;
        }
    }

    public string? HeightText
    {
        get;
        set
        {
            Form.HeightCm = ReadCentimetres(value, nameof(WorkForm.HeightCm));
            field = CentimetresText(Form.HeightCm) ?? value;
        }
    }

    public string? WidthText
    {
        get;
        set
        {
            Form.WidthCm = ReadCentimetres(value, nameof(WorkForm.WidthCm));
            field = CentimetresText(Form.WidthCm) ?? value;
        }
    }

    public string? DepthText
    {
        get;
        set
        {
            Form.DepthCm = ReadCentimetres(value, nameof(WorkForm.DepthCm));
            field = CentimetresText(Form.DepthCm) ?? value;
        }
    }

    private const string DecimalMessage = "Enter a number, like 12.5.";

    // rounded here as the database would, so the field shows what's saved
    private decimal? ReadCentimetres(string? text, string propertyName) =>
        ReadNumber<decimal>(text, propertyName, NumberStyles.Float, DecimalMessage) is { } centimetres
            ? Math.Round(centimetres, ArtistShopLimits.DimensionDecimalPlaces)
            : null;

    // by form property name, text in a number field that isn't a number
    private readonly Dictionary<string, string> _unreadable = [];

    // by form property name, the first message for each
    public IReadOnlyDictionary<string, string> Errors { get; private set; } = new Dictionary<string, string>();

    // what the database said on the last save, such as the work having been deleted
    public string? SaveError { get; set; }

    // the images dialog saves apart from the other fields, so its outcome can't clear their error
    public string? ImagesSaveError { get; set; }

    // counts up with each save, so the thumbnail's saved mark plays again
    public int SaveCount { get; set; }

    public bool IsSaving { get; set; }

    // checked for the table's bulk changes
    public bool IsSelected { get; set; }

    // a field changed while a save was running, so another save follows it
    public bool SaveAgain { get; set; }

    // nothing typed wrongly, saving or refused by the database
    public bool IsSaved => !IsSaving && Errors.Count is 0 && SaveError is null && ImagesSaveError is null;

    public WorkImage? Thumbnail =>
        SavedImages.FirstOrDefault(image => image.StorageKey == MainImageKey) ?? SavedImages.FirstOrDefault();

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
