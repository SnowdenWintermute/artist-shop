namespace ArtistShop.Web.Database;

using System.Data;
using Dapper;

// Npgsql reads and writes DateOnly as a Postgres date by itself, but Dapper refuses a type it
// doesn't know unless a handler claims it, so this only hands the value through
public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value) =>
        parameter.Value = value;

    public override DateOnly Parse(object value) => (DateOnly)value;
}
