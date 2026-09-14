namespace ArtistShop.Web.Database;

using System.Data;
using Dapper;

// builds a value for a stored procedure parameter declared as dbo.IdList READONLY
public static class IdListParameter
{
    // ICustomQueryParameter is Dapper's interface for "a parameter that knows how to add
    // itself to the command"; a table-valued parameter can't be passed as a plain value
    public static SqlMapper.ICustomQueryParameter Create(IEnumerable<int> ids)
    {
        var table = new DataTable();
        // the column name and type must match the Id column in dbo.IdList
        table.Columns.Add("Id", typeof(int));

        foreach (var id in ids)
        {
            table.Rows.Add(id);
        }

        return table.AsTableValuedParameter("dbo.IdList");
    }
}
