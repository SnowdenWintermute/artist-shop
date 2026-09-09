namespace ArtistShop.Web.Database;

using System.Data;
using Dapper;

public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    // Going out: SQL Server has no DateOnly, so send a DateTime at midnight and
    // tell the parameter it's DbType.Date, which drops the time component.
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    // Coming back: a `date` column arrives as DateTime; strip it back down.
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);
}
