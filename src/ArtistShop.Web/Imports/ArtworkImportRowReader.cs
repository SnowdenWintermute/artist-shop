using System.Globalization;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

// reads typed values from one row's cells; a bad cell adds an error and reads as null
public class ArtworkImportRowReader(CsvRow row)
{
    private const decimal CentimetersPerInch = 2.54m;

    // an inch value with 2 decimal places converts to exactly 4, which is what the columns hold
    private const int MaximumInchDecimalPlaces = 2;
    private const int MaximumCentimeterDecimalPlaces = 4;
    private const int MaximumPriceDecimalPlaces = 2;

    // spreadsheets often store an unknown year as 0
    private const string UnknownYear = "0";

    private static readonly decimal MinimumDimensionCm = ParseLimit(CatalogLimits.MinimumDimensionCm);
    private static readonly decimal MaximumDimensionCm = ParseLimit(CatalogLimits.MaximumDimensionCm);
    private static readonly decimal MinimumPrice = ParseLimit(CatalogLimits.MinimumPrice);
    private static readonly decimal MaximumPrice = ParseLimit(CatalogLimits.MaximumPrice);

    private readonly List<ArtworkImportError> _errors = [];

    public int RowNumber => row.RowNumber;

    public IReadOnlyList<ArtworkImportError> Errors => _errors;

    public void AddError(string? column, string message) =>
        _errors.Add(new ArtworkImportError(row.RowNumber, column, message));

    // null for a blank cell or a column the file doesn't have
    public string? Text(int? column) =>
        column is int index && row.Cells[index].Length > 0 ? row.Cells[index] : null;

    public IReadOnlyList<string> List(int? column, char separator) =>
        Text(column) is string text
            ? [.. text.Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(ArtworkImportNames.Comparer)]
            : [];

    public PartialDate? Date(int? column, string header)
    {
        if (Text(column) is not string text || text == UnknownYear)
        {
            return null;
        }

        if (PartialDate.TryParse(text, out var date))
        {
            return date;
        }

        AddError(header, $"\"{text}\" isn't a date. Use yyyy, yyyy-MM or yyyy-MM-dd, or 0 for unknown.");
        return null;
    }

    public decimal? LengthInCentimeters(int? column, string header, LengthUnit unit)
    {
        var maximumDecimalPlaces = unit is LengthUnit.Inches ? MaximumInchDecimalPlaces : MaximumCentimeterDecimalPlaces;

        if (Decimal(column, header, maximumDecimalPlaces) is not decimal length)
        {
            return null;
        }

        var centimeters = unit is LengthUnit.Inches ? length * CentimetersPerInch : length;

        if (centimeters < MinimumDimensionCm || centimeters > MaximumDimensionCm)
        {
            var value = unit is LengthUnit.Inches ? $"{Text(column)} inches ({centimeters} cm)" : $"{Text(column)} cm";
            AddError(header, $"{value} is outside {MinimumDimensionCm} to {MaximumDimensionCm} cm.");
            return null;
        }

        return centimeters;
    }

    public decimal? Price(int? column, string header)
    {
        if (Decimal(column, header, MaximumPriceDecimalPlaces) is not decimal price)
        {
            return null;
        }

        if (price < MinimumPrice || price > MaximumPrice)
        {
            AddError(header, $"{Text(column)} is outside {MinimumPrice} to {MaximumPrice}.");
            return null;
        }

        return price;
    }

    public int? WholeNumber(int? column, string header, int minimum)
    {
        if (Text(column) is not string text)
        {
            return null;
        }

        // NumberStyles.None: digits only, so no sign, spaces or thousands separators
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= minimum)
        {
            return number;
        }

        AddError(header, $"\"{text}\" isn't a whole number of {minimum} or more.");
        return null;
    }

    // blank reads as false, as spreadsheets leave an unticked box empty
    public bool Boolean(int? column, string header)
    {
        if (Text(column) is not string text)
        {
            return false;
        }

        if (bool.TryParse(text, out var value))
        {
            return value;
        }

        AddError(header, $"\"{text}\" isn't TRUE, FALSE or blank.");
        return false;
    }

    public TimeSpan? Duration(int? column, string header)
    {
        if (Text(column) is not string text)
        {
            return null;
        }

        if (ArtworkDuration.TryParse(text) is not TimeSpan duration)
        {
            AddError(header, $"\"{text}\" isn't a duration. Use {ArtworkDuration.ExpectedFormat}.");
            return null;
        }

        return duration;
    }

    private decimal? Decimal(int? column, string header, int maximumDecimalPlaces)
    {
        if (Text(column) is not string text)
        {
            return null;
        }

        // AllowDecimalPoint alone: no sign, currency symbol, thousands separator or exponent
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
        {
            AddError(header, $"\"{text}\" isn't a number. Use digits and a decimal point only.");
            return null;
        }

        var decimalPlaces = text.Contains('.') ? text[(text.IndexOf('.') + 1)..].TrimEnd('0').Length : 0;

        if (decimalPlaces > maximumDecimalPlaces)
        {
            AddError(header, $"\"{text}\" has more than {maximumDecimalPlaces} decimal places.");
            return null;
        }

        return number;
    }

    private static decimal ParseLimit(string limit) => decimal.Parse(limit, CultureInfo.InvariantCulture);
}
